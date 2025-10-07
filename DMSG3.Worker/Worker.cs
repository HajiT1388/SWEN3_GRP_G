using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

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

    protected override Task ExecuteAsync(CancellationToken stoppingToken)
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

        _conn = factory.CreateConnection();
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
                    ocrTextPreview = "Es wurde nichts ausgelesen. Noch.",
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
                _ch!.BasicNack(ea.DeliveryTag, false, requeue: true);
            }
            await Task.CompletedTask;
        };

        _ch.BasicConsume(_opt.OcrQueue, autoAck: false, consumer);

        var tcs = new TaskCompletionSource();
        stoppingToken.Register(() => tcs.TrySetResult());
        return tcs.Task;
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
