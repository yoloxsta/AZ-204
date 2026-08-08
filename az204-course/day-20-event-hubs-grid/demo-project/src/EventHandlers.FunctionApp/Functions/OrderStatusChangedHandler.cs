using Azure.Messaging.EventGrid;
using EventHandlers.FunctionApp.Models;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace EventHandlers.FunctionApp.Functions;

public class OrderStatusChangedHandler
{
    private readonly ILogger<OrderStatusChangedHandler> _logger;

    public OrderStatusChangedHandler(ILogger<OrderStatusChangedHandler> logger)
    {
        _logger = logger;
    }

    [Function(nameof(OrderStatusChangedHandler))]
    public void Run(
        [EventGridTrigger] EventGridEvent eventGridEvent)
    {
        _logger.LogInformation("=== OrderStatusChanged Event ===");
        
        var statusEvent = eventGridEvent.Data.ToObjectFromJson<OrderStatusChangedEvent>();
        
        _logger.LogInformation("Order {OrderId}: {PreviousStatus} -> {NewStatus}",
            statusEvent.OrderId, statusEvent.PreviousStatus, statusEvent.NewStatus);
        
        // Handle status-specific logic
        switch (statusEvent.NewStatus)
        {
            case "Shipped":
                HandleShippedOrder(statusEvent);
                break;
            case "Delivered":
                HandleDeliveredOrder(statusEvent);
                break;
            case "Cancelled":
                HandleCancelledOrder(statusEvent);
                break;
            default:
                _logger.LogInformation("Status change to {Status}", statusEvent.NewStatus);
                break;
        }
    }
    
    private void HandleShippedOrder(OrderStatusChangedEvent statusEvent)
    {
        _logger.LogInformation("Sending shipping notification to customer {CustomerId}", 
            statusEvent.CustomerId);
        // Send SMS or push notification
    }
    
    private void HandleDeliveredOrder(OrderStatusChangedEvent statusEvent)
    {
        _logger.LogInformation("Order {OrderId} delivered. Requesting feedback.", 
            statusEvent.OrderId);
        // Send feedback request email
    }
    
    private void HandleCancelledOrder(OrderStatusChangedEvent statusEvent)
    {
        _logger.LogWarning("Order {OrderId} cancelled. Initiating refund if applicable.", 
            statusEvent.OrderId);
        // Process refund if payment was taken
    }
}
