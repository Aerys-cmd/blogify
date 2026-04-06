using Blogify.Web.Data;
using Blogify.Web.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace Blogify.Web.Pages;

/// <summary>
/// Resolves the current user's blog tenant and redirects to the correct
/// subdomain admin URL (e.g. http://myblog.localhost/admin).
/// Linked from the landing layout navbar for authenticated BlogAdmin users.
/// </summary>
[Authorize(Roles = "BlogAdmin")]
public sealed class MyAdminModel(
    ApplicationDbContext dbContext,
    UserManager<ApplicationUser> userManager) : PageModel
{
    public async Task<IActionResult> OnGetAsync(CancellationToken ct)
    {
        string? userId = userManager.GetUserId(User);
        if (userId is null)
        {
            return RedirectToPage("/GetStarted/Step1");
        }

        Tenant? tenant = await dbContext.Blogs
            .AsNoTracking()
            .FirstOrDefaultAsync(b => b.OwnerId == userId && b.DeletedAt == null, ct);

        if (tenant is null)
        {
            return RedirectToPage("/GetStarted/Step2");
        }

        HostString currentHost = Request.Host;
        string rootHost = currentHost.Host;
        string scheme = Request.Scheme;

        // Build the tenant admin URL: {scheme}://{subdomain}.{rootHost}/admin
        // preserving any non-standard port (e.g. myblog.localhost:5001/admin in dev).
        string adminUrl = currentHost.Port.HasValue
            ? $"{scheme}://{tenant.Subdomain}.{rootHost}:{currentHost.Port}/admin"
            : $"{scheme}://{tenant.Subdomain}.{rootHost}/admin";

        return Redirect(adminUrl);
    }
}
