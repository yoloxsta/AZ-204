# Event Demo - Azure Event Grid & Event Hubs

This demo project demonstrates a complete event-driven architecture using Azure Event Grid and Event Hubs.

## Architecture Overview

```
┌─────────────────────────────────────────────────────────────────────────────┐
│                    EVENT-DRIVEN SYSTEM ARCHITECTURE                          │
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

## Project Structure

```
EventDemo/
├── src/
│   ├── OrderService.Api/          # Web API that publishes to Event Grid
│   │   ├── Controllers/
│   │   │   └── OrdersController.cs
│   │   ├── Models/
│   │   │   └── Order.cs
│   │   ├── Services/
│   │   │   └── EventGridPublisher.cs
│   │   ├── Program.cs
│   │   └── appsettings.json
│   │
│   ├── TelemetrySimulator/         # Console app that sends to Event Hubs
│   │   ├── Models/
│   │   │   └── DeviceTelemetry.cs
│   │   ├── Services/
│   │   │   └── EventHubProducer.cs
│   │   ├── Program.cs
│   │   └── appsettings.json
│   │
│   └── EventHandlers.FunctionApp/  # Azure Functions for processing events
│       ├── Functions/
│       │   ├── OrderCreatedHandler.cs
│       │   ├── OrderStatusChangedHandler.cs
│       │   ├── TelemetryProcessor.cs
│       │   └── TelemetryArchiver.cs
│       ├── Models/
│       │   └── OrderEvent.cs
│       ├── Program.cs
│       ├── local.settings.json
│       └── host.json
│
├── deploy/
│   └── azure-resources.bicep      # Bicep template for all resources
│
└── README.md
```

## Components

### 1. OrderService.Api

**Purpose**: REST API that handles order creation and publishes events to Event Grid.

**Features**:
- Create orders
- Update order status
- Publish `OrderCreated` events
- Publish `OrderStatusChanged` events

**Event Types**:
- `OrderCreated` - Published when a new order is created
- `OrderStatusChanged` - Published when order status is updated

### 2. TelemetrySimulator

**Purpose**: Simulates IoT devices sending telemetry data to Event Hubs.

**Features**:
- Simulates 10 IoT devices
- Sends realistic telemetry data (temperature, humidity, pressure, battery)
- Uses partition keys for ordered delivery
- Batch sending for high throughput

**Event Structure**:
```json
{
  "deviceId": "DEV-1234",
  "timestamp": "2024-01-01T00:00:00Z",
  "temperature": 25.5,
  "humidity": 65.2,
  "pressure": 1013.25,
  "batteryLevel": 85.5,
  "location": "Building-A",
  "status": "Active"
}
```

### 3. EventHandlers.FunctionApp

**Purpose**: Azure Functions that process events from Event Grid and Event Hubs.

**Functions**:
- `OrderCreatedHandler` - Processes OrderCreated events
- `OrderStatusChangedHandler` - Processes OrderStatusChanged events
- `TelemetryProcessor` - Processes telemetry from Event Hubs (real-time)
- `TelemetryArchiver` - Archives telemetry to Blob Storage (batch)

## Setup Instructions

### Prerequisites

- .NET 8 SDK
- Azure CLI
- Azure subscription with contributor access
- Azure Functions Core Tools

### Step 1: Deploy Azure Resources

```bash
# Login to Azure
az login

# Create resource group
az group create --name rg-eventdemo --location eastus

# Deploy using Bicep (template provided separately)
az deployment group create \
    --resource-group rg-eventdemo \
    --template-file deploy/azure-resources.bicep
```

### Step 2: Configure Application Settings

Update `appsettings.json` files in each project with your Azure resource details:

**OrderService.Api/appsettings.json**:
```json
{
  "EventGrid": {
    "TopicEndpoint": "https://YOUR_TOPIC.eastus-1.eventgrid.azure.net/api/events",
    "TopicKey": "YOUR_TOPIC_KEY"
  }
}
```

**TelemetrySimulator/appsettings.json**:
```json
{
  "EventHub": {
    "ConnectionString": "YOUR_EVENT_HUB_CONNECTION_STRING",
    "Name": "telemetry-hub"
  }
}
```

**EventHandlers.FunctionApp/local.settings.json**:
```json
{
  "Values": {
    "EventHubConnectionString": "YOUR_EVENT_HUB_CONNECTION_STRING",
    "StorageConnectionString": "YOUR_STORAGE_CONNECTION_STRING",
    "EventGridTopicEndpoint": "YOUR_EVENT_GRID_TOPIC_ENDPOINT",
    "EventGridTopicKey": "YOUR_EVENT_GRID_TOPIC_KEY"
  }
}
```

### Step 3: Run the Applications

**Terminal 1 - Start Function App**:
```bash
cd src/EventHandlers.FunctionApp
func start
```

**Terminal 2 - Start Order API**:
```bash
cd src/OrderService.Api
dotnet run
```

**Terminal 3 - Start Telemetry Simulator**:
```bash
cd src/TelemetrySimulator
dotnet run
```

## Testing the System

### Test Event Grid Flow

Create an order using curl or Postman:

```bash
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
      }
    ]
  }'
```

Update order status:

```bash
curl -X PUT https://localhost:5001/api/orders/{orderId}/status \
  -H "Content-Type: application/json" \
  -d '{"newStatus": "Shipped"}'
```

### Test Event Hubs Flow

1. Run the Telemetry Simulator
2. Press ENTER to start sending telemetry
3. Watch the Function App logs for processed events
4. Check Blob Storage for archived telemetry

## Monitoring

### View Event Grid Metrics

1. Navigate to Event Grid Topic in Azure Portal
2. Check "Metrics" blade
3. Monitor:
   - Published Events
   - Delivered Events
   - Dropped Events

### View Event Hubs Metrics

1. Navigate to Event Hubs Namespace
2. Check "Metrics" blade
3. Monitor:
   - Incoming Messages
   - Outgoing Messages
   - Consumer Lag

### View Function Logs

```bash
# Stream Function App logs
func azure functionapp logstream func-eventdemo
```

## Key Concepts Demonstrated

### Event Grid Patterns
- ✅ Pub/Sub messaging
- ✅ Event filtering
- ✅ Multiple subscribers
- ✅ Reactive programming

### Event Hubs Patterns
- ✅ Event streaming
- ✅ Partition key for ordering
- ✅ Consumer groups
- ✅ Checkpointing
- ✅ Batch processing
- ✅ Event archiving

## Clean Up

```bash
# Delete all resources
az group delete --name rg-eventdemo --yes --no-wait
```

## Next Steps

- Add Azure Stream Analytics for complex event processing
- Implement Event Hub Capture for automatic archiving
- Add Application Insights for monitoring
- Explore Event Grid Domains for multi-tenant scenarios
- Integrate with Azure Data Lake for analytics
