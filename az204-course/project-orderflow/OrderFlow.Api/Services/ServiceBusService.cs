using System.Text.Json;
using Azure.Messaging.ServiceBus;
using Microsoft.Extensions.Logging;
using OrderFlow.Api.Models;

namespace OrderFlow.Api.Services;

public class ServiceBusService : IServiceBusService
{
    private readonly ServiceBusClient _client;
    private readonly ILogger<ServiceBusService> _logger;

    public ServiceBusService(ServiceBusClient client, ILogger<ServiceBusService> logger)
    {
        _client = client ?? throw new ArgumentNullException(nameof(client));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task SendMessageAsync(string queueName, string messageBody, Dictionary<string, object>? properties = null)
    {
        var sender = _client.CreateSender(queueName);
        try
        {
            var message = new ServiceBusMessage(messageBody);

            if (properties is not null)
            {
                foreach (var prop in properties)
                {
                    message.ApplicationProperties[prop.Key] = prop.Value;
                }
            }

            await sender.SendMessageAsync(message);
            _logger.LogInformation("Message sent to queue {QueueName}", queueName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send message to queue {QueueName}", queueName);
            throw;
        }
        finally
        {
            await sender.DisposeAsync();
        }
    }

    public async Task SendOrderToQueueAsync(Order order)
    {
        var sender = _client.CreateSender("order-queue");
        try
        {
            var json = JsonSerializer.Serialize(order);
            var message = new ServiceBusMessage(json)
            {
                ContentType = "application/json",
                Subject = "NewOrder"
            };
            message.ApplicationProperties["OrderId"] = order.Id;
            message.ApplicationProperties["Status"] = order.Status.ToString();

            await sender.SendMessageAsync(message);
            _logger.LogInformation("Order {OrderId} sent to order queue", order.Id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send order {OrderId} to queue", order.Id);
            throw;
        }
        finally
        {
            await sender.DisposeAsync();
        }
    }

    public async Task<string?> ReceiveMessageAsync(string queueName)
    {
        var receiver = _client.CreateReceiver(queueName);
        try
        {
            var message = await receiver.ReceiveMessageAsync(TimeSpan.FromSeconds(5));
            if (message is null)
            {
                _logger.LogDebug("No messages available on queue {QueueName}", queueName);
                return null;
            }

            var body = message.Body.ToString();
            await receiver.CompleteMessageAsync(message);
            _logger.LogInformation("Message received and completed from queue {QueueName}", queueName);
            return body;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to receive message from queue {QueueName}", queueName);
            throw;
        }
        finally
        {
            await receiver.DisposeAsync();
        }
    }

    public async Task SendTopicMessageAsync(string topicName, string messageBody, Dictionary<string, object>? properties = null)
    {
        var sender = _client.CreateSender(topicName);
        try
        {
            var message = new ServiceBusMessage(messageBody);

            if (properties is not null)
            {
                foreach (var prop in properties)
                {
                    message.ApplicationProperties[prop.Key] = prop.Value;
                }
            }

            await sender.SendMessageAsync(message);
            _logger.LogInformation("Message sent to topic {TopicName}", topicName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send message to topic {TopicName}", topicName);
            throw;
        }
        finally
        {
            await sender.DisposeAsync();
        }
    }

    public async Task SendOrderEventAsync(Order order, string eventType)
    {
        var sender = _client.CreateSender("order-events");
        try
        {
            var message = new ServiceBusMessage(JsonSerializer.Serialize(order))
            {
                ContentType = "application/json",
                Subject = eventType
            };
            message.ApplicationProperties["OrderId"] = order.Id;
            message.ApplicationProperties["Status"] = order.Status.ToString();
            message.ApplicationProperties["EventType"] = eventType;
            message.ApplicationProperties["Timestamp"] = DateTime.UtcNow;

            await sender.SendMessageAsync(message);
            _logger.LogInformation("Order event {EventType} sent for order {OrderId}", eventType, order.Id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send order event {EventType} for order {OrderId}", eventType, order.Id);
            throw;
        }
        finally
        {
            await sender.DisposeAsync();
        }
    }
}
