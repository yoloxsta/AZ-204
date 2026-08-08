# Day 20 Lab: Building an Event-Driven System with Event Grid & Event Hubs

## Lab Overview

In this comprehensive lab, you'll build a real-world event-driven system that demonstrates both Azure Event Grid and Event Hubs. We'll create an order processing system that:

1. **Uses Event Grid** to publish and react to order events
2. **Uses Event Hubs** to stream telemetry data from simulated IoT devices
3. **Combines both** for a complete event-driven architecture

### What We'll Build

```
┌─────────────────────────────────────────────────────────────────────────────┐
│                    ORDER FLOW EVENT SYSTEM                                   │
├─────────────────────────────────────────────────────────────────────────────┤
│                                                                              │
│  ┌─────────────────┐        ┌─────────────────┐        ┌─────────────────┐ │
│  │   Order API     │        │  Event Grid     │        │ Azure Functions │ │
│  │   Service       │ ────►  │  Custom Topic   │ ────►  │ • Email Handler │ │
│  │                 │        │                 │        │ • Inventory     │ │
│  └─────────────────┘        └─────────────────┘        │ • Analytics     │ │
│                                                          └─────────────────┘ │
│                                                                              │
│  ┌─────────────────┐        ┌─────────────────┐        ┌─────────────────┐ │
│  │   IoT Simulator │        │  Event Hubs     │        │ Azure Function  │ │
│  │   (Telemetry)   │ ────►  │  Telemetry Hub  │ ────►  │ Stream Processor│ │
│  │                 │        │  4 Partitions   │        │                 │ │
│  └─────────────────┘        └─────────────────┘        └─────────────────┘ │
│                                     │                                        │
│                                     ▼                                        │
│                              ┌─────────────────┐                           │
│                              │ Azure Stream    │                           │
│                              │ Analytics       │                           │
│                              └─────────────────┘                           │
│                                                                              │
└─────────────────────────────────────────────────────────────────────────────┘
```

---

## Prerequisites

- Azure subscription with contributor access
- .NET 8 SDK installed
- Azure CLI installed
- Visual Studio 2022 or VS Code with C# extension
- Azure Functions Core Tools (`npm install -g azure-functions-core-tools@4`)

---

## Part 1: Create Azure Resources

### Step 1: Create Resource Group

```bash
# Set variables
RESOURCE_GROUP="rg-eventdemo"
LOCATION="eastus"

# Create resource group
az group create --name $RESOURCE_GROUP --location $LOCATION
```

### Step 2: Create Event Grid Custom Topic

```bash
# Create Event Grid topic
az eventgrid topic create \
    --name order-events-topic \
    --resource-group $RESOURCE_GROUP \
    --location $LOCATION

# Get topic endpoint (save this for later)
az eventgrid topic show \
    --name order-events-topic \
    --resource-group $RESOURCE_GROUP \
    --query endpoint --output tsv

# Get topic key (save this for later)
az eventgrid topic key list \
    --name order-events-topic \
    --resource-group $RESOURCE_GROUP \
    --query key1 --output tsv
```

### Step 3: Create Event Hubs Namespace and Hub

```bash
# Create Event Hubs namespace
az eventhubs namespace create \
    --name evhns-telemetry-demo \
    --resource-group $RESOURCE_GROUP \
    --location $LOCATION \
    --sku Standard \
    --capacity 1 \
    --enable-auto-inflate true \
    --maximum-throughput-units 4

# Create Event Hub with 4 partitions
az eventhubs eventhub create \
    --name telemetry-hub \
    --namespace-name evhns-telemetry-demo \
    --resource-group $RESOURCE_GROUP \
    --message-retention 7 \
    --partition-count 4

# Create consumer groups
az eventhubs eventhub consumer-group create \
    --name real-time-consumers \
    --namespace-name evhns-telemetry-demo \
    --eventhub-name telemetry-hub \
    --resource-group $RESOURCE_GROUP

az eventhubs eventhub consumer-group create \
    --name batch-consumers \
    --namespace-name evhns-telemetry-demo \
    --eventhub-name telemetry-hub \
    --resource-group $RESOURCE_GROUP

# Get connection string
az eventhubs namespace authorization-rule keys list \
    --name RootManageSharedAccessKey \
    --namespace-name evhns-telemetry-demo \
    --resource-group $RESOURCE_GROUP \
    --query primaryConnectionString --output tsv
```

