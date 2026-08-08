# Day 20: Azure Event Hubs & Event Grid - Deep Dive

## Overview

Azure provides two powerful event services: **Event Grid** and **Event Hubs**. While both handle events, they serve different purposes and patterns. Today we'll master both.

---

## Part 1: What, Why, How

### What is Event Grid?

**Event Grid** is a fully managed event routing service that enables reactive programming models. Think of it as a "pub/sub notification system" for Azure resources.

```
┌─────────────────────────────────────────────────────────────────────────┐
│                         AZURE EVENT GRID                                 │
│                     "The Event Router"                                   │
├─────────────────────────────────────────────────────────────────────────┤
│                                                                          │
│   Event Sources                 Event Grid              Subscribers      │
│   ┌──────────────┐            ┌─────────────┐        ┌──────────────┐  │
│   │ Blob Storage │ ─────────► │             │ ─────► │ Function     │  │
│   └──────────────┘            │             │        └──────────────┘  │
│   ┌──────────────┐            │   Publish-  │        ┌──────────────┐  │
│   │ Resource     │ ─────────► │   Subscribe │ ─────► │ Logic App    │  │
│   │ Group        │            │   Service   │        └──────────────┘  │
│   └──────────────┘            │             │        ┌──────────────┐  │
│   ┌──────────────┐            │             │ ─────► │ Event Hub    │  │
│   │ Service Bus  │ ─────────► │             │        └──────────────┘  │
│   └──────────────┘            └─────────────┘        ┌──────────────┐  │
│   ┌──────────────┐                                   │ Webhook/API  │  │
│   │ Custom Topics│ ─────────────────────────────────►│              │  │
│   └──────────────┘                                   └──────────────┘  │
│                                                                          │
└─────────────────────────────────────────────────────────────────────────┘
```

### Key Characteristics of Event Grid

| Feature | Description |
|---------|-------------|
| **Event Type** | Discrete events (something happened) |
| **Delivery** | Push model (pushes to subscribers) |
| **Retention** | Very short (hours) - fire and forget |
| **Throughput** | Millions of events/second |
| **Ordering** | No guaranteed ordering |
| **Pricing** | Per operation (very cheap) |

---

### What is Event Hubs?

**Event Hubs** is a big data streaming platform and event ingestion service. Think of it as a "real-time data pipeline" that can handle massive throughput.

```
┌─────────────────────────────────────────────────────────────────────────────┐
│                         AZURE EVENT HUBS                                     │
│                      "The Event Streaming Platform"                          │
├─────────────────────────────────────────────────────────────────────────────┤
│                                                                              │
│   Producers               Event Hubs (Partitions)            Consumers       │
│   ┌──────────┐           ┌─────────────────────────┐       ┌──────────┐    │
│   │ IoT      │           │ Partition 0: [1][2][3]..│       │ Consumer │    │
│   │ Devices  │ ────────► │ Partition 1: [4][5][6]..│ ────► │ Group A  │    │
│   └──────────┘           │ Partition 2: [7][8][9]..│       └──────────┘    │
│   ┌──────────┐           └─────────────────────────┘       ┌──────────┐    │
│   │ Apps     │                     │                       │ Consumer │    │
│   │ Services │ ────────►           │               ────►   │ Group B  │    │
│   └──────────┘           ┌─────────┴─────────┐             └──────────┘    │
│   ┌──────────┐           │ Retention: 1-90   │             ┌──────────┐    │
│   │ Log      │           │ days              │     ────►   │ Stream   │    │
│   │ Files    │ ────────► │                   │             │ Analytics│    │
│   └──────────┘           │ Throughput Units  │             └──────────┘    │
│                          └───────────────────┘                             │
│                                                                              │
└─────────────────────────────────────────────────────────────────────────────┘
```

### Key Characteristics of Event Hubs

| Feature | Description |
|---------|-------------|
| **Event Type** | Event streams (series of events) |
| **Delivery** | Pull model (consumers pull events) |
| **Retention** | 1-90 days |
| **Throughput** | Millions of events/second per TU |
| **Ordering** | Guaranteed within partition |
| **Pricing** | Throughput Units (TU) or Capacity Units (CU) |

---

## Part 2: When to Use What?

### Decision Flowchart

```
                        ┌─────────────────────────┐
                        │   Do you need to react  │
                        │   to state changes?     │
                        └───────────┬─────────────┘
                                    │
                    ┌───────────────┴───────────────┐
                    │                               │
                   YES                              NO
                    │                               │
                    ▼                               ▼
          ┌─────────────────┐            ┌─────────────────────┐
          │  EVENT GRID     │            │ Do you need ordered │
          │                 │            │ event streams with  │
          │  ✓ React to     │            │ high throughput?    │
          │    changes      │            └──────────┬──────────┘
          │  ✓ Fan-out to   │                       │
          │    multiple     │          ┌────────────┴────────────┐
          │    handlers     │          │                         │
          │  ✓ Simple       │         YES                       NO
          │    routing      │          │                         │
          └─────────────────┘          ▼                         ▼
                              ┌─────────────────┐      ┌─────────────────┐
                              │  EVENT HUBS     │      │  SERVICE BUS    │
                              │                 │      │                 │
                              │  ✓ Telemetry    │      │  ✓ Enterprise   │
                              │  ✓ Log streams  │      │    messaging    │
                              │  ✓ Pipelines    │      │  ✓ Transactions │
                              │  ✓ Real-time    │      │  ✓ Guaranteed   │
                              │    analytics    │      │    delivery     │
                              └─────────────────┘      └─────────────────┘
```

