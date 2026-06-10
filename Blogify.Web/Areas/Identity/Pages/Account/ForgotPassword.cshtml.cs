using System.ComponentModel.DataAnnotations;
using Blogify.Web.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Blogify.Web.Areas.Identity.Pages.Account;

public sealed class ForgotPasswordModel(UserManager<ApplicationUser> userManager) : PageModel
{
    [BindProperty]
    public ForgotPasswordInput Input { get; set; } = new();

    public bool EmailSent { get; private set; }

    public void OnGet() { }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid)
            return Page();

        // We always show the "email sent" message to prevent user enumeration,
        // even if no account exists for the given email.
        ApplicationUser? user = await userManager.FindByEmailAsync(Input.Email);
        if (user is not null && await userManager.IsEmailConfirmedAsync(user))
        {
            // TODO: Generate reset token and send email via IEmailSender once an
            // email provider is configured.
            // string token = await userManager.GeneratePasswordResetTokenAsync(user);
            // string callbackUrl = Url.Page("/Account/ResetPassword", null,
            //     new { area = "Identity", token = token, email = user.Email }, Request.Scheme)!;
            // await emailSender.SendEmailAsync(user.Email!, "Reset Password", ...);
        }

        EmailSent = true;
        return Page();
    }

    public sealed record ForgotPasswordInput
    {
        [Required]
        [EmailAddress]
        public string Email { get; init; } = string.Empty;
    }
}
