using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;

namespace Blogify.Web.Services;

public sealed class R2FileStorageService(
    HttpClient httpClient,
    IOptions<StorageOptions> storageOptions,
    ImageStorageProcessor imageProcessor) : IFileStorageService
{
    private readonly string _bucketName = RequireValue(storageOptions.Value.R2.BucketName, "Storage:R2:BucketName");
    private readonly string _accessKeyId = RequireValue(storageOptions.Value.R2.AccessKeyId, "Storage:R2:AccessKeyId");
    private readonly string _secretAccessKey = RequireValue(storageOptions.Value.R2.SecretAccessKey, "Storage:R2:SecretAccessKey");
    private readonly string _publicBaseUrl = StoragePathHelper.NormalizePublicBaseUrl(
        RequireValue(storageOptions.Value.R2.PublicBaseUrl, "Storage:R2:PublicBaseUrl"));
    private readonly Uri _endpoint = new($"https://{RequireValue(storageOptions.Value.R2.AccountId, "Storage:R2:AccountId")}.r2.cloudflarestorage.com");

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
        string requestPath = $"/{_bucketName}/{StoragePathHelper.EscapeObjectKey(objectKey)}";
        using HttpRequestMessage request = new(HttpMethod.Put, new Uri(_endpoint, requestPath));
        request.Content = new ByteArrayContent(data);
        request.Content.Headers.ContentType = MediaTypeHeaderValue.Parse(contentType);

        AddSigningHeaders(
            request,
            payloadHash: HexHash(data),
            contentType: contentType,
            contentLength: data.LongLength);

        using HttpResponseMessage response = await httpClient.SendAsync(request, ct);
        response.EnsureSuccessStatusCode();
    }

    private async Task DeleteObjectAsync(string objectKey, CancellationToken ct)
    {
        string requestPath = $"/{_bucketName}/{StoragePathHelper.EscapeObjectKey(objectKey)}";
        using HttpRequestMessage request = new(HttpMethod.Delete, new Uri(_endpoint, requestPath));
        AddSigningHeaders(
            request,
            payloadHash: HexHash(Array.Empty<byte>()),
            contentType: null,
            contentLength: 0);

        using HttpResponseMessage response = await httpClient.SendAsync(request, ct);
        if (response.IsSuccessStatusCode || response.StatusCode == HttpStatusCode.NotFound)
        {
            return;
        }

        response.EnsureSuccessStatusCode();
    }

    private void AddSigningHeaders(
        HttpRequestMessage request,
        string payloadHash,
        string? contentType,
        long contentLength)
    {
        DateTimeOffset now = DateTimeOffset.UtcNow;
        string amzDate = now.ToString("yyyyMMdd'T'HHmmss'Z'", CultureInfo.InvariantCulture);
        string dateStamp = now.ToString("yyyyMMdd", CultureInfo.InvariantCulture);

        request.Headers.Host = _endpoint.Host;
        request.Headers.TryAddWithoutValidation("x-amz-content-sha256", payloadHash);
        request.Headers.TryAddWithoutValidation("x-amz-date", amzDate);

        if (request.Content is not null)
        {
            request.Content.Headers.ContentLength = contentLength;
            request.Content.Headers.TryAddWithoutValidation("x-amz-content-sha256", payloadHash);
            if (contentType is not null)
            {
                request.Content.Headers.ContentType = MediaTypeHeaderValue.Parse(contentType);
            }
        }

        string canonicalRequest = BuildCanonicalRequest(request, amzDate, payloadHash, contentType);
        string scope = $"{dateStamp}/auto/s3/aws4_request";
        string stringToSign = BuildStringToSign(amzDate, scope, canonicalRequest);
        string signature = CalculateSignature(dateStamp, stringToSign);
        string signedHeaders = contentType is null
            ? "host;x-amz-content-sha256;x-amz-date"
            : "content-type;host;x-amz-content-sha256;x-amz-date";

        request.Headers.TryAddWithoutValidation(
            "Authorization",
            $"AWS4-HMAC-SHA256 Credential={_accessKeyId}/{scope}, SignedHeaders={signedHeaders}, Signature={signature}");
    }

    private static string BuildCanonicalRequest(
        HttpRequestMessage request,
        string amzDate,
        string payloadHash,
        string? contentType)
    {
        StringBuilder builder = new();
        builder.Append(request.Method.Method).Append('\n');
        builder.Append(request.RequestUri!.AbsolutePath).Append('\n');
        builder.Append('\n');

        if (contentType is not null)
        {
            builder.Append("content-type:").Append(contentType).Append('\n');
        }

        builder.Append("host:").Append(request.Headers.Host).Append('\n');
        builder.Append("x-amz-content-sha256:").Append(payloadHash).Append('\n');
        builder.Append("x-amz-date:").Append(amzDate).Append('\n');
        builder.Append('\n');
        builder.Append(contentType is null
            ? "host;x-amz-content-sha256;x-amz-date"
            : "content-type;host;x-amz-content-sha256;x-amz-date");
        builder.Append('\n');
        builder.Append(payloadHash);
        return builder.ToString();
    }

    private string BuildStringToSign(string amzDate, string scope, string canonicalRequest)
    {
        string hashedRequest = HexHash(Encoding.UTF8.GetBytes(canonicalRequest));
        return $"AWS4-HMAC-SHA256\n{amzDate}\n{scope}\n{hashedRequest}";
    }

    private string CalculateSignature(string dateStamp, string stringToSign)
    {
        byte[] signingKey = GetSignatureKey(_secretAccessKey, dateStamp, "auto", "s3");
        byte[] signature = HmacSha256(signingKey, stringToSign);
        return Convert.ToHexString(signature).ToLowerInvariant();
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

    private static byte[] GetSignatureKey(string key, string dateStamp, string regionName, string serviceName)
    {
        byte[] kDate = HmacSha256(Encoding.UTF8.GetBytes("AWS4" + key), dateStamp);
        byte[] kRegion = HmacSha256(kDate, regionName);
        byte[] kService = HmacSha256(kRegion, serviceName);
        return HmacSha256(kService, "aws4_request");
    }

    private static byte[] HmacSha256(byte[] key, string data)
    {
        using HMACSHA256 hmac = new(key);
        return hmac.ComputeHash(Encoding.UTF8.GetBytes(data));
    }

    private static string HexHash(byte[] data) =>
        Convert.ToHexString(SHA256.HashData(data)).ToLowerInvariant();
}
