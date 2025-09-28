namespace DMSG3.REST.DTOs;

public record DocumentListItemDto(
    Guid Id,
    string Name,
    DateTime UploadTime,
    long SizeBytes,
    string ContentType
);