namespace Blogify.Web.Models.Posts;

public class PostRevision
{
    private PostRevision()
    {
    }

    private PostRevision(int tenantId, Guid postId, string title, string content, string createdBy)
    {
        Id = Guid.NewGuid();
        TenantId = tenantId;
        PostId = postId;
        UpdateBody(title, content, createdBy);
        IsDraft = true;
        CreatedAt = DateTime.UtcNow;
        CreatedBy = createdBy;
        ModifiedBy = createdBy;
    }

    public Guid Id { get; private set; }
    public int TenantId { get; private set; }
    public Guid PostId { get; private set; }
    public string Title { get; private set; } = string.Empty;
    public string Content { get; private set; } = string.Empty;
    public string? SeoTitle { get; private set; }
    public string? SeoKeywords { get; private set; }
    public string? SeoDescription { get; private set; }
    public bool IsDraft { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime? PublishedAt { get; private set; }
    public string CreatedBy { get; private set; } = string.Empty;
    public string ModifiedBy { get; private set; } = string.Empty;

    public Post? Post { get; private set; }

    public static PostRevision CreateDraft(int tenantId, Guid postId, string title, string content, string createdBy)
    {
        if (tenantId <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(tenantId), "Tenant id must be greater than zero.");
        }

        if (postId == Guid.Empty)
        {
            throw new ArgumentException("Post id is required.", nameof(postId));
        }

        if (string.IsNullOrWhiteSpace(createdBy))
        {
            throw new ArgumentException("Creator id is required.", nameof(createdBy));
        }

        return new PostRevision(tenantId, postId, title, content, createdBy);
    }

    public void UpdateBody(string title, string content, string modifiedBy)
    {
        if (!IsDraft && PublishedAt.HasValue)
        {
            throw new InvalidOperationException("Published revisions are immutable.");
        }

        if (string.IsNullOrWhiteSpace(title))
        {
            throw new ArgumentException("Revision title is required.", nameof(title));
        }

        if (string.IsNullOrWhiteSpace(content))
        {
            throw new ArgumentException("Revision content is required.", nameof(content));
        }

        if (string.IsNullOrWhiteSpace(modifiedBy))
        {
            throw new ArgumentException("Modifier id is required.", nameof(modifiedBy));
        }

        Title = title.Trim();
        Content = content.Trim();
        ModifiedBy = modifiedBy;
    }

    public void UpdateSeo(string? seoTitle, string? seoKeywords, string? seoDescription, string modifiedBy)
    {
        if (string.IsNullOrWhiteSpace(modifiedBy))
        {
            throw new ArgumentException("Modifier id is required.", nameof(modifiedBy));
        }

        SeoTitle = string.IsNullOrWhiteSpace(seoTitle) ? null : seoTitle.Trim();
        SeoKeywords = string.IsNullOrWhiteSpace(seoKeywords) ? null : seoKeywords.Trim();
        SeoDescription = string.IsNullOrWhiteSpace(seoDescription) ? null : seoDescription.Trim();
        ModifiedBy = modifiedBy;
    }

    public void Publish(string modifiedBy)
    {
        if (!IsDraft)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(modifiedBy))
        {
            throw new ArgumentException("Publisher id is required.", nameof(modifiedBy));
        }

        IsDraft = false;
        PublishedAt = DateTime.UtcNow;
        ModifiedBy = modifiedBy;
    }
}