using DMSG3.Domain.Entities;

namespace DMSG3.Infrastructure.Storage;

public interface IDocumentStorage
{
    Task UploadAsync(Document document, Stream content, CancellationToken ct = default);
    Task<Stream> DownloadAsync(Document document, CancellationToken ct = default);
    Task DeleteAsync(Document document, CancellationToken ct = default);
}