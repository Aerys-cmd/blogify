using Blogify.Web.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace Blogify.Web.Areas.Blog.Pages;

public sealed class TagsModel(ApplicationDbContext dbContext) : PageModel
{
    public IReadOnlyList<TagLinkViewModel> Tags { get; private set; } = [];
    public BlogSidebarViewModel Sidebar { get; private set; } = new([], []);

    public async Task<IActionResult> OnGetAsync(CancellationToken ct = default)
    {
        Tags = await dbContext.Tags
            .AsNoTracking()
            .OrderBy(t => t.Name)
            .Select(t => new TagLinkViewModel(t.Name, t.Slug))
            .ToListAsync(ct);

        Sidebar = await BlogPostListLoader.LoadSidebarAsync(dbContext, ct);
        ViewData["Title"] = "Tags";
        ViewData["CanonicalUrl"] = $"{Request.Scheme}://{Request.Host}{Request.PathBase}{Request.Path}";
        return Page();
    }
}
