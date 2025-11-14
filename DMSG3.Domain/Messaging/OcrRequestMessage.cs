using System;

namespace DMSG3.Domain.Messaging;

public record OcrRequestMessage(
    Guid DocumentId,
    string OriginalFileName,
    string ContentType,
    long SizeBytes,
    DateTime UploadedAtUtc
);