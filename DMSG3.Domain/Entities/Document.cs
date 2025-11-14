using System;

namespace DMSG3.Domain.Entities
{
    public class Document
    {
        public Guid Id { get; set; }

        public string Name { get; set; } = null!;

        public string OriginalFileName { get; set; } = null!;

        public string ContentType { get; set; } = null!;

        public long SizeBytes { get; set; }

        public byte[] Content { get; set; } = null!;

        public DateTime UploadTime { get; set; } = DateTime.UtcNow;
    }
}