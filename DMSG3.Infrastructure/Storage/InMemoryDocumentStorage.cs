using System.Collections.Concurrent;
using DMSG3.Domain.Entities;

namespace DMSG3.Infrastructure.Storage;

public class InMemoryDocumentStorage : IDocumentStorage
{
    private readonly ConcurrentDictionary<Guid, byte[]> _store = new();

    public Task UploadAsync(Document document, Stream content, CancellationToken ct = default)
    {
        using var ms = new MemoryStream();
        content.CopyTo(ms);
        _store[document.Id] = ms.ToArray();
        return Task.CompletedTask;
    }

    public Task<Stream> DownloadAsync(Document document, CancellationToken ct = default)
    {
        if (!_store.TryGetValue(document.Id, out var entry))
        {
            throw new InvalidOperationException($"Dokument {document.Id} nicht im Speicher gefunden.");
        }

        var ms = new MemoryStream(entry, writable: false);
        return Task.FromResult<Stream>(ms);
    }

    public Task DeleteAsync(Document document, CancellationToken ct = default)
    {
        _store.TryRemove(document.Id, out _);
        return Task.CompletedTask;
    }

    public bool TryGet(Guid documentId, out byte[]? bytes)
    {
        if (_store.TryGetValue(documentId, out var entry))
        {
            bytes = entry;
            return true;
        }

        bytes = null;
        return false;
    }

    public void Clear() => _store.Clear();
}