using System.ComponentModel.DataAnnotations;
using System.Globalization;
using Blogify.Web.Models;
using Blogify.Web.Services.Email;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Blogify.Web.Areas.Identity.Pages.Account;

public sealed class ForgotPasswordModel(
    UserManager<ApplicationUser> userManager,
    IAppEmailSender emailSender,
    ILogger<ForgotPasswordModel> logger) : PageModel
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
        if (user?.Email is not null)
        {
            try
            {
                string token = await userManager.GeneratePasswordResetTokenAsync(user);
                await emailSender.SendPasswordResetAsync(
                    user.Email,
                    token,
                    CultureInfo.CurrentUICulture,
                    HttpContext.RequestAborted);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "Failed to enqueue password reset email.");
            }
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