### Comparison Table

| Aspect | Event Grid | Event Hubs | Service Bus |
|--------|------------|------------|-------------|
| **Pattern** | Pub/Sub | Event Streaming | Messaging |
| **Message Type** | Events | Events | Messages |
| **Delivery** | Push | Pull | Pull |
| **Ordering** | No | Yes (per partition) | Yes |
| **Replay** | No | Yes | No |
| **Retention** | Hours | 1-90 days | Configurable |
| **Typical Use** | React to changes | Data pipelines | Enterprise integration |

---

## Part 3: Event Grid Deep Dive

### Event Grid Concepts

```
┌─────────────────────────────────────────────────────────────────┐
│                    EVENT GRID ARCHITECTURE                       │
├─────────────────────────────────────────────────────────────────┤
│                                                                  │
│  ┌──────────────────────────────────────────────────────────┐   │
│  │                    TOPIC (Event Source)                   │   │
│  │  ┌────────────────────────────────────────────────────┐  │   │
│  │  │  Event Schema:                                     │  │   │
│  │  │  {                                                 │  │   │
│  │  │    "id": "unique-id",                              │  │   │
│  │  │    "eventType": "Microsoft.Storage.BlobCreated",   │  │   │
│  │  │    "subject": "/blobServices/default/containers",  │  │   │
│  │  │    "eventTime": "2024-01-01T00:00:00Z",           │  │   │
│  │  │    "data": { ... },                               │  │   │
│  │  │    "dataVersion": "1.0"                           │  │   │
│  │  │  }                                                 │  │   │
│  │  └────────────────────────────────────────────────────┘  │   │
│  └───────────────────────────┬───────────────────────────────┘   │
│                              │                                   │
│                              ▼                                   │
│  ┌──────────────────────────────────────────────────────────┐   │
│  │              EVENT SUBSCRIPTION (Filter)                  │   │
│  │                                                           │   │
│  │  Filters:                                                 │   │
│  │  • Subject filters: /blobServices/default/containers/...  │   │
│  │  • Event type filters: Microsoft.Storage.BlobCreated      │   │
│  │  • Advanced filters: data.contentType = "image/jpeg"      │   │
│  │                                                           │   │
│  └───────────────────────────┬───────────────────────────────┘   │
│                              │                                   │
│                              ▼                                   │
│  ┌──────────────────────────────────────────────────────────┐   │
│  │                    HANDLER (Destination)                  │   │
│  │                                                           │   │
│  │  Supported:                                               │   │
│  │  • Azure Functions      • Event Hubs                     │   │
│  │  • Logic Apps          • Service Bus Topics              │   │
│  │  • Webhooks            • Storage Queues                  │   │
│  │  • Hybrid Connections                                   │   │
│  │                                                           │   │
│  └──────────────────────────────────────────────────────────┘   │
│                                                                  │
└─────────────────────────────────────────────────────────────────┘
```

### Event Grid Topic Types

```
┌─────────────────────────────────────────────────────────────────┐
│                    EVENT GRID TOPIC TYPES                        │
├─────────────────────────────────────────────────────────────────┤
│                                                                  │
│  ┌─────────────────────────────────────────────────────────┐    │
│  │  SYSTEM TOPICS (Azure Resources)                         │    │
│  │                                                          │    │
│  │  • Blob Storage events      • Resource Group events     │    │
│  │  • Event Hub events         • Subscription events       │    │
│  │  • Service Bus events       • Media Services events     │    │
│  │  • IoT Hub events           • Maps events               │    │
│  │                                                          │    │
│  │  ✅ Automatic, no need to create topic                   │    │
│  └─────────────────────────────────────────────────────────┘    │
│                                                                  │
│  ┌─────────────────────────────────────────────────────────┐    │
│  │  CUSTOM TOPICS (Your Events)                             │    │
│  │                                                          │    │
│  │  • Publish custom events from your application           │    │
│  │  • Define your own event types                           │    │
│  │  • Full control over event schema                        │    │
│  │                                                          │    │
│  │  ✅ Create manually, supports domain topics              │    │
│  └─────────────────────────────────────────────────────────┘    │
│                                                                  │
│  ┌─────────────────────────────────────────────────────────┐    │
│  │  PARTNER TOPICS (SaaS Providers)                         │    │
│  │                                                          │    │
│  │  • Stripe, Auth0, etc.                                   │    │
│  │  • Third-party services publishing events                │    │
│  │                                                          │    │
│  │  ✅ Enable in partner systems                            │    │
│  └─────────────────────────────────────────────────────────┘    │
│                                                                  │
└─────────────────────────────────────────────────────────────────┘
```

---

## Part 4: Event Hubs Deep Dive

### Event Hubs Architecture

