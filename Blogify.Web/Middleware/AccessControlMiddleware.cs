using System.Security.Claims;
using Blogify.Web.Data;
using Blogify.Web.Models;
using Blogify.Web.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace Blogify.Web.Middleware;

/// <summary>
/// Unified access control middleware that enforces host-based routing and blog
/// ownership/membership rules for all Razor Pages areas.
///
/// Rules applied in order:
/// 1. No-area Razor Pages (root Pages/) are root-domain only. On a tenant subdomain
///    they return 404. Non-Razor endpoints (health checks, feeds, etc.) are exempt.
/// 2. Blog area on root domain (no tenant): serve the landing page inline via
///    endpoint replacement so the browser URL stays at "/" instead of redirecting to
///    "/Index". SuperAdmin users are redirected to /sa instead.
/// 3. Blog area on a tenant subdomain: public access, pass through.
/// 4. BlogAdmin area: requires tenant resolution (from route slug), authentication,
///    and blog ownership or membership. Unauthenticated users are redirected to login.
///    Authenticated users without access receive 403.
/// 5. All other areas (SuperAdmin, Identity, …): pass through.
///
/// Must be placed after UseRouting(), UseAuthentication(), and UseTenantResolution(),
/// but before UseAuthorization() and UseAnalyticsTracking().
/// </summary>
public sealed class AccessControlMiddleware
{
    /// <summary>Razor Pages relative path for the root landing page.</summary>
    private const string LandingPageRelativePath = "/Pages/Index.cshtml";

    private readonly RequestDelegate _next;
    private readonly IEnumerable<EndpointDataSource> _endpointSources;
    // Written at most once (benign first-writer-wins race via Interlocked).
    private Endpoint? _landingPageEndpoint;

    public AccessControlMiddleware(RequestDelegate next, IEnumerable<EndpointDataSource> endpointSources)
    {
        ArgumentNullException.ThrowIfNull(next);
        ArgumentNullException.ThrowIfNull(endpointSources);
        _next = next;
        _endpointSources = endpointSources;
    }

    public async Task InvokeAsync(HttpContext context, TenantContext tenantContext)
    {
        string? area = context.GetRouteValue("area")?.ToString();
        bool tenantResolved = tenantContext.IsTenantResolved;

        // --- Rule 1: no-area Razor Pages (root Pages/) are root-domain only ---
        if (string.IsNullOrEmpty(area))
        {
            if (tenantResolved)
            {
                bool isRazorPage = context.GetEndpoint()?.Metadata.GetMetadata<PageActionDescriptor>() is not null;
                if (isRazorPage)
                {
                    context.Response.StatusCode = StatusCodes.Status404NotFound;
                    await context.Response.WriteAsync("Not found.");
                    return;
                }
            }

            await _next(context);
            return;
        }

        // --- Rules 2 & 3: Blog area ---
        if (string.Equals(area, "Blog", StringComparison.OrdinalIgnoreCase))
        {
            if (!tenantResolved)
            {
                // SuperAdmin has no blog — send them to the platform dashboard.
                if (context.User.Identity?.IsAuthenticated == true && context.User.IsInRole("SuperAdmin"))
                {
                    context.Response.Redirect("/sa", permanent: false);
                    return;
                }

                // Serve the landing page inline at "/" without redirecting.
                Endpoint? landing = ResolveLandingPageEndpoint();
                if (landing is not null)
                {
                    context.SetEndpoint(landing);
                    context.Request.RouteValues.Remove("area");
                    context.Request.RouteValues["page"] = "/Index";
                    await _next(context);
                    return;
                }

                context.Response.StatusCode = StatusCodes.Status404NotFound;
                return;
            }

            await _next(context);
            return;
        }

        // --- Rule 4: BlogAdmin area ---
        if (string.Equals(area, "BlogAdmin", StringComparison.OrdinalIgnoreCase))
        {
            if (!tenantResolved)
            {
                context.Response.StatusCode = StatusCodes.Status404NotFound;
                await context.Response.WriteAsync("Blog not found.");
                return;
            }

            // Authentication guard: redirect unauthenticated users to login.
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

            // Ownership check: the blog owner always has full access.
            if (string.Equals(tenantContext.RequiredTenant.OwnerId, userId, StringComparison.Ordinal))
            {
                await _next(context);
                return;
            }

            // Membership check: user must have an active BlogMembership for this blog.
            ApplicationDbContext dbContext =
                context.RequestServices.GetRequiredService<ApplicationDbContext>();

            bool isMember = await dbContext.BlogMemberships
                .AnyAsync(m => m.BlogId == tenantContext.RequiredTenant.Id && m.UserId == userId);

            if (isMember)
            {
                await _next(context);
                return;
            }

            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            return;
        }

        // --- Rule 5: all other areas (SuperAdmin, Identity, …) ---
        await _next(context);
    }

    private Endpoint? ResolveLandingPageEndpoint()
    {
        if (_landingPageEndpoint is not null)
            return _landingPageEndpoint;

        Endpoint? found = _endpointSources
            .SelectMany(s => s.Endpoints)
            .FirstOrDefault(e =>
            {
                PageActionDescriptor? pad = e.Metadata.GetMetadata<PageActionDescriptor>();
                return pad is not null
                    && string.IsNullOrEmpty(pad.AreaName)
                    && string.Equals(pad.RelativePath, LandingPageRelativePath, StringComparison.OrdinalIgnoreCase);
            });

        Interlocked.CompareExchange(ref _landingPageEndpoint, found, null);
        return _landingPageEndpoint;
    }
}

public static class AccessControlMiddlewareExtensions
{
    public static IApplicationBuilder UseAccessControl(this IApplicationBuilder builder)
    {
        return builder.UseMiddleware<AccessControlMiddleware>();
    }
}
