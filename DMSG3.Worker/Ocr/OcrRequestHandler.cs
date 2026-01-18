using System.Text;
using DMSG3.Domain.Entities;
using DMSG3.Domain.Messaging;
using DMSG3.Infrastructure;
using DMSG3.Infrastructure.Storage;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace DMSG3.Worker.Ocr;

public record OcrProcessingResult(Guid DocumentId, string Status, string? TextPreview, DateTime? CompletedAt);

public class OcrRequestHandler
{
    private readonly DMSG3_DbContext _db;
    private readonly IDocumentStorage _storage;
    private readonly IOcrEngine _ocrEngine;
    private readonly ILogger<OcrRequestHandler> _logger;

    public OcrRequestHandler(
        DMSG3_DbContext db,
        IDocumentStorage storage,
        IOcrEngine ocrEngine,
        ILogger<OcrRequestHandler> logger)
    {
        _db = db;
        _storage = storage;
        _ocrEngine = ocrEngine;
        _logger = logger;
    }

    public async Task<OcrProcessingResult?> ProcessAsync(OcrRequestMessage message, CancellationToken ct)
    {
        var doc = await _db.Documents.FirstOrDefaultAsync(d => d.Id == message.DocumentId, ct);
        if (doc is null)
        {
            _logger.LogWarning("Dokument für OCR nicht gefunden. Id={Id}", message.DocumentId);
            return null;
        }

        try
        {
            doc.OcrStatus = DocumentOcrStatus.Processing;
            doc.OcrStartedAt = DateTime.UtcNow;
            doc.OcrError = null;
            await _db.SaveChangesAsync(ct);

            string text;
            await using var contentStream = await _storage.DownloadAsync(doc, ct);

            if (doc.ContentType.StartsWith("text/", StringComparison.OrdinalIgnoreCase))
            {
                using var reader = new StreamReader(contentStream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true, leaveOpen: false);
                text = await reader.ReadToEndAsync(ct);
            }
            else
            {
                var tempFile = Path.Combine(Path.GetTempPath(), $"ocr-doc-{doc.Id:N}.pdf");
                await SaveStreamToFileAsync(contentStream, tempFile, ct);
                try
                {
                    text = await _ocrEngine.RecognizePdfAsync(tempFile, ct);
                }
                finally
                {
                    try { File.Delete(tempFile); } catch { }
                }
            }

            var truncated = Truncate(text, 8000);
            doc.OcrText = truncated;
            doc.OcrStatus = DocumentOcrStatus.Completed;
            doc.OcrCompletedAt = DateTime.UtcNow;
            doc.SummaryStatus = DocumentSummaryStatus.Pending;
            doc.SummaryText = null;
            doc.SummaryError = null;
            doc.SummaryCompletedAt = null;
            await _db.SaveChangesAsync(ct);

            return new OcrProcessingResult(doc.Id, doc.OcrStatus, BuildPreview(truncated), doc.OcrCompletedAt);
        }
        catch (Exception ex)
        {
            doc.OcrStatus = DocumentOcrStatus.Failed;
            doc.OcrError = ex.Message;
            doc.OcrCompletedAt = DateTime.UtcNow;
            doc.SummaryStatus = DocumentSummaryStatus.Failed;
            doc.SummaryError = "OCR fehlgeschlagen; keine Summary.";
            doc.SummaryCompletedAt = DateTime.UtcNow;
            doc.SummaryText = null;
            await _db.SaveChangesAsync(ct);
            _logger.LogError(ex, "OCR fehlgeschlagen. Id={Id}", doc.Id);
            throw;
        }
    }

    private static async Task SaveStreamToFileAsync(Stream stream, string path, CancellationToken ct)
    {
        stream.Position = 0;
        await using var fs = File.Create(path);
        await stream.CopyToAsync(fs, ct);
    }

    private static string Truncate(string value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length <= maxLength)
            return value;

        return value[..maxLength];
    }

    private static string? BuildPreview(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        var trimmed = text.Trim();
        return trimmed.Length <= 200 ? trimmed : $"{trimmed[..200]}…";
    }
}