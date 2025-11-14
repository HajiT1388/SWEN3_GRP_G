using System;
using System.Threading;
using System.Threading.Tasks;
using DMSG3.Infrastructure.Storage;
using DMSG3.REST.Messaging;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace DMSG3.Tests;

public class TestWebApplicationFactory : WebApplicationFactory<Program>
{
    public Guid SeededDocumentId { get; } = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");

    public InMemoryDocumentStorage DocumentStorage =>
        (InMemoryDocumentStorage)Services.GetRequiredService<IDocumentStorage>();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.ConfigureServices(services =>
        {
            services.RemoveAll<IRabbitPublisher>();
            services.AddSingleton<IRabbitPublisher, NoOpRabbitPublisher>();
        });
    }

    private sealed class NoOpRabbitPublisher : IRabbitPublisher
    {
        public Task PublishAsync<T>(string routingKey, T message, CancellationToken ct = default) => Task.CompletedTask;
    }
}