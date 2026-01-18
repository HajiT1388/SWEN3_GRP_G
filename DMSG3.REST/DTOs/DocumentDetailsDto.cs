namespace DMSG3.REST.DTOs;

public record DocumentDetailsDto(
    Guid Id,
    string Name,
    string OriginalFileName,
    string ContentType,
    long SizeBytes,
    DateTime UploadTime,
    string OcrStatus,
    DateTime? OcrCompletedAt,
    string? OcrText,
    string? OcrError,
    string SummaryStatus,
    DateTime? SummaryCompletedAt,
    string? SummaryText,
    string? SummaryError,
    string VirusScanStatus,
    DateTime? VirusScanCompletedAt,
    string? VirusScanError
);
