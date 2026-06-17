using Blogify.Web.Data;
using Blogify.Web.Models;
using Blogify.Web.Services;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Blogify.Tests;

public sealed class TenantBrandingSeoTests
{
    [Fact]
    public void UpdateSeoMetadata_TrimsAndClearsBlogMetadata()
    {
        Tenant tenant = Tenant.Create("Blog", "blog", "owner");

        tenant.UpdateSeoMetadata("  Custom title  ", "  Custom description  ");

        Assert.Equal("Custom title", tenant.MetaTitle);
        Assert.Equal("Custom description", tenant.MetaDescription);

        tenant.UpdateSeoMetadata(" ", "");

        Assert.Null(tenant.MetaTitle);
        Assert.Null(tenant.MetaDescription);
    }

    [Fact]
    public void UpdateSeoMetadata_RejectsTooLongBlogMetadata()
    {
        Tenant tenant = Tenant.Create("Blog", "blog", "owner");

        Assert.Throws<ArgumentException>(() => tenant.UpdateSeoMetadata(new string('a', 61), null));
        Assert.Throws<ArgumentException>(() => tenant.UpdateSeoMetadata(null, new string('a', 161)));
    }

    [Fact]
    public void UpdateBranding_StoresAndClearsMediaReferences()
    {
        Tenant tenant = Tenant.Create("Blog", "blog", "owner");
        Guid logoId = Guid.NewGuid();
        Guid faviconId = Guid.NewGuid();
        Guid socialId = Guid.NewGuid();

        tenant.UpdateBranding(logoId, faviconId, socialId);

        Assert.Equal(logoId, tenant.LogoMediaId);
        Assert.Equal(faviconId, tenant.FaviconMediaId);
        Assert.Equal(socialId, tenant.SocialPreviewImageMediaId);

        tenant.UpdateBranding(null, null, null);

        Assert.Null(tenant.LogoMediaId);
        Assert.Null(tenant.FaviconMediaId);
        Assert.Null(tenant.SocialPreviewImageMediaId);
    }

    [Fact]
    public async Task BlogBrandingService_LoadsSelectedMediaUrlsForCurrentTenant()
    {
        await using SqliteConnection connection = new("Data Source=:memory:");
        await connection.OpenAsync();

        DbContextOptions<ApplicationDbContext> options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlite(connection)
            .Options;

        await using ApplicationDbContext dbContext = new(options);
        await dbContext.Database.EnsureCreatedAsync();

        Tenant tenant = Tenant.Create("Blog", "blog", "owner");
        Media logo = Media.Upload(tenant.Id, "logo.webp", "/uploads/logo.webp", "image/webp", 100);
        Media favicon = Media.Upload(tenant.Id, "favicon.webp", "/uploads/favicon.webp", "image/webp", 100);
        Media social = Media.Upload(tenant.Id, "social.webp", "/uploads/social.webp", "image/webp", 100);
        tenant.UpdateBranding(logo.Id, favicon.Id, social.Id);

        dbContext.Blogs.Add(tenant);
        dbContext.Media.AddRange(logo, favicon, social);
        await dbContext.SaveChangesAsync();
        dbContext.CurrentTenantId = tenant.Id;

        TenantContext tenantContext = new();
        tenantContext.Resolve(tenant);
        BlogBrandingService service = new(dbContext, tenantContext);

        BlogBrandingAssets assets = await service.GetAssetsAsync();

        Assert.Equal("/uploads/logo.webp", assets.LogoUrl);
        Assert.Equal("/uploads/favicon.webp", assets.FaviconUrl);
        Assert.Equal("/uploads/social.webp", assets.SocialPreviewImageUrl);
    }
}
