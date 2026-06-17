using Blogify.Web.Data;
using Blogify.Web.Models;
using Blogify.Web.Models.Posts;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Blogify.Tests;

public sealed class ContentOrganizationTests
{
    [Fact]
    public void SetTags_ReplacesRemovedTagsAndKeepsExistingAssignments()
    {
        Post post = Post.Create(Guid.NewGuid(), "author", "hello-world", "Hello world", "{}");
        Guid first = Guid.NewGuid();
        Guid second = Guid.NewGuid();
        Guid third = Guid.NewGuid();

        post.SetTags([first, second]);
        post.SetTags([second, third]);

        Assert.Equal(
            new[] { second, third }.Order().ToArray(),
            post.Tags.Select(t => t.TagId).Order().ToArray());
    }

    [Fact]
    public async Task Tags_AreTenantScopedAndHideSoftDeletedRows()
    {
        await using SqliteConnection connection = new("Data Source=:memory:");
        await connection.OpenAsync();

        DbContextOptions<ApplicationDbContext> options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlite(connection)
            .Options;

        Tenant firstTenant = Tenant.Create("First", "first", "owner");
        Tenant secondTenant = Tenant.Create("Second", "second", "owner");
        Tag visible = Tag.Create(firstTenant.Id, "Visible", "visible");
        Tag deleted = Tag.Create(firstTenant.Id, "Deleted", "deleted");
        Tag otherTenant = Tag.Create(secondTenant.Id, "Other", "other");
        deleted.SoftDelete();

        await using (ApplicationDbContext setup = new(options))
        {
            await setup.Database.EnsureCreatedAsync();
            setup.Blogs.AddRange(firstTenant, secondTenant);
            setup.Tags.AddRange(visible, deleted, otherTenant);
            await setup.SaveChangesAsync();
        }

        await using ApplicationDbContext dbContext = new(options);
        dbContext.CurrentTenantId = firstTenant.Id;

        List<string> slugs = await dbContext.Tags
            .OrderBy(t => t.Slug)
            .Select(t => t.Slug)
            .ToListAsync();

        Assert.Equal(["visible"], slugs);
    }
}
