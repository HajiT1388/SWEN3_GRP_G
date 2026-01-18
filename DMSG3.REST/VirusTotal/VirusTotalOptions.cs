namespace DMSG3.REST.VirusTotal;

public class VirusTotalOptions
{
    public string BaseUrl { get; set; } = "https://www.virustotal.com/api/v3/";
    public string? ApiKey { get; set; }
    public int RequestTimeoutSeconds { get; set; } = 60;
    public int AnalysisPollSeconds { get; set; } = 2;
    public int AnalysisMaxWaitSeconds { get; set; } = 25;
}
