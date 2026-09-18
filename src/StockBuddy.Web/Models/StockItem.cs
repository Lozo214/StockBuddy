using System.ComponentModel.DataAnnotations;
namespace StockBuddy.Web.Models;

public class StockItem
{
    public int Id { get; set; }
    // Null is reserved for preserved inventory from the original shared prototype.
    [MaxLength(450)]
    public string? OwnerId { get; set; }
    [MaxLength(120)]
    public string Name { get; set; } = string.Empty;
    public int Count { get; set; }
    public int Adjustment { get; set; } = 1;
    [ConcurrencyCheck]
    public Guid Version { get; set; } = Guid.NewGuid();
}
