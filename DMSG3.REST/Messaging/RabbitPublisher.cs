using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using System.Text;
using System.Text.Json;

namespace DMSG3.REST.Messaging;

public class RabbitPublisher : IRabbitPublisher, IDisposable
{
    private readonly IConnection _conn;
    private readonly IModel _ch;
    private readonly RabbitMqOptions _opt;
    private readonly ILogger<RabbitPublisher> _log;

    public RabbitPublisher(IOptions<RabbitMqOptions> options, ILogger<RabbitPublisher> log)
    {
        _opt = options.Value;
        _log = log;

        var factory = new ConnectionFactory
        {
            HostName = _opt.HostName,
            Port = _opt.Port,
            UserName = _opt.UserName,
            Password = _opt.Password,
            DispatchConsumersAsync = true,
            AutomaticRecoveryEnabled = true
        };

        _conn = factory.CreateConnection();
        _ch = _conn.CreateModel();

        _ch.ExchangeDeclare(_opt.Exchange, type: ExchangeType.Topic, durable: true, autoDelete: false);
        _ch.ConfirmSelect();

        _log.LogInformation("RabbitPublisher verbunden: {Host}:{Port} Exchange={Exchange}",
            _opt.HostName, _opt.Port, _opt.Exchange);
    }

    public Task PublishAsync<T>(string routingKey, T message, CancellationToken ct = default)
    {
        var json = JsonSerializer.Serialize(message);
        var body = Encoding.UTF8.GetBytes(json);

        var props = _ch.CreateBasicProperties();
        props.Persistent = true;

        _ch.BasicPublish(exchange: _opt.Exchange, routingKey: routingKey, basicProperties: props, body: body);
        _ch.WaitForConfirmsOrDie(TimeSpan.FromSeconds(5));

        _log.LogInformation("Nachricht veröffentlicht. RoutingKey={RoutingKey} Size={Size}B", routingKey, body.Length);
        return Task.CompletedTask;
    }

    public void Dispose()
    {
        try { _ch?.Close(); } catch { }
        try { _conn?.Close(); } catch { }
        _ch?.Dispose();
        _conn?.Dispose();
        _log.LogInformation("RabbitPublisher getrennt.");
    }
}