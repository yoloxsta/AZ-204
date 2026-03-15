# Lab: Azure Cache for Redis

## Title
Implement Caching with Azure Cache for Redis

## Objective
- Create an Azure Cache for Redis instance
- Connect and perform operations using the .NET SDK
- Implement the cache-aside pattern
- Work with different Redis data types
- Configure expiration and eviction

## Architecture

```
┌──────────────────────────────────────────────────────────┐
│              Resource Group: az204-day09-rg               │
│                                                          │
│  ┌──────────────────┐    ┌──────────────────────────┐   │
│  │  .NET Console App│    │  Azure Cache for Redis   │   │
│  │                  │───►│  az204redis<unique>      │   │
│  │  - Cache-aside   │    │  Tier: Basic C0          │   │
│  │  - Data types    │    │  Port: 6380 (SSL)        │   │
│  │  - TTL demo      │    │                          │   │
│  └──────────────────┘    │  Data:                   │   │
│                          │  - Strings (sessions)    │   │
│                          │  - Hashes (user profiles)│   │
│                          │  - Sorted Sets (leaders) │   │
│                          └──────────────────────────┘   │
└──────────────────────────────────────────────────────────┘
```

## Prerequisites
- Azure CLI installed and logged in
- .NET 8 SDK installed

---

## Step 1 — Create Azure Cache for Redis

```bash
az group create --name az204-day09-rg --location eastus

az redis create \
  --name az204redis<unique> \
  --resource-group az204-day09-rg \
  --location eastus \
  --sku Basic \
  --vm-size c0
```

This takes 10-15 minutes. Check status:
```bash
az redis show \
  --name az204redis<unique> \
  --resource-group az204-day09-rg \
  --query "{name:name, provisioningState:provisioningState, hostName:hostName, port:sslPort}" \
  --output table
```

Get the connection string:
```bash
az redis list-keys \
  --name az204redis<unique> \
  --resource-group az204-day09-rg \
  --query primaryKey \
  --output tsv
```

---

## Step 2 — Create the .NET Project

```bash
mkdir az204-redis-lab
cd az204-redis-lab
dotnet new console -n RedisApp
cd RedisApp
dotnet add package StackExchange.Redis
dotnet add package System.Text.Json
```

---

## Step 3 — Write the Redis Application

Replace `Program.cs`:

