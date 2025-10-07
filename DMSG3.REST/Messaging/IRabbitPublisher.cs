namespace DMSG3.REST.Messaging;

public interface IRabbitPublisher
{
    Task PublishAsync<T>(string routingKey, T message, CancellationToken ct = default);
}
