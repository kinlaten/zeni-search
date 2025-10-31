using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;
using ZeniSearch.Api.Data;
using ZeniSearch.Api.Models;

namespace ZeniSearch.Api.Services;

public class PriceHistoryService
{
    private readonly AppDbContext _context;
    private readonly ILogger _logger;

    public PriceHistoryService(
        AppDbContext context,
        ILogger<PriceHistoryService> logger
    )
    {
        _context = context;
        _logger = logger;
    }

    public async Task<bool> RecordPriceIfChange(int productId, decimal newPrice, string source = "scraper")
    {
        try
        {
            // Get the msot recent price of this product
            var lastestPrice = await _context.PriceHistory
                .Where(ph => ph.ProductId == productId)
                .OrderByDescending(ph => ph.RecordedAt)
                .Select(ph => ph.Price)
                .FirstOrDefaultAsync();

            // If price changed( or no history exist), record it
            if (lastestPrice == 0 || lastestPrice != newPrice)
            {
                var priceHistory = new PriceHistory
                {
                    ProductId = productId,
                    Price = newPrice,
                    RecordedAt = DateTime.UtcNow,
                    Source = source
                };

                _context.PriceHistory.Add(priceHistory);
                await _context.SaveChangesAsync();

                _logger.LogInformation(
                    "Price changed for product {ProductId}: {OldPrice} -> {NewPrice}",
                    productId,
                    lastestPrice,
                    newPrice
                );

                return true;
            }
            return false;
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Error recording price history for product {ProductId}", productId);
            throw;
        }
    }

    public async Task<List<PriceHistory>> GetPriceHistory(
        int productId,
        DateTime? startDate = null,
        DateTime? endDate = null
    )
    {
        var query = _context.PriceHistory.Where(ph => ph.ProductId == productId);

        if (startDate.HasValue)
        {
            query = query.Where(ph => ph.RecordedAt >= startDate.Value);
        }

        if (endDate.HasValue)
        {
            query = query.Where(ph => ph.RecordedAt <= endDate.Value);
        }

        return await query.OrderBy(ph => ph.RecordedAt)
                            .ToListAsync();
    }

    public async Task<decimal?> GetLowestPrice(int productId)
    {
        return await _context.PriceHistory
        .Where(ph => ph.ProductId == productId)
        .MinAsync(ph => (decimal?)ph.Price);
    }

    public async Task<decimal?> GetHighestPrice(int productId)
    {
        return await _context.PriceHistory
            .Where(ph => ph.ProductId == productId)
            .MaxAsync(ph => (decimal?)ph.Price);
    }

    public async Task<decimal?> GetAveragePrice(int productId)
    {
        return await _context.PriceHistory
            .Where(ph => ph.ProductId == productId)
            .AverageAsync(ph => (decimal?)ph.Price);
    }

    public async Task<bool> IsPriceDrop(int productId, decimal threshholdPercentage = 10)
    {
        // Get last 2 prices
        var recentPrices = await _context.PriceHistory
                            .Where(ph => ph.ProductId == productId)
                            .OrderByDescending(ph => ph.RecordedAt)
                            .Take(2)
                            .Select(ph => ph.Price)
                            .ToListAsync();

        if (recentPrices.Count < 2)
        {
            return false;
        }

        var currentPrice = recentPrices[0];
        var previousPrice = recentPrices[1];

        var dropPercentage = ((previousPrice - currentPrice) / previousPrice) * 100;

        return dropPercentage >= threshholdPercentage;
    }


    public async Task<List<Product>> GetProductsWithPriceDrops(decimal threshholdPercentage = 10, int daysBack = 7)
    {
        // Get all product in last 7 days
        var cutoffDate = DateTime.UtcNow.AddDays(-daysBack);

        var productsWithDrops = new List<Product>();

        var products = await _context.Product.Where(p => p.LastUpdated > cutoffDate).ToListAsync();

        // See which one has dropPrice
        foreach (var product in products)
        {
            var hasDrop = await IsPriceDrop(product.Id, threshholdPercentage);
            if (hasDrop)
            {
                productsWithDrops.Add(product);
            }
        }

        return productsWithDrops;
    }
}