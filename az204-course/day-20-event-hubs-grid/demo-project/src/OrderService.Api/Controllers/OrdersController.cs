using Microsoft.AspNetCore.Mvc;
using OrderService.Api.Models;
using OrderService.Api.Services;

namespace OrderService.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class OrdersController : ControllerBase
{
    private readonly IEventGridPublisher _eventPublisher;
    private readonly ILogger<OrdersController> _logger;
    
    // In-memory store for demo purposes
    private static readonly List<Order> _orders = new();

    public OrdersController(
        IEventGridPublisher eventPublisher,
        ILogger<OrdersController> logger)
    {
        _eventPublisher = eventPublisher;
        _logger = logger;
    }

    [HttpPost]
    public async Task<ActionResult<Order>> CreateOrder([FromBody] CreateOrderRequest request)
    {
        var order = new Order
        {
            CustomerId = request.CustomerId,
            CustomerEmail = request.CustomerEmail,
            Items = request.Items,
            Status = "Created"
        };
        
        order.Total = order.CalculateTotal();
        
        _orders.Add(order);
        
        // Publish OrderCreated event to Event Grid
        await _eventPublisher.PublishOrderCreatedEventAsync(order);
        
        return CreatedAtAction(
            nameof(GetOrder), 
            new { id = order.Id }, 
            order);
    }

    [HttpGet("{id}")]
    public ActionResult<Order> GetOrder(string id)
    {
        var order = _orders.FirstOrDefault(o => o.Id == id);
        if (order == null)
        {
            return NotFound();
        }
        return Ok(order);
    }

    [HttpGet]
    public ActionResult<IEnumerable<Order>> GetOrders()
    {
        return Ok(_orders);
    }

    [HttpPut("{id}/status")]
    public async Task<ActionResult<Order>> UpdateStatus(string id, [FromBody] UpdateStatusRequest request)
    {
        var order = _orders.FirstOrDefault(o => o.Id == id);
        if (order == null)
        {
            return NotFound();
        }
        
        var previousStatus = order.Status;
        order.Status = request.NewStatus;
        
        // Publish status changed event
        await _eventPublisher.PublishOrderStatusChangedEventAsync(order, previousStatus);
        
        return Ok(order);
    }
}

public record CreateOrderRequest(
    string CustomerId,
    string CustomerEmail,
    List<OrderItem> Items);

public record UpdateStatusRequest(string NewStatus);
