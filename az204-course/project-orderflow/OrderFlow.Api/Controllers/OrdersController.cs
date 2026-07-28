using Microsoft.AspNetCore.Mvc;
using OrderFlow.Api.Models;
using OrderFlow.Api.Services;

namespace OrderFlow.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class OrdersController : ControllerBase
{
    private readonly OrderProcessingService _orderProcessingService;
    private readonly ICosmosService _cosmosService;
    private readonly ILogger<OrdersController> _logger;

    public OrdersController(
        OrderProcessingService orderProcessingService,
        ICosmosService cosmosService,
        ILogger<OrdersController> logger)
    {
        _orderProcessingService = orderProcessingService;
        _cosmosService = cosmosService;
        _logger = logger;
    }

    [HttpPost]
    [ProducesResponseType(typeof(Order), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CreateOrder([FromBody] CreateOrderRequest request)
    {
        try
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);

            var order = await _orderProcessingService.CreateOrderAsync(request);
            return CreatedAtAction(nameof(GetOrder), new { id = order.Id, customerId = order.CustomerId }, order);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpGet]
    [ProducesResponseType(typeof(List<Order>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAllOrders([FromQuery] string customerId)
    {
        if (string.IsNullOrWhiteSpace(customerId))
            return BadRequest(new { error = "Query parameter 'customerId' is required." });

        var orders = await _cosmosService.GetItemsAsync<Order>(customerId);
        return Ok(orders);
    }

    [HttpGet("{id}")]
    [ProducesResponseType(typeof(Order), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetOrder(string id, [FromQuery] string customerId)
    {
        if (string.IsNullOrWhiteSpace(customerId))
            return BadRequest(new { error = "Query parameter 'customerId' is required." });

        var order = await _orderProcessingService.GetOrderAsync(id, customerId);
        if (order is null) return NotFound(new { error = $"Order '{id}' not found." });
        return Ok(order);
    }

    [HttpPut("{id}/status")]
    [ProducesResponseType(typeof(Order), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateOrderStatus(string id, [FromBody] UpdateOrderStatusRequest request)
    {
        try
        {
            var order = await _orderProcessingService.UpdateOrderStatusAsync(id, request.CustomerId, request.Status);
            return Ok(order);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { error = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpDelete("{id}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> CancelOrder(string id, [FromQuery] string customerId)
    {
        if (string.IsNullOrWhiteSpace(customerId))
            return BadRequest(new { error = "Query parameter 'customerId' is required." });

        try
        {
            await _orderProcessingService.CancelOrderAsync(id, customerId);
            return NoContent();
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { error = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpGet("health")]
    public async Task<IActionResult> HealthCheck()
    {
        var count = await _cosmosService.GetOrderCountAsync();
        return Ok(new { status = "Healthy", orderCount = count, checkedAt = DateTime.UtcNow });
    }
}

public class UpdateOrderStatusRequest
{
    public string CustomerId { get; set; } = string.Empty;
    public OrderStatus Status { get; set; }
}