```
┌─────────────────────────────────────────────────────────────────────────────┐
│                      EVENT HUBS NAMESPACE                                    │
├─────────────────────────────────────────────────────────────────────────────┤
│                                                                              │
│  ┌─────────────────────────────────────────────────────────────────────┐    │
│  │                         EVENT HUB                                    │    │
│  │  ┌───────────────────────────────────────────────────────────────┐  │    │
│  │  │                     PARTITIONS                                 │  │    │
│  │  │                                                                │  │    │
│  │  │  ┌─────────────────┐  ┌─────────────────┐  ┌────────────────┐ │  │    │
│  │  │  │   Partition 0   │  │   Partition 1   │  │  Partition 2   │ │  │    │
│  │  │  │                 │  │                 │  │                │ │  │    │
│  │  │  │  Offset: 100    │  │  Offset: 250    │  │  Offset: 75    │ │  │    │
│  │  │  │  Sequence: 100  │  │  Sequence: 250  │  │  Sequence: 75  │ │  │    │
│  │  │  │                 │  │                 │  │                │ │  │    │
│  │  │  │  [Event][Event] │  │  [Event][Event] │  │  [Event]...    │ │  │    │
│  │  │  │  [Event][Event] │  │  [Event][Event] │  │                │ │  │    │
│  │  │  │  [Event]...     │  │  [Event]...     │  │                │ │  │    │
│  │  │  │                 │  │                 │  │                │ │  │    │
│  │  │  │  ┌───────────┐  │  │  ┌───────────┐  │  │  ┌──────────┐  │ │  │    │
│  │  │  │  │  Consumer │  │  │  │  Consumer │  │  │  │ Consumer │  │ │  │    │
│  │  │  │  │  Group A  │  │  │  │  Group A  │  │  │  │ Group A  │  │ │  │    │
│  │  │  │  └───────────┘  │  │  └───────────┘  │  │  └──────────┘  │ │  │    │
│  │  │  │  ┌───────────┐  │  │  ┌───────────┐  │  │  ┌──────────┐  │ │  │    │
│  │  │  │  │  Consumer │  │  │  │  Consumer │  │  │  │ Consumer │  │ │  │    │
│  │  │  │  │  Group B  │  │  │  │  Group B  │  │  │  │ Group B  │  │ │  │    │
│  │  │  │  └───────────┘  │  │  └───────────┘  │  │  └──────────┘  │ │  │    │
│  │  │  └─────────────────┘  └─────────────────┘  └────────────────┘ │  │    │
│  │  │                                                                │  │    │
│  │  └───────────────────────────────────────────────────────────────┘  │    │
│  └─────────────────────────────────────────────────────────────────────┘    │
│                                                                              │
└─────────────────────────────────────────────────────────────────────────────┘
```

### Key Concepts

```
┌─────────────────────────────────────────────────────────────────┐
│                EVENT HUBS KEY CONCEPTS                           │
├─────────────────────────────────────────────────────────────────┤
│                                                                  │
│  ┌─────────────────────────────────────────────────────────┐    │
│  │  PARTITIONS                                              │    │
│  │                                                          │    │
│  │  • Ordered sequence of events                            │    │
│  │  • Each partition is a separate log                      │    │
│  │  • Events in same partition are ordered                  │    │
│  │  • Number of partitions: 1-32 (Standard), up to 1024     │    │
│  │                                                          │    │
│  │  Use cases:                                              │    │
│  │  • Partition by device ID for device order               │    │
│  │  • Partition by customer ID for customer events          │    │
│  └─────────────────────────────────────────────────────────┘    │
│                                                                  │
│  ┌─────────────────────────────────────────────────────────┐    │
│  │  CONSUMER GROUPS                                         │    │
│  │                                                          │    │
│  │  • Independent view of the event stream                  │    │
│  │  • Each consumer group has its own position              │    │
│  │  • Enables parallel consumers                            │    │
│  │  • Max 20 consumer groups per Event Hub                  │    │
│  │                                                          │    │
│  │  Use cases:                                              │    │
│  │  • One group for real-time processing                    │    │
│  │  • Another group for batch analytics                     │    │
│  └─────────────────────────────────────────────────────────┘    │
│                                                                  │
│  ┌─────────────────────────────────────────────────────────┐    │
│  │  OFFSETS & CHECKPOINTING                                 │    │
│  │                                                          │    │
│  │  • Offset: Position within a partition (byte position)   │    │
│  │  • Sequence Number: Monotonically increasing integer     │    │
│  │  • Checkpoint: Save position for restart                 │    │
│  │                                                          │    │
│  │  Position Options:                                       │    │
│  │  • Earliest: Start from beginning                        │    │
│  │  • Latest: Start from now                                │    │
│  │  • From specific offset/sequence                         │    │
│  │  • From specific datetime                                │    │
│  └─────────────────────────────────────────────────────────┘    │
│                                                                  │
└─────────────────────────────────────────────────────────────────┘
```

### Throughput Units & Capacity

