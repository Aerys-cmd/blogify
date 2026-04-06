using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Processing;

namespace Blogify.Web.Services;

public sealed class LocalFileStorageService(IWebHostEnvironment env) : IFileStorageService
{
    private static readonly HashSet<string> AllowedContentTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "image/jpeg",
        "image/png",
        "image/gif",
        "image/webp"
    };

    private const long MaxFileSizeBytes = 10L * 1024L * 1024L;

    public async Task<string> SaveAsync(IFormFile file, Guid tenantId, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(file);

        if (!AllowedContentTypes.Contains(file.ContentType))
        {
            throw new ArgumentException(
                $"Content type '{file.ContentType}' is not allowed. Only JPEG, PNG, GIF, and WebP images are accepted.",
                nameof(file));
        }

        if (file.Length > MaxFileSizeBytes)
        {
            throw new ArgumentOutOfRangeException(
                nameof(file),
                "File size exceeds the maximum allowed size of 10 MB.");
        }

        string extension = Path.GetExtension(file.FileName);
        string fileName = $"{Guid.NewGuid()}{extension}";
        string directoryPath = Path.Combine(env.WebRootPath, "uploads", tenantId.ToString());

        Directory.CreateDirectory(directoryPath);

        string filePath = Path.Combine(directoryPath, fileName);

        await using (FileStream stream = new(filePath, FileMode.Create))
        {
            await file.CopyToAsync(stream, ct);
        }

        return $"/uploads/{tenantId}/{fileName}";
    }

    public Task DeleteAsync(string url, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(url);

        if (string.IsNullOrWhiteSpace(url))
        {
            throw new ArgumentException("URL must not be empty.", nameof(url));
        }

        // URL format: /uploads/{tenantId}/{fileName}
        string relativePath = url.TrimStart('/').Replace('/', Path.DirectorySeparatorChar);
        string absolutePath = Path.Combine(env.WebRootPath, relativePath);

        if (File.Exists(absolutePath))
        {
            File.Delete(absolutePath);
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

        string relativePath = sourceUrl.TrimStart('/').Replace('/', Path.DirectorySeparatorChar);
        string absolutePath = Path.Combine(env.WebRootPath, relativePath);

        if (!File.Exists(absolutePath))
        {
            return null;
        }

        try
        {
            string thumbDir = Path.Combine(
                env.WebRootPath, "uploads", tenantId.ToString(), "thumbnails");
            Directory.CreateDirectory(thumbDir);

            string originalName = Path.GetFileNameWithoutExtension(absolutePath);
            string thumbFileName = $"{originalName}-thumb.webp";
            string thumbPath = Path.Combine(thumbDir, thumbFileName);

            using Image image = await Image.LoadAsync(absolutePath, ct);

            if (image.Width > maxWidthPx)
            {
                int newHeight = (int)Math.Round((double)image.Height * maxWidthPx / image.Width);
                image.Mutate(ctx => ctx.Resize(maxWidthPx, newHeight));
            }

            await image.SaveAsWebpAsync(thumbPath, ct);

            return $"/uploads/{tenantId}/thumbnails/{thumbFileName}";
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

        string relativePath = sourceUrl.TrimStart('/').Replace('/', Path.DirectorySeparatorChar);
        string absolutePath = Path.Combine(env.WebRootPath, relativePath);

        if (!File.Exists(absolutePath))
        {
            return null;
        }

        try
        {
            ImageInfo info = await Image.IdentifyAsync(absolutePath, ct);
            return info is null ? null : (info.Width, info.Height);
        }
        catch
        {
            return null;
        }
    }
}
