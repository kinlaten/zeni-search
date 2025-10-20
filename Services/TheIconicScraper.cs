using System.Runtime.InteropServices;
using HtmlAgilityPack;
using Microsoft.EntityFrameworkCore;
using ZeniSearch.Api.Controllers;
using ZeniSearch.Api.Data;
using ZeniSearch.Api.Models;

namespace ZeniSearch.Api.Services;

// Fixed typo: TheIconicScaper → TheIconicScraper
public class TheIconicScraper
{
    private readonly AppDbContext _context;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<TheIconicScraper> _logger;

    //Base URL for the website
    private const string BASE_URL = "https://www.theiconic.com.au";

    // Constructor: Receives dependencies from DI
    public TheIconicScraper(
        AppDbContext context,
        IHttpClientFactory httpClientFactory,
        ILogger<TheIconicScraper> logger)
    {
        _context = context;
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }



    //Main method: Scape products by search term
    public async Task<int> ScapeProducts(string searchTerm, int maxProducts = 50)
    {
        try
        {
            _logger.LogInformation("Starting scrape for search term: {SearchTerm}", searchTerm);

            //1. Build the search URL
            var searchUrl = $"{BASE_URL}/catalog/?q={Uri.EscapeDataString(searchTerm)}";

            //2. Fetch the HTML from the website
            var html = await FetchHtmlAsync(searchTerm);

            if (string.IsNullOrEmpty(html))
            {
                _logger.LogWarning("Failing to fetch HTML from {Url}", searchUrl);
                return 0;
            }

            //3. Parse HTML to extract product data
            var products = ParseProducts(html, searchTerm, maxProducts);

            if (products.Count == 0)
            {
                _logger.LogWarning("No products found for search term: {SearchTerm}", searchTerm);
                return 0;
            }

            //4. Check which products already exist in database - avoid duplicated
            var existingUrls = await _context.Product
                            .Where(p => products.Select(x => x.ProductUrl).Contains(p.ProductUrl))
                            .Select(p => p.ProductUrl)
                            .ToListAsync();


            //Filter out products already exist in db
            var newProducts = products
                            .Where(p => !existingUrls.Contains(p.ProductUrl))
                            .ToList();

            if (newProducts.Count == 0)
            {
                _logger.LogInformation("All products already exist in database");
                return 0;
            }


            //5. Save new products to database
            _context.Product.AddRange(newProducts);
            await _context.SaveChangesAsync();

            _logger.LogInformation(
                "Successfully scraped {Count} products for '{SearchTerm}'", newProducts.Count, searchTerm
            );

            return newProducts.Count;
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Error scraping productgs for term: {SearchTerm}", searchTerm);
            throw;
        }
    }

    // Helper: Step 1: Fetch HTML from website
    private async Task<string> FetchHtmlAsync(string url)
    {
        try
        {
            // Create HttpClient using factory
            var client = _httpClientFactory.CreateClient();

            // Set User-Agent to identify our bot. Be honest
            client.DefaultRequestHeaders.Add("User-Agent", "ZeniSearch/1.0 (Price Comparison Bot; Education Purpose)");

            // Optional: Add delay to be respecful to other real human users. Dont be DDOS
            await Task.Delay(1000);

            //Make GET request
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
        catch (HttpRequestException e) { _logger.LogError(e, "HTTP request failed for URL: {Url}", url); return string.Empty; }
    }

    // Helper: Step 2: Parse HTML to extract Products. This is for all products found by searchTerm
    private List<Product> ParseProducts(string html, string searchTerm, int maxProducts)
    {

        var products = new List<Product>();

        try
        {
            //Load HTML into HtmlAgilityPack document
            var doc = new HtmlDocument();
            doc.LoadHtml(html);

            //Find all divs that have class 'product'
            var productNodes = doc.DocumentNode.SelectNodes("//div[contains(@class,'product')]");

            if (productNodes == null || productNodes.Count == 0)
            {
                _logger.LogWarning("No product nodes found in HTML");
                return products;
            }

            _logger.LogInformation("Found {Count} product nodes", productNodes.Count);

            foreach (var node in productNodes.Take(maxProducts))
            {
                try
                {
                    //Extracr product data from HTML node
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
            _logger.LogError(e, "Error parsing product");
        }
        return products;
    }


    //Helper: Step 3: Extract product info from HTML node called by step 2. This is for a single product found by searchTerm
    private Product? ExtractProductFromNode(HtmlNode node, string searchTerm)
    {
        try
        {
            // 0. Get Product Info node
            var productDetailNode = node.SelectSingleNode(".//a[contains(@class, 'product-details')]");

            // 1. Extract name 
            var nameNode = productDetailNode?.SelectSingleNode(".//span[contains(@class, 'name')]");
            var name = nameNode?.InnerText?.Trim();

            if (string.IsNullOrEmpty(name))
            {
                _logger.LogWarning("Product name not found in node");
                return null;
            }

            // 2. Extract brand: optional
            var brandNode = productDetailNode?.SelectSingleNode(".//span[contains(@class, 'brand')]");
            var brand = brandNode?.InnerText.Trim();


            // 3. Extract price
            var priceNode = productDetailNode?.SelectSingleNode(".//span[contains(@class, 'price')]");
            var priceText = priceNode?.InnerText.Trim();

            //Parse price - remove currency sign and convert to decimal: $25.88 => 25.88
            var price = ParsePrice(priceText);

            if (price == 0)
            {
                _logger.LogWarning("Invalid price for product: {Name}", name);
                return null;
            }

            // 4. Extract Product URL
            var relativeUrl = productDetailNode?.GetAttributeValue("href", "");

            if (string.IsNullOrEmpty(relativeUrl))
            {
                _logger.LogWarning("Product URL not found for: {Name}", name);
                return null;
            }

            //Convert relative URL  to absolute URL
            var productUrl = relativeUrl.StartsWith("https")
                            ? relativeUrl :
                            $"{BASE_URL}{(relativeUrl.StartsWith("/") ? "" : "/")}{relativeUrl}";

            // 5. Extract image Url
            var imgNode = node.SelectSingleNode(".//img[@src]");
            var imgUrl = imgNode?.GetAttributeValue("src", "");

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