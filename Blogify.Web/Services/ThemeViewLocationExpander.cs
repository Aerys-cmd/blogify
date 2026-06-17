using Microsoft.AspNetCore.Mvc.Razor;
using Blogify.Web.Services.Themes;

namespace Blogify.Web.Services;

public sealed class ThemeViewLocationExpander : IViewLocationExpander
{
    public void PopulateValues(ViewLocationExpanderContext context)
    {
        if (!string.Equals(context.AreaName, "Blog", StringComparison.OrdinalIgnoreCase))
            return;

        TenantContext tenantContext = context.ActionContext.HttpContext.RequestServices
            .GetRequiredService<TenantContext>();
        IThemeRegistry themeRegistry = context.ActionContext.HttpContext.RequestServices
            .GetRequiredService<IThemeRegistry>();

        string? previewTheme = ResolvePreviewTheme(context, tenantContext, themeRegistry);
        BlogTheme theme = themeRegistry.ResolveTheme(previewTheme ?? tenantContext.CurrentTenant?.ActiveTheme);
        context.Values["theme"] = theme.RazorFolder;
    }

    public IEnumerable<string> ExpandViewLocations(
        ViewLocationExpanderContext context,
        IEnumerable<string> viewLocations)
    {
        if (!context.Values.TryGetValue("theme", out string? theme) || string.IsNullOrEmpty(theme))
            return viewLocations;

        IEnumerable<string> themeLocations =
        [
            $"/Areas/Blog/Themes/{theme}/Shared/{{0}}.cshtml",
            $"/Areas/Blog/Themes/{theme}/{{0}}.cshtml",
            $"/Areas/Blog/Themes/Shared/{{0}}.cshtml",
        ];

        return themeLocations.Concat(viewLocations);
    }

    private static string? ResolvePreviewTheme(
        ViewLocationExpanderContext context,
        TenantContext tenantContext,
        IThemeRegistry themeRegistry)
    {
        HttpContext httpContext = context.ActionContext.HttpContext;
        if (tenantContext.CurrentTenant is null)
            return null;

        ThemePreviewTokenService previewTokenService = httpContext.RequestServices
            .GetRequiredService<ThemePreviewTokenService>();

        if (httpContext.Request.Query.ContainsKey(ThemePreviewTokenService.ExitQueryKey))
        {
            httpContext.Response.Cookies.Delete(ThemePreviewTokenService.CookieName, PreviewCookieOptions(httpContext));
            return null;
        }

        string? previewTheme = httpContext.Request.Query[ThemePreviewTokenService.ThemeQueryKey].FirstOrDefault();
        string? queryToken = httpContext.Request.Query[ThemePreviewTokenService.TokenQueryKey].FirstOrDefault();

        if (themeRegistry.IsSelectableTheme(previewTheme) &&
            previewTokenService.IsValidToken(queryToken, tenantContext.CurrentTenant.Id, previewTheme!))
        {
            httpContext.Response.Cookies.Append(
                ThemePreviewTokenService.CookieName,
                queryToken!,
                PreviewCookieOptions(httpContext));
            httpContext.Items[ThemePreviewTokenService.ThemeQueryKey] = previewTheme!;
            return previewTheme;
        }

        string? cookieToken = httpContext.Request.Cookies[ThemePreviewTokenService.CookieName];
        if (previewTokenService.TryValidateToken(cookieToken, tenantContext.CurrentTenant.Id, out string? cookieTheme) &&
            themeRegistry.IsSelectableTheme(cookieTheme))
        {
            httpContext.Items[ThemePreviewTokenService.ThemeQueryKey] = cookieTheme!;
            return cookieTheme;
        }

        if (!string.IsNullOrWhiteSpace(cookieToken))
        {
            httpContext.Response.Cookies.Delete(ThemePreviewTokenService.CookieName, PreviewCookieOptions(httpContext));
        }

        return null;
    }

    private static CookieOptions PreviewCookieOptions(HttpContext httpContext) =>
        new()
        {
            HttpOnly = true,
            IsEssential = true,
            MaxAge = ThemePreviewTokenService.PreviewDuration,
            Path = "/",
            SameSite = SameSiteMode.Lax,
            Secure = httpContext.Request.IsHttps
        };
}
