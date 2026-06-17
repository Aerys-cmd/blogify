using Blogify.Web.Data;
using Microsoft.AspNetCore.OutputCaching;
using Microsoft.EntityFrameworkCore;

namespace Blogify.Web.Services;

public sealed class PublicBlogCacheInvalidator(
    IOutputCacheStore outputCacheStore,
    FeedService feedService,
    ApplicationDbContext dbContext) : IPublicBlogCacheInvalidator
{
    public async Task InvalidateTenantAsync(Guid tenantId, CancellationToken ct = default)
    {
        feedService.InvalidateTenant(tenantId);
        await outputCacheStore.EvictByTagAsync(PublicBlogOutputCachePolicy.TenantTag(tenantId), ct);
    }

    public async Task InvalidateIfMediaIsPubliclyReferencedAsync(
        Guid tenantId,
        IEnumerable<Guid> mediaIds,
        CancellationToken ct = default)
    {
        List<Guid> ids = mediaIds
            .Where(id => id != Guid.Empty)
            .Distinct()
            .ToList();

        if (ids.Count == 0)
        {
            return;
        }

        bool isReferencedByPublishedCover = await dbContext.Posts
            .AsNoTracking()
            .AnyAsync(post =>
                post.BlogId == tenantId &&
                post.PublishedRevisionId != null &&
                post.CoverImageId.HasValue &&
                ids.Contains(post.CoverImageId.Value), ct);

        if (isReferencedByPublishedCover)
        {
            await InvalidateTenantAsync(tenantId, ct);
            return;
        }

        bool isReferencedByBranding = await dbContext.Blogs
            .AsNoTracking()
            .AnyAsync(blog =>
                blog.Id == tenantId &&
                ((blog.LogoMediaId.HasValue && ids.Contains(blog.LogoMediaId.Value)) ||
                 (blog.FaviconMediaId.HasValue && ids.Contains(blog.FaviconMediaId.Value)) ||
                 (blog.SocialPreviewImageMediaId.HasValue && ids.Contains(blog.SocialPreviewImageMediaId.Value))), ct);

        if (isReferencedByBranding)
        {
            await InvalidateTenantAsync(tenantId, ct);
        }
    }
}
