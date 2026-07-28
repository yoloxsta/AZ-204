using Microsoft.Extensions.Logging;
using OrderFlow.Api.Models;

namespace OrderFlow.Api.Services;

public class OrderProcessingService
{
    private readonly ICosmosService _cosmosService;
    private readonly IServiceBusService _serviceBusService;
    private readonly IEventGridService _eventGridService;
    private readonly ICacheService _cacheService;
    private readonly ILogger<OrderProcessingService> _logger;

    private const string CacheKeyPrefix = "order:";

    public OrderProcessingService(
        ICosmosService cosmosService,
        IServiceBusService serviceBusService,
        IEventGridService eventGridService,
        ICacheService cacheService,
        ILogger<OrderProcessingService> logger)
    {
        _cosmosService = cosmosService;
        _serviceBusService = serviceBusService;
        _eventGridService = eventGridService;
        _cacheService = cacheService;
        _logger = logger;
    }

    public async Task<Order> CreateOrderAsync(CreateOrderRequest request)
    {
        var order = new Order
        {
            Id = Guid.NewGuid().ToString(),
            CustomerId = request.CustomerId,
            CustomerName = request.CustomerName,
            ShippingAddress = request.ShippingAddress,
            Items = request.Items.Select(i => new OrderItem
            {
                ProductId = i.ProductId,
                ProductName = i.ProductName,
                Quantity = i.Quantity,
                UnitPrice = i.UnitPrice
            }).ToList(),
            Status = OrderStatus.Pending,
            OrderDate = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            PartitionKey = request.CustomerId
        };

        order.TotalAmount = order.Items.Sum(i => i.Quantity * i.UnitPrice);

        var created = await _cosmosService.CreateItemAsync(order, order.CustomerId);

        await _serviceBusService.SendOrderToQueueAsync(created);
        await _eventGridService.PublishEventAsync("Order.Created", new { created.Id, created.CustomerName, created.TotalAmount });
        await _cacheService.SetAsync($"{CacheKeyPrefix}{created.Id}", created, TimeSpan.FromMinutes(15));

        _logger.LogInformation("Order {OrderId} created, total {Total}", created.Id, created.TotalAmount);
        return created;
    }

    public async Task<Order?> GetOrderAsync(string orderId, string customerId)
    {
        var cached = await _cacheService.GetAsync<Order>($"{CacheKeyPrefix}{orderId}");
        if (cached is not null) return cached;

        var order = await _cosmosService.GetItemAsync<Order>(orderId, customerId);
        if (order is not null)
            await _cacheService.SetAsync($"{CacheKeyPrefix}{orderId}", order, TimeSpan.FromMinutes(15));

        return order;
    }

    public async Task<Order> UpdateOrderStatusAsync(string orderId, string customerId, OrderStatus newStatus)
    {
        var order = await _cosmosService.GetItemAsync<Order>(orderId, customerId);
        if (order is null) throw new KeyNotFoundException($"Order {orderId} not found.");

        var previousStatus = order.Status;
        order.Status = newStatus;
        order.UpdatedAt = DateTime.UtcNow;

        var updated = await _cosmosService.UpdateItemAsync(orderId, order, customerId);
        await _serviceBusService.SendOrderEventAsync(updated, $"Order.{newStatus}");
        await _eventGridService.PublishOrderEventAsync(updated, previousStatus);
        await _cacheService.SetAsync($"{CacheKeyPrefix}{orderId}", updated, TimeSpan.FromMinutes(15));

        return updated;
    }

    public async Task CancelOrderAsync(string orderId, string customerId)
    {
        var order = await _cosmosService.GetItemAsync<Order>(orderId, customerId);
        if (order is null) throw new KeyNotFoundException($"Order {orderId} not found.");
        if (order.Status == OrderStatus.Shipped || order.Status == OrderStatus.Delivered)
            throw new InvalidOperationException($"Cannot cancel order in '{order.Status}' status.");

        var previousStatus = order.Status;
        order.Status = OrderStatus.Cancelled;
        order.UpdatedAt = DateTime.UtcNow;

        var updated = await _cosmosService.UpdateItemAsync(orderId, order, customerId);
        await _serviceBusService.SendOrderEventAsync(updated, "Order.Cancelled");
        await _eventGridService.PublishOrderEventAsync(updated, previousStatus);
        await _cacheService.SetAsync($"{CacheKeyPrefix}{orderId}", updated, TimeSpan.FromMinutes(15));
    }
}
