using Blogify.Web.Data;
using Blogify.Web.Models;
using Blogify.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace Blogify.Web.Areas.BlogAdmin.Pages.Tags;

[Authorize]
public sealed class IndexModel(
    ApplicationDbContext dbContext,
    TenantContext tenantContext,
    IPublicBlogCacheInvalidator publicBlogCacheInvalidator) : PageModel
{
    public IReadOnlyList<TagListItem> Tags { get; private set; } = [];
    public string TenantTitle { get; private set; } = string.Empty;

    public async Task<IActionResult> OnGetAsync(CancellationToken ct = default)
    {
        TenantTitle = tenantContext.RequiredTenant.Title;

        Tags = await dbContext.Tags
            .AsNoTracking()
            .OrderBy(t => t.Name)
            .Select(t => new TagListItem(t.Id, t.Name, t.Slug))
            .ToListAsync(ct);

        return Page();
    }

    public async Task<IActionResult> OnPostDeleteAsync(Guid id, CancellationToken ct = default)
    {
        Tag? tag = await dbContext.Tags.FirstOrDefaultAsync(item => item.Id == id, ct);
        if (tag is null)
        {
            return NotFound();
        }

        tag.SoftDelete();
        await dbContext.SaveChangesAsync(ct);
        await publicBlogCacheInvalidator.InvalidateTenantAsync(tag.BlogId, ct);
        return RedirectToPage(new { blogSlug = RouteData.Values["blogSlug"] });
    }
}

public sealed record TagListItem(Guid Id, string Name, string Slug);
