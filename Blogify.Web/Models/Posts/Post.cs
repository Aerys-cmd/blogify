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
    public Guid? CoverImageId { get; private set; }
    public PostStatus Status { get; private set; }
    public Guid? PublishedRevisionId { get; private set; }
    public string? MetaTitle { get; private set; }
    public string? MetaDescription { get; private set; }
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

    public void SetCoverImage(Guid? mediaId)
    {
        CoverImageId = mediaId;
    }

    public void SetCategories(IEnumerable<Guid> categoryIds)
    {
        ArgumentNullException.ThrowIfNull(categoryIds);

        HashSet<Guid> newIds = categoryIds.ToHashSet();

        // Remove only entries that are no longer selected.
        // Mutating the existing tracked instances lets EF Core mark them Deleted
        // without creating a PK identity conflict with newly added objects.
        _categories.RemoveAll(pc => !newIds.Contains(pc.CategoryId));

        // After trimming, capture which IDs are still present (retained / unchanged).
        HashSet<Guid> existingIds = _categories.Select(pc => pc.CategoryId).ToHashSet();

        // Add only genuinely new category associations.
        foreach (Guid categoryId in newIds.Where(id => !existingIds.Contains(id)))
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

    public void UpdateSeoMetadata(string? metaTitle, string? metaDescription)
    {
        if (metaTitle is not null)
        {
            string trimmedTitle = metaTitle.Trim();
            if (trimmedTitle.Length > 60)
            {
                throw new ArgumentException("Meta title must not exceed 60 characters.", nameof(metaTitle));
            }

            MetaTitle = string.IsNullOrEmpty(trimmedTitle) ? null : trimmedTitle;
        }
        else
        {
            MetaTitle = null;
        }

        if (metaDescription is not null)
        {
            string trimmedDesc = metaDescription.Trim();
            if (trimmedDesc.Length > 160)
            {
                throw new ArgumentException("Meta description must not exceed 160 characters.", nameof(metaDescription));
            }

            MetaDescription = string.IsNullOrEmpty(trimmedDesc) ? null : trimmedDesc;
        }
        else
        {
            MetaDescription = null;
        }
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