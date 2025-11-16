using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using DMSG3.Domain.Entities;
using DMSG3.Infrastructure;
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

        if (storage is InMemoryDocumentStorage inMemory)
        {
            inMemory.Clear();
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
        }
    }
}

public record SeedDocument(Document Document, byte[] Content);