namespace DMSG3.REST.DTOs;

public record DocumentListItemDto(
    Guid Id,
    string Name,
    DateTime UploadTime,
    long SizeBytes,
    string ContentType,
    string OcrStatus,
    DateTime? OcrCompletedAt,
    string SummaryStatus,
    DateTime? SummaryCompletedAt,
    string? SummaryError,
    string VirusScanStatus,
    DateTime? VirusScanCompletedAt
);
