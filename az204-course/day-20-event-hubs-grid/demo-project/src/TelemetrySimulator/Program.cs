using Bogus;
using TelemetrySimulator.Models;
using TelemetrySimulator.Services;

// Create host
var builder = Host.CreateApplicationBuilder(args);

// Configure services
builder.Services.AddSingleton<IEventHubProducer, EventHubProducer>();

using var host = builder.Build();

var producer = host.Services.GetRequiredService<IEventHubProducer>();
var logger = host.Services.GetRequiredService<ILogger<Program>>();

// Generate fake devices
var devices = GenerateDevices(10);
var faker = CreateTelemetryFaker(devices);

Console.WriteLine("=== IoT Telemetry Simulator ===");
Console.WriteLine($"Simulating {devices.Count} devices");
Console.WriteLine("Press ENTER to start, Q to quit");
Console.WriteLine();

// Interactive mode
var running = true;
while (running)
{
    var key = Console.ReadKey(true);
    
    if (key.Key == ConsoleKey.Enter)
    {
        Console.WriteLine("Starting telemetry stream... (Press Q to stop)");
        
        var cts = new CancellationTokenSource();
        var task = Task.Run(async () =>
        {
            while (!cts.Token.IsCancellationRequested)
            {
                try
                {
                    // Generate batch of telemetry
                    var telemetryBatch = faker.Generate(100);
                    
                    await producer.SendBatchTelemetryAsync(telemetryBatch);
                    
                    Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] Sent 100 telemetry events");
                    
                    await Task.Delay(1000, cts.Token); // 1 second interval
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Error sending telemetry");
                }
            }
        });
        
        // Wait for Q to stop
        while (Console.ReadKey(true).Key != ConsoleKey.Q) { }
        
        cts.Cancel();
        await task;
        Console.WriteLine("Stopped. Press ENTER to restart, Q to quit.");
    }
    else if (key.Key == ConsoleKey.Q)
    {
        running = false;
    }
}

Console.WriteLine("Goodbye!");

// Helper methods
List<Device> GenerateDevices(int count)
{
    var locations = new[] { "Building-A", "Building-B", "Building-C", "Warehouse", "Outdoor" };
    var types = new[] { "Temperature", "Humidity", "Pressure", "Multi-Sensor" };
    
    var deviceFaker = new Faker<Device>()
        .RuleFor(d => d.Id, f => $"DEV-{f.Random.Number(1000, 9999)}")
        .RuleFor(d => d.Location, f => f.PickRandom(locations))
        .RuleFor(d => d.Type, f => f.PickRandom(types));
    
    return deviceFaker.Generate(count);
}

Faker<DeviceTelemetry> CreateTelemetryFaker(List<Device> devices)
{
    return new Faker<DeviceTelemetry>()
        .RuleFor(t => t.DeviceId, f => f.PickRandom(devices).Id)
        .RuleFor(t => t.Timestamp, f => DateTime.UtcNow)
        .RuleFor(t => t.Temperature, f => f.Random.Double(15, 35))
        .RuleFor(t => t.Humidity, f => f.Random.Double(30, 80))
        .RuleFor(t => t.Pressure, f => f.Random.Double(990, 1020))
        .RuleFor(t => t.BatteryLevel, f => f.Random.Double(20, 100))
        .RuleFor(t => t.Location, f => f.PickRandom(devices).Location)
        .RuleFor(t => t.Status, f => f.PickRandom("Active", "Warning", "Offline"));
}
