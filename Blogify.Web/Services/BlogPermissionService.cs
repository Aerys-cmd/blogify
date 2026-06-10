using Blogify.Web.Data;
using Blogify.Web.Models;
using Microsoft.EntityFrameworkCore;

namespace Blogify.Web.Services;

/// <summary>
/// Evaluates blog-level permissions for a user based on ownership and membership.
/// Owner has unrestricted access. Members have role-gated access.
/// SuperAdmin does NOT automatically gain blog access — they must be explicitly added.
/// </summary>
public sealed class BlogPermissionService(ApplicationDbContext dbContext) : IBlogPermissionService
{
    /// <inheritdoc />
    public async Task<BlogRole?> GetUserRoleAsync(string userId, Guid blogId, CancellationToken ct = default)
    {
        bool isOwner = await dbContext.Blogs
            .AsNoTracking()
            .AnyAsync(b => b.Id == blogId && b.OwnerId == userId && b.DeletedAt == null, ct);

        if (isOwner)
            return BlogRole.Admin; // Owner has equivalent of Admin-level permissions.

        BlogMembership? membership = await dbContext.BlogMemberships
            .AsNoTracking()
            .FirstOrDefaultAsync(m => m.BlogId == blogId && m.UserId == userId, ct);

        return membership?.Role;
    }

    /// <inheritdoc />
    public async Task<bool> HasBlogAccessAsync(string userId, Guid blogId, CancellationToken ct = default)
        => await GetUserRoleAsync(userId, blogId, ct) is not null;

    /// <inheritdoc />
    public async Task<bool> CanManageUsersAsync(string userId, Guid blogId, CancellationToken ct = default)
    {
        BlogRole? role = await GetUserRoleAsync(userId, blogId, ct);
        return role is BlogRole.Admin || IsOwner(userId, blogId);
    }

    /// <inheritdoc />
    public async Task<bool> CanManageSettingsAsync(string userId, Guid blogId, CancellationToken ct = default)
    {
        BlogRole? role = await GetUserRoleAsync(userId, blogId, ct);
        return role is BlogRole.Admin || IsOwner(userId, blogId);
    }

    /// <inheritdoc />
    public async Task<bool> CanWritePostsAsync(string userId, Guid blogId, CancellationToken ct = default)
        => await GetUserRoleAsync(userId, blogId, ct) is not null;

    /// <inheritdoc />
    public async Task<bool> CanPublishPostsAsync(string userId, Guid blogId, CancellationToken ct = default)
    {
        BlogRole? role = await GetUserRoleAsync(userId, blogId, ct);
        return role is BlogRole.Editor or BlogRole.Admin || IsOwner(userId, blogId);
    }

    /// <inheritdoc />
    public async Task<bool> CanEditAnyPostAsync(string userId, Guid blogId, CancellationToken ct = default)
    {
        BlogRole? role = await GetUserRoleAsync(userId, blogId, ct);
        return role is BlogRole.Editor or BlogRole.Admin || IsOwner(userId, blogId);
    }

    /// <inheritdoc />
    public async Task<bool> CanEditOwnPostAsync(string userId, Guid blogId, CancellationToken ct = default)
        => await GetUserRoleAsync(userId, blogId, ct) is not null;

    // Synchronous owner check used internally to avoid extra DB round-trips when
    // GetUserRoleAsync has already confirmed ownership by returning Admin.
    private static bool IsOwner(string userId, Guid blogId) => false; // Ownership is resolved inside GetUserRoleAsync.
}
