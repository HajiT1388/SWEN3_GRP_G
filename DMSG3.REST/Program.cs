using DMSG3.Domain.Entities;
using DMSG3.Infrastructure;
using DMSG3.REST.DTOs;
using DMSG3.REST.Logging;
using DMSG3.REST.Messaging;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Wenn es im Container läuft, nicht in Datei loggen, sondern Container-Konsolen-Log
var runningInContainer = string.Equals(Environment.GetEnvironmentVariable("DOTNET_RUNNING_IN_CONTAINER"), "true", StringComparison.OrdinalIgnoreCase);
var runStamp = DateTime.Now; // Dateiname für die Erstellen Logs enthält die Startzeit dieser Program.cs

builder.Logging.ClearProviders();
builder.Logging.AddConsole();

if (!runningInContainer)
{
    var logsDir = Path.Combine(AppContext.BaseDirectory, "logs");
    Directory.CreateDirectory(logsDir);
    builder.Logging.AddProvider(new FileLoggerProvider(
        directory: logsDir,
        filenameBase: $"rest_{runStamp:yyyyMMdd_HHmmss}",
        minLevel: LogLevel.Information
    ));
}

// InMemory für Tests (Abfrage bei Tests) sonst PostgreSQL
if (builder.Environment.IsEnvironment("Testing"))
{
    builder.Services.AddDbContext<DMSG3_DbContext>(opt =>
        opt.UseInMemoryDatabase("DMSG3_TestDb"));
}
else
{
    builder.Services.AddDbContext<DMSG3_DbContext>(opt =>
        opt.UseNpgsql(builder.Configuration.GetConnectionString("Default"),
            o => o.EnableRetryOnFailure()));
}

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// RabbitMQ & Publisher
builder.Services.Configure<RabbitMqOptions>(builder.Configuration.GetSection("RabbitMq"));
builder.Services.AddSingleton<IRabbitPublisher, RabbitPublisher>();

var app = builder.Build();

// Start-Log
app.Logger.LogInformation("REST gestartet. Container={Container} BaseDir={BaseDir} LogFileBase=rest_{Stamp}",
    runningInContainer, AppContext.BaseDirectory, runStamp.ToString("yyyyMMdd_HHmmss"));

// ProblemDetails JSON
app.UseExceptionHandler(errApp =>
{
    errApp.Run(async context =>
    {
        var feat = context.Features.Get<IExceptionHandlerFeature>();
        var ex = feat?.Error;

        var pd = new ProblemDetails
        {
            Title = "Interner Serverfehler",
            Status = StatusCodes.Status500InternalServerError,
            Detail = app.Environment.IsDevelopment() ? ex?.Message : null,
            Instance = context.Request.Path
        };
        pd.Extensions["traceId"] = context.TraceIdentifier;

        context.Response.StatusCode = StatusCodes.Status500InternalServerError;
        context.Response.ContentType = "application/problem+json";
        await context.Response.WriteAsJsonAsync(pd);
    });
});

if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
}

// pgcrypto + Migrationen (nur bei rel)
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<DMSG3_DbContext>();
    var providerName = db.Database.ProviderName;

    if (!string.Equals(providerName, "Microsoft.EntityFrameworkCore.InMemory", StringComparison.OrdinalIgnoreCase))
    {
        try
        {
            db.Database.ExecuteSqlRaw("CREATE EXTENSION IF NOT EXISTS pgcrypto;");
            app.Logger.LogInformation("pgcrypto geprüft/aktiviert.");
        }
        catch (Exception ex)
        {
            app.Logger.LogWarning(ex, "pgcrypto konnte nicht erstellt werden.");
        }

        try
        {
            db.Database.Migrate();
            app.Logger.LogInformation("EF Core Migrationen ausgeführt.");
        }
        catch (Exception ex)
        {
            app.Logger.LogError(ex, "Migrationen fehlgeschlagen");
            throw;
        }
    }
}

app.UseSwagger();
app.UseSwaggerUI();

// Health Endpoint
app.MapGet("/health", () => Results.Ok("healthy"));

// API-Gruppe
var api = app.MapGroup("/api").WithTags("Documents");
api.DisableAntiforgery();

// Whitelist f r Dateiendungen
var AllowedExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { ".pdf", ".txt" };

// GET /api/documents Liste ohne Inhalt
api.MapGet("/documents", async (DMSG3_DbContext db, ILoggerFactory lf) =>
{
    var log = lf.CreateLogger("Documents");
    var items = await db.Documents
        .AsNoTracking()
        .OrderByDescending(d => d.UploadTime)
        .Select(d => new DocumentListItemDto(
            d.Id,
            d.Name,
            d.UploadTime,
            d.SizeBytes,
            d.ContentType
        ))
        .ToListAsync();

    log.LogInformation("Dokumentenliste abgefragt. Count={Count}", items.Count);
    return Results.Ok(items);
});

