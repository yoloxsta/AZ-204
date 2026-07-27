# Day 16 — Advanced Event Grid: Domains, Filtering & Dead-Lettering

## What You'll Learn Today

- Event Grid domains for multi-tenant publishing
- Advanced filtering (subject, event type, data filtering)
- Dead-lettering and retry policies
- System topics vs custom topics vs domain topics
- Event Grid with CloudEvents schema
- Input/output mapping and transformation
- Event delivery and guarantees

---

## 1. Event Grid Domains

An Event Grid **domain** is a top-level resource that acts as a logical grouping of multiple Event Grid topics. Domains are designed for **multi-tenant scenarios** where you want to publish events across many tenants through a single managed infrastructure.

### Why Use Domains?

| Scenario | Without Domains | With Domains |
|----------|----------------|--------------|
| 10 tenants, each with events | 10 separate custom topics | 1 domain with 10 domain topics |
| Access control | Manage 10 SAS keys | Domain-level + per-topic keys |
| Subscription management | 10 sets of subscriptions | Centralized or per-topic subscriptions |
| Monitoring | 10 separate diagnostic logs | Unified logging under one resource |

### Domain Architecture

```
                        ┌─────────────────────────────────────┐
                        │       Event Grid Domain             │
                        │       (az204-eg-domain)             │
                        │                                     │
  Tenant A  ──publish──►│  ┌───────────────────────────┐      │
                        │  │ Domain Topic: tenant-a    │      │──► Subscription ──► Handler A
  Tenant B  ──publish──►│  │ Domain Topic: tenant-b    │      │
                        │  ├───────────────────────────┤      │
  Tenant C  ──publish──►│  │ Domain Topic: tenant-c    │      │──► Subscription ──► Handler B
                        │  └───────────────────────────┘      │
                        └─────────────────────────────────────┘
```

### Creating a Domain

```bash
az eventgrid domain create \
  --name az204-eg-domain \
  --resource-group az204-day16-rg \
  --location eastus
```

### Creating a Domain Topic

```bash
az eventgrid domain topic create \
  --name tenant-a \
  --domain-name az204-eg-domain \
  --resource-group az204-day16-rg
```

### Publishing to a Domain Topic

To publish an event to a specific domain topic, include the topic name in the request:

```http
POST https://<domain-endpoint>/topics/<topic-name>:publish
Content-Type: application/json
aeg-sas-key: <domain-key>

[{
  "id": "evt-001",
  "eventType": "Tenant.OrderCreated",
  "subject": "/tenants/tenant-a/orders/12345",
  "eventTime": "2026-03-14T10:00:00Z",
  "data": {
    "orderId": "12345",
    "amount": 299.99,
    "currency": "USD"
  },
  "dataVersion": "1.0"
}]
```

### Domain-Level Access Control

Domains support three access patterns:

| Access Level | Description | Use Case |
|-------------|-------------|----------|
| Domain-level key | Publish to any topic in the domain | Shared publishing service |
| Topic-level key | Publish only to a specific domain topic | Per-tenant publishing |
| Azure RBAC | Use managed identity + role assignments | Zero-secret authentication |

```bash
# Grant a managed identity EventGrid EventHubbingData sender role
az role assignment create \
  --assignee <managed-identity-principal-id> \
  --role "EventGrid EventHubbingData Sender" \
  --scope $(az eventgrid domain show --name az204-eg-domain --resource-group az204-day16-rg --query id --output tsv)
```

### Subscribing to Domain Events

You can subscribe at the domain level or at the topic level:

```bash
# Domain-level subscription (receives ALL events from ALL topics)
az eventgrid event-subscription create \
  --name domain-all-events-sub \
  --source-resource-id $(az eventgrid domain show --name az204-eg-domain --resource-group az204-day16-rg --query id --output tsv) \
  --endpoint <handler-endpoint>

# Topic-level subscription (receives events from ONE topic only)
az eventgrid event-subscription create \
  --name tenant-a-sub \
  --source-resource-id $(az eventgrid domain topic show --name tenant-a --domain-name az204-eg-domain --resource-group az204-day16-rg --query id --output tsv) \
  --endpoint <handler-endpoint>
```

---

## 2. Advanced Filtering

Event Grid subscriptions support multiple filter types that can be combined using logical AND.

### Simple Filters

