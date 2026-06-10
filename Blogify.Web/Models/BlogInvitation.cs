using Blogify.Web.Models.Exceptions;

namespace Blogify.Web.Models;

/// <summary>
/// Represents a pending invitation for a user to join a blog with a specific role.
/// </summary>
public sealed class BlogInvitation
{
    private BlogInvitation() { } // EF constructor

    private BlogInvitation(Guid blogId, string email, BlogRole role, string token, string invitedByUserId)
    {
        if (blogId == Guid.Empty)
            throw new ArgumentException("Blog id is required.", nameof(blogId));

        ArgumentException.ThrowIfNullOrWhiteSpace(email);
        ArgumentException.ThrowIfNullOrWhiteSpace(token);
        ArgumentException.ThrowIfNullOrWhiteSpace(invitedByUserId);

        if (!Enum.IsDefined(typeof(BlogRole), role))
            throw new DomainException($"'{role}' is not a valid blog role.");

        Id              = Guid.NewGuid();
        BlogId          = blogId;
        Email           = email.Trim().ToLowerInvariant();
        Role            = role;
        Token           = token.Trim();
        InvitedByUserId = invitedByUserId.Trim();
        CreatedAtUtc    = DateTimeOffset.UtcNow;
        ExpiresAtUtc    = DateTimeOffset.UtcNow.AddDays(7);
    }

    public Guid Id { get; private init; }
    public Guid BlogId { get; private init; }
    public string Email { get; private init; } = string.Empty;
    public BlogRole Role { get; private init; }
    public string Token { get; private init; } = string.Empty;
    public string InvitedByUserId { get; private init; } = string.Empty;
    public DateTimeOffset CreatedAtUtc { get; private init; }
    public DateTimeOffset ExpiresAtUtc { get; private set; }
    public DateTimeOffset? AcceptedAtUtc { get; private set; }

    public bool IsExpired => DateTimeOffset.UtcNow > ExpiresAtUtc;
    public bool IsAccepted => AcceptedAtUtc.HasValue;
    public bool IsValid => !IsExpired && !IsAccepted;

    public static BlogInvitation Create(Guid blogId, string email, BlogRole role, string token, string invitedByUserId)
        => new(blogId, email, role, token, invitedByUserId);

    public void Accept()
    {
        if (IsAccepted)
            throw new DomainException("Invitation has already been accepted.");

        if (IsExpired)
            throw new DomainException("Invitation has expired.");

        AcceptedAtUtc = DateTimeOffset.UtcNow;
    }
}
