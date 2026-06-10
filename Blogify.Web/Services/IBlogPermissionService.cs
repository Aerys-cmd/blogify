using Blogify.Web.Models;

namespace Blogify.Web.Services;

/// <summary>
/// Determines what a given user can do within a specific blog.
/// Blog access comes from blog ownership (<see cref="Tenant.OwnerId"/>) or
/// blog membership (<see cref="BlogMembership"/>). Platform roles are never
/// used for blog authorization.
/// </summary>
public interface IBlogPermissionService
{
    /// <summary>Returns the user's effective role in the blog, or <c>null</c> if the user has no access.</summary>
    Task<BlogRole?> GetUserRoleAsync(string userId, Guid blogId, CancellationToken ct = default);

    /// <summary>Owner or Admin can manage blog members and invitations.</summary>
    Task<bool> CanManageUsersAsync(string userId, Guid blogId, CancellationToken ct = default);

    /// <summary>Owner or Admin can manage blog settings and SEO metadata.</summary>
    Task<bool> CanManageSettingsAsync(string userId, Guid blogId, CancellationToken ct = default);

    /// <summary>Writer, Editor, Admin, and Owner can create posts.</summary>
    Task<bool> CanWritePostsAsync(string userId, Guid blogId, CancellationToken ct = default);

    /// <summary>Editor, Admin, and Owner can publish or unpublish posts.</summary>
    Task<bool> CanPublishPostsAsync(string userId, Guid blogId, CancellationToken ct = default);

    /// <summary>Editor, Admin, and Owner can edit any post regardless of authorship.</summary>
    Task<bool> CanEditAnyPostAsync(string userId, Guid blogId, CancellationToken ct = default);

    /// <summary>Writer can edit their own draft posts; all higher roles can as well.</summary>
    Task<bool> CanEditOwnPostAsync(string userId, Guid blogId, CancellationToken ct = default);

    /// <summary>Returns true if the user has at least read/write access (any role or ownership) to the blog admin.</summary>
    Task<bool> HasBlogAccessAsync(string userId, Guid blogId, CancellationToken ct = default);
}
