using System.ComponentModel.DataAnnotations;
using Blogify.Web.Data;
using Blogify.Web.Models;
using Blogify.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;

namespace Blogify.Web.Areas.BlogAdmin.Pages.Settings;

[Authorize]
public sealed class IndexModel(ApplicationDbContext dbContext, TenantContext tenantContext, IStringLocalizer<SharedResource> localizer) : PageModel
{
    [BindProperty]
    public BlogSettingsInput Input { get; set; } = new();

    public string BlogTitle { get; private set; } = string.Empty;

    public Task<IActionResult> OnGetAsync(CancellationToken ct = default)
    {
        Tenant tenant = tenantContext.RequiredTenant;

        BlogTitle = tenant.Title;
        Input = new BlogSettingsInput
        {
            MetaDescription = tenant.MetaDescription
        };

        return Task.FromResult<IActionResult>(Page());
    }

    public async Task<IActionResult> OnPostAsync(CancellationToken ct = default)
    {
        if (!ModelState.IsValid)
        {
            BlogTitle = tenantContext.RequiredTenant.Title;
            return Page();
        }

        Tenant? tenant = await dbContext.Blogs
            .FirstOrDefaultAsync(t => t.Id == tenantContext.RequiredTenant.Id, ct);

        if (tenant is null)
        {
            return NotFound();
        }

        tenant.UpdateSeoMetadata(Input.MetaDescription);
        await dbContext.SaveChangesAsync(ct);

        TempData["SuccessMessage"] = localizer["Message.SaveSuccess"].Value;
        return RedirectToPage(new { blogSlug = RouteData.Values["blogSlug"] });
    }
}

public sealed class BlogSettingsInput
{
    [MaxLength(160)]
    public string? MetaDescription { get; set; }
}
