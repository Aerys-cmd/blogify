using Blogify.Web.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.ComponentModel.DataAnnotations;

namespace Blogify.Web.Pages.GetStarted;

public sealed class Step1Model(
    UserManager<ApplicationUser> userManager,
    SignInManager<ApplicationUser> signInManager) : PageModel
{
    [BindProperty]
    public required Step1Input Input { get; set; }

    public IActionResult OnGet()
    {
        if (User.Identity?.IsAuthenticated == true)
        {
            return RedirectToPage("/GetStarted/Step2");
        }

        return Page();
    }

    public async Task<IActionResult> OnPostAsync(CancellationToken ct)
    {
        if (!ModelState.IsValid)
        {
            return Page();
        }

        ApplicationUser user = new ApplicationUser
        {
            UserName = Input.Email,
            Email = Input.Email
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

        await userManager.AddToRoleAsync(user, "BlogAdmin");
        await signInManager.SignInAsync(user, isPersistent: false);

        return RedirectToPage("/GetStarted/Step2");
    }

    public sealed record Step1Input
    {
        [Required]
        [EmailAddress]
        [StringLength(256)]
        public required string Email { get; init; }

        [Required]
        [DataType(DataType.Password)]
        [StringLength(100, MinimumLength = 8)]
        public required string Password { get; init; }

        [Required]
        [DataType(DataType.Password)]
        [Compare(nameof(Password), ErrorMessage = "The password and confirmation password do not match.")]
        public required string ConfirmPassword { get; init; }
    }
}