```csharp
using StackExchange.Redis;
using System.Text.Json;

// --- Connection (use as singleton in real apps!) ---
string redisHost = "az204redis<unique>.redis.cache.windows.net";
string redisKey = "<your-primary-key>";
string connectionString = $"{redisHost}:6380,password={redisKey},ssl=True,abortConnect=False";

var redis = ConnectionMultiplexer.Connect(connectionString);
IDatabase db = redis.GetDatabase();

Console.WriteLine("Connected to Azure Cache for Redis!\n");

// ===== 1. String Operations =====
Console.WriteLine("=== String Operations ===");

// Set a simple string
await db.StringSetAsync("greeting", "Hello from AZ-204 Redis Lab!");
string greeting = await db.StringGetAsync("greeting");
Console.WriteLine($"  greeting = {greeting}");

// Set with expiration (TTL)
await db.StringSetAsync("session:user123", "active", TimeSpan.FromSeconds(30));
Console.WriteLine($"  session:user123 = {await db.StringGetAsync("session:user123")}");
Console.WriteLine($"  TTL: {await db.KeyTimeToLiveAsync("session:user123")}");

// Increment/Decrement (atomic counters)
await db.StringSetAsync("page:views", 0);
await db.StringIncrementAsync("page:views");
await db.StringIncrementAsync("page:views");
await db.StringIncrementAsync("page:views");
Console.WriteLine($"  page:views = {await db.StringGetAsync("page:views")}");

// ===== 2. Hash Operations (Objects) =====
Console.WriteLine("\n=== Hash Operations ===");

var userHash = new HashEntry[]
{
    new("name", "Jane Developer"),
    new("email", "jane@example.com"),
    new("role", "Senior Engineer"),
    new("loginCount", 42)
};

await db.HashSetAsync("user:1001", userHash);

// Get all fields
var allFields = await db.HashGetAllAsync("user:1001");
Console.WriteLine("  user:1001:");
foreach (var field in allFields)
    Console.WriteLine($"    {field.Name} = {field.Value}");

// Get single field
string name = await db.HashGetAsync("user:1001", "name");
Console.WriteLine($"  Name only: {name}");

// Increment a hash field
await db.HashIncrementAsync("user:1001", "loginCount");
Console.WriteLine($"  loginCount after increment: {await db.HashGetAsync("user:1001", "loginCount")}");

// ===== 3. Sorted Set (Leaderboard) =====
Console.WriteLine("\n=== Sorted Set (Leaderboard) ===");

await db.SortedSetAddAsync("leaderboard", "Alice", 2500);
await db.SortedSetAddAsync("leaderboard", "Bob", 3200);
await db.SortedSetAddAsync("leaderboard", "Charlie", 1800);
await db.SortedSetAddAsync("leaderboard", "Diana", 4100);
await db.SortedSetAddAsync("leaderboard", "Eve", 2900);

// Get top 3 (descending)
var top3 = await db.SortedSetRangeByRankWithScoresAsync("leaderboard", 0, 2, Order.Descending);
Console.WriteLine("  Top 3:");
int rank = 1;
foreach (var entry in top3)
    Console.WriteLine($"    #{rank++} {entry.Element}: {entry.Score} points");

// Get rank of a specific player
long? bobRank = await db.SortedSetRankAsync("leaderboard", "Bob", Order.Descending);
Console.WriteLine($"  Bob's rank: #{bobRank + 1}");

// ===== 4. Cache-Aside Pattern =====
Console.WriteLine("\n=== Cache-Aside Pattern ===");

async Task<string> GetProductAsync(string productId)
{
    string cacheKey = $"product:{productId}";

    // Step 1: Check cache
    string cached = await db.StringGetAsync(cacheKey);
    if (cached != null)
    {
        Console.WriteLine($"  CACHE HIT: {cacheKey}");
        return cached;
    }

    // Step 2: Cache miss — "load from database" (simulated)
    Console.WriteLine($"  CACHE MISS: {cacheKey} — loading from database...");
    var product = JsonSerializer.Serialize(new
    {
        id = productId,
        name = $"Product {productId}",
        price = 99.99,
        loadedAt = DateTime.UtcNow
    });

    // Step 3: Store in cache with TTL
    await db.StringSetAsync(cacheKey, product, TimeSpan.FromMinutes(5));

    return product;
}

// First call — cache miss
string result1 = await GetProductAsync("P-100");
Console.WriteLine($"  Result: {result1}");

// Second call — cache hit
string result2 = await GetProductAsync("P-100");
Console.WriteLine($"  Result: {result2}");

// ===== 5. Key Expiration Demo =====
Console.WriteLine("\n=== Key Expiration ===");

await db.StringSetAsync("temp-key", "I will expire in 5 seconds", TimeSpan.FromSeconds(5));
Console.WriteLine($"  Before expiry: {await db.StringGetAsync("temp-key")}");
Console.WriteLine("  Waiting 6 seconds...");
await Task.Delay(6000);
string expired = await db.StringGetAsync("temp-key");
Console.WriteLine($"  After expiry: {(expired.IsNull ? "(null - expired!)" : expired.ToString())}");

// ===== 6. Cleanup =====
Console.WriteLine("\n=== Cleanup ===");
await db.KeyDeleteAsync(new RedisKey[] {
    "greeting", "session:user123", "page:views",
    "user:1001", "leaderboard", "product:P-100", "temp-key"
});
Console.WriteLine("  All keys deleted.");

redis.Dispose();
Console.WriteLine("\nLab complete!");
```

---

## Step 4 — Run the Application

```bash
dotnet run
```

Expected output shows all Redis operations with cache hits/misses and TTL behavior.

---

## Step 5 — Monitor Redis via CLI

Check Redis metrics:
```bash
az redis show \
  --name az204redis<unique> \
  --resource-group az204-day09-rg \
  --query "{name:name, usedMemory:instances[0].nonSslPort, connectedClients:provisioningState}" \
  --output table
```

Use the Redis console in the Azure Portal:
1. Go to your Redis instance in the Portal
2. Click "Console" in the left menu
3. Run Redis commands directly: `PING`, `INFO`, `DBSIZE`

---

## Cleanup

```bash
cd ../..
rm -rf az204-redis-lab

az group delete --name az204-day09-rg --yes --no-wait
```

---

## Expected Outcomes

| Task | Status |
|------|--------|
| Created Azure Cache for Redis (Basic C0) | ✅ |
| Connected with StackExchange.Redis | ✅ |
| String operations with TTL | ✅ |
| Hash operations (user profile) | ✅ |
| Sorted set (leaderboard) | ✅ |
| Cache-aside pattern implementation | ✅ |
| Key expiration demonstration | ✅ |

---

Next: [Day 10 — API Management](../day-10-api/theory.md)
