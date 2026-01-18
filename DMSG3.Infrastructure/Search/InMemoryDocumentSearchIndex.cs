using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace DMSG3.Infrastructure.Search;

public class InMemoryDocumentSearchIndex : IDocumentSearchIndex
{
    private readonly ConcurrentDictionary<Guid, DocumentSearchEntry> _entries = new();

    public Task IndexAsync(DocumentSearchEntry entry, CancellationToken ct)
    {
        if (entry.Id == Guid.Empty)
        {
            return Task.CompletedTask;
        }

        _entries[entry.Id] = new DocumentSearchEntry
        {
            Id = entry.Id,
            Name = entry.Name,
            OriginalFileName = entry.OriginalFileName,
            OcrText = entry.OcrText
        };
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<DocumentSearchHit>> SearchAsync(string query, int size, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return Task.FromResult<IReadOnlyList<DocumentSearchHit>>(Array.Empty<DocumentSearchHit>());
        }

        var term = query.Trim();

        var hits = _entries.Values
            .Select(entry => new
            {
                Entry = entry,
                Score = Score(entry, term)
            })
            .Where(x => x.Score > 0)
            .OrderByDescending(x => x.Score)
            .Take(size)
            .Select(x => new DocumentSearchHit(x.Entry.Id, x.Score))
            .ToList();

        return Task.FromResult<IReadOnlyList<DocumentSearchHit>>(hits);
    }

    public void Clear()
    {
        _entries.Clear();
    }

    private static double Score(DocumentSearchEntry entry, string term)
    {
        var score = 0;
        if (Contains(entry.Name, term)) score++;
        if (Contains(entry.OriginalFileName, term)) score++;
        if (Contains(entry.OcrText, term)) score++;
        return score;
    }

    private static bool Contains(string? value, string term)
    {
        return !string.IsNullOrWhiteSpace(value) &&
               value.IndexOf(term, StringComparison.OrdinalIgnoreCase) >= 0;
    }
}
