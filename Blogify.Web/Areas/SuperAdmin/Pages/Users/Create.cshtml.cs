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
        {
            return Page();
        }

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
            {
                ModelState.AddModelError(string.Empty, error.Description);
            }
            return Page();
        }

        IdentityResult roleResult = await userManager.AddToRoleAsync(user, Input.Role);
        if (!roleResult.Succeeded)
        {
            foreach (IdentityError error in roleResult.Errors)
            {
                ModelState.AddModelError(string.Empty, error.Description);
            }
            return Page();
        }

        return RedirectToPage("./Index");
    }
}

public sealed record CreateUserInput
{
    [Required(ErrorMessage = "Email is required.")]
    [EmailAddress(ErrorMessage = "Enter a valid email address.")]
    public string Email { get; init; } = string.Empty;

    [Required(ErrorMessage = "Password is required.")]
    [DataType(DataType.Password)]
    [MinLength(8, ErrorMessage = "Password must be at least 8 characters.")]
    public string Password { get; init; } = string.Empty;

    [Required(ErrorMessage = "Please confirm your password.")]
    [DataType(DataType.Password)]
    [Compare(nameof(Password), ErrorMessage = "Passwords do not match.")]
    public string ConfirmPassword { get; init; } = string.Empty;

    [Required(ErrorMessage = "Role is required.")]
    public string Role { get; init; } = string.Empty;
}