// GET /api/documents/id Details ohne Inhalt
api.MapGet("/documents/{id:guid}", async (Guid id, DMSG3_DbContext db, ILoggerFactory lf) =>
{
    var log = lf.CreateLogger("Documents");
    var dto = await db.Documents
        .AsNoTracking()
        .Where(d => d.Id == id)
        .Select(d => new DocumentDetailsDto(
            d.Id,
            d.Name,
            d.OriginalFileName,
            d.ContentType,
            d.SizeBytes,
            d.UploadTime
        ))
        .FirstOrDefaultAsync();

    if (dto is null)
    {
        log.LogWarning("Dokument nicht gefunden. Id={Id}", id);
        return Results.NotFound();
    }

    return Results.Ok(dto);
});

// GET /api/documents/id/download Datei inline optional
api.MapGet("/documents/{id:guid}/download", async (
    Guid id,
    [FromQuery(Name = "inline")] string? inline,
    DMSG3_DbContext db,
    ILoggerFactory lf) =>
{
    var log = lf.CreateLogger("Documents");
    var doc = await db.Documents.AsNoTracking().FirstOrDefaultAsync(d => d.Id == id);
    if (doc is null)
    {
        log.LogWarning("Download: Dokument nicht gefunden. Id={Id}", id);
        return Results.NotFound();
    }

    var stream = new MemoryStream(doc.Content, writable: false);

    var showInline = inline is not null &&
        (inline == "1" ||
         inline.Equals("true", StringComparison.OrdinalIgnoreCase) ||
         inline.Equals("yes", StringComparison.OrdinalIgnoreCase) ||
         inline.Equals("on", StringComparison.OrdinalIgnoreCase));

    if (showInline)
    {
        // ohne filename Browser rendert inline PDF/Text
        return Results.File(stream, doc.ContentType, fileDownloadName: null, enableRangeProcessing: true);
    }

    var ext = Path.GetExtension(doc.OriginalFileName);
    var suggestedName = string.IsNullOrWhiteSpace(ext) ? doc.Name : $"{doc.Name}{ext}";
    return Results.File(stream, doc.ContentType, suggestedName, enableRangeProcessing: true);
});

// POST /api/documents multipart/form-data Upload
api.MapPost("/documents", async (
    [FromForm] DocumentUploadRequest request,
    DMSG3_DbContext db,
    IRabbitPublisher publisher,
    ILoggerFactory lf) =>
{
    var log = lf.CreateLogger("Documents");

    if (request?.File == null || request.File.Length == 0)
        return Results.BadRequest("Datei fehlt.");

    var ext = Path.GetExtension(request.File.FileName);
    if (string.IsNullOrWhiteSpace(ext) || !AllowedExtensions.Contains(ext))
        return Results.BadRequest("Es sind nur .pdf und .txt erlaubt.");

    var name = (request.Name ?? "").Trim();
    if (string.IsNullOrWhiteSpace(name))
    {
        // Dateiname ohne Endung, wenn kein Name eingegeben ^
        name = Path.GetFileNameWithoutExtension(request.File.FileName);
    }
    if (string.IsNullOrWhiteSpace(name))
        return Results.BadRequest("Name konnte nicht ermittelt werden.");

    var contentType = !string.IsNullOrWhiteSpace(request.File.ContentType)
        ? request.File.ContentType
        : (ext.Equals(".pdf", StringComparison.OrdinalIgnoreCase) ? "application/pdf" : "text/plain; charset=utf-8");

    await using var ms = new MemoryStream();
    await request.File.CopyToAsync(ms);
    var bytes = ms.ToArray();

    var doc = new Document
    {
        Id = Guid.NewGuid(),
        Name = name,
        OriginalFileName = request.File.FileName,
        ContentType = contentType,
        SizeBytes = bytes.LongLength,
        Content = bytes
    };

    db.Documents.Add(doc);
    await db.SaveChangesAsync();

    // LOG Upload
    log.LogInformation("Dokument wurde hochgeladen. Id={Id} Name={Name} SizeBytes={Size}", doc.Id, doc.Name, doc.SizeBytes);

    // OCR-Request ins Topic (ins Nichts jetzt)
    var ocrRequest = new
    {
        id = doc.Id,
        originalFileName = doc.OriginalFileName,
        contentType = doc.ContentType,
        sizeBytes = doc.SizeBytes,
        uploadedAtUtc = doc.UploadTime
    };
    try
    {
        await publisher.PublishAsync("ocr.request", ocrRequest);
        log.LogInformation("OCR_REQUEST gepublished. Id={Id}", doc.Id);
    }
    catch (Exception ex)
    {
        log.LogError(ex, "Publish OCR_REQUEST fehlgeschlagen. Id={Id}", doc.Id);
    }

    // Nur id zur ck
    return Results.Created($"/api/documents/{doc.Id}", new { id = doc.Id });
})
.Accepts<DocumentUploadRequest>("multipart/form-data");

// DELETE /api/documents/id
api.MapDelete("/documents/{id:guid}", async (Guid id, DMSG3_DbContext db, ILoggerFactory lf) =>
{
    var log = lf.CreateLogger("Documents");
    var doc = await db.Documents.FindAsync(id);
    if (doc is null)
    {
        log.LogWarning("Löschen: Dokument nicht gefunden. Id={Id}", id);
        return Results.NotFound();
    }

    db.Documents.Remove(doc);
    await db.SaveChangesAsync();

    // Logging: Delete
    log.LogInformation("Dokument gelöscht. Id={Id} Name={Name}", id, doc.Name);

    return Results.NoContent();
});

app.Run();

public partial class Program { }