using Blogify.Web.Data;
using Blogify.Web.Models;
using Blogify.Web.Models.Exceptions;
using Blogify.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;

namespace Blogify.Web.Areas.BlogAdmin.Pages.Themes;

[Authorize]
public sealed class IndexModel(
    ApplicationDbContext dbContext,
    TenantContext tenantContext,
    IPublicBlogCacheInvalidator publicBlogCacheInvalidator,
    IStringLocalizer<SharedResource> localizer) : PageModel
{
    public string CurrentTheme { get; private set; } = string.Empty;
    public IReadOnlyList<ThemeOptionViewModel> AvailableThemes { get; private set; } = [];

    [BindProperty]
    public string SelectedTheme { get; set; } = string.Empty;

    public IActionResult OnGet()
    {
        CurrentTheme = tenantContext.RequiredTenant.ActiveTheme;
        AvailableThemes = BuildAvailableThemes();
        SelectedTheme = CurrentTheme;
        return Page();
    }

    public async Task<IActionResult> OnPostAsync(CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(SelectedTheme))
        {
            ModelState.AddModelError(nameof(SelectedTheme), "A theme must be selected.");
            CurrentTheme = tenantContext.RequiredTenant.ActiveTheme;
            AvailableThemes = BuildAvailableThemes();
            return Page();
        }

        Tenant? tenant = await dbContext.Blogs
            .FirstOrDefaultAsync(t => t.Id == tenantContext.RequiredTenant.Id, ct);

        if (tenant is null)
            return NotFound();

        try
        {
            tenant.ChangeTheme(SelectedTheme);
        }
        catch (DomainException ex)
        {
            ModelState.AddModelError(nameof(SelectedTheme), ex.Message);
            CurrentTheme = tenantContext.RequiredTenant.ActiveTheme;
            AvailableThemes = BuildAvailableThemes();
            return Page();
        }

        await dbContext.SaveChangesAsync(ct);
        await publicBlogCacheInvalidator.InvalidateTenantAsync(tenant.Id, ct);
        return RedirectToPage(new { blogSlug = RouteData.Values["blogSlug"] });
    }

    private IReadOnlyList<ThemeOptionViewModel> BuildAvailableThemes() =>
    [
        new ThemeOptionViewModel(
            "default",
            localizer["Themes.Default.Name"],
            localizer["Themes.Default.Description"],
            "/images/theme-previews/default.png"),
        new ThemeOptionViewModel(
            "minimal",
            localizer["Themes.Minimal.Name"],
            localizer["Themes.Minimal.Description"],
            "/images/theme-previews/minimal.png"),
        new ThemeOptionViewModel(
            "aurora",
            localizer["Themes.Aurora.Name"],
            localizer["Themes.Aurora.Description"],
            "/images/theme-previews/aurora.png"),
    ];
}

public sealed record ThemeOptionViewModel(
    string Slug,
    string DisplayName,
    string Description,
    string PreviewImageUrl);
