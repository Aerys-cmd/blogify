using Microsoft.EntityFrameworkCore;

namespace Blogify.Web.Data;

public sealed class DatabaseMigrator(ApplicationDbContext dbContext)
{
    public async Task MigrateAsync(CancellationToken cancellationToken = default)
    {
        await dbContext.Database.MigrateAsync(cancellationToken);
    }
}

