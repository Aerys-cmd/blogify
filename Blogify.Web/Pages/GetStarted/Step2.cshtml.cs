using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Blogify.Web.Pages.GetStarted;

/// <summary>
/// Redirects to the new Dashboard/CreateBlog page.
/// Blog creation is now an explicit action from the dashboard, not a wizard step.
/// </summary>
[Authorize]
public sealed class Step2Model : PageModel
{
    public IActionResult OnGet() => RedirectToPage("/Dashboard/CreateBlog");
}
