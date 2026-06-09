using Blogify.Web.Data;
using Blogify.Web.Models;
using Blogify.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Blogify.Web.Pages;

/// <summary>
/// Resolves the current user's blog tenant and redirects to the correct
/// subdomain admin URL (e.g. http://myblog.localhost/admin).
/// Linked from the landing layout navbar for authenticated BlogAdmin users.
/// </summary>
[Authorize(Roles = "BlogAdmin")]
public sealed class MyAdminModel(
    ApplicationDbContext dbContext,
    UserManager<ApplicationUser> userManager,
    IOptions<TenantOptions> tenantOptions) : PageModel
{
    public async Task<IActionResult> OnGetAsync(CancellationToken ct)
    {
        string? userId = userManager.GetUserId(User);
        if (userId is null)
        {
            // [Authorize] ensures authentication; this guard handles edge cases such
            // as a user record deleted after the cookie was issued.
            return RedirectToPage("/Account/Login", new { area = "Identity" });
        }

        Tenant? tenant = await dbContext.Blogs
            .AsNoTracking()
            .FirstOrDefaultAsync(b => b.OwnerId == userId && b.DeletedAt == null, ct);

        if (tenant is null)
        {
            return RedirectToPage("/GetStarted/Step2");
        }

        HostString currentHost = Request.Host;
        string scheme = Request.Scheme;
        string baseHost = ResolveBaseHost(currentHost.Host, tenantOptions.Value.PlatformHosts);

        // Prefix the tenant label onto the resolved platform host
        string tenantHost = baseHost.StartsWith(
            tenant.Subdomain + ".",
            StringComparison.OrdinalIgnoreCase)
            ? baseHost
            : $"{tenant.Subdomain}.{baseHost}";

        // Route through AdminRedirect so the user is signed in on the subdomain via a one-time cross-auth token before landing on the admin panel.
        string adminUrl = currentHost.Port.HasValue
            ? $"{scheme}://{tenantHost}:{currentHost.Port}/GetStarted/AdminRedirect?subdomain={tenant.Subdomain}"
            : $"{scheme}://{tenantHost}/GetStarted/AdminRedirect?subdomain={tenant.Subdomain}";

        return Redirect(adminUrl);
    }

    private static string ResolveBaseHost(string currentHost, IReadOnlyList<string> platformHosts)
    {
        List<string> normalizedPlatformHosts = platformHosts
            .Where(h => !string.IsNullOrWhiteSpace(h))
            .Select(h => h.Trim().ToLowerInvariant())
            .Distinct()
            .ToList();

        string? matchedHost = normalizedPlatformHosts
            .Where(h => currentHost.Equals(h, StringComparison.OrdinalIgnoreCase)
                        || currentHost.EndsWith($".{h}", StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(h => h.Length)
            .FirstOrDefault();

        if (matchedHost is not null)
        {
            return matchedHost;
        }

        string? preferredPlatformHost = normalizedPlatformHosts
            .Where(h => !h.StartsWith("www.", StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(h => h.Length)
            .FirstOrDefault();

        return preferredPlatformHost ?? currentHost;
    }
}
