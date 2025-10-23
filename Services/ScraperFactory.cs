using System.Numerics;

namespace ZeniSearch.Api.Services;

// Factory to get the right scraper for a retailer
public class ScraperFactory
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<ScraperFactory> _logger;

    public ScraperFactory(
        IServiceProvider serviceProvider,
        ILogger<ScraperFactory> logger
    )
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    // Get all registed scrapers
    public IEnumerable<IProductScraper> GetAllScrapers()
    {
        return _serviceProvider.GetServices<IProductScraper>();
    }

    // Get scraper based on retailer name
    public IProductScraper? GetService(string retailerName)
    {
        var scrapers = GetAllScrapers();

        return scrapers.FirstOrDefault(s =>
        s.RetailerName.Equals(retailerName, StringComparison.OrdinalIgnoreCase));
    }

    // Get all healthy scrapers
    public async Task<IEnumerable<IProductScraper>> GetHealthyScrapers()
    {
        var scrapers = GetAllScrapers();
        var healthy = new List<IProductScraper>();

        foreach (var scraper in scrapers)
        {
            try
            {
                if (await scraper.HealthCheck())
                {
                    healthy.Add(scraper);
                }
            }
            catch (Exception e)
            {
                _logger.LogWarning(e, "Healthy cherck failed for {Retailer}", scraper.RetailerName);
            }
        }
        return healthy;
    }
}
