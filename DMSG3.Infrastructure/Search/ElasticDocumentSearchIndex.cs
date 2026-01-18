using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Elastic.Clients.Elasticsearch;
using Microsoft.Extensions.Logging;

namespace DMSG3.Infrastructure.Search;

public class ElasticDocumentSearchIndex : IDocumentSearchIndex
{
    public const string IndexName = "documents";

    private static readonly Regex QueryEscape = new("([+\\-=&|><!(){}\\[\\]^\"~*?:\\\\/])", RegexOptions.Compiled);

    private readonly ElasticsearchClient _client;
    private readonly ILogger<ElasticDocumentSearchIndex> _log;

    public ElasticDocumentSearchIndex(ElasticsearchClient client, ILogger<ElasticDocumentSearchIndex> log)
    {
        _client = client;
        _log = log;
    }

    public async Task IndexAsync(DocumentSearchEntry entry, CancellationToken ct)
    {
        if (entry.Id == Guid.Empty)
        {
            _log.LogWarning("Indexing uebersprungen: leere DocumentId.");
            return;
        }

        var response = await _client.IndexAsync(entry, i => i
            .Index(IndexName)
            .Id(entry.Id)
            .Refresh(Refresh.WaitFor),
            ct);

        if (!response.IsValidResponse)
        {
            _log.LogWarning("Elasticsearch Indexing fehlgeschlagen. Id={Id} Debug={Debug}",
                entry.Id, response.DebugInformation);
        }
    }

    public async Task<IReadOnlyList<DocumentSearchHit>> SearchAsync(string query, int size, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return Array.Empty<DocumentSearchHit>();
        }

        var sanitized = QueryEscape.Replace(query.Trim(), "\\$1");

        var response = await _client.SearchAsync<DocumentSearchEntry>(s => s
            .Index(IndexName)
            .Size(size)
            .Query(q => q.QueryString(qs => qs
                .AnalyzeWildcard(true)
                .Query($"*{sanitized}*")
                .Fields(new[] { "name", "originalFileName", "ocrText" }))),
            ct);

        if (!response.IsValidResponse)
        {
            _log.LogWarning("Elasticsearch Suche fehlgeschlagen. Query={Query} Debug={Debug}",
                query, response.DebugInformation);
            return Array.Empty<DocumentSearchHit>();
        }

        var hits = new List<DocumentSearchHit>();
        foreach (var hit in response.Hits)
        {
            if (TryGetId(hit.Id, hit.Source?.Id, out var id))
            {
                hits.Add(new DocumentSearchHit(id, hit.Score));
            }
        }

        return hits;
    }

    private static bool TryGetId(string? hitId, Guid? sourceId, out Guid id)
    {
        if (sourceId.HasValue && sourceId.Value != Guid.Empty)
        {
            id = sourceId.Value;
            return true;
        }

        if (!string.IsNullOrWhiteSpace(hitId) && Guid.TryParse(hitId, out id))
        {
            return true;
        }

        id = Guid.Empty;
        return false;
    }
}
