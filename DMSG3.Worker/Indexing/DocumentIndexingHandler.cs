using DMSG3.Domain.Messaging;
using DMSG3.Infrastructure;
using DMSG3.Infrastructure.Search;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace DMSG3.Worker.Indexing;

public class DocumentIndexingHandler
{
    private readonly DMSG3_DbContext _db;
    private readonly IDocumentSearchIndex _searchIndex;
    private readonly ILogger<DocumentIndexingHandler> _log;

    public DocumentIndexingHandler(
        DMSG3_DbContext db,
        IDocumentSearchIndex searchIndex,
        ILogger<DocumentIndexingHandler> log)
    {
        _db = db;
        _searchIndex = searchIndex;
        _log = log;
    }

    public async Task ProcessAsync(IndexRequestMessage message, CancellationToken ct)
    {
        var doc = await _db.Documents.AsNoTracking()
            .FirstOrDefaultAsync(d => d.Id == message.DocumentId, ct);

        if (doc is null)
        {
            _log.LogWarning("Indexing: Dokument nicht gefunden. Id={Id}", message.DocumentId);
            return;
        }

        var entry = new DocumentSearchEntry
        {
            Id = doc.Id,
            Name = doc.Name,
            OriginalFileName = doc.OriginalFileName,
            OcrText = doc.OcrText
        };

        await _searchIndex.IndexAsync(entry, ct);
        _log.LogInformation("Indexing abgeschlossen. Id={Id}", doc.Id);
    }
}
