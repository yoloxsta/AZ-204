using Azure.Messaging.EventGrid;
using EventHandlers.FunctionApp.Models;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace EventHandlers.FunctionApp.Functions;

public class OrderCreatedHandler
{
    private readonly ILogger<OrderCreatedHandler> _logger;

    public OrderCreatedHandler(ILogger<OrderCreatedHandler> logger)
    {
        _logger = logger;
    }

    [Function(nameof(OrderCreatedHandler))]
    public void Run(
        [EventGridTrigger] EventGridEvent eventGridEvent)
    {
        _logger.LogInformation("=== OrderCreated Event Received ===");
        _logger.LogInformation("Event Type: {EventType}", eventGridEvent.EventType);
        _logger.LogInformation("Subject: {Subject}", eventGridEvent.Subject);
        _logger.LogInformation("Event Time: {EventTime}", eventGridEvent.EventTime);
        
        try
        {
            // Deserialize the order data
            var order = eventGridEvent.Data.ToObjectFromJson<OrderEvent>();
            
            _logger.LogInformation("Order ID: {OrderId}", order.OrderId);
            _logger.LogInformation("Customer: {CustomerId} ({Email})", order.CustomerId, order.CustomerEmail);
            _logger.LogInformation("Total: {Total:C}", order.Total);
            _logger.LogInformation("Status: {Status}", order.Status);
            
            // Simulate processing - e.g., send email, update inventory, etc.
            ProcessOrder(order);
            
            _logger.LogInformation("Order processing completed successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing OrderCreated event");
            throw; // Let Event Grid retry
        }
    }
    
    private void ProcessOrder(OrderEvent order)
    {
        // Simulate different processing based on order total
        if (order.Total > 1000)
        {
            _logger.LogWarning("High-value order detected! OrderId: {OrderId}, Total: {Total}", 
                order.OrderId, order.Total);
            // Could trigger additional validation or approval workflow
        }
        
        // Simulate email notification
        _logger.LogInformation("Sending order confirmation email to {Email}", order.CustomerEmail);
        
        // Simulate inventory check
        _logger.LogInformation("Checking inventory for order {OrderId}", order.OrderId);
    }
}
