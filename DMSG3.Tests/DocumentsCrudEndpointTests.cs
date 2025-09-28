using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
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
        var aBytes = Encoding.UTF8.GetBytes("A");
        var bBytes = Encoding.UTF8.GetBytes("B");

        var d1 = new Document
        {
            Id = Guid.NewGuid(),
            Name = "yes",
            OriginalFileName = "yes.txt",
            ContentType = "text/plain; charset=utf-8",
            Content = aBytes,
            SizeBytes = aBytes.LongLength,
            UploadTime = DateTime.UtcNow
        };
        var d2 = new Document
        {
            Id = Guid.NewGuid(),
            Name = "no",
            OriginalFileName = "no.txt",
            ContentType = "text/plain; charset=utf-8",
            Content = bBytes,
            SizeBytes = bBytes.LongLength,
            UploadTime = DateTime.UtcNow
        };

        await _factory.ResetAndSeedAsync(d1, d2);

        var client = _factory.CreateClient();
        var response = await client.GetAsync("/api/documents");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var docs = await response.Content.ReadFromJsonAsync<List<DocumentListItemDto>>();
        Assert.NotNull(docs);
        Assert.Equal(2, docs!.Count);
        Assert.Contains(docs, d => d.Name == "yes");
        Assert.Contains(docs, d => d.Name == "no");
    }

    private static MultipartFormDataContent BuildMultipart(string? name, string fileName, string content, string contentType = "text/plain")
    {
        var fd = new MultipartFormDataContent();
        if (!string.IsNullOrWhiteSpace(name))
            fd.Add(new StringContent(name), "name");

        var bytes = Encoding.UTF8.GetBytes(content ?? "");
        var filePart = new ByteArrayContent(bytes);
        filePart.Headers.ContentType = new MediaTypeHeaderValue(contentType);
        fd.Add(filePart, "file", fileName);
        return fd;
    }

    private record CreatedIdDto(Guid Id);

    [Fact]
    public async Task Post_creates_document_and_persists()
    {
        await _factory.ResetAndSeedAsync();

        var client = _factory.CreateClient();

        using var fd = BuildMultipart("neu", "neu.txt", "123", "text/plain");
        var response = await client.PostAsync("/api/documents", fd);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.NotNull(response.Headers.Location);

        var created = await response.Content.ReadFromJsonAsync<CreatedIdDto>();
        Assert.NotNull(created);
        Assert.NotEqual(Guid.Empty, created!.Id);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DMSG3_DbContext>();
        var inDb = db.Documents.SingleOrDefault(d => d.Id == created.Id);
        Assert.NotNull(inDb);
        Assert.Equal("neu", inDb!.Name);
        Assert.Equal("neu.txt", inDb.OriginalFileName);
        Assert.Equal(3, inDb.SizeBytes);
        Assert.Equal("123", Encoding.UTF8.GetString(inDb.Content));
    }

    [Fact]
    public async Task Delete_existing_document_removes_and_returns_204()
    {
        var bytes = Encoding.UTF8.GetBytes("bye");
        var id = Guid.NewGuid();
        await _factory.ResetAndSeedAsync(new Document
        {
            Id = id,
            Name = "delete",
            OriginalFileName = "delete.txt",
            ContentType = "text/plain; charset=utf-8",
            Content = bytes,
            SizeBytes = bytes.LongLength,
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
        await _factory.ResetAndSeedAsync();

        var client = _factory.CreateClient();
        var del = await client.DeleteAsync($"/api/documents/{Guid.NewGuid()}");
        Assert.Equal(HttpStatusCode.NotFound, del.StatusCode);
    }
}