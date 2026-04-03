namespace Blogify.Web.Models.Posts;

public sealed class PostRevision
{
    private PostRevision() { }

    private PostRevision(Guid postId, string title, string content)
    {
        Id = Guid.NewGuid();
        PostId = postId;
        Title = title.Trim();
        Content = content.Trim();
        CreatedAt = DateTimeOffset.UtcNow;
    }

    public Guid Id { get; private init; }
    public Guid PostId { get; private init; }
    public string Title { get; private init; } = string.Empty;
    public string Content { get; private init; } = string.Empty;
    public DateTimeOffset CreatedAt { get; private init; }

    public static PostRevision Create(Guid postId, string title, string content)
    {
        if (postId == Guid.Empty)
        {
            throw new ArgumentException("Post id is required.", nameof(postId));
        }

        if (string.IsNullOrWhiteSpace(title))
        {
            throw new ArgumentException("Revision title is required.", nameof(title));
        }

        if (title.Trim().Length > 500)
        {
            throw new ArgumentException("Revision title must not exceed 500 characters.", nameof(title));
        }

        if (string.IsNullOrWhiteSpace(content))
        {
            throw new ArgumentException("Revision content is required.", nameof(content));
        }

        return new PostRevision(postId, title, content);
    }
}