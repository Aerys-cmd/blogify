using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Blogify.Web.Pages;

public sealed class IndexModel : PageModel
{
    public IActionResult OnGet()
    {
        if (User.Identity?.IsAuthenticated != true)
        {
            return RedirectToPage("/Account/Login", new { area = "Identity" });
        }

        if (User.IsInRole("SuperAdmin"))
        {
            return RedirectToPage("/Index", new { area = "SuperAdmin" });
        }

        return RedirectToPage("/Account/Login", new { area = "Identity" });
    }
}
