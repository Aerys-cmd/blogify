namespace Blogify.Web.Services;

public interface IPublicBlogCacheInvalidator
{
    Task InvalidateTenantAsync(Guid tenantId, CancellationToken ct = default);

    Task InvalidateIfMediaIsPubliclyReferencedAsync(Guid tenantId, IEnumerable<Guid> mediaIds, CancellationToken ct = default);
}
