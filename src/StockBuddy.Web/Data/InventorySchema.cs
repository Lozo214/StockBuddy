using Microsoft.EntityFrameworkCore;

namespace StockBuddy.Web.Data;

public static class InventorySchema
{
    public static async Task InitializeAsync(StockBuddyDbContext db)
    {
        await db.Database.EnsureCreatedAsync();
        await db.Database.OpenConnectionAsync();
        try
        {
            // One additive, idempotent upgrade for databases made by the original prototype.
            // Existing rows keep a null owner and are inaccessible through the account service.
            await using var command = db.Database.GetDbConnection().CreateCommand();
            command.CommandText = "PRAGMA table_info('StockItems')";
            var hasOwner = false;
            await using (var reader = await command.ExecuteReaderAsync())
                while (await reader.ReadAsync())
                    hasOwner |= reader.GetString(1) == "OwnerId";
            if (!hasOwner)
                await db.Database.ExecuteSqlRawAsync("ALTER TABLE StockItems ADD COLUMN OwnerId TEXT NULL");
            await db.Database.ExecuteSqlRawAsync("CREATE INDEX IF NOT EXISTS IX_StockItems_OwnerId ON StockItems (OwnerId)");
        }
        finally { await db.Database.CloseConnectionAsync(); }
    }
}
