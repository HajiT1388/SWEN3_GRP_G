namespace DMSG3.Worker;

public class RabbitMqOptions
{
    public string HostName { get; set; } = "localhost";
    public int Port { get; set; } = 5672;
    public string UserName { get; set; } = "guest";
    public string Password { get; set; } = "guest";
    public string Exchange { get; set; } = "dmsg3.events";
    public string OcrQueue { get; set; } = "dmsg3.ocr.queue";
    public string ResultQueue { get; set; } = "dmsg3.result.queue";
}