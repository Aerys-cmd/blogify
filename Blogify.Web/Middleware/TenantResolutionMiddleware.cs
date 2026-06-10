using Blogify.Web.Data;
using Blogify.Web.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Blogify.Web.Middleware
{
    /// <summary>
    /// Resolves the current tenant from:
    /// 1. The <c>{blogSlug}</c> route parameter when the request comes from a platform host
    ///    (used for the blog admin area at /app/admin/{blogSlug}/...).
    /// 2. The subdomain when the request comes from a tenant subdomain
    ///    (used for the public Blog area).
    /// Sets <see cref="TenantContext"/> and <see cref="ApplicationDbContext.CurrentTenantId"/>
    /// so that downstream middleware, global query filters, and page models have the correct tenant context.
    /// </summary>
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
            string host = context.Request.Host.Host;
            dbContext.CurrentTenantId = null;
            tenantContext.Clear();

            bool isPlatformHost = _options.PlatformHosts.Any(
                h => host.Equals(h, StringComparison.OrdinalIgnoreCase));

            if (isPlatformHost)
            {
                // On the platform host, the blog admin area embeds the blog slug in the route:
                // /app/admin/{blogSlug}/... — resolve tenant from that route value.
                string? blogSlug = context.GetRouteValue("blogSlug")?.ToString();
                if (!string.IsNullOrWhiteSpace(blogSlug))
                {
                    string normalizedSlug = blogSlug.ToLowerInvariant();
                    Models.Tenant? tenant = await dbContext.Blogs
                        .AsNoTracking()
                        .FirstOrDefaultAsync(t => t.Subdomain == normalizedSlug);

                    if (tenant is not null)
                    {
                        tenantContext.Resolve(tenant);
                        dbContext.CurrentTenantId = tenant.Id;
                    }
                    else
                    {
                        context.Response.StatusCode = 404;
                        await context.Response.WriteAsync("Blog not found.");
                        return;
                    }
                }

                // No blogSlug in route — no tenant context (dashboard, landing, SA, etc.).
                await _next(context);
                return;
            }

            // Non-platform host: extract subdomain for public Blog area.
            string[] parts = host.Split('.');
            if (parts.Length >= 2)
            {
                string subdomain = parts[0].ToLowerInvariant();

                Models.Tenant? tenant = await dbContext.Blogs
                    .AsNoTracking()
                    .FirstOrDefaultAsync(t => t.Subdomain == subdomain);

                if (tenant is not null)
                {
                    tenantContext.Resolve(tenant);
                    dbContext.CurrentTenantId = tenant.Id;
                }
                else
                {
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
