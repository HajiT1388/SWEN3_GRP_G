using System.Net;
using DMSG3.Domain.Entities;
using DMSG3.Domain.Messaging;
using DMSG3.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace DMSG3.GenAIWorker.Summaries;

public class DocumentSummaryHandler
{
    private readonly DMSG3_DbContext _db;
    private readonly IGenAiClient _client;
    private readonly ILogger<DocumentSummaryHandler> _logger;
    private readonly int _inputLimit;

    public DocumentSummaryHandler(
        DMSG3_DbContext db,
        IGenAiClient client,
        ILogger<DocumentSummaryHandler> logger,
        IConfiguration configuration)
    {
        _db = db;
        _client = client;
        _logger = logger;
        var limit = configuration.GetValue<int?>("GenAi:InputMaxCharacters") ?? 4000;
        _inputLimit = Math.Max(0, limit);
    }

    public async Task ProcessAsync(SummaryRequestMessage message, CancellationToken ct)
    {
        var document = await _db.Documents.FirstOrDefaultAsync(d => d.Id == message.DocumentId, ct);
        if (document is null)
        {
            _logger.LogWarning("Dokument für Zusammenfassung nicht gefunden. DocumentId={DocumentId}", message.DocumentId);
            return;
        }

        if (string.IsNullOrWhiteSpace(document.OcrText))
        {
            document.SummaryStatus = DocumentSummaryStatus.Failed;
            document.SummaryError = "Kein OCR-Text vorhanden.";
            document.SummaryCompletedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync(ct);
            _logger.LogWarning("Zusammenfassen übersprungen, OCR-Text fehlt. DocumentId={DocumentId}", document.Id);
            return;
        }

        document.SummaryStatus = DocumentSummaryStatus.Processing;
        document.SummaryError = null;
        document.SummaryCompletedAt = null;
        await _db.SaveChangesAsync(ct);
        _logger.LogInformation("Zusammenfassen gestartet. DocumentId={DocumentId}", document.Id);

        try
        {
            var source = PrepareInput(document.OcrText!, _inputLimit);
            var summary = await _client.SummarizeAsync(source, ct);
            document.SummaryText = summary;
            document.SummaryStatus = DocumentSummaryStatus.Completed;
            document.SummaryCompletedAt = DateTime.UtcNow;
            document.SummaryError = null;
            await _db.SaveChangesAsync(ct);
            _logger.LogInformation("Zusammenfassen gespeichert. DocumentId={DocumentId}", document.Id);
        }
        catch (GenAiClientException ex) when (ex.IsTransient)
        {
            document.SummaryStatus = DocumentSummaryStatus.Pending;
            var limitMsg = Limit(ex.Message);
            document.SummaryError = $"GenAI meldet: {limitMsg}";
            await _db.SaveChangesAsync(ct);

            var delay = ex.StatusCode == HttpStatusCode.TooManyRequests
                ? TimeSpan.FromSeconds(60)
                : TimeSpan.FromSeconds(5);

            _logger.LogWarning(ex, "GenAI-Fehler. DocumentId={DocumentId} Delay={Delay}s", document.Id, delay.TotalSeconds);
            throw new SummaryTransientException("GenAI temporärer Fehler", ex, delay);
        }
        catch (GenAiClientException ex)
        {
            document.SummaryStatus = DocumentSummaryStatus.Failed;
            document.SummaryError = Limit(ex.Message);
            document.SummaryCompletedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync(ct);
            _logger.LogError(ex, "GenAI permanent fehlgeschlagen. DocumentId={DocumentId}", document.Id);
        }
        catch (Exception ex)
        {
            document.SummaryStatus = DocumentSummaryStatus.Pending;
            document.SummaryError = $"Interner Fehler: {Limit(ex.Message)}";
            await _db.SaveChangesAsync(ct);
            _logger.LogWarning(ex, "Zusammenfassung muss später erneut versucht werden. DocumentId={DocumentId}", document.Id);
            throw new SummaryTransientException("Interner Fehler", ex);
        }
    }

    private static string PrepareInput(string text, int limit)
    {
        if (string.IsNullOrWhiteSpace(text)) return text;
        if (limit <= 0 || text.Length <= limit) return text;

        var half = Math.Max(1, limit / 2);
        var head = text[..half];
        var tail = text[^half..];

        return $"{head}\n\n[... dokumenttext wurde gekürzt ...]\n\n{tail}";
    }

    private static string Limit(string? message)
    {
        if (string.IsNullOrWhiteSpace(message)) return string.Empty;
        message = message.Trim();
        return message.Length <= 512 ? message : message[..512] + "…";
    }
}