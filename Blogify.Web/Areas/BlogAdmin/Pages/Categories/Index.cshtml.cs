using Blogify.Web.Data;
using Blogify.Web.Models;
using Blogify.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace Blogify.Web.Areas.BlogAdmin.Pages.Categories;

[Authorize]
public sealed class IndexModel(
    ApplicationDbContext dbContext,
    TenantContext tenantContext,
    IPublicBlogCacheInvalidator publicBlogCacheInvalidator) : PageModel
{
    public IReadOnlyList<CategoryListItem> Categories { get; private set; } = [];
    public string TenantTitle { get; private set; } = string.Empty;

    public async Task<IActionResult> OnGetAsync(CancellationToken ct = default)
    {
        TenantTitle = tenantContext.RequiredTenant.Title;

        Categories = await dbContext.Categories
            .AsNoTracking()
            .OrderBy(c => c.Name)
            .Select(c => new CategoryListItem(c.Id, c.Name, c.Slug))
            .ToListAsync(ct);

        return Page();
    }

    public async Task<IActionResult> OnPostDeleteAsync(Guid id, CancellationToken ct = default)
    {
        Category? category = await dbContext.Categories.FirstOrDefaultAsync(item => item.Id == id, ct);
        if (category is null)
        {
            return NotFound();
        }

        category.SoftDelete();
        await dbContext.SaveChangesAsync(ct);
        await publicBlogCacheInvalidator.InvalidateTenantAsync(category.BlogId, ct);
        return RedirectToPage(new { blogSlug = RouteData.Values["blogSlug"] });
    }
}

public sealed record CategoryListItem(Guid Id, string Name, string Slug);
