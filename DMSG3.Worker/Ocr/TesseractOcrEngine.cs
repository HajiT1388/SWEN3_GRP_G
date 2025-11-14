using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DMSG3.Worker.Ocr;

public interface IOcrEngine
{
    Task<string> RecognizePdfAsync(string pdfPath, CancellationToken ct);
}

public class TesseractOcrEngine : IOcrEngine
{
    private readonly IProcessRunner _runner;
    private readonly OcrCliOptions _options;
    private readonly ILogger<TesseractOcrEngine> _logger;

    public TesseractOcrEngine(IProcessRunner runner, IOptions<OcrCliOptions> options, ILogger<TesseractOcrEngine> logger)
    {
        _runner = runner;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<string> RecognizePdfAsync(string pdfPath, CancellationToken ct)
    {
        if (!File.Exists(pdfPath))
        {
            throw new FileNotFoundException("PDF für OCR nicht gefunden.", pdfPath);
        }

        var workDir = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), $"ocr-{Guid.NewGuid():N}"));
        var outputPattern = Path.Combine(workDir.FullName, "page-%03d.png");

        try
        {
            var gsArgs = new List<string>
            {
                "-dNOPAUSE",
                "-dBATCH",
                "-sDEVICE=pnggray",
                $"-r{_options.ImageDpi}",
                $"-sOutputFile={outputPattern}",
                pdfPath
            };

            await _runner.RunAsync(_options.GhostscriptExecutable, gsArgs, captureOutput: false, ct);

            var pageFiles = Directory.GetFiles(workDir.FullName, "page-*.png", SearchOption.TopDirectoryOnly)
                .OrderBy(f => f, StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (pageFiles.Count == 0)
            {
                throw new InvalidOperationException("Ghostscript hat keine Seiten erzeugt.");
            }

            var sb = new StringBuilder();
            foreach (var page in pageFiles)
            {
                var tessArgs = BuildTesseractArgs(page);
                var result = await _runner.RunAsync(_options.TesseractExecutable, tessArgs, captureOutput: true, ct);
                if (!string.IsNullOrWhiteSpace(result.StandardOutput))
                {
                    sb.AppendLine(result.StandardOutput.Trim());
                }
            }

            var text = sb.ToString().Trim();
            _logger.LogInformation("OCR abgeschlossen. Pages={Count}", pageFiles.Count);
            return text;
        }
        finally
        {
            try { Directory.Delete(workDir.FullName, recursive: true); } catch { }
        }
    }

    private IEnumerable<string> BuildTesseractArgs(string imagePath)
    {
        var args = new List<string>
        {
            imagePath,
            "stdout",
            "-l",
            _options.Language
        };

        if (_options.PageSegmentationMode.HasValue)
        {
            args.Add("--psm");
            args.Add(_options.PageSegmentationMode.Value.ToString());
        }

        return args;
    }
}