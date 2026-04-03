using Blogify.Web.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace Blogify.Web.Areas.SuperAdmin.Pages;

[Authorize]
public class IndexModel(ApplicationDbContext dbContext) : PageModel
{
    public int BlogCount { get; private set; }

    public async Task OnGetAsync()
    {
        BlogCount = await dbContext.Blogs.CountAsync();
    }
}

