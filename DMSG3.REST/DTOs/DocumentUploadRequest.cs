namespace DMSG3.REST.DTOs
{
    public class DocumentUploadRequest
    {
        public string? Name { get; set; }
        public IFormFile File { get; set; } = default!;
    }
}