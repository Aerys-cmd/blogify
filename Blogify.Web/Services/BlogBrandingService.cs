using Blogify.Web.Data;
using Blogify.Web.Models;
using Microsoft.EntityFrameworkCore;

namespace Blogify.Web.Services;

public sealed class BlogBrandingService(ApplicationDbContext dbContext, TenantContext tenantContext)
{
    private Task<BlogBrandingAssets>? _assetsTask;

    public Task<BlogBrandingAssets> GetAssetsAsync(CancellationToken ct = default)
    {
        _assetsTask ??= LoadAssetsAsync(ct);
        return _assetsTask;
    }

    private async Task<BlogBrandingAssets> LoadAssetsAsync(CancellationToken ct)
    {
        Tenant tenant = tenantContext.RequiredTenant;
        List<Guid> mediaIds = new Guid?[]
            {
                tenant.LogoMediaId,
                tenant.FaviconMediaId,
                tenant.SocialPreviewImageMediaId
            }
            .Where(id => id.HasValue)
            .Select(id => id!.Value)
            .Distinct()
            .ToList();

        if (mediaIds.Count == 0)
        {
            return new BlogBrandingAssets(null, null, null);
        }

        Dictionary<Guid, string> urls = await dbContext.Media
            .AsNoTracking()
            .Where(m => mediaIds.Contains(m.Id))
            .ToDictionaryAsync(m => m.Id, m => m.Url, ct);

        return new BlogBrandingAssets(
            tenant.LogoMediaId is Guid logoId && urls.TryGetValue(logoId, out string? logoUrl) ? logoUrl : null,
            tenant.FaviconMediaId is Guid faviconId && urls.TryGetValue(faviconId, out string? faviconUrl) ? faviconUrl : null,
            tenant.SocialPreviewImageMediaId is Guid socialId && urls.TryGetValue(socialId, out string? socialUrl) ? socialUrl : null);
    }
}

public sealed record BlogBrandingAssets(
    string? LogoUrl,
    string? FaviconUrl,
    string? SocialPreviewImageUrl);
