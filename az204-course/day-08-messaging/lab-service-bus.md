# Lab: Azure Service Bus

## Title
Send and Receive Messages with Azure Service Bus Queues and Topics

## Objective
- Create a Service Bus namespace, queue, and topic
- Send and receive messages using the .NET SDK
- Implement topic subscriptions with filters
- Handle dead-letter messages
- Use sessions for ordered processing

## Architecture

```
┌──────────────────────────────────────────────────────────┐
│              Resource Group: az204-day08-rg               │
│                                                          │
│  ┌────────────────────────────────────────────────────┐  │
│  │  Service Bus Namespace: az204sb<unique>            │  │
│  │  Tier: Standard                                    │  │
│  │                                                    │  │
│  │  ┌──────────────────┐  ┌────────────────────────┐ │  │
│  │  │ Queue:           │  │ Topic: orders          │ │  │
│  │  │ order-queue      │  │                        │ │  │
│  │  │                  │  │ Sub: all-orders        │ │  │
│  │  │ DLQ: $deadletter │  │ Sub: high-value        │ │  │
│  │  │                  │  │   (filter: total>500)  │ │  │
│  │  └──────────────────┘  └────────────────────────┘ │  │
│  └────────────────────────────────────────────────────┘  │
│                                                          │
│  ┌──────────────────┐    ┌──────────────────┐           │
│  │  .NET Sender App │    │ .NET Receiver App│           │
│  └──────────────────┘    └──────────────────┘           │
└──────────────────────────────────────────────────────────┘
```

## Prerequisites
- Azure CLI installed and logged in
- .NET 8 SDK installed

---

## Step 1 — Create Service Bus Resources

```bash
az group create --name az204-day08-rg --location eastus

# Create namespace (Standard tier for topics)
az servicebus namespace create \
  --name az204sb<unique> \
  --resource-group az204-day08-rg \
  --location eastus \
  --sku Standard
```

Create a queue:
```bash
az servicebus queue create \
  --name order-queue \
  --namespace-name az204sb<unique> \
  --resource-group az204-day08-rg \
  --max-delivery-count 3 \
  --default-message-time-to-live P14D \
  --enable-dead-lettering-on-message-expiration true
```

Create a topic with subscriptions:
```bash
az servicebus topic create \
  --name orders \
  --namespace-name az204sb<unique> \
  --resource-group az204-day08-rg

# Subscription that receives ALL messages
az servicebus topic subscription create \
  --name all-orders \
  --namespace-name az204sb<unique> \
  --resource-group az204-day08-rg \
  --topic-name orders

# Subscription with SQL filter (high-value orders only)
az servicebus topic subscription create \
  --name high-value \
  --namespace-name az204sb<unique> \
  --resource-group az204-day08-rg \
  --topic-name orders

az servicebus topic subscription rule create \
  --name high-value-filter \
  --namespace-name az204sb<unique> \
  --resource-group az204-day08-rg \
  --topic-name orders \
  --subscription-name high-value \
  --filter-sql-expression "total > 500"
```

Get the connection string:
```bash
az servicebus namespace authorization-rule keys list \
  --name RootManageSharedAccessKey \
  --namespace-name az204sb<unique> \
  --resource-group az204-day08-rg \
  --query primaryConnectionString \
  --output tsv
```

---

## Step 2 — Create the .NET Project

```bash
mkdir az204-servicebus-lab
cd az204-servicebus-lab
dotnet new console -n ServiceBusApp
cd ServiceBusApp
dotnet add package Azure.Messaging.ServiceBus
```

---

## Step 3 — Write the Application

Replace `Program.cs`:

```csharp
using Azure.Messaging.ServiceBus;

string connectionString = "<your-connection-string>";
string queueName = "order-queue";
string topicName = "orders";

var client = new ServiceBusClient(connectionString);

// ===== PART 1: Queue Operations =====
Console.WriteLine("=== Queue: Sending Messages ===");
var queueSender = client.CreateSender(queueName);

// Send individual messages
for (int i = 1; i <= 5; i++)
{
    var message = new ServiceBusMessage($"Order #{i}: Product-{i}")
    {
        Subject = $"order-{i}",
        ApplicationProperties = { { "priority", i <= 2 ? "high" : "normal" } }
    };
    await queueSender.SendMessageAsync(message);
    Console.WriteLine($"  Sent: {message.Body}");
}

// Send a batch of messages
var batch = await queueSender.CreateMessageBatchAsync();
for (int i = 6; i <= 10; i++)
{
    batch.TryAddMessage(new ServiceBusMessage($"Batch Order #{i}"));
}
await queueSender.SendMessageBatchAsync(batch);
Console.WriteLine($"  Sent batch of {batch.Count} messages");

// Receive messages from queue
Console.WriteLine("\n=== Queue: Receiving Messages ===");
var queueReceiver = client.CreateReceiver(queueName);

var messages = await queueReceiver.ReceiveMessagesAsync(maxMessages: 10, maxWaitTime: TimeSpan.FromSeconds(5));
foreach (var msg in messages)
{
    Console.WriteLine($"  Received: {msg.Body} | Subject: {msg.Subject}");
    await queueReceiver.CompleteMessageAsync(msg); // Remove from queue
}
```

