using System.Linq;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Configuration;

namespace DMSG3.GenAIWorker.Summaries;

public class GeminiGenAiClient : IGenAiClient
{
    private readonly HttpClient _httpClient;
    private readonly IConfiguration _configuration;
    private readonly ILogger<GeminiGenAiClient> _logger;
    private readonly JsonSerializerOptions _serializerOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public GeminiGenAiClient(HttpClient httpClient, IConfiguration configuration, ILogger<GeminiGenAiClient> logger)
    {
        _httpClient = httpClient;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task<string> SummarizeAsync(string text, CancellationToken ct)
    {
        var apiKey = (_configuration["GenAi:ApiKey"] ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            throw new GenAiClientException("GenAI API-Key fehlt.", isTransient: false);
        }

        var promptConfig = _configuration["GenAi:SystemPrompt"];
        var prompt = string.IsNullOrWhiteSpace(promptConfig)
            ? "Fasse den Dokumententext kurz und sachlich auf deutsch zusammen. Verwende kein Markdown, sondern ausschließlich unformatierten Text."
            : promptConfig.Trim();

        var modelConfig = _configuration["GenAi:Model"];
        var model = string.IsNullOrWhiteSpace(modelConfig)
            ? "gemini-2.0-flash-lite"
            : modelConfig.Trim();

        var temperature = _configuration.GetValue<double?>("GenAi:Temperature") ?? 0.2;
        var maxTokens = _configuration.GetValue<int?>("GenAi:MaxOutputTokens") ?? 1024;

        var body = new GeminiRequest
        {
            Contents = new[]
            {
                new GeminiContent
                {
                    Role = "user",
                    Parts = new[]
                    {
                        new GeminiPart { Text = $"{prompt}\n\n{text}" }
                    }
                }
            },
            GenerationConfig = new GeminiGenerationConfig
            {
                Temperature = temperature,
                MaxOutputTokens = maxTokens
            }
        };

        var requestUri = new Uri($"v1beta/models/{model}:generateContent?key={Uri.EscapeDataString(apiKey)}", UriKind.Relative);

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, requestUri)
            {
                Content = new StringContent(JsonSerializer.Serialize(body, _serializerOptions), Encoding.UTF8, "application/json")
            };
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

            _logger.LogInformation("Sende {Length} Zeichen an GenAI.", text?.Length ?? 0);
            using var response = await _httpClient.SendAsync(request, ct);
            var payload = await response.Content.ReadAsStringAsync(ct);

            if (!response.IsSuccessStatusCode)
            {
                var transient = (int)response.StatusCode >= 500 || response.StatusCode == HttpStatusCode.TooManyRequests;
                throw new GenAiClientException($"Gemini API {response.StatusCode}: {Trim(payload)}", transient, statusCode: response.StatusCode);
            }

            var parsed = JsonSerializer.Deserialize<GeminiResponse>(payload, _serializerOptions);
            var summary = parsed?.Candidates?
                .SelectMany(c => c.Content?.Parts ?? Array.Empty<GeminiPart>())
                .Select(p => p.Text)
                .FirstOrDefault(t => !string.IsNullOrWhiteSpace(t))
                ?.Trim();

            if (string.IsNullOrWhiteSpace(summary))
            {
                var blockReason = parsed?.PromptFeedback?.BlockReason;
                if (!string.IsNullOrWhiteSpace(blockReason))
                {
                    throw new GenAiClientException($"Gemini blockiert: {blockReason}", isTransient: false);
                }

                var blockedCategories = parsed?.Candidates?
                    .SelectMany(c => c.SafetyRatings ?? Array.Empty<GeminiSafetyRating>())
                    .Where(r => r.Blocked == true)
                    .Select(r => r.Category)
                    .Where(c => !string.IsNullOrWhiteSpace(c))
                    .Distinct()
                    .ToArray();

                if (blockedCategories is { Length: > 0 })
                {
                    throw new GenAiClientException(
                        $"Gemini blockiert wegen Sicherheitskategorien: {string.Join(", ", blockedCategories)}",
                        isTransient: false);
                }

                throw new GenAiClientException($"Gemini-Antwort enthielt keinen Text. Payload: {Trim(payload)}", isTransient: false);
            }

            _logger.LogInformation("GenAI Zusammenfassung empfangen ({Length} Zeichen).", summary.Length);
            return summary;
        }
        catch (OperationCanceledException ex) when (!ct.IsCancellationRequested)
        {
            throw new GenAiClientException("GenAI Request Timeout.", isTransient: true, ex);
        }
        catch (HttpRequestException ex)
        {
            throw new GenAiClientException("GenAI HTTP-Fehler.", isTransient: true, ex);
        }
    }

    private static string Trim(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return string.Empty;
        value = value.Trim();
        return value.Length <= 256 ? value : value[..256] + "...";
    }

    private sealed class GeminiRequest
    {
        public GeminiContent[] Contents { get; set; } = Array.Empty<GeminiContent>();
        public GeminiGenerationConfig? GenerationConfig { get; set; }
    }

    private sealed class GeminiContent
    {
        public string? Role { get; set; }
        public GeminiPart[] Parts { get; set; } = Array.Empty<GeminiPart>();
    }

    private sealed class GeminiPart
    {
        public string? Text { get; set; }
    }

    private sealed class GeminiGenerationConfig
    {
        public double Temperature { get; set; }
        public int MaxOutputTokens { get; set; }
    }

    private sealed class GeminiResponse
    {
        public GeminiCandidate[]? Candidates { get; set; }
        public GeminiPromptFeedback? PromptFeedback { get; set; }
    }

    private sealed class GeminiCandidate
    {
        public GeminiContent? Content { get; set; }
        public GeminiSafetyRating[]? SafetyRatings { get; set; }
    }

    private sealed class GeminiPromptFeedback
    {
        public string? BlockReason { get; set; }
        public GeminiSafetyRating[]? SafetyRatings { get; set; }
    }

    private sealed class GeminiSafetyRating
    {
        public string? Category { get; set; }
        public string? Probability { get; set; }
        public bool? Blocked { get; set; }
    }
}