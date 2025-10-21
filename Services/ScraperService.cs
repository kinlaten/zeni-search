using System.ComponentModel.DataAnnotations;
using ZeniSearch.Api.Data;

namespace ZeniSearch.Api.Services;

public class ScraperService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger _logger;

    // Use IServiceProvider to create scope per job rather than AppDbContext
    // constructor
    public ScraperService(IServiceProvider serviceProvider, ILogger logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    // Scrape all retailers for a given searchTerm
    public async Task ScrapeAllRetailers(string searchTerm)
    {
        _logger.LogInformation("Starting scheduled scrape for: {SearchTerm}", searchTerm);

        try
        {
            // Create scope for this job
            // This ensures DBContext is disposed (release the connection to DB ), to allow connection from this job to DB
            using var scope = _serviceProvider.CreateScope();

            //Get scraper from scope
            var scraper = scope.ServiceProvider
                        .GetRequiredService<IProductScraper>();

            var count = await scraper.ScraperProducts(searchTerm, maxProducts: 100);

            _logger.LogInformation(
                "Scheduled scrape completd. Scraped {Count} products for '{SearchTerm}'",
               count,
               searchTerm
            );
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Scheduled scrape failed for: {SearchTerm}", searchTerm);
            throw; //hangfire will retry
        }

    }

    public async Task ScrapePopularProducts()
    {
        var popularSearchs = new[] { "sandals", "slides", "thongs", "flip flops" };

        foreach (var term in popularSearchs)
        {
            await ScrapeAllRetailers(term);

            // Rate limiting: wait between searches
            await Task.Delay(TimeSpan.FromSeconds(5));
        }
    }
}