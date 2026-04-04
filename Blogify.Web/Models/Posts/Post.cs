using System.Text.RegularExpressions;
using Blogify.Web.Models.Exceptions;

namespace Blogify.Web.Models.Posts;

public sealed class Post
{
    private static readonly Regex SlugRegex =
        new Regex(@"^[a-z0-9-]+$", RegexOptions.Compiled, TimeSpan.FromMilliseconds(100));

    private readonly List<PostRevision> _revisions = [];
    private readonly List<PostCategory> _categories = [];

    private Post() { }

    private Post(Guid blogId, string authorId, string slug, string initialTitle, string initialContent)
    {
        if (blogId == Guid.Empty)
        {
            throw new ArgumentException("Blog id is required.", nameof(blogId));
        }

        if (string.IsNullOrWhiteSpace(authorId))
        {
            throw new ArgumentException("Author id is required.", nameof(authorId));
        }

        Id = Guid.NewGuid();
        BlogId = blogId;
        AuthorId = authorId.Trim();
        Status = PostStatus.Draft;
        CreatedAt = DateTimeOffset.UtcNow;
        ChangeSlug(slug);

        PostRevision initialRevision = PostRevision.Create(Id, initialTitle, initialContent);
        _revisions.Add(initialRevision);
    }

    public Guid Id { get; private init; }
    public Guid BlogId { get; private init; }
    public string AuthorId { get; private set; } = string.Empty;
    public string Slug { get; private set; } = string.Empty;
    public string? Excerpt { get; private set; }
    public string? FeaturedImageUrl { get; private set; }
    public PostStatus Status { get; private set; }
    public Guid? PublishedRevisionId { get; private set; }
    public DateTimeOffset CreatedAt { get; private init; }
    public DateTimeOffset? DeletedAt { get; private set; }
    public IReadOnlyList<PostRevision> Revisions => _revisions.AsReadOnly();
    public IReadOnlyList<PostCategory> Categories => _categories.AsReadOnly();

    public static Post Create(Guid blogId, string authorId, string slug, string initialTitle, string initialContent)
    {
        return new Post(blogId, authorId, slug, initialTitle, initialContent);
    }

    public void UpdateExcerpt(string? excerpt)
    {
        if (excerpt is null)
        {
            Excerpt = null;
            return;
        }

        string trimmed = excerpt.Trim();
        if (trimmed.Length > 500)
        {
            throw new ArgumentException("Excerpt must not exceed 500 characters.", nameof(excerpt));
        }

        Excerpt = string.IsNullOrEmpty(trimmed) ? null : trimmed;
    }

    public void UpdateFeaturedImageUrl(string? url)
    {
        if (url is null)
        {
            FeaturedImageUrl = null;
            return;
        }

        string trimmed = url.Trim();
        FeaturedImageUrl = string.IsNullOrEmpty(trimmed) ? null : trimmed;
    }

    public void SetCategories(IEnumerable<Guid> categoryIds)
    {
        ArgumentNullException.ThrowIfNull(categoryIds);
        _categories.Clear();
        foreach (Guid categoryId in categoryIds)
        {
            _categories.Add(new PostCategory(Id, categoryId));
        }
    }

    public void AddRevision(string title, string content)
    {
        PostRevision revision = PostRevision.Create(Id, title, content);
        _revisions.Add(revision);
    }

    public void Publish(Guid revisionId)
    {
        if (revisionId == Guid.Empty)
        {
            throw new ArgumentException("Revision id is required.", nameof(revisionId));
        }

        bool belongs = _revisions.Exists(r => r.Id == revisionId);
        if (!belongs)
        {
            throw new DomainException("The specified revision does not belong to this post.");
        }

        PublishedRevisionId = revisionId;
        Status = PostStatus.Published;
    }

    public void Unpublish()
    {
        PublishedRevisionId = null;
        Status = PostStatus.Draft;
    }

    public void ChangeSlug(string slug)
    {
        if (string.IsNullOrWhiteSpace(slug))
        {
            throw new ArgumentException("Slug is required.", nameof(slug));
        }

        string trimmed = slug.Trim().ToLowerInvariant();
        if (trimmed.Length > 300)
        {
            throw new ArgumentException("Slug must not exceed 300 characters.", nameof(slug));
        }

        if (!SlugRegex.IsMatch(trimmed))
        {
            throw new DomainException("Slug may only contain lowercase letters, digits, and hyphens.");
        }

        Slug = trimmed;
    }

    public void SoftDelete()
    {
        if (DeletedAt.HasValue)
        {
            throw new DomainException("Post is already deleted.");
        }

        DeletedAt = DateTimeOffset.UtcNow;
    }
}