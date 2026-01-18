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

        public string StorageBucket { get; set; } = null!;

        public string StorageObjectName { get; set; } = null!;

        public string OcrStatus { get; set; } = DocumentOcrStatus.Pending;

        public string? OcrText { get; set; }

        public DateTime? OcrStartedAt { get; set; }

        public DateTime? OcrCompletedAt { get; set; }

        public string? OcrError { get; set; }

        public string SummaryStatus { get; set; } = DocumentSummaryStatus.Pending;

        public string? SummaryText { get; set; }

        public DateTime? SummaryCompletedAt { get; set; }

        public string? SummaryError { get; set; }

        public string VirusScanStatus { get; set; } = DocumentVirusScanStatus.NotScanned;

        public DateTime? VirusScanStartedAt { get; set; }

        public DateTime? VirusScanCompletedAt { get; set; }

        public string? VirusScanError { get; set; }

        public string? VirusScanAnalysisId { get; set; }

        public DateTime UploadTime { get; set; } = DateTime.UtcNow;
    }
}
