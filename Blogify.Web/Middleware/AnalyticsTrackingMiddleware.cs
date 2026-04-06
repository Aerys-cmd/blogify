using System.Security.Cryptography;
using System.Text;
using Blogify.Web.Data;
using Blogify.Web.Models;
using Blogify.Web.Services;
using Microsoft.EntityFrameworkCore;

namespace Blogify.Web.Middleware
{
    public sealed class AnalyticsTrackingMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<AnalyticsTrackingMiddleware> _logger;

        public AnalyticsTrackingMiddleware(
            RequestDelegate next,
            ILogger<AnalyticsTrackingMiddleware> logger)
        {
            _next = next;
            _logger = logger;
        }

        public async Task InvokeAsync(
            HttpContext context,
            TenantContext tenantContext,
            IServiceScopeFactory scopeFactory)
        {
            await _next(context);

            // Only track successful GET responses
            if (context.Request.Method != HttpMethods.Get || context.Response.StatusCode != 200)
            {
                return;
            }

            // Only track Blog area page views
            string? area = context.GetRouteData().Values["area"]?.ToString();
            if (!string.Equals(area, "Blog", StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            // Ignore bots by user-agent
            string userAgent = context.Request.Headers.UserAgent.ToString();
            if (!string.IsNullOrEmpty(userAgent) &&
                userAgent.Contains("bot", StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            if (!tenantContext.IsTenantResolved)
            {
                return;
            }

            // Capture all primitive data before leaving request scope
            Guid tenantId = tenantContext.RequiredTenant.Id;
            string? slug = context.GetRouteData().Values["slug"]?.ToString();
            string? referrer = context.Request.Headers.Referer.ToString();
            string? utmSource = context.Request.Query["utm_source"].ToString();
            string? ipAddress = context.Connection.RemoteIpAddress?.ToString();
            string? ipHash = ipAddress is null ? null : HashIp(ipAddress);

            // Fire-and-forget: create a new DI scope for the background DB operation
            _ = RecordEventAsync(
                scopeFactory,
                _logger,
                tenantId,
                slug,
                string.IsNullOrEmpty(referrer) ? null : referrer,
                string.IsNullOrEmpty(utmSource) ? null : utmSource,
                ipHash);
        }

        private static async Task RecordEventAsync(
            IServiceScopeFactory scopeFactory,
            ILogger logger,
            Guid tenantId,
            string? slug,
            string? referrer,
            string? utmSource,
            string? ipHash)
        {
            try
            {
                await using AsyncServiceScope scope = scopeFactory.CreateAsyncScope();
                ApplicationDbContext db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                db.CurrentTenantId = tenantId;

                Guid? postId = null;
                if (slug is not null)
                {
                    var post = await db.Posts
                        .AsNoTracking()
                        .Where(p => p.Slug == slug && p.PublishedRevisionId != null)
                        .Select(p => new { p.Id })
                        .FirstOrDefaultAsync(CancellationToken.None);

                    postId = post?.Id;
                }

                AnalyticsEvent analyticsEvent = AnalyticsEvent.Create(
                    tenantId,
                    postId,
                    AnalyticsEventType.PageView,
                    referrer,
                    utmSource,
                    ipHash);

                db.AnalyticsEvents.Add(analyticsEvent);
                await db.SaveChangesAsync(CancellationToken.None);
            }
            catch (Exception ex)
            {
                // Analytics tracking must never affect the user-facing response.
                logger.LogWarning(ex, "Failed to record analytics event for tenant {TenantId}.", tenantId);
            }
        }

        private static string HashIp(string ipAddress)
        {
            byte[] hash = SHA256.HashData(Encoding.UTF8.GetBytes(ipAddress));
            return Convert.ToHexString(hash).ToLowerInvariant();
        }
    }

    public static class AnalyticsTrackingMiddlewareExtensions
    {
        public static IApplicationBuilder UseAnalyticsTracking(this IApplicationBuilder builder)
        {
            return builder.UseMiddleware<AnalyticsTrackingMiddleware>();
        }
    }
}
