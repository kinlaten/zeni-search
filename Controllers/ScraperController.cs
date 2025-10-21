using System.Security.Cryptography.X509Certificates;
using Microsoft.AspNetCore.Mvc;
using ZeniSearch.Api.Services;

namespace ZeniSearch.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ScraperController : ControllerBase
{
    private readonly TheIconicScraper _scraper;
    private readonly ILogger<ScraperController> _logger;

    //Contructor: Inject scraper service
    public ScraperController(
        TheIconicScraper scraper,
        ILogger<ScraperController> logger
    )
    {
        _scraper = scraper;
        _logger = logger;
    }

    // POST: /api/scraper/run (for testing)
    [HttpPost("run")]
    public async Task<ActionResult> RunScraper(
        [FromQuery] string searchTerm,
        [FromQuery] int maxProducts = 50
    )
    {
        try
        {
            // Validate input 
            if (string.IsNullOrWhiteSpace(searchTerm))
            {
                return BadRequest(new { message = "Search term is required" });
            }

            if (maxProducts < 1 || maxProducts > 100)
            {
                return BadRequest(new { message = "maxProducts must be in range 1 to 100" });
            }

            _logger.LogInformation(
                "Scraper trigger for term: {SearchTerm}, max: {MaxProducts}",
                searchTerm,
                maxProducts
            );

            // Runner
            var startTime = DateTime.UtcNow;
            var productsScraped = await _scraper.ScapeProducts(searchTerm, maxProducts);
            var duration = (DateTime.UtcNow - startTime).TotalSeconds;

            return Ok(new
            {
                success = true,
                searchTerm = searchTerm,
                productsScraped = productsScraped,
                durationSeconds = Math.Round(duration, 2),
                message = $"Successfully scraped {productsScraped} products"
            });
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Error running scraper");
            return StatusCode(500, new
            {
                success = false,
                message = "An error occured while scraping",
            });
        }
    }


    // Health check 
    [HttpGet("status")]
    public ActionResult GetStatus()
    {
        return Ok(new
        {
            status = "operational",
            message = "Scraper service is ready",
            timestamp = DateTime.UtcNow
        });
    }

}