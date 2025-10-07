using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;

namespace DMSG3.Worker;

public class Worker : BackgroundService
{
    private readonly ILogger<Worker> _log;
    private readonly RabbitMqOptions _opt;
    private IConnection? _conn;
    private IModel? _ch;

    public Worker(ILogger<Worker> log, IOptions<RabbitMqOptions> opt)
    {
        _log = log;
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
                {
                    _log.LogWarning("RabbitMQ-Verbindung beendet: {ReplyText} (Code {ReplyCode})", ea.ReplyText, ea.ReplyCode);
                };

                _ch = _conn.CreateModel();

                _ch.ExchangeDeclare(_opt.Exchange, ExchangeType.Topic, durable: true);

                _ch.QueueDeclare(_opt.OcrQueue, durable: true, exclusive: false, autoDelete: false);
                _ch.QueueBind(_opt.OcrQueue, _opt.Exchange, routingKey: "ocr.request");

                _ch.QueueDeclare(_opt.ResultQueue, durable: true, exclusive: false, autoDelete: false);
                _ch.QueueBind(_opt.ResultQueue, _opt.Exchange, routingKey: "ocr.result");

                _ch.BasicQos(0, 1, false);

                var consumer = new AsyncEventingBasicConsumer(_ch);
                consumer.Received += async (_, ea) =>
                {
                    try
                    {
                        var json = Encoding.UTF8.GetString(ea.Body.ToArray());
                        _log.LogInformation("OCR_REQUEST empfangen: {Json}", json);

                        // FAKE OCR:
                        await Task.Delay(500, stoppingToken);

                        var req = JsonSerializer.Deserialize<Dictionary<string, object>>(json);
                        var result = new
                        {
                            id = req?["id"],
                            ocrTextPreview = "!!!!! Es wurde nichts ausgelesen. Noch. !!!!!",
                            processedAtUtc = DateTime.UtcNow
                        };

                        var props = _ch!.CreateBasicProperties();
                        props.Persistent = true;
                        var body = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(result));

                        _ch.BasicPublish(_opt.Exchange, "ocr.result", props, body);
                        _log.LogInformation("OCR_RESULT gepublished: {Json}", JsonSerializer.Serialize(result));

                        _ch.BasicAck(ea.DeliveryTag, false);
                    }
                    catch (Exception ex)
                    {
                        _log.LogError(ex, "Fehler bei OCR-Verarbeitung");
                        try { _ch!.BasicNack(ea.DeliveryTag, false, requeue: true); } catch { }
                    }
                    await Task.CompletedTask;
                };

                _ch.BasicConsume(_opt.OcrQueue, autoAck: false, consumer);

                _log.LogInformation("Mit RabbitMQ verbunden und Consumer gestartet (Versuch {Attempt}).", attempt);
                return; // Schleife wird verlassen, wenn Connection endlich geht (Um Logspam zu vermeiden).
            }
            catch (RabbitMQ.Client.Exceptions.BrokerUnreachableException ex)
            {
                _log.LogWarning(ex, "RabbitMQ noch nicht erreichbar (Versuch {Attempt}).", attempt);
            }
            catch (SocketException ex)
            {
                _log.LogWarning(ex, "Socket-Fehler zu RabbitMQ (Versuch {Attempt}).", attempt);
            }
            catch (Exception ex)
            {
                _log.LogError(ex, "Fehler beim RabbitMQ-Setup (Versuch {Attempt}).", attempt);
            }

            var delaySecs = 2;
            try { await Task.Delay(TimeSpan.FromSeconds(delaySecs), stoppingToken); } catch { }
        }

        stoppingToken.ThrowIfCancellationRequested();
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