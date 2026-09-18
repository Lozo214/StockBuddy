using Microsoft.EntityFrameworkCore;
using StockBuddy.Web.Models;
namespace StockBuddy.Web.Data;

public class StockBuddyDbContext(DbContextOptions<StockBuddyDbContext> options) : DbContext(options)
{
    public DbSet<StockItem> StockItems => Set<StockItem>();
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<StockItem>().HasIndex(x => x.OwnerId);
        modelBuilder.Entity<StockItem>().ToTable("StockItems", table =>
        {
            table.HasCheckConstraint("CK_Count", "\"Count\" >= 0");
            table.HasCheckConstraint("CK_Adjustment", "\"Adjustment\" > 0");
            table.HasCheckConstraint("CK_Name", "length(\"Name\") <= 120");
        });
    }
}
