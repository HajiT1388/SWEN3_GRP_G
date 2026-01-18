using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using DMSG3.Domain.Entities;
using DMSG3.Infrastructure;
using DMSG3.Infrastructure.Search;
using DMSG3.Infrastructure.Storage;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace DMSG3.Tests;

public static class TestingExtensions
{
    public static async Task ResetAndSeedAsync(this WebApplicationFactory<Program> factory, params SeedDocument[] docs)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DMSG3_DbContext>();
        var storage = scope.ServiceProvider.GetRequiredService<IDocumentStorage>();
        var searchIndex = scope.ServiceProvider.GetService<IDocumentSearchIndex>();

        if (storage is InMemoryDocumentStorage inMemory)
        {
            inMemory.Clear();
        }
        if (searchIndex is InMemoryDocumentSearchIndex inMemorySearch)
        {
            inMemorySearch.Clear();
        }

        await db.Database.EnsureDeletedAsync();
        await db.Database.EnsureCreatedAsync();

        if (docs is { Length: > 0 })
        {
            foreach (var seed in docs)
            {
                if (seed.Document.Id == Guid.Empty)
                {
                    seed.Document.Id = Guid.NewGuid();
                }

                if (string.IsNullOrWhiteSpace(seed.Document.StorageBucket))
                {
                    seed.Document.StorageBucket = "documents";
                }

                if (string.IsNullOrWhiteSpace(seed.Document.StorageObjectName))
                {
                    var ext = Path.GetExtension(seed.Document.OriginalFileName);
                    seed.Document.StorageObjectName = $"{seed.Document.Id:N}{(string.IsNullOrWhiteSpace(ext) ? ".bin" : ext)}";
                }

                db.Documents.Add(seed.Document);
                await storage.UploadAsync(seed.Document, new MemoryStream(seed.Content), CancellationToken.None);
            }

            await db.SaveChangesAsync();

            if (searchIndex is not null)
            {
                foreach (var seed in docs)
                {
                    var entry = new DocumentSearchEntry
                    {
                        Id = seed.Document.Id,
                        Name = seed.Document.Name,
                        OriginalFileName = seed.Document.OriginalFileName,
                        OcrText = seed.Document.OcrText
                    };

                    await searchIndex.IndexAsync(entry, CancellationToken.None);
                }
            }
        }
    }
}

public record SeedDocument(Document Document, byte[] Content);