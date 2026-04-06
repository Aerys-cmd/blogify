using Blogify.Web.Services;

namespace Blogify.Web.Middleware;

/// <summary>
/// Enforces tenant resolution for all requests targeting the Blog area.
/// When no tenant is resolved, authenticated SuperAdmin users are redirected to /sa
/// and all other users (authenticated or not) receive a 404 response, because no blog
/// exists at the current host. Must be placed after TenantResolutionMiddleware and
/// after UseAuthentication (so that context.User is populated), but before the
/// Authorization middleware.
/// </summary>
public sealed class BlogAccessMiddleware
{
    private readonly RequestDelegate _next;

    public BlogAccessMiddleware(RequestDelegate next)
    {
        ArgumentNullException.ThrowIfNull(next);
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context, TenantContext tenantContext)
    {
        // Step 1 — Scope check: only handle Blog area requests.
        string? area = context.GetRouteValue("area")?.ToString();
        if (!string.Equals(area, "Blog", StringComparison.OrdinalIgnoreCase))
        {
            await _next(context);
            return;
        }

        // Step 2 — Tenant guard: no blog exists at this host.
        if (!tenantContext.IsTenantResolved)
        {
            // SuperAdmin has no blog of their own — send them to the platform dashboard.
            if (context.User.Identity?.IsAuthenticated == true && context.User.IsInRole("SuperAdmin"))
            {
                context.Response.Redirect("/sa");
                return;
            }

            // Everyone else (unauthenticated or BlogAdmin without a resolved tenant)
            // should see a 404 — there is simply no blog at this host.
            context.Response.StatusCode = StatusCodes.Status404NotFound;
            return;
        }

        // Step 3 — Happy path: tenant is resolved, Blog area is public.
        await _next(context);
    }
}

public static class BlogAccessMiddlewareExtensions
{
    public static IApplicationBuilder UseBlogAccess(this IApplicationBuilder builder)
    {
        return builder.UseMiddleware<BlogAccessMiddleware>();
    }
}
