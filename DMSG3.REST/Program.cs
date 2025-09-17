using Microsoft.EntityFrameworkCore;
using DMSG3.Domain.Entities;
using DMSG3.Infrastructure;
using DMSG3.REST.DTOs;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<DMSG3_DbContext>(opt =>
    opt.UseNpgsql(builder.Configuration.GetConnectionString("Default"),
        o => o.EnableRetryOnFailure())); // DB noch nicht bereit? NEIN

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
    try
    {
        db.Database.ExecuteSqlRaw("CREATE EXTENSION IF NOT EXISTS pgcrypto;");
    }
    catch (Exception ex)
    {
        app.Logger.LogWarning(ex, "pgcrypto konnte nicht erstellt werden, REST/Program.cs/Z32 (17.09).");
    }

    db.Database.Migrate();
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
// GET api/documents listet alle Dokumente
api.MapGet("/documents", async (DMSG3_DbContext db)
        => await db.Documents.ToListAsync());

// GET api/documents/{guid} gibt ein Dokument oder not found 404
api.MapGet("/documents/{id:guid}", async (Guid id, DMSG3_DbContext db)
        => await db.Documents.FindAsync(id) is { } d ? Results.Ok(d) : Results.NotFound());

// POST api/documents legt ein neues Dokument an
api.MapPost("/documents", async (DocumentDto dto, DMSG3_DbContext db) =>
{
    // DTO -> Domain Entity
    var doc = new Document
    {
        Id = Guid.NewGuid(), // Primary key
        FileName = dto.FileName,
        FileContent = dto.FileContent
    };

    db.Documents.Add(doc);
    await db.SaveChangesAsync();
    return Results.Created($"/api/documents/{doc.Id}", doc); // 201 und Location-Header
});

// DELETE api/documents/{guid} löscht ein Dokument
api.MapDelete("/documents/{id:guid}", async (Guid id, DMSG3_DbContext db) =>
{
    var doc = await db.Documents.FindAsync(id);
    if (doc is null) return Results.NotFound();
    db.Remove(doc);
    await db.SaveChangesAsync();
    return Results.NoContent(); // 204, nichts mehr da 
});

app.Run();