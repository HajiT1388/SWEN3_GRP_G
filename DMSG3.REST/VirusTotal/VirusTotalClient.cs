using System.Net.Http.Headers;
using System.Text.Json;
using DMSG3.Domain.Entities;
using Microsoft.Extensions.Options;

namespace DMSG3.REST.VirusTotal;

public class VirusTotalClient
{
    private const long LargeFileThresholdBytes = 32L * 1024L * 1024L;
    private readonly HttpClient _httpClient;
    private readonly VirusTotalOptions _options;
    private readonly ILogger<VirusTotalClient> _logger;

    public VirusTotalClient(HttpClient httpClient, IOptions<VirusTotalOptions> options, ILogger<VirusTotalClient> logger)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<VirusScanSubmission> SubmitAsync(Document document, Stream content, CancellationToken ct)
    {
        var apiKey = GetApiKey();

        if (content.CanSeek)
        {
            content.Position = 0;
        }

        var uploadUri = await GetUploadUriAsync(apiKey, document.SizeBytes, ct);
        var analysisId = await UploadFileAsync(apiKey, uploadUri, document, content, ct);

        return new VirusScanSubmission(analysisId);
    }

    public async Task<VirusScanResult> CheckAnalysisAsync(string analysisId, CancellationToken ct)
    {
        var apiKey = GetApiKey();
        var analysis = await GetAnalysisAsync(apiKey, analysisId, ct);

        if (!string.Equals(analysis.Status, "completed", StringComparison.OrdinalIgnoreCase))
        {
            return new VirusScanResult(DocumentVirusScanStatus.Scanning, analysis.Malicious, analysis.Suspicious, analysis.Status);
        }

        var status = analysis.IsMalicious
            ? DocumentVirusScanStatus.Malicious
            : DocumentVirusScanStatus.Clean;

        return new VirusScanResult(status, analysis.Malicious, analysis.Suspicious, analysis.Status);
    }

    private async Task<Uri> GetUploadUriAsync(string apiKey, long sizeBytes, CancellationToken ct)
    {
        if (sizeBytes <= LargeFileThresholdBytes)
        {
            return new Uri("files", UriKind.Relative);
        }

        using var request = new HttpRequestMessage(HttpMethod.Get, "files/upload_url");
        request.Headers.Add("x-apikey", apiKey);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        using var response = await _httpClient.SendAsync(request, ct);
        var payload = await response.Content.ReadAsStringAsync(ct);

        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException($"VirusTotal upload_url fehlgeschlagen ({(int)response.StatusCode}). {Trim(payload)}");
        }

        using var json = JsonDocument.Parse(payload);
        if (!json.RootElement.TryGetProperty("data", out var dataElem))
        {
            throw new InvalidOperationException("VirusTotal upload_url Antwort ohne data.");
        }

        var url = dataElem.GetString();
        if (string.IsNullOrWhiteSpace(url))
        {
            throw new InvalidOperationException("VirusTotal upload_url Antwort ohne URL.");
        }

        return new Uri(url, UriKind.Absolute);
    }

    private async Task<string> UploadFileAsync(string apiKey, Uri uploadUri, Document document, Stream content, CancellationToken ct)
    {
        using var form = new MultipartFormDataContent();
        var streamContent = new StreamContent(content);
        var contentType = string.IsNullOrWhiteSpace(document.ContentType) ? "application/octet-stream" : document.ContentType;
        streamContent.Headers.ContentType = new MediaTypeHeaderValue(contentType);
        form.Add(streamContent, "file", document.OriginalFileName ?? "file");

        using var request = new HttpRequestMessage(HttpMethod.Post, uploadUri)
        {
            Content = form
        };
        request.Headers.Add("x-apikey", apiKey);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        using var response = await _httpClient.SendAsync(request, ct);
        var payload = await response.Content.ReadAsStringAsync(ct);

        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException($"VirusTotal Upload fehlgeschlagen ({(int)response.StatusCode}). {Trim(payload)}");
        }

        var analysisId = ExtractAnalysisId(payload);
        if (string.IsNullOrWhiteSpace(analysisId))
        {
            throw new InvalidOperationException("VirusTotal Upload Antwort ohne Analysis-ID.");
        }

        return analysisId;
    }

    private async Task<VirusAnalysisResult> GetAnalysisAsync(string apiKey, string analysisId, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, $"analyses/{analysisId}");
        request.Headers.Add("x-apikey", apiKey);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        using var response = await _httpClient.SendAsync(request, ct);
        var payload = await response.Content.ReadAsStringAsync(ct);

        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException($"VirusTotal Analyse fehlgeschlagen ({(int)response.StatusCode}). {Trim(payload)}");
        }

        return ParseAnalysis(payload);
    }

    private string GetApiKey()
    {
        var apiKey = (_options.ApiKey ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            throw new InvalidOperationException("VT_API_KEY fehlt.");
        }

        return apiKey;
    }

    private static string? ExtractAnalysisId(string payload)
    {
        using var json = JsonDocument.Parse(payload);
        if (!json.RootElement.TryGetProperty("data", out var dataElem))
        {
            return null;
        }

        if (dataElem.TryGetProperty("id", out var idElem))
        {
            return idElem.GetString();
        }

        return null;
    }

    private static VirusAnalysisResult ParseAnalysis(string payload)
    {
        using var json = JsonDocument.Parse(payload);
        if (!json.RootElement.TryGetProperty("data", out var dataElem))
        {
            throw new InvalidOperationException("VirusTotal Analyse Antwort ohne data.");
        }

        if (!dataElem.TryGetProperty("attributes", out var attributesElem))
        {
            throw new InvalidOperationException("VirusTotal Analyse Antwort ohne attributes.");
        }

        var status = attributesElem.TryGetProperty("status", out var statusElem)
            ? statusElem.GetString() ?? "unknown"
            : "unknown";

        int malicious = 0;
        int suspicious = 0;
        if (attributesElem.TryGetProperty("stats", out var statsElem))
        {
            if (statsElem.TryGetProperty("malicious", out var malElem) && malElem.TryGetInt32(out var mal))
            {
                malicious = mal;
            }
            if (statsElem.TryGetProperty("suspicious", out var susElem) && susElem.TryGetInt32(out var sus))
            {
                suspicious = sus;
            }
        }

        return new VirusAnalysisResult(status, malicious, suspicious);
    }

    private static string Trim(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return string.Empty;
        value = value.Trim();
        return value.Length <= 256 ? value : value[..256] + "...";
    }

    private sealed record VirusAnalysisResult(string Status, int Malicious, int Suspicious)
    {
        public bool IsMalicious => Malicious > 0 || Suspicious > 0;
    }
}

public record VirusScanSubmission(string AnalysisId);

public record VirusScanResult(string Status, int MaliciousCount, int SuspiciousCount, string AnalysisStatus);
