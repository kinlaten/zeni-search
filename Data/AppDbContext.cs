using Microsoft.EntityFrameworkCore;
using ZeniSearch.Api.Models;

namespace ZeniSearch.Api.Data;

public class AppDbContext : DbContext
{
    //Constructor
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    //Setter
    public DbSet<Product> Product { get; set; }
    public DbSet<PriceHistory> PriceHistory { get; set; }

    //Custom Config
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        //Apply default config at first
        base.OnModelCreating(modelBuilder);

        // Product config
        modelBuilder.Entity<Product>().HasIndex(p => p.RetailerName);
        modelBuilder.Entity<Product>().HasIndex(p => p.Price);

        // PriceHistory config
        modelBuilder.Entity<PriceHistory>()
            .HasOne(ph => ph.Product)
            .WithMany(p => p.PriceHistory)
            .HasForeignKey(ph => ph.ProductId)
            .OnDelete(DeleteBehavior.Cascade); //Dlete history when product deleted

        modelBuilder.Entity<PriceHistory>().HasIndex(ph => ph.ProductId);
        modelBuilder.Entity<PriceHistory>().HasIndex(ph => ph.RecordedAt);
        modelBuilder.Entity<PriceHistory>().HasIndex(ph => new { ph.ProductId, ph.RecordedAt });

        // More search index (add later)
    }
}