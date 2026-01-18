using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using DMSG3.Domain.Messaging;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace DMSG3.Worker.Indexing;

public class IndexingWorker : BackgroundService
{
    private readonly ILogger<IndexingWorker> _log;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly RabbitMqOptions _options;
    private readonly JsonSerializerOptions _serializerOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private IConnection? _connection;
    private IModel? _channel;

    public IndexingWorker(ILogger<IndexingWorker> log, IServiceScopeFactory scopeFactory, IOptions<RabbitMqOptions> options)
    {
        _log = log;
        _scopeFactory = scopeFactory;
        _options = options.Value;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (string.IsNullOrWhiteSpace(_options.IndexQueue))
        {
            _log.LogWarning("IndexQueue ist nicht konfiguriert. IndexingWorker wird nicht gestartet.");
            return;
        }

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
                _channel.QueueDeclare(_options.IndexQueue, durable: true, exclusive: false, autoDelete: false);
                _channel.QueueBind(_options.IndexQueue, _options.Exchange, routingKey: "index.request");
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
                    catch (Exception ex)
                    {
                        _log.LogError(ex, "Fehler beim Indexing, Nachricht wird erneut zugestellt.");
                        _channel.BasicNack(ea.DeliveryTag, false, requeue: true);
                    }
                };

                _channel.BasicConsume(_options.IndexQueue, autoAck: false, consumer);
                _log.LogInformation("Indexing-Consumer gestartet (Versuch {Attempt}).", attempt);
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
        var message = JsonSerializer.Deserialize<IndexRequestMessage>(json, _serializerOptions);
        if (message is null)
        {
            _log.LogWarning("Ungültige Index-Nachricht ignoriert: {Payload}", json);
            return;
        }

        using var scope = _scopeFactory.CreateScope();
        var handler = scope.ServiceProvider.GetRequiredService<DocumentIndexingHandler>();
        _log.LogInformation("INDEX_REQUEST erhalten. DocumentId={DocumentId}", message.DocumentId);
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
