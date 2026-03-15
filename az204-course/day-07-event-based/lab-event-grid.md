# Lab: Azure Event Grid

## Title
React to Azure Events with Event Grid

## Objective
- Create a custom Event Grid topic
- Subscribe to blob storage events (system topic)
- Route events to an Azure Function
- Filter events by type and subject
- Test event delivery and view event data

## Architecture

```
┌──────────────────────────────────────────────────────────┐
│              Resource Group: az204-day07-rg               │
│                                                          │
│  ┌──────────────┐   System Topic    ┌────────────────┐  │
│  │   Storage    │ ─────────────────►│  Event Grid    │  │
│  │   Account    │  BlobCreated      │  Subscription  │  │
│  │              │  BlobDeleted      │                │  │
│  └──────────────┘                   │  Filter:       │  │
│                                     │  *.jpg, *.png  │  │
│                                     └───────┬────────┘  │
│                                             │           │
│  ┌──────────────┐   Custom Topic    ┌───────▼────────┐  │
│  │  Publisher    │ ────────────────►│  Azure Function│  │
│  │  (CLI/code)  │  OrderCreated     │  (Event Handler│  │
│  └──────────────┘                   │   logs events) │  │
│                                     └────────────────┘  │
└──────────────────────────────────────────────────────────┘
```

## Prerequisites
- Azure CLI installed and logged in
- Azure Functions Core Tools v4 installed

---

## Step 1 — Create Resources

```bash
az group create --name az204-day07-rg --location eastus

# Register Event Grid provider
az provider register --namespace Microsoft.EventGrid

# Create storage account (event source)
az storage account create \
  --name az204evtstore<unique> \
  --resource-group az204-day07-rg \
  --location eastus \
  --sku Standard_LRS

# Create storage account for Function App
az storage account create \
  --name az204evtfunc<unique> \
  --resource-group az204-day07-rg \
  --location eastus \
  --sku Standard_LRS

# Create Function App (event handler)
az functionapp create \
  --name az204-evt-func-<unique> \
  --resource-group az204-day07-rg \
  --storage-account az204evtfunc<unique> \
  --consumption-plan-location eastus \
  --runtime dotnet-isolated \
  --runtime-version 8 \
  --functions-version 4 \
  --os-type Linux
```

---

## Step 2 — Create an Event Grid Function Locally

```bash
mkdir az204-eventgrid-func
cd az204-eventgrid-func
func init --worker-runtime dotnet-isolated --target-framework net8.0
```

Add the Event Grid extension:
```bash
dotnet add package Microsoft.Azure.Functions.Worker.Extensions.EventGrid
```

Create the function:
```bash
func new --name BlobEventHandler --template "EventGrid trigger"
```

Edit `BlobEventHandler.cs`:

```csharp
using Azure.Messaging.EventGrid;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace az204_eventgrid_func
{
    public class BlobEventHandler
    {
        private readonly ILogger _logger;

        public BlobEventHandler(ILoggerFactory loggerFactory)
        {
            _logger = loggerFactory.CreateLogger<BlobEventHandler>();
        }

        [Function("BlobEventHandler")]
        public void Run([EventGridTrigger] EventGridEvent eventGridEvent)
        {
            _logger.LogInformation("=== Event Grid Event Received ===");
            _logger.LogInformation($"Event Type: {eventGridEvent.EventType}");
            _logger.LogInformation($"Subject: {eventGridEvent.Subject}");
            _logger.LogInformation($"Time: {eventGridEvent.EventTime}");
            _logger.LogInformation($"Data: {eventGridEvent.Data}");
        }
    }
}
```

---

## Step 3 — Deploy the Function

```bash
func azure functionapp publish az204-evt-func-<unique>
```

Get the function URL:
```bash
az functionapp function show \
  --name az204-evt-func-<unique> \
  --resource-group az204-day07-rg \
  --function-name BlobEventHandler \
  --query invokeUrlTemplate \
  --output tsv
```

---

## Step 4 — Subscribe to Blob Storage Events

Create a system topic for the storage account:
```bash
STORAGE_ID=$(az storage account show \
  --name az204evtstore<unique> \
  --resource-group az204-day07-rg \
  --query id --output tsv)

az eventgrid system-topic create \
  --name blob-events-topic \
  --resource-group az204-day07-rg \
  --source $STORAGE_ID \
  --topic-type Microsoft.Storage.StorageAccounts \
  --location eastus
```

