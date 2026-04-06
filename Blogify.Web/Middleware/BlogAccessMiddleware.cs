using Blogify.Web.Services;

namespace Blogify.Web.Middleware;

/// <summary>
/// Enforces tenant resolution for all requests targeting the Blog area.
/// When no tenant is resolved, authenticated SuperAdmin users are redirected to /sa,
/// authenticated non-SuperAdmin users are redirected to /admin, and unauthenticated
/// users are redirected to the login page. Must be placed after
/// TenantResolutionMiddleware and after UseAuthentication (so that context.User is
/// populated), but before the Authorization middleware.
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

        // Step 2 — Tenant guard: redirect when no tenant has been resolved.
        if (!tenantContext.IsTenantResolved)
        {
            if (context.User.Identity?.IsAuthenticated == true)
            {
                if (context.User.IsInRole("SuperAdmin"))
                {
                    context.Response.Redirect("/sa");
                    return;
                }

                context.Response.Redirect("/admin");
                return;
            }

            context.Response.Redirect("/Identity/Account/Login");
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
