using Microsoft.AspNetCore.Mvc.Razor;

namespace Blogify.Web.Services;

public sealed class ThemeViewLocationExpander : IViewLocationExpander
{
    public void PopulateValues(ViewLocationExpanderContext context)
    {
        if (!string.Equals(context.AreaName, "Blog", StringComparison.OrdinalIgnoreCase))
            return;

        TenantContext tenantContext = context.ActionContext.HttpContext.RequestServices
            .GetRequiredService<TenantContext>();

        context.Values["theme"] = tenantContext.CurrentTenant?.ActiveTheme ?? "default";
    }

    public IEnumerable<string> ExpandViewLocations(
        ViewLocationExpanderContext context,
        IEnumerable<string> viewLocations)
    {
        if (!context.Values.TryGetValue("theme", out string? theme) || string.IsNullOrEmpty(theme))
            return viewLocations;

        string themeFolder = char.ToUpperInvariant(theme[0]) + theme[1..];

        IEnumerable<string> themeLocations =
        [
            $"/Areas/Blog/Themes/{themeFolder}/Shared/{{0}}.cshtml",
            $"/Areas/Blog/Themes/{themeFolder}/{{0}}.cshtml",
        ];

        return themeLocations.Concat(viewLocations);
    }
}