```
┌─────────────────────────────────────────────────────────────────┐
│              EVENT HUBS CAPACITY PLANNING                        │
├─────────────────────────────────────────────────────────────────┤
│                                                                  │
│  ┌─────────────────────────────────────────────────────────┐    │
│  │  STANDARD TIER - Throughput Units (TU)                   │    │
│  │                                                          │    │
│  │  1 TU =                                                  │    │
│  │  • Ingress:  1 MB/s   or  1000 events/s                  │    │
│  │  • Egress:   2 MB/s                                      │    │
│  │  • 84 GB/event hub per month (retention)                 │    │
│  │                                                          │    │
│  │  Scale: Auto-inflate or manually increase                │    │
│  └─────────────────────────────────────────────────────────┘    │
│                                                                  │
│  ┌─────────────────────────────────────────────────────────┐    │
│  │  PREMIUM TIER - Capacity Units (CU)                      │    │
│  │                                                          │    │
│  │  1 CU =                                                  │    │
│  │  • 10x Standard TU capacity                              │    │
│  │  • Dedicated resources                                   │    │
│  │  • Lower latency, higher throughput                      │    │
│  │                                                          │    │
│  │  Scale: 1, 2, 4, 8, 16 CU                                │    │
│  └─────────────────────────────────────────────────────────┘    │
│                                                                  │
│  ┌─────────────────────────────────────────────────────────┐    │
│  │  DEDICATED TIER                                          │    │
│  │                                                          │    │
│  │  • Single-tenant deployment                              │    │
│  │  • Up to 75 CUs                                          │    │
│  │  • Custom retention (up to 90 days)                      │    │
│  │  • SLA: 99.99%                                           │    │
│  └─────────────────────────────────────────────────────────┘    │
│                                                                  │
└─────────────────────────────────────────────────────────────────┘
```

---

## Part 5: Real-World Architecture Patterns

### Pattern 1: IoT Telemetry Pipeline

```
┌─────────────────────────────────────────────────────────────────────────────┐
│                    IOT TELEMETRY PIPELINE                                    │
├─────────────────────────────────────────────────────────────────────────────┤
│                                                                              │
│   IoT Devices          Event Hub           Stream Analytics       Power BI  │
│   ┌─────────┐         ┌─────────┐         ┌─────────────┐       ┌────────┐ │
│   │ Device  │         │         │         │             │       │        │ │
│   │  001    │ ──────► │         │ ──────► │ Real-time   │ ────► │ Dashboard│
│   └─────────┘         │         │         │ Analytics   │       │        │ │
│   ┌─────────┐         │ Event   │         │             │       └────────┘ │
│   │ Device  │ ──────► │ Hubs    │         │ Queries:    │                    │
│   │  002    │         │         │         │ • AVG temp  │                    │
│   └─────────┘         │ 4       │         │ • Anomalies │                    │
│   ┌─────────┐         │ Partitions│        │ • Alerts    │                    │
│   │ Device  │ ──────► │         │         │             │                    │
│   │  003    │         │         │         └─────────────┘                    │
│   └─────────┘         └─────────┘                    │                       │
│                        │                             │                       │
│                        │                             ▼                       │
│                        │                     ┌─────────────┐                │
│                        └───────────────────► │ Azure Data  │                │
│                                              │ Lake / Blob │                │
│                                              │ (Raw data)  │                │
│                                              └─────────────┘                │
│                                                     │                       │
│                                                     ▼                       │
│                                              ┌─────────────┐                │
│                                              │ Azure       │                │
│                                              │ Synapse     │                │
│                                              │ Analytics   │                │
│                                              └─────────────┘                │
│                                                                              │
└─────────────────────────────────────────────────────────────────────────────┘
```

### Pattern 2: Event-Driven Microservices

```
┌─────────────────────────────────────────────────────────────────────────────┐
│                EVENT-DRIVEN MICROSERVICES                                    │
├─────────────────────────────────────────────────────────────────────────────┤
│                                                                              │
│   Order Service        Event Grid            Multiple Subscribers           │
│   ┌────────────┐      ┌────────────┐        ┌────────────────┐             │
│   │            │      │            │       │  Email Service  │             │
│   │  Create    │      │  Custom    │ ────► │  (Send confirm) │             │
│   │  Order     │ ───► │  Topic:    │       └────────────────┘             │
│   │            │      │  orders    │       ┌────────────────┐             │
│   │            │      │            │ ────► │  Inventory      │             │
│   └────────────┘      │            │       │  (Reserve items)│             │
│                       │            │       └────────────────┘             │
│                       │            │       ┌────────────────┐             │
│                       │            │ ────► │  Analytics      │             │
│                       │            │       │  (Track metrics)│             │
│                       │            │       └────────────────┘             │
│                       │            │       ┌────────────────┐             │
│                       │            │ ────► │  Loyalty        │             │
│                       │            │       │  (Update points)│             │
│                       └────────────┘       └────────────────┘             │
│                                                                              │
│   Event Schema:                                                              │
│   {                                                                          │
│     "eventType": "OrderCreated",                                            │
│     "data": {                                                                │
│       "orderId": "12345",                                                    │
│       "customerId": "cust-001",                                              │
│       "items": [...],                                                        │
│       "total": 150.00                                                        │
│     }                                                                        │
│   }                                                                          │
│                                                                              │
└─────────────────────────────────────────────────────────────────────────────┘
```

### Pattern 3: Blob Processing Pipeline

