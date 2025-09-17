using Microsoft.EntityFrameworkCore;
using DMSG3.Domain.Entities;
using DMSG3.Infrastructure;
using DMSG3.REST.DTOs;

var builder = WebApplication.CreateBuilder(args);

// wenn test env dann in-memory, sonst normale
if (builder.Environment.IsEnvironment("Testing"))
{
    builder.Services.AddDbContext<DMSG3_DbContext>(opt =>
        opt.UseInMemoryDatabase("DMSG3_TestDb"));
}
else
{
    builder.Services.AddDbContext<DMSG3_DbContext>(opt =>
        opt.UseNpgsql(builder.Configuration.GetConnectionString("Default"),
            o => o.EnableRetryOnFailure()));
}

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
}

// pgcrypto und Migrs
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<DMSG3_DbContext>();

    // nur bei rel-db migrieren und sql; tests in memory
    var providerName = db.Database.ProviderName;
    if (!string.Equals(providerName, "Microsoft.EntityFrameworkCore.InMemory", StringComparison.OrdinalIgnoreCase))
    {
        try
        {
            db.Database.ExecuteSqlRaw("CREATE EXTENSION IF NOT EXISTS pgcrypto;");
        }
        catch (Exception ex)
        {
            app.Logger.LogWarning(ex, "pgcrypto konnte nicht erstellt werden, REST/Program.cs/Z37 (17.09).");
        }
        db.Database.Migrate();
    }
}

app.UseSwagger();
app.UseSwaggerUI();

app.UseSwagger();
app.UseSwaggerUI();

// Health Endpoint http://localhost:port/health
app.MapGet("/health", () => Results.Ok("healthy"));

// API-Gruppe für alle Endpoints, die unter api/ erreichbar sind
var api = app.MapGroup("/api");

// CRUD-Dokumentendpoints
api.MapGet("/documents", async (DMSG3_DbContext db)
        => await db.Documents.ToListAsync());

api.MapGet("/documents/{id:guid}", async (Guid id, DMSG3_DbContext db)
        => await db.Documents.FindAsync(id) is { } d ? Results.Ok(d) : Results.NotFound());

api.MapPost("/documents", async (DocumentDto dto, DMSG3_DbContext db) =>
{
    var doc = new Document
    {
        Id = Guid.NewGuid(),
        FileName = dto.FileName,
        FileContent = dto.FileContent
    };

    db.Documents.Add(doc);
    await db.SaveChangesAsync();
    return Results.Created($"/api/documents/{doc.Id}", doc);
});

api.MapDelete("/documents/{id:guid}", async (Guid id, DMSG3_DbContext db) =>
{
    var doc = await db.Documents.FindAsync(id);
    if (doc is null) return Results.NotFound();
    db.Remove(doc);
    await db.SaveChangesAsync();
    return Results.NoContent();
});

app.Run();

public partial class Program { }