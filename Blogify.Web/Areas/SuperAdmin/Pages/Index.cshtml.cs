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

        Task<int> blogsTask = dbContext.Blogs.CountAsync(t => t.DeletedAt == null, ct);
        Task<int> usersTask = dbContext.Users.CountAsync(ct);
        Task<int> postsTask = dbContext.Posts.IgnoreQueryFilters().CountAsync(p => p.DeletedAt == null, ct);
        Task<int> newBlogsTask = dbContext.Blogs.CountAsync(t => t.DeletedAt == null && t.CreatedAt >= firstDayOfMonth, ct);

        await Task.WhenAll(blogsTask, usersTask, postsTask, newBlogsTask);

        TotalBlogs = blogsTask.Result;
        TotalUsers = usersTask.Result;
        TotalPosts = postsTask.Result;
        NewBlogsThisMonth = newBlogsTask.Result;
    }
}

