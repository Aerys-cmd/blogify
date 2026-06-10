using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Blogify.Web.Pages.GetStarted;

/// <summary>
/// The cross-subdomain admin redirect has been removed.
/// Admin access is now available at /app/admin/{blogSlug} on the root domain — no token handshake needed.
/// </summary>
[Authorize]
public sealed class AdminRedirectModel : PageModel
{
    public IActionResult OnGet(string? subdomain = null)
    {
        if (!string.IsNullOrWhiteSpace(subdomain))
            return Redirect($"/app/admin/{subdomain.Trim().ToLowerInvariant()}");

        return RedirectToPage("/Dashboard/Index");
    }
}
