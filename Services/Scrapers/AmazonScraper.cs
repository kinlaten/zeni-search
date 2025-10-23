using HtmlAgilityPack;
using Microsoft.EntityFrameworkCore;
using ZeniSearch.Api.Data;
using ZeniSearch.Api.Models;
using ZeniSearch.Api.Services;

namespace ZeniSearch.Api.Services.Scrapers;

public class AmazonScraper : IProductScraper
{
    private readonly AppDbContext _context;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<AmazonScraper> _logger;

    //BASE URL for the website
    private readonly string BASE_URL = "https://www.amazon.com.au";

    public string RetailerName => "Amazon Au";

    public AmazonScraper(
        AppDbContext context,
        IHttpClientFactory httpClientFactory,
        ILogger<AmazonScraper> logger
    )
    {
        _context = context;
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    // Main method: Scrape products by search term
    public async Task<int> ScrapeProducts(string searchTerm, int maxProducts)
    {
        //TODO: Logic to scrape product from AZ
        try
        {
            _logger.LogInformation("Starting scrape for search term: {SearchTerm}", searchTerm);

            // 1. Build the search URL
            var searchUrl = $"{BASE_URL}/s?k={Uri.EscapeDataString(searchTerm)}";

            // 2. Fetch the HTML from the site
            var html = await FetchHtmlAsync(searchUrl);

            if (string.IsNullOrEmpty(html))
            {
                _logger.LogWarning("Failing to fetch HTML from {Url}", searchUrl);
            }

            // 3. Parse HTML to get products
            var products = ParseProducts(html, searchTerm, maxProducts);

            if (products.Count == 0)
            {
                _logger.LogWarning("No products found for search term: {SearchTerm}", searchTerm);
                return 0;
            }

            // 4. Check if any fetched products already exist in db, if so filter out
            var existingUrls = await _context.Product
                            .Where(p => products.Select(x => x.ProductUrl).Contains(p.ProductUrl))
                            .Select(p => p.ProductUrl)
                            .ToListAsync();

            var newProducts = products
                            .Where(p => !existingUrls.Contains(p.ProductUrl))
                            .ToList();

            if (newProducts.Count == 0)
            {
                _logger.LogInformation("All products already exist in database");
                return 0;
            }

            // 5. Save new products into db
            _context.Product.AddRange(newProducts);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Successfully scraped {Count} products for '{SearchTerm}'",
            newProducts.Count, searchTerm);

            return newProducts.Count;
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Error scraping products for term: {SearchTerm}", searchTerm);
            throw;
        }
    }

    public async Task<bool> HealthCheck()
    {
        try
        {
            var html = await FetchHtmlAsync(BASE_URL);
            return !string.IsNullOrEmpty(html);
        }
        catch
        {
            return false;
        }
    }

    // Helper

