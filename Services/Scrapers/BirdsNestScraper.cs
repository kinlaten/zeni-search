using HtmlAgilityPack;
using ZeniSearch.Api.Data;
using Microsoft.EntityFrameworkCore;
using ZeniSearch.Api.Models;

namespace ZeniSearch.Api.Services.Scrapers;

public class BirdsNestScraper : IProductScraper
{
    private readonly AppDbContext _context;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<BirdsNestScraper> _logger;


    private const string BASE_URL = "https://www.birdsnest.com.au";

    public string RetailerName => "Birds Nest";

    public BirdsNestScraper(AppDbContext context,
    IHttpClientFactory httpClientFactory,
    ILogger<BirdsNestScraper> logger)
    {
        _context = context;
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    //Main method: Scrape products by search term
    public async Task<int> ScrapeProducts(string searchTerm, int maxProducts = 50)
    {
        try
        {
            _logger.LogInformation("Starting scrape for search term: {SearchTerm}", searchTerm);

            //1. Build the search URL
            var searchUrl = $"{BASE_URL}/search.php?search_query={Uri.EscapeDataString(searchTerm)}&section=content";

            //2. Fetch the HTML from the website
            var html = await FetchHtmlAsync(searchUrl);

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
        catch { return false; }
    }

    // Helper: Step 1: Fetch HTML from website
    private async Task<string> FetchHtmlAsync(string url)
    {
        try
        {
            // Create HttpClient using factory
            var client = _httpClientFactory.CreateClient();

            // Set User-Agent to identify our bot. Be honest
            client.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Linux; Android 6.0; Nexus 5 Build/MRA58N) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/140.0.0.0 Mobile Safari/537.36");

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

            //Find all divs that have class 'product-card-container'
            var productNodes = doc.DocumentNode.SelectNodes("//div[contains(@class,'product-card-container')]");

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
            // 1. Extract Product URL from data-producturl attribute of the relevant child node
            var productUrlNode = node.SelectSingleNode(".//div[contains(@class, 'product-card') and @data-producturl]");
            var relativeUrl = productUrlNode?.GetAttributeValue("data-producturl", "");

            if (string.IsNullOrEmpty(relativeUrl))
            {
                _logger.LogWarning("Product URL not found in node. Node HTML: {NodeHtml}", node.InnerHtml);
                return null;
            }

            // Absolute Url
            var productUrl = relativeUrl.StartsWith("https")
                            ? relativeUrl
                            : $"{BASE_URL}{(relativeUrl.StartsWith("/") ? "" : "/")}{relativeUrl}";


            // 2. Extract raw Name, prioritizing 'alt' attribute of the main image
            var imgNodeForName = node.SelectSingleNode(".//img[@alt]");
            var rawName = imgNodeForName?.GetAttributeValue("alt", "").Trim();

            if (string.IsNullOrEmpty(rawName))
            {
                imgNodeForName = node.SelectSingleNode(".//img[@title]"); // Fallback to title if alt is empty
                rawName = imgNodeForName?.GetAttributeValue("title", "").Trim();
            }

            if (string.IsNullOrEmpty(rawName))
            {
                _logger.LogWarning("Product name not found in node. Node HTML: {NodeHtml}", node.InnerHtml);
                return null;
            }
            var name = rawName;

            // 3. Extract Brand from URL
            var brand = ParseBrandFromUrl(relativeUrl);

            // If brand was not found from URL, try to extract it from a specific node as a fallback
            if (string.IsNullOrEmpty(brand))
            {
                var brandNode = node.SelectSingleNode(".//div[contains(@class, 'bEdZhHjcOVrOqVKS1zCj')]");
                brand = brandNode?.InnerText?.Trim();
            }


            // 4. Extract Price
            var priceNode = node.SelectSingleNode(".//span[contains(@class, 'globalPrices-defaultPrice')]");
            var priceText = priceNode?.InnerText?.Trim();

            var price = ParsePrice(priceText);

            if (price == 0)
            {
                _logger.LogWarning("Invalid price for product: {Name}. Price Text: '{PriceText}'. Node HTML: {NodeHtml}", name, priceText, node.InnerHtml);
                return null;
            }

            // 5. Extract Image URL from the src attribute of the main image
            var imgNode = node.SelectSingleNode(".//img[contains(@class, 'lazyloaded') and @src]");
            var imgUrl = imgNode?.GetAttributeValue("src", "");

            // 6. Create Product object
            var product = new Product
            {
                Name = name,
                Brand = brand,
                Price = price,
                ProductUrl = productUrl,
                ImageUrl = imgUrl,
                RetailerName = RetailerName, // Use the class property
                LastUpdated = DateTime.UtcNow
            };

            return product;
        }
        catch (Exception e) { _logger.LogError(e, "Error extracting product from node. Node HTML: {NodeHtml}", node.InnerHtml); return null; }
    }

    /// <summary>
    /// Helper to parse brand from a product URL like '/brands/los-cabos/granada-sandal-los-granada'
    /// </summary>
    private string? ParseBrandFromUrl(string relativeUrl)
    {
        var segments = relativeUrl.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (segments.Length >= 2 && segments[0].Equals("brands", StringComparison.OrdinalIgnoreCase))
        {
            var brandSlug = segments[1];
            // Convert hyphen-separated slug to Title Case (e.g., "los-cabos" -> "Los Cabos")
            return System.Globalization.CultureInfo.CurrentCulture.TextInfo.ToTitleCase(brandSlug.Replace('-', ' '));
        }
        return null;
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
