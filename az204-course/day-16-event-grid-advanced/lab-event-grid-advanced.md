# Lab: Advanced Event Grid Patterns

## Title
Implement Advanced Event Grid Patterns with Filtering and Dead-Lettering

## Objective
- Create a custom Event Grid topic
- Publish events using .NET SDK
- Configure advanced filtering (subject, data)
- Set up dead-lettering with Storage Blob
- Implement multi-handler routing

## Architecture

```
┌─────────────────────────────────────────────────────────────────────────┐
│                                                                         │
│  ┌──────────────┐                                                       │
│  │  .NET        │                                                       │
│  │  Console App │──── Publish ────►┌────────────────────────────┐       │
│  │  (Publisher) │                 │   Custom Event Grid Topic   │       │
│  └──────────────┘                 │   (az204-eg-topic)          │       │
│                                   └──────────┬─────────────────┘       │
│                                              │                         │
│                         ┌────────────────────┼────────────────────┐    │
│                         │                    │                    │    │
│                         ▼                    ▼                    ▼    │
│                  ┌────────────┐       ┌────────────┐       ┌────────┐ │
│                  │Sub: Subject│       │Sub: Data   │       │Sub: All│ │
│                  │Begins:     │       │Amount>500  │       │Events  │ │
│                  │/orders/    │       │AND Region  │       │        │ │
│                  │premium/*   │       │== us-east  │       │        │ │
│                  └─────┬──────┘       └─────┬──────┘       └───┬────┘ │
│                        │                    │                   │      │
│                        ▼                    ▼                   ▼      │
│               ┌─────────────┐       ┌─────────────┐     ┌──────────┐  │
│               │Azure Function│       │Azure Function│     │  Azure   │  │
│               │(PremiumOrder)│       │(HighValue)   │     │ Function │  │
│               └──────┬──────┘       └──────┬──────┘     │ (AllEvts)│  │
│                      │                     │            └────┬─────┘  │
│                      │ (on failure)        │ (on failure)    │        │
│                      ▼                     ▼                 ▼        │
│               ┌──────────────────────────────────────────────────┐    │
│               │         Dead-Letter: Storage Blob                │    │
│               │         (az204deadletter/blobdeadletter)         │    │
│               └──────────────────────────────────────────────────┘    │
│                                                                       │
└─────────────────────────────────────────────────────────────────────────┘
```

## Prerequisites
- Azure CLI installed and logged in
- .NET 8 SDK installed
- Azure Functions Core Tools v4 installed

---

## Step 1 — Create Resource Group and Storage Account

```bash
az group create --name az204-day16-rg --location eastus

# Register Event Grid provider
az provider register --namespace Microsoft.EventGrid

# Create storage account for dead-letter container
az storage account create \
  --name az204dlstore<unique> \
  --resource-group az204-day16-rg \
  --location eastus \
  --sku Standard_LRS

# Create the dead-letter container
az storage container create \
  --name deadletter \
  --account-name az204dlstore<unique> \
  --auth-mode login
```

---

## Step 2 — Create Custom Event Grid Topic

```bash
az eventgrid topic create \
  --name az204-eg-advanced-topic \
  --resource-group az204-day16-rg \
  --location eastus
```

Get the topic endpoint and access key:

```bash
TOPIC_ENDPOINT=$(az eventgrid topic show \
  --name az204-eg-advanced-topic \
  --resource-group az204-day16-rg \
  --query endpoint --output tsv)

TOPIC_KEY=$(az eventgrid topic key list \
  --name az204-eg-advanced-topic \
  --resource-group az204-day16-rg \
  --query key1 --output tsv)

TOPIC_ID=$(az eventgrid topic show \
  --name az204-eg-advanced-topic \
  --resource-group az204-day16-rg \
  --query id --output tsv)

echo "Endpoint: $TOPIC_ENDPOINT"
echo "Key: $TOPIC_KEY"
echo "ID: $TOPIC_ID"
```

