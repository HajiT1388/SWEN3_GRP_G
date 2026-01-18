using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Threading.Tasks;
using DMSG3.Domain.Entities;
using DMSG3.REST.DTOs;
using Xunit;

namespace DMSG3.Tests;

public class DocumentsSearchEndpointTests : IClassFixture<TestWebApplicationFactory>
{
    private readonly TestWebApplicationFactory _factory;

    public DocumentsSearchEndpointTests(TestWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Search_returns_matches_from_name_and_ocr_text()
    {
        var d1 = BuildSeedDocument(Guid.NewGuid(), "Befund Labor", "befund-labor.pdf", "Blutwert");
        var d2 = BuildSeedDocument(Guid.NewGuid(), "Befund Labor 2", "befund-labor-2.txt", "Blauwal");
        await _factory.ResetAndSeedAsync(d1, d2);

        var client = _factory.CreateClient();

        var byOcr = await client.GetAsync("/api/documents/search?q=Blut");
        Assert.Equal(HttpStatusCode.OK, byOcr.StatusCode);
        var ocrDocs = await byOcr.Content.ReadFromJsonAsync<List<DocumentListItemDto>>();
        Assert.NotNull(ocrDocs);
        Assert.Contains(ocrDocs!, d => d.Id == d1.Document.Id);
        Assert.DoesNotContain(ocrDocs!, d => d.Id == d2.Document.Id);

        var byName = await client.GetAsync("/api/documents/search?q=blau");
        Assert.Equal(HttpStatusCode.OK, byName.StatusCode);
        var nameDocs = await byName.Content.ReadFromJsonAsync<List<DocumentListItemDto>>();
        Assert.NotNull(nameDocs);
        Assert.Contains(nameDocs!, d => d.Id == d2.Document.Id);
        Assert.DoesNotContain(nameDocs!, d => d.Id == d1.Document.Id);
    }

    [Fact]
    public async Task Search_returns_all_when_query_is_empty()
    {
        var d1 = BuildSeedDocument(Guid.NewGuid(), "Alpha", "alpha.txt", "eins");
        var d2 = BuildSeedDocument(Guid.NewGuid(), "Beta", "beta.txt", "zwei");
        await _factory.ResetAndSeedAsync(d1, d2);

        var client = _factory.CreateClient();
        var response = await client.GetAsync("/api/documents/search");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var docs = await response.Content.ReadFromJsonAsync<List<DocumentListItemDto>>();
        Assert.NotNull(docs);
        Assert.Equal(2, docs!.Count);
    }

    private static SeedDocument BuildSeedDocument(Guid id, string name, string fileName, string ocrText)
    {
        var bytes = Encoding.UTF8.GetBytes(ocrText);
        var doc = new Document
        {
            Id = id,
            Name = name,
            OriginalFileName = fileName,
            ContentType = "text/plain; charset=utf-8",
            SizeBytes = bytes.LongLength,
            StorageBucket = "documents",
            StorageObjectName = $"{id:N}.txt",
            UploadTime = DateTime.UtcNow,
            OcrStatus = DocumentOcrStatus.Completed,
            OcrText = ocrText,
            OcrCompletedAt = DateTime.UtcNow,
            SummaryStatus = DocumentSummaryStatus.Pending
        };
        return new SeedDocument(doc, bytes);
    }
}