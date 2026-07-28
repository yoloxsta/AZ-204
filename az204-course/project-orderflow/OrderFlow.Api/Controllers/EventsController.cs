using Microsoft.AspNetCore.Mvc;
using OrderFlow.Api.Models;
using OrderFlow.Api.Services;

namespace OrderFlow.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class EventsController : ControllerBase
{
    private readonly IEventGridService _eventGridService;
    private readonly ICosmosService _cosmosService;
    private readonly ILogger<EventsController> _logger;

    public EventsController(
        IEventGridService eventGridService,
        ICosmosService cosmosService,
        ILogger<EventsController> logger)
    {
        _eventGridService = eventGridService ?? throw new ArgumentNullException(nameof(eventGridService));
        _cosmosService = cosmosService ?? throw new ArgumentNullException(nameof(cosmosService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    [HttpPost("publish")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> PublishEvent([FromBody] PublishEventRequest request)
    {
        try
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            if (string.IsNullOrWhiteSpace(request.EventType))
                return BadRequest(new { error = "Event type is required." });

            _logger.LogInformation("Publishing event of type '{EventType}'", request.EventType);

            await _eventGridService.PublishEventAsync(request.EventType, request.Data);

            _logger.LogInformation("Event of type '{EventType}' published successfully", request.EventType);

            return Ok(new
            {
                message = "Event published successfully.",
                eventType = request.EventType,
                publishedAt = DateTime.UtcNow
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error publishing event of type '{EventType}'", request.EventType);
            return StatusCode(StatusCodes.Status500InternalServerError, new { error = "An error occurred while publishing the event." });
        }
    }

    [HttpPost("publish-order")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> PublishOrderEvent([FromBody] PublishOrderEventRequest request)
    {
        try
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            if (string.IsNullOrWhiteSpace(request.OrderId))
                return BadRequest(new { error = "Order ID is required." });

            if (string.IsNullOrWhiteSpace(request.CustomerId))
                return BadRequest(new { error = "Customer ID is required." });

            _logger.LogInformation("Publishing order event for order {OrderId}", request.OrderId);

            var order = await _cosmosService.GetItemAsync<Order>(request.OrderId, request.CustomerId)
                ?? throw new KeyNotFoundException($"Order '{request.OrderId}' not found.");

            var previousStatus = order.Status;
            order.Status = request.NewStatus;
            order.UpdatedAt = DateTime.UtcNow;

            await _cosmosService.UpdateItemAsync(request.OrderId, order, request.CustomerId);
            await _eventGridService.PublishOrderEventAsync(order, previousStatus);

            _logger.LogInformation("Order event published for order {OrderId}, status changed from {PreviousStatus} to {NewStatus}", request.OrderId, previousStatus, request.NewStatus);

            return Ok(new
            {
                message = "Order event published successfully.",
                orderId = request.OrderId,
                previousStatus = previousStatus.ToString(),
                newStatus = request.NewStatus.ToString(),
                publishedAt = DateTime.UtcNow
            });
        }
        catch (KeyNotFoundException ex)
        {
            _logger.LogWarning(ex, "Order {OrderId} not found for event publishing", request.OrderId);
            return NotFound(new { error = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error publishing order event for order {OrderId}", request.OrderId);
            return StatusCode(StatusCodes.Status500InternalServerError, new { error = "An error occurred while publishing the order event." });
        }
    }

    [HttpGet("health")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> HealthCheck()
    {
        try
        {
            _logger.LogInformation("Performing Event Grid health check");

            await _eventGridService.PublishEventAsync("HealthCheck.Test", new
            {
                message = "Health check ping",
                timestamp = DateTime.UtcNow
            });

            return Ok(new
            {
                status = "Healthy",
                service = "EventGrid",
                checkedAt = DateTime.UtcNow
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Event Grid health check failed");
            return StatusCode(StatusCodes.Status500InternalServerError, new
            {
                status = "Unhealthy",
                service = "EventGrid",
                error = ex.Message,
                checkedAt = DateTime.UtcNow
            });
        }
    }
}

public class PublishEventRequest
{
    public string EventType { get; set; } = string.Empty;
    public object? Data { get; set; }
}

public class PublishOrderEventRequest
{
    public string OrderId { get; set; } = string.Empty;
    public string CustomerId { get; set; } = string.Empty;
    public OrderStatus NewStatus { get; set; }
}
