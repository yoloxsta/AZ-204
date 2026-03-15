# Day 04 — Azure Cosmos DB

## What You'll Learn Today

- What Cosmos DB is and why it exists
- APIs: SQL (NoSQL), MongoDB, Cassandra, Gremlin, Table
- Partitioning strategy and partition keys
- Consistency levels (the 5 models)
- Request Units (RUs) and throughput
- Change feed
- The .NET SDK for Cosmos DB

---

## 1. What is Azure Cosmos DB?

Cosmos DB is a globally distributed, multi-model NoSQL database. It provides single-digit millisecond latency, automatic scaling, and multiple consistency models.

### Why Developers Use It

- **Global distribution** — replicate data to any Azure region with a click
- **Multi-model** — use SQL, MongoDB, Cassandra, Gremlin, or Table API
- **Guaranteed latency** — <10ms reads, <15ms writes at the 99th percentile
- **Elastic scale** — throughput and storage scale independently
- **No schema enforcement** — store JSON documents without rigid schemas

### Cosmos DB Hierarchy

```
Cosmos DB Account
  └── Database
       └── Container (collection/table)
            └── Items (documents/rows)
```

- **Account** — top-level resource, defines API and global distribution
- **Database** — namespace for containers, can share throughput
- **Container** — where data lives, has a partition key and optional indexing policy
- **Item** — a single JSON document (in SQL API)

---

## 2. APIs

| API | Data Model | Use Case |
|-----|-----------|----------|
| **NoSQL (SQL)** | JSON documents | New apps, flexible queries (AZ-204 focus) |
| **MongoDB** | BSON documents | Migrate existing MongoDB apps |
| **Cassandra** | Wide-column | Migrate existing Cassandra apps |
| **Gremlin** | Graph | Social networks, recommendations |
| **Table** | Key-value | Migrate from Azure Table Storage |

For AZ-204, focus on the **NoSQL (SQL) API**.

---

## 3. Partitioning

Partitioning is the most important design decision in Cosmos DB.

### How It Works

- Each container has a **partition key** (a property in your JSON documents)
- Cosmos DB uses the partition key value to distribute data across **physical partitions**
- All items with the same partition key value are in the same **logical partition**
- A logical partition can hold up to 20 GB

### Choosing a Good Partition Key

| Good | Bad | Why |
|------|-----|-----|
| `/userId` | `/country` | High cardinality, even distribution |
| `/tenantId` | `/status` | Avoids hot partitions |
| `/deviceId` | `/createdDate` | Queries filter on it |

Rules of thumb:
1. High cardinality (many distinct values)
2. Even distribution of data and requests
3. Frequently used in WHERE clauses
4. Immutable (can't change after creation)

### Cross-Partition Queries

- Queries that filter on the partition key → efficient (single partition)
- Queries without partition key filter → fan-out to all partitions (expensive)

---

## 4. Consistency Levels

Cosmos DB offers 5 consistency levels, from strongest to weakest:

| Level | Guarantee | Latency | Use Case |
|-------|-----------|---------|----------|
| **Strong** | Linearizable reads | Highest | Financial transactions |
| **Bounded Staleness** | Reads lag by K versions or T time | High | Leaderboards, counters |
| **Session** | Read-your-own-writes within a session | Medium | Most apps (default) |
| **Consistent Prefix** | Reads never see out-of-order writes | Low | Social feeds |
| **Eventual** | No ordering guarantee | Lowest | Analytics, non-critical reads |

**Session consistency** is the default and recommended for most applications. It guarantees that within a single client session, you always read what you wrote.

---

## 5. Request Units (RUs)

RUs are the currency of Cosmos DB. Every operation costs RUs:

| Operation | Approximate Cost |
|-----------|-----------------|
| Point read (1 KB item by ID + partition key) | 1 RU |
| Write (1 KB item) | ~5 RUs |
| Query (depends on complexity) | 3-100+ RUs |

### Throughput Models

| Model | Description | Use Case |
|-------|-------------|----------|
| **Provisioned** | Set RU/s manually (400-unlimited) | Predictable workloads |
| **Autoscale** | Scales between 10% and max RU/s | Variable workloads |
| **Serverless** | Pay per RU consumed | Dev/test, low traffic |

---

## 6. Change Feed

The change feed is a persistent, ordered log of changes to items in a container.

- Captures inserts and updates (not deletes by default)
- Ordered within a partition key
- Can be processed by Azure Functions, the Change Feed Processor, or the SDK
- Use cases: event sourcing, real-time analytics, cache invalidation, data sync

---

## 7. Indexing

By default, Cosmos DB indexes every property in every document. You can customize:

- **Include/exclude paths** — index only what you query
- **Index types** — Range (default), Spatial (geospatial), Composite (multi-property ORDER BY)
- **Indexing mode** — Consistent (default), None (disable indexing)

Custom indexing policy example:
```json
{
  "indexingMode": "consistent",
  "includedPaths": [
    { "path": "/name/?" },
    { "path": "/category/?" }
  ],
  "excludedPaths": [
    { "path": "/*" }
  ],
  "compositeIndexes": [
    [
      { "path": "/category", "order": "ascending" },
      { "path": "/name", "order": "ascending" }
    ]
  ]
}
```

---

## Key Takeaways

1. Cosmos DB is globally distributed NoSQL — use the SQL (NoSQL) API for AZ-204
2. Partition key choice is critical — high cardinality, even distribution
3. 5 consistency levels — Session is the default and most common
4. RUs are the cost unit — point reads are cheapest, cross-partition queries are expensive
5. Change feed enables event-driven patterns on top of Cosmos DB
6. Default indexing covers everything — customize to optimize RU cost

---

Next: [Lab — Cosmos DB](./lab-cosmos.md)