| Filter Type | Property | Operator | Example |
|------------|----------|----------|---------|
| Event Type | `eventType` | Include/Exclude | `Microsoft.Storage.BlobCreated` |
| Subject | `subject` | Begins with / Ends with | `/blobServices/default/containers/logs/` |
| Advanced | `data.field` | String, Number, Bool, IP, DateTime | `data.amount > 100` |

### Subject Filtering

Subject filtering matches the `subject` property of the event:

```bash
az eventgrid event-subscription create \
  --name advanced-filter-sub \
  --source-resource-id <topic-id> \
  --endpoint <handler> \
  --subject-begins-with "/orders/electronics/" \
  --subject-ends-with "/vip"
```

**Subject filter operators:**

| Operator | Description | Example |
|----------|-------------|---------|
| `--subject-begins-with` | Subject starts with prefix | `/orders/electronics/` |
| `--subject-ends-with` | Subject ends with suffix | `/vip` |
| `--subject-contains` | Subject contains substring | `electronics` |

### Advanced Filtering on Data Fields

Advanced filters let you query the `data` payload of events using typed comparisons.

#### String Comparison

```bash
az eventgrid event-subscription create \
  --name string-filter-sub \
  --source-resource-id <topic-id> \
  --endpoint <handler> \
  --advanced-filter data.customerType stringContains "premium" \
  --advanced-filter data.region stringBeginsWith "us-"
```

#### Number Comparison

```bash
az eventgrid event-subscription create \
  --name number-filter-sub \
  --source-resource-id <topic-id> \
  --endpoint <handler> \
  --advanced-filter data.amount numberGreaterThan 500 \
  --advanced-filter data.quantity numberLessThanOrEquals 10
```

#### Boolean Comparison

```bash
az eventgrid event-subscription create \
  --name bool-filter-sub \
  --source-resource-id <topic-id> \
  --endpoint <handler> \
  --advanced-filter data.isExpressDelivery booleanEquals true
```

#### IP Address Comparison

```bash
az eventgrid event-subscription create \
  --name ip-filter-sub \
  --source-resource-id <topic-id> \
  --endpoint <handler> \
  --advanced-filter data.sourceIp ipInRange "10.0.0.0/16"
```

#### DateTime Comparison

```bash
az eventgrid event-subscription create \
  --name datetime-filter-sub \
  --source-resource-id <topic-id> \
  --endpoint <handler> \
  --advanced-filter data.eventTimestamp isGreaterThan "2026-01-01T00:00:00Z"
```

### Combining Filters

All filters on a single subscription are combined with **AND** logic. To achieve OR logic, create multiple subscriptions.

```
Subscription 1 (AND logic):
  ├── Event Type: Order.Created OR Order.Updated
  ├── Subject begins with: /orders/premium/
  ├── Advanced: data.amount > 1000
  └── Advanced: data.region == "us-east"

Subscription 2 (separate OR branch):
  ├── Event Type: Order.Created
  └── Subject begins with: /orders/international/
```

### Advanced Filtering Data Types Reference

| Data Type | Operators | Example Value |
|-----------|-----------|---------------|
| String | `stringContains`, `stringBeginsWith`, `stringEndsWith`, `stringIn`, `stringNotIn` | `"premium"` |
| Number | `numberGreaterThan`, `numberGreaterThanOrEquals`, `numberLessThan`, `numberLessThanOrEquals`, `numberIn`, `numberNotIn` | `500`, `3.14` |
| Boolean | `booleanEquals` | `true`, `false` |
| IP | `ipInRange`, `ipNotInRange` | `"10.0.0.0/16"` |
| DateTime | `isGreaterThan`, `isGreaterThanOrEquals`, `isLessThan`, `isLessThanOrEquals` | `"2026-01-01T00:00:00Z"` |

---

## 3. Delivery and Retry

Event Grid guarantees **at-least-once** delivery. If an event cannot be delivered on the first attempt, it retries using an exponential backoff policy.

### Retry Policy Parameters

| Parameter | Default | Range | Description |
|-----------|---------|-------|-------------|
| `maxDeliveryAttempts` | 30 | 1–30 | Number of retry attempts before dead-lettering |
| `eventTimeToLiveInMinutes` | 1440 (24h) | 5–1440 | How long an event stays in the retry queue |

### Exponential Backoff Timeline

```
Attempt    Time After First Failure
───────    ─────────────────────────
  1        0 seconds (immediate)
  2        ~10 seconds
  3        ~30 seconds
  4        ~1 minute
  5        ~5 minutes
  6        ~10 minutes
  7        ~30 minutes
  8        ~1 hour
  9        ~2 hours
  ...      ...
  30       ~24 hours (max TTL)
```

