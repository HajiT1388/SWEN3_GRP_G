namespace DMSG3.GenAIWorker.Summaries;

public interface IGenAiClient
{
    Task<string> SummarizeAsync(string text, CancellationToken ct);
}