using Blogify.Web.Data;
using Blogify.Web.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace Blogify.Web.Pages.GetStarted;

[Authorize(Roles = "BlogAdmin")]
public sealed class CompleteModel(
    ApplicationDbContext dbContext,
    UserManager<ApplicationUser> userManager) : PageModel
{
    public string BlogTitle { get; private set; } = string.Empty;
    public string BlogSubdomain { get; private set; } = string.Empty;
    public string AdminUrl { get; private set; } = string.Empty;
    public string BlogUrl { get; private set; } = string.Empty;

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

        BlogTitle = tenant.Title;
        BlogSubdomain = tenant.Subdomain;

        string scheme = Request.Scheme;
        string hostName = Request.Host.Host;
        int dotIndex = hostName.IndexOf('.');
        string rootDomain = dotIndex >= 0 ? hostName[(dotIndex + 1)..] : hostName;
        string portSuffix = Request.Host.Port.HasValue ? $":{Request.Host.Port}" : string.Empty;
        AdminUrl = $"{scheme}://{tenant.Subdomain}.{rootDomain}{portSuffix}/admin";
        BlogUrl = $"{scheme}://{tenant.Subdomain}.{rootDomain}{portSuffix}";

        return Page();
    }
}