Create an event subscription with filtering:
```bash
FUNC_ID=$(az functionapp function show \
  --name az204-evt-func-<unique> \
  --resource-group az204-day07-rg \
  --function-name BlobEventHandler \
  --query id --output tsv)

az eventgrid system-topic event-subscription create \
  --name blob-sub \
  --resource-group az204-day07-rg \
  --system-topic-name blob-events-topic \
  --endpoint $FUNC_ID \
  --endpoint-type azurefunction \
  --included-event-types Microsoft.Storage.BlobCreated Microsoft.Storage.BlobDeleted \
  --subject-begins-with /blobServices/default/containers/images/ \
  --subject-ends-with .jpg .png
```

This subscription:
- Only triggers for BlobCreated and BlobDeleted events
- Only for blobs in the `images` container
- Only for `.jpg` and `.png` files

---

## Step 5 — Test Blob Events

Create the images container and upload a file:
```bash
az storage container create \
  --name images \
  --account-name az204evtstore<unique>

echo "fake image data" > test.jpg

az storage blob upload \
  --container-name images \
  --name photo.jpg \
  --file test.jpg \
  --account-name az204evtstore<unique>
```

Check the function logs to see the event:
```bash
az webapp log tail \
  --name az204-evt-func-<unique> \
  --resource-group az204-day07-rg
```

You should see the BlobCreated event with subject, type, and data.

Upload a non-matching file (should NOT trigger):
```bash
echo "text data" > notes.txt
az storage blob upload \
  --container-name images \
  --name notes.txt \
  --file notes.txt \
  --account-name az204evtstore<unique>
```

This won't trigger the function because the filter requires `.jpg` or `.png`.

---

## Step 6 — Create a Custom Topic

```bash
az eventgrid topic create \
  --name az204-custom-topic \
  --resource-group az204-day07-rg \
  --location eastus
```

Get the topic endpoint and key:
```bash
TOPIC_ENDPOINT=$(az eventgrid topic show \
  --name az204-custom-topic \
  --resource-group az204-day07-rg \
  --query endpoint --output tsv)

TOPIC_KEY=$(az eventgrid topic key list \
  --name az204-custom-topic \
  --resource-group az204-day07-rg \
  --query key1 --output tsv)

echo "Endpoint: $TOPIC_ENDPOINT"
echo "Key: $TOPIC_KEY"
```

Subscribe the function to the custom topic:
```bash
az eventgrid event-subscription create \
  --name custom-sub \
  --source-resource-id $(az eventgrid topic show --name az204-custom-topic --resource-group az204-day07-rg --query id --output tsv) \
  --endpoint $FUNC_ID \
  --endpoint-type azurefunction
```

---

## Step 7 — Publish Custom Events

Send a custom event using curl:
```bash
curl -X POST $TOPIC_ENDPOINT \
  -H "aeg-sas-key: $TOPIC_KEY" \
  -H "Content-Type: application/json" \
  -d '[{
    "id": "order-001",
    "eventType": "Order.Created",
    "subject": "orders/electronics/laptop",
    "eventTime": "2026-03-14T10:00:00Z",
    "data": {
      "orderId": "ORD-001",
      "product": "Laptop Pro",
      "quantity": 1,
      "total": 1299.99
    },
    "dataVersion": "1.0"
  }]'
```

Check the function logs — you should see the custom event.

Send multiple events:
```bash
curl -X POST $TOPIC_ENDPOINT \
  -H "aeg-sas-key: $TOPIC_KEY" \
  -H "Content-Type: application/json" \
  -d '[
    {
      "id": "order-002",
      "eventType": "Order.Created",
      "subject": "orders/furniture/desk",
      "eventTime": "2026-03-14T10:01:00Z",
      "data": {"orderId": "ORD-002", "product": "Standing Desk", "total": 499.99},
      "dataVersion": "1.0"
    },
    {
      "id": "order-003",
      "eventType": "Order.Cancelled",
      "subject": "orders/electronics/mouse",
      "eventTime": "2026-03-14T10:02:00Z",
      "data": {"orderId": "ORD-003", "product": "Wireless Mouse", "reason": "Out of stock"},
      "dataVersion": "1.0"
    }
  ]'
```

---

## Cleanup

```bash
cd ..
rm -rf az204-eventgrid-func test.jpg notes.txt

az group delete --name az204-day07-rg --yes --no-wait
```

---

## Expected Outcomes

| Task | Status |
|------|--------|
| Created Event Grid system topic for blob events | ✅ |
| Created event subscription with filters | ✅ |
| Triggered function from blob upload | ✅ |
| Verified filtering works (non-matching files ignored) | ✅ |
| Created custom Event Grid topic | ✅ |
| Published custom events via HTTP | ✅ |
| Function processed all events | ✅ |

---

Next: [Day 08 — Message-Based Solutions](../day-08-messaging/theory.md)
