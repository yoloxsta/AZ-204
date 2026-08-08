using Azure.Messaging.EventHubs;
using Azure.Messaging.EventHubs.Producer;
using TelemetrySimulator.Models;

namespace TelemetrySimulator.Services;

public interface IEventHubProducer
{
    Task SendTelemetryAsync(DeviceTelemetry telemetry);
    Task SendBatchTelemetryAsync(IEnumerable<DeviceTelemetry> telemetryBatch);
}

public class EventHubProducer : IEventHubProducer, IAsyncDisposable
{
    private readonly EventHubProducerClient _producerClient;
    private readonly ILogger<EventHubProducer> _logger;

    public EventHubProducer(
        IConfiguration configuration,
        ILogger<EventHubProducer> logger)
    {
        var connectionString = configuration["EventHub:ConnectionString"];
        var eventHubName = configuration["EventHub:Name"];
        
        _producerClient = new EventHubProducerClient(
            connectionString, eventHubName);
        
        _logger = logger;
    }

    public async Task SendTelemetryAsync(DeviceTelemetry telemetry)
    {
        try
        {
            // Create batch with partition key (device ID for ordering)
            using var eventBatch = await _producerClient.CreateBatchAsync(
                new CreateBatchOptions 
                { 
                    PartitionKey = telemetry.DeviceId // Same device = same partition
                });
            
            var eventData = new EventData(
                BinaryData.FromObjectAsJson(telemetry));
            
            // Add custom properties
            eventData.Properties["DeviceType"] = "IoT";
            eventData.Properties["ContentType"] = "application/json";
            
            if (!eventBatch.TryAdd(eventData))
            {
                throw new InvalidOperationException("Event batch is full");
            }
            
            await _producerClient.SendAsync(eventBatch);
            
            _logger.LogInformation(
                "Sent telemetry from device {DeviceId} at {Timestamp}",
                telemetry.DeviceId, telemetry.Timestamp);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send telemetry");
            throw;
        }
    }

    public async Task SendBatchTelemetryAsync(IEnumerable<DeviceTelemetry> telemetryBatch)
    {
        try
        {
            // Group by device to maintain partition ordering
            var groupedByDevice = telemetryBatch.GroupBy(t => t.DeviceId);
            
            foreach (var deviceGroup in groupedByDevice)
            {
                using var eventBatch = await _producerClient.CreateBatchAsync(
                    new CreateBatchOptions 
                    { 
                        PartitionKey = deviceGroup.Key 
                    });
                
                foreach (var telemetry in deviceGroup)
                {
                    var eventData = new EventData(
                        BinaryData.FromObjectAsJson(telemetry));
                    
                    if (!eventBatch.TryAdd(eventData))
                    {
                        // Batch full, send and create new
                        await _producerClient.SendAsync(eventBatch);
                        break;
                    }
                }
                
                if (eventBatch.Count > 0)
                {
                    await _producerClient.SendAsync(eventBatch);
                }
            }
            
            _logger.LogInformation(
                "Sent batch of {Count} telemetry events",
                telemetryBatch.Count());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send batch telemetry");
            throw;
        }
    }

    public async ValueTask DisposeAsync()
    {
        await _producerClient.DisposeAsync();
    }
}
