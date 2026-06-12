using Blogify.Web.Models;

namespace Blogify.Web.Services;

public interface IAccessibleBlogService
{
    Task<IReadOnlyList<AccessibleBlog>> GetForUserAsync(
        string userId,
        string scheme,
        HostString host,
        CancellationToken cancellationToken = default);
}

public sealed record AccessibleBlog(
    Guid Id,
    string Title,
    string Subdomain,
    BlogRole? Role,
    bool IsOwner,
    string PublicUrl);
