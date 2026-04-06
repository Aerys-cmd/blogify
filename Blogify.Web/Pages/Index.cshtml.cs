using Blogify.Web.Data;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace Blogify.Web.Pages;

public sealed class IndexModel(ApplicationDbContext dbContext) : PageModel
{
    public int TotalBlogCount { get; private set; }

    public async Task OnGetAsync(CancellationToken ct)
    {
        TotalBlogCount = await dbContext.Blogs
            .AsNoTracking()
            .CountAsync(b => b.DeletedAt == null, ct);
    }
}
