using Microsoft.EntityFrameworkCore;
using System.Data.Common;

namespace Blogify.Web.Data;

public sealed class DatabaseMigrator(ApplicationDbContext dbContext)
{
    public async Task MigrateAsync(CancellationToken cancellationToken = default)
    {
        DbConnection connection = dbContext.Database.GetDbConnection();
        await connection.OpenAsync(cancellationToken);

        await using (DbCommand command = connection.CreateCommand())
        {
            command.CommandText = "PRAGMA journal_mode=WAL;";
            await command.ExecuteScalarAsync(cancellationToken);
        }

        await connection.CloseAsync();
        await dbContext.Database.MigrateAsync(cancellationToken);
    }
}
