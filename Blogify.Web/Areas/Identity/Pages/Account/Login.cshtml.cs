using System.ComponentModel.DataAnnotations;
using Blogify.Web.Models;
using Blogify.Web.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Blogify.Web.Areas.Identity.Pages.Account;

public sealed class LoginModel(
    SignInManager<ApplicationUser> signInManager,
    UserManager<ApplicationUser> userManager,
    ILoginRedirectService loginRedirectService) : PageModel
{
    [BindProperty]
    public LoginInput Input { get; set; } = new();

    public string? ReturnUrl { get; private set; }
    public bool HasExplicitReturnUrl { get; private set; }

    public async Task OnGetAsync(string? returnUrl = null)
    {
        ReturnUrl = returnUrl;
        HasExplicitReturnUrl = !string.IsNullOrWhiteSpace(returnUrl);
        await HttpContext.SignOutAsync(IdentityConstants.ExternalScheme);
    }

    public async Task<IActionResult> OnPostAsync(string? returnUrl = null)
    {
        ReturnUrl = returnUrl;
        HasExplicitReturnUrl = !string.IsNullOrWhiteSpace(returnUrl);

        if (!ModelState.IsValid)
            return Page();

        Microsoft.AspNetCore.Identity.SignInResult result =
            await signInManager.PasswordSignInAsync(
                Input.Email,
                Input.Password,
                Input.RememberMe,
                lockoutOnFailure: false);

        if (result.Succeeded)
        {
            if (HasExplicitReturnUrl)
            {
                return LocalRedirect(ReturnUrl!);
            }

            ApplicationUser? user = await userManager.FindByEmailAsync(Input.Email);
            if (user is null)
            {
                return RedirectToPage("/Dashboard/Index");
            }

            string destination = await loginRedirectService.GetDestinationAsync(
                user.Id,
                Request.Scheme,
                Request.Host);

            return LocalRedirect(Url.Content(destination));
        }

        ModelState.AddModelError(string.Empty, "Invalid email or password.");
        return Page();
    }

    public sealed record LoginInput
    {
        [Required]
        [EmailAddress]
        public string Email { get; init; } = string.Empty;

        [Required]
        [DataType(DataType.Password)]
        public string Password { get; init; } = string.Empty;

        public bool RememberMe { get; init; }
    }
}
