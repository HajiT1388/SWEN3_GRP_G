namespace DMSG3.Infrastructure.Storage;

public class MinioOptions
{
    public string Endpoint { get; set; } = "localhost:9000";
    public string AccessKey { get; set; } = "minio";
    public string SecretKey { get; set; } = "minio123";
    public bool UseSsl { get; set; }
    public string BucketName { get; set; } = "documents";
}