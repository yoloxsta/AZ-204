# Lab: Azure Cosmos DB — Create, Query, and Use the SDK

## Title
Build a Cosmos DB Application with the SQL API

## Objective
- Create a Cosmos DB account with the SQL (NoSQL) API
- Create a database and container with a partition key
- Insert, query, and manage documents via CLI and SDK
- Explore consistency levels and RU charges
- Use the change feed

## Architecture

```
┌──────────────────────────────────────────────────────┐
│           Resource Group: az204-day04-rg              │
│                                                      │
│  ┌────────────────────────────────────────────────┐  │
│  │  Cosmos DB Account: az204cosmos<unique>        │  │
│  │  API: NoSQL (SQL)                              │  │
│  │  Consistency: Session                          │  │
│  │                                                │  │
│  │  ┌──────────────────────────────────────────┐  │  │
│  │  │  Database: ProductsDB                    │  │  │
│  │  │                                          │  │  │
│  │  │  ┌────────────────────────────────────┐  │  │  │
│  │  │  │  Container: Products              │  │  │  │
│  │  │  │  Partition Key: /category         │  │  │  │
│  │  │  │  Throughput: 400 RU/s             │  │  │  │
│  │  │  │                                    │  │  │  │
│  │  │  │  { id, name, category, price }    │  │  │  │
│  │  │  └────────────────────────────────────┘  │  │  │
│  │  └──────────────────────────────────────────┘  │  │
│  └────────────────────────────────────────────────┘  │
│                                                      │
│  ┌────────────────────────────────────────────────┐  │
│  │  .NET Console App (Cosmos DB SDK)              │  │
│  │  - CRUD operations                             │  │
│  │  - Parameterized queries                       │  │
│  │  - Read RU charges                             │  │
│  └────────────────────────────────────────────────┘  │
└──────────────────────────────────────────────────────┘
```

## Prerequisites
- Azure CLI installed and logged in
- .NET 8 SDK installed

---

## Step 1 — Create Resource Group and Cosmos DB Account

```bash
az group create \
  --name az204-day04-rg \
  --location eastus

az cosmosdb create \
  --name az204cosmos<unique> \
  --resource-group az204-day04-rg \
  --default-consistency-level Session \
  --locations regionName=eastus failoverPriority=0 isZoneRedundant=false \
  --kind GlobalDocumentDB
```

This takes 3-5 minutes. Verify:
```bash
az cosmosdb show \
  --name az204cosmos<unique> \
  --resource-group az204-day04-rg \
  --query "{name:name, location:location, consistencyPolicy:consistencyPolicy.defaultConsistencyLevel, endpoint:documentEndpoint}" \
  --output table
```

---

## Step 2 — Create a Database and Container

```bash
# Create database
az cosmosdb sql database create \
  --account-name az204cosmos<unique> \
  --resource-group az204-day04-rg \
  --name ProductsDB

# Create container with partition key
az cosmosdb sql container create \
  --account-name az204cosmos<unique> \
  --resource-group az204-day04-rg \
  --database-name ProductsDB \
  --name Products \
  --partition-key-path "/category" \
  --throughput 400
```

Verify:
```bash
az cosmosdb sql container show \
  --account-name az204cosmos<unique> \
  --resource-group az204-day04-rg \
  --database-name ProductsDB \
  --name Products \
  --query "{name:name, partitionKey:resource.partitionKey.paths[0], throughput:resource.defaultTtl}" \
  --output table
```

---

## Step 3 — Get Connection Details

```bash
# Get the endpoint
az cosmosdb show \
  --name az204cosmos<unique> \
  --resource-group az204-day04-rg \
  --query documentEndpoint \
  --output tsv

# Get the primary key
az cosmosdb keys list \
  --name az204cosmos<unique> \
  --resource-group az204-day04-rg \
  --query primaryMasterKey \
  --output tsv
```

Save these values — you'll need them for the SDK.

---

## Step 4 — Create a .NET Console Application

```bash
mkdir az204-cosmos-lab
cd az204-cosmos-lab
dotnet new console -n CosmosApp
cd CosmosApp
dotnet add package Microsoft.Azure.Cosmos
```

---

## Step 5 — Write the Cosmos DB Application

