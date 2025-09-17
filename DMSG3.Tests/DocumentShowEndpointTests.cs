using System;
using System.Net;
using System.Net.Http.Json;
using System.Threading.Tasks;
using DMSG3.Domain.Entities;
using Xunit;

namespace DMSG3.Tests;

public class DocumentShowEndpointTests : IClassFixture<TestWebApplicationFactory>
{
    private readonly TestWebApplicationFactory _factory;

    public DocumentShowEndpointTests(TestWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Show_returns_correct_document_by_id()
    {
        await _factory.ResetAndSeedAsync(new Document
        {
            Id = _factory.SeededDocumentId,
            FileName = "testdatei.txt",
            FileContent = "hallo hallo",
            UploadTime = DateTime.UtcNow
        });

        var client = _factory.CreateClient();
        var id = _factory.SeededDocumentId;

        var response = await client.GetAsync($"/api/documents/{id}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var doc = await response.Content.ReadFromJsonAsync<Document>();
        Assert.NotNull(doc);
        Assert.Equal(id, doc!.Id);
        Assert.Equal("testdatei.txt", doc.FileName);
        Assert.Equal("hallo hallo", doc.FileContent);
    }

    [Fact]
    public async Task Show_returns_404_for_unknown_id()
    {
        await _factory.ResetAndSeedAsync(); // db = leer

        var client = _factory.CreateClient();
        var response = await client.GetAsync($"/api/documents/{Guid.NewGuid()}");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}