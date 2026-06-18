using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Blogify.Web.Areas.Identity.Pages.Account;

public sealed class AccessDeniedModel : PageModel
{
    public void OnGet()
    {
        Response.StatusCode = StatusCodes.Status403Forbidden;
    }
}
