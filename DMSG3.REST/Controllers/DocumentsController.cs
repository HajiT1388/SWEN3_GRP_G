using System.IO;
using DMSG3.Domain.Entities;
using DMSG3.Infrastructure;
using DMSG3.REST.DTOs;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DMSG3.REST.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class DocumentsController : ControllerBase
    {
        private readonly DMSG3_DbContext _db;
        private static readonly HashSet<string> AllowedExtensions = new(StringComparer.OrdinalIgnoreCase) { ".pdf", ".txt" };

        public DocumentsController(DMSG3_DbContext db)
        {
            _db = db;
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<DocumentListItemDto>>> GetAll(CancellationToken ct)
        {
            var items = await _db.Documents
                .AsNoTracking()
                .OrderByDescending(d => d.UploadTime)
                .Select(d => new DocumentListItemDto(
                    d.Id,
                    d.Name,
                    d.UploadTime,
                    d.SizeBytes,
                    d.ContentType
                ))
                .ToListAsync(ct);

            return Ok(items);
        }

        [HttpGet("{id:guid}")]
        public async Task<ActionResult<DocumentDetailsDto>> GetById(Guid id, CancellationToken ct)
        {
            var doc = await _db.Documents
                .AsNoTracking()
                .Where(d => d.Id == id)
                .Select(d => new DocumentDetailsDto(
                    d.Id,
                    d.Name,
                    d.OriginalFileName,
                    d.ContentType,
                    d.SizeBytes,
                    d.UploadTime
                ))
                .FirstOrDefaultAsync(ct);

            if (doc is null) return NotFound();
            return Ok(doc);
        }

        [HttpGet("{id:guid}/download")]
        public async Task<IActionResult> Download(Guid id, [FromQuery] bool inline = false, CancellationToken ct = default)
        {
            var doc = await _db.Documents.AsNoTracking().FirstOrDefaultAsync(d => d.Id == id, ct);
            if (doc is null) return NotFound();

            if (inline)
            {
                return File(doc.Content, doc.ContentType);
            }

            var ext = Path.GetExtension(doc.OriginalFileName);
            var suggestedName = string.IsNullOrWhiteSpace(ext) ? doc.Name : $"{doc.Name}{ext}";
            return File(doc.Content, doc.ContentType, suggestedName);
        }

        [HttpPost]
        [Consumes("multipart/form-data")]
        [RequestSizeLimit(50_000_000)] // TODO Ändern in nginx Konfig, wenn noch nicht, glaube da noch 20
        public async Task<ActionResult<object>> Create([FromForm] DocumentUploadRequest request, CancellationToken ct)
        {
            if (request?.File == null || request.File.Length == 0)
                return BadRequest("Datei fehlt.");

            var ext = Path.GetExtension(request.File.FileName);
            if (string.IsNullOrWhiteSpace(ext) || !AllowedExtensions.Contains(ext))
                return BadRequest("Es sind nur .pdf und .txt erlaubt.");

            var name = (request.Name ?? "").Trim();
            if (string.IsNullOrWhiteSpace(name))
            {
                name = Path.GetFileNameWithoutExtension(request.File.FileName);
            }
            if (string.IsNullOrWhiteSpace(name))
                return BadRequest("Name konnte nicht ermittelt werden.");

            var contentType = !string.IsNullOrWhiteSpace(request.File.ContentType)
                ? request.File.ContentType
                : (ext.Equals(".pdf", StringComparison.OrdinalIgnoreCase) ? "application/pdf" : "text/plain; charset=utf-8");

            await using var ms = new MemoryStream();
            await request.File.CopyToAsync(ms, ct);
            var bytes = ms.ToArray();

            var doc = new Document
            {
                Name = name,
                OriginalFileName = request.File.FileName,
                ContentType = contentType,
                SizeBytes = bytes.LongLength,
                Content = bytes
            };

            _db.Documents.Add(doc);
            await _db.SaveChangesAsync(ct);

            return CreatedAtAction(nameof(GetById), new { id = doc.Id }, new { id = doc.Id });
        }

        [HttpDelete("{id:guid}")]
        public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
        {
            var doc = await _db.Documents.FirstOrDefaultAsync(d => d.Id == id, ct);
            if (doc is null) return NotFound();

            _db.Documents.Remove(doc);
            await _db.SaveChangesAsync(ct);
            return NoContent();
        }
    }
}