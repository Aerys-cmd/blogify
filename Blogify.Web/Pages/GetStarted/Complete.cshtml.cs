using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Blogify.Web.Pages.GetStarted;

/// <summary>
/// The GetStarted wizard is no longer used. Users are redirected to the Dashboard.
/// </summary>
[Authorize]
public sealed class CompleteModel : PageModel
{
    public IActionResult OnGet() => RedirectToPage("/Dashboard/Index");
}
