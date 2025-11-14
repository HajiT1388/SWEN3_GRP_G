using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using DMSG3.Domain.Entities;
using DMSG3.Domain.Messaging;
using DMSG3.Infrastructure;
using DMSG3.Infrastructure.Storage;
using DMSG3.Worker.Ocr;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace DMSG3.Tests.Worker;

public class OcrRequestHandlerTests
{
    private static DMSG3_DbContext BuildDbContext(string dbName)
    {
        var options = new DbContextOptionsBuilder<DMSG3_DbContext>()
            .UseInMemoryDatabase(dbName)
            .Options;
        return new DMSG3_DbContext(options);
    }

    [Fact]
    public async Task Processes_plain_text_without_ocr_engine()
    {
        await using var db = BuildDbContext(nameof(Processes_plain_text_without_ocr_engine));
        var storage = new InMemoryDocumentStorage();
        var fakeEngine = new FakeOcrEngine("SHOULD_NOT_BE_USED");
        var handler = new OcrRequestHandler(db, storage, fakeEngine, NullLogger<OcrRequestHandler>.Instance);

        var doc = new Document
        {
            Id = Guid.NewGuid(),
            Name = "txt",
            OriginalFileName = "txt.txt",
            ContentType = "text/plain",
            SizeBytes = 4,
            StorageBucket = "documents",
            StorageObjectName = "txt.txt"
        };

        db.Documents.Add(doc);
        await db.SaveChangesAsync();

        await storage.UploadAsync(doc, new MemoryStream(Encoding.UTF8.GetBytes("text")), CancellationToken.None);

        var message = new OcrRequestMessage(doc.Id, doc.OriginalFileName, doc.ContentType, doc.SizeBytes, DateTime.UtcNow);
        var result = await handler.ProcessAsync(message, CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal(DocumentOcrStatus.Completed, doc.OcrStatus);
        Assert.Equal("text", doc.OcrText);
        Assert.Equal(0, fakeEngine.InvocationCount);
    }

    [Fact]
    public async Task Invokes_ocr_engine_for_pdf_and_updates_document()
    {
        await using var db = BuildDbContext(nameof(Invokes_ocr_engine_for_pdf_and_updates_document));
        var storage = new InMemoryDocumentStorage();
        var fakeEngine = new FakeOcrEngine("PDF OCR TEXT");
        var handler = new OcrRequestHandler(db, storage, fakeEngine, NullLogger<OcrRequestHandler>.Instance);

        var doc = new Document
        {
            Id = Guid.NewGuid(),
            Name = "pdf",
            OriginalFileName = "sample.pdf",
            ContentType = "application/pdf",
            SizeBytes = 10,
            StorageBucket = "documents",
            StorageObjectName = "sample.pdf"
        };

        db.Documents.Add(doc);
        await db.SaveChangesAsync();

        await storage.UploadAsync(doc, new MemoryStream(new byte[] { 1, 2, 3, 4 }), CancellationToken.None);

        var message = new OcrRequestMessage(doc.Id, doc.OriginalFileName, doc.ContentType, doc.SizeBytes, DateTime.UtcNow);
        var result = await handler.ProcessAsync(message, CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal(DocumentOcrStatus.Completed, doc.OcrStatus);
        Assert.Equal("PDF OCR TEXT", doc.OcrText);
        Assert.Equal(1, fakeEngine.InvocationCount);
    }

    private sealed class FakeOcrEngine : IOcrEngine
    {
        private readonly string _text;
        public int InvocationCount { get; private set; }

        public FakeOcrEngine(string text)
        {
            _text = text;
        }

        public Task<string> RecognizePdfAsync(string pdfPath, CancellationToken ct)
        {
            InvocationCount++;
            Assert.True(File.Exists(pdfPath));
            return Task.FromResult(_text);
        }
    }
}