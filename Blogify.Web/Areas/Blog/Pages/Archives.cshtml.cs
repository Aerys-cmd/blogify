using Blogify.Web.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace Blogify.Web.Areas.Blog.Pages;

public sealed class ArchivesModel(ApplicationDbContext dbContext) : PageModel
{
    public IReadOnlyList<ArchiveMonthViewModel> Months { get; private set; } = [];
    public BlogSidebarViewModel Sidebar { get; private set; } = new([], []);

    public async Task<IActionResult> OnGetAsync(CancellationToken ct = default)
    {
        List<DateTimeOffset> dates = await (
            from p in dbContext.Posts.AsNoTracking()
            where p.PublishedRevisionId != null
            join r in dbContext.PostRevisions.AsNoTracking() on p.PublishedRevisionId equals r.Id
            orderby r.CreatedAt descending
            select r.CreatedAt
        ).ToListAsync(ct);

        Months = dates
            .GroupBy(d => new { d.Year, d.Month })
            .OrderByDescending(g => g.Key.Year)
            .ThenByDescending(g => g.Key.Month)
            .Select(g => new ArchiveMonthViewModel(g.Key.Year, g.Key.Month, g.Count()))
            .ToList();

        Sidebar = await BlogPostListLoader.LoadSidebarAsync(dbContext, ct);
        ViewData["Title"] = "Archives";
        ViewData["CanonicalUrl"] = $"{Request.Scheme}://{Request.Host}{Request.PathBase}{Request.Path}";
        return Page();
    }
}

public sealed record ArchiveMonthViewModel(int Year, int Month, int Count)
{
    public DateTime MonthStart => new(Year, Month, 1);
}
