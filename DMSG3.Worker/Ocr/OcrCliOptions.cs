namespace DMSG3.Worker.Ocr;

public class OcrCliOptions
{
    public string GhostscriptExecutable { get; set; } = "gs";
    public string TesseractExecutable { get; set; } = "tesseract";
    public string Language { get; set; } = "eng";
    public int ImageDpi { get; set; } = 300;
    public int? PageSegmentationMode { get; set; }
}