### Step 4: Create Storage Account (for Function App and Event Hub checkpointing)

```bash
# Create storage account
az storage account create \
    --name stevendemo2026 \
    --resource-group $RESOURCE_GROUP \
    --location $LOCATION \
    --sku Standard_LRS \
    --kind StorageV2

# Create containers
az storage container create \
    --name eventhub-checkpoints \
    --account-name stevendemo2026

az storage container create \
    --name function-app \
    --account-name stevendemo2026
```

### Step 5: Create Function App

```bash
# Create Function App
az functionapp create \
    --name func-eventdemo \
    --resource-group $RESOURCE_GROUP \
    --consumption-plan-location $LOCATION \
    --runtime dotnet-isolated \
    --runtime-version 8.0 \
    --functions-version 4 \
    --storage-account stevendemo2026

# Configure app settings
az functionapp config appsettings set \
    --name func-eventdemo \
    --resource-group $RESOURCE_GROUP \
    --settings \
        "EventGridTopicEndpoint=https://order-events-topic.eastus-1.eventgrid.azure.net/api/events" \
        "EventGridTopicKey=YOUR_TOPIC_KEY_HERE" \
        "EventHubConnectionString=YOUR_EVENTHUB_CONNECTION_STRING_HERE" \
        "StorageConnectionString=YOUR_STORAGE_CONNECTION_STRING_HERE"
```

---

## Part 2: Create the Demo Project

### Project Structure

```
EventDemo/
├── src/
│   ├── OrderService.Api/          # Web API that publishes to Event Grid
│   ├── TelemetrySimulator/         # Console app that sends to Event Hubs
│   └── EventHandlers.FunctionApp/  # Azure Functions for processing events
├── deploy/
│   └── azure-resources.bicep      # Bicep template for all resources
└── README.md
```

### Step 1: Create Solution and Projects

```bash
# Create solution
dotnet new sln -n EventDemo

# Create projects
dotnet new webapi -n OrderService.Api -o src/OrderService.Api
dotnet new console -n TelemetrySimulator -o src/TelemetrySimulator
dotnet new func -n EventHandlers.FunctionApp -o src/EventHandlers.FunctionApp --worker-runtime dotnetIsolated

# Add to solution
dotnet sln add src/OrderService.Api
dotnet sln add src/TelemetrySimulator
dotnet sln add src/EventHandlers.FunctionApp
```

### Step 2: Add NuGet Packages

```bash
# OrderService.Api packages
cd src/OrderService.Api
dotnet add package Azure.Messaging.EventGrid
dotnet add package Azure.Identity
dotnet add package Swashbuckle.AspNetCore

# TelemetrySimulator packages
cd ../TelemetrySimulator
dotnet add package Azure.Messaging.EventHubs
dotnet add package Azure.Messaging.EventHubs.Producer
dotnet add package Bogus

# EventHandlers.FunctionApp packages
cd ../EventHandlers.FunctionApp
dotnet add package Microsoft.Azure.Functions.Worker
dotnet add package Microsoft.Azure.Functions.Worker.Extensions.EventGrid
dotnet add package Microsoft.Azure.Functions.Worker.Extensions.EventHubs
dotnet add package Azure.Messaging.EventHubs
dotnet add package Azure.Storage.Blobs
dotnet add package Azure.Messaging.EventHubs.Processor
```

---

## Part 3: Implement OrderService.Api (Event Grid Publisher)

### Models/Order.cs

