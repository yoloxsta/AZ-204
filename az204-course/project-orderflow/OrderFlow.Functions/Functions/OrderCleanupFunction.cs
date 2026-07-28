using Microsoft.Azure.Cosmos;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace OrderFlow.Functions.Functions;

public class OrderCleanupFunction
{
    private readonly CosmosClient _cosmosClient;
    private readonly ILogger<OrderCleanupFunction> _logger;
    private readonly Container _orderContainer;

    public OrderCleanupFunction(CosmosClient cosmosClient, ILogger<OrderCleanupFunction> logger)
    {
        _cosmosClient = cosmosClient;
        _logger = logger;
        _orderContainer = _cosmosClient.GetContainer("OrderFlowDb", "Orders");
    }

    [Function(nameof(OrderCleanupFunction))]
    public async Task Run(
        [TimerTrigger("0 0 0 * * *")] TimerInfo timerInfo)
    {
        _logger.LogInformation("OrderCleanupFunction started at: {Time}", DateTime.UtcNow);

        var cutoff = DateTime.UtcNow.AddHours(-24);
        var cancelledCount = 0;

        var query = new QueryDefinition(
            "SELECT c.id, c.customerId FROM c WHERE c.status = 'Pending' AND c.createdAt < @cutoff")
            .WithParameter("@cutoff", cutoff);

        using var iterator = _orderContainer.GetItemQueryIterator<OrderRecord>(
            query,
            requestOptions: new QueryRequestOptions
            {
                MaxItemCount = 100
            });

        while (iterator.HasMoreResults)
        {
            var batch = await iterator.ReadNextAsync();
            foreach (var order in batch)
            {
                try
                {
                    var patchOperation = PatchOperation.Replace("/status", "Cancelled");
                    await _orderContainer.PatchItemAsync<OrderRecord>(
                        id: order.Id,
                        partitionKey: new PartitionKey(order.CustomerId),
                        patchOperations: [patchOperation]);

                    cancelledCount++;
                    _logger.LogInformation("Cancelled stale order {OrderId} for customer {CustomerId}.",
                        order.Id, order.CustomerId);
                }
                catch (CosmosException ex)
                {
                    _logger.LogError(ex, "Failed to cancel order {OrderId}.", order.Id);
                }
            }
        }

        _logger.LogInformation(
            "OrderCleanupFunction completed. Cancelled {Count} stale orders older than 24 hours.",
            cancelledCount);

        if (timerInfo.ScheduleStatus is not null)
        {
            _logger.LogInformation("Next run scheduled at: {NextRun}", timerInfo.ScheduleStatus.Next);
        }
    }
}
