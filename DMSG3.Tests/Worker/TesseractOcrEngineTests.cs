using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DMSG3.Worker.Ocr;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace DMSG3.Tests.Worker;

public class TesseractOcrEngineTests
{
    [Fact]
    public async Task Aggregates_text_from_all_generated_images()
    {
        var runner = new FakeProcessRunner();
        var options = Options.Create(new OcrCliOptions
        {
            GhostscriptExecutable = "gs",
            TesseractExecutable = "tesseract",
            Language = "eng"
        });

        var engine = new TesseractOcrEngine(runner, options, NullLogger<TesseractOcrEngine>.Instance);

        var pdfPath = Path.Combine(Path.GetTempPath(), $"test-{Guid.NewGuid():N}.pdf");
        await File.WriteAllTextAsync(pdfPath, "pdf");

        try
        {
            var text = await engine.RecognizePdfAsync(pdfPath, CancellationToken.None);
            Assert.Equal("tesseract-output-page-001\ntesseract-output-page-002", text.Replace("\r\n", "\n"));
            Assert.Equal(3, runner.Calls.Count); // GS TS TS
        }
        finally
        {
            try { File.Delete(pdfPath); } catch { }
        }
    }

    private sealed class FakeProcessRunner : IProcessRunner
    {
        public List<(string Command, IReadOnlyList<string> Args)> Calls { get; } = new();

        public Task<ProcessResult> RunAsync(string command, IEnumerable<string> arguments, bool captureOutput, CancellationToken ct)
        {
            var args = arguments.ToList();
            Calls.Add((command, args));

            if (command == "gs")
            {
                var outputArg = args.First(a => a.StartsWith("-sOutputFile=", StringComparison.OrdinalIgnoreCase));
                var pattern = outputArg.Split('=')[1];
                var dir = Path.GetDirectoryName(pattern)!;
                Directory.CreateDirectory(dir);
                File.WriteAllText(pattern.Replace("%03d", "001"), string.Empty);
                File.WriteAllText(pattern.Replace("%03d", "002"), string.Empty);
                return Task.FromResult(new ProcessResult(0, string.Empty, string.Empty));
            }

            if (command == "tesseract")
            {
                var imagePath = args.First();
                var fileName = Path.GetFileNameWithoutExtension(imagePath);
                return Task.FromResult(new ProcessResult(0, $"tesseract-output-{fileName}", string.Empty));
            }

            return Task.FromResult(new ProcessResult(0, string.Empty, string.Empty));
        }
    }
}