using Blogify.Web.Data;
using Blogify.Web.Models;
using Blogify.Web.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace Blogify.Web.Areas.Blog.Pages;

public sealed class IndexModel(ApplicationDbContext dbContext, TenantContext tenantContext) : PageModel
{
    public string BlogTitle { get; private set; } = string.Empty;
    public IReadOnlyList<PostCardViewModel> Posts { get; private set; } = [];
    public BlogSidebarViewModel Sidebar { get; private set; } = new([], []);
    public PaginationViewModel Pagination { get; private set; } = new(1, 0, 0);

    public async Task<IActionResult> OnGetAsync(int page = 1, CancellationToken ct = default)
    {
        Tenant tenant = tenantContext.RequiredTenant;
        BlogTitle = tenant.Title;

        ViewData["Title"] = tenant.Title;
        ViewData["MetaTitle"] = tenant.MetaTitle ?? tenant.Title;
        ViewData["MetaDescription"] = tenant.MetaDescription;
        ViewData["OgType"] = "website";
        ViewData["CanonicalUrl"] = $"{Request.Scheme}://{Request.Host}{Request.PathBase}{Request.Path}";

        Sidebar = await BlogPostListLoader.LoadSidebarAsync(dbContext, ct);
        PagedPostListViewModel list = await BlogPostListLoader.LoadPostsAsync(
            dbContext,
            dbContext.Posts.AsNoTracking().Where(p => p.PublishedRevisionId != null),
            page,
            ct);
        Posts = list.Posts;
        Pagination = list.Pagination;

        return Page();
    }
}

public sealed record PostCardViewModel(
    string Slug,
    string Title,
    string? Excerpt,
    string? CoverImageUrl,
    DateTimeOffset PublishedAt,
    IReadOnlyList<CategoryLinkViewModel> Categories,
    IReadOnlyList<TagLinkViewModel> Tags);

public sealed record CategoryLinkViewModel(string Name, string Slug);
public sealed record TagLinkViewModel(string Name, string Slug);

public sealed record BlogSidebarViewModel(
    IReadOnlyList<CategoryLinkViewModel> Categories,
    IReadOnlyList<TagLinkViewModel> Tags);
