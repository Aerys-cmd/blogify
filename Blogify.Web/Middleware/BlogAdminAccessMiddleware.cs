using System.Security.Claims;
using Blogify.Web.Models;
using Blogify.Web.Services;
using Microsoft.AspNetCore.Identity;

namespace Blogify.Web.Middleware;

/// <summary>
/// Enforces tenant existence and tenant ownership / membership for all requests
/// targeting the BlogAdmin area. Must be placed after TenantResolutionMiddleware
/// and before the Authorization middleware.
/// </summary>
public sealed class BlogAdminAccessMiddleware
{
    private readonly RequestDelegate _next;

    public BlogAdminAccessMiddleware(RequestDelegate next)
    {
        ArgumentNullException.ThrowIfNull(next);
        _next = next;
    }

    public async Task InvokeAsync(
        HttpContext context,
        TenantContext tenantContext,
        UserManager<ApplicationUser> userManager)
    {
        // Step 1 — Scope check: only handle BlogAdmin area requests.
        string? area = context.GetRouteValue("area")?.ToString();
        if (!string.Equals(area, "BlogAdmin", StringComparison.OrdinalIgnoreCase))
        {
            await _next(context);
            return;
        }

        // Step 2 — Tenant validation: the tenant must have been resolved by TenantResolutionMiddleware.
        if (!tenantContext.IsTenantResolved)
        {
            context.Response.StatusCode = StatusCodes.Status404NotFound;
            await context.Response.WriteAsync("Blog not found.");
            return;
        }

        // Step 3 — Authentication guard: redirect unauthenticated users to the login page.
        if (context.User.Identity is not { IsAuthenticated: true })
        {
            string returnUrl = context.Request.PathBase + context.Request.Path + context.Request.QueryString;
            context.Response.Redirect($"/Identity/Account/Login?ReturnUrl={Uri.EscapeDataString(returnUrl)}");
            return;
        }

        string? userId = context.User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userId is null)
        {
            string returnUrl = context.Request.PathBase + context.Request.Path + context.Request.QueryString;
            context.Response.Redirect($"/Identity/Account/Login?ReturnUrl={Uri.EscapeDataString(returnUrl)}");
            return;
        }

        // Step 3 (continued) — Ownership check: the tenant owner always has access.
        if (string.Equals(tenantContext.RequiredTenant.OwnerId, userId, StringComparison.Ordinal))
        {
            await _next(context);
            return;
        }

        // Step 4 — Membership check: non-owner users are allowed only when their TenantId
        // matches the currently resolved tenant.
        ApplicationUser? user = await userManager.FindByIdAsync(userId);
        if (user?.TenantId == tenantContext.RequiredTenant.Id)
        {
            await _next(context);
            return;
        }

        context.Response.StatusCode = StatusCodes.Status403Forbidden;
    }
}

public static class BlogAdminAccessMiddlewareExtensions
{
    public static IApplicationBuilder UseBlogAdminAccess(this IApplicationBuilder builder)
    {
        return builder.UseMiddleware<BlogAdminAccessMiddleware>();
    }
}

