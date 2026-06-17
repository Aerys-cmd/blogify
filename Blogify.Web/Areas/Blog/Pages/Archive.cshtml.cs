using Blogify.Web.Data;
using Blogify.Web.Models.Posts;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace Blogify.Web.Areas.Blog.Pages;

public sealed class ArchiveModel(ApplicationDbContext dbContext) : PageModel
{
    public int Year { get; private set; }
    public int Month { get; private set; }
    public DateTime MonthStart { get; private set; }
    public IReadOnlyList<PostCardViewModel> Posts { get; private set; } = [];
    public BlogSidebarViewModel Sidebar { get; private set; } = new([], []);
    public PaginationViewModel Pagination { get; private set; } = new(1, 0, 0);

    public async Task<IActionResult> OnGetAsync(int year, int month, int page = 1, CancellationToken ct = default)
    {
        if (year < 1 || month is < 1 or > 12)
        {
            return NotFound();
        }

        Year = year;
        Month = month;
        MonthStart = new DateTime(year, month, 1);
        DateTimeOffset start = new(year, month, 1, 0, 0, 0, TimeSpan.Zero);
        DateTimeOffset end = start.AddMonths(1);

        ViewData["Title"] = MonthStart.ToString("MMMM yyyy");
        ViewData["MetaTitle"] = ViewData["Title"];
        ViewData["CanonicalUrl"] = $"{Request.Scheme}://{Request.Host}{Request.PathBase}{Request.Path}";

        Sidebar = await BlogPostListLoader.LoadSidebarAsync(dbContext, ct);

        IQueryable<Post> query =
            from p in dbContext.Posts.AsNoTracking()
            where p.PublishedRevisionId != null
            join r in dbContext.PostRevisions.AsNoTracking() on p.PublishedRevisionId equals r.Id
            where r.CreatedAt >= start && r.CreatedAt < end
            select p;

        PagedPostListViewModel list = await BlogPostListLoader.LoadPostsAsync(dbContext, query, page, ct);
        Posts = list.Posts;
        Pagination = list.Pagination;

        return Page();
    }
}
