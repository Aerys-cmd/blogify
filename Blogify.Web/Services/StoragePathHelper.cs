namespace Blogify.Web.Services;

internal static class StoragePathHelper
{
    public static string BuildLocalUrl(Guid tenantId, string fileName) =>
        $"/uploads/{tenantId}/{fileName}";

    public static string BuildLocalThumbnailUrl(Guid tenantId, string fileName) =>
        $"/uploads/{tenantId}/thumbnails/{fileName}";

    public static string BuildR2ObjectKey(Guid tenantId, string fileName) =>
        $"uploads/{tenantId}/{fileName}";

    public static string BuildR2ThumbnailKey(Guid tenantId, string fileName) =>
        $"uploads/{tenantId}/thumbnails/{fileName}";

    public static string GetThumbnailFileName(string fileName) =>
        $"{Path.GetFileNameWithoutExtension(fileName)}-thumb.webp";

    public static string NormalizePublicBaseUrl(string publicBaseUrl) =>
        publicBaseUrl.EndsWith('/') ? publicBaseUrl : publicBaseUrl + "/";

    public static string EscapeObjectKey(string objectKey) =>
        string.Join(
            "/",
            objectKey.Split('/', StringSplitOptions.RemoveEmptyEntries)
                .Select(Uri.EscapeDataString));

    public static string GetRelativePathFromUrl(string url)
    {
        if (Uri.TryCreate(url, UriKind.Absolute, out Uri? absoluteUri))
        {
            return absoluteUri.AbsolutePath.TrimStart('/');
        }

        return url.TrimStart('/');
    }

    public static string? TryGetThumbnailKeyFromObjectKey(string objectKey)
    {
        string[] segments = objectKey.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (segments.Length != 3 || !string.Equals(segments[0], "uploads", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        if (!Guid.TryParse(segments[1], out Guid tenantId))
        {
            return null;
        }

        return BuildR2ThumbnailKey(tenantId, GetThumbnailFileName(segments[2]));
    }
}
