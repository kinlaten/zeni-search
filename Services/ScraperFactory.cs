
namespace ZeniSearch.Api.Services;

// Factory to get the right scraper for a retailer
public class ScraperFactory
{
    private readonly IEnumerable<IProductScraper> _scrapers;
    private readonly ILogger<ScraperFactory> _logger;

    public ScraperFactory(
        IEnumerable<IProductScraper> scrapers,
        ILogger<ScraperFactory> logger
    )
    {
        _scrapers = scrapers;
        _logger = logger;
    }

    // Get all registed scrapers
    public IEnumerable<IProductScraper> GetAllScrapers()
    {
        return _scrapers;
    }

    // Get scraper based on retailer name
    public IProductScraper? GetService(string retailerName)
    {
        return _scrapers.FirstOrDefault(s =>
        s.RetailerName.Equals(retailerName, StringComparison.OrdinalIgnoreCase));
    }

    // Get all healthy scrapers
    public async Task<IEnumerable<IProductScraper>> GetHealthyScrapers()
    {
        var healthy = new List<IProductScraper>();

        foreach (var scraper in _scrapers)
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
