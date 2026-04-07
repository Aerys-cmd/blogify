using System.ComponentModel.DataAnnotations;
using Blogify.Web.Data;
using Blogify.Web.Models;
using Blogify.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;

namespace Blogify.Web.Areas.SuperAdmin.Pages.Blogs;

[Authorize(Roles = "SuperAdmin")]
public sealed class CreateModel(ApplicationDbContext dbContext, UserManager<ApplicationUser> userManager, IStringLocalizer<SharedResource> localizer) : PageModel
{
    [BindProperty]
    public CreateBlogInput Input { get; set; } = new();

    public IReadOnlyList<OwnerSelectItem> AvailableOwners { get; private set; } = [];

    public async Task<IActionResult> OnGetAsync(CancellationToken ct = default)
    {
        await LoadOwnersAsync(ct);
        return Page();
    }

    public async Task<IActionResult> OnPostAsync(CancellationToken ct = default)
    {
        await LoadOwnersAsync(ct);

        if (!ModelState.IsValid)
        {
            return Page();
        }

        string normalizedSubdomain = Input.Subdomain.Trim().ToLowerInvariant();
        bool subdomainExists = await dbContext.Blogs
            .AnyAsync(t => t.Subdomain == normalizedSubdomain, ct);

        if (subdomainExists)
        {
            ModelState.AddModelError(nameof(Input.Subdomain), localizer["Message.SubdomainTaken"]);
            return Page();
        }

        Tenant tenant = Tenant.Create(Input.Title, Input.Subdomain, Input.OwnerId);
        dbContext.Blogs.Add(tenant);
        await dbContext.SaveChangesAsync(ct);

        return RedirectToPage("./Index");
    }

    private async Task LoadOwnersAsync(CancellationToken ct)
    {
        IList<ApplicationUser> superAdmins = await userManager.GetUsersInRoleAsync("SuperAdmin");
        HashSet<string> superAdminIds = superAdmins.Select(u => u.Id).ToHashSet();

        AvailableOwners = await dbContext.Users
            .AsNoTracking()
            .Where(u => u.TenantId == null && !superAdminIds.Contains(u.Id))
            .Select(u => new OwnerSelectItem(u.Id, u.Email ?? u.UserName ?? u.Id))
            .ToListAsync(ct);
    }
}

public sealed record CreateBlogInput
{
    [Required, MaxLength(200)]
    public string Title { get; init; } = string.Empty;

    [Required, MaxLength(63)]
    [RegularExpression(@"^[a-z0-9-]+$")]
    public string Subdomain { get; init; } = string.Empty;

    [Required]
    public string OwnerId { get; init; } = string.Empty;
}

public sealed record OwnerSelectItem(string Id, string Email);
