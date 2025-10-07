using DMSG3.Worker;

var builder = Host.CreateApplicationBuilder(args);

// Selbiges wie bei REST/Program.cs: Wenn im Container, wird nicht in Datei geloggt.
var runningInContainer = string.Equals(Environment.GetEnvironmentVariable("DOTNET_RUNNING_IN_CONTAINER"), "true", StringComparison.OrdinalIgnoreCase);
var runStamp = DateTime.Now;

builder.Logging.ClearProviders();
builder.Logging.AddConsole();

if (!runningInContainer)
{
    var logsDir = Path.Combine(AppContext.BaseDirectory, "logs");
    Directory.CreateDirectory(logsDir);
    builder.Logging.AddProvider(new DMSG3.Worker.Logging.FileLoggerProvider(
        directory: logsDir,
        filenameBase: $"worker_{runStamp:yyyyMMdd_HHmmss}",
        minLevel: LogLevel.Information
    ));
}

builder.Services.Configure<RabbitMqOptions>(builder.Configuration.GetSection("RabbitMq"));
builder.Services.AddHostedService<Worker>();

var host = builder.Build();

host.Services.GetRequiredService<ILoggerFactory>()
    .CreateLogger("Startup")
    .LogInformation("Worker gestartet. Container={Container} BaseDir={BaseDir} LogFileBase=worker_{Stamp}",
        runningInContainer, AppContext.BaseDirectory, runStamp.ToString("yyyyMMdd_HHmmss"));

host.Run();