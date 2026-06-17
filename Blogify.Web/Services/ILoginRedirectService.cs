using Blogify.Web.Models;
using Microsoft.AspNetCore.Http;

namespace Blogify.Web.Services;

public interface ILoginRedirectService
{
    Task<string> GetDestinationAsync(
        string userId,
        string scheme,
        HostString host,
        CancellationToken cancellationToken = default);
}

public sealed class LoginRedirectService(IAccessibleBlogService accessibleBlogService) : ILoginRedirectService
{
    public async Task<string> GetDestinationAsync(
        string userId,
        string scheme,
        HostString host,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userId);

        IReadOnlyList<AccessibleBlog> blogs = await accessibleBlogService.GetForUserAsync(
            userId,
            scheme,
            host,
            cancellationToken);

        return blogs.Count switch
        {
            0 => "~/dashboard/create-blog",
            1 => $"~/app/admin/{blogs[0].Subdomain}",
            _ => "~/dashboard",
        };
    }
}
