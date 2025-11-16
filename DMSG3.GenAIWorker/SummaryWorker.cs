using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using DMSG3.Domain.Messaging;
using DMSG3.GenAIWorker.Summaries;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace DMSG3.GenAIWorker;

public class SummaryWorker : BackgroundService
{
    private readonly ILogger<SummaryWorker> _log;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly RabbitMqOptions _options;
    private readonly JsonSerializerOptions _serializerOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private IConnection? _connection;
    private IModel? _channel;

    public SummaryWorker(ILogger<SummaryWorker> log, IServiceScopeFactory scopeFactory, IOptions<RabbitMqOptions> options)
    {
        _log = log;
        _scopeFactory = scopeFactory;
        _options = options.Value;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await ConnectWithRetryAsync(stoppingToken);
        var tcs = new TaskCompletionSource();
        stoppingToken.Register(() => tcs.TrySetResult());
        await tcs.Task;
    }

    private async Task ConnectWithRetryAsync(CancellationToken stoppingToken)
    {
        var factory = new ConnectionFactory
        {
            HostName = _options.HostName,
            Port = _options.Port,
            UserName = _options.UserName,
            Password = _options.Password,
            DispatchConsumersAsync = true,
            AutomaticRecoveryEnabled = true
        };

        var attempt = 0;
        while (!stoppingToken.IsCancellationRequested)
        {
            attempt++;
            try
            {
                _connection = factory.CreateConnection();
                _connection.ConnectionShutdown += (_, ea) =>
                    _log.LogWarning("RabbitMQ-Verbindung beendet. ReplyText={ReplyText} Code={ReplyCode}", ea.ReplyText, ea.ReplyCode);

                _channel = _connection.CreateModel();
                _channel.ExchangeDeclare(_options.Exchange, ExchangeType.Topic, durable: true);
                _channel.QueueDeclare(_options.SummaryQueue, durable: true, exclusive: false, autoDelete: false);
                _channel.QueueBind(_options.SummaryQueue, _options.Exchange, routingKey: "summary.request");
                _channel.BasicQos(0, 1, false);

                var consumer = new AsyncEventingBasicConsumer(_channel);
                consumer.Received += async (_, ea) =>
                {
                    var body = ea.Body.ToArray();
                    try
                    {
                        await HandleMessageAsync(body, stoppingToken);
                        _channel.BasicAck(ea.DeliveryTag, false);
                    }
                    catch (SummaryTransientException ex)
                    {
                        _log.LogWarning(ex, "Zusammenfassung wird erneut versucht.");
                        var delay = ex.Delay ?? TimeSpan.FromSeconds(5);
                        try { await Task.Delay(delay, stoppingToken); } catch { }
                        _channel.BasicNack(ea.DeliveryTag, false, requeue: true);
                    }
                    catch (Exception ex)
                    {
                        _log.LogError(ex, "Fehler beim Zusammenfassen, Nachricht wird erneut zugestellt.");
                        _channel.BasicNack(ea.DeliveryTag, false, requeue: true);
                    }
                };

                _channel.BasicConsume(_options.SummaryQueue, autoAck: false, consumer);
                _log.LogInformation("GenAI-Consumer gestartet (Versuch {Attempt}).", attempt);
                return;
            }
            catch (RabbitMQ.Client.Exceptions.BrokerUnreachableException ex)
            {
                _log.LogWarning(ex, "RabbitMQ nicht erreichbar (Versuch {Attempt}).", attempt);
            }
            catch (SocketException ex)
            {
                _log.LogWarning(ex, "Socket-Fehler zu RabbitMQ (Versuch {Attempt}).", attempt);
            }
            catch (Exception ex)
            {
                _log.LogError(ex, "Fehler beim Aufbau der RabbitMQ-Verbindung (Versuch {Attempt}).", attempt);
            }

            try { await Task.Delay(TimeSpan.FromSeconds(2), stoppingToken); } catch { }
        }
    }

    private async Task HandleMessageAsync(byte[] body, CancellationToken ct)
    {
        var json = Encoding.UTF8.GetString(body);
        var message = JsonSerializer.Deserialize<SummaryRequestMessage>(json, _serializerOptions);
        if (message is null)
        {
            _log.LogWarning("Ungültige Summary-Nachricht ignoriert: {Payload}", json);
            return;
        }

        using var scope = _scopeFactory.CreateScope();
        var handler = scope.ServiceProvider.GetRequiredService<DocumentSummaryHandler>();
        _log.LogInformation("SUMMARY_REQUEST erhalten. DocumentId={DocumentId}", message.DocumentId);
        await handler.ProcessAsync(message, ct);
    }

    public override void Dispose()
    {
        try { _channel?.Close(); } catch { }
        try { _connection?.Close(); } catch { }
        _channel?.Dispose();
        _connection?.Dispose();
        base.Dispose();
    }
}