using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;

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
}