Replace `Program.cs` with:

```csharp
using Microsoft.Azure.Cosmos;
using System.Net;

// --- Configuration ---
string endpoint = "https://az204cosmos<unique>.documents.azure.com:443/";
string key = "<your-primary-key>";
string databaseName = "ProductsDB";
string containerName = "Products";

// --- Initialize client ---
CosmosClient client = new CosmosClient(endpoint, key, new CosmosClientOptions
{
    ApplicationName = "AZ204-CosmosLab",
    SerializerOptions = new CosmosSerializationOptions
    {
        PropertyNamingPolicy = CosmosPropertyNamingPolicy.CamelCase
    }
});

Database database = client.GetDatabase(databaseName);
Container container = database.GetContainer(containerName);

Console.WriteLine("Connected to Cosmos DB!\n");

// --- 1. Create items ---
var products = new[]
{
    new Product("1", "Laptop Pro", "Electronics", 1299.99, 50),
    new Product("2", "Wireless Mouse", "Electronics", 29.99, 200),
    new Product("3", "Standing Desk", "Furniture", 499.99, 30),
    new Product("4", "Monitor 27\"", "Electronics", 349.99, 75),
    new Product("5", "Office Chair", "Furniture", 299.99, 45),
    new Product("6", "USB-C Hub", "Electronics", 49.99, 150),
    new Product("7", "Bookshelf", "Furniture", 189.99, 20)
};

Console.WriteLine("Creating items...");
foreach (var product in products)
{
    ItemResponse<Product> response = await container.CreateItemAsync(
        product,
        new PartitionKey(product.Category));

    Console.WriteLine($"  Created: {product.Name} | RU charge: {response.RequestCharge}");
}

// --- 2. Point read (most efficient operation) ---
Console.WriteLine("\n--- Point Read ---");
ItemResponse<Product> readResponse = await container.ReadItemAsync<Product>(
    "1",
    new PartitionKey("Electronics"));

Console.WriteLine($"Read: {readResponse.Resource.Name} - ${readResponse.Resource.Price}");
Console.WriteLine($"RU charge: {readResponse.RequestCharge}");

// --- 3. Query with partition key (efficient) ---
Console.WriteLine("\n--- Query: Electronics under $100 ---");
QueryDefinition query = new QueryDefinition(
    "SELECT * FROM c WHERE c.category = @category AND c.price < @maxPrice")
    .WithParameter("@category", "Electronics")
    .WithParameter("@maxPrice", 100);

FeedIterator<Product> iterator = container.GetItemQueryIterator<Product>(
    query,
    requestOptions: new QueryRequestOptions
    {
        PartitionKey = new PartitionKey("Electronics")
    });

double totalRUs = 0;
while (iterator.HasMoreResults)
{
    FeedResponse<Product> resultSet = await iterator.ReadNextAsync();
    totalRUs += resultSet.RequestCharge;
    foreach (Product item in resultSet)
    {
        Console.WriteLine($"  {item.Name} - ${item.Price}");
    }
}
Console.WriteLine($"Total RU charge: {totalRUs}");

// --- 4. Cross-partition query (more expensive) ---
Console.WriteLine("\n--- Cross-Partition Query: All items sorted by price ---");
QueryDefinition crossQuery = new QueryDefinition(
    "SELECT c.name, c.category, c.price FROM c ORDER BY c.price DESC");

FeedIterator<Product> crossIterator = container.GetItemQueryIterator<Product>(crossQuery);

totalRUs = 0;
while (crossIterator.HasMoreResults)
{
    FeedResponse<Product> resultSet = await crossIterator.ReadNextAsync();
    totalRUs += resultSet.RequestCharge;
    foreach (Product item in resultSet)
    {
        Console.WriteLine($"  {item.Name} ({item.Category}) - ${item.Price}");
    }
}
Console.WriteLine($"Cross-partition RU charge: {totalRUs}");

// --- 5. Replace (update) an item ---
Console.WriteLine("\n--- Update Item ---");
readResponse.Resource.Price = 1199.99;
readResponse.Resource.Stock = 45;

ItemResponse<Product> updateResponse = await container.ReplaceItemAsync(
    readResponse.Resource,
    readResponse.Resource.Id,
    new PartitionKey(readResponse.Resource.Category));

Console.WriteLine($"Updated: {updateResponse.Resource.Name} new price: ${updateResponse.Resource.Price}");
Console.WriteLine($"RU charge: {updateResponse.RequestCharge}");

// --- 6. Upsert an item ---
Console.WriteLine("\n--- Upsert Item ---");
var newProduct = new Product("8", "Webcam HD", "Electronics", 79.99, 100);
ItemResponse<Product> upsertResponse = await container.UpsertItemAsync(
    newProduct,
    new PartitionKey(newProduct.Category));

Console.WriteLine($"Upserted: {upsertResponse.Resource.Name} (Status: {upsertResponse.StatusCode})");
Console.WriteLine($"RU charge: {upsertResponse.RequestCharge}");

// --- 7. Delete an item ---
Console.WriteLine("\n--- Delete Item ---");
ItemResponse<Product> deleteResponse = await container.DeleteItemAsync<Product>(
    "7",
    new PartitionKey("Furniture"));

Console.WriteLine($"Deleted bookshelf. RU charge: {deleteResponse.RequestCharge}");

// --- 8. Read all items in a partition ---
Console.WriteLine("\n--- All Furniture items ---");
FeedIterator<Product> partitionIterator = container.GetItemQueryIterator<Product>(
    "SELECT * FROM c",
    requestOptions: new QueryRequestOptions
    {
        PartitionKey = new PartitionKey("Furniture")
    });

while (partitionIterator.HasMoreResults)
{
    FeedResponse<Product> resultSet = await partitionIterator.ReadNextAsync();
    foreach (Product item in resultSet)
    {
        Console.WriteLine($"  {item.Name} - ${item.Price} (Stock: {item.Stock})");
    }
}

Console.WriteLine("\nLab complete!");
client.Dispose();

// --- Model ---
public record Product(
    string Id,
    string Name,
    string Category,
    double Price,
    int Stock)
{
    public string id { get; set; } = Id;
}
```

