using Blogify.Web.Data;
using Blogify.Web.Services;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace Blogify.Web.Areas.Blog.Pages;

public class IndexModel(ApplicationDbContext dbContext, TenantContext tenantContext) : PageModel
{
    public bool IsTenantView => tenantContext.IsTenantResolved;
    public string TenantTitle { get; private set; } = string.Empty;
    public IReadOnlyList<BlogSummary> Blogs { get; private set; } = [];
    public IReadOnlyList<PublishedPostSummary> PublishedPosts { get; private set; } = [];

    public async Task OnGetAsync()
    {
        if (tenantContext.IsTenantResolved && tenantContext.CurrentTenant is not null)
        {
            TenantTitle = tenantContext.CurrentTenant.Title;

            PublishedPosts = await dbContext.Posts
                .AsNoTracking()
                .Where(post => post.PublishedRevisionId.HasValue)
                .Select(post => new PublishedPostSummary(
                    post.Slug,
                    dbContext.PostRevisions
                        .Where(revision => revision.Id == post.PublishedRevisionId)
                        .Select(revision => revision.Title)
                        .FirstOrDefault() ?? post.Slug))
                .ToListAsync();

            return;
        }

        Blogs = await dbContext.Blogs
            .AsNoTracking()
            .OrderBy(blog => blog.Title)
            .Select(blog => new BlogSummary(blog.Title, blog.Subdomain))
            .ToListAsync();
    }

    public sealed record BlogSummary(string Title, string Subdomain);
    public sealed record PublishedPostSummary(string Slug, string Title);
}

