using Blogify.Web.Models.Exceptions;

namespace Blogify.Web.Models;

/// <summary>
/// Represents a user's membership in a blog with a specific role.
/// Owners are tracked via <see cref="Tenant.OwnerId"/> and are never stored in this table.
/// </summary>
public sealed class BlogMembership
{
    private BlogMembership() { } // EF constructor

    private BlogMembership(Guid blogId, string userId, BlogRole role, string invitedByUserId)
    {
        if (blogId == Guid.Empty)
            throw new ArgumentException("Blog id is required.", nameof(blogId));

        ArgumentException.ThrowIfNullOrWhiteSpace(userId);
        ArgumentException.ThrowIfNullOrWhiteSpace(invitedByUserId);

        if (!Enum.IsDefined(typeof(BlogRole), role))
            throw new DomainException($"'{role}' is not a valid blog role.");

        Id            = Guid.NewGuid();
        BlogId        = blogId;
        UserId        = userId.Trim();
        Role          = role;
        InvitedByUserId = invitedByUserId.Trim();
        JoinedAtUtc   = DateTimeOffset.UtcNow;
    }

    public Guid Id { get; private init; }
    public Guid BlogId { get; private init; }
    public string UserId { get; private init; } = string.Empty;
    public BlogRole Role { get; private set; }
    public string InvitedByUserId { get; private init; } = string.Empty;
    public DateTimeOffset JoinedAtUtc { get; private init; }

    public static BlogMembership Create(Guid blogId, string userId, BlogRole role, string invitedByUserId)
        => new(blogId, userId, role, invitedByUserId);

    public void ChangeRole(BlogRole newRole)
    {
        if (!Enum.IsDefined(typeof(BlogRole), newRole))
            throw new DomainException($"'{newRole}' is not a valid blog role.");

        Role = newRole;
    }
}
