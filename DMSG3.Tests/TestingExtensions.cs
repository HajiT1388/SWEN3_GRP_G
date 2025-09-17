using System.Threading.Tasks;
using DMSG3.Domain.Entities;
using DMSG3.Infrastructure;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace DMSG3.Tests;

public static class TestingExtensions
{
    public static async Task ResetAndSeedAsync(this WebApplicationFactory<Program> factory, params Document[] docs)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DMSG3_DbContext>();

        await db.Database.EnsureDeletedAsync();
        await db.Database.EnsureCreatedAsync();

        if (docs is { Length: > 0 })
        {
            await db.AddRangeAsync(docs);
            await db.SaveChangesAsync();
        }
    }
}