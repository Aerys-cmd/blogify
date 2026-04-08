using System.Security.Claims;
using Blogify.Web.Models;
using Blogify.Web.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Blogify.Web.Middleware;

/// <summary>
/// Unified access control middleware that enforces host-based routing and tenant
/// ownership/membership rules for all Razor Pages areas.
///
/// Rules applied in order:
/// 1. No-area Razor Pages (root Pages/) are root-domain only. On a tenant subdomain
///    they return 404. Non-Razor endpoints (health checks, feeds, etc.) are exempt.
/// 2. Blog area on root domain (no tenant): serve the landing page inline via
///    endpoint replacement so the browser URL stays at "/" instead of redirecting to
///    "/Index". SuperAdmin users are redirected to /sa instead.
/// 3. Blog area on a tenant subdomain: public access, pass through.
/// 4. BlogAdmin area: requires tenant resolution, authentication, and tenant
///    ownership or membership. Unauthenticated users are redirected to the login page.
///    Authenticated users without ownership or membership receive 403.
/// 5. All other areas (SuperAdmin, Identity, …): pass through. Per-page
///    [Authorize] attributes handle fine-grained access for those areas.
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

    public async Task InvokeAsync(
        HttpContext context,
        TenantContext tenantContext,
        UserManager<ApplicationUser> userManager)
    {
        string? area = context.GetRouteValue("area")?.ToString();
        bool tenantResolved = tenantContext.IsTenantResolved;

        // --- Rule 1: no-area Razor Pages (root Pages/) are root-domain only ---
        // Non-Razor endpoints (health checks at /health, /alive; feeds at /sitemap.xml
        // and /rss.xml; culture endpoint; etc.) are not subject to this rule.
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

                // Serve the landing page inline at "/" without issuing a redirect so the
                // browser URL stays at "/" rather than showing "/Index". The Blog area
                // Index uses @page "/" which claims the root route globally; on the root
                // domain we replace the matched endpoint with the landing page endpoint
                // before continuing the pipeline, so the correct PageModel executes.
                Endpoint? landing = ResolveLandingPageEndpoint();
                if (landing is not null)
                {
                    context.SetEndpoint(landing);
                    // Remove the Blog area route value and set the landing page route so
                    // that downstream components (authorization metadata, URL generation
                    // helpers such as asp-page) see the correct context for the landing
                    // page rather than the replaced Blog area endpoint.
                    context.Request.RouteValues.Remove("area");
                    context.Request.RouteValues["page"] = "/Index";
                    await _next(context);
                    return;
                }

                // Fallback: landing endpoint could not be located (should not happen in
                // a correctly configured application).
                context.Response.StatusCode = StatusCodes.Status404NotFound;
                return;
            }

            // Tenant is resolved — Blog area is publicly accessible, pass through.
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

            // Authentication guard: redirect unauthenticated users to the login page.
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

            // Ownership check: the tenant owner always has full access.
            if (string.Equals(tenantContext.RequiredTenant.OwnerId, userId, StringComparison.Ordinal))
            {
                await _next(context);
                return;
            }

            // Membership check: non-owner users are allowed only when their TenantId
            // matches the currently resolved tenant.
            ApplicationUser? user = await userManager.FindByIdAsync(userId);
            if (user?.TenantId == tenantContext.RequiredTenant.Id)
            {
                await _next(context);
                return;
            }

            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            return;
        }

        // --- Rule 5: all other areas (SuperAdmin, Identity, …) ---
        // Per-page [Authorize] attributes handle access control for these areas.
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

        // First-writer-wins: if multiple requests race on startup the same endpoint
        // object is written by all of them, so the final state is always correct.
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
