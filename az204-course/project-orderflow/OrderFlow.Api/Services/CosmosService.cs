using System.Linq.Expressions;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Logging;

namespace OrderFlow.Api.Services;

public class CosmosService : ICosmosService
{
    private readonly CosmosClient _cosmosClient;
    private readonly ILogger<CosmosService> _logger;
    private readonly string _databaseName;
    private Container? _ordersContainer;
    private Container? _productsContainer;

    public CosmosService(CosmosClient cosmosClient, IConfiguration configuration, ILogger<CosmosService> logger)
    {
        _cosmosClient = cosmosClient ?? throw new ArgumentNullException(nameof(cosmosClient));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _databaseName = configuration["CosmosDb:DatabaseName"] ?? "OrderFlowDb";
    }

    public async Task InitializeAsync()
    {
        try
        {
            var database = await _cosmosClient.CreateDatabaseIfNotExistsAsync(_databaseName, throughput: 400);
            var ordersContainer = await database.Database.CreateContainerIfNotExistsAsync("orders", "/customerId", throughput: 400);
            _ordersContainer = ordersContainer.Container;
            var productsContainer = await database.Database.CreateContainerIfNotExistsAsync("products", "/partitionKey", throughput: 400);
            _productsContainer = productsContainer.Container;
            _logger.LogInformation("Cosmos DB initialized: database={Database}, orders + products containers created", _databaseName);
        }
        catch (CosmosException ex)
        {
            _logger.LogError(ex, "Failed to initialize Cosmos DB");
            throw;
        }
    }

    private Container GetContainer<T>()
    {
        if (typeof(T) == typeof(Models.Product))
            return _productsContainer ?? _cosmosClient.GetDatabase(_databaseName).GetContainer("products");
        return _ordersContainer ?? _cosmosClient.GetDatabase(_databaseName).GetContainer("orders");
    }

    public async Task<T> CreateItemAsync<T>(T item, string partitionKey)
    {
        try
        {
            var container = GetContainer<T>();
            var response = await container.CreateItemAsync(item, new PartitionKey(partitionKey));
            _logger.LogInformation("Item created, charge: {Charge} RU", response.RequestCharge);
            return response.Resource;
        }
        catch (CosmosException ex) when (ex.StatusCode == System.Net.HttpStatusCode.Conflict)
        {
            throw new InvalidOperationException("An item with the same key already exists.", ex);
        }
    }

    public async Task<T> CreateItemAsync<T>(T item)
    {
        var container = GetContainer<T>();
        var response = await container.CreateItemAsync(item);
        return response.Resource;
    }

    public async Task<T?> GetItemAsync<T>(string id, string partitionKey)
    {
        try
        {
            var container = GetContainer<T>();
            var response = await container.ReadItemAsync<T>(id, new PartitionKey(partitionKey));
            return response.Resource;
        }
        catch (CosmosException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return default;
        }
    }

    public async Task<List<T>> GetItemsAsync<T>(string partitionKey)
    {
        var container = GetContainer<T>();
        var query = new QueryDefinition("SELECT * FROM c");
        var iterator = container.GetItemQueryIterator<T>(query, requestOptions: new QueryRequestOptions
        {
            PartitionKey = new PartitionKey(partitionKey)
        });

        var results = new List<T>();
        while (iterator.HasMoreResults)
        {
            var response = await iterator.ReadNextAsync();
            results.AddRange(response);
        }
        return results;
    }

    public async Task<List<T>> GetItemsAsync<T>(string partitionKey, Expression<Func<T, bool>> filter)
    {
        var items = await GetItemsAsync<T>(partitionKey);
        return items.Where(filter.Compile()).ToList();
    }

    public async Task<T> UpdateItemAsync<T>(string id, T item, string partitionKey)
    {
        var container = GetContainer<T>();
        var response = await container.UpsertItemAsync(item, new PartitionKey(partitionKey));
        return response.Resource;
    }

    public async Task DeleteItemAsync(string id, string partitionKey)
    {
        try
        {
            var container = GetContainer<Models.Order>();
            await container.DeleteItemAsync<Models.Order>(id, new PartitionKey(partitionKey));
        }
        catch (CosmosException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            throw new KeyNotFoundException($"Item '{id}' not found.", ex);
        }
    }

    public async Task<int> GetOrderCountAsync()
    {
        var container = GetContainer<Models.Order>();
        var query = new QueryDefinition("SELECT VALUE COUNT(1) FROM c");
        var iterator = container.GetItemQueryIterator<int>(query);

        var count = 0;
        while (iterator.HasMoreResults)
        {
            var response = await iterator.ReadNextAsync();
            foreach (var item in response)
                count += item;
        }
        return count;
    }
}
