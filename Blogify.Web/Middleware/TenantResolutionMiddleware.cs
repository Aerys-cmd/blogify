using Blogify.Web.Data;
using Blogify.Web.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Blogify.Web.Middleware
{
    public class TenantResolutionMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly TenantOptions _options;

        public TenantResolutionMiddleware(RequestDelegate next, IOptions<TenantOptions> options)
        {
            _next = next;
            _options = options.Value;
        }

        public async Task InvokeAsync(HttpContext context, ApplicationDbContext dbContext, TenantContext tenantContext)
        {
            var host = context.Request.Host.Host;
            dbContext.CurrentTenantId = null;
            tenantContext.Clear();

            // Handle root domain (admin panel / dashboard)
            // Local fallback uses "localhost" or specific explicit bindings.
            if (_options.PlatformHosts.Any(h => host.Equals(h, StringComparison.OrdinalIgnoreCase)))
            {
                // Unresolved tenant, allowed to proceed to dashboard routes
                await _next(context);
                return;
            }

            // Extract subdomain (e.g. myblog from myblog.localhost)
            var parts = host.Split('.');
            if (parts.Length >= 2)
            {
                var subdomain = parts[0].ToLowerInvariant();

                var tenant = await dbContext.Blogs
                    .AsNoTracking()
                    .FirstOrDefaultAsync(t => t.Subdomain == subdomain);

                if (tenant != null)
                {
                    tenantContext.Resolve(tenant);
                    dbContext.CurrentTenantId = tenant.Id;
                }
                else
                {
                    // Tenant doesn't exist, we can output a neat 404 or redirect.
                    context.Response.StatusCode = 404;
                    await context.Response.WriteAsync("Blog not found.");
                    return;
                }
            }

            await _next(context);
        }
    }

    public static class TenantResolutionMiddlewareExtensions
    {
        public static IApplicationBuilder UseTenantResolution(this IApplicationBuilder builder)
        {
            return builder.UseMiddleware<TenantResolutionMiddleware>();
        }
    }
}
