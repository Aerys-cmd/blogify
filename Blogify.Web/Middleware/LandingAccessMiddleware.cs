using Blogify.Web.Services;

namespace Blogify.Web.Middleware;

/// <summary>
/// Enforces correct host-based access for the public landing pages (root Pages/).
///
/// Two rules are applied:
/// 1. Root pages (no area) are only accessible on the root domain. When a tenant is
///    resolved (subdomain request), they return 404 — the landing page and registration
///    wizard must not be reachable from tenant subdomains.
/// 2. The Blog area index uses <c>@page "/"</c> which claims the absolute "/" route and
///    therefore wins over Pages/Index.cshtml for the "/" URL. On the root domain (no
///    tenant resolved), such Blog area hits are redirected to the landing page at /Index
///    rather than falling through to BlogAccessMiddleware which would return a bare 404.
///
/// Must be placed after <c>UseRouting()</c> (route values must be available),
/// <c>UseAuthentication()</c> (user identity must be populated), and
/// <c>UseTenantResolution()</c>, but before BlogAdminAccessMiddleware and
/// BlogAccessMiddleware.
/// </summary>
public sealed class LandingAccessMiddleware
{
    private readonly RequestDelegate _next;

    public LandingAccessMiddleware(RequestDelegate next)
    {
        ArgumentNullException.ThrowIfNull(next);
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context, TenantContext tenantContext)
    {
        string? area = context.GetRouteValue("area")?.ToString();

        // Rule 1 — Root pages (area = null) are root-domain only.
        // Tenant subdomain requests to these pages must return 404.
        if (string.IsNullOrEmpty(area) && tenantContext.IsTenantResolved)
        {
            context.Response.StatusCode = StatusCodes.Status404NotFound;
            await context.Response.WriteAsync("Not found.");
            return;
        }

        // Rule 2 — Blog area on root domain: the Blog area index page uses @page "/"
        // which claims the "/" route globally and would otherwise be blocked with a
        // plain 404 by BlogAccessMiddleware when no tenant is resolved. Intercept here
        // and redirect to the landing page at /Index so that root domain visitors land
        // on the marketing page rather than a 404.
        if (string.Equals(area, "Blog", StringComparison.OrdinalIgnoreCase) && !tenantContext.IsTenantResolved)
        {
            // SuperAdmin users have no blog — send them to their own dashboard.
            if (context.User.Identity?.IsAuthenticated == true && context.User.IsInRole("SuperAdmin"))
            {
                context.Response.Redirect("/sa", permanent: false);
                return;
            }

            context.Response.Redirect("/Index", permanent: false);
            return;
        }

        await _next(context);
    }
}

public static class LandingAccessMiddlewareExtensions
{
    public static IApplicationBuilder UseLandingAccess(this IApplicationBuilder builder)
    {
        return builder.UseMiddleware<LandingAccessMiddleware>();
    }
}
