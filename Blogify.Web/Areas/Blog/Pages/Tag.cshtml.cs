using Blogify.Web.Data;
using Blogify.Web.Models;
using Blogify.Web.Models.Posts;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace Blogify.Web.Areas.Blog.Pages;

public sealed class TagModel(ApplicationDbContext dbContext) : PageModel
{
    public string TagName { get; private set; } = string.Empty;
    public string TagSlug { get; private set; } = string.Empty;
    public IReadOnlyList<PostCardViewModel> Posts { get; private set; } = [];
    public BlogSidebarViewModel Sidebar { get; private set; } = new([], []);
    public PaginationViewModel Pagination { get; private set; } = new(1, 0, 0);

    public async Task<IActionResult> OnGetAsync(string slug, int page = 1, CancellationToken ct = default)
    {
        Tag? tag = await dbContext.Tags
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.Slug == slug, ct);

        if (tag is null)
        {
            return NotFound();
        }

        TagName = tag.Name;
        TagSlug = tag.Slug;

        ViewData["Title"] = tag.Name;
        ViewData["MetaTitle"] = tag.Name;
        ViewData["CanonicalUrl"] = $"{Request.Scheme}://{Request.Host}{Request.PathBase}{Request.Path}";

        Sidebar = await BlogPostListLoader.LoadSidebarAsync(dbContext, ct);

        IQueryable<Post> query =
            from pt in dbContext.PostTags.AsNoTracking()
            where pt.TagId == tag.Id
            join p in dbContext.Posts.AsNoTracking() on pt.PostId equals p.Id
            where p.PublishedRevisionId != null
            select p;

        PagedPostListViewModel list = await BlogPostListLoader.LoadPostsAsync(dbContext, query, page, ct);
        Posts = list.Posts;
        Pagination = list.Pagination;

        return Page();
    }
}
