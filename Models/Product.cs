using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ZeniSearch.Api.Models;

public class Product
{
    //Core properties
    public int Id { get; set; }

    [Required]
    [MaxLength(500)]
    public string Name { get; set; } = string.Empty;

    [Required]
    [MaxLength(1000)]
    public string ProductUrl { get; set; } = string.Empty;

    [Required]
    [Column(TypeName = "decimal(10,2)")]
    public decimal Price { get; set; }

    [Required]
    [MaxLength(100)]
    public string RetailerName { get; set; } = string.Empty;

    [Required]
    public DateTime LastUpdated { get; set; } = DateTime.UtcNow;

    //Optional
    [MaxLength(200)]
    public string? Brand { get; set; }

    [MaxLength(1000)]
    public string? ImageUrl { get; set; }


}