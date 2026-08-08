namespace TelemetrySimulator.Models;

public class DeviceTelemetry
{
    public string DeviceId { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    public double Temperature { get; set; }
    public double Humidity { get; set; }
    public double Pressure { get; set; }
    public double BatteryLevel { get; set; }
    public string Location { get; set; } = string.Empty;
    public string Status { get; set; } = "Active";
}

public class Device
{
    public string Id { get; set; } = string.Empty;
    public string Location { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
}