### Configuring Retry Policy

```bash
az eventgrid event-subscription create \
  --name retry-policy-sub \
  --source-resource-id <topic-id> \
  --endpoint <handler> \
  --max-delivery-attempts 10 \
  --event-ttl 1440
```

### Delivery Attributes

Each event delivery includes these HTTP headers:

| Header | Description |
|--------|-------------|
| `aeg-event-type` | Array of event types in the batch |
| `aeg-subscription-id` | The subscription ID |
| `aeg-delivery-count` | How many times delivery was attempted (0 = first try) |
| `aeg-metadata` | Custom metadata sent with the event |

### Delivery Status Codes

| Handler Response | Event Grid Action |
|-----------------|-------------------|
| `200 OK` | Event marked as delivered |
| `202 Accepted` | Event marked as delivered |
| `4xx` (except 400, 403) | Retry with exponential backoff |
| `5xx` | Retry with exponential backoff |
| `400 Bad Request` | Event dropped (not retried) |
| `403 Forbidden` | Event dropped (not retried) |
| No response (timeout) | Retry with exponential backoff |

---

## 4. Dead-Lettering

When an event fails all retry attempts, Event Grid can send it to a **dead-letter endpoint** instead of dropping it.

### Supported Dead-Letter Endpoints

| Endpoint Type | Use Case | Setup Complexity |
|--------------|----------|-----------------|
| Storage Blob | Durable storage, batch analysis | Low |
| Storage Queue | Integration with queue processors | Medium |
| Event Hub | Stream processing of failed events | High |

### Configuring Dead-Lettering with Storage Blob

```bash
# Create dead-letter storage account and container
az storage account create \
  --name az204deadletter<unique> \
  --resource-group az204-day16-rg \
  --location eastus \
  --sku Standard_LRS

az storage container create \
  --name deadletter \
  --account-name az204deadletter<unique> \
  --auth-mode login

# Get the storage account resource ID
DEADLETTER_ID=$(az storage account show \
  --name az204deadletter<unique> \
  --resource-group az204-day16-rg \
  --query id --output tsv)

# Create subscription with dead-letter endpoint
az eventgrid event-subscription create \
  --name dl-sub \
  --source-resource-id <topic-id> \
  --endpoint <handler> \
  --max-delivery-attempts 5 \
  --deadletter-endpoint "$DEADLETTER_ID/blobServices/default/containers/deadletter"
```

### Dead-Letter Event Envelope

Dead-lettered events are wrapped in a dead-letter envelope:

```json
{
  "id": "original-event-id",
  "eventType": "Order.Created",
  "subject": "/orders/premium/12345",
  "eventTime": "2026-03-14T10:00:00Z",
  "data": {
    "orderId": "12345",
    "amount": 299.99
  },
  "dataVersion": "1.0",
  "metadata": {
    "deadLetterReason": "MaxDeliveryAttemptsExceeded",
    "deliveryCount": 5,
    "lastDeliveryErrorMessage": "Handler returned 500 Internal Server Error"
  }
}
```

### Dead-Lettering with Storage Queue

```bash
az storage queue create \
  --name deadletter-queue \
  --account-name az204deadletter<unique>

az eventgrid event-subscription create \
  --name dl-queue-sub \
  --source-resource-id <topic-id> \
  --endpoint <handler> \
  --max-delivery-attempts 5 \
  --deadletter-endpoint "$(az storage account show --name az204deadletter<unique> --resource-group az204-day16-rg --query id --output tsv)/queueServices/default/queues/deadletter-queue"
```

### Dead-Lettering with Event Hub

```bash
az eventhubs namespace create \
  --name az204-deadhubs<unique> \
  --resource-group az204-day16-rg \
  --location eastus \
  --sku Standard

az eventhubs eventhub create \
  --name deadletter-hub \
  --namespace-name az204-deadhubs<unique> \
  --resource-group az204-day16-rg

az eventgrid event-subscription create \
  --name dl-hub-sub \
  --source-resource-id <topic-id> \
  --endpoint <handler> \
  --max-delivery-attempts 5 \
  --deadletter-endpoint "$(az eventhubs namespace show --name az204-deadhubs<unique> --resource-group az204-day16-rg --query id --output tsv)/eventhubs/deadletter-hub"
```