```csharp
// ===== PART 2: Topic Operations =====
Console.WriteLine("\n=== Topic: Sending Messages ===");
var topicSender = client.CreateSender(topicName);

var orders = new[]
{
    new { Id = "ORD-100", Product = "Laptop", Total = 1299.99 },
    new { Id = "ORD-101", Product = "Mouse", Total = 29.99 },
    new { Id = "ORD-102", Product = "Desk", Total = 599.99 },
    new { Id = "ORD-103", Product = "Keyboard", Total = 89.99 },
    new { Id = "ORD-104", Product = "Monitor", Total = 749.99 }
};

foreach (var order in orders)
{
    var message = new ServiceBusMessage(
        System.Text.Json.JsonSerializer.Serialize(order))
    {
        Subject = order.Product,
        ApplicationProperties = { { "total", order.Total } }
    };
    await topicSender.SendMessageAsync(message);
    Console.WriteLine($"  Sent: {order.Id} - {order.Product} (${order.Total})");
}

// Receive from "all-orders" subscription
Console.WriteLine("\n=== Subscription: all-orders ===");
var allOrdersReceiver = client.CreateReceiver(topicName, "all-orders");
var allMessages = await allOrdersReceiver.ReceiveMessagesAsync(10, TimeSpan.FromSeconds(5));
Console.WriteLine($"  Received {allMessages.Count} messages (all orders)");
foreach (var msg in allMessages)
{
    Console.WriteLine($"    {msg.Body}");
    await allOrdersReceiver.CompleteMessageAsync(msg);
}

// Receive from "high-value" subscription (filtered: total > 500)
Console.WriteLine("\n=== Subscription: high-value (total > 500) ===");
var highValueReceiver = client.CreateReceiver(topicName, "high-value");
var highValueMessages = await highValueReceiver.ReceiveMessagesAsync(10, TimeSpan.FromSeconds(5));
Console.WriteLine($"  Received {highValueMessages.Count} messages (high-value only)");
foreach (var msg in highValueMessages)
{
    Console.WriteLine($"    {msg.Body}");
    await highValueReceiver.CompleteMessageAsync(msg);
}

// ===== PART 3: Dead-Letter Queue =====
Console.WriteLine("\n=== Dead-Letter Queue Demo ===");

// Send a message and abandon it multiple times to trigger DLQ
var dlqSender = client.CreateSender(queueName);
await dlqSender.SendMessageAsync(new ServiceBusMessage("Problematic message"));

var dlqReceiver = client.CreateReceiver(queueName);
for (int attempt = 1; attempt <= 3; attempt++)
{
    var dlqMsg = await dlqReceiver.ReceiveMessageAsync(TimeSpan.FromSeconds(5));
    if (dlqMsg != null)
    {
        Console.WriteLine($"  Attempt {attempt}: Abandoning message (simulating failure)");
        await dlqReceiver.AbandonMessageAsync(dlqMsg);
    }
}

// Read from dead-letter queue
await Task.Delay(2000); // Wait for DLQ processing
var deadLetterReceiver = client.CreateReceiver(queueName,
    new ServiceBusReceiverOptions { SubQueue = SubQueue.DeadLetter });

var deadMsg = await deadLetterReceiver.ReceiveMessageAsync(TimeSpan.FromSeconds(5));
if (deadMsg != null)
{
    Console.WriteLine($"  Dead-letter message: {deadMsg.Body}");
    Console.WriteLine($"  Dead-letter reason: {deadMsg.DeadLetterReason}");
    await deadLetterReceiver.CompleteMessageAsync(deadMsg);
}

// Cleanup
await queueSender.DisposeAsync();
await queueReceiver.DisposeAsync();
await topicSender.DisposeAsync();
await allOrdersReceiver.DisposeAsync();
await highValueReceiver.DisposeAsync();
await dlqSender.DisposeAsync();
await dlqReceiver.DisposeAsync();
await deadLetterReceiver.DisposeAsync();
await client.DisposeAsync();

Console.WriteLine("\nLab complete!");
```

---

## Step 4 — Run the Application

```bash
dotnet run
```

Expected output shows:
- 10 messages sent to queue, 10 received
- 5 orders sent to topic
- `all-orders` subscription receives all 5
- `high-value` subscription receives only 3 (Laptop $1299, Desk $599, Monitor $749)
- Dead-letter queue captures the abandoned message after 3 attempts

---

## Cleanup

```bash
cd ../..
rm -rf az204-servicebus-lab

az group delete --name az204-day08-rg --yes --no-wait
```

---

## Expected Outcomes

| Task | Status |
|------|--------|
| Created Service Bus namespace, queue, and topic | ✅ |
| Sent individual and batch messages to queue | ✅ |
| Received and completed messages from queue | ✅ |
| Sent messages to topic with application properties | ✅ |
| Received from all-orders subscription (all messages) | ✅ |
| Received from high-value subscription (filtered) | ✅ |
| Demonstrated dead-letter queue behavior | ✅ |

---

Next: [Day 09 — Caching & Content Delivery](../day-09-caching/theory.md)
