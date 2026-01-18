using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace DMSG3.Infrastructure.Search;

public interface IDocumentSearchIndex
{
    Task IndexAsync(DocumentSearchEntry entry, CancellationToken ct);
    Task<IReadOnlyList<DocumentSearchHit>> SearchAsync(string query, int size, CancellationToken ct);
}
