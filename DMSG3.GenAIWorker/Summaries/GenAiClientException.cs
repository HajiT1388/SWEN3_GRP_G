using System.Net;

namespace DMSG3.GenAIWorker.Summaries;

public class GenAiClientException : Exception
{
    public bool IsTransient { get; }
    public HttpStatusCode? StatusCode { get; }

    public GenAiClientException(string message, bool isTransient, Exception? inner = null, HttpStatusCode? statusCode = null)
        : base(message, inner)
    {
        IsTransient = isTransient;
        StatusCode = statusCode;
    }
}