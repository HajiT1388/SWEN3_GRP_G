using DMSG3.Domain.Messaging;
using DMSG3.Infrastructure;
using DMSG3.Infrastructure.Search;
using DMSG3.Infrastructure.Storage;
using DMSG3.Worker;
using DMSG3.Worker.Indexing;
using DMSG3.Worker.Ocr;
using Elastic.Clients.Elasticsearch;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Minio;

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
builder.Services.Configure<MinioOptions>(builder.Configuration.GetSection("Minio"));
builder.Services.Configure<OcrCliOptions>(builder.Configuration.GetSection("Ocr"));

builder.Services.AddDbContext<DMSG3_DbContext>(opt =>
    opt.UseNpgsql(builder.Configuration.GetConnectionString("Default")));

var elasticUri = builder.Configuration.GetConnectionString("ElasticSearch") ?? "http://localhost:9200";
var elasticSettings = new ElasticsearchClientSettings(new Uri(elasticUri))
    .DefaultIndex(ElasticDocumentSearchIndex.IndexName);
builder.Services.AddSingleton(new ElasticsearchClient(elasticSettings));
builder.Services.AddSingleton<IDocumentSearchIndex, ElasticDocumentSearchIndex>();

builder.Services.AddSingleton<IMinioClient>(sp =>
{
    var opt = sp.GetRequiredService<IOptions<MinioOptions>>().Value;
    return new MinioClient()
        .WithEndpoint(opt.Endpoint)
        .WithCredentials(opt.AccessKey, opt.SecretKey)
        .WithSSL(opt.UseSsl)
        .Build();
});

builder.Services.AddSingleton<IDocumentStorage, MinioDocumentStorage>();
builder.Services.AddSingleton<IProcessRunner, ProcessRunner>();
builder.Services.AddSingleton<IOcrEngine, TesseractOcrEngine>();
builder.Services.AddScoped<OcrRequestHandler>();
builder.Services.AddScoped<DocumentIndexingHandler>();

builder.Services.AddHostedService<Worker>();
builder.Services.AddHostedService<IndexingWorker>();

var host = builder.Build();

host.Services.GetRequiredService<ILoggerFactory>()
    .CreateLogger("Startup")
    .LogInformation("Worker gestartet. Container={Container} BaseDir={BaseDir} LogFileBase=worker_{Stamp}",
        runningInContainer, AppContext.BaseDirectory, runStamp.ToString("yyyyMMdd_HHmmss"));

host.Run();
