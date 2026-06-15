using System.Globalization;
using Blogify.Web.Services;
using Microsoft.AspNetCore.Localization;

namespace Blogify.Web.Middleware;

public sealed class PublicBlogCultureMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context, TenantContext tenantContext)
    {
        string? area = context.GetRouteValue("area")?.ToString();
        if (tenantContext.IsTenantResolved &&
            string.Equals(area, "Blog", StringComparison.OrdinalIgnoreCase))
        {
            CultureInfo culture = CultureInfo.GetCultureInfo(tenantContext.RequiredTenant.PublicLanguage);
            CultureInfo.CurrentCulture = culture;
            CultureInfo.CurrentUICulture = culture;
            context.Features.Set<IRequestCultureFeature>(
                new RequestCultureFeature(new RequestCulture(culture), provider: null));
        }

        await next(context);
    }
}

public static class PublicBlogCultureMiddlewareExtensions
{
    public static IApplicationBuilder UsePublicBlogCulture(this IApplicationBuilder builder) =>
        builder.UseMiddleware<PublicBlogCultureMiddleware>();
}
