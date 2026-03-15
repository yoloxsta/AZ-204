# Day 08 — Message-Based Solutions

## What You'll Learn Today

- Message-based architecture patterns
- Azure Service Bus: queues, topics, subscriptions
- Azure Queue Storage
- When to use Service Bus vs Queue Storage
- Dead-letter queues, sessions, and duplicate detection

---

## 1. Message-Based Architecture

Messages differ from events: a message carries data that the receiver needs to process. The sender expects the message to be handled.

| Concept | Events | Messages |
|---------|--------|----------|
| Intent | Notification (something happened) | Command (do something) |
| Coupling | Loose (sender doesn't care who listens) | Tighter (sender expects processing) |
| Delivery | At-least-once, no guarantee of order | Guaranteed delivery, ordering possible |
| Example | "Blob created" | "Process this order" |

---

## 2. Azure Service Bus

Service Bus is an enterprise message broker with queues and publish-subscribe topics.

### Tiers

| Tier | Features | Use Case |
|------|----------|----------|
| **Basic** | Queues only | Simple queue scenarios |
| **Standard** | Queues + Topics, 256 KB messages | Most production apps |
| **Premium** | Dedicated resources, 100 MB messages, VNet | High throughput, isolation |

### Queues

Point-to-point communication: one sender, one receiver.

```
Producer ──► [  Queue  ] ──► Consumer
              msg1 msg2
```

- FIFO ordering (with sessions)
- At-least-once or at-most-once delivery
- Lock-based consumption (peek-lock or receive-and-delete)

### Topics and Subscriptions

Publish-subscribe: one sender, multiple receivers.

```
Producer ──► [  Topic  ] ──► Subscription A ──► Consumer A
                         ──► Subscription B ──► Consumer B
                         ──► Subscription C ──► Consumer C
```

Each subscription can have filters:
- **SQL filter** — SQL-like expression on message properties
- **Correlation filter** — match on specific properties
- **Boolean filter** — true (all) or false (none)

### Key Features

- **Dead-letter queue (DLQ)** — messages that can't be processed go here
- **Sessions** — group related messages and process in order (FIFO)
- **Duplicate detection** — prevent duplicate messages within a time window
- **Scheduled delivery** — send a message now, deliver later
- **Message deferral** — defer processing to a later time
- **Auto-forwarding** — chain queues/topics together
- **TTL (Time to Live)** — messages expire after a configurable time

### Peek-Lock vs Receive-and-Delete

| Mode | Behavior | Use Case |
|------|----------|----------|
| **Peek-Lock** | Message locked, must be completed or abandoned | Reliable processing |
| **Receive-and-Delete** | Message removed immediately | Fire-and-forget |

---

## 3. Azure Queue Storage

Queue Storage is a simple queue service built into Azure Storage accounts.

| Feature | Queue Storage | Service Bus |
|---------|--------------|-------------|
| Max message size | 64 KB | 256 KB (Standard), 100 MB (Premium) |
| Max queue size | 500 TB | 1-80 GB |
| Ordering | No guarantee | FIFO (with sessions) |
| Duplicate detection | No | Yes |
| Dead-letter | No | Yes |
| Topics/subscriptions | No | Yes |
| Transactions | No | Yes |
| Cost | Very low | Higher |

Use Queue Storage for simple, high-volume queuing. Use Service Bus for enterprise messaging with advanced features.

---

## Key Takeaways

1. Service Bus is enterprise messaging — queues for point-to-point, topics for pub-sub
2. Peek-Lock ensures reliable processing — complete or abandon messages
3. Dead-letter queues catch unprocessable messages
4. Sessions enable FIFO ordering and message grouping
5. Queue Storage is simpler and cheaper — good for basic queuing
6. Use Service Bus when you need topics, sessions, duplicate detection, or transactions

---

Next: [Lab — Service Bus](./lab-service-bus.md)
