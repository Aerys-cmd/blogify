namespace Blogify.Web.Services.Themes;

public interface IThemeRegistry
{
    BlogTheme FallbackTheme { get; }
    IReadOnlyList<BlogTheme> AllThemes { get; }
    IReadOnlyList<BlogTheme> SelectableThemes { get; }
    bool TryGetTheme(string? slug, out BlogTheme theme);
    bool IsSelectableTheme(string? slug);
    BlogTheme ResolveTheme(string? slug);
}