```csharp
namespace OrderService.Api.Models;

public class Order
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string CustomerId { get; set; } = string.Empty;
    public string CustomerEmail { get; set; } = string.Empty;
    public List<OrderItem> Items { get; set; } = new();
    public decimal Total { get; set; }
    public string Status { get; set; } = "Created";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public class OrderItem
{
    public string ProductId { get; set; } = string.Empty;
    public string ProductName { get; set; } = string.Empty;
    public int Quantity { get; set; }
    public decimal UnitPrice { get; set; }
}

public static class OrderExtensions
{
    public static decimal CalculateTotal(this Order order)
    {
        return order.Items.Sum(i => i.Quantity * i.UnitPrice);
    }
}
```

### Services/EventGridPublisher.cs

```csharp
using Azure;
using Azure.Messaging.EventGrid;
using OrderService.Api.Models;

namespace OrderService.Api.Services;

public interface IEventGridPublisher
{
    Task PublishOrderCreatedEventAsync(Order order);
    Task PublishOrderStatusChangedEventAsync(Order order, string previousStatus);
}

public class EventGridPublisher : IEventGridPublisher
{
    private readonly EventGridPublisherClient _client;
    private readonly ILogger<EventGridPublisher> _logger;

    public EventGridPublisher(
        IConfiguration configuration,
        ILogger<EventGridPublisher> logger)
    {
        var topicEndpoint = configuration["EventGrid:TopicEndpoint"];
        var topicKey = configuration["EventGrid:TopicKey"];
        
        _client = new EventGridPublisherClient(
            new Uri(topicEndpoint!),
            new AzureKeyCredential(topicKey!));
        
        _logger = logger;
    }

    public async Task PublishOrderCreatedEventAsync(Order order)
    {
        var eventGridEvent = new EventGridEvent(
            subject: $"orders/{order.Id}",
            eventType: "OrderCreated",
            dataVersion: "1.0",
            data: BinaryData.FromObjectAsJson(order)
        );
        
        eventGridEvent.Id = Guid.NewGuid().ToString();
        
        await _client.SendEventAsync(eventGridEvent);
        
        _logger.LogInformation(
            "Published OrderCreated event for order {OrderId}", 
            order.Id);
    }

    public async Task PublishOrderStatusChangedEventAsync(Order order, string previousStatus)
    {
        var eventData = new
        {
            OrderId = order.Id,
            PreviousStatus = previousStatus,
            NewStatus = order.Status,
            Timestamp = DateTime.UtcNow,
            CustomerId = order.CustomerId
        };
        
        var eventGridEvent = new EventGridEvent(
            subject: $"orders/{order.Id}/status",
            eventType: "OrderStatusChanged",
            dataVersion: "1.0",
            data: BinaryData.FromObjectAsJson(eventData)
        );
        
        await _client.SendEventAsync(eventGridEvent);
        
        _logger.LogInformation(
            "Published OrderStatusChanged event for order {OrderId}: {Previous} -> {New}",
            order.Id, previousStatus, order.Status);
    }
}
```

### Controllers/OrdersController.cs