---

## 5. Input/Output Mapping

Event Grid supports **input mapping** to transform event properties before delivery. This is useful when your handler expects a specific event shape.

### Input Mapping for Custom Topics

When publishing to a custom topic, you can define an input schema mapping that transforms the published event into the schema your handler expects.

```bash
az eventgrid event-subscription create \
  --name mapped-sub \
  --source-resource-id <topic-id> \
  --endpoint <handler> \
  --event-delivery-schema EventGridSchema
```

### Input Mapping Configuration

Input mappings are configured at the topic level using the Azure CLI or ARM template:

```json
{
  "properties": {
    "inputSchema": "CustomEventSchema",
    "inputSchemaMapping": {
      "mappingType": "EventGridSchema",
      "fields": {
        "id": "=$.id",
        "eventType": "=$.eventType",
        "subject": "=$.subject",
        "eventTime": "=$.eventTime",
        "dataVersion": "=$.dataVersion",
        "data": "=$.data"
      }
    }
  }
}
```

### Custom Schema to Event Grid Schema

Publish events in your own schema, and Event Grid transforms them:

```
Publisher sends:                    Event Grid delivers:
┌─────────────────────┐            ┌─────────────────────────┐
│ {                   │            │ {                       │
│   "myEventId": "1" │  ────────► │   "id": "1"             │
│   "myType": "Order"│  Mapping   │   "eventType": "Order"  │
│   "myData": {...}  │            │   "data": {...}         │
│ }                   │            │   "dataVersion": "1.0"  │
└─────────────────────┘            │ }                       │
                                   └─────────────────────────┘
```

### Input Schema Options

| Schema | Description | When to Use |
|--------|-------------|-------------|
| `EventGridSchema` | Standard Event Grid envelope | Default, most handlers expect this |
| `CloudEventsSchema` | CloudEvents v1.0 envelope | Cross-platform, CNCF standard |
| `CustomEventSchema` | Your own envelope + mapping | Legacy systems with custom formats |

---

## 6. System Topics Deep Dive

System topics are automatically created when you enable event publishing for supported Azure services.

### Blob Storage Events

| Event Type | When |
|-----------|------|
| `Microsoft.Storage.BlobCreated` | Blob created or overwritten |
| `Microsoft.Storage.BlobDeleted` | Blob deleted |
| `Microsoft.Storage.BlobRenamed` | Blob renamed (Hierarchical Namespace) |
| `Microsoft.Storage.BlobTierChanged` | Access tier changed |

**Event data payload:**

```json
{
  "api": "PutBlob",
  "clientRequestId": "6d79dbfb-0e3d-4a63-9d81-5fbed7c565cf",
  "requestId": "d8c979ff-9d53-4856-9ef4-40552f48e114",
  "eTag": "0x8D45B5D4277CFEF",
  "contentType": "application/octet-stream",
  "contentLength": 524288,
  "contentOffset": 0,
  "blobType": "BlockBlob",
  "url": "https://myaccount.blob.core.windows.net/container/blob.txt"
}
```

### Resource Group Events

| Event Type | When |
|-----------|------|
| `Microsoft.Resources.ResourceWriteSuccess` | Resource create/update succeeded |
| `Microsoft.Resources.ResourceWriteFailure` | Resource create/update failed |
| `Microsoft.Resources.ResourceDeleteSuccess` | Resource deleted |
| `Microsoft.Resources.ResourceDeleteFailure` | Resource deletion failed |
| `Microsoft.Resources.ResourceActionSuccess` | Resource action succeeded |

### Key Vault Events

| Event Type | When |
|-----------|------|
| `Microsoft.KeyVault.SecretNearExpiry` | Secret within 30 days of expiry |
| `Microsoft.KeyVault.SecretExpired` | Secret has expired |
| `Microsoft.KeyVault.SecretNewVersionCreated` | New secret version created |
| `Microsoft.KeyVault.CertificateNearExpiry` | Certificate within 30 days of expiry |
| `Microsoft.KeyVault.CertificateExpired` | Certificate has expired |
| `Microsoft.KeyVault.KeyNearExpiry` | Key within 30 days of expiry |

### Container Registry Events

| Event Type | When |
|-----------|------|
| `Microsoft.ContainerRegistry.ImagePushed` | Image pushed to registry |
| `Microsoft.ContainerRegistry.ImageDeleted` | Image deleted from registry |

---

## 7. Custom Topics