---

## Step 3 — Create Event Grid Subscription with Subject Filtering

Create an Azure Function project for the premium order handler:

```bash
mkdir az204-premium-func && cd az204-premium-func
func init --worker-runtime dotnet-isolated --target-framework net8.0
dotnet add package Microsoft.Azure.Functions.Worker.Extensions.EventGrid
func new --name PremiumOrderHandler --template "EventGrid trigger"
```

Edit `PremiumOrderHandler.cs`:

```csharp
using Azure.Messaging.EventGrid;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace az204_premium_func
{
    public class PremiumOrderHandler
    {
        private readonly ILogger _logger;

        public PremiumOrderHandler(ILoggerFactory loggerFactory)
        {
            _logger = loggerFactory.CreateLogger<PremiumOrderHandler>();
        }

        [Function("PremiumOrderHandler")]
        public void Run([EventGridTrigger] EventGridEvent eventGridEvent)
        {
            _logger.LogInformation("=== PREMIUM ORDER EVENT ===");
            _logger.LogInformation($"Event Type: {eventGridEvent.EventType}");
            _logger.LogInformation($"Subject: {eventGridEvent.Subject}");
            _logger.LogInformation($"Time: {eventGridEvent.EventTime}");
            _logger.LogInformation($"Data: {eventGridEvent.Data}");

            // In a real scenario, process the premium order here
            // e.g., send a notification to the premium support team
        }
    }
}
```

Deploy the Function App:

```bash
# Create Function App storage
az storage account create \
  --name az204funcs16<unique> \
  --resource-group az204-day16-rg \
  --location eastus \
  --sku Standard_LRS

# Create Function App
az functionapp create \
  --name az204-eg-func-16<unique> \
  --resource-group az204-day16-rg \
  --storage-account az204funcs16<unique> \
  --consumption-plan-location eastus \
  --runtime dotnet-isolated \
  --runtime-version 8 \
  --functions-version 4 \
  --os-type Linux

# Deploy
func azure functionapp publish az204-eg-func-16<unique>
```

Create the subscription with subject filtering:

```bash
FUNC_ID=$(az functionapp function show \
  --name az204-eg-func-16<unique> \
  --resource-group az204-day16-rg \
  --function-name PremiumOrderHandler \
  --query id --output tsv)

az eventgrid event-subscription create \
  --name premium-orders-sub \
  --source-resource-id $TOPIC_ID \
  --endpoint $FUNC_ID \
  --endpoint-type azurefunction \
  --subject-begins-with "/orders/premium/" \
  --subject-ends-with "/vip"
```

---

## Step 4 — Create Subscription with Advanced Data Filtering

Create a second Function for high-value orders:

```bash
cd ..
mkdir az204-highvalue-func && cd az204-highvalue-func
func init --worker-runtime dotnet-isolated --target-framework net8.0
dotnet add package Microsoft.Azure.Functions.Worker.Extensions.EventGrid
func new --name HighValueOrderHandler --template "EventGrid trigger"
```

Edit `HighValueOrderHandler.cs`:

```csharp
using Azure.Messaging.EventGrid;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace az204_highvalue_func
{
    public class HighValueOrderHandler
    {
        private readonly ILogger _logger;

        public HighValueOrderHandler(ILoggerFactory loggerFactory)
        {
            _logger = loggerFactory.CreateLogger<HighValueOrderHandler>();
        }

        [Function("HighValueOrderHandler")]
        public void Run([EventGridTrigger] EventGridEvent eventGridEvent)
        {
            _logger.LogInformation("=== HIGH-VALUE ORDER EVENT ===");
            _logger.LogInformation($"Event Type: {eventGridEvent.EventType}");
            _logger.LogInformation($"Subject: {eventGridEvent.Subject}");
            _logger.LogInformation($"Time: {eventGridEvent.EventTime}");
            _logger.LogInformation($"Data: {eventGridEvent.Data}");

            // In a real scenario, trigger fraud review or priority shipping
        }
    }
}
```

