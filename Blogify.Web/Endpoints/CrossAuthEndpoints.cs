using Blogify.Web.Models;
using Blogify.Web.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Caching.Memory;

namespace Blogify.Web.Endpoints;

public static class CrossAuthEndpoints
{
    /// <summary>
    /// Maps the /crossauth endpoint used for cross-subdomain authentication handshake.
    /// AdminRedirect (root domain) mints a one-time token tied to the current user and
    /// redirects here. This endpoint validates the token, signs the user in on the
    /// subdomain, discards the token, then redirects to the admin panel.
    /// It is intentionally NOT inside the BlogAdmin area so BlogAdminAccessMiddleware
    /// does not intercept it before the sign-in has been completed.
    /// </summary>
    public static IEndpointRouteBuilder MapCrossAuthEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/crossauth", async (
            string? token,
            IMemoryCache cache,
            UserManager<ApplicationUser> userManager,
            SignInManager<ApplicationUser> signInManager,
            TenantContext tenantContext) =>
        {
            if (string.IsNullOrWhiteSpace(token))
                return Results.BadRequest("Missing token.");

            string cacheKey = $"crossauth:{token}";

            if (!cache.TryGetValue(cacheKey, out string? userId) || string.IsNullOrEmpty(userId))
                return Results.BadRequest("Token is invalid or has expired.");

            // Consume immediately — one-time use only.
            cache.Remove(cacheKey);

            if (!tenantContext.IsTenantResolved)
                return Results.NotFound("Blog not found.");

            ApplicationUser? user = await userManager.FindByIdAsync(userId);
            if (user is null)
                return Results.BadRequest("User not found.");

            // Verify the user is the tenant owner or a member to prevent token misuse.
            bool isOwner = string.Equals(tenantContext.RequiredTenant.OwnerId, user.Id, StringComparison.Ordinal);
            bool isMember = user.TenantId == tenantContext.RequiredTenant.Id;
            if (!isOwner && !isMember)
                return Results.Forbid();

            await signInManager.SignInAsync(user, isPersistent: false);

            return Results.Redirect("/admin");
        });

        return app;
    }
}