Custom topics let you define your own event types and publish from any application.

### Creating a Custom Topic

```bash
az eventgrid topic create \
  --name az204-custom-topic \
  --resource-group az204-day16-rg \
  --location eastus \
  --kind EventGrid
```

### Publishing Events with .NET SDK

```bash
dotnet add package Azure.Messaging.EventGrid
```

```csharp
using Azure;
using Azure.Messaging.EventGrid;
using Azure.Messaging.EventGrid.Models;

var endpoint = new Uri("https://<topic-endpoint>");
var credential = new AzureKeyCredential("<topic-key>");
var client = new EventGridPublisherClient(endpoint, credential);

var events = new List<EventGridEvent>
{
    new EventGridEvent(
        subject: "/orders/premium/12345",
        eventType: "Order.Created",
        dataVersion: "1.0",
        data: BinaryData.FromObjectAsJson(new
        {
            orderId = "12345",
            product = "Laptop Pro",
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
    new EventGridEvent(
        subject: "/orders/standard/12346",
        eventType: "Order.Created",
        dataVersion: "1.0",
        data: BinaryData.FromObjectAsJson(new
        {
            orderId = "12346",
            product = "Wireless Mouse",
            quantity = 2,
            amount = 29.98,
            currency = "USD",
            customerType = "standard",
            region = "eu-west"
        }))
    {
        Id = "evt-002",
        EventTime = DateTimeOffset.UtcNow
    }
};

await client.SendEventsAsync(events);
Console.WriteLine($"Published {events.Count} events");
```

### Publishing via HTTP REST

```http
POST https://<topic-endpoint>/api/events
Content-Type: application/json
aeg-sas-key: <access-key>

[{
  "id": "evt-001",
  "eventType": "Order.Created",
  "subject": "/orders/premium/12345",
  "eventTime": "2026-03-14T10:00:00Z",
  "data": {
    "orderId": "12345",
    "amount": 1299.99,
    "currency": "USD"
  },
  "dataVersion": "1.0"
}]
```

### Publishing with Managed Identity (RBAC)

```csharp
using Azure.Identity;

var credential = new DefaultAzureCredential();
var client = new EventGridPublisherClient(endpoint, credential);

await client.SendEventsAsync(events);
```

**Required role:** `EventGrid Data Sender` on the topic resource.

---

## 8. CloudEvents Schema

CloudEvents is a CNCF specification for describing event data in a common way. Event Grid supports it natively.

### Event Grid Schema vs CloudEvents Schema

| Property | Event Grid Schema | CloudEvents Schema |
|----------|------------------|-------------------|
| ID | `id` | `id` |
| Event type | `eventType` | `type` |
| Subject | `subject` | `source` + `subject` |
| Timestamp | `eventTime` | `time` |
| Data | `data` | `data` |
| Data version | `dataVersion` | N/A (use `datacontenttype`) |
| Spec version | N/A | `specversion` |
| Content type | N/A | `datacontenttype` |

### CloudEvents Event Example

```json
{
  "specversion": "1.0",
  "type": "com.contoso.order.created",
  "source": "/contoso/orders",
  "id": "evt-001",
  "time": "2026-03-14T10:00:00.0000000Z",
  "datacontenttype": "application/json",
  "subject": "/orders/premium/12345",
  "data": {
    "orderId": "12345",
    "amount": 1299.99,
    "currency": "USD"
  }
}
```

### Creating a CloudEvents Topic

```bash
az eventgrid topic create \
  --name az204-cloudevents-topic \
  --resource-group az204-day16-rg \
  --location eastus \
  --kind EventGrid \
  --input-schema CloudEventSchemaV1_0
```

### When to Use Each Schema

| Use Case | Recommended Schema |
|----------|-------------------|
| Azure-native handlers (Functions, Logic Apps) | Event Grid Schema |
| Cross-platform, vendor-neutral | CloudEvents Schema |
| Kubernetes event-driven architecture | CloudEvents Schema |
| Existing Event Grid integrations | Event Grid Schema |
| CNCF ecosystem (Knative, Dapr) | CloudEvents Schema |

---

## 9. Event Grid vs Event Hubs vs Service Bus

### Comprehensive Comparison

