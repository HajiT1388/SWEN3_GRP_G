using System;
using System.Threading;
using System.Threading.Tasks;
using DMSG3.Domain.Entities;
using DMSG3.Domain.Messaging;
using DMSG3.Infrastructure;
using DMSG3.Infrastructure.Search;
using DMSG3.Worker.Indexing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace DMSG3.Tests.Worker;

public class DocumentIndexingHandlerTests
{
    private static DMSG3_DbContext BuildDbContext(string dbName)
    {
        var options = new DbContextOptionsBuilder<DMSG3_DbContext>()
            .UseInMemoryDatabase(dbName)
            .Options;
        return new DMSG3_DbContext(options);
    }

    [Fact]
    public async Task Indexing_handler_indexes_document_fields()
    {
        await using var db = BuildDbContext(nameof(Indexing_handler_indexes_document_fields));
        var searchIndex = new InMemoryDocumentSearchIndex();
        var handler = new DocumentIndexingHandler(db, searchIndex, NullLogger<DocumentIndexingHandler>.Instance);

        var doc = new Document
        {
            Id = Guid.NewGuid(),
            Name = "Test",
            OriginalFileName = "test.txt",
            ContentType = "text/plain",
            SizeBytes = 10,
            StorageBucket = "documents",
            StorageObjectName = "test.txt",
            OcrStatus = DocumentOcrStatus.Completed,
            OcrText = "Gruppe Gee"
        };

        db.Documents.Add(doc);
        await db.SaveChangesAsync();

        await handler.ProcessAsync(new IndexRequestMessage(doc.Id), CancellationToken.None);

        var hits = await searchIndex.SearchAsync("Gee", 10, CancellationToken.None);
        Assert.Contains(hits, h => h.Id == doc.Id);
    }
}