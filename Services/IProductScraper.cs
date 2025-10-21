using ZeniSearch.Api.Models;

namespace ZeniSearch.Api.Services;

//Interface for all product scrapers
public interface IProductScraper
{
    string RetailerName { get; }

    Task<int> ScraperProducts(string searchTerm, int maxProducts = 50); //return number of new products added

    Task<bool> HealthCheck();
}