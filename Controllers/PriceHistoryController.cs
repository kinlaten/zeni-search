using Microsoft.AspNetCore.Mvc;
using ZeniSearch.Api.Services;

namespace ZeniSearch.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class PriceHistoryController : ControllerBase
{
    private readonly PriceHistoryService _priceHistoryService;
    private readonly ILogger<PriceHistoryController> _logger;

    public PriceHistoryController(
        PriceHistoryService priceHistoryService,
        ILogger<PriceHistoryController> logger
    )
    {
        _priceHistoryService = priceHistoryService;
        _logger = logger;
    }

    // Get price history for a product
    // GET /api/pricehistory/5
    [HttpGet("{productId}")]
    public async Task<ActionResult> GetPriceHistory(
        int productId,
        [FromQuery] DateTime? startDate = null,
        [FromQuery] DateTime? endDate = null
    )
    {
        try
        {
            var history = await _priceHistoryService.GetPriceHistory(productId, startDate, endDate);

            var lowest = await _priceHistoryService.GetLowestPrice(productId);
            var highest = await _priceHistoryService.GetHighestPrice(productId);
            var average = await _priceHistoryService.GetAveragePrice(productId);

            return Ok(new
            {
                productId,
                history,
                statistics = new
                {
                    lowest,
                    highest,
                    average,
                    recordCount = history.Count
                }
            });
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Error getting price history for product {ProductId}", productId);
            return StatusCode(500, "Error retrieving price history");
        }
    }

    [HttpGet("drops")]
    public async Task<ActionResult> GetPriceDrops(
        [FromQuery] decimal threshholdPercentage = 10,
        [FromQuery] int daysBack = 7
    )
    {
        try
        {
            var products = await _priceHistoryService.GetProductsWithPriceDrops(threshholdPercentage, daysBack);
            return Ok(new
            {
                threshholdPercentage,
                daysBack,
                count = products.Count,
                products
            });
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Error getting price drops");
            return StatusCode(500, "Error retrieving price drops");
        }
    }


}