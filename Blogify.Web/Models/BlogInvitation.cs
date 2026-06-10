using Blogify.Web.Models.Exceptions;

namespace Blogify.Web.Models;

/// <summary>
/// Represents a pending invitation for a user to join a blog with a specific role.
/// </summary>
public sealed class BlogInvitation
{
    private BlogInvitation() { } // EF constructor

    private BlogInvitation(Guid blogId, string email, BlogRole role, string tokenHash, string invitedByUserId)
    {
        if (blogId == Guid.Empty)
            throw new ArgumentException("Blog id is required.", nameof(blogId));

        ArgumentException.ThrowIfNullOrWhiteSpace(email);
        ArgumentException.ThrowIfNullOrWhiteSpace(tokenHash);
        ArgumentException.ThrowIfNullOrWhiteSpace(invitedByUserId);

        if (!Enum.IsDefined(typeof(BlogRole), role))
            throw new DomainException($"'{role}' is not a valid blog role.");

        Id              = Guid.NewGuid();
        BlogId          = blogId;
        Email           = email.Trim().ToLowerInvariant();
        Role            = role;
        TokenHash       = tokenHash.Trim();
        InvitedByUserId = invitedByUserId.Trim();
        CreatedAtUtc    = DateTimeOffset.UtcNow;
        ExpiresAtUtc    = DateTimeOffset.UtcNow.AddDays(7);
        Status          = BlogInvitationStatus.Pending;
    }

    public Guid Id { get; private init; }
    public Guid BlogId { get; private init; }
    public string Email { get; private init; } = string.Empty;
    public BlogRole Role { get; private init; }
    public string TokenHash { get; private set; } = string.Empty;
    public string InvitedByUserId { get; private init; } = string.Empty;
    public DateTimeOffset CreatedAtUtc { get; private init; }
    public DateTimeOffset ExpiresAtUtc { get; private set; }
    public DateTimeOffset? AcceptedAtUtc { get; private set; }
    public DateTimeOffset? LastSentAtUtc { get; private set; }
    public BlogInvitationStatus Status { get; private set; }

    public bool IsExpired => Status == BlogInvitationStatus.Pending && DateTimeOffset.UtcNow > ExpiresAtUtc;
    public bool IsAccepted => Status == BlogInvitationStatus.Accepted;
    public bool IsValid => Status == BlogInvitationStatus.Pending && !IsExpired;

    public static BlogInvitation Create(Guid blogId, string email, BlogRole role, string tokenHash, string invitedByUserId)
        => new(blogId, email, role, tokenHash, invitedByUserId);

    public void MarkSent() => LastSentAtUtc = DateTimeOffset.UtcNow;

    public void Resend(string tokenHash)
    {
        EnsurePending();
        ArgumentException.ThrowIfNullOrWhiteSpace(tokenHash);
        TokenHash = tokenHash.Trim();
        ExpiresAtUtc = DateTimeOffset.UtcNow.AddDays(7);
    }

    public void Accept()
    {
        EnsurePending();
        AcceptedAtUtc = DateTimeOffset.UtcNow;
        Status = BlogInvitationStatus.Accepted;
    }

    public void Decline() => TransitionTo(BlogInvitationStatus.Declined);
    public void Cancel() => TransitionTo(BlogInvitationStatus.Cancelled);
    public void Expire()
    {
        if (Status != BlogInvitationStatus.Pending)
            throw new DomainException("Invitation is no longer pending.");
        Status = BlogInvitationStatus.Expired;
    }

    private void TransitionTo(BlogInvitationStatus status)
    {
        EnsurePending();
        Status = status;
    }

    private void EnsurePending()
    {
        if (Status != BlogInvitationStatus.Pending)
            throw new DomainException("Invitation is no longer pending.");
        if (DateTimeOffset.UtcNow > ExpiresAtUtc)
            throw new DomainException("Invitation has expired.");
    }
}
