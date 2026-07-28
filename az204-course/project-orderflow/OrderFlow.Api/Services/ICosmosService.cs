using System.Linq.Expressions;

namespace OrderFlow.Api.Services;

public interface ICosmosService
{
    Task<T> CreateItemAsync<T>(T item, string partitionKey);
    Task<T> CreateItemAsync<T>(T item);
    Task<T?> GetItemAsync<T>(string id, string partitionKey);
    Task<List<T>> GetItemsAsync<T>(string partitionKey);
    Task<List<T>> GetItemsAsync<T>(string partitionKey, Expression<Func<T, bool>> filter);
    Task<T> UpdateItemAsync<T>(string id, T item, string partitionKey);
    Task DeleteItemAsync(string id, string partitionKey);
    Task<int> GetOrderCountAsync();
}
