namespace DMSG3.Domain.Entities;

public static class DocumentVirusScanStatus
{
    public const string NotScanned = "NotScanned";
    public const string Scanning = "Scanning";
    public const string Clean = "Clean";
    public const string Malicious = "Malicious";
    public const string Failed = "Failed";
}