    private async Task<string> FetchHtmlAsync(string url)
    {
        try
        {
            var client = _httpClientFactory.CreateClient();

            client.DefaultRequestHeaders.Add("User-Agent", "ZeniSearch/1.0 (Price Comparison Bot)");

            await Task.Delay(1000);

            // Make GET request 
            var response = await client.GetAsync(url);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning(
                    "HTTP request failed with status {StatusCode} for {Url}",
                    response.StatusCode,
                    url
                );
                return string.Empty;
            }

            // Read response body
            var html = await response.Content.ReadAsStringAsync();
            return html;
        }
        catch (HttpRequestException e)
        {
            _logger.LogError(e, "HTTP request failed for URL: {Url}", url);
            return string.Empty;
        }
    }

    private List<Product> ParseProducts(string html, string searchTerm, int maxProducts)
    {
        var products = new List<Product>();
        try
        {
            // Load HTML into a document
            var doc = new HtmlDocument();
            doc.LoadHtml(html);

            // Find all root nodes of products
            var productNodes = doc.DocumentNode.SelectNodes("//div[contains(@role,'listitem')]");

            if (productNodes == null || productNodes.Count == 0)
            {
                _logger.LogWarning("No product nodes found in HTML");
                return products;// empty list
            }

            _logger.LogInformation("Found {Count} product nodes", productNodes.Count);

            foreach (var node in productNodes.Take(maxProducts))
            {
                try
                {
                    // Extract data for each product node
                    var product = ExtractProductFromNode(node, searchTerm);

                    if (product != null)
                    {
                        products.Add(product);
                    }
                }
                catch (Exception e)
                {
                    _logger.LogWarning(e, "Failed to parse individual product node");
                    continue;
                }
            }

            _logger.LogInformation("Successfully parsed {Count} products", products.Count);

        }
        catch (Exception e)
        {
            _logger.LogWarning(e, "Error parsing product");
        }
        return products;
    }

    private Product? ExtractProductFromNode(HtmlNode node, string searchTerm)
    {
        try
        {
            // 0. Get Product Info node
            var productDetailNode = node.SelectSingleNode(".//div[contains(@data-component-type, 's-search-result')]");

            // 1. Extract name 
            var nameNode = productDetailNode?.SelectSingleNode(".//a[@class='a-link-normal s-line-clamp-2 s-line-clamp-3-for-col-12 s-link-style a-text-normal']/h2/span");
            var name = nameNode?.InnerText.Trim();

            if (string.IsNullOrEmpty(name))
            {
                _logger.LogWarning("Product name not found in node");
                return null;
            }

            // 2. Extract brand: optional
            var brandNode = productDetailNode?.SelectSingleNode(".//div[@data-cy='title-recipe']//span[@class='a-size-base-plus a-color-base']");
            var brand = brandNode?.InnerText.Trim();


            // 3. Extract price
            var priceNode = productDetailNode?.SelectSingleNode(".//div[@data-cy='price-recipe']//span[@class='a-price']/span[@class='a-offscreen']");
            var priceText = priceNode?.InnerText.Trim();

            //Parse price - remove currency sign and convert to decimal: $25.88 => 25.88
            var price = ParsePrice(priceText);

            if (price == 0)
            {
                _logger.LogWarning("Invalid price for product: {Name}", name);
                return null;
            }

            // 4, 5. Extract image Url and Product URL

            var imgNode = node.SelectSingleNode(".//div[contains(@class,'s-product-image-container')]//img[@class='a-link-normal s-no-outline']");

            var relativeUrl = imgNode.GetAttributeValue("href", "");
            var imgUrl = imgNode?.SelectSingleNode(".//img[@class='s-image']")?.GetAttributeValue("src", "");

            if (string.IsNullOrEmpty(relativeUrl))
            {
                _logger.LogWarning("Product URL not found for: {Name}", name);
                return null;
            }

            var productUrl = relativeUrl.StartsWith("https")
                            ? relativeUrl :
                            $"{BASE_URL}{(relativeUrl.StartsWith("/") ? "" : "/")}{relativeUrl}";

            // 6. Create Product object
            var product = new Product
            {
                Name = name,
                Brand = brand,
                Price = price,
                ProductUrl = productUrl,
                ImageUrl = imgUrl,
                RetailerName = "The Iconic",
                LastUpdated = DateTime.UtcNow
            };

            return product;
        }
        catch (Exception e) { _logger.LogError(e, "Error extracting product from node"); return null; }
    }


    private decimal ParsePrice(string? priceText)
    {
        if (string.IsNullOrEmpty(priceText))
        {
            return 0;
        }

        try
        {
            // 1. Remove prefix currency sign
            var cleaned = priceText
                        .Replace("$", "")
                        .Replace("AUD", "")
                        .Trim();


            // 2. Parse as decimal
            if (decimal.TryParse(cleaned, out var price))
            {
                return price;
            }

            _logger.LogWarning("Failed to parse price: {PriceText}", priceText);
            return 0;
        }
        catch { return 0; }
    }

}