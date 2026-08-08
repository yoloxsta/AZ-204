using Azure.Messaging.EventHubs;
using EventHandlers.FunctionApp.Models;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace EventHandlers.FunctionApp.Functions;

public class TelemetryProcessor
{
    private readonly ILogger<TelemetryProcessor> _logger;

    public TelemetryProcessor(ILogger<TelemetryProcessor> logger)
    {
        _logger = logger;
    }

    [Function(nameof(TelemetryProcessor))]
    public void Run(
        [EventHubTrigger("telemetry-hub", 
         Connection = "EventHubConnectionString",
         ConsumerGroup = "real-time-consumers")] EventData[] events)
    {
        _logger.LogInformation("=== Processing {Count} telemetry events ===", events.Length);
        
        var statistics = new Dictionary<string, List<TelemetryEvent>>();
        
        foreach (var eventData in events)
        {
            try
            {
                var telemetry = eventData.EventBody.ToObjectFromJson<TelemetryEvent>();
                
                // Group by device for statistics
                if (!statistics.ContainsKey(telemetry.DeviceId))
                {
                    statistics[telemetry.DeviceId] = new List<TelemetryEvent>();
                }
                statistics[telemetry.DeviceId].Add(telemetry);
                
                // Check for anomalies
                CheckForAnomalies(telemetry);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing telemetry event");
            }
        }
        
        // Log summary statistics
        foreach (var kvp in statistics)
        {
            var avgTemp = kvp.Value.Average(t => t.Temperature);
            var avgHumidity = kvp.Value.Average(t => t.Humidity);
            
            _logger.LogInformation(
                "Device {DeviceId}: {Count} events, Avg Temp: {AvgTemp:F1}°C, Avg Humidity: {AvgHumidity:F1}%",
                kvp.Key, kvp.Value.Count, avgTemp, avgHumidity);
        }
        
        _logger.LogInformation("Telemetry batch processing completed");
    }
    
    private void CheckForAnomalies(TelemetryEvent telemetry)
    {
        // Temperature anomaly
        if (telemetry.Temperature > 30)
        {
            _logger.LogWarning(
                "HIGH TEMPERATURE ALERT: Device {DeviceId} at {Temperature:F1}°C (Location: {Location})",
                telemetry.DeviceId, telemetry.Temperature, telemetry.Location);
        }
        
        // Low battery warning
        if (telemetry.BatteryLevel < 30)
        {
            _logger.LogWarning(
                "LOW BATTERY: Device {DeviceId} at {Battery:F1}% (Location: {Location})",
                telemetry.DeviceId, telemetry.BatteryLevel, telemetry.Location);
        }
        
        // Status check
        if (telemetry.Status == "Warning" || telemetry.Status == "Offline")
        {
            _logger.LogWarning(
                "Device {DeviceId} status: {Status}",
                telemetry.DeviceId, telemetry.Status);
        }
    }
}
