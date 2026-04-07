using System.ComponentModel.DataAnnotations;
using Blogify.Web.Data;
using Blogify.Web.Models;
using Blogify.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;

namespace Blogify.Web.Areas.SuperAdmin.Pages.Blogs;

[Authorize(Roles = "SuperAdmin")]
public sealed class EditModel(ApplicationDbContext dbContext, IStringLocalizer<SharedResource> localizer) : PageModel
{
    [BindProperty(SupportsGet = true)]
    public Guid Id { get; set; }

    [BindProperty]
    public EditBlogInput Input { get; set; } = new();

    public string TenantTitle { get; private set; } = string.Empty;

    public async Task<IActionResult> OnGetAsync(CancellationToken ct = default)
    {
        Tenant? tenant = await dbContext.Blogs
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.Id == Id, ct);

        if (tenant is null)
        {
            return NotFound();
        }

        TenantTitle = tenant.Title;
        Input = new EditBlogInput { Title = tenant.Title, Subdomain = tenant.Subdomain };

        return Page();
    }

    public async Task<IActionResult> OnPostAsync(CancellationToken ct = default)
    {
        Tenant? tenant = await dbContext.Blogs
            .FirstOrDefaultAsync(t => t.Id == Id, ct);

        if (tenant is null)
        {
            return NotFound();
        }

        TenantTitle = tenant.Title;

        if (!ModelState.IsValid)
        {
            return Page();
        }

        string normalizedSubdomain = Input.Subdomain.Trim().ToLowerInvariant();
        bool subdomainExists = await dbContext.Blogs
            .AnyAsync(t => t.Id != Id && t.Subdomain == normalizedSubdomain, ct);

        if (subdomainExists)
        {
            ModelState.AddModelError(nameof(Input.Subdomain), localizer["Message.SubdomainTaken"]);
            return Page();
        }

        tenant.Rename(Input.Title);
        tenant.ChangeSubdomain(Input.Subdomain);
        await dbContext.SaveChangesAsync(ct);

        return RedirectToPage("./Index");
    }
}

public sealed record EditBlogInput
{
    [Required, MaxLength(200)]
    public string Title { get; init; } = string.Empty;

    [Required, MaxLength(63)]
    [RegularExpression(@"^[a-z0-9-]+$")]
    public string Subdomain { get; init; } = string.Empty;
}