```
┌─────────────────────────────────────────────────────────────────────────────┐
│                  BLOB PROCESSING PIPELINE                                    │
├─────────────────────────────────────────────────────────────────────────────┤
│                                                                              │
│   Upload           Event Grid Event          Processing                     │
│   ┌────────┐       ┌──────────────┐         ┌────────────────┐             │
│   │        │       │ System Topic │         │                │             │
│   │ User   │       │              │  ─────► │ Azure Function │             │
│   │ uploads│ ────► │ Blob Created │         │ (Resize image) │             │
│   │ image  │       │ Event        │         │                │             │
│   │        │       │              │  ─────► │ Azure Function │             │
│   └────────┘       │              │         │ (Scan content) │             │
│                    │              │         └────────────────┘             │
│   Storage Account  │              │         ┌────────────────┐             │
│   ┌────────────┐   │              │  ─────► │ Logic App      │             │
│   │            │   └──────────────┘         │ (Send          │             │
│   │  Blob      │                            │  notification) │             │
│   │  Container │                            └────────────────┘             │
│   │            │                                                            │
│   │  uploads/  │                                                            │
│   └────────────┘                                                            │
│                                                                              │
│   Filter Example:                                                            │
│   • Event Type: Microsoft.Storage.BlobCreated                               │
│   • Subject ends with: .jpg, .png, .gif                                     │
│   • Data.api: "PutBlob" (not "CopyBlob")                                    │
│                                                                              │
└─────────────────────────────────────────────────────────────────────────────┘
```

### Pattern 4: Event Grid + Event Hubs Together

```
┌─────────────────────────────────────────────────────────────────────────────┐
│              COMBINED PATTERN: Real-time Processing Pipeline                 │
├─────────────────────────────────────────────────────────────────────────────┤
│                                                                              │
│   App Service          Event Grid            Event Hubs                      │
│   ┌────────────┐      ┌────────────┐        ┌────────────┐                 │
│   │            │      │            │        │            │                 │
│   │  Business  │      │  Custom    │ ────►  │  orders    │                 │
│   │  Events    │ ───► │  Topic     │        │  Event Hub │                 │
│   │            │      │            │        │            │                 │
│   └────────────┘      └────────────┘        │  Consumer  │                 │
│                                              │  Groups:   │                 │
│                                              │  • realtime│                 │
│                                              │  • batch   │                 │
│                                              │  • archive │                 │
│                                              └────────────┘                 │
│                                                     │                       │
│                        ┌────────────────────────────┼───────────────────┐   │
│                        │                            │                   │   │
│                        ▼                            ▼                   ▼   │
│                 ┌─────────────┐              ┌─────────────┐    ┌─────────┐│
│                 │ Azure       │              │ Stream      │    │ Blob    ││
│                 │ Function    │              │ Analytics   │    │ Storage ││
│                 │ (Immediate) │              │ (Windowed)  │    │ (Raw)   ││
│                 └─────────────┘              └─────────────┘    └─────────┘│
│                                                                              │
│   Why combine?                                                              │
│   • Event Grid: Route events to Event Hubs                                  │
│   • Event Hubs: Store and replay event stream                               │
│   • Multiple independent consumers with different views                      │
│                                                                              │
└─────────────────────────────────────────────────────────────────────────────┘
```

---

## Part 6: Best Practices

### Event Grid Best Practices

```
┌─────────────────────────────────────────────────────────────────┐
│              EVENT GRID BEST PRACTICES                           │
├─────────────────────────────────────────────────────────────────┤
│                                                                  │
│  ✅ DO:                                                          │
│  ├── Use Event Grid for reactive programming                    │
│  ├── Implement idempotent handlers (events may be delivered     │
│  │   more than once)                                             │
│  ├── Use advanced filters to reduce unnecessary processing      │
│  ├── Return 200 OK quickly from webhook handlers                │
│  └── Use dead-letter destinations for failed events             │
│                                                                  │
│  ❌ DON'T:                                                       │
│  ├── Rely on event ordering                                     │
│  ├── Expect events to arrive in real-time (may have delays)     │
│  ├── Process events synchronously in webhook                    │
│  └── Ignore retry policies                                      │
│                                                                  │
│  🔄 Retry Policy:                                                │
│  • Exponential backoff: 10s → 10m → 10m → ... → 2h             │
│  • Max attempts: 30 (default)                                   │
│  • Dead-letter after max attempts                               │
│                                                                  │
└─────────────────────────────────────────────────────────────────┘
```

### Event Hubs Best Practices

```
┌─────────────────────────────────────────────────────────────────┐
│              EVENT HUBS BEST PRACTICES                           │
├─────────────────────────────────────────────────────────────────┤
│                                                                  │
│  ✅ DO:                                                          │
│  ├── Choose partition count based on throughput needs           │
│  ├── Use partition key for ordering (device ID, user ID)        │
│  ├── Implement checkpointing regularly                          │
│  ├── Use consumer groups for different consumers                │
│  └── Enable Auto-Inflate for variable workloads                 │
│                                                                  │
│  ❌ DON'T:                                                       │
│  ├── Create too many partitions (increases overhead)            │
│  ├── Skip checkpointing (may reprocess events)                  │
│  ├── Have more consumers than partitions in a group             │
│  └── Use Event Hubs for simple messaging (use Service Bus)      │
│                                                                  │
│  📊 Partition Strategy:                                          │
│  • IoT: Partition by device ID                                  │
│  • Orders: Partition by customer ID                             │
│  • Logs: Round-robin (if order doesn't matter)                  │
│                                                                  │
│  🔢 Partition Count Guide:                                       │
│  • < 1 MB/s: 2 partitions                                       │
│  • 1-10 MB/s: 4-8 partitions                                    │
│  • > 10 MB/s: 16-32 partitions                                  │
│                                                                  │
└─────────────────────────────────────────────────────────────────┘
```

