using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;

namespace Blogify.Web.Services;

public sealed class LocalFileStorageService(
    IWebHostEnvironment env,
    ImageStorageProcessor imageProcessor) : IFileStorageService
{
    public async Task<string> SaveAsync(IFormFile file, Guid tenantId, CancellationToken ct = default)
    {
        imageProcessor.ValidateUpload(file);

        ProcessedImage stored = await imageProcessor.CreateStoredImageAsync(file, ct);
        string fileName = $"{Guid.NewGuid()}{stored.FileExtension}";
        string directoryPath = Path.Combine(env.WebRootPath, "uploads", tenantId.ToString());
        Directory.CreateDirectory(directoryPath);

        string filePath = Path.Combine(directoryPath, fileName);
        await File.WriteAllBytesAsync(filePath, stored.Bytes, ct);

        return StoragePathHelper.BuildLocalUrl(tenantId, fileName);
    }

    public Task DeleteAsync(string url, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(url);

        if (string.IsNullOrWhiteSpace(url))
        {
            throw new ArgumentException("URL must not be empty.", nameof(url));
        }

        string relativePath = StoragePathHelper.GetRelativePathFromUrl(url).Replace('/', Path.DirectorySeparatorChar);
        string absolutePath = Path.Combine(env.WebRootPath, relativePath);
        if (File.Exists(absolutePath))
        {
            File.Delete(absolutePath);
        }

        string[] segments = relativePath.Split(Path.DirectorySeparatorChar, StringSplitOptions.RemoveEmptyEntries);
        if (segments.Length == 3 &&
            string.Equals(segments[0], "uploads", StringComparison.OrdinalIgnoreCase))
        {
            string thumbnailPath = Path.Combine(
                env.WebRootPath,
                "uploads",
                segments[1],
                "thumbnails",
                StoragePathHelper.GetThumbnailFileName(segments[2]));

            if (File.Exists(thumbnailPath))
            {
                File.Delete(thumbnailPath);
            }
        }

        return Task.CompletedTask;
    }

    public async Task<string?> SaveThumbnailAsync(
        string sourceUrl,
        Guid tenantId,
        int maxWidthPx,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(sourceUrl);

        string relativePath = StoragePathHelper.GetRelativePathFromUrl(sourceUrl).Replace('/', Path.DirectorySeparatorChar);
        string absolutePath = Path.Combine(env.WebRootPath, relativePath);
        if (!File.Exists(absolutePath))
        {
            return null;
        }

        try
        {
            string thumbDir = Path.Combine(env.WebRootPath, "uploads", tenantId.ToString(), "thumbnails");
            Directory.CreateDirectory(thumbDir);

            string thumbFileName = StoragePathHelper.GetThumbnailFileName(Path.GetFileName(absolutePath));
            string thumbPath = Path.Combine(thumbDir, thumbFileName);

            await using FileStream input = File.OpenRead(absolutePath);
            ProcessedImage? thumbnail = await imageProcessor.CreateThumbnailAsync(input, maxWidthPx, ct);
            if (thumbnail is null)
            {
                return null;
            }

            await File.WriteAllBytesAsync(thumbPath, thumbnail.Bytes, ct);
            return StoragePathHelper.BuildLocalThumbnailUrl(tenantId, thumbFileName);
        }
        catch
        {
            return null;
        }
    }

    public async Task<(int Width, int Height)?> GetImageDimensionsAsync(
        string sourceUrl,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(sourceUrl);

        string relativePath = StoragePathHelper.GetRelativePathFromUrl(sourceUrl).Replace('/', Path.DirectorySeparatorChar);
        string absolutePath = Path.Combine(env.WebRootPath, relativePath);
        if (!File.Exists(absolutePath))
        {
            return null;
        }

        try
        {
            await using FileStream input = File.OpenRead(absolutePath);
            return await imageProcessor.GetDimensionsAsync(input, ct);
        }
        catch
        {
            return null;
        }
    }
}
