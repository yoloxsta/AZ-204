using OrderFlow.Api.Models;

namespace OrderFlow.Api.Services;

public interface IEventGridService
{
    Task PublishEventAsync<T>(string eventType, T data);
    Task PublishOrderEventAsync(Order order, OrderStatus previousStatus);
}
