using System.ComponentModel.DataAnnotations;
using Blogify.Web.Data;
using Blogify.Web.Models;
using Blogify.Web.Models.Exceptions;
using Blogify.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;

namespace Blogify.Web.Areas.BlogAdmin.Pages.Settings;

[Authorize]
public sealed class IndexModel(ApplicationDbContext dbContext, TenantContext tenantContext, IStringLocalizer<SharedResource> localizer) : PageModel
{
    [BindProperty]
    public BlogSettingsInput Input { get; set; } = new();

    public string BlogTitle { get; private set; } = string.Empty;
    public string? LogoPreviewUrl { get; private set; }
    public string? FaviconPreviewUrl { get; private set; }
    public string? SocialPreviewImageUrl { get; private set; }

    public async Task<IActionResult> OnGetAsync(CancellationToken ct = default)
    {
        Tenant tenant = tenantContext.RequiredTenant;

        BlogTitle = tenant.Title;
        Input = new BlogSettingsInput
        {
            PublicLanguage = tenant.PublicLanguage,
            LogoMediaId = tenant.LogoMediaId,
            FaviconMediaId = tenant.FaviconMediaId,
            SocialPreviewImageMediaId = tenant.SocialPreviewImageMediaId,
            MetaTitle = tenant.MetaTitle,
            MetaDescription = tenant.MetaDescription
        };

        await LoadMediaPreviewsAsync(ct);
        return Page();
    }

    public async Task<IActionResult> OnPostAsync(CancellationToken ct = default)
    {
        if (!ModelState.IsValid)
        {
            BlogTitle = tenantContext.RequiredTenant.Title;
            await LoadMediaPreviewsAsync(ct);
            return Page();
        }

        Tenant? tenant = await dbContext.Blogs
            .FirstOrDefaultAsync(t => t.Id == tenantContext.RequiredTenant.Id, ct);

        if (tenant is null)
        {
            return NotFound();
        }

        if (!await ValidateMediaSelectionAsync(tenant.Id, ct))
        {
            BlogTitle = tenant.Title;
            await LoadMediaPreviewsAsync(ct);
            return Page();
        }

        try
        {
            tenant.ChangePublicLanguage(Input.PublicLanguage);
            tenant.UpdateBranding(Input.LogoMediaId, Input.FaviconMediaId, Input.SocialPreviewImageMediaId);
            tenant.UpdateSeoMetadata(Input.MetaTitle, Input.MetaDescription);
        }
        catch (DomainException ex)
        {
            ModelState.AddModelError(nameof(Input.PublicLanguage), ex.Message);
            BlogTitle = tenant.Title;
            await LoadMediaPreviewsAsync(ct);
            return Page();
        }
        catch (ArgumentException ex)
        {
            ModelState.AddModelError(string.Empty, ex.Message);
            BlogTitle = tenant.Title;
            await LoadMediaPreviewsAsync(ct);
            return Page();
        }

        await dbContext.SaveChangesAsync(ct);

        TempData["SuccessMessage"] = localizer["Message.SaveSuccess"].Value;
        return RedirectToPage(new { blogSlug = RouteData.Values["blogSlug"] });
    }

    private async Task LoadMediaPreviewsAsync(CancellationToken ct)
    {
        Guid?[] ids =
        [
            Input.LogoMediaId,
            Input.FaviconMediaId,
            Input.SocialPreviewImageMediaId
        ];

        List<Guid> selectedIds = ids
            .Where(id => id.HasValue)
            .Select(id => id!.Value)
            .Distinct()
            .ToList();

        if (selectedIds.Count == 0)
        {
            LogoPreviewUrl = null;
            FaviconPreviewUrl = null;
            SocialPreviewImageUrl = null;
            return;
        }

        Dictionary<Guid, string> urls = await dbContext.Media
            .AsNoTracking()
            .Where(m => selectedIds.Contains(m.Id))
            .ToDictionaryAsync(m => m.Id, m => m.ThumbnailUrl ?? m.Url, ct);

        LogoPreviewUrl = Input.LogoMediaId is Guid logoId && urls.TryGetValue(logoId, out string? logoUrl)
            ? logoUrl
            : null;
        FaviconPreviewUrl = Input.FaviconMediaId is Guid faviconId && urls.TryGetValue(faviconId, out string? faviconUrl)
            ? faviconUrl
            : null;
        SocialPreviewImageUrl = Input.SocialPreviewImageMediaId is Guid socialId && urls.TryGetValue(socialId, out string? socialUrl)
            ? socialUrl
            : null;
    }

    private async Task<bool> ValidateMediaSelectionAsync(Guid blogId, CancellationToken ct)
    {
        List<Guid> selectedIds = new Guid?[]
            {
                Input.LogoMediaId,
                Input.FaviconMediaId,
                Input.SocialPreviewImageMediaId
            }
            .Where(id => id.HasValue)
            .Select(id => id!.Value)
            .Distinct()
            .ToList();

        if (selectedIds.Count == 0)
        {
            return true;
        }

        int matchingMediaCount = await dbContext.Media
            .AsNoTracking()
            .CountAsync(m => selectedIds.Contains(m.Id) && m.BlogId == blogId && m.ContentType.StartsWith("image/"), ct);

        if (matchingMediaCount == selectedIds.Count)
        {
            return true;
        }

        ModelState.AddModelError(string.Empty, localizer["Settings.Branding.InvalidMedia"].Value);
        return false;
    }
}

public sealed class BlogSettingsInput
{
    [Required]
    public string PublicLanguage { get; set; } = "tr";

    public Guid? LogoMediaId { get; set; }

    public Guid? FaviconMediaId { get; set; }

    public Guid? SocialPreviewImageMediaId { get; set; }

    [MaxLength(60)]
    public string? MetaTitle { get; set; }

    [MaxLength(160)]
    public string? MetaDescription { get; set; }
}
