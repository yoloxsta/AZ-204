using OrderFlow.Api.Models;

namespace OrderFlow.Api.Services;

public interface IServiceBusService
{
    Task SendMessageAsync(string queueName, string messageBody, Dictionary<string, object>? properties = null);
    Task SendOrderToQueueAsync(Order order);
    Task<string?> ReceiveMessageAsync(string queueName);
    Task SendTopicMessageAsync(string topicName, string messageBody, Dictionary<string, object>? properties = null);
    Task SendOrderEventAsync(Order order, string eventType);
}
