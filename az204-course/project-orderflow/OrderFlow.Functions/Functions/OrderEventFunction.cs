using Azure.Messaging.EventGrid;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace OrderFlow.Functions.Functions;

public class OrderEventFunction
{
    private readonly ILogger<OrderEventFunction> _logger;

    public OrderEventFunction(ILogger<OrderEventFunction> logger)
    {
        _logger = logger;
    }

    [Function(nameof(OrderEventFunction))]
    public async Task Run(
        [EventGridTrigger] EventGridEvent eventGridEvent)
    {
        _logger.LogInformation(
            "Received EventGrid event. Id: {Id}, Subject: {Subject}, EventType: {EventType}, EventTime: {EventTime}",
            eventGridEvent.Id,
            eventGridEvent.Subject,
            eventGridEvent.EventType,
            eventGridEvent.EventTime);

        OrderStatusEvent? orderEvent;
        try
        {
            orderEvent = eventGridEvent.Data.ToObjectFromJson<OrderStatusEvent>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to deserialize EventGrid event data.");
            return;
        }

        if (orderEvent is not null)
        {
            _logger.LogInformation(
                "Order status change: OrderId={OrderId}, CustomerId={CustomerId}, OldStatus={OldStatus}, NewStatus={NewStatus}",
                orderEvent.OrderId,
                orderEvent.CustomerId,
                orderEvent.OldStatus,
                orderEvent.NewStatus);

            if (orderEvent.NewStatus == "Completed")
            {
                _logger.LogInformation("Order {OrderId} completed. Sending notification email to customer {CustomerId}.",
                    orderEvent.OrderId, orderEvent.CustomerId);

                // TODO: Integrate with Azure Communication Services or SendGrid
                // await _emailService.SendOrderCompletedEmail(orderEvent.CustomerId, orderEvent.OrderId);
            }
            else if (orderEvent.NewStatus == "Cancelled")
            {
                _logger.LogInformation("Order {OrderId} cancelled. Notifying customer {CustomerId}.",
                    orderEvent.OrderId, orderEvent.CustomerId);
            }
        }

        await Task.CompletedTask;
    }
}

public class OrderStatusEvent
{
    public string OrderId { get; set; } = string.Empty;
    public string CustomerId { get; set; } = string.Empty;
    public string OldStatus { get; set; } = string.Empty;
    public string NewStatus { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; }
}
