using DMSG3.Domain.Entities;
using DMSG3.REST.DTOs;
using System;
using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Threading.Tasks;
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
        var bytes = Encoding.UTF8.GetBytes("hallo hallo");
        await _factory.ResetAndSeedAsync(new Document
        {
            Id = _factory.SeededDocumentId,
            Name = "testdatei",
            OriginalFileName = "testdatei.txt",
            ContentType = "text/plain; charset=utf-8",
            Content = bytes,
            SizeBytes = bytes.LongLength,
            UploadTime = DateTime.UtcNow
        });

        var client = _factory.CreateClient();
        var id = _factory.SeededDocumentId;

        var response = await client.GetAsync($"/api/documents/{id}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var doc = await response.Content.ReadFromJsonAsync<DocumentDetailsDto>();
        Assert.NotNull(doc);
        Assert.Equal(id, doc!.Id);
        Assert.Equal("testdatei", doc.Name);
        Assert.Equal("testdatei.txt", doc.OriginalFileName);
        Assert.True(doc.SizeBytes >= 1);

        var dl = await client.GetAsync($"/api/documents/{id}/download");
        Assert.Equal(HttpStatusCode.OK, dl.StatusCode);
        var text = await dl.Content.ReadAsStringAsync();
        Assert.Equal("hallo hallo", text);
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