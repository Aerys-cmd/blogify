using System.Text.RegularExpressions;
using Blogify.Web.Models.Exceptions;

namespace Blogify.Web.Models;

public sealed class Tag
{
    private static readonly Regex SlugRegex =
        new Regex(@"^[a-z0-9-]+$", RegexOptions.Compiled, TimeSpan.FromMilliseconds(100));

    private Tag() { }

    private Tag(Guid blogId, string name, string slug)
    {
        if (blogId == Guid.Empty)
        {
            throw new ArgumentException("Blog id is required.", nameof(blogId));
        }

        Id = Guid.NewGuid();
        BlogId = blogId;
        CreatedAt = DateTimeOffset.UtcNow;
        Rename(name);
        ChangeSlug(slug);
    }

    public Guid Id { get; private init; }
    public Guid BlogId { get; private init; }
    public string Name { get; private set; } = string.Empty;
    public string Slug { get; private set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; private init; }
    public DateTimeOffset? DeletedAt { get; private set; }

    public static Tag Create(Guid blogId, string name, string slug)
    {
        return new Tag(blogId, name, slug);
    }

    public void Rename(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Tag name is required.", nameof(name));
        }

        string trimmed = name.Trim();
        if (trimmed.Length > 100)
        {
            throw new ArgumentException("Tag name must not exceed 100 characters.", nameof(name));
        }

        Name = trimmed;
    }

    public void ChangeSlug(string slug)
    {
        if (string.IsNullOrWhiteSpace(slug))
        {
            throw new ArgumentException("Tag slug is required.", nameof(slug));
        }

        string trimmed = slug.Trim().ToLowerInvariant();
        if (trimmed.Length > 100)
        {
            throw new ArgumentException("Tag slug must not exceed 100 characters.", nameof(slug));
        }

        if (!SlugRegex.IsMatch(trimmed))
        {
            throw new DomainException("Tag slug may only contain lowercase letters, digits, and hyphens.");
        }

        Slug = trimmed;
    }

    public void SoftDelete()
    {
        if (DeletedAt.HasValue)
        {
            throw new DomainException("Tag is already deleted.");
        }

        DeletedAt = DateTimeOffset.UtcNow;
    }
}

