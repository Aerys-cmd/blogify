using System.ComponentModel.DataAnnotations;
using Blogify.Web.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Blogify.Web.Areas.SuperAdmin.Pages.Users;

[Authorize(Roles = "SuperAdmin")]
public sealed class CreateModel(UserManager<ApplicationUser> userManager) : PageModel
{
    [BindProperty]
    public CreateUserInput Input { get; set; } = new();

    public IActionResult OnGet() => Page();

    public async Task<IActionResult> OnPostAsync(CancellationToken ct = default)
    {
        if (!ModelState.IsValid)
            return Page();

        ApplicationUser user = new()
        {
            UserName = Input.Email,
            Email = Input.Email,
            EmailConfirmed = true
        };

        IdentityResult result = await userManager.CreateAsync(user, Input.Password);
        if (!result.Succeeded)
        {
            foreach (IdentityError error in result.Errors)
                ModelState.AddModelError(string.Empty, error.Description);
            return Page();
        }

        // All users get the User role; SuperAdmin is optional.
        await userManager.AddToRoleAsync(user, "User");

        if (Input.IsSuperAdmin)
        {
            IdentityResult roleResult = await userManager.AddToRoleAsync(user, "SuperAdmin");
            if (!roleResult.Succeeded)
            {
                foreach (IdentityError error in roleResult.Errors)
                    ModelState.AddModelError(string.Empty, error.Description);
                return Page();
            }
        }

        return RedirectToPage("./Index");
    }
}

public sealed record CreateUserInput
{
    [Required]
    [EmailAddress]
    public string Email { get; init; } = string.Empty;

    [Required]
    [DataType(DataType.Password)]
    [MinLength(8)]
    public string Password { get; init; } = string.Empty;

    [Required]
    [DataType(DataType.Password)]
    [Compare(nameof(Password))]
    public string ConfirmPassword { get; init; } = string.Empty;

    public bool IsSuperAdmin { get; init; }
}
