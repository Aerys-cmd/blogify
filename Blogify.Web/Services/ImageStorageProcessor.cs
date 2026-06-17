using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Webp;
using SixLabors.ImageSharp.Processing;

namespace Blogify.Web.Services;

public sealed record ProcessedImage(
    byte[] Bytes,
    string ContentType,
    string FileExtension,
    int Width,
    int Height);

public sealed class ImageStorageProcessor(IOptions<StorageOptions> options)
{
    private static readonly HashSet<string> AllowedContentTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "image/jpeg",
        "image/png",
        "image/gif",
        "image/webp"
    };

    private const long MaxFileSizeBytes = 10L * 1024L * 1024L;
    private readonly StorageOptions _options = options.Value;

    public void ValidateUpload(IFormFile file)
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
    }

    public async Task<ProcessedImage> CreateStoredImageAsync(IFormFile file, CancellationToken ct = default)
    {
        ValidateUpload(file);

        await using Stream input = file.OpenReadStream();
        using Image image = await Image.LoadAsync(input, ct);
        return await EncodeWebpAsync(image, _options.ImageQuality, ct);
    }

    public async Task<ProcessedImage?> CreateThumbnailAsync(
        Stream input,
        int maxWidthPx,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(input);

        if (maxWidthPx <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maxWidthPx));
        }

        using Image image = await Image.LoadAsync(input, ct);
        if (image.Width > maxWidthPx)
        {
            int newHeight = (int)Math.Round((double)image.Height * maxWidthPx / image.Width);
            image.Mutate(ctx => ctx.Resize(maxWidthPx, newHeight));
        }

        return await EncodeWebpAsync(image, _options.ThumbnailQuality, ct);
    }

    public async Task<(int Width, int Height)?> GetDimensionsAsync(
        Stream input,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(input);

        ImageInfo? info = await Image.IdentifyAsync(input, ct);
        return info is null ? null : (info.Width, info.Height);
    }

    private static async Task<ProcessedImage> EncodeWebpAsync(
        Image image,
        int quality,
        CancellationToken ct)
    {
        await using MemoryStream output = new();
        await image.SaveAsWebpAsync(output, new WebpEncoder
        {
            Quality = quality
        }, ct);

        return new ProcessedImage(
            output.ToArray(),
            "image/webp",
            ".webp",
            image.Width,
            image.Height);
    }
}
