using System.ComponentModel.DataAnnotations;

namespace ZeniSearch.Api.Services;

public class ScraperService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<ScraperService> _logger;

    // Use IServiceProvider to create scope per job rather than AppDbContext
    // constructor
    public ScraperService(IServiceProvider serviceProvider, ILogger<ScraperService> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    // Scrape all retailers for a given searchTerm
    public async Task ScrapeAllRetailers(string searchTerm)
    {
        _logger.LogInformation("Starting scheduled scrape for: {SearchTerm}", searchTerm);

        using var scope = _serviceProvider.CreateScope();
        var factory = scope.ServiceProvider.GetRequiredService<ScraperFactory>();

        // Get all scrapers
        var scrapers = factory.GetAllScrapers();

        var results = new Dictionary<string, int>();

        foreach (var scraper in scrapers)
        {
            try
            {

                _logger.LogInformation(
                    "Scraping {Retailer} for '{SearchTerm}'",
                    scraper.RetailerName,
                    searchTerm
                );

                var count = await scraper.ScraperProducts(searchTerm, maxProducts: 100);

                _logger.LogInformation(
                    "{Retailer}: {Count} new products",
                   scraper.RetailerName,
                   count
                );

                // Rate limiting: wait between retailers
                await Task.Delay(TimeSpan.FromSeconds(5));
            }
            catch (Exception e)
            {
                _logger.LogError(e, "Failed to scrape {RetailerName}", scraper.RetailerName);
                results[scraper.RetailerName] = 0;
            }
        }

        var total = results.Values.Sum();
        _logger.LogInformation(
            "Multi-retailer scrape complete. Total: {Total} new products. Details: {@Results}",
            total,
            results
        );
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

    public async Task ScrapeHealthyRetailers(string searchTerm)
    {
        using var scope = _serviceProvider.CreateScope();
        var factory = scope.ServiceProvider.GetRequiredService<ScraperFactory>();


        // Only get scrapers that pass health check
        var healthyScrapers = await factory.GetHealthyScrapers();

        _logger.LogInformation(
            "Found {Count} healthy scrapers",
            healthyScrapers.Count()
        );

        foreach (var scraper in healthyScrapers)
        {
            try
            {
                await scraper.ScraperProducts(searchTerm, maxProducts: 50);
                await Task.Delay(TimeSpan.FromSeconds(5));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Scrape failed for {Retailer}", scraper.RetailerName);
            }
        }
    }
}