using Blogify.Web.Data;
using Blogify.Web.Models.Posts;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace Blogify.Web.Areas.Blog.Pages;

public sealed class SearchModel(ApplicationDbContext dbContext) : PageModel
{
    [BindProperty(SupportsGet = true, Name = "q")]
    public string? Query { get; set; }

    public IReadOnlyList<PostCardViewModel> Posts { get; private set; } = [];
    public BlogSidebarViewModel Sidebar { get; private set; } = new([], []);
    public PaginationViewModel Pagination { get; private set; } = new(1, 0, 0);

    public async Task<IActionResult> OnGetAsync(int page = 1, CancellationToken ct = default)
    {
        ViewData["Title"] = string.IsNullOrWhiteSpace(Query) ? "Search" : Query.Trim();
        ViewData["MetaTitle"] = ViewData["Title"];
        ViewData["CanonicalUrl"] = $"{Request.Scheme}://{Request.Host}{Request.PathBase}{Request.Path}";

        Sidebar = await BlogPostListLoader.LoadSidebarAsync(dbContext, ct);

        IQueryable<Post> query = dbContext.Posts
            .AsNoTracking()
            .Where(p => p.PublishedRevisionId != null);

        if (!string.IsNullOrWhiteSpace(Query))
        {
            string searchPattern = $"%{Query.Trim()}%";
            query =
                from p in query
                join r in dbContext.PostRevisions.AsNoTracking() on p.PublishedRevisionId equals r.Id
                where EF.Functions.Like(r.Title, searchPattern)
                    || (p.Excerpt != null && EF.Functions.Like(p.Excerpt, searchPattern))
                    || (r.ContentText != null && EF.Functions.Like(r.ContentText, searchPattern))
                select p;
        }
        else
        {
            query = query.Where(_ => false);
        }

        PagedPostListViewModel list = await BlogPostListLoader.LoadPostsAsync(dbContext, query, page, ct);
        Posts = list.Posts;
        Pagination = list.Pagination;

        return Page();
    }
}
