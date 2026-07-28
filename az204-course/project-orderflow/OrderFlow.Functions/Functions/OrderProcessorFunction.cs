using System.Text.Json;
using Microsoft.Azure.Cosmos;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace OrderFlow.Functions.Functions;

public class OrderProcessorFunction
{
    private readonly CosmosClient _cosmosClient;
    private readonly ILogger<OrderProcessorFunction> _logger;
    private readonly Container _orderContainer;

    public OrderProcessorFunction(CosmosClient cosmosClient, ILogger<OrderProcessorFunction> logger)
    {
        _cosmosClient = cosmosClient;
        _logger = logger;
        _orderContainer = _cosmosClient.GetContainer("OrderFlowDb", "Orders");
    }

    [Function(nameof(OrderProcessorFunction))]
    public async Task Run(
        [ServiceBusTrigger("order-queue", Connection = "ServiceBus:ConnectionString")]
        string message)
    {
        _logger.LogInformation("OrderProcessorFunction received message: {Message}", message);

        OrderMessage? orderMessage;
        try
        {
            orderMessage = JsonSerializer.Deserialize<OrderMessage>(message, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Failed to deserialize order message.");
            throw;
        }

        if (orderMessage is null || string.IsNullOrEmpty(orderMessage.OrderId))
        {
            _logger.LogWarning("Invalid order message received. Skipping.");
            return;
        }

        _logger.LogInformation("Processing order {OrderId} for customer {CustomerId}.",
            orderMessage.OrderId, orderMessage.CustomerId);

        try
        {
            _logger.LogInformation("Updating order {OrderId} status from Pending to Processing.", orderMessage.OrderId);

            var patchOperation = PatchOperation.Replace("/status", "Processing");
            var response = await _orderContainer.PatchItemAsync<OrderRecord>(
                id: orderMessage.OrderId,
                partitionKey: new PartitionKey(orderMessage.CustomerId),
                patchOperations: [patchOperation]);

            _logger.LogInformation("Order {OrderId} updated to Processing. Cost: {Charge} RUs.",
                orderMessage.OrderId, response.RequestCharge);

            _logger.LogInformation("Order {OrderId} processing complete.", orderMessage.OrderId);
        }
        catch (CosmosException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            _logger.LogError(ex, "Order {OrderId} not found in Cosmos DB.", orderMessage.OrderId);
            throw;
        }
        catch (CosmosException ex)
        {
            _logger.LogError(ex, "Cosmos DB error processing order {OrderId}.", orderMessage.OrderId);
            throw;
        }
    }
}

public class OrderMessage
{
    public string OrderId { get; set; } = string.Empty;
    public string CustomerId { get; set; } = string.Empty;
    public decimal TotalAmount { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class OrderRecord
{
    public string Id { get; set; } = string.Empty;
    public string CustomerId { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public decimal TotalAmount { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public string PartitionKey => CustomerId;
}