---

## Part 7: SDK Code Examples

### Publishing to Event Grid

```csharp
// Install: Azure.Messaging.EventGrid

using Azure.Messaging.EventGrid;

public class EventGridPublisher
{
    private readonly EventGridPublisherClient _client;
    
    public EventGridPublisher(string topicEndpoint, string accessKey)
    {
        _client = new EventGridPublisherClient(
            new Uri(topicEndpoint),
            new Azure.AzureKeyCredential(accessKey));
    }
    
    public async Task PublishOrderCreatedEvent(Order order)
    {
        var eventGridEvent = new EventGridEvent(
            subject: $"orders/{order.Id}",
            eventType: "OrderCreated",
            dataVersion: "1.0",
            data: new BinaryData(order)
        );
        
        await _client.SendEventAsync(eventGridEvent);
    }
    
    public async Task PublishBatchEvents(IEnumerable<Order> orders)
    {
        var events = orders.Select(o => new EventGridEvent(
            subject: $"orders/{o.Id}",
            eventType: "OrderCreated",
            dataVersion: "1.0",
            data: new BinaryData(o)
        ));
        
        await _client.SendEventsAsync(events);
    }
}
```

### Publishing to Event Hubs

```csharp
// Install: Azure.Messaging.EventHubs

using Azure.Messaging.EventHubs;
using Azure.Messaging.EventHubs.Producer;

public class EventHubPublisher
{
    private readonly EventHubProducerClient _producerClient;
    
    public EventHubPublisher(string connectionString, string eventHubName)
    {
        _producerClient = new EventHubProducerClient(
            connectionString, eventHubName);
    }
    
    // Simple send
    public async Task SendEventAsync(object data, string partitionKey = null)
    {
        using var eventBatch = await _producerClient.CreateBatchAsync(
            new CreateBatchOptions { PartitionKey = partitionKey });
        
        var eventData = new EventData(
            BinaryData.FromObjectAsJson(data));
        
        eventBatch.TryAdd(eventData);
        
        await _producerClient.SendAsync(eventBatch);
    }
    
    // Batch send for high throughput
    public async Task SendBatchAsync(IEnumerable<object> items, string partitionKey)
    {
        using var eventBatch = await _producerClient.CreateBatchAsync(
            new CreateBatchOptions { PartitionKey = partitionKey });
        
        foreach (var item in items)
        {
            var eventData = new EventData(
                BinaryData.FromObjectAsJson(item));
            
            if (!eventBatch.TryAdd(eventData))
            {
                // Batch is full, send it and create new one
                await _producerClient.SendAsync(eventBatch);
                // Continue with new batch...
            }
        }
        
        await _producerClient.SendAsync(eventBatch);
    }
}
```

### Consuming from Event Hubs

```csharp
// Install: Azure.Messaging.EventHubs

using Azure.Messaging.EventHubs;
using Azure.Messaging.EventHubs.Consumer;
using Azure.Storage.Blobs;

public class EventHubConsumer
{
    private readonly EventProcessorClient _processor;
    
    public EventHubConsumer(
        string eventHubConnectionString,
        string eventHubName,
        string blobConnectionString,
        string containerName,
        string consumerGroup = EventHubConsumerClient.DefaultConsumerGroupName)
    {
        // Blob storage for checkpointing
        var blobContainer = new BlobContainerClient(
            blobConnectionString, containerName);
        
        _processor = new EventProcessorClient(
            blobContainer,
            consumerGroup,
            eventHubConnectionString,
            eventHubName);
        
        // Register handlers
        _processor.ProcessEventAsync += ProcessEventHandler;
        _processor.ProcessErrorAsync += ProcessErrorHandler;
    }
    
    public async Task StartProcessingAsync()
    {
        await _processor.StartProcessingAsync();
    }
    
    public async Task StopProcessingAsync()
    {
        await _processor.StopProcessingAsync();
    }
    
    private async Task ProcessEventHandler(ProcessEventArgs args)
    {
        // Deserialize event
        var order = args.Data.EventBody.ToObjectFromJson<Order>();
        
        // Process the event
        Console.WriteLine($"Processing order: {order.Id}");
        
        // Checkpoint (save position)
        await args.UpdateCheckpointAsync();
    }
    
    private Task ProcessErrorHandler(ProcessErrorEventArgs args)
    {
        Console.WriteLine($"Error: {args.Exception.Message}");
        return Task.CompletedTask;
    }
}
```

### Azure Function Event Grid Trigger

```csharp
// Function triggered by Event Grid
public static class OrderCreatedFunction
{
    [FunctionName("OrderCreatedHandler")]
    public static void Run(
        [EventGridTrigger] EventGridEvent eventGridEvent,
        ILogger log)
    {
        log.LogInformation($"Event: {eventGridEvent.EventType}");
        log.LogInformation($"Subject: {eventGridEvent.Subject}");
        
        // Deserialize data
        var order = eventGridEvent.Data.ToObjectFromJson<Order>();
        
        // Process the order
        ProcessOrder(order);
    }
}

// Function with Blob trigger from Event Grid
public static class ImageProcessorFunction
{
    [FunctionName("ImageProcessor")]
    public static void Run(
        [EventGridTrigger] EventGridEvent eventGridEvent,
        [Blob("{data.url}", FileAccess.Read)] Stream imageStream,
        ILogger log)
    {
        // Process image
    }
}
```