```csharp
using Microsoft.AspNetCore.Mvc;
using OrderService.Api.Models;
using OrderService.Api.Services;

namespace OrderService.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class OrdersController : ControllerBase
{
    private readonly IEventGridPublisher _eventPublisher;
    private readonly ILogger<OrdersController> _logger;
    
    // In-memory store for demo purposes
    private static readonly List<Order> _orders = new();

    public OrdersController(
        IEventGridPublisher eventPublisher,
        ILogger<OrdersController> logger)
    {
        _eventPublisher = eventPublisher;
        _logger = logger;
    }

    [HttpPost]
    public async Task<ActionResult<Order>> CreateOrder([FromBody] CreateOrderRequest request)
    {
        var order = new Order
        {
            CustomerId = request.CustomerId,
            CustomerEmail = request.CustomerEmail,
            Items = request.Items,
            Status = "Created"
        };
        
        order.Total = order.CalculateTotal();
        
        _orders.Add(order);
        
        // Publish OrderCreated event to Event Grid
        await _eventPublisher.PublishOrderCreatedEventAsync(order);
        
        return CreatedAtAction(
            nameof(GetOrder), 
            new { id = order.Id }, 
            order);
    }

    [HttpGet("{id}")]
    public ActionResult<Order> GetOrder(string id)
    {
        var order = _orders.FirstOrDefault(o => o.Id == id);
        if (order == null)
        {
            return NotFound();
        }
        return Ok(order);
    }

    [HttpGet]
    public ActionResult<IEnumerable<Order>> GetOrders()
    {
        return Ok(_orders);
    }

    [HttpPut("{id}/status")]
    public async Task<ActionResult<Order>> UpdateStatus(string id, [FromBody] UpdateStatusRequest request)
    {
        var order = _orders.FirstOrDefault(o => o.Id == id);
        if (order == null)
        {
            return NotFound();
        }
        
        var previousStatus = order.Status;
        order.Status = request.NewStatus;
        
        // Publish status changed event
        await _eventPublisher.PublishOrderStatusChangedEventAsync(order, previousStatus);
        
        return Ok(order);
    }
}

public record CreateOrderRequest(
    string CustomerId,
    string CustomerEmail,
    List<OrderItem> Items);

public record UpdateStatusRequest(string NewStatus);
```

### Program.cs

```csharp
using OrderService.Api.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Configure Event Grid
builder.Services.Configure<EventGridOptions>(
    builder.Configuration.GetSection("EventGrid"));

builder.Services.AddSingleton<IEventGridPublisher, EventGridPublisher>();

var app = builder.Build();

// Configure pipeline
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseAuthorization();
app.MapControllers();

app.Run();

public class EventGridOptions
{
    public string TopicEndpoint { get; set; } = string.Empty;
    public string TopicKey { get; set; } = string.Empty;
}
```

### appsettings.json

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning"
    }
  },
  "EventGrid": {
    "TopicEndpoint": "https://order-events-topic.eastus-1.eventgrid.azure.net/api/events",
    "TopicKey": "YOUR_EVENT_GRID_TOPIC_KEY"
  },
  "AllowedHosts": "*"
}
```

---

## Part 4: Implement TelemetrySimulator (Event Hubs Producer)

### Models/DeviceTelemetry.cs

```csharp
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
```

### Services/EventHubProducer.cs

```csharp
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
```

### Program.cs (Telemetry Simulator)

```csharp
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
```

### appsettings.json

```json
{
  "EventHub": {
    "ConnectionString": "Endpoint=sb://evhns-telemetry-demo.servicebus.windows.net/;SharedAccessKeyName=RootManageSharedAccessKey;SharedAccessKey=YOUR_KEY",
    "Name": "telemetry-hub"
  },
  "Logging": {
    "LogLevel": {
      "Default": "Information"
    }
  }
}
```

---

## Part 5: Implement EventHandlers.FunctionApp

### Models/OrderEvent.cs

```csharp
using Azure.Messaging.EventGrid;

namespace EventHandlers.FunctionApp.Models;

public class OrderEvent
{
    public string OrderId { get; set; } = string.Empty;
    public string CustomerId { get; set; } = string.Empty;
    public string CustomerEmail { get; set; } = string.Empty;
    public decimal Total { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}

public class OrderStatusChangedEvent
{
    public string OrderId { get; set; } = string.Empty;
    public string PreviousStatus { get; set; } = string.Empty;
    public string NewStatus { get; set; } = string.Empty;
    public string CustomerId { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; }
}

public class TelemetryEvent
{
    public string DeviceId { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; }
    public double Temperature { get; set; }
    public double Humidity { get; set; }
    public double Pressure { get; set; }
    public double BatteryLevel { get; set; }
    public string Location { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
}
```

### Functions/OrderCreatedHandler.cs

```csharp
using Azure.Messaging.EventGrid;
using EventHandlers.FunctionApp.Models;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace EventHandlers.FunctionApp.Functions;

public class OrderCreatedHandler
{
    private readonly ILogger<OrderCreatedHandler> _logger;

