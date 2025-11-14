using System.Diagnostics;
using System.Text;

namespace DMSG3.Worker.Ocr;

public interface IProcessRunner
{
    Task<ProcessResult> RunAsync(string command, IEnumerable<string> arguments, bool captureOutput, CancellationToken ct);
}

public record ProcessResult(int ExitCode, string StandardOutput, string StandardError);

public sealed class ProcessRunner : IProcessRunner
{
    public async Task<ProcessResult> RunAsync(string command, IEnumerable<string> arguments, bool captureOutput, CancellationToken ct)
    {
        var psi = new ProcessStartInfo
        {
            FileName = command,
            RedirectStandardOutput = captureOutput,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        foreach (var arg in arguments)
        {
            psi.ArgumentList.Add(arg);
        }

        using var process = Process.Start(psi) ?? throw new InvalidOperationException($"Prozess {command} konnte nicht gestartet werden.");

        Task<string> stdOutTask = captureOutput ? process.StandardOutput.ReadToEndAsync() : Task.FromResult(string.Empty);
        var stdErrTask = process.StandardError.ReadToEndAsync();

        await process.WaitForExitAsync(ct);

        var stdOut = captureOutput ? await stdOutTask : string.Empty;
        var stdErr = await stdErrTask;

        var result = new ProcessResult(process.ExitCode, stdOut, stdErr);
        if (result.ExitCode != 0)
        {
            throw new ProcessRunException(command, arguments.ToArray(), result);
        }

        return result;
    }
}

public class ProcessRunException : Exception
{
    public ProcessRunException(string command, IReadOnlyList<string> args, ProcessResult result)
        : base($"Prozess '{command}' endete mit Code {result.ExitCode}. {result.StandardError}")
    {
        Command = command;
        Arguments = args;
        Result = result;
    }

    public string Command { get; }
    public IReadOnlyList<string> Arguments { get; }
    public ProcessResult Result { get; }
}