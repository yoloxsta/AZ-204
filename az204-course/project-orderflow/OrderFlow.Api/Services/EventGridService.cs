using Azure.Messaging.EventGrid;
using Microsoft.Extensions.Logging;
using OrderFlow.Api.Models;

namespace OrderFlow.Api.Services;

public class EventGridService : IEventGridService
{
    private readonly EventGridPublisherClient _client;
    private readonly ILogger<EventGridService> _logger;

    public EventGridService(EventGridPublisherClient client, ILogger<EventGridService> logger)
    {
        _client = client ?? throw new ArgumentNullException(nameof(client));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task PublishEventAsync<T>(string eventType, T data)
    {
        try
        {
            var eventGridEvent = new EventGridEvent(
                subject: $"/events/{eventType}",
                eventType: eventType,
                dataVersion: "1.0",
                data: BinaryData.FromObjectAsJson(data));

            await _client.SendEventAsync(eventGridEvent);
            _logger.LogInformation("Event {EventType} published to Event Grid", eventType);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to publish event {EventType} to Event Grid", eventType);
            throw;
        }
    }

    public async Task PublishOrderEventAsync(Order order, OrderStatus previousStatus)
    {
        try
        {
            var eventData = new
            {
                order.Id,
                order.CustomerName,
                order.TotalAmount,
                PreviousStatus = previousStatus.ToString(),
                NewStatus = order.Status.ToString(),
                Timestamp = DateTime.UtcNow
            };

            var eventGridEvent = new EventGridEvent(
                subject: $"/orders/{order.Id}/statuschanged",
                eventType: "Order.StatusChanged",
                dataVersion: "1.0",
                data: BinaryData.FromObjectAsJson(eventData));

            await _client.SendEventAsync(eventGridEvent);
            _logger.LogInformation(
                "Order status change event published for order {OrderId}: {PreviousStatus} -> {NewStatus}",
                order.Id, previousStatus, order.Status);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to publish order event for order {OrderId}", order.Id);
            throw;
        }
    }
}
