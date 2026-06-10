using System.ComponentModel.DataAnnotations;
using Blogify.Web.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Blogify.Web.Areas.Identity.Pages.Account;

public sealed class ResetPasswordModel(UserManager<ApplicationUser> userManager) : PageModel
{
    [BindProperty]
    public ResetPasswordInput Input { get; set; } = new();

    public bool Succeeded { get; private set; }

    public IActionResult OnGet(string? token = null, string? email = null)
    {
        if (token is null || email is null)
            return BadRequest("A token and email are required to reset the password.");

        Input = new ResetPasswordInput { Token = token, Email = email };
        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid)
            return Page();

        ApplicationUser? user = await userManager.FindByEmailAsync(Input.Email);
        if (user is null)
        {
            // Do not reveal that the user does not exist.
            Succeeded = true;
            return Page();
        }

        IdentityResult result = await userManager.ResetPasswordAsync(user, Input.Token, Input.Password);
        if (result.Succeeded)
        {
            Succeeded = true;
            return Page();
        }

        foreach (IdentityError error in result.Errors)
            ModelState.AddModelError(string.Empty, error.Description);

        return Page();
    }

    public sealed record ResetPasswordInput
    {
        [Required]
        public string Token { get; init; } = string.Empty;

        [Required]
        [EmailAddress]
        public string Email { get; init; } = string.Empty;

        [Required]
        [DataType(DataType.Password)]
        [StringLength(100, MinimumLength = 8)]
        public string Password { get; init; } = string.Empty;

        [Required]
        [DataType(DataType.Password)]
        [Compare(nameof(Password))]
        public string ConfirmPassword { get; init; } = string.Empty;
    }
}
