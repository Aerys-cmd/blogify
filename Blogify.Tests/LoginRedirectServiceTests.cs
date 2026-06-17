using Blogify.Web.Data;
using Blogify.Web.Models;
using Blogify.Web.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Blogify.Tests;

public sealed class LoginRedirectServiceTests
{
    [Fact]
    public async Task GetsCreateBlogWhenUserHasNoBlogs()
    {
        await using TestContext context = await CreateContextAsync();
        ILoginRedirectService service = CreateService(context);

        string destination = await service.GetDestinationAsync("user-1", "https", new HostString("blogify.com"));

        Assert.Equal("~/dashboard/create-blog", destination);
    }

    [Fact]
    public async Task GetsAdminWorkspaceWhenUserHasOneBlog()
    {
        await using TestContext context = await CreateContextAsync();
        Tenant blog = Tenant.Create("One Blog", "one-blog", "owner-1");
        context.DbContext.Blogs.Add(blog);
        await context.DbContext.SaveChangesAsync();

        ILoginRedirectService service = CreateService(context);

        string destination = await service.GetDestinationAsync("owner-1", "https", new HostString("blogify.com"));

        Assert.Equal("~/app/admin/one-blog", destination);
    }

    [Fact]
    public async Task GetsDashboardWhenUserHasMultipleBlogs()
    {
        await using TestContext context = await CreateContextAsync();

        Tenant owned = Tenant.Create("Owned", "owned", "user-1");
        Tenant member = Tenant.Create("Member", "member", "other-owner");
        context.DbContext.Blogs.AddRange(owned, member);
        await context.DbContext.SaveChangesAsync();

        context.DbContext.BlogMemberships.Add(
            BlogMembership.Create(member.Id, "user-1", BlogRole.Writer, "inviter"));
        await context.DbContext.SaveChangesAsync();

        ILoginRedirectService service = CreateService(context);

        string destination = await service.GetDestinationAsync("user-1", "https", new HostString("blogify.com"));

        Assert.Equal("~/dashboard", destination);
    }

    private static LoginRedirectService CreateService(TestContext context) =>
        new(new AccessibleBlogService(context.DbContext));

    private static async Task<TestContext> CreateContextAsync()
    {
        SqliteConnection connection = new("DataSource=:memory:");
        await connection.OpenAsync();

        DbContextOptions<ApplicationDbContext> options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlite(connection)
            .Options;

        ApplicationDbContext dbContext = new(options);
        await dbContext.Database.EnsureCreatedAsync();

        return new TestContext(connection, dbContext);
    }

    private sealed class TestContext : IAsyncDisposable
    {
        public TestContext(SqliteConnection connection, ApplicationDbContext dbContext)
        {
            Connection = connection;
            DbContext = dbContext;
        }

        public SqliteConnection Connection { get; }
        public ApplicationDbContext DbContext { get; }

        public async ValueTask DisposeAsync()
        {
            await DbContext.DisposeAsync();
            await Connection.DisposeAsync();
        }
    }
}
