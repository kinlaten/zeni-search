using Microsoft.AspNetCore.Mvc;
using ZeniSearch.Api.Models;
using ZeniSearch.Api.Data;
using ZeniSearch.Api.Services;
using Microsoft.EntityFrameworkCore;
using System.Security.Cryptography.X509Certificates;

namespace ZeniSearch.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ProductsController : ControllerBase
{
    private readonly AppDbContext _context;
    private readonly CachedProductService _cachedProductService;
    private readonly ILogger<ProductsController> _logger;

    //Constructor
    public ProductsController(
        AppDbContext context,
        CachedProductService cachedProductService,
        ILogger<ProductsController> logger)
    {
        _context = context;
        _cachedProductService = cachedProductService;
        _logger = logger;
    }

    //GET: api/products
    [HttpGet]
    public async Task<ActionResult<IEnumerable<Product>>> GetProducts()
    {
        try
        {
            var products = await _context.Product.ToListAsync();
            return Ok(products);
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Error retrieving products");
            return StatusCode(500, "An error occured while retrieving products");
        }
    }

    //Get: api/products/[id]
    [HttpGet("{id}")]
    public async Task<ActionResult<Product>> GetProduct(int id)
    {
        try
        {
            var product = await _context.Product.FindAsync(id);
            return Ok(product);
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Error retrieving product {ProductId}", id);
            return StatusCode(500, "An error occurred");
        }
    }

    //GET: api/products/search?q=sandals
    [HttpGet("search")]
    public async Task<ActionResult<IEnumerable<Product>>> SearchProducts([FromQuery] string q)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(q))
            {
                return BadRequest(new { message = "Search query cannot be empty" });
            }

            // Use cached search service for better performance
            var products = await _cachedProductService.SearchProducts(q);

            return Ok(new { products, totalCount = products.Count, query = q });
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Error searching products with query: {Query}", q);
            return StatusCode(500, "An error occured while searching");
        }
    }

    //POST: api/products 
    [HttpPost]
    public async Task<ActionResult<Product>> CreateProduct([FromBody] Product product)

    {
        try
        {
            // ModelState track: Binding status and Validation Status
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            // Update property 
            product.LastUpdated = DateTime.UtcNow;

            _context.Product.Add(product);
            await _context.SaveChangesAsync();

            //Create HTTP201 response
            return CreatedAtAction(nameof(GetProduct), new { id = product.Id }, product);
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Error creating product");
            return StatusCode(500, "An error  occured while creating the product");
        }
    }
}