---

## Step 6 — Run the Application

```bash
dotnet run
```

Expected output shows each operation with its RU charge. Notice:
- Point reads cost ~1 RU
- Single-partition queries cost 3-5 RUs
- Cross-partition queries cost significantly more
- Writes cost ~5-10 RUs

---

## Step 7 — Explore Consistency Levels via CLI

Check current consistency:
```bash
az cosmosdb show \
  --name az204cosmos<unique> \
  --resource-group az204-day04-rg \
  --query consistencyPolicy \
  --output json
```

Update to Bounded Staleness (for demonstration):
```bash
az cosmosdb update \
  --name az204cosmos<unique> \
  --resource-group az204-day04-rg \
  --default-consistency-level BoundedStaleness \
  --max-staleness-prefix 100 \
  --max-interval 5
```

Reset back to Session:
```bash
az cosmosdb update \
  --name az204cosmos<unique> \
  --resource-group az204-day04-rg \
  --default-consistency-level Session
```

---

## Step 8 — Configure Indexing Policy

Update the indexing policy to only index queried fields:

```bash
az cosmosdb sql container update \
  --account-name az204cosmos<unique> \
  --resource-group az204-day04-rg \
  --database-name ProductsDB \
  --name Products \
  --idx '{
    "indexingMode": "consistent",
    "includedPaths": [
      {"path": "/category/?"},
      {"path": "/price/?"},
      {"path": "/name/?"}
    ],
    "excludedPaths": [
      {"path": "/*"}
    ]
  }'
```

---

## Cleanup

```bash
cd ../..
rm -rf az204-cosmos-lab

az group delete --name az204-day04-rg --yes --no-wait
```

---

## Expected Outcomes

| Task | Status |
|------|--------|
| Created Cosmos DB account (SQL API) | ✅ |
| Created database and container with partition key | ✅ |
| Inserted documents via SDK | ✅ |
| Performed point reads | ✅ |
| Ran single-partition and cross-partition queries | ✅ |
| Observed RU charges for different operations | ✅ |
| Updated and deleted items | ✅ |
| Explored consistency levels | ✅ |
| Configured indexing policy | ✅ |

---

Next: [Day 05 — Containers](../day-05-container/theory.md)