| Feature | Event Grid | Event Hubs | Service Bus |
|---------|-----------|------------|-------------|
| **Pattern** | Event notification (pub-sub) | Big data streaming | Enterprise messaging |
| **Throughput** | Moderate (thousands/sec) | Very high (millions/sec) | Moderate (thousands/sec) |
| **Ordering** | No guarantee | Per partition | Session-based FIFO |
| **Delivery** | At-least-once | At-least-once | At-most-once (or PEEK-LOCK) |
| **Retention** | Retry up to 24h | 1–90 days | Max 14 days |
| **Filtering** | Advanced (subject, type, data) | Consumer groups only | SQL filters, subscription rules |
| **Dead-letter** | Blob, Queue, Event Hub | Capture to Blob | Dead-letter queue (built-in) |
| **Batching** | Up to 5000 events per call | Built-in batching | Batch receive |
| **Consumer model** | Push (webhook/subscription) | Pull (receiver reads) | Push or Pull (both supported) |
| **Protocols** | HTTP, AMQP, MQTT | AMQP, Kafka | AMQP, SBMP |
| **Auth** | SAS, RBAC, Managed Identity | SAS, RBAC, Managed Identity | SAS, RBAC, Managed Identity |
| **Use case** | React to changes, fan-out | Telemetry, logs, analytics | Command processing, workflows |

### Decision Matrix

```
Do you need to react to Azure resource events?
  └─► YES ──► Event Grid (system topics)

Do you need to ingest millions of events/sec?
  └─► YES ──► Event Hubs

Do you need guaranteed FIFO ordering?
  └─► YES ──► Service Bus (with sessions)

Do you need pub-sub with advanced filtering?
  └─► YES ──► Event Grid (custom topics)

Do you need request-reply or two-way communication?
  └─► YES ──► Service Bus (queues with sessions)

Do you need long-term event retention (days/weeks)?
  └─► YES ──► Event Hubs (with Capture)
```

---

## 10. Architecture — Advanced Event Grid

```
┌─────────────────────────────────────────────────────────────────────────────────┐
│                                                                                 │
│   PUBLISHERS                                                                    │
│                                                                                 │
│   ┌──────────────┐  ┌──────────────┐  ┌──────────────┐  ┌──────────────┐       │
│   │   Azure      │  │   .NET       │  │  Third-Party │  │  Kubernetes  │       │
│   │   Blob       │  │   App        │  │  SaaS App    │  │  Cluster     │       │
│   │   Storage    │  │              │  │              │  │              │       │
│   └──────┬───────┘  └──────┬───────┘  └──────┬───────┘  └──────┬───────┘       │
│          │                 │                 │                 │                │
│          ▼                 ▼                 ▼                 ▼                │
│   ┌──────────────────────────────────────────────────────────────────────┐      │
│   │                      Event Grid DOMAIN                               │      │
│   │                                                                      │      │
│   │  ┌──────────────┐  ┌──────────────┐  ┌──────────────┐              │      │
│   │  │  System      │  │  Domain      │  │  Domain      │              │      │
│   │  │  Topic       │  │  Topic A     │  │  Topic B     │              │      │
│   │  │  (Blob)      │  │  (Tenant A)  │  │  (Tenant B)  │              │      │
│   │  └──────┬───────┘  └──────┬───────┘  └──────┬───────┘              │      │
│   │         │                 │                 │                       │      │
│   │         │    ┌────────────┼─────────────────┼─────────────────┐    │      │
│   │         │    │            │                 │                 │    │      │
│   │         ▼    ▼            ▼                 ▼                 ▼    │      │
│   │  ┌──────────────┐  ┌──────────────┐  ┌──────────────┐            │      │
│   │  │ Subscription │  │ Subscription │  │ Subscription │            │      │
│   │  │ (Subject:    │  │ (Advanced:   │  │ (Domain      │            │      │
│   │  │  *.jpg)      │  │  amount>500) │  │  level)      │            │      │
│   │  └──────┬───────┘  └──────┬───────┘  └──────┬───────┘            │      │
│   │         │                 │                 │                     │      │
│   │         ▼                 ▼                 ▼                     │      │
│   └──────────────────────────────────────────────────────────────────────┘      │
│          │                 │                 │                                  │
│          ▼                 ▼                 ▼                                  │
│   ┌──────────────┐  ┌──────────────┐  ┌──────────────┐                        │
│   │ Azure        │  │ Azure        │  │ Azure Logic  │                        │
│   │ Function     │  │ Function     │  │ App          │                        │
│   │ (Image       │  │ (Order       │  │ (Multi-      │                        │
│   │  Processor)  │  │  Processor)  │  │  Tenant)     │                        │
│   └──────┬───────┘  └──────┬───────┘  └──────┬───────┘                        │
│          │                 │                 │                                  │
│          │ (on failure)    │ (on failure)    │ (on failure)                    │
│          ▼                 ▼                 ▼                                  │
│   ┌──────────────────────────────────────────────────────────────────────┐     │
│   │                     DEAD-LETTER ENDPOINT                             │     │
│   │                                                                      │     │
│   │  ┌──────────────┐  ┌──────────────┐  ┌──────────────┐              │     │
│   │  │ Storage Blob │  │ Storage      │  │ Event Hub    │              │     │
│   │  │ (deadletter) │  │ Queue        │  │ (stream)     │              │     │
│   │  └──────────────┘  └──────────────┘  └──────────────┘              │     │
│   └──────────────────────────────────────────────────────────────────────┘     │
│                                                                                 │
└─────────────────────────────────────────────────────────────────────────────────┘
```

