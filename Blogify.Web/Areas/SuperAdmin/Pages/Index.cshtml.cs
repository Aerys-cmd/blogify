using Blogify.Web.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace Blogify.Web.Areas.SuperAdmin.Pages;

[Authorize(Roles = "SuperAdmin")]
public sealed class IndexModel(ApplicationDbContext dbContext) : PageModel
{
    public int TotalBlogs { get; private set; }
    public int TotalUsers { get; private set; }
    public int TotalPosts { get; private set; }
    public int NewBlogsThisMonth { get; private set; }

    public async Task OnGetAsync(CancellationToken ct = default)
    {
        DateTimeOffset firstDayOfMonth = new DateTimeOffset(
            DateTimeOffset.UtcNow.Year,
            DateTimeOffset.UtcNow.Month,
            1, 0, 0, 0, TimeSpan.Zero);

        TotalBlogs = await dbContext.Blogs.CountAsync(ct);
        TotalUsers = await dbContext.Users.CountAsync(ct);
        TotalPosts  = await dbContext.Posts.IgnoreQueryFilters().CountAsync(p => p.DeletedAt == null, ct);
        NewBlogsThisMonth = await dbContext.Blogs.CountAsync(t => t.CreatedAt >= firstDayOfMonth, ct);
    }
}

