using Azure.Messaging.EventHubs;
using EventHandlers.FunctionApp.Models;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace EventHandlers.FunctionApp.Functions;

public class TelemetryArchiver
{
    private readonly ILogger<TelemetryArchiver> _logger;

    public TelemetryArchiver(ILogger<TelemetryArchiver> logger)
    {
        _logger = logger;
    }

    [Function(nameof(TelemetryArchiver))]
    [BlobOutput("telemetry-archive/{date:yyyy}/{date:MM}/{date:dd}/{rand-guid}.json", Connection = "StorageConnectionString")]
    public string Run(
        [EventHubTrigger("telemetry-hub", 
         Connection = "EventHubConnectionString",
         ConsumerGroup = "batch-consumers")] EventData[] events,
        FunctionContext context)
    {
        _logger.LogInformation("Archiving {Count} telemetry events", events.Length);
        
        var telemetryList = new List<TelemetryEvent>();
        
        foreach (var eventData in events)
        {
            try
            {
                var telemetry = eventData.EventBody.ToObjectFromJson<TelemetryEvent>();
                telemetryList.Add(telemetry);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deserializing telemetry event");
            }
        }
        
        // Return JSON for blob storage
        var archiveData = new
        {
            ArchivedAt = DateTime.UtcNow,
            EventCount = telemetryList.Count,
            Events = telemetryList
        };
        
        return JsonSerializer.Serialize(archiveData, new JsonSerializerOptions 
        { 
            WriteIndented = true 
        });
    }
}
