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
        ModerationStatus = CommentModerationStatus.Pending;
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
    public CommentModerationStatus ModerationStatus { get; private set; }
    public DateTimeOffset? ModeratedAt { get; private set; }
    public string? ModeratedByUserId { get; private set; }
    public string? ModerationReason { get; private set; }
    public DateTimeOffset? DeletedAt { get; private set; }

    public bool IsApproved => ModerationStatus == CommentModerationStatus.Approved;

    public void Approve(string moderatorUserId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(moderatorUserId);

        ModerationStatus = CommentModerationStatus.Approved;
        ModeratedByUserId = moderatorUserId.Trim();
        ModeratedAt = DateTimeOffset.UtcNow;
        ModerationReason = null;
    }

    public void Reject(string moderatorUserId, string? reason = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(moderatorUserId);

        string? trimmedReason = string.IsNullOrWhiteSpace(reason) ? null : reason.Trim();
        if (trimmedReason?.Length > 500)
        {
            throw new ArgumentException("Moderation reason must not exceed 500 characters.", nameof(reason));
        }

        ModerationStatus = CommentModerationStatus.Rejected;
        ModeratedByUserId = moderatorUserId.Trim();
        ModeratedAt = DateTimeOffset.UtcNow;
        ModerationReason = trimmedReason;
    }

    public void SoftDelete()
    {
        if (DeletedAt.HasValue)
        {
            throw new DomainException("Comment is already deleted.");
        }

        DeletedAt = DateTimeOffset.UtcNow;
    }
}
