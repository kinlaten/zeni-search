using Microsoft.EntityFrameworkCore;
using ZeniSearch.Api.Models;

namespace ZeniSearch.Api.Data;

public class AppDbContext : DbContext
{
    //Constructor
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    //Setter
    public DbSet<Product> Product { get; set; }

    //Custom Config
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        //Apply default config at first
        base.OnModelCreating(modelBuilder);

        //Indexes for better performance
        modelBuilder.Entity<Product>().HasIndex(p => p.RetailerName);
        modelBuilder.Entity<Product>().HasIndex(p => p.Price);

        // More search index (add later)
    }
}