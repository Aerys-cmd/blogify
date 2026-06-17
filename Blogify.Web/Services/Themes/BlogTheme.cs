namespace Blogify.Web.Services.Themes;

public sealed record BlogTheme(
    string Slug,
    string DisplayNameResourceKey,
    string DescriptionResourceKey,
    string PreviewImagePath,
    string RazorFolder,
    string CssAssetPath,
    bool IsEnabled,
    bool IsSelectable,
    bool IsFallback);
