using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using System;

namespace DMSG3.Tests;

public class TestWebApplicationFactory : WebApplicationFactory<Program>
{
    public Guid SeededDocumentId { get; } = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        // für Program.cs -> inMemDB verwenden
        builder.UseEnvironment("Testing");
    }
}