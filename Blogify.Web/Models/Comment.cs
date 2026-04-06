using Blogify.Web.Models.Exceptions;

namespace Blogify.Web.Models;

public sealed class Comment
{
    private Comment() { } // EF constructor

    private Comment(Guid blogId, Guid postId, string authorId, string content, Guid? parentCommentId)
    {
        if (blogId == Guid.Empty)
        {
            throw new ArgumentException("Blog id is required.", nameof(blogId));
        }

        if (postId == Guid.Empty)
        {
            throw new ArgumentException("Post id is required.", nameof(postId));
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(authorId);
        ArgumentException.ThrowIfNullOrWhiteSpace(content);

        string trimmedContent = content.Trim();
        if (trimmedContent.Length > 2000)
        {
            throw new ArgumentException("Comment must not exceed 2000 characters.", nameof(content));
        }

        Id = Guid.NewGuid();
        BlogId = blogId;
        PostId = postId;
        AuthorId = authorId.Trim();
        Content = trimmedContent;
        ParentCommentId = parentCommentId == Guid.Empty ? null : parentCommentId;
        CreatedAt = DateTimeOffset.UtcNow;
    }

    public static Comment Create(Guid blogId, Guid postId, string authorId, string content, Guid? parentCommentId = null)
        => new(blogId, postId, authorId, content, parentCommentId);

    public Guid Id { get; private init; }
    public Guid BlogId { get; private init; }
    public Guid PostId { get; private init; }
    public string AuthorId { get; private init; } = string.Empty;
    public string Content { get; private set; } = string.Empty;
    public Guid? ParentCommentId { get; private init; }
    public DateTimeOffset CreatedAt { get; private init; }
    public DateTimeOffset? DeletedAt { get; private set; }

    public void SoftDelete()
    {
        if (DeletedAt.HasValue)
        {
            throw new DomainException("Comment is already deleted.");
        }

        DeletedAt = DateTimeOffset.UtcNow;
    }
}
