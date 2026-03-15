# Exploring Azure Event Hubs — Deep Dive

## Overview

This module covers the Event Hubs topics that appear on the AZ-204 exam: architecture, Capture, scaling, access control, and the client library.

---

## 1. Discover Azure Event Hubs

Event Hubs is a big data streaming platform and event ingestion service. It can receive and process millions of events per second.

### Key Concepts

| Concept | Description |
|---------|-------------|
| **Namespace** | Container for one or more Event Hubs (like a server) |
| **Event Hub** | A stream of events (like a topic/feed) |
| **Partition** | An ordered sequence of events within an Event Hub |
| **Consumer Group** | An independent view/cursor into the event stream |
| **Event** | A single data record (up to 1 MB) |
| **Throughput Unit (TU)** | Capacity unit: 1 TU = 1 MB/s ingress, 2 MB/s egress |

### Architecture

```
Producers                Event Hub                    Consumers
─────────               ──────────                   ─────────
App 1 ──────►  ┌──────────────────────┐  ──────►  Consumer Group A
App 2 ──────►  │  Partition 0: ██████ │  ──────►    ├── Reader 0
IoT Device ──► │  Partition 1: ████   │             ├── Reader 1
               │  Partition 2: ██████ │  ──────►  Consumer Group B
               │  Partition 3: █████  │             └── Reader 0
               └──────────────────────┘
                         │
                    (Capture)
                         ▼
                   Blob Storage /
                   Data Lake
```

### Tiers

| Tier | Partitions | Retention | Throughput |
|------|-----------|-----------|-----------|
| **Basic** | 32 max | 1 day | 1-20 TUs |
| **Standard** | 32 max | 7 days (max) | 1-40 TUs |
| **Premium** | 100 max | 90 days | Processing Units |
| **Dedicated** | 1024 max | 90 days | Capacity Units |

### Partitions

- Events are distributed across partitions
- Each partition is an independent ordered sequence
- Ordering is guaranteed within a partition, not across partitions
- Partition count is set at creation and cannot be changed (Standard tier)
- Use a partition key to group related events into the same partition

---

## 2. Explore Event Hubs Capture

Capture automatically saves event data to Azure Blob Storage or Azure Data Lake Storage.

### How Capture Works

- Events are saved in Apache Avro format
- Configurable time window (1-15 minutes) and size window (10-500 MB)
- Zero-admin: no code needed, just enable and configure
- Does not affect throughput or latency

### Capture Path Format

```
{Namespace}/{EventHub}/{PartitionId}/{Year}/{Month}/{Day}/{Hour}/{Minute}/{Second}
```

Example:
```
myns/myeventhub/0/2026/03/15/10/30/00.avro
```

### Enable Capture via CLI

```bash
az eventhubs eventhub update \
  --name myeventhub \
  --namespace-name myns \
  --resource-group myRG \
  --enable-capture true \
  --capture-interval 300 \
  --capture-size-limit 314572800 \
  --destination-name EventHubArchive.AzureBlockBlob \
  --storage-account mystorageaccount \
  --blob-container capture-container \
  --archive-name-format "{Namespace}/{EventHub}/{PartitionId}/{Year}/{Month}/{Day}/{Hour}/{Minute}/{Second}"
```

---

## 3. Scale Your Processing Application

### Event Processor

The `EventProcessorClient` (.NET) handles:
- Reading events from all partitions
- Load balancing across multiple consumers
- Checkpointing (tracking read position)
- Fault tolerance (rebalancing when consumers fail)

### Scaling Pattern

```
Event Hub (4 partitions)
    │
    ├── Partition 0 ──► Consumer Instance A
    ├── Partition 1 ──► Consumer Instance A
    ├── Partition 2 ──► Consumer Instance B
    └── Partition 3 ──► Consumer Instance B
```

- Maximum parallelism = number of partitions
- If you have 4 partitions, at most 4 consumers in a group can read simultaneously
- Adding a 5th consumer means one will be idle
- The processor automatically rebalances when consumers join or leave

### Checkpointing

Checkpointing marks the last successfully processed event per partition. If a consumer fails, the new consumer resumes from the checkpoint.

Checkpoints are stored in Azure Blob Storage:
```csharp
// Checkpoint after processing a batch
await args.UpdateCheckpointAsync(args.CancellationToken);
```

---

## 4. Control Access to Events

### Authentication Options

