using System.ComponentModel.DataAnnotations;
using Blogify.Web.Data;
using Blogify.Web.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Localization;

namespace Blogify.Web.Areas.SuperAdmin.Pages.Users;

[Authorize(Roles = "SuperAdmin")]
public sealed class EditModel(
    UserManager<ApplicationUser> userManager,
    IStringLocalizer<SharedResource> localizer) : PageModel
{
    [BindProperty(SupportsGet = true)]
    public string Id { get; set; } = string.Empty;

    [BindProperty]
    public EditUserInput Input { get; set; } = new();

    public string CurrentEmail { get; private set; } = string.Empty;

    public async Task<IActionResult> OnGetAsync(CancellationToken ct = default)
    {
        ApplicationUser? user = await userManager.FindByIdAsync(Id);
        if (user is null)
            return NotFound();

        IList<string> roles = await userManager.GetRolesAsync(user);
        CurrentEmail = user.Email ?? string.Empty;

        Input = new EditUserInput
        {
            Email    = user.Email    ?? string.Empty,
            UserName = user.UserName ?? string.Empty,
            IsSuperAdmin = roles.Contains("SuperAdmin")
        };

        return Page();
    }

    public async Task<IActionResult> OnPostAsync(CancellationToken ct = default)
    {
        ApplicationUser? user = await userManager.FindByIdAsync(Id);
        if (user is null)
            return NotFound();

        CurrentEmail = user.Email ?? string.Empty;

        if (!ModelState.IsValid)
            return Page();

        user.Email    = Input.Email;
        user.UserName = Input.UserName;

        IdentityResult updateResult = await userManager.UpdateAsync(user);
        if (!updateResult.Succeeded)
        {
            foreach (IdentityError error in updateResult.Errors)
                ModelState.AddModelError(string.Empty, error.Description);
            return Page();
        }

        bool currentlySuperAdmin = await userManager.IsInRoleAsync(user, "SuperAdmin");
        if (Input.IsSuperAdmin && !currentlySuperAdmin)
        {
            IdentityResult addResult = await userManager.AddToRoleAsync(user, "SuperAdmin");
            if (!addResult.Succeeded)
            {
                foreach (IdentityError error in addResult.Errors)
                    ModelState.AddModelError(string.Empty, error.Description);
                return Page();
            }
        }
        else if (!Input.IsSuperAdmin && currentlySuperAdmin)
        {
            IdentityResult removeResult = await userManager.RemoveFromRoleAsync(user, "SuperAdmin");
            if (!removeResult.Succeeded)
            {
                foreach (IdentityError error in removeResult.Errors)
                    ModelState.AddModelError(string.Empty, error.Description);
                return Page();
            }
        }

        return RedirectToPage("./Index");
    }
}

public sealed record EditUserInput
{
    [Required]
    [EmailAddress]
    public string Email { get; init; } = string.Empty;

    [Required]
    public string UserName { get; init; } = string.Empty;

    public bool IsSuperAdmin { get; init; }
}
