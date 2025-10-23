using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ZeniSearch.Api.Services.Scrapers;
using ZeniSearch.Api.Services;

namespace ZeniSearch.Api.Controllers;


[ApiController]
[Route("api/[controller]")]
// [Authorize(Roles = "Admin")] //Only admin can manually trigger
public class ScraperController : ControllerBase
{
    private readonly ScraperService _scraperService;
    private readonly ILogger<ScraperController> _logger;

    //Contructor: Inject scraper service
    public ScraperController(
        ScraperService scraperService,
        ILogger<ScraperController> logger
    )
    {
        _scraperService = scraperService;
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

            // maxProducts is now handled by ScraperService

            _logger.LogInformation(
                "Scraper trigger for term: {SearchTerm}",
                searchTerm
            );

            // Runner
            var startTime = DateTime.UtcNow;
            await _scraperService.ScrapeAllRetailers(searchTerm);
            var duration = (DateTime.UtcNow - startTime).TotalSeconds;

            return Ok(new
            {
                success = true,
                searchTerm = searchTerm,
                durationSeconds = Math.Round(duration, 2),
                message = $"Successfully scraped '{searchTerm}' products from multi-retailer."
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