using Microsoft.AspNetCore.Http;

namespace Blogify.Web.Services;

public interface IFileStorageService
{
    Task<string> SaveAsync(IFormFile file, Guid tenantId, CancellationToken ct = default);
    Task DeleteAsync(string url, CancellationToken ct = default);

    /// <summary>
    /// Generates and persists a thumbnail for the given source URL.
    /// Returns <c>null</c> if thumbnails are not supported by this provider.
    /// </summary>
    Task<string?> SaveThumbnailAsync(
        string sourceUrl,
        Guid tenantId,
        int maxWidthPx,
        CancellationToken ct = default);

    /// <summary>
    /// Returns the original image dimensions (width × height in pixels),
    /// or <c>null</c> for non-image files or unsupported providers.
    /// </summary>
    Task<(int Width, int Height)?> GetImageDimensionsAsync(
        string sourceUrl,
        CancellationToken ct = default);
}

