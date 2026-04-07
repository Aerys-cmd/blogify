using Blogify.Web.Models;
using Blogify.Web.Services;

namespace Blogify.Web.Endpoints;

public static class FeedEndpoints
{
    /// <summary>
    /// Maps the /sitemap.xml and /rss.xml feed endpoints for the Blog area.
    /// Both endpoints are tenant-scoped: requests on an unresolved tenant return 404.
    /// </summary>
    public static IEndpointRouteBuilder MapFeedEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/sitemap.xml", async (
            TenantContext tenantContext,
            FeedService feedService,
            HttpContext httpContext,
            CancellationToken ct) =>
        {
            if (!tenantContext.IsTenantResolved)
                return Results.NotFound();

            Guid tenantId = tenantContext.RequiredTenant.Id;
            string baseUrl = $"{httpContext.Request.Scheme}://{httpContext.Request.Host}{httpContext.Request.PathBase}";
            string xml = await feedService.GetSitemapAsync(tenantId, baseUrl, ct);
            return Results.Content(xml, "application/xml; charset=utf-8");
        });

        app.MapGet("/rss.xml", async (
            TenantContext tenantContext,
            FeedService feedService,
            HttpContext httpContext,
            CancellationToken ct) =>
        {
            if (!tenantContext.IsTenantResolved)
                return Results.NotFound();

            Tenant tenant = tenantContext.RequiredTenant;
            string baseUrl = $"{httpContext.Request.Scheme}://{httpContext.Request.Host}{httpContext.Request.PathBase}";
            string xml = await feedService.GetRssAsync(tenant.Id, tenant.Title, baseUrl, ct);
            return Results.Content(xml, "application/rss+xml; charset=utf-8");
        });

        return app;
    }
}