    public OrderCreatedHandler(ILogger<OrderCreatedHandler> logger)
    {
        _logger = logger;
    }

    [Function(nameof(OrderCreatedHandler))]
    public void Run(
        [EventGridTrigger] EventGridEvent eventGridEvent)
    {
        _logger.LogInformation("=== OrderCreated Event Received ===");
        _logger.LogInformation("Event Type: {EventType}", eventGridEvent.EventType);
        _logger.LogInformation("Subject: {Subject}", eventGridEvent.Subject);
        _logger.LogInformation("Event Time: {EventTime}", eventGridEvent.EventTime);
        
        try
        {
            // Deserialize the order data
            var order = eventGridEvent.Data.ToObjectFromJson<OrderEvent>();
            
            _logger.LogInformation("Order ID: {OrderId}", order.OrderId);
            _logger.LogInformation("Customer: {CustomerId} ({Email})", order.CustomerId, order.CustomerEmail);
            _logger.LogInformation("Total: {Total:C}", order.Total);
            _logger.LogInformation("Status: {Status}", order.Status);
            
            // Simulate processing - e.g., send email, update inventory, etc.
            ProcessOrder(order);
            
            _logger.LogInformation("Order processing completed successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing OrderCreated event");
            throw; // Let Event Grid retry
        }
    }
    
    private void ProcessOrder(OrderEvent order)
    {
        // Simulate different processing based on order total
        if (order.Total > 1000)
        {
            _logger.LogWarning("High-value order detected! OrderId: {OrderId}, Total: {Total}", 
                order.OrderId, order.Total);
            // Could trigger additional validation or approval workflow
        }
        
        // Simulate email notification
        _logger.LogInformation("Sending order confirmation email to {Email}", order.CustomerEmail);
        
        // Simulate inventory check
        _logger.LogInformation("Checking inventory for order {OrderId}", order.OrderId);
    }
}
```

### Functions/OrderStatusChangedHandler.cs

```csharp
using Azure.Messaging.EventGrid;
using EventHandlers.FunctionApp.Models;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace EventHandlers.FunctionApp.Functions;

public class OrderStatusChangedHandler
{
    private readonly ILogger<OrderStatusChangedHandler> _logger;

    public OrderStatusChangedHandler(ILogger<OrderStatusChangedHandler> logger)
    {
        _logger = logger;
    }

    [Function(nameof(OrderStatusChangedHandler))]
    public void Run(
        [EventGridTrigger] EventGridEvent eventGridEvent)
    {
        _logger.LogInformation("=== OrderStatusChanged Event ===");
        
        var statusEvent = eventGridEvent.Data.ToObjectFromJson<OrderStatusChangedEvent>();
        
        _logger.LogInformation("Order {OrderId}: {PreviousStatus} -> {NewStatus}",
            statusEvent.OrderId, statusEvent.PreviousStatus, statusEvent.NewStatus);
        
        // Handle status-specific logic
        switch (statusEvent.NewStatus)
        {
            case "Shipped":
                HandleShippedOrder(statusEvent);
                break;
            case "Delivered":
                HandleDeliveredOrder(statusEvent);
                break;
            case "Cancelled":
                HandleCancelledOrder(statusEvent);
                break;
            default:
                _logger.LogInformation("Status change to {Status}", statusEvent.NewStatus);
                break;
        }
    }
    
    private void HandleShippedOrder(OrderStatusChangedEvent statusEvent)
    {
        _logger.LogInformation("Sending shipping notification to customer {CustomerId}", 
            statusEvent.CustomerId);
        // Send SMS or push notification
    }
    
    private void HandleDeliveredOrder(OrderStatusChangedEvent statusEvent)
    {
        _logger.LogInformation("Order {OrderId} delivered. Requesting feedback.", 
            statusEvent.OrderId);
        // Send feedback request email
    }
    
