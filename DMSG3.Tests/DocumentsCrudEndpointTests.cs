using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http.Json;
using System.Threading.Tasks;
using DMSG3.Domain.Entities;
using DMSG3.Infrastructure;
using DMSG3.REST.DTOs;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace DMSG3.Tests;

public class DocumentsCrudEndpointTests : IClassFixture<TestWebApplicationFactory>
{
    private readonly TestWebApplicationFactory _factory;

    public DocumentsCrudEndpointTests(TestWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task List_returns_all_documents()
    {
        await _factory.ResetAndSeedAsync(
            new Document { Id = Guid.NewGuid(), FileName = "alpha.txt", FileContent = "A", UploadTime = DateTime.UtcNow },
            new Document { Id = Guid.NewGuid(), FileName = "beta.txt",  FileContent = "B", UploadTime = DateTime.UtcNow }
        );

        var client = _factory.CreateClient();
        var response = await client.GetAsync("/api/documents");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var docs = await response.Content.ReadFromJsonAsync<List<Document>>();
        Assert.NotNull(docs);
        Assert.Equal(2, docs!.Count);
        Assert.Contains(docs, d => d.FileName == "alpha.txt");
        Assert.Contains(docs, d => d.FileName == "beta.txt");
    }

    [Fact]
    public async Task Post_creates_document_and_persists()
    {
        await _factory.ResetAndSeedAsync(); // db x

        var client = _factory.CreateClient();
        var dto = new DocumentDto("neu.txt", "123");

        var response = await client.PostAsJsonAsync("/api/documents", dto);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.NotNull(response.Headers.Location);

        var created = await response.Content.ReadFromJsonAsync<Document>();
        Assert.NotNull(created);
        Assert.NotEqual(Guid.Empty, created!.Id);
        Assert.Equal("neu.txt", created.FileName);
        Assert.Equal("123", created.FileContent);

        // in DB?
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DMSG3_DbContext>();
        var inDb = db.Documents.SingleOrDefault(d => d.Id == created.Id);
        Assert.NotNull(inDb);
    }

    [Fact]
    public async Task Delete_existing_document_removes_and_returns_204()
    {
        var id = Guid.NewGuid();
        await _factory.ResetAndSeedAsync(new Document
        {
            Id = id,
            FileName = "deleteme.txt",
            FileContent = "bye",
            UploadTime = DateTime.UtcNow
        });

        var client = _factory.CreateClient();
        var del = await client.DeleteAsync($"/api/documents/{id}");
        Assert.Equal(HttpStatusCode.NoContent, del.StatusCode);

        var get = await client.GetAsync($"/api/documents/{id}");
        Assert.Equal(HttpStatusCode.NotFound, get.StatusCode);
    }

    [Fact]
    public async Task Delete_returns_404_for_unknown_id()
    {
        await _factory.ResetAndSeedAsync(); // db leer

        var client = _factory.CreateClient();
        var del = await client.DeleteAsync($"/api/documents/{Guid.NewGuid()}");
        Assert.Equal(HttpStatusCode.NotFound, del.StatusCode);
    }
}