| Method | Use Case |
|--------|----------|
| **Shared Access Signature (SAS)** | Simple, token-based |
| **Microsoft Entra ID (RBAC)** | Recommended, identity-based |
| **Managed Identity** | Azure-hosted apps |

### RBAC Roles

| Role | Permissions |
|------|------------|
| Azure Event Hubs Data Owner | Full access (send, receive, manage) |
| Azure Event Hubs Data Sender | Send events |
| Azure Event Hubs Data Receiver | Receive events |

```bash
# Grant send access to a managed identity
az role assignment create \
  --assignee <principalId> \
  --role "Azure Event Hubs Data Sender" \
  --scope <eventhub-resource-id>
```

### SAS Policies

```bash
# Create a send-only policy
az eventhubs eventhub authorization-rule create \
  --name SendPolicy \
  --namespace-name myns \
  --resource-group myRG \
  --eventhub-name myeventhub \
  --rights Send
```

---

## 5. Common Operations with the Event Hubs Client Library

### NuGet Package

```bash
dotnet add package Azure.Messaging.EventHubs
dotnet add package Azure.Messaging.EventHubs.Processor
dotnet add package Azure.Identity
```

### Sending Events

```csharp
using Azure.Messaging.EventHubs;
using Azure.Messaging.EventHubs.Producer;
using Azure.Identity;

var producerClient = new EventHubProducerClient(
    "myns.servicebus.windows.net",
    "myeventhub",
    new DefaultAzureCredential());

// Create a batch
using EventDataBatch batch = await producerClient.CreateBatchAsync();

for (int i = 0; i < 10; i++)
{
    var eventData = new EventData($"Event {i}");
    eventData.Properties["source"] = "az204-lab";

    if (!batch.TryAdd(eventData))
    {
        // Batch is full, send it and create a new one
        await producerClient.SendAsync(batch);
        batch = await producerClient.CreateBatchAsync();
        batch.TryAdd(eventData);
    }
}

await producerClient.SendAsync(batch);
await producerClient.DisposeAsync();
```

### Sending with Partition Key

```csharp
// All events with the same partition key go to the same partition
var options = new SendEventOptions { PartitionKey = "user-123" };
await producerClient.SendAsync(events, options);
```

### Receiving Events with EventProcessorClient

```csharp
using Azure.Messaging.EventHubs;
using Azure.Messaging.EventHubs.Consumer;
using Azure.Messaging.EventHubs.Processor;
using Azure.Storage.Blobs;
using Azure.Identity;

var credential = new DefaultAzureCredential();

var blobClient = new BlobContainerClient(
    new Uri("https://mystorage.blob.core.windows.net/checkpoints"),
    credential);

var processor = new EventProcessorClient(
    blobClient,
    EventHubConsumerClient.DefaultConsumerGroupName,
    "myns.servicebus.windows.net",
    "myeventhub",
    credential);

processor.ProcessEventAsync += async (args) =>
{
    Console.WriteLine($"Partition: {args.Partition.PartitionId}");
    Console.WriteLine($"Data: {args.Data.EventBody}");

    // Checkpoint every 10 events
    if (args.Data.SequenceNumber % 10 == 0)
    {
        await args.UpdateCheckpointAsync(args.CancellationToken);
    }
};

processor.ProcessErrorAsync += (args) =>
{
    Console.WriteLine($"Error: {args.Exception.Message}");
    return Task.CompletedTask;
};

await processor.StartProcessingAsync();
// ... run until stopped
await processor.StopProcessingAsync();
```

---

## Event Hubs vs Service Bus vs Event Grid

| Feature | Event Hubs | Service Bus | Event Grid |
|---------|-----------|-------------|-----------|
| Pattern | Streaming | Messaging | Event routing |
| Throughput | Millions/sec | Moderate | Moderate |
| Ordering | Per partition | Per session | None |
| Retention | 1-90 days | Until consumed | Retry 24h |
| Consumer model | Pull (read stream) | Pull (receive msg) | Push (webhook) |
| Replay | Yes (seek to offset) | No | No |
| Use case | Telemetry, IoT, logs | Orders, commands | React to changes |

---

## Key Exam Points

1. Partitions determine max parallelism — one consumer per partition per consumer group
2. Ordering is guaranteed within a partition only
3. Capture saves events to Blob/Data Lake in Avro format automatically
4. EventProcessorClient handles load balancing and checkpointing
5. Checkpoints are stored in Blob Storage
6. Use partition keys to group related events
7. Consumer groups provide independent views of the same stream
8. Events are retained (not deleted after reading) — multiple consumers can read the same events
