using Microsoft.AspNetCore.Mvc;
using OrderFlow.Api.Models;
using OrderFlow.Api.Services;

namespace OrderFlow.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ProductsController : ControllerBase
{
    private readonly ICosmosService _cosmosService;
    private readonly ICacheService _cacheService;
    private readonly IImageService _imageService;
    private readonly ILogger<ProductsController> _logger;

    private const string ProductCachePrefix = "product:";
    private const string ProductListCacheKey = "products:all";

    public ProductsController(
        ICosmosService cosmosService,
        ICacheService cacheService,
        IImageService imageService,
        ILogger<ProductsController> logger)
    {
        _cosmosService = cosmosService;
        _cacheService = cacheService;
        _imageService = imageService;
        _logger = logger;
    }

    [HttpGet]
    [ProducesResponseType(typeof(List<Product>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAllProducts()
    {
        var cached = await _cacheService.GetAsync<List<Product>>(ProductListCacheKey);
        if (cached is not null) return Ok(cached);

        var products = await _cosmosService.GetItemsAsync<Product>("products");
        await _cacheService.SetAsync(ProductListCacheKey, products, TimeSpan.FromMinutes(5));
        return Ok(products);
    }

    [HttpGet("{id}")]
    [ProducesResponseType(typeof(Product), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetProduct(string id)
    {
        var cacheKey = $"{ProductCachePrefix}{id}";
        var cached = await _cacheService.GetAsync<Product>(cacheKey);
        if (cached is not null) return Ok(cached);

        var product = await _cosmosService.GetItemAsync<Product>(id, "products");
        if (product is null) return NotFound(new { error = $"Product '{id}' not found." });

        await _cacheService.SetAsync(cacheKey, product, TimeSpan.FromMinutes(10));
        return Ok(product);
    }

    [HttpPost]
    [ProducesResponseType(typeof(Product), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CreateProduct([FromBody] CreateProductRequest request)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        var product = new Product
        {
            Id = Guid.NewGuid().ToString(),
            Name = request.Name,
            Description = request.Description,
            Price = request.Price,
            Category = request.Category,
            StockQuantity = request.StockQuantity,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };

        var created = await _cosmosService.CreateItemAsync(product, "products");
        await _cacheService.RemoveAsync(ProductListCacheKey);
        return CreatedAtAction(nameof(GetProduct), new { id = created.Id }, created);
    }

    [HttpPost("{id}/image")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> UploadProductImage(string id, IFormFile image)
    {
        if (image is null || image.Length == 0)
            return BadRequest(new { error = "No image file provided." });

        var imageUrl = await _imageService.UploadProductImageAsync(id, image.OpenReadStream(), image.ContentType);
        return Ok(new { productId = id, imageUrl, uploadedAt = DateTime.UtcNow });
    }

    [HttpDelete("{id}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteProduct(string id)
    {
        try
        {
            await _cosmosService.DeleteItemAsync(id, "products");
            await _cacheService.RemoveAsync($"{ProductCachePrefix}{id}");
            await _cacheService.RemoveAsync(ProductListCacheKey);
            return NoContent();
        }
        catch (KeyNotFoundException)
        {
            return NotFound(new { error = $"Product '{id}' not found." });
        }
    }
}
