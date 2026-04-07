using Blogify.Web.Data;
using Blogify.Web.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using System.Security.Cryptography;

namespace Blogify.Web.Pages.GetStarted;

[Authorize(Roles = "BlogAdmin")]
public sealed class AdminRedirectModel(
    ApplicationDbContext dbContext,
    UserManager<ApplicationUser> userManager,
    IMemoryCache cache) : PageModel
{
    public async Task<IActionResult> OnGetAsync(string subdomain, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(subdomain))
            return BadRequest();

        string? userId = userManager.GetUserId(User);
        if (userId is null)
            return RedirectToPage("/GetStarted/Step1");

        string normalizedSubdomain = subdomain.Trim().ToLowerInvariant();

        bool tenantBelongsToUser = await dbContext.Blogs
            .AsNoTracking()
            .AnyAsync(b => b.Subdomain == normalizedSubdomain
                           && b.OwnerId == userId
                           && b.DeletedAt == null, ct);

        if (!tenantBelongsToUser)
            return Forbid();

        string token = Convert.ToHexString(RandomNumberGenerator.GetBytes(32)).ToLowerInvariant();

        cache.Set(
            $"crossauth:{token}",
            userId,
            new MemoryCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(60)
            });

        string scheme = Request.Scheme;
        string host = Request.Host.Host;
        string portSuffix = Request.Host.Port.HasValue ? $":{Request.Host.Port}" : string.Empty;

        string redirectUrl = $"{scheme}://{normalizedSubdomain}.{host}{portSuffix}/crossauth?token={token}";
        return Redirect(redirectUrl);
    }
}