### Azure Function Event Hub Trigger

```csharp
// Function triggered by Event Hub
public static class TelemetryProcessorFunction
{
    [FunctionName("TelemetryProcessor")]
    public static void Run(
        [EventHubTrigger("telemetry-hub", 
         Connection = "EventHubConnectionString",
         ConsumerGroup = "analytics")] EventData[] events,
        ILogger log)
    {
        foreach (var eventData in events)
        {
            var telemetry = eventData.EventBody.ToObjectFromJson<Telemetry>();
            log.LogInformation($"Device: {telemetry.DeviceId}, Temp: {telemetry.Temperature}");
        }
    }
}
```

---

## Part 8: Monitoring & Troubleshooting

### Event Grid Monitoring

```
┌─────────────────────────────────────────────────────────────────┐
│              EVENT GRID METRICS                                  │
├─────────────────────────────────────────────────────────────────┤
│                                                                  │
│  Key Metrics to Monitor:                                         │
│                                                                  │
│  📊 Published Events          - Total events sent to topic       │
│  📊 Matched Events            - Events matching subscriptions    │
│  📊 Delivered Events          - Events successfully delivered    │
│  📊 Dropped Events            - Events dropped (delivery failed) │
│  📊 Dead Lettered Events      - Events moved to dead-letter      │
│  📊 Processing Time (MS)      - Handler response time            │
│                                                                  │
│  Diagnostic Settings:                                            │
│  • Send to Log Analytics                                        │
│  • Stream to Event Hubs                                         │
│  • Archive to Storage Account                                   │
│                                                                  │
└─────────────────────────────────────────────────────────────────┘
```

### Event Hubs Monitoring

```
┌─────────────────────────────────────────────────────────────────┐
│              EVENT HUBS METRICS                                  │
├─────────────────────────────────────────────────────────────────┤
│                                                                  │
│  Key Metrics to Monitor:                                         │
│                                                                  │
│  📊 Incoming Messages         - Events received                  │
│  📊 Outgoing Messages         - Events consumed                  │
│  📊 Incoming Bytes            - Data ingested                    │
│  📊 Outgoing Bytes            - Data consumed                    │
│  📊 Quota Exceeded Errors     - Throttling events                │
│  📊 Capture Backlog           - Archive lag                      │
│                                                                  │
│  Consumer Lag:                                                   │
│  • Tracks how far behind consumers are                          │
│  • Critical for real-time processing                            │
│  • Set up alerts for high lag                                   │
│                                                                  │
│  Throughput Monitoring:                                          │
│  • Monitor TU utilization                                       │
│  • Enable Auto-Inflate alerts                                   │
│  • Check partition distribution                                 │
│                                                                  │
└─────────────────────────────────────────────────────────────────┘
```

---

## Part 9: CLI Commands Reference

### Event Grid CLI Commands

```bash
# Create Event Grid custom topic
az eventgrid topic create \
    --name orders-topic \
    --resource-group rg-events \
    --location eastus

# Create Event Grid subscription
az eventgrid event-subscription create \
    --name order-handler-sub \
    --source-resource-id /subscriptions/{sub}/resourceGroups/rg-events/providers/Microsoft.EventGrid/topics/orders-topic \
    --endpoint-type azurefunction \
    --endpoint /subscriptions/{sub}/resourceGroups/rg-events/providers/Microsoft.Web/sites/orderfunc/functions/OrderHandler

# List event subscriptions
az eventgrid event-subscription list \
    --source-resource-id /subscriptions/{sub}/resourceGroups/rg-events/providers/Microsoft.EventGrid/topics/orders-topic

# Send test event
az eventgrid event publish \
    --endpoint https://orders-topic.eastus-1.eventgrid.azure.net/api/events \
    --key {topic-key} \
    --data '{"orderId": "123", "customerId": "cust-001"}'

# Create Blob Storage event subscription
az eventgrid event-subscription create \
    --name blob-processor \
    --source-resource-id /subscriptions/{sub}/resourceGroups/rg-events/providers/Microsoft.Storage/storageAccounts/mystorageaccount \
    --endpoint-type webhook \
    --endpoint https://myapp.azurewebsites.net/api/events \
    --subject-begins-with /blobServices/default/containers/uploads \
    --included-event-types Microsoft.Storage.BlobCreated
```

### Event Hubs CLI Commands

