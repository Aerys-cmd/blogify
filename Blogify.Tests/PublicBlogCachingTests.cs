using Blogify.Web.Areas.Blog.Pages;
using Blogify.Web.Data;
using Blogify.Web.Models;
using Blogify.Web.Models.Posts;
using Blogify.Web.Services;
using Microsoft.AspNetCore.OutputCaching;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace Blogify.Tests;

public sealed class PublicBlogCachingTests
{
    [Fact]
    public async Task InvalidateTenant_EvictsTenantOutputCacheTag()
    {
        await using SqliteConnection connection = new("Data Source=:memory:");
        await connection.OpenAsync();

        DbContextOptions<ApplicationDbContext> options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlite(connection)
            .Options;

        await using ApplicationDbContext dbContext = new(options);
        await dbContext.Database.EnsureCreatedAsync();

        FakeOutputCacheStore outputCacheStore = new();
        using MemoryCache memoryCache = new(new MemoryCacheOptions());
        FeedService feedService = new(dbContext, memoryCache);
        PublicBlogCacheInvalidator invalidator = new(outputCacheStore, feedService, dbContext);
        Guid tenantId = Guid.NewGuid();

        await invalidator.InvalidateTenantAsync(tenantId);

        Assert.Equal([PublicBlogOutputCachePolicy.TenantTag(tenantId)], outputCacheStore.EvictedTags);
    }

    [Fact]
    public async Task InvalidateIfMediaIsPubliclyReferenced_EvictsOnlyForPublishedCoverOrBranding()
    {
        await using SqliteConnection connection = new("Data Source=:memory:");
        await connection.OpenAsync();

        DbContextOptions<ApplicationDbContext> options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlite(connection)
            .Options;

        await using ApplicationDbContext dbContext = new(options);
        await dbContext.Database.EnsureCreatedAsync();

        Tenant tenant = Tenant.Create("Blog", "blog", "owner");
        Media publishedCover = Media.Upload(tenant.Id, "cover.webp", "/cover.webp", "image/webp", 100);
        Media draftCover = Media.Upload(tenant.Id, "draft.webp", "/draft.webp", "image/webp", 100);
        Media logo = Media.Upload(tenant.Id, "logo.webp", "/logo.webp", "image/webp", 100);
        Media unused = Media.Upload(tenant.Id, "unused.webp", "/unused.webp", "image/webp", 100);

        Post publishedPost = Post.Create(tenant.Id, "author", "published", "Published", "{}");
        publishedPost.SetCoverImage(publishedCover.Id);
        publishedPost.Publish(publishedPost.Revisions[0].Id);

        Post draftPost = Post.Create(tenant.Id, "author", "draft", "Draft", "{}");
        draftPost.SetCoverImage(draftCover.Id);

        tenant.UpdateBranding(logo.Id, null, null);

        dbContext.Blogs.Add(tenant);
        dbContext.Media.AddRange(publishedCover, draftCover, logo, unused);
        dbContext.Posts.AddRange(publishedPost, draftPost);
        await dbContext.SaveChangesAsync();
        dbContext.CurrentTenantId = tenant.Id;

        FakeOutputCacheStore outputCacheStore = new();
        using MemoryCache memoryCache = new(new MemoryCacheOptions());
        PublicBlogCacheInvalidator invalidator = new(outputCacheStore, new FeedService(dbContext, memoryCache), dbContext);

        await invalidator.InvalidateIfMediaIsPubliclyReferencedAsync(tenant.Id, [unused.Id, draftCover.Id]);
        Assert.Empty(outputCacheStore.EvictedTags);

        await invalidator.InvalidateIfMediaIsPubliclyReferencedAsync(tenant.Id, [publishedCover.Id]);
        await invalidator.InvalidateIfMediaIsPubliclyReferencedAsync(tenant.Id, [logo.Id]);

        Assert.Equal(
            [PublicBlogOutputCachePolicy.TenantTag(tenant.Id), PublicBlogOutputCachePolicy.TenantTag(tenant.Id)],
            outputCacheStore.EvictedTags);
    }

    [Fact]
    public async Task PublicPostLists_PreferThumbnailUrlAndFallbackToOriginal()
    {
        await using SqliteConnection connection = new("Data Source=:memory:");
        await connection.OpenAsync();

        DbContextOptions<ApplicationDbContext> options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlite(connection)
            .Options;

        await using ApplicationDbContext dbContext = new(options);
        await dbContext.Database.EnsureCreatedAsync();

        Tenant tenant = Tenant.Create("Blog", "blog", "owner");
        Media withThumbnail = Media.Upload(tenant.Id, "thumb.webp", "/images/original.webp", "image/webp", 100);
        withThumbnail.SetThumbnail("/images/thumb.webp", 220, 120);
        Media withoutThumbnail = Media.Upload(tenant.Id, "plain.webp", "/images/plain.webp", "image/webp", 100);

        Post first = Post.Create(tenant.Id, "author", "first", "First", "{}");
        first.SetCoverImage(withThumbnail.Id);
        first.Publish(first.Revisions[0].Id);

        Post second = Post.Create(tenant.Id, "author", "second", "Second", "{}");
        second.SetCoverImage(withoutThumbnail.Id);
        second.Publish(second.Revisions[0].Id);

        dbContext.Blogs.Add(tenant);
        dbContext.Media.AddRange(withThumbnail, withoutThumbnail);
        dbContext.Posts.AddRange(first, second);
        await dbContext.SaveChangesAsync();
        dbContext.CurrentTenantId = tenant.Id;

        PagedPostListViewModel result = await BlogPostListLoader.LoadPostsAsync(
            dbContext,
            dbContext.Posts.AsNoTracking().Where(p => p.PublishedRevisionId != null),
            page: 1,
            CancellationToken.None);

        Assert.Contains(result.Posts, post => post.Slug == "first" && post.CoverImageUrl == "/images/thumb.webp");
        Assert.Contains(result.Posts, post => post.Slug == "second" && post.CoverImageUrl == "/images/plain.webp");
    }

    private sealed class FakeOutputCacheStore : IOutputCacheStore
    {
        public List<string> EvictedTags { get; } = [];

        public ValueTask EvictByTagAsync(string tag, CancellationToken cancellationToken)
        {
            EvictedTags.Add(tag);
            return ValueTask.CompletedTask;
        }

        public ValueTask<byte[]?> GetAsync(string key, CancellationToken cancellationToken) =>
            ValueTask.FromResult<byte[]?>(null);

        public ValueTask SetAsync(
            string key,
            byte[] value,
            string[]? tags,
            TimeSpan validFor,
            CancellationToken cancellationToken) =>
            ValueTask.CompletedTask;
    }
}
