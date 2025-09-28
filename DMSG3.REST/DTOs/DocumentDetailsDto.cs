namespace DMSG3.REST.DTOs;

public record DocumentDetailsDto(
    Guid Id,
    string Name,
    string OriginalFileName,
    string ContentType,
    long SizeBytes,
    DateTime UploadTime
);