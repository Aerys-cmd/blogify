namespace Blogify.Web.Services.Themes;

public sealed class ThemeRegistry : IThemeRegistry
{
    private readonly Dictionary<string, BlogTheme> _themes;

    public ThemeRegistry()
        : this(
        [
            new BlogTheme(
                "default",
                "Themes.Default.Name",
                "Themes.Default.Description",
                "/images/theme-previews/default.png",
                "Default",
                "/css/themes/default.css",
                IsEnabled: true,
                IsSelectable: true,
                IsFallback: true),
            new BlogTheme(
                "minimal",
                "Themes.Minimal.Name",
                "Themes.Minimal.Description",
                "/images/theme-previews/minimal.png",
                "Minimal",
                "/css/themes/minimal.css",
                IsEnabled: true,
                IsSelectable: true,
                IsFallback: false),
            new BlogTheme(
                "aurora",
                "Themes.Aurora.Name",
                "Themes.Aurora.Description",
                "/images/theme-previews/aurora.png",
                "Aurora",
                "/css/themes/aurora.css",
                IsEnabled: true,
                IsSelectable: true,
                IsFallback: false)
        ])
    {
    }

    public ThemeRegistry(IEnumerable<BlogTheme> themes)
    {
        ArgumentNullException.ThrowIfNull(themes);

        AllThemes = themes.ToArray();
        if (AllThemes.Count == 0)
        {
            throw new InvalidOperationException("At least one blog theme must be registered.");
        }

        BlogTheme[] fallbackThemes = AllThemes.Where(t => t.IsFallback).ToArray();
        if (fallbackThemes.Length != 1)
        {
            throw new InvalidOperationException("Exactly one blog theme must be marked as fallback.");
        }

        FallbackTheme = fallbackThemes[0];
        if (!FallbackTheme.IsEnabled)
        {
            throw new InvalidOperationException("The fallback blog theme must be enabled.");
        }

        _themes = new Dictionary<string, BlogTheme>(StringComparer.OrdinalIgnoreCase);
        foreach (BlogTheme theme in AllThemes)
        {
            ValidateTheme(theme);
            if (!_themes.TryAdd(theme.Slug, theme))
            {
                throw new InvalidOperationException($"Duplicate blog theme slug '{theme.Slug}'.");
            }
        }

        SelectableThemes = AllThemes
            .Where(t => t.IsEnabled && t.IsSelectable)
            .ToArray();

        if (!SelectableThemes.Contains(FallbackTheme))
        {
            throw new InvalidOperationException("The fallback blog theme must be selectable.");
        }
    }

    public BlogTheme FallbackTheme { get; }
    public IReadOnlyList<BlogTheme> AllThemes { get; }
    public IReadOnlyList<BlogTheme> SelectableThemes { get; }

    public bool TryGetTheme(string? slug, out BlogTheme theme)
    {
        if (!string.IsNullOrWhiteSpace(slug) &&
            _themes.TryGetValue(slug.Trim(), out BlogTheme? registered))
        {
            theme = registered;
            return true;
        }

        theme = FallbackTheme;
        return false;
    }

    public bool IsSelectableTheme(string? slug) =>
        TryGetTheme(slug, out BlogTheme theme) && theme.IsEnabled && theme.IsSelectable;

    public BlogTheme ResolveTheme(string? slug)
    {
        return TryGetTheme(slug, out BlogTheme theme) && theme.IsEnabled
            ? theme
            : FallbackTheme;
    }

    private static void ValidateTheme(BlogTheme theme)
    {
        if (string.IsNullOrWhiteSpace(theme.Slug))
            throw new InvalidOperationException("Blog theme slug is required.");

        if (string.IsNullOrWhiteSpace(theme.DisplayNameResourceKey))
            throw new InvalidOperationException($"Blog theme '{theme.Slug}' is missing a display name resource key.");

        if (string.IsNullOrWhiteSpace(theme.DescriptionResourceKey))
            throw new InvalidOperationException($"Blog theme '{theme.Slug}' is missing a description resource key.");

        if (string.IsNullOrWhiteSpace(theme.PreviewImagePath))
            throw new InvalidOperationException($"Blog theme '{theme.Slug}' is missing a preview image path.");

        if (string.IsNullOrWhiteSpace(theme.RazorFolder))
            throw new InvalidOperationException($"Blog theme '{theme.Slug}' is missing a Razor folder.");

        if (string.IsNullOrWhiteSpace(theme.CssAssetPath))
            throw new InvalidOperationException($"Blog theme '{theme.Slug}' is missing a CSS asset path.");
    }
}
