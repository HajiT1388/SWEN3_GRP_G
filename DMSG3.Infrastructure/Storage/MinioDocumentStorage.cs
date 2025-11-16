using DMSG3.Domain.Entities;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Minio;
using Minio.DataModel.Args;

namespace DMSG3.Infrastructure.Storage;

public class MinioDocumentStorage : IDocumentStorage
{
    private readonly IMinioClient _client;
    private readonly MinioOptions _options;
    private readonly ILogger<MinioDocumentStorage> _logger;
    private bool _bucketChecked;
    private readonly SemaphoreSlim _bucketLock = new(1, 1);

    public MinioDocumentStorage(IMinioClient client, IOptions<MinioOptions> options, ILogger<MinioDocumentStorage> logger)
    {
        _client = client;
        _options = options.Value;
        _logger = logger;
    }

    public async Task UploadAsync(Document document, Stream content, CancellationToken ct = default)
    {
        await EnsureBucketAsync(ct);

        var bucket = ResolveBucket(document);
        using var ms = new MemoryStream();
        await content.CopyToAsync(ms, ct);
        ms.Position = 0;

        var args = new PutObjectArgs()
            .WithBucket(bucket)
            .WithObject(document.StorageObjectName)
            .WithStreamData(ms)
            .WithObjectSize(ms.Length)
            .WithContentType(document.ContentType);

        await _client.PutObjectAsync(args);
        _logger.LogInformation("Datei in MinIO hochgeladen. Bucket={Bucket} Object={Object}", bucket, document.StorageObjectName);
    }

    public async Task<Stream> DownloadAsync(Document document, CancellationToken ct = default)
    {
        await EnsureBucketAsync(ct);
        var ms = new MemoryStream();
        var args = new GetObjectArgs()
            .WithBucket(ResolveBucket(document))
            .WithObject(document.StorageObjectName)
            .WithCallbackStream(stream => stream.CopyTo(ms));

        await _client.GetObjectAsync(args);
        ms.Position = 0;
        return ms;
    }

    public async Task DeleteAsync(Document document, CancellationToken ct = default)
    {
        await EnsureBucketAsync(ct);
        var args = new RemoveObjectArgs()
            .WithBucket(ResolveBucket(document))
            .WithObject(document.StorageObjectName);

        await _client.RemoveObjectAsync(args);
        _logger.LogInformation("Datei aus MinIO entfernt. Bucket={Bucket} Object={Object}", ResolveBucket(document), document.StorageObjectName);
    }

    private string ResolveBucket(Document document) =>
        string.IsNullOrWhiteSpace(document.StorageBucket) ? _options.BucketName : document.StorageBucket;

    private async Task EnsureBucketAsync(CancellationToken ct)
    {
        if (_bucketChecked) return;

        await _bucketLock.WaitAsync(ct);
        try
        {
            if (_bucketChecked) return;

            var bucketName = _options.BucketName;
            var exists = await _client.BucketExistsAsync(
                new BucketExistsArgs().WithBucket(bucketName));

            if (!exists)
            {
                await _client.MakeBucketAsync(
                    new MakeBucketArgs().WithBucket(bucketName));
                _logger.LogInformation("MinIO Bucket erstellt: {Bucket}", bucketName);
            }

            _bucketChecked = true;
        }
        finally
        {
            _bucketLock.Release();
        }
    }
}