using System.ComponentModel.DataAnnotations;
using Blogify.Web.Data;
using Blogify.Web.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace Blogify.Web.Areas.SuperAdmin.Pages.Users;

[Authorize(Roles = "SuperAdmin")]
public sealed class EditModel(
    ApplicationDbContext dbContext,
    UserManager<ApplicationUser> userManager) : PageModel
{
    [BindProperty(SupportsGet = true)]
    public string Id { get; set; } = string.Empty;

    [BindProperty]
    public EditUserInput Input { get; set; } = new();

    public string CurrentEmail { get; private set; } = string.Empty;
    public IReadOnlyList<TenantSelectItem> AvailableTenants { get; private set; } = [];

    public async Task<IActionResult> OnGetAsync(CancellationToken ct = default)
    {
        ApplicationUser? user = await userManager.FindByIdAsync(Id);
        if (user is null)
        {
            return NotFound();
        }

        IList<string> roles = await userManager.GetRolesAsync(user);
        CurrentEmail = user.Email ?? string.Empty;

        Input = new EditUserInput
        {
            Email = user.Email ?? string.Empty,
            UserName = user.UserName ?? string.Empty,
            Role = roles.FirstOrDefault() ?? string.Empty,
            TenantId = user.TenantId?.ToString() ?? string.Empty
        };

        await LoadTenantsAsync(ct);
        return Page();
    }

    public async Task<IActionResult> OnPostAsync(CancellationToken ct = default)
    {
        ApplicationUser? user = await userManager.FindByIdAsync(Id);
        if (user is null)
        {
            return NotFound();
        }

        CurrentEmail = user.Email ?? string.Empty;
        await LoadTenantsAsync(ct);

        if (!ModelState.IsValid)
        {
            return Page();
        }

        user.Email = Input.Email;
        user.UserName = Input.UserName;

        if (string.IsNullOrEmpty(Input.TenantId))
        {
            user.TenantId = null;
        }
        else if (Guid.TryParse(Input.TenantId, out Guid tenantId))
        {
            user.TenantId = tenantId;
        }
        else
        {
            ModelState.AddModelError($"{nameof(Input)}.{nameof(Input.TenantId)}", "The selected tenant is invalid.");
            return Page();
        }
        IdentityResult updateResult = await userManager.UpdateAsync(user);
        if (!updateResult.Succeeded)
        {
            foreach (IdentityError error in updateResult.Errors)
            {
                ModelState.AddModelError(string.Empty, error.Description);
            }
            return Page();
        }

        IList<string> currentRoles = await userManager.GetRolesAsync(user);
        if (currentRoles.Count > 0)
        {
            IdentityResult removeResult = await userManager.RemoveFromRolesAsync(user, currentRoles);
            if (!removeResult.Succeeded)
            {
                foreach (IdentityError error in removeResult.Errors)
                {
                    ModelState.AddModelError(string.Empty, error.Description);
                }
                return Page();
            }
        }

        if (!string.IsNullOrEmpty(Input.Role))
        {
            IdentityResult addResult = await userManager.AddToRoleAsync(user, Input.Role);
            if (!addResult.Succeeded)
            {
                foreach (IdentityError error in addResult.Errors)
                {
                    ModelState.AddModelError(string.Empty, error.Description);
                }
                return Page();
            }
        }

        return RedirectToPage("./Index");
    }

    private async Task LoadTenantsAsync(CancellationToken ct)
    {
        AvailableTenants = await dbContext.Blogs
            .AsNoTracking()
            .OrderBy(t => t.Title)
            .Select(t => new TenantSelectItem(t.Id, t.Title))
            .ToListAsync(ct);
    }
}

public sealed record EditUserInput
{
    [Required(ErrorMessage = "Email is required.")]
    [EmailAddress(ErrorMessage = "Enter a valid email address.")]
    public string Email { get; init; } = string.Empty;

    [Required(ErrorMessage = "Username is required.")]
    public string UserName { get; init; } = string.Empty;

    public string Role { get; init; } = string.Empty;

    public string TenantId { get; init; } = string.Empty;
}

public sealed record TenantSelectItem(Guid Id, string Title);
