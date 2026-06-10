using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Blogify.Web.Pages.GetStarted;

/// <summary>
/// Redirects to the custom Register page.
/// Registration is no longer part of the GetStarted wizard.
/// </summary>
public sealed class Step1Model : PageModel
{
    public IActionResult OnGet()
    {
        if (User.Identity?.IsAuthenticated == true)
            return RedirectToPage("/Dashboard/Index");

        return RedirectToPage("/Identity/Account/Register", new { area = "Identity" });
    }
}
