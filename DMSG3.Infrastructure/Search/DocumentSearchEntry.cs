using System;

namespace DMSG3.Infrastructure.Search;

public class DocumentSearchEntry
{
    public Guid Id { get; set; }
    public string? Name { get; set; }
    public string? OriginalFileName { get; set; }
    public string? OcrText { get; set; }
}
