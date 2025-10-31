using System.ComponentModel.DataAnnotations;

namespace ZeniSearch.Api.Services;

public class ScraperService
{
    private readonly ScraperFactory _factory;
    private readonly ScraperMonitor _monitor;
    private readonly ILogger<ScraperService> _logger;

    // Use IServiceProvider to create scope per job rather than AppDbContext
    // constructor
    public ScraperService(
        ScraperFactory factory,
        ScraperMonitor monitor,
        ILogger<ScraperService> logger)
    {
        _factory = factory;
        _monitor = monitor;
        _logger = logger;
    }

    // Scrape all retailers for a given searchTerm
    public async Task ScrapeAllRetailers(string searchTerm)
    {
        _logger.LogInformation("Starting scheduled scrape for: {SearchTerm}", searchTerm);

        // Get all scrapers
        var scrapers = _factory.GetAllScrapers();

        var results = new Dictionary<string, int>();

        foreach (var scraper in scrapers)
        {
            try
            {
                // Use monitor to wrap scraper execution with structured logging and alerting
                var count = await _monitor.MonitorScraperExecution(
                    scraper.RetailerName,
                    async () => await scraper.ScrapeProducts(searchTerm, maxProducts: 100)
                );

                results[scraper.RetailerName] = count;

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
        // Only get scrapers that pass health check
        var healthyScrapers = await _factory.GetHealthyScrapers();

        _logger.LogInformation(
            "Found {Count} healthy scrapers",
            healthyScrapers.Count()
        );

        foreach (var scraper in healthyScrapers)
        {
            try
            {
                // Use monitor for structured monitoring
                await _monitor.MonitorScraperExecution(
                    $"{scraper.RetailerName}-healthy",
                    async () => await scraper.ScrapeProducts(searchTerm, maxProducts: 50)
                );

                await Task.Delay(TimeSpan.FromSeconds(5));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Scrape failed for {Retailer}", scraper.RetailerName);
            }
        }
    }
}