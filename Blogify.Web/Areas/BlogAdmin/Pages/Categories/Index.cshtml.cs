using Blogify.Web.Data;
using Blogify.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace Blogify.Web.Areas.BlogAdmin.Pages.Categories;

[Authorize(Roles = "BlogAdmin")]
public sealed class IndexModel(ApplicationDbContext dbContext, TenantContext tenantContext) : PageModel
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
}

public sealed record CategoryListItem(Guid Id, string Name, string Slug);

