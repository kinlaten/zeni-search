
using HtmlAgilityPack;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using ZeniSearch.Api.Data;
using ZeniSearch.Api.Models;

namespace ZeniSearch.Api.Services;

public class CachedProductService
{
    private readonly AppDbContext _context;
    private readonly IMemoryCache _cache;
    private readonly ILogger<CachedProductService> _logger;

    public CachedProductService(AppDbContext context
    , IMemoryCache cache, ILogger<CachedProductService> logger)
    {
        _context = context;
        _cache = cache;
        _logger = logger;
    }

    // Search products with caching and fallback if needed
    public async Task<List<Product>> SearchProducts(string query)
    {
        var cacheKey = $"search_{query}";

        // If cache has data, get it
        if (_cache.TryGetValue(cacheKey, out List<Product>? cachedProducts))
        {
            _logger.LogInformation("Returning cached results for: {Query}", query);
            return cachedProducts ?? new List<Product>();
        }

        // Else, get from database, also keep those data into cache
        try
        {
            // Get from db
            var products = await _context.Product
                            .Where(p => p.Name.Contains(query) || (p.Brand != null && p.Brand.Contains(query)))
                            .OrderBy(p => p.Price)
                            .Take(50)
                            .ToListAsync();

            // Cache for 5 minutes
            var cacheOptions = new MemoryCacheEntryOptions()
                .SetAbsoluteExpiration(TimeSpan.FromMinutes(5));

            _cache.Set(cacheKey, products, cacheOptions);

            // Cache for longer, as stale data
            var staleCacheOptions = new MemoryCacheEntryOptions()
                .SetAbsoluteExpiration(TimeSpan.FromHours(1));

            _cache.Set(cacheKey + "_stale", products, staleCacheOptions);

            return products;
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Database query failed, returning cached data");

            // Fallback: try to return stale cache
            if (_cache.TryGetValue(cacheKey + "_stale", out List<Product>? staleProducts))
            {
                return staleProducts ?? new List<Product>();
            }

            return new List<Product>();
        }
    }

}