```bash
# Create Event Hubs namespace
az eventhubs namespace create \
    --name evhns-telemetry \
    --resource-group rg-events \
    --location eastus \
    --sku Standard \
    --capacity 1 \
    --enable-auto-inflate \
    --maximum-throughput-units 4

# Create Event Hub
az eventhubs eventhub create \
    --name telemetry-hub \
    --namespace-name evhns-telemetry \
    --resource-group rg-events \
    --message-retention 7 \
    --partition-count 4

# Create Consumer Group
az eventhubs eventhub consumer-group create \
    --name analytics-consumers \
    --namespace-name evhns-telemetry \
    --eventhub-name telemetry-hub \
    --resource-group rg-events

# Get connection string
az eventhubs namespace authorization-rule keys list \
    --name RootManageSharedAccessKey \
    --namespace-name evhns-telemetry \
    --resource-group rg-events

# Enable Capture (archive to blob)
az eventhubs eventhub update \
    --name telemetry-hub \
    --namespace-name evhns-telemetry \
    --resource-group rg-events \
    --enable-capture true \
    --capture-interval 300 \
    --capture-size-limit 314572800 \
    --storage-account /subscriptions/{sub}/resourceGroups/rg-events/providers/Microsoft.Storage/storageAccounts/mystorage \
    --blob-container capture

# List Event Hubs
az eventhubs eventhub list \
    --namespace-name evhns-telemetry \
    --resource-group rg-events

# Create Event Hub subscription to Event Grid
az eventgrid event-subscription create \
    --name telemetry-events \
    --source-resource-id /subscriptions/{sub}/resourceGroups/rg-events/providers/Microsoft.EventHub/namespaces/evhns-telemetry \
    --endpoint-type eventhub \
    --endpoint /subscriptions/{sub}/resourceGroups/rg-events/providers/Microsoft.EventHub/namespaces/evhns-telemetry/eventhubs/archive-hub
```

---

## Part 10: Security

### Event Grid Security

```
┌─────────────────────────────────────────────────────────────────┐
│              EVENT GRID SECURITY                                 │
├─────────────────────────────────────────────────────────────────┤
│                                                                  │
│  Authentication Options:                                         │
│                                                                  │
│  1. Access Keys (for publishers)                                │
│     • Topic Key: Used to publish events                         │
│     • Key rotation supported                                    │
│                                                                  │
│  2. Managed Identity                                            │
│     • System-assigned or user-assigned                          │
│     • For publishing from Azure services                        │
│                                                                  │
│  3. Webhook Security                                            │
│     • Validation handshake required                             │
│     • Support for Azure AD (Azure Functions, Logic Apps)        │
│                                                                  │
│  Authorization:                                                  │
│  • RBAC roles: EventGrid Data Sender, EventGrid Subscriber      │
│  • Private endpoints support                                    │
│                                                                  │
└─────────────────────────────────────────────────────────────────┘
```

### Event Hubs Security

```
┌─────────────────────────────────────────────────────────────────┐
│              EVENT HUBS SECURITY                                 │
├─────────────────────────────────────────────────────────────────┤
│                                                                  │
│  Authentication Options:                                         │
│                                                                  │
│  1. Shared Access Signatures (SAS)                              │
│     • Namespace level policies                                  │
│     • Event Hub specific policies                               │
│     • Send, Listen, Manage permissions                          │
│                                                                  │
│  2. Managed Identity                                            │
│     • System-assigned or user-assigned                          │
│     • Azure AD authentication                                   │
│                                                                  │
│  Authorization:                                                  │
│  • RBAC roles:                                                   │
│    - Azure Event Hubs Data Owner                                │
│    - Azure Event Hubs Data Sender                               │
│    - Azure Event Hubs Data Receiver                             │
│                                                                  │
│  Network Security:                                               │
│  • Private endpoints                                            │
│  • IP filtering                                                 │
│  • Service endpoints                                            │
│                                                                  │
└─────────────────────────────────────────────────────────────────┘
```

---

## Summary

### Quick Reference: Event Grid vs Event Hubs

```
┌──────────────────────────────────────────────────────────────────────────┐
│                    QUICK DECISION GUIDE                                  │
├──────────────────────────────────────────────────────────────────────────┤
│                                                                          │
│  USE EVENT GRID WHEN:                                                   │
│  ✓ You need to react to changes in Azure resources                      │
│  ✓ You want to fan-out events to multiple subscribers                  │
│  ✓ You're building event-driven microservices                           │
│  ✓ You need to trigger workflows or functions                          │
│  ✓ Cost is a primary concern (very cheap per operation)                 │
│                                                                          │
│  USE EVENT HUBS WHEN:                                                   │
│  ✓ You need to stream large volumes of events                           │
│  ✓ You need ordered event processing (within partitions)                │
│  ✓ You need to replay events or maintain event history                 │
│  ✓ You're building IoT or telemetry pipelines                          │
│  ✓ You need multiple independent consumers with different views         │
│                                                                          │
│  USE TOGETHER WHEN:                                                     │
│  ✓ You want Event Grid to route events to Event Hubs                   │
│  ✓ You need both reactive triggers and event streaming                  │
│  ✓ You want Event Hubs as an Event Grid subscriber                      │
│                                                                          │
└──────────────────────────────────────────────────────────────────────────┘
```

### Exam Tips

1. **Event Grid is reactive** - Use for "something happened, do X"
2. **Event Hubs is streaming** - Use for "process this stream of data"
3. **Event Grid is push** - Pushes to subscribers
4. **Event Hubs is pull** - Consumers pull events
5. **Event Grid has no ordering** - Events may arrive out of order
6. **Event Hubs guarantees ordering** - Within same partition
7. **Event Grid is cheaper** - Pay per operation
8. **Event Hubs scales better** - For high-throughput scenarios
9. **Event Grid filters events** - Use subscriptions with filters
10. **Event Hubs uses partitions** - Choose partition key wisely

---

## Next Steps

Proceed to the lab to build a complete demo project implementing both Event Grid and Event Hubs:
- **lab-event-hubs-grid.md** - Hands-on project with full implementation
