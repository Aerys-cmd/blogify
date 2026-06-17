using Microsoft.AspNetCore.OutputCaching;
using Microsoft.Net.Http.Headers;

namespace Blogify.Web.Services;

public sealed class PublicBlogOutputCachePolicy : IOutputCachePolicy
{
    public const string PolicyName = "PublicBlog";
    public const string GlobalTag = "public-blog";
    public static readonly TimeSpan Duration = TimeSpan.FromMinutes(10);

    public ValueTask CacheRequestAsync(OutputCacheContext context, CancellationToken cancellationToken)
    {
        HttpContext httpContext = context.HttpContext;

        if (!IsPublicBlogCacheCandidate(httpContext))
        {
            Disable(context);
            return ValueTask.CompletedTask;
        }

        Guid tenantId = httpContext.RequestServices
            .GetRequiredService<TenantContext>()
            .RequiredTenant
            .Id;

        bool isAuthenticated = httpContext.User.Identity?.IsAuthenticated ?? false;

        context.EnableOutputCaching = true;
        context.AllowCacheLookup = !isAuthenticated;
        context.AllowCacheStorage = !isAuthenticated;
        context.AllowLocking = !isAuthenticated;
        context.ResponseExpirationTimeSpan = Duration;
        context.CacheVaryByRules.VaryByHost = true;
        context.CacheVaryByRules.QueryKeys = "*";
        context.CacheVaryByRules.CacheKeyPrefix = TenantKeyPrefix(tenantId);
        context.Tags.Add(GlobalTag);
        context.Tags.Add(TenantTag(tenantId));
        httpContext.Response.OnStarting(static state =>
        {
            HttpResponse response = (HttpResponse)state;

            if (response.StatusCode == StatusCodes.Status200OK &&
                response.Headers.SetCookie.Count == 0 &&
                !response.Headers.ContainsKey(HeaderNames.CacheControl))
            {
                response.Headers.CacheControl = PublicCacheControl;
            }

            return Task.CompletedTask;
        }, httpContext.Response);

        return ValueTask.CompletedTask;
    }

    public ValueTask ServeFromCacheAsync(OutputCacheContext context, CancellationToken cancellationToken) =>
        ValueTask.CompletedTask;

    public ValueTask ServeResponseAsync(OutputCacheContext context, CancellationToken cancellationToken)
    {
        HttpResponse response = context.HttpContext.Response;
        if (response.Headers.SetCookie.Count > 0 || response.StatusCode != StatusCodes.Status200OK)
        {
            context.AllowCacheStorage = false;
            return ValueTask.CompletedTask;
        }

        if (!response.HasStarted && !response.Headers.ContainsKey(HeaderNames.CacheControl))
        {
            response.Headers.CacheControl = PublicCacheControl;
        }

        return ValueTask.CompletedTask;
    }

    private const string PublicCacheControl = "public, max-age=0, s-maxage=600, stale-while-revalidate=60";

    public static string TenantTag(Guid tenantId) => $"{GlobalTag}:{tenantId}";

    public static string TenantKeyPrefix(Guid tenantId) => $"{GlobalTag}:{tenantId}";

    private static bool IsPublicBlogCacheCandidate(HttpContext httpContext)
    {
        string method = httpContext.Request.Method;
        if (!HttpMethods.IsGet(method) && !HttpMethods.IsHead(method))
        {
            return false;
        }

        if (!httpContext.RequestServices.GetRequiredService<TenantContext>().IsTenantResolved)
        {
            return false;
        }

        object? area = httpContext.Request.RouteValues["area"];
        return string.Equals(area?.ToString(), "Blog", StringComparison.OrdinalIgnoreCase);
    }

    private static void Disable(OutputCacheContext context)
    {
        context.EnableOutputCaching = false;
        context.AllowCacheLookup = false;
        context.AllowCacheStorage = false;
        context.AllowLocking = false;
    }
}
