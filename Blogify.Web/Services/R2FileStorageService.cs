using System.Net;
using Amazon.S3;
using Amazon.S3.Model;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;

namespace Blogify.Web.Services;

public sealed class R2FileStorageService(
    HttpClient httpClient,
    IAmazonS3 s3Client,
    IOptions<StorageOptions> storageOptions,
    ImageStorageProcessor imageProcessor) : IFileStorageService
{
    private readonly string _bucketName = RequireValue(storageOptions.Value.R2.BucketName, "Storage:R2:BucketName");
    private readonly string _publicBaseUrl = StoragePathHelper.NormalizePublicBaseUrl(
        RequireValue(storageOptions.Value.R2.PublicBaseUrl, "Storage:R2:PublicBaseUrl"));

    public async Task<string> SaveAsync(IFormFile file, Guid tenantId, CancellationToken ct = default)
    {
        imageProcessor.ValidateUpload(file);

        ProcessedImage stored = await imageProcessor.CreateStoredImageAsync(file, ct);
        string fileName = $"{Guid.NewGuid()}{stored.FileExtension}";
        string objectKey = StoragePathHelper.BuildR2ObjectKey(tenantId, fileName);

        await PutObjectAsync(objectKey, stored.Bytes, stored.ContentType, ct);
        return BuildPublicUrl(objectKey);
    }

    public async Task DeleteAsync(string url, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(url);

        string objectKey = GetObjectKey(url);
        await DeleteObjectAsync(objectKey, ct);

        string? thumbnailKey = StoragePathHelper.TryGetThumbnailKeyFromObjectKey(objectKey);
        if (thumbnailKey is not null)
        {
            await DeleteObjectAsync(thumbnailKey, ct);
        }
    }

    public async Task<string?> SaveThumbnailAsync(
        string sourceUrl,
        Guid tenantId,
        int maxWidthPx,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(sourceUrl);

        using HttpResponseMessage response = await httpClient.GetAsync(sourceUrl, ct);
        if (!response.IsSuccessStatusCode)
        {
            return null;
        }

        await using Stream source = await response.Content.ReadAsStreamAsync(ct);
        ProcessedImage? thumbnail = await imageProcessor.CreateThumbnailAsync(source, maxWidthPx, ct);
        if (thumbnail is null)
        {
            return null;
        }

        string fileName = $"{Guid.NewGuid()}{thumbnail.FileExtension}";
        string thumbnailKey = StoragePathHelper.BuildR2ThumbnailKey(tenantId, fileName);
        await PutObjectAsync(thumbnailKey, thumbnail.Bytes, thumbnail.ContentType, ct);
        return BuildPublicUrl(thumbnailKey);
    }

    public async Task<(int Width, int Height)?> GetImageDimensionsAsync(
        string sourceUrl,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(sourceUrl);

        using HttpResponseMessage response = await httpClient.GetAsync(sourceUrl, ct);
        if (!response.IsSuccessStatusCode)
        {
            return null;
        }

        await using Stream input = await response.Content.ReadAsStreamAsync(ct);
        return await imageProcessor.GetDimensionsAsync(input, ct);
    }

    private async Task PutObjectAsync(
        string objectKey,
        byte[] data,
        string contentType,
        CancellationToken ct)
    {
        using MemoryStream input = new(data, writable: false);
        PutObjectRequest request = new()
        {
            BucketName = _bucketName,
            Key = objectKey,
            InputStream = input,
            ContentType = contentType,
            AutoCloseStream = false,
            DisablePayloadSigning = true,
            DisableDefaultChecksumValidation = true
        };

        await s3Client.PutObjectAsync(request, ct);
    }

    private async Task DeleteObjectAsync(string objectKey, CancellationToken ct)
    {
        DeleteObjectRequest request = new()
        {
            BucketName = _bucketName,
            Key = objectKey
        };

        try
        {
            await s3Client.DeleteObjectAsync(request, ct);
        }
        catch (AmazonS3Exception exception) when (exception.StatusCode == HttpStatusCode.NotFound)
        {
        }
    }

    private string BuildPublicUrl(string objectKey) =>
        new Uri(new Uri(_publicBaseUrl, UriKind.Absolute), StoragePathHelper.EscapeObjectKey(objectKey)).ToString();

    private string GetObjectKey(string url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out Uri? absoluteUri))
        {
            throw new ArgumentException("URL must be absolute.", nameof(url));
        }

        return absoluteUri.AbsolutePath.TrimStart('/');
    }

    private static string RequireValue(string? value, string name) =>
        !string.IsNullOrWhiteSpace(value)
            ? value
            : throw new InvalidOperationException($"Configuration value '{name}' was not found.");
}
