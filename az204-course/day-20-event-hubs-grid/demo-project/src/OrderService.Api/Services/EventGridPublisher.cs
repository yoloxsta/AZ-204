using Azure;
using Azure.Messaging.EventGrid;
using OrderService.Api.Models;

namespace OrderService.Api.Services;

public interface IEventGridPublisher
{
    Task PublishOrderCreatedEventAsync(Order order);
    Task PublishOrderStatusChangedEventAsync(Order order, string previousStatus);
}

public class EventGridPublisher : IEventGridPublisher
{
    private readonly EventGridPublisherClient _client;
    private readonly ILogger<EventGridPublisher> _logger;

    public EventGridPublisher(
        IConfiguration configuration,
        ILogger<EventGridPublisher> logger)
    {
        var topicEndpoint = configuration["EventGrid:TopicEndpoint"];
        var topicKey = configuration["EventGrid:TopicKey"];
        
        _client = new EventGridPublisherClient(
            new Uri(topicEndpoint!),
            new AzureKeyCredential(topicKey!));
        
        _logger = logger;
    }

    public async Task PublishOrderCreatedEventAsync(Order order)
    {
        var eventGridEvent = new EventGridEvent(
            subject: $"orders/{order.Id}",
            eventType: "OrderCreated",
            dataVersion: "1.0",
            data: BinaryData.FromObjectAsJson(order)
        );
        
        eventGridEvent.Id = Guid.NewGuid().ToString();
        
        await _client.SendEventAsync(eventGridEvent);
        
        _logger.LogInformation(
            "Published OrderCreated event for order {OrderId}", 
            order.Id);
    }

    public async Task PublishOrderStatusChangedEventAsync(Order order, string previousStatus)
    {
        var eventData = new
        {
            OrderId = order.Id,
            PreviousStatus = previousStatus,
            NewStatus = order.Status,
            Timestamp = DateTime.UtcNow,
            CustomerId = order.CustomerId
        };
        
        var eventGridEvent = new EventGridEvent(
            subject: $"orders/{order.Id}/status",
            eventType: "OrderStatusChanged",
            dataVersion: "1.0",
            data: BinaryData.FromObjectAsJson(eventData)
        );
        
        await _client.SendEventAsync(eventGridEvent);
        
        _logger.LogInformation(
            "Published OrderStatusChanged event for order {OrderId}: {Previous} -> {New}",
            order.Id, previousStatus, order.Status);
    }
}
