using Blogify.Web.Data;
using Blogify.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace Blogify.Web.Areas.BlogAdmin.Pages;

[Authorize]
public class IndexModel(ApplicationDbContext dbContext, TenantContext tenantContext) : PageModel
{
    public string TenantTitle { get; private set; } = string.Empty;
    public int TotalPostCount { get; private set; }
    public int PublishedPostCount { get; private set; }
    public int DraftPostCount => TotalPostCount - PublishedPostCount;

    public async Task<IActionResult> OnGetAsync()
    {
        if (!tenantContext.IsTenantResolved || tenantContext.CurrentTenant is null)
        {
            return NotFound("Blog admin area requires a tenant host (for example: yourblog.localhost).");
        }

        TenantTitle = tenantContext.CurrentTenant.Title;
        TotalPostCount = await dbContext.Posts.CountAsync();
        PublishedPostCount = await dbContext.Posts.CountAsync(post => post.PublishedRevisionId.HasValue);

        return Page();
    }
}

