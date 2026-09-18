using Microsoft.EntityFrameworkCore;
using StockBuddy.Web.Data;
using StockBuddy.Web.Models;
using StockBuddy.Web.Services;
using Xunit;

namespace StockBuddy.Tests;

public class InventoryTests : IAsyncLifetime
{
    private readonly string path = Path.Combine(Path.GetTempPath(), $"stockbuddy-test-{Guid.NewGuid()}.db");
    private InventoryService service = null!;
    private TestFactory factory = null!;

    public async Task InitializeAsync()
    {
        factory = new TestFactory(new DbContextOptionsBuilder<StockBuddyDbContext>()
            .UseSqlite($"Data Source={path};Pooling=False").Options);
        await using var db = factory.CreateDbContext();
        await db.Database.EnsureCreatedAsync();
        service = new InventoryService(factory, new TestUser("alice"));
    }
    public Task DisposeAsync() { File.Delete(path); return Task.CompletedTask; }

    [Fact]
    public async Task DataSurvivesNewContextAndService()
    {
        var item = await service.AddAsync();
        item.Name = "Printer paper";
        item.Count = 12;
        item.Adjustment = 5;
        await service.SaveAsync(item);
        var reloaded = Assert.Single(await new InventoryService(factory, new TestUser("alice")).ListAsync());
        Assert.Equal(item.Id, reloaded.Id);
        Assert.Equal("Printer paper", reloaded.Name);
        Assert.Equal(12, reloaded.Count);
        Assert.Equal(5, reloaded.Adjustment);
        await service.DeleteAsync(reloaded);
        Assert.Empty(await service.ListAsync());
    }

    [Fact]
    public async Task StaleEditCannotOverwriteAnotherTabsChange()
    {
        await service.AddAsync();
        var first = Assert.Single(await service.ListAsync());
        var second = Assert.Single(await service.ListAsync());
        first.Count = 5;
        await service.SaveAsync(first);
        second.Count = 10;
        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => service.SaveAsync(second));
        Assert.Equal(5, Assert.Single(await service.ListAsync()).Count);
        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => service.DeleteAsync(second));
    }

    [Fact]
    public async Task DeletedRowCannotBeResurrectedByStaleEdit()
    {
        var row = await service.AddAsync();
        await service.DeleteAsync(row);
        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => service.SaveAsync(row));
    }

    [Theory]
    [InlineData(-1, 1)]
    [InlineData(0, 0)]
    [InlineData(0, -1)]
    public async Task InvalidValuesAreNotPersisted(int count, int adjustment)
    {
        var row = await service.AddAsync();
        row.Count = count;
        row.Adjustment = adjustment;
        await Assert.ThrowsAsync<ArgumentException>(() => service.SaveAsync(row));
        var saved = Assert.Single(await service.ListAsync());
        Assert.Equal(0, saved.Count);
        Assert.Equal(1, saved.Adjustment);
    }

    [Fact]
    public async Task NamesHaveLengthLimit()
    {
        var row = await service.AddAsync();
        row.Name = new string('x', 121);
        await Assert.ThrowsAsync<ArgumentException>(() => service.SaveAsync(row));
    }

    [Theory]
    [InlineData(12, 5, false, 17)]
    [InlineData(12, 5, true, 7)]
    [InlineData(3, 5, true, 0)]
    public void AdjustmentIsCorrect(int count, int amount, bool subtract, int expected) =>
        Assert.Equal(expected, InventoryService.Adjust(count, amount, subtract));

    [Fact]
    public void OverflowIsRejected() =>
        Assert.Throws<ArgumentException>(() => InventoryService.Adjust(int.MaxValue, 1, false));

    private class TestFactory(DbContextOptions<StockBuddyDbContext> options) : IDbContextFactory<StockBuddyDbContext>
    {
        public StockBuddyDbContext CreateDbContext() => new(options);
    }

    private class TestUser(string? id) : IUserSession
    {
        public Task<string> GetUserIdAsync() =>
            id is null ? throw new UnauthorizedAccessException() : Task.FromResult(id);
    }

    [Fact]
    public async Task UsersCannotReadUpdateOrDeleteAnotherUsersItemsEvenWithForgedOwner()
    {
        var alice = await service.AddAsync();
        alice.Name = "Private inventory";
        alice.Count = 7;
        await service.SaveAsync(alice);
        var bobService = new InventoryService(factory, new TestUser("bob"));
        Assert.Empty(await bobService.ListAsync());
        var bob = await bobService.AddAsync();
        Assert.Equal("bob", bob.OwnerId);
        alice.OwnerId = "bob"; // Simulate tampering with a submitted record.
        alice.Count = 99;
        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => bobService.SaveAsync(alice));
        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => bobService.DeleteAsync(alice));
        Assert.Equal(7, Assert.Single(await service.ListAsync()).Count);
        Assert.Single(await bobService.ListAsync());
    }

    [Fact]
    public async Task AnonymousOperationsAreRejected()
    {
        var anonymous = new InventoryService(factory, new TestUser(null));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => anonymous.ListAsync());
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => anonymous.AddAsync());
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => anonymous.SaveAsync(new StockItem()));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => anonymous.DeleteAsync(new StockItem()));
    }

    [Fact]
    public async Task LegacySchemaUpgradePreservesAndHidesExistingSharedRows()
    {
        await using var db = factory.CreateDbContext();
        await db.Database.EnsureDeletedAsync();
        await db.Database.ExecuteSqlRawAsync("""
            CREATE TABLE StockItems (
                Id INTEGER PRIMARY KEY AUTOINCREMENT, Name TEXT NOT NULL,
                Count INTEGER NOT NULL, Adjustment INTEGER NOT NULL, Version TEXT NOT NULL);
            INSERT INTO StockItems (Name, Count, Adjustment, Version)
            VALUES ('Preserved shared item', 42, 1, '00000000-0000-0000-0000-000000000001');
            """);
        await InventorySchema.InitializeAsync(db);
        await InventorySchema.InitializeAsync(db); // Idempotent across restarts.
        var legacy = await db.StockItems.SingleAsync();
        Assert.Equal(42, legacy.Count);
        Assert.Null(legacy.OwnerId);
        Assert.Empty(await service.ListAsync());
        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => service.DeleteAsync(legacy));
        Assert.Equal("alice", (await service.AddAsync()).OwnerId);
        Assert.Single(await service.ListAsync());
    }
}
