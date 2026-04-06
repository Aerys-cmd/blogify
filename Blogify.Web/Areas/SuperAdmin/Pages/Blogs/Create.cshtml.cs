using System.ComponentModel.DataAnnotations;
using Blogify.Web.Data;
using Blogify.Web.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace Blogify.Web.Areas.SuperAdmin.Pages.Blogs;

[Authorize(Roles = "SuperAdmin")]
public sealed class CreateModel(ApplicationDbContext dbContext, UserManager<ApplicationUser> userManager) : PageModel
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
            .AnyAsync(t => t.DeletedAt == null && t.Subdomain == normalizedSubdomain, ct);

        if (subdomainExists)
        {
            ModelState.AddModelError(nameof(Input.Subdomain), "This subdomain is already taken.");
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
    [Required(ErrorMessage = "Title is required."), MaxLength(200)]
    public string Title { get; init; } = string.Empty;

    [Required(ErrorMessage = "Subdomain is required."), MaxLength(63)]
    [RegularExpression(@"^[a-z0-9-]+$", ErrorMessage = "Subdomain may only contain lowercase letters, digits, and hyphens.")]
    public string Subdomain { get; init; } = string.Empty;

    [Required(ErrorMessage = "Owner is required.")]
    public string OwnerId { get; init; } = string.Empty;
}

public sealed record OwnerSelectItem(string Id, string Email);
