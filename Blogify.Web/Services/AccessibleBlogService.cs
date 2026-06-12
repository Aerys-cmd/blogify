using Blogify.Web.Data;
using Microsoft.EntityFrameworkCore;

namespace Blogify.Web.Services;

public sealed class AccessibleBlogService(ApplicationDbContext dbContext) : IAccessibleBlogService
{
    public async Task<IReadOnlyList<AccessibleBlog>> GetForUserAsync(
        string userId,
        string scheme,
        HostString host,
        CancellationToken cancellationToken = default)
    {
        var owned = await dbContext.Blogs
            .AsNoTracking()
            .Where(blog => blog.OwnerId == userId && blog.DeletedAt == null)
            .Select(blog => new { blog.Id, blog.Title, blog.Subdomain })
            .ToListAsync(cancellationToken);

        var memberships = await (
            from membership in dbContext.BlogMemberships.AsNoTracking()
            join blog in dbContext.Blogs.AsNoTracking() on membership.BlogId equals blog.Id
            where membership.UserId == userId && blog.DeletedAt == null
            select new { blog.Id, blog.Title, blog.Subdomain, membership.Role }
        ).ToListAsync(cancellationToken);

        string rootHost = host.Host;
        string port = host.Port.HasValue ? $":{host.Port}" : string.Empty;

        return owned
            .Select(blog => new AccessibleBlog(
                blog.Id,
                blog.Title,
                blog.Subdomain,
                Role: null,
                IsOwner: true,
                PublicUrl: $"{scheme}://{blog.Subdomain}.{rootHost}{port}"))
            .Concat(memberships.Select(blog => new AccessibleBlog(
                blog.Id,
                blog.Title,
                blog.Subdomain,
                blog.Role,
                IsOwner: false,
                PublicUrl: $"{scheme}://{blog.Subdomain}.{rootHost}{port}")))
            .OrderBy(blog => blog.Title)
            .ToList();
    }
}
