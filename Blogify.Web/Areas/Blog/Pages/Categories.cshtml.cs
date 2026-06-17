using Blogify.Web.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace Blogify.Web.Areas.Blog.Pages;

public sealed class CategoriesModel(ApplicationDbContext dbContext) : PageModel
{
    public IReadOnlyList<CategoryLinkViewModel> Categories { get; private set; } = [];
    public BlogSidebarViewModel Sidebar { get; private set; } = new([], []);

    public async Task<IActionResult> OnGetAsync(CancellationToken ct = default)
    {
        Categories = await dbContext.Categories
            .AsNoTracking()
            .OrderBy(c => c.Name)
            .Select(c => new CategoryLinkViewModel(c.Name, c.Slug))
            .ToListAsync(ct);

        Sidebar = await BlogPostListLoader.LoadSidebarAsync(dbContext, ct);
        ViewData["Title"] = "Categories";
        ViewData["CanonicalUrl"] = $"{Request.Scheme}://{Request.Host}{Request.PathBase}{Request.Path}";
        return Page();
    }
}
