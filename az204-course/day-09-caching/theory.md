# Day 09 — Caching & Content Delivery

## What You'll Learn Today

- Caching patterns and strategies
- Azure Cache for Redis: tiers, data types, use cases
- Azure CDN: profiles, endpoints, caching rules
- Cache-aside pattern implementation

---

## 1. Why Caching?

Caching stores frequently accessed data in a fast, in-memory store to reduce latency and database load.

### Common Caching Patterns

| Pattern | Description | Use Case |
|---------|-------------|----------|
| **Cache-Aside** | App checks cache first, loads from DB on miss | General purpose |
| **Write-Through** | Write to cache and DB simultaneously | Data consistency |
| **Write-Behind** | Write to cache, async write to DB | High write throughput |
| **Read-Through** | Cache loads from DB automatically on miss | Simplified app code |

### Cache-Aside Pattern (Most Common for AZ-204)

```
1. App checks cache for data
2. Cache HIT → return cached data
3. Cache MISS → read from database
4. Store result in cache
5. Return data to caller
```

```
         ┌──────────┐
    ┌───►│  Cache   │───► HIT: return data
    │    │  (Redis) │
    │    └──────────┘
    │         │
    │      MISS
    │         │
┌───┴───┐     ▼
│  App  │◄── Read from DB
│       │──► Store in cache
└───────┘
```

---

## 2. Azure Cache for Redis

Azure Cache for Redis is a managed Redis service. Redis is an in-memory data store that supports strings, hashes, lists, sets, sorted sets, and more.

### Tiers

| Tier | Memory | Features | Use Case |
|------|--------|----------|----------|
| **Basic** | 250 MB - 53 GB | No SLA, no replication | Dev/test |
| **Standard** | 250 MB - 53 GB | Replication, SLA | Production |
| **Premium** | 6 GB - 120 GB | Clustering, persistence, VNet | High performance |
| **Enterprise** | 12 GB - 2 TB | Redis modules, active geo-replication | Enterprise |

### Redis Data Types

| Type | Description | Example |
|------|-------------|---------|
| **String** | Simple key-value | Session tokens, counters |
| **Hash** | Object with fields | User profiles |
| **List** | Ordered collection | Recent activity, queues |
| **Set** | Unique unordered collection | Tags, unique visitors |
| **Sorted Set** | Unique ordered by score | Leaderboards, rankings |

### Key Concepts

- **Expiration (TTL)** — keys automatically expire after a set time
- **Eviction policies** — what happens when memory is full (LRU, LFU, random, etc.)
- **Connection pooling** — reuse connections (use `ConnectionMultiplexer` as singleton)
- **Pipelining** — send multiple commands without waiting for each response

---

## 3. Azure CDN

Azure CDN caches static content at edge locations worldwide, reducing latency for users.

### How CDN Works

```
User ──► Edge POP (Point of Presence)
              │
         Cache HIT? ──► Return cached content
              │
         Cache MISS ──► Fetch from origin ──► Cache at edge ──► Return
```

### CDN Profiles and Endpoints

- **Profile** — a collection of CDN endpoints (defines the CDN provider)
- **Endpoint** — a specific URL that serves cached content
- **Origin** — the source of the content (Storage, App Service, custom URL)

### Caching Rules

- **Global caching rules** — apply to all requests
- **Custom caching rules** — match on path or extension
- **Query string caching** — ignore, bypass, or cache each unique query string

### Cache Purging

Force removal of cached content before TTL expires:
```bash
az cdn endpoint purge \
  --resource-group myRG \
  --profile-name myCDN \
  --name myEndpoint \
  --content-paths "/images/*" "/css/*"
```

---

## Key Takeaways

1. Cache-aside is the most common pattern — check cache, load from DB on miss
2. Azure Cache for Redis is managed Redis — use Standard tier for production
3. Use `ConnectionMultiplexer` as a singleton — don't create per request
4. Set TTL on cached data to prevent stale data
5. CDN caches static content at edge locations — reduces latency globally
6. Purge CDN cache when content changes

---

## Deep Dive

For detailed coverage of Azure CDN, see:
- [Developing for Storage on CDNs — Deep Dive](./cdn-deep-dive.md)

---

Next: [Lab — Redis Cache](./lab-redis.md)
