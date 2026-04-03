namespace Blogify.Web.Models.Posts;

public class Post
{
    private readonly List<PostRevision> _revisions = [];

    private Post()
    {
    }

    private Post(int tenantId, string slug)
    {
        Id = Guid.NewGuid();
        TenantId = tenantId;
        ChangeSlug(slug);
    }

    public Guid Id { get; private set; }
    public int TenantId { get; private set; }
    public string Slug { get; private set; } = string.Empty;
    public Guid? PublishedRevisionId { get; private set; }
    public Guid? DraftRevisionId { get; private set; }

    public bool IsPublished => PublishedRevisionId.HasValue;

    public IReadOnlyCollection<PostRevision> Revisions => _revisions.AsReadOnly();

    public static Post Create(int tenantId, string slug)
    {
        if (tenantId <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(tenantId), "Tenant id must be greater than zero.");
        }

        return new Post(tenantId, slug);
    }

    public void ChangeSlug(string slug)
    {
        if (string.IsNullOrWhiteSpace(slug))
        {
            throw new ArgumentException("Slug is required.", nameof(slug));
        }

        Slug = slug.Trim().ToLowerInvariant();
    }

    public PostRevision CreateDraft(string title, string content, string authorId)
    {
        var draft = PostRevision.CreateDraft(TenantId, Id, title, content, authorId);
        _revisions.Add(draft);
        DraftRevisionId = draft.Id;
        return draft;
    }

    public void PublishDraft(string publisherId)
    {
        if (!DraftRevisionId.HasValue)
        {
            throw new InvalidOperationException("There is no draft revision to publish.");
        }

        var draft = _revisions.FirstOrDefault(r => r.Id == DraftRevisionId.Value)
            ?? throw new InvalidOperationException("Draft revision cannot be found.");

        draft.Publish(publisherId);
        PublishedRevisionId = draft.Id;
        DraftRevisionId = null;
    }
}