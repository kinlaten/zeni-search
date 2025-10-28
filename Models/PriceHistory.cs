using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ZeniSearch.Api.Models;

public class PriceHistory
{
    public int Id { get; set; }

    // Foreign key to Product
    [Required]
    public int ProductId { get; set; }

    // Navigation property to Product
    public Product Product { get; set; } = null!;

    [Required]
    [Column(TypeName = "decimal(10,2)")]
    public decimal Price { get; set; }

    [Required]
    public DateTime RecordedAt { get; set; } = DateTime.UtcNow;

    [MaxLength(100)]
    public string? Source { get; set; }
}