---

## 11. Key Concepts for AZ-204

### Must-Know for the Exam

| Concept | Details |
|---------|---------|
| Event Grid Domain | Multi-tenant event routing with per-topic subscriptions |
| Advanced Filtering | String, number, boolean, IP, datetime comparisons on data fields |
| Dead-Lettering | Storage Blob, Storage Queue, or Event Hub for failed events |
| Retry Policy | Exponential backoff, max 30 attempts, 24h TTL |
| Event Schema | Event Grid schema vs CloudEvents schema |
| Input Mapping | Transform custom schemas to Event Grid/CloudEvents schema |
| System Topics | Blob, Resource Group, Key Vault, Container Registry events |
| Custom Topics | Publisher SDK, HTTP REST, managed identity authentication |
| At-least-once | Event Grid guarantees delivery but no ordering |
| RBAC | EventGrid Data Sender / EventGrid Data Contributor roles |

### Common Exam Scenarios

1. **Multi-tenant SaaS** — Use Event Grid Domains with domain topics per tenant
2. **Event filtering** — Use advanced filters to route events to specific handlers
3. **Failed event handling** — Configure dead-lettering to capture events that fail processing
4. **Schema transformation** — Use input mapping to convert custom schemas
5. **Cross-platform events** — Use CloudEvents schema for vendor-neutral eventing
6. **Key Vault rotation** — Subscribe to `SecretNearExpiry` events for automated rotation
7. **Blob processing pipeline** — Filter by subject to route different file types to different handlers

### Useful CLI Commands

```bash
# List all Event Grid topic types
az eventgrid topic list --query "[].kind" -o tsv

# Show subscription filters
az eventgrid event-subscription show \
  --name <sub-name> \
  --source-resource-id <topic-id> \
  --query "{filters:filter,endpoint:endpoint}" -o json

# Enable diagnostic logging on a topic
az monitor diagnostic-settings create \
  --name eventgrid-diagnostics \
  --resource <topic-id> \
  --workspace <log-analytics-id> \
  --logs '[{"category":"EventGridLogs","enabled":true}]'

# Test event delivery (simulate a failure)
az eventgrid event-subscription test-full-url \
  --name <sub-name> \
  --source-resource-id <topic-id> \
  --endpoint <handler-url>
```

### SDK Packages

| Package | Purpose |
|---------|---------|
| `Azure.Messaging.EventGrid` | Publish events (custom topics, domains) |
| `Microsoft.Azure.WebJobs.Extensions.EventGrid` | Azure Functions trigger binding |
| `Azure.Messaging.EventGrid.SystemEvents` | Deserialize system event data |

---

## Key Takeaways

1. **Domains** enable multi-tenant event routing with per-topic isolation and access control
2. **Advanced filtering** uses typed comparisons (string, number, bool, IP, datetime) on event data
3. **Dead-lettering** captures events that fail all retry attempts for later analysis
4. **Retry policy** uses exponential backoff (up to 30 attempts, 24-hour TTL)
5. **CloudEvents** is a vendor-neutral alternative to the Event Grid schema
6. **Input mapping** transforms custom publisher schemas into the format handlers expect
7. **System topics** cover Blob Storage, Key Vault, Container Registry, and Resource Group events
8. **Event Grid vs Event Hubs vs Service Bus** — know when to use each based on throughput, ordering, and delivery guarantees

---

Next: [Lab — Advanced Event Grid Patterns](./lab-event-grid-advanced.md)
