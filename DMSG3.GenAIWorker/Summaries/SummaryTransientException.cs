namespace DMSG3.GenAIWorker.Summaries;

public class SummaryTransientException : Exception
{
    public TimeSpan? Delay { get; }

    public SummaryTransientException(string message, Exception? inner = null, TimeSpan? delay = null)
        : base(message, inner)
    {
        Delay = delay;
    }
}