Deploy and get the function ID:

```bash
func azure functionapp publish az204-eg-func-16<unique>

HIGHVALUE_FUNC_ID=$(az functionapp function show \
  --name az204-eg-func-16<unique> \
  --resource-group az204-day16-rg \
  --function-name HighValueOrderHandler \
  --query id --output tsv)
```

Create the subscription with advanced data filtering:

```bash
az eventgrid event-subscription create \
  --name high-value-orders-sub \
  --source-resource-id $TOPIC_ID \
  --endpoint $HIGHVALUE_FUNC_ID \
  --endpoint-type azurefunction \
  --advanced-filter data.amount numberGreaterThan 500 \
  --advanced-filter data.region stringEquals "us-east"
```

This subscription only receives events where `amount > 500` AND `region == "us-east"`.

---

## Step 5 — Create .NET Console App to Publish Events

```bash
cd ..
dotnet new console -n EventGridPublisher
cd EventGridPublisher
dotnet add package Azure.Messaging.EventGrid
```

Edit `Program.cs`:

```csharp
using Azure;
using Azure.Messaging.EventGrid;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

class Program
{
    static async Task Main(string[] args)
    {
        var endpoint = new Uri(Environment.GetEnvironmentVariable("TOPIC_ENDPOINT")!);
        var credential = new AzureKeyCredential(Environment.GetEnvironmentVariable("TOPIC_KEY")!);
        var client = new EventGridPublisherClient(endpoint, credential);

        var events = new List<EventGridEvent>
        {
            // Event 1: Premium order in us-east (matches BOTH subscriptions)
            new EventGridEvent(
                subject: "/orders/premium/laptop-001/vip",
                eventType: "Order.Created",
                dataVersion: "1.0",
                data: BinaryData.FromObjectAsJson(new
                {
                    orderId = "ORD-001",
                    product = "Laptop Pro 16",
                    quantity = 1,
                    amount = 1299.99,
                    currency = "USD",
                    customerType = "premium",
                    region = "us-east"
                }))
            {
                Id = "evt-001",
                EventTime = DateTimeOffset.UtcNow
            },

            // Event 2: Standard order in us-east, high value (matches HIGH-VALUE only)
            new EventGridEvent(
                subject: "/orders/standard/headphones-002",
                eventType: "Order.Created",
                dataVersion: "1.0",
                data: BinaryData.FromObjectAsJson(new
                {
                    orderId = "ORD-002",
                    product = "Noise-Cancel Headphones",
                    quantity = 1,
                    amount = 599.99,
                    currency = "USD",
                    customerType = "standard",
                    region = "us-east"
                }))
            {
                Id = "evt-002",
                EventTime = DateTimeOffset.UtcNow
            },

            // Event 3: Premium order in eu-west (matches PREMIUM only, not high-value)
            new EventGridEvent(
                subject: "/orders/premium/tablet-003/vip",
                eventType: "Order.Created",
                dataVersion: "1.0",
                data: BinaryData.FromObjectAsJson(new
                {
                    orderId = "ORD-003",
                    product = "Tablet Air",
                    quantity = 2,
                    amount = 899.98,
                    currency = "EUR",
                    customerType = "premium",
                    region = "eu-west"
                }))
            {
                Id = "evt-003",
                EventTime = DateTimeOffset.UtcNow
            },

            // Event 4: Low-value standard order (matches NEITHER filter)
            new EventGridEvent(
                subject: "/orders/standard/mouse-004",
                eventType: "Order.Created",
                dataVersion: "1.0",
                data: BinaryData.FromObjectAsJson(new
                {
                    orderId = "ORD-004",
                    product = "Wireless Mouse",
                    quantity = 1,
                    amount = 29.99,
                    currency = "USD",
                    customerType = "standard",
                    region = "us-east"
                }))
            {
                Id = "evt-004",
                EventTime = DateTimeOffset.UtcNow
            },

            // Event 5: Cancelled order (different event type)
            new EventGridEvent(
                subject: "/orders/premium/laptop-001/cancelled",
                eventType: "Order.Cancelled",
                dataVersion: "1.0",
                data: BinaryData.FromObjectAsJson(new
                {
                    orderId = "ORD-001",
                    reason = "Customer changed mind",
                    refundAmount = 1299.99
                }))
            {
                Id = "evt-005",
                EventTime = DateTimeOffset.UtcNow
            }
        };

        await client.SendEventsAsync(events);

        Console.WriteLine($"Published {events.Count} events to Event Grid topic");
        Console.WriteLine();
        Console.WriteLine("Expected results:");
        Console.WriteLine("  evt-001: Matches PremiumOrders (subject) AND HighValue (amount>500, us-east)");
        Console.WriteLine("  evt-002: Matches HighValue (amount>500, us-east) only");
        Console.WriteLine("  evt-003: Matches PremiumOrders (subject) only (wrong region for HighValue)");
        Console.WriteLine("  evt-004: Matches NEITHER (low value, not premium)");
        Console.WriteLine("  evt-005: Matches PremiumOrders (subject) - different event type");
    }
}
```

Set environment variables and run:

```bash
# PowerShell
$env:TOPIC_ENDPOINT = "<your-topic-endpoint>"
$env:TOPIC_KEY = "<your-topic-key>"

dotnet run
```

---

## Step 6 — Test Filtering

After publishing, check each Function's logs:

```bash
# Check PremiumOrderHandler logs
az functionapp log tail --name az204-eg-func-16<unique> --resource-group az204-day16-rg

# Check HighValueOrderHandler logs
az functionapp log tail --name az204-eg-func-16<unique> --resource-group az204-day16-rg
```

**Expected results:**

| Event | PremiumOrderHandler | HighValueOrderHandler |
|-------|--------------------|-----------------------|
| evt-001 (`/orders/premium/.../vip`, $1299, us-east) | ✅ Received | ✅ Received |
| evt-002 (`/orders/standard/...`, $599, us-east) | ❌ Filtered out | ✅ Received |
| evt-003 (`/orders/premium/.../vip`, $899, eu-west) | ✅ Received | ❌ Filtered out (wrong region) |
| evt-004 (`/orders/standard/...`, $29, us-east) | ❌ Filtered out | ❌ Filtered out (low value) |
| evt-005 (`/orders/premium/.../cancelled`, $1299) | ✅ Received | ❌ Filtered out (cancelled type) |

---

## Step 7 — Configure Dead-Letter Endpoint

Set up dead-lettering on both subscriptions:

```bash
DEADLETTER_ID=$(az storage account show \
  --name az204dlstore<unique> \
  --resource-group az204-day16-rg \
  --query id --output tsv)

# Update premium subscription with dead-letter
az eventgrid event-subscription update \
  --name premium-orders-sub \
  --source-resource-id $TOPIC_ID \
  --max-delivery-attempts 3 \
  --deadletter-endpoint "$DEADLETTER_ID/blobServices/default/containers/deadletter"

# Update high-value subscription with dead-letter
az eventgrid event-subscription update \
  --name high-value-orders-sub \
  --source-resource-id $TOPIC_ID \
  --max-delivery-attempts 3 \
  --deadletter-endpoint "$DEADLETTER_ID/blobServices/default/containers/deadletter"
```

---

## Step 8 — Test Dead-Lettering

To test dead-lettering, deploy a handler that deliberately fails:

```bash
cd ..
mkdir az204-failing-func && cd az204-failing-func
func init --worker-runtime dotnet-isolated --target-framework net8.0
dotnet add package Microsoft.Azure.Functions.Worker.Extensions.EventGrid
func new --name FailingHandler --template "EventGrid trigger"
```

Edit `FailingHandler.cs`:

```csharp
using Azure.Messaging.EventGrid;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using System;

namespace az204_failing_func
{
    public class FailingHandler
    {
        private readonly ILogger _logger;

        public FailingHandler(ILoggerFactory loggerFactory)
        {
            _logger = loggerFactory.CreateLogger<FailingHandler>();
        }

        [Function("FailingHandler")]
        public void Run([EventGridTrigger] EventGridEvent eventGridEvent)
        {
            _logger.LogInformation("=== FAILING HANDLER RECEIVED EVENT ===");
            _logger.LogInformation($"Event ID: {eventGridEvent.Id}");
            _logger.LogInformation($"Subject: {eventGridEvent.Subject}");

            // Simulate a processing failure
            throw new InvalidOperationException("Simulated processing error - event will be retried and dead-lettered");
        }
    }
}
```

Deploy and create a failing subscription:

```bash
func azure functionapp publish az204-eg-func-16<unique>

FAILING_FUNC_ID=$(az functionapp function show \
  --name az204-eg-func-16<unique> \
  --resource-group az204-day16-rg \
  --function-name FailingHandler \
  --query id --output tsv)

az eventgrid event-subscription create \
  --name failing-handler-sub \
  --source-resource-id $TOPIC_ID \
  --endpoint $FAILING_FUNC_ID \
  --endpoint-type azurefunction \
  --max-delivery-attempts 3 \
  --deadletter-endpoint "$DEADLETTER_ID/blobServices/default/containers/deadletter"
```

Publish a test event:

```bash
curl -X POST $TOPIC_ENDPOINT \
  -H "aeg-sas-key: $TOPIC_KEY" \
  -H "Content-Type: application/json" \
  -d '[{
    "id": "evt-dl-001",
    "eventType": "Order.Created",
    "subject": "/orders/test/failing-order",
    "eventTime": "2026-03-14T10:00:00Z",
    "data": {
      "orderId": "ORD-DL-001",
      "amount": 99.99
    },
    "dataVersion": "1.0"
  }]'
```

Wait 2–3 minutes for retries to complete (3 attempts with exponential backoff).

---

## Step 9 — View Dead-Lettered Events

```bash
az storage blob list \
  --container-name deadletter \
  --account-name az204dlstore<unique> \
  --auth-mode login \
  --query "[].name" \
  --output tsv
```

Download and inspect a dead-lettered event:

```bash
az storage blob download \
  --container-name deadletter \
  --name <blob-name> \
  --file dead-letter-event.json \
  --account-name az204dlstore<unique> \
  --auth-mode login
```

The dead-lettered event will contain the original event data plus metadata about why delivery failed.

---

## Step 10 — Create a Catch-All Subscription

Create a third subscription that receives ALL events (for logging/auditing):

```bash
az eventgrid event-subscription create \
  --name all-events-audit-sub \
  --source-resource-id $TOPIC_ID \
  --endpoint $FUNC_ID \
  --endpoint-type azurefunction
```

This subscription has no filters, so every published event reaches its handler. Use this pattern for audit logging.

---

## Cleanup

```bash
cd ../..

rm -rf az204-premium-func az204-highvalue-func az204-failing-func EventGridPublisher

az group delete --name az204-day16-rg --yes --no-wait
```

---

## Expected Outcomes

| Task | Status |
|------|--------|
| Created custom Event Grid topic | ✅ |
| Published events using .NET SDK | ✅ |
| Created subscription with subject filtering | ✅ |
| Created subscription with advanced data filtering | ✅ |
| Verified filtering (events routed to correct handlers) | ✅ |
| Configured dead-letter endpoint (Storage Blob) | ✅ |
| Tested dead-lettering (failed events captured) | ✅ |
| Viewed dead-lettered events in blob storage | ✅ |
| Created catch-all audit subscription | ✅ |

---

Next: [Day 17 — Final Review](../day-14-final-project/theory.md)
