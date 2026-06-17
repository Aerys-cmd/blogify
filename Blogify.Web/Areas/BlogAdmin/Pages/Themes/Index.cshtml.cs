using Blogify.Web.Data;
using Blogify.Web.Models;
using Blogify.Web.Models.Exceptions;
using Blogify.Web.Services;
using Blogify.Web.Services.Themes;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;

namespace Blogify.Web.Areas.BlogAdmin.Pages.Themes;

[Authorize]
public sealed class IndexModel(
    ApplicationDbContext dbContext,
    TenantContext tenantContext,
    IPublicBlogCacheInvalidator publicBlogCacheInvalidator,
    IThemeRegistry themeRegistry,
    ThemePreviewTokenService previewTokenService,
    IOptions<TenantOptions> tenantOptions,
    IStringLocalizer<SharedResource> localizer) : PageModel
{
    public string CurrentTheme { get; private set; } = string.Empty;
    public string StoredTheme { get; private set; } = string.Empty;
    public bool IsStoredThemeUnavailable { get; private set; }
    public IReadOnlyList<ThemeOptionViewModel> AvailableThemes { get; private set; } = [];

    [BindProperty]
    public string SelectedTheme { get; set; } = string.Empty;

    public IActionResult OnGet()
    {
        LoadThemePicker();
        AvailableThemes = BuildAvailableThemes();
        SelectedTheme = CurrentTheme;
        return Page();
    }

    public async Task<IActionResult> OnPostAsync(CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(SelectedTheme))
        {
            ModelState.AddModelError(nameof(SelectedTheme), localizer["Themes.SelectionRequired"]);
            LoadThemePicker();
            AvailableThemes = BuildAvailableThemes();
            return Page();
        }

        Tenant? tenant = await dbContext.Blogs
            .FirstOrDefaultAsync(t => t.Id == tenantContext.RequiredTenant.Id, ct);

        if (tenant is null)
            return NotFound();

        try
        {
            tenant.ChangeTheme(SelectedTheme, themeRegistry);
        }
        catch (DomainException ex)
        {
            ModelState.AddModelError(nameof(SelectedTheme), ex.Message);
            LoadThemePicker();
            AvailableThemes = BuildAvailableThemes();
            return Page();
        }

        await dbContext.SaveChangesAsync(ct);
        await publicBlogCacheInvalidator.InvalidateTenantAsync(tenant.Id, ct);
        return RedirectToPage(new { blogSlug = RouteData.Values["blogSlug"] });
    }

    private void LoadThemePicker()
    {
        StoredTheme = tenantContext.RequiredTenant.ActiveTheme;
        BlogTheme resolvedTheme = themeRegistry.ResolveTheme(StoredTheme);
        CurrentTheme = resolvedTheme.Slug;
        IsStoredThemeUnavailable = !themeRegistry.IsSelectableTheme(StoredTheme);
    }

    private IReadOnlyList<ThemeOptionViewModel> BuildAvailableThemes() =>
        themeRegistry.SelectableThemes
            .Select(theme => new ThemeOptionViewModel(
                theme.Slug,
                localizer[theme.DisplayNameResourceKey],
                localizer[theme.DescriptionResourceKey],
                theme.PreviewImagePath,
                BuildPreviewUrl(theme.Slug)))
            .ToArray();

    private string BuildPreviewUrl(string themeSlug)
    {
        Tenant tenant = tenantContext.RequiredTenant;
        string token = previewTokenService.CreateToken(tenant.Id, themeSlug);
        string query =
            $"{ThemePreviewTokenService.ThemeQueryKey}={Uri.EscapeDataString(themeSlug)}&" +
            $"{ThemePreviewTokenService.TokenQueryKey}={Uri.EscapeDataString(token)}";

        string platformHost = tenantOptions.Value.PlatformHosts
            .FirstOrDefault(h => Request.Host.Host.Equals(h, StringComparison.OrdinalIgnoreCase)) ??
            Request.Host.Host;
        string host = $"{tenant.Subdomain}.{platformHost}";

        if (Request.Host.Port is int port)
        {
            host = $"{host}:{port}";
        }

        return $"{Request.Scheme}://{host}/?{query}";
    }
}

public sealed record ThemeOptionViewModel(
    string Slug,
    string DisplayName,
    string Description,
    string PreviewImageUrl,
    string PreviewUrl);