    private void HandleCancelledOrder(OrderStatusChangedEvent statusEvent)
    {
        _logger.LogWarning("Order {OrderId} cancelled. Initiating refund if applicable.", 
            statusEvent.OrderId);
        // Process refund if payment was taken
    }
}
```

### Functions/TelemetryProcessor.cs

```csharp
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
```

### Functions/TelemetryArchiver.cs

```csharp
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
```

### Program.cs (Function App)

```csharp
using Microsoft.Extensions.Hosting;

var host = new HostBuilder()
    .ConfigureFunctionsWorkerDefaults()
    .Build();

host.Run();
```

### local.settings.json

```json
{
  "IsEncrypted": false,
  "Values": {
    "AzureWebJobsStorage": "UseDevelopmentStorage=true",
    "FUNCTIONS_WORKER_RUNTIME": "dotnet-isolated",
    "EventGrid:TopicEndpoint": "https://order-events-topic.eastus-1.eventgrid.azure.net/api/events",
    "EventGrid:TopicKey": "YOUR_EVENT_GRID_TOPIC_KEY",
    "EventHubConnectionString": "Endpoint=sb://evhns-telemetry-demo.servicebus.windows.net/;SharedAccessKeyName=RootManageSharedAccessKey;SharedAccessKey=YOUR_KEY",
    "StorageConnectionString": "DefaultEndpointsProtocol=https;AccountName=steventdemo2026;AccountKey=YOUR_KEY;EndpointSuffix=core.windows.net"
  }
}
```

---

## Part 6: Deploy and Configure

### Step 1: Deploy Function App

```bash
# Publish Function App
cd src/EventHandlers.FunctionApp
dotnet publish -c Release

# Deploy using Azure Functions Core Tools
func azure functionapp publish func-eventdemo
```

### Step 2: Create Event Grid Subscriptions

```bash
# Get Function App resource ID
FUNCTION_ID=$(az functionapp show \
    --name func-eventdemo \
    --resource-group $RESOURCE_GROUP \
    --query id --output tsv)

# Create Event Grid subscription for OrderCreated events
az eventgrid event-subscription create \
    --name order-created-sub \
    --source-resource-id /subscriptions/{sub}/resourceGroups/$RESOURCE_GROUP/providers/Microsoft.EventGrid/topics/order-events-topic \
    --endpoint-type azurefunction \
    --endpoint $FUNCTION_ID/functions/OrderCreatedHandler \
    --included-event-types OrderCreated

# Create Event Grid subscription for OrderStatusChanged events
az eventgrid event-subscription create \
    --name order-status-sub \
    --source-resource-id /subscriptions/{sub}/resourceGroups/$RESOURCE_GROUP/providers/Microsoft.EventGrid/topics/order-events-topic \
    --endpoint-type azurefunction \
    --endpoint $FUNCTION_ID/functions/OrderStatusChangedHandler \
    --included-event-types OrderStatusChanged
```

### Step 3: Configure Event Hub Trigger

The Event Hub triggers are configured via attributes in the function code. The Function App will automatically create the necessary Event Hub trigger bindings.

---

## Part 7: Testing the System

### Test 1: Event Grid Order Flow

```bash
# Run the Order API
cd src/OrderService.Api
dotnet run

# In another terminal, create an order
curl -X POST https://localhost:5001/api/orders \
  -H "Content-Type: application/json" \
  -d '{
    "customerId": "cust-001",
    "customerEmail": "customer@example.com",
    "items": [
      {
        "productId": "prod-001",
        "productName": "Widget A",
        "quantity": 2,
        "unitPrice": 29.99
      },
      {
        "productId": "prod-002",
        "productName": "Widget B",
        "quantity": 1,
        "unitPrice": 49.99
      }
    ]
  }'

# Update order status
curl -X PUT https://localhost:5001/api/orders/{orderId}/status \
  -H "Content-Type: application/json" \
  -d '{"newStatus": "Shipped"}'
```

### Test 2: Event Hubs Telemetry

```bash
# Run the telemetry simulator
cd src/TelemetrySimulator
dotnet run

# Press ENTER to start sending telemetry
# Watch the Function App logs for processed events
```

### Test 3: Monitor Events in Azure Portal

1. Navigate to Event Grid Topic in Azure Portal
2. Check "Metrics" for published/delivered events
3. Navigate to Event Hubs
4. Check "Process data" for real-time metrics
5. View archived telemetry in Blob Storage

---

## Part 8: Bicep Deployment Template

### deploy/azure-resources.bicep

```bicep
param location string = resourceGroup().location
param eventGridTopicName string = 'order-events-topic'
param eventHubNamespaceName string = 'evhns-telemetry-demo'
param eventHubName string = 'telemetry-hub'
param storageAccountName string = 'steventdemo2026'
param functionAppName string = 'func-eventdemo'

// Event Grid Topic
resource eventGridTopic 'Microsoft.EventGrid/topics@2020-06-01' = {
  name: eventGridTopicName
  location: location
  sku: {
    name: 'Basic'
  }
  properties: {
    publicNetworkAccess: 'Enabled'
  }
}

// Event Hubs Namespace
resource eventHubNamespace 'Microsoft.EventHub/namespaces@2021-11-01' = {
  name: eventHubNamespaceName
  location: location
  sku: {
    name: 'Standard'
    capacity: 1
  }
  properties: {
    isAutoInflateEnabled: true
    maximumThroughputUnits: 4
  }
}

// Event Hub
resource eventHub 'Microsoft.EventHub/namespaces/eventhubs@2021-11-01' = {
  parent: eventHubNamespace
  name: eventHubName
  properties: {
    messageRetentionInDays: 7
    partitionCount: 4
  }
}

// Consumer Groups
resource realTimeConsumers 'Microsoft.EventHub/namespaces/eventhubs/consumergroups@2021-11-01' = {
  parent: eventHub
  name: 'real-time-consumers'
}

resource batchConsumers 'Microsoft.EventHub/namespaces/eventhubs/consumergroups@2021-11-01' = {
  parent: eventHub
  name: 'batch-consumers'
}

// Storage Account
resource storageAccount 'Microsoft.Storage/storageAccounts@2021-09-01' = {
  name: storageAccountName
  location: location
  sku: {
    name: 'Standard_LRS'
  }
  kind: 'StorageV2'
}

// Storage Containers
resource checkpointsContainer 'Microsoft.Storage/storageAccounts/blobServices/containers@2021-09-01' = {
  parent: storageAccount
  name: 'default/eventhub-checkpoints'
}

resource archiveContainer 'Microsoft.Storage/storageAccounts/blobServices/containers@2021-09-01' = {
  parent: storageAccount
  name: 'default/telemetry-archive'
}

// Function App Plan
resource functionPlan 'Microsoft.Web/serverfarms@2022-03-01' = {
  name: '${functionAppName}-plan'
  location: location
  sku: {
    name: 'Y1'
    tier: 'Dynamic'
  }
  properties: {
    maximumElasticWorkerCount: 1
  }
}

// Function App
resource functionApp 'Microsoft.Web/sites@2022-03-01' = {
  name: functionAppName
  location: location
  kind: 'functionapp'
  properties: {
    serverFarmId: functionPlan.id
    siteConfig: {
      appSettings: [
        {
          name: 'AzureWebJobsStorage'
          value: 'DefaultEndpointsProtocol=https;AccountName=${storageAccount.name};EndpointSuffix=core.windows.net'
        }
        {
          name: 'FUNCTIONS_WORKER_RUNTIME'
          value: 'dotnet-isolated'
        }
        {
          name: 'EventGridTopicEndpoint'
          value: eventGridTopic.properties.endpoint
        }
        {
          name: 'EventHubConnectionString'
          value: listKeys(eventHubNamespace.id, '2017-04-01').primaryConnectionString
        }
      ]
    }
  }
}

// Outputs
output eventGridTopicEndpoint string = eventGridTopic.properties.endpoint
output eventHubNamespaceName string = eventHubNamespace.name
output functionAppName string = functionApp.name
```

### Deploy with Bicep

```bash
# Deploy all resources
az deployment group create \
    --resource-group $RESOURCE_GROUP \
    --template-file deploy/azure-resources.bicep \
    --parameters location=eastus
```

---

## Part 9: Monitoring and Observability

### Application Insights Integration

Add Application Insights to monitor your event-driven system:

```bash
# Create Application Insights
az monitor app-insights component create \
    --app appi-eventdemo \
    --resource-group $RESOURCE_GROUP \
    --location $LOCATION \
    --kind web \
    --application-type web

# Get instrumentation key
INSTRUMENTATION_KEY=$(az monitor app-insights component show \
    --app appi-eventdemo \
    --resource-group $RESOURCE_GROUP \
    --query instrumentationKey --output tsv)

# Add to Function App
az functionapp config appsettings set \
    --name func-eventdemo \
    --resource-group $RESOURCE_GROUP \
    --settings "APPINSIGHTS_INSTRUMENTATIONKEY=$INSTRUMENTATION_KEY"
```

### Query Telemetry in Log Analytics

```kusto
// Query Event Grid events
AzureDiagnostics
| where ResourceType == "MICROSOFT.EVENTGRID/TOPICS"
| project TimeGenerated, EventType_s, Subject_s, Status_s
| order by TimeGenerated desc

// Query Event Hubs metrics
AzureDiagnostics
| where ResourceType == "MICROSOFT.EVENTHUB/NAMESPACES"
| where Category == "Metrics"
| project TimeGenerated, CounterName, CounterValue
| order by TimeGenerated desc

// Query Function App logs
traces
| where cloud_RoleName == "func-eventdemo"
| project timestamp, message, severityLevel
| order by timestamp desc
```

---

## Part 10: Clean Up Resources

```bash
# Delete all resources when done
az group delete --name $RESOURCE_GROUP --yes --no-wait
```

---

## Summary

### What You Built

1. **Event Grid System**
   - Custom topic for order events
   - Event Grid Publisher service
   - Function handlers for OrderCreated and OrderStatusChanged events
   - Event-driven microservices pattern

2. **Event Hubs System**
   - Event Hub with 4 partitions
   - Telemetry simulator with realistic IoT data
   - Multiple consumer groups for different processing patterns
   - Real-time telemetry processor
   - Batch archiver to blob storage

3. **Complete Architecture**
   - Event-driven communication
   - Real-time stream processing
   - Event replay capability
   - Multiple independent consumers
   - Monitoring and observability

### Key Learnings

| Concept | Event Grid | Event Hubs |
|---------|------------|------------|
| **Purpose** | React to discrete events | Stream event data |
| **Pattern** | Pub/Sub | Event Streaming |
| **Delivery** | Push to subscribers | Pull by consumers |
| **Ordering** | Not guaranteed | Per partition |
| **Retention** | Hours | 1-90 days |
| **Replay** | No | Yes |
| **Best For** | Notifications, triggers | IoT, telemetry, logs |

### Next Steps

1. Explore Event Grid Domains for multi-tenant scenarios
2. Implement Event Hub Capture for long-term archiving
3. Add Azure Stream Analytics for complex event processing
4. Integrate with Azure Data Lake for analytics
5. Explore Event Grid Partner Topics (Stripe, Auth0, etc.)

---

## Additional Resources

- [Event Grid Documentation](https://docs.microsoft.com/azure/event-grid/)
- [Event Hubs Documentation](https://docs.microsoft.com/azure/event-hubs/)
- [Event Grid SDK for .NET](https://docs.microsoft.com/dotnet/api/overview/azure/messaging.eventgrid)
- [Event Hubs SDK for .NET](https://docs.microsoft.com/dotnet/api/overview/azure/messaging.eventhubs)
- [Azure Functions triggers and bindings](https://docs.microsoft.com/azure/azure-functions/functions-triggers-bindings)
