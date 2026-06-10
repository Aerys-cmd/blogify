using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Blogify.Web.Pages;

/// <summary>
/// Redirects authenticated users to the centralized blog dashboard.
/// The cross-subdomain admin handshake has been replaced by direct admin access
/// at /app/admin/{blogSlug} on the root domain.
/// </summary>
[Authorize]
public sealed class MyAdminModel : PageModel
{
    public IActionResult OnGet() => RedirectToPage("/Dashboard/Index");
}
