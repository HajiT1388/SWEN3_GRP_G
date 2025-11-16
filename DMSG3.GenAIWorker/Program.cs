using DMSG3.Domain.Messaging;
using DMSG3.GenAIWorker;
using DMSG3.GenAIWorker.Logging;
using DMSG3.GenAIWorker.Summaries;
using DMSG3.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

var builder = Host.CreateApplicationBuilder(args);

var runningInContainer = string.Equals(Environment.GetEnvironmentVariable("DOTNET_RUNNING_IN_CONTAINER"), "true", StringComparison.OrdinalIgnoreCase);
var runStamp = DateTime.Now;

builder.Logging.ClearProviders();
builder.Logging.AddConsole();

if (!runningInContainer)
{
    var logsDir = Path.Combine(AppContext.BaseDirectory, "logs");
    Directory.CreateDirectory(logsDir);
    builder.Logging.AddProvider(new FileLoggerProvider(
        directory: logsDir,
        filenameBase: $"genai_worker_{runStamp:yyyyMMdd_HHmmss}",
        minLevel: LogLevel.Information));
}

builder.Services.Configure<RabbitMqOptions>(builder.Configuration.GetSection("RabbitMq"));

builder.Services.AddDbContext<DMSG3_DbContext>(opt =>
    opt.UseNpgsql(builder.Configuration.GetConnectionString("Default")));

builder.Services.AddHttpClient<IGenAiClient, GeminiGenAiClient>()
    .ConfigureHttpClient((sp, client) =>
    {
        var configuration = sp.GetRequiredService<IConfiguration>();
        var baseUrl = configuration["GenAi:BaseUrl"]?.Trim();
        if (string.IsNullOrWhiteSpace(baseUrl))
        {
            baseUrl = "https://generativelanguage.googleapis.com/";
        }

        var timeoutSeconds = configuration.GetValue<int?>("GenAi:RequestTimeoutSeconds") ?? 30;

        client.BaseAddress = new Uri(baseUrl);
        client.Timeout = TimeSpan.FromSeconds(Math.Max(5, timeoutSeconds));
    });
builder.Services.AddScoped<DocumentSummaryHandler>();
builder.Services.AddHostedService<SummaryWorker>();

var host = builder.Build();

host.Services.GetRequiredService<ILoggerFactory>()
    .CreateLogger("Startup")
    .LogInformation("GenAI Worker gestartet. Container={Container} BaseDir={BaseDir} LogFileBase=genai_worker_{Stamp}",
        runningInContainer, AppContext.BaseDirectory, runStamp.ToString("yyyyMMdd_HHmmss"));

host.Run();