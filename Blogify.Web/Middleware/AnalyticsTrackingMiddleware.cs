using System.Net;
using System.Security.Cryptography;
using System.Text;
using Blogify.Web.Services;
using Microsoft.Extensions.Options;

namespace Blogify.Web.Middleware
{
    public sealed class AnalyticsTrackingMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<AnalyticsTrackingMiddleware> _logger;
        private readonly AnalyticsChannel _channel;
        private readonly byte[] _ipHashKey;

        public AnalyticsTrackingMiddleware(
            RequestDelegate next,
            ILogger<AnalyticsTrackingMiddleware> logger,
            AnalyticsChannel analyticsChannel,
            IOptions<AnalyticsOptions> analyticsOptions)
        {
            _next = next;
            _logger = logger;
            _channel = analyticsChannel;

            string salt = analyticsOptions.Value.IpHashSalt;
            _ipHashKey = string.IsNullOrEmpty(salt)
                ? RandomNumberGenerator.GetBytes(32)
                : Encoding.UTF8.GetBytes(salt);
        }

        public async Task InvokeAsync(HttpContext context, TenantContext tenantContext)
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

            // Capture all request-scoped data synchronously before the request ends
            Guid tenantId = tenantContext.RequiredTenant.Id;
            string? pageName = context.GetRouteData().Values["page"]?.ToString();
            string? slug = context.GetRouteData().Values["slug"]?.ToString();
            string? referrer = NormalizeReferrer(context.Request.Headers.Referer.ToString());
            string? utmSource = NormalizeUtmSource(context.Request.Query["utm_source"].ToString());
            string? ipHash = context.Connection.RemoteIpAddress is IPAddress ip
                ? HashIp(ip, _ipHashKey)
                : null;

            AnalyticsEventData data = new(
                tenantId,
                pageName,
                slug,
                referrer,
                utmSource,
                ipHash,
                DateTimeOffset.UtcNow);

            if (!_channel.TryEnqueue(data))
            {
                _logger.LogDebug(
                    "Analytics channel is full; dropping page view event for tenant {TenantId}.",
                    tenantId);
            }
        }

        private static string? NormalizeReferrer(string? referrer)
        {
            if (string.IsNullOrEmpty(referrer))
            {
                return null;
            }

            // Strip query string and fragment to reduce cardinality and avoid storing
            // potentially sensitive query parameters from inbound referrer headers.
            if (Uri.TryCreate(referrer, UriKind.Absolute, out Uri? uri))
            {
                string normalized = uri.GetLeftPart(UriPartial.Path);
                return normalized.Length > 2048 ? normalized[..2048] : normalized;
            }

            return referrer.Length > 2048 ? referrer[..2048] : referrer;
        }

        private static string? NormalizeUtmSource(string? utmSource)
        {
            if (string.IsNullOrEmpty(utmSource))
            {
                return null;
            }

            return utmSource.Length > 255 ? utmSource[..255] : utmSource;
        }

        private static string HashIp(IPAddress ipAddress, byte[] key)
        {
            // Use address bytes directly (stable for IPv4/IPv6, avoids string encoding overhead)
            byte[] addressBytes = ipAddress.GetAddressBytes();
            byte[] hash = HMACSHA256.HashData(key, addressBytes);
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
