using Blogify.Web.Data;
using Blogify.Web.Models;
using Blogify.Web.Services;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Blogify.Tests;

public sealed class ModerationAndSafetyTests
{
    [Fact]
    public void CommentCreate_StartsPendingUntilModerated()
    {
        Comment comment = Comment.Create(Guid.NewGuid(), Guid.NewGuid(), "author", " Looks good ");

        Assert.Equal(CommentModerationStatus.Pending, comment.ModerationStatus);
        Assert.False(comment.IsApproved);
        Assert.Equal("Looks good", comment.Content);
    }

    [Fact]
    public void CommentApproveAndReject_RecordModerator()
    {
        Comment comment = Comment.Create(Guid.NewGuid(), Guid.NewGuid(), "author", "Looks good");

        comment.Approve(" moderator ");

        Assert.True(comment.IsApproved);
        Assert.Equal("moderator", comment.ModeratedByUserId);
        Assert.NotNull(comment.ModeratedAt);

        comment.Reject("moderator", " spam ");

        Assert.Equal(CommentModerationStatus.Rejected, comment.ModerationStatus);
        Assert.Equal("spam", comment.ModerationReason);
    }

    [Fact]
    public async Task BlogPermissionService_GatesModerationMediaAndSettingsByRole()
    {
        await using SqliteConnection connection = new("Data Source=:memory:");
        await connection.OpenAsync();

        DbContextOptions<ApplicationDbContext> options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlite(connection)
            .Options;

        Tenant tenant = Tenant.Create("Blog", "blog", "owner");

        await using (ApplicationDbContext setup = new(options))
        {
            await setup.Database.EnsureCreatedAsync();
            setup.Blogs.Add(tenant);
            setup.BlogMemberships.AddRange(
                BlogMembership.Create(tenant.Id, "writer", BlogRole.Writer, "owner"),
                BlogMembership.Create(tenant.Id, "editor", BlogRole.Editor, "owner"),
                BlogMembership.Create(tenant.Id, "admin", BlogRole.Admin, "owner"));
            await setup.SaveChangesAsync();
        }

        await using ApplicationDbContext dbContext = new(options);
        BlogPermissionService service = new(dbContext);

        Assert.False(await service.CanManageCommentsAsync("writer", tenant.Id));
        Assert.False(await service.CanManageMediaAsync("writer", tenant.Id));
        Assert.False(await service.CanManageSettingsAsync("writer", tenant.Id));

        Assert.True(await service.CanManageCommentsAsync("editor", tenant.Id));
        Assert.True(await service.CanManageMediaAsync("editor", tenant.Id));
        Assert.False(await service.CanManageSettingsAsync("editor", tenant.Id));

        Assert.True(await service.CanManageCommentsAsync("admin", tenant.Id));
        Assert.True(await service.CanManageMediaAsync("admin", tenant.Id));
        Assert.True(await service.CanManageSettingsAsync("admin", tenant.Id));

        Assert.True(await service.CanManageCommentsAsync("owner", tenant.Id));
        Assert.True(await service.CanManageMediaAsync("owner", tenant.Id));
        Assert.True(await service.CanManageSettingsAsync("owner", tenant.Id));
    }

    [Fact]
    public async Task Migrations_CreateCommentModerationColumns()
    {
        await using SqliteConnection connection = new("Data Source=:memory:");
        await connection.OpenAsync();

        DbContextOptions<ApplicationDbContext> options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlite(connection)
            .Options;

        await using ApplicationDbContext dbContext = new(options);
        await dbContext.Database.MigrateAsync();

        await using SqliteCommand command = connection.CreateCommand();
        command.CommandText = "PRAGMA table_info('Comments');";

        HashSet<string> columns = [];
        await using SqliteDataReader reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            columns.Add(reader.GetString(1));
        }

        Assert.Contains("ModerationStatus", columns);
        Assert.Contains("ModeratedAt", columns);
        Assert.Contains("ModeratedByUserId", columns);
        Assert.Contains("ModerationReason", columns);
    }
}
