using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using DMSG3.Domain.Entities;
using DMSG3.Domain.Messaging;
using DMSG3.Worker.Ocr;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace DMSG3.Worker;

public class Worker : BackgroundService
{
    private readonly ILogger<Worker> _log;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly RabbitMqOptions _opt;
    private readonly JsonSerializerOptions _serializerOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private IConnection? _conn;
    private IModel? _ch;

    public Worker(ILogger<Worker> log, IServiceScopeFactory scopeFactory, IOptions<RabbitMqOptions> opt)
    {
        _log = log;
        _scopeFactory = scopeFactory;
        _opt = opt.Value;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await ConnectWithRetryAndStartConsumerAsync(stoppingToken);

        var tcs = new TaskCompletionSource();
        stoppingToken.Register(() => tcs.TrySetResult());
        await tcs.Task;
    }

    private async Task ConnectWithRetryAndStartConsumerAsync(CancellationToken stoppingToken)
    {
        var factory = new ConnectionFactory
        {
            HostName = _opt.HostName,
            Port = _opt.Port,
            UserName = _opt.UserName,
            Password = _opt.Password,
            DispatchConsumersAsync = true,
            AutomaticRecoveryEnabled = true
        };

        var attempt = 0;
        while (!stoppingToken.IsCancellationRequested)
        {
            attempt++;
            try
            {
                _conn = factory.CreateConnection();
                _conn.ConnectionShutdown += (_, ea) =>
                    _log.LogWarning("RabbitMQ-Verbindung beendet. {ReplyText} (Code {ReplyCode})", ea.ReplyText, ea.ReplyCode);

                _ch = _conn.CreateModel();
                _ch.ExchangeDeclare(_opt.Exchange, ExchangeType.Topic, durable: true);

                _ch.QueueDeclare(_opt.OcrQueue, durable: true, exclusive: false, autoDelete: false);
                _ch.QueueBind(_opt.OcrQueue, _opt.Exchange, routingKey: "ocr.request");

                _ch.QueueDeclare(_opt.ResultQueue, durable: true, exclusive: false, autoDelete: false);
                _ch.QueueBind(_opt.ResultQueue, _opt.Exchange, routingKey: "ocr.result");

                if (!string.IsNullOrWhiteSpace(_opt.SummaryQueue))
                {
                    _ch.QueueDeclare(_opt.SummaryQueue, durable: true, exclusive: false, autoDelete: false);
                    _ch.QueueBind(_opt.SummaryQueue, _opt.Exchange, routingKey: "summary.request");
                }

                _ch.BasicQos(0, 1, false);

                var consumer = new AsyncEventingBasicConsumer(_ch);
                consumer.Received += async (_, ea) =>
                {
                    var body = ea.Body.ToArray();
                    try
                    {
                        await ProcessMessageAsync(body, stoppingToken);
                        _ch.BasicAck(ea.DeliveryTag, false);
                    }
                    catch (Exception ex)
                    {
                        _log.LogError(ex, "Fehler bei der OCR-Verarbeitung");
                        try { _ch.BasicNack(ea.DeliveryTag, false, requeue: true); } catch { }
                    }
                };

                _ch.BasicConsume(_opt.OcrQueue, autoAck: false, consumer);
                _log.LogInformation("Mit RabbitMQ verbunden und Consumer gestartet (Versuch Nummer {Attempt}).", attempt);
                return;
            }
            catch (RabbitMQ.Client.Exceptions.BrokerUnreachableException ex)
            {
                _log.LogWarning(ex, "RabbitMQ noch nicht erreichbar (Versuch Nummer {Attempt}).", attempt);
            }
            catch (SocketException ex)
            {
                _log.LogWarning(ex, "Socket-Fehler zu RabbitMQ (Versuch Nummer {Attempt}).", attempt);
            }
            catch (Exception ex)
            {
                _log.LogError(ex, "Fehler beim RabbitMQ-Setup (Versuch Nummer {Attempt}).", attempt);
            }

            try { await Task.Delay(TimeSpan.FromSeconds(2), stoppingToken); } catch { }
        }

        stoppingToken.ThrowIfCancellationRequested();
    }

    private async Task ProcessMessageAsync(byte[] body, CancellationToken ct)
    {
        var json = Encoding.UTF8.GetString(body);
        var message = JsonSerializer.Deserialize<OcrRequestMessage>(json, _serializerOptions);
        if (message is null)
        {
            _log.LogWarning("Ungültige OCR-Nachricht ignoriert: {Payload}", json);
            return;
        }

        using var scope = _scopeFactory.CreateScope();
        var handler = scope.ServiceProvider.GetRequiredService<OcrRequestHandler>();
        _log.LogInformation("OCR_REQUEST empfangen. Dokument={DocumentId}", message.DocumentId);
        var result = await handler.ProcessAsync(message, ct);
        if (result != null)
        {
            PublishResult(result);
            if (string.Equals(result.Status, DocumentOcrStatus.Completed, StringComparison.OrdinalIgnoreCase))
            {
                PublishSummaryRequest(result.DocumentId);
            }
        }
    }

    private void PublishResult(OcrProcessingResult result)
    {
        if (_ch is null) return;

        var payload = new
        {
            id = result.DocumentId,
            status = result.Status,
            preview = result.TextPreview,
            completedAtUtc = result.CompletedAt ?? DateTime.UtcNow
        };

        var props = _ch.CreateBasicProperties();
        props.Persistent = true;
        var bytes = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(payload, _serializerOptions));
        _ch.BasicPublish(_opt.Exchange, "ocr.result", props, bytes);
        _log.LogInformation("OCR_RESULT gesendet. Dokument={DocumentId} Status={Status}", result.DocumentId, result.Status);
    }

    private void PublishSummaryRequest(Guid documentId)
    {
        if (_ch is null) return;
        if (string.IsNullOrWhiteSpace(_opt.SummaryQueue))
        {
            _log.LogWarning("SummaryQueue ist nicht konfiguriert. DocumentId={DocumentId}", documentId);
            return;
        }

        var message = new SummaryRequestMessage(documentId);
        var props = _ch.CreateBasicProperties();
        props.Persistent = true;
        var bytes = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(message, _serializerOptions));
        _ch.BasicPublish(_opt.Exchange, "summary.request", props, bytes);
        _log.LogInformation("SUMMARY_REQUEST gesendet. Dokument={DocumentId}", documentId);
    }

    public override void Dispose()
    {
        try { _ch?.Close(); } catch { }
        try { _conn?.Close(); } catch { }
        _ch?.Dispose();
        _conn?.Dispose();
        base.Dispose();
    }
}