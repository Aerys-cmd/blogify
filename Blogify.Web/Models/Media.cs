using Blogify.Web.Models.Exceptions;

namespace Blogify.Web.Models;

public sealed class Media
{
    private Media() { }

    private Media(Guid blogId, string fileName, string url, string contentType, long sizeBytes)
    {
        if (blogId == Guid.Empty)
        {
            throw new ArgumentException("Blog id is required.", nameof(blogId));
        }

        ArgumentNullException.ThrowIfNull(fileName);
        ArgumentNullException.ThrowIfNull(url);
        ArgumentNullException.ThrowIfNull(contentType);

        if (string.IsNullOrWhiteSpace(fileName))
        {
            throw new ArgumentException("File name is required.", nameof(fileName));
        }

        if (fileName.Trim().Length > 255)
        {
            throw new ArgumentException("File name must not exceed 255 characters.", nameof(fileName));
        }

        if (string.IsNullOrWhiteSpace(url))
        {
            throw new ArgumentException("URL is required.", nameof(url));
        }

        if (string.IsNullOrWhiteSpace(contentType))
        {
            throw new ArgumentException("Content type is required.", nameof(contentType));
        }

        if (contentType.Trim().Length > 100)
        {
            throw new ArgumentException("Content type must not exceed 100 characters.", nameof(contentType));
        }

        if (sizeBytes <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(sizeBytes), "Size must be greater than zero.");
        }

        Id = Guid.NewGuid();
        BlogId = blogId;
        FileName = fileName.Trim();
        Url = url.Trim();
        ContentType = contentType.Trim();
        SizeBytes = sizeBytes;
        UploadedAt = DateTimeOffset.UtcNow;
    }

    public Guid Id { get; private init; }
    public Guid BlogId { get; private init; }
    public string FileName { get; private set; } = string.Empty;
    public string Url { get; private set; } = string.Empty;
    public string ContentType { get; private set; } = string.Empty;
    public long SizeBytes { get; private init; }
    public DateTimeOffset UploadedAt { get; private init; }
    public DateTimeOffset? DeletedAt { get; private set; }
    public string? AltText { get; private set; }
    public string? Title { get; private set; }
    public string? Description { get; private set; }
    public string? ThumbnailUrl { get; private set; }
    public int? WidthPx { get; private set; }
    public int? HeightPx { get; private set; }

    public static Media Upload(Guid blogId, string fileName, string url, string contentType, long sizeBytes)
    {
        return new Media(blogId, fileName, url, contentType, sizeBytes);
    }

    public void UpdateMetadata(string? altText, string? title, string? description)
    {
        if (altText is not null && altText.Trim().Length > 500)
        {
            throw new ArgumentException("Alt text must not exceed 500 characters.", nameof(altText));
        }

        if (title is not null && title.Trim().Length > 255)
        {
            throw new ArgumentException("Title must not exceed 255 characters.", nameof(title));
        }

        if (description is not null && description.Trim().Length > 2000)
        {
            throw new ArgumentException("Description must not exceed 2000 characters.", nameof(description));
        }

        AltText = string.IsNullOrWhiteSpace(altText) ? null : altText.Trim();
        Title = string.IsNullOrWhiteSpace(title) ? null : title.Trim();
        Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim();
    }

    internal void SetThumbnail(string thumbnailUrl, int widthPx, int heightPx)
    {
        ArgumentNullException.ThrowIfNull(thumbnailUrl);
        ThumbnailUrl = thumbnailUrl;
        WidthPx = widthPx;
        HeightPx = heightPx;
    }

    public void SoftDelete()
    {
        if (DeletedAt.HasValue)
        {
            throw new DomainException("Media is already deleted.");
        }

        DeletedAt = DateTimeOffset.UtcNow;
    }
}

