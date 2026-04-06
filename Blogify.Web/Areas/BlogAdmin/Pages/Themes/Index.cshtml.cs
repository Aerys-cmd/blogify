using Blogify.Web.Data;
using Blogify.Web.Models;
using Blogify.Web.Models.Exceptions;
using Blogify.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace Blogify.Web.Areas.BlogAdmin.Pages.Themes;

[Authorize(Roles = "BlogAdmin")]
public sealed class IndexModel(ApplicationDbContext dbContext, TenantContext tenantContext) : PageModel
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
        return RedirectToPage();
    }

    private static IReadOnlyList<ThemeOptionViewModel> BuildAvailableThemes() =>
    [
        new ThemeOptionViewModel(
            "default",
            "Default",
            "Classic Bootstrap 5 layout with a responsive card grid and dark navbar.",
            "/images/theme-previews/default.png"),
        new ThemeOptionViewModel(
            "minimal",
            "Minimal",
            "Typography-first, single-column layout with generous whitespace and a neutral palette.",
            "/images/theme-previews/minimal.png"),
        new ThemeOptionViewModel(
            "aurora",
            "Aurora",
            "Bold magazine style with a dark indigo header, card grid, and vibrant accent colours.",
            "/images/theme-previews/aurora.png"),
    ];
}

public sealed record ThemeOptionViewModel(
    string Slug,
    string DisplayName,
    string Description,
    string PreviewImageUrl);
