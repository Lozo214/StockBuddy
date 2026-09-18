using Microsoft.EntityFrameworkCore;
using StockBuddy.Web.Data;
using StockBuddy.Web.Models;
namespace StockBuddy.Web.Services;

// Each operation gets its own context; Blazor circuits must not share a long-lived context.
public class InventoryService(IDbContextFactory<StockBuddyDbContext> factory, IUserSession session)
{
    public async Task<List<StockItem>> ListAsync()
    {
        var owner = await session.GetUserIdAsync();
        await using var db = await factory.CreateDbContextAsync();
        return await db.StockItems.AsNoTracking().Where(x => x.OwnerId == owner).OrderBy(x => x.Id).ToListAsync();
    }
    public async Task<StockItem> AddAsync()
    {
        var owner = await session.GetUserIdAsync();
        await using var db = await factory.CreateDbContextAsync();
        var item = new StockItem { OwnerId = owner };
        db.StockItems.Add(item);
        await db.SaveChangesAsync();
        return item;
    }
    public async Task SaveAsync(StockItem item)
    {
        var owner = await session.GetUserIdAsync();
        Validate(item);
        await using var db = await factory.CreateDbContextAsync();
        var stored = await db.StockItems.SingleOrDefaultAsync(x => x.Id == item.Id && x.OwnerId == owner)
            ?? throw new DbUpdateConcurrencyException("This row was removed in another tab.");
        if (stored.Version != item.Version)
            throw new DbUpdateConcurrencyException("This row changed in another tab.");
        stored.Name = item.Name.Trim();
        stored.Count = item.Count;
        stored.Adjustment = item.Adjustment;
        stored.Version = Guid.NewGuid();
        await db.SaveChangesAsync();
        item.Name = stored.Name;
        item.Version = stored.Version;
    }
    public async Task DeleteAsync(StockItem item)
    {
        var owner = await session.GetUserIdAsync();
        await using var db = await factory.CreateDbContextAsync();
        var stored = await db.StockItems.SingleOrDefaultAsync(x => x.Id == item.Id && x.OwnerId == owner)
            ?? throw new DbUpdateConcurrencyException("This row is no longer available.");
        if (stored.Version != item.Version)
            throw new DbUpdateConcurrencyException("This row changed in another tab.");
        db.Remove(stored);
        await db.SaveChangesAsync();
    }
    public static void Validate(StockItem item)
    {
        if (item.Name is null || item.Name.Length > 120)
            throw new ArgumentException("Item names can contain at most 120 characters.");
        if (item.Count < 0) throw new ArgumentException("Count cannot be negative.");
        if (item.Adjustment < 1) throw new ArgumentException("Adjustment must be at least 1.");
    }
    public static int Adjust(int count, int amount, bool subtract)
    {
        if (count < 0 || amount < 1) throw new ArgumentException("Use a nonnegative count and positive adjustment.");
        var result = subtract ? Math.Max(0L, (long)count - amount) : (long)count + amount;
        if (result > int.MaxValue) throw new ArgumentException("That adjustment would exceed the maximum count.");
        return (int)result;
    }
}
