# Day 07 — Event-Based Solutions

## What You'll Learn Today

- Event-driven architecture patterns
- Azure Event Grid: topics, subscriptions, event schemas
- Azure Event Hubs: streaming, partitions, consumer groups
- When to use Event Grid vs Event Hubs
---

## 1. Event-Driven Architecture

In event-driven architecture, components communicate by producing and consuming events. This decouples producers from consumers.

### Key Concepts

- **Event** — a notification that something happened (lightweight, no expectation of action)
- **Producer** — the service that emits the event
- **Consumer** — the service that reacts to the event
- **Broker** — the middleware that routes events (Event Grid, Event Hubs)

---

## 2. Azure Event Grid

Event Grid is a fully managed event routing service. It uses a publish-subscribe model.

### Key Concepts
- **Topics** — where events are sent (system topics or custom topics)
- **Event Subscriptions** — route events from a topic to a handler
- **Event Handlers** — what processes the event (Functions, Webhooks, Queues, etc.)

### System Topics

Azure services automatically publish events to system topics:
- Blob Storage: blob created, deleted
- Resource Groups: resource created, deleted, updated
- Container Registry: image pushed
- Key Vault: secret expiring

### Event Schema

Event Grid supports two schemas:
**Event Grid Schema:**
```json
{
  "id": "unique-id",
  "eventType": "Microsoft.Storage.BlobCreated",
  "subject": "/blobServices/default/containers/images/blobs/photo.jpg",
  "eventTime": "2026-03-14T10:00:00Z",
  "data": {
    "api": "PutBlob",
    "contentLength": 524288,
    "url": "https://myaccount.blob.core.windows.net/images/photo.jpg"
  },
  "dataVersion": "1.0"
}
```

**CloudEvents Schema (industry standard):**
```json
{
  "specversion": "1.0",
  "type": "Microsoft.Storage.BlobCreated",
  "source": "/subscriptions/.../storageAccounts/myaccount",
  "id": "unique-id",
  "time": "2026-03-14T10:00:00Z",
  "data": { ... }
}
```

### Event Filtering

Filter events before they reach the handler:
- **Event type filtering** — only specific event types
- **Subject filtering** — prefix/suffix matching on the subject
- **Advanced filtering** — filter on data properties

### Retry and Dead-Letter

- Events are retried with exponential backoff (up to 24 hours)
- Failed events can be sent to a dead-letter storage container
- Maximum delivery attempts: configurable (1-30, default 30)

---

## 3. Azure Event Hubs

Event Hubs is a big data streaming platform. It can receive millions of events per second.

### Key Concepts

- **Namespace** — container for Event Hubs (like a server)
- **Event Hub** — the actual stream (like a topic)
- **Partitions** — parallel streams within an Event Hub (2-32)
- **Consumer Groups** — independent views of the stream
- **Capture** — automatically save events to Blob Storage or Data Lake

### Architecture

```
Producers ──► ┌─────────────────────────┐ ──► Consumer Group 1
              │      Event Hub          │ ──► Consumer Group 2
              │                         │
              │ Partition 0 ████████    │
              │ Partition 1 ██████      │
              │ Partition 2 ████████    │
              │ Partition 3 █████       │
              └─────────────────────────┘
                        │
                        ▼ (Capture)
                   Blob Storage
```

### Retention

- Default: 1 day (up to 7 days on Standard, 90 days on Premium)
- Events are not deleted after consumption — multiple consumers can read the same events

---

## 4. Event Grid vs Event Hubs

| Feature | Event Grid | Event Hubs |
|---------|-----------|------------|
| Pattern | Reactive (event notification) | Streaming (high throughput) |
| Throughput | Moderate | Millions/sec |
| Ordering | No guarantee | Per partition |
| Retention | Retry up to 24h | 1-90 days |
| Use case | React to Azure events | Telemetry, logs, analytics |
| Consumer model | Push (webhook/subscription) | Pull (consumer reads) |

---

## Key Takeaways

1. Event Grid is for reactive event routing — something happened, react to it
2. Event Hubs is for high-throughput streaming — millions of events per second
3. Event Grid has system topics (Azure service events) and custom topics
4. Event subscriptions route events to handlers with optional filtering
5. Event Hubs uses partitions for parallelism and consumer groups for independent reading
6. Use Event Grid for Azure resource events, Event Hubs for telemetry/IoT

---

## Deep Dive

For detailed coverage of Azure Event Hubs, see:
- [Exploring Azure Event Hubs — Deep Dive](./event-hubs-deep-dive.md)

---

Next: [Lab — Event Grid](./lab-event-grid.md)
