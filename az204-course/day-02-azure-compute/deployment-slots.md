# Exploring App Service Deployment Slots — Deep Dive

## Overview

This module covers the 6 key deployment slot topics for Azure App Service that appear on the AZ-204 exam.

---

## 1. Explore Staging Environments

Deployment slots are live apps with their own hostnames. Each slot is a full App Service instance.

- Available on Standard (S1) tier and above
- Standard: 5 slots, Premium: 20 slots
- Each slot has its own URL: `myapp-staging.azurewebsites.net`
- Slots share the same App Service Plan (compute resources)

### Common Slot Patterns

| Slot | Purpose |
|------|---------|
| Production | Live traffic |
| Staging | Pre-production validation |
| Dev | Development testing |
| Canary | Small percentage of live traffic |

---

## 2. Examine Slot Swapping

A swap exchanges the content and configuration between two slots. During a swap:

1. Settings from the **target slot** (production) are applied to the **source slot** (staging)
2. The source slot restarts with the target's settings (warm-up)
3. The source slot's warmed-up content is moved to the target slot
4. The target slot's old content moves to the source slot

This means:
- Production never goes down — the swap is atomic
- The staging slot gets the old production code (instant rollback)
- Warm-up happens before traffic switches

### Swap with Preview (Multi-Phase Swap)

A two-phase swap that lets you validate before completing:

1. Phase 1: Apply target settings to source, warm up (but don't swap traffic)
2. You validate the source slot with production settings
3. Phase 2: Complete the swap (or cancel)

```bash
# Start swap with preview
az webapp deployment slot swap \
  --name myapp --resource-group myRG \
  --slot staging --target-slot production \
  --action preview

# Complete the swap
az webapp deployment slot swap \
  --name myapp --resource-group myRG \
  --slot staging --target-slot production

# Or cancel
az webapp deployment slot swap \
  --name myapp --resource-group myRG \
  --slot staging --action reset
```

---

## 3. Swap Deployment Slots

### Standard Swap

```bash
az webapp deployment slot swap \
  --name myapp --resource-group myRG \
  --slot staging \
  --target-slot production
```

### Auto Swap

Configure a slot to automatically swap to production after code is deployed:

```bash
az webapp deployment slot auto-swap \
  --name myapp --resource-group myRG \
  --slot staging \
  --auto-swap-slot production
```

Auto swap is useful for CI/CD pipelines where you want zero-touch deployment to production after staging validation.

### What Happens During a Swap

```
Before Swap:
  Production: Code v1 + Production Settings
  Staging:    Code v2 + Staging Settings

Swap Process:
  1. Apply Production Settings → Staging (warm-up)
  2. Wait for staging to respond to health checks
  3. Swap routing: Production ↔ Staging

After Swap:
  Production: Code v2 + Production Settings  ← new code, same settings
  Staging:    Code v1 + Staging Settings     ← old code for rollback
```

---

## 4. Route Traffic in App Service

You can route a percentage of production traffic to a different slot for canary testing.

```bash
# Route 10% of traffic to staging
az webapp traffic-routing set \
  --name myapp --resource-group myRG \
  --distribution staging=10
```

Users are randomly assigned to a slot and stay on that slot for the session (via `x-ms-routing-name` cookie).

### Manual Routing

Users can opt into a specific slot by adding a query parameter:
```
https://myapp.azurewebsites.net/?x-ms-routing-name=staging
```

Or by setting the cookie:
```
x-ms-routing-name=staging
```

### Clear Traffic Routing

```bash
az webapp traffic-routing clear --name myapp --resource-group myRG
```

---

## 5. Configure Slot-Sticky Settings

Settings can be either:
- **Swap with the code** (default) — move to the target slot during swap
- **Slot-sticky** — stay with the slot, don't move during swap

### What Should Be Slot-Sticky

| Setting | Sticky? | Why |
|---------|---------|-----|
| `ASPNETCORE_ENVIRONMENT` | Yes | Different per environment |
| Database connection string (staging DB) | Yes | Points to different DB per slot |
| Application Insights key | Yes | Separate telemetry per slot |
| Feature flags for testing | Yes | Only enabled in staging |
| API keys for external services | Usually no | Same service in all environments |
| App-specific business settings | Usually no | Should follow the code |

```bash
# Mark settings as slot-sticky
az webapp config appsettings set \
  --name myapp --resource-group myRG \
  --slot-settings ASPNETCORE_ENVIRONMENT=Staging DB_CONNECTION="staging-db-conn"
```

### Connection Strings

Connection strings can also be slot-sticky:
```bash
az webapp config connection-string set \
  --name myapp --resource-group myRG \
  --slot staging \
  --slot-settings StagingDb="Server=staging;..."
```

---

## 6. Roll Back and Test in Production

### Instant Rollback

After a swap, the old production code is in the staging slot. To roll back:

```bash
# Just swap again — old code goes back to production
az webapp deployment slot swap \
  --name myapp --resource-group myRG \
  --slot staging --target-slot production
```

This is instant because the old code is already warmed up in the staging slot.

### Testing in Production

Use traffic routing to test with real production traffic:

1. Deploy new code to staging
2. Route 5% of traffic to staging: `az webapp traffic-routing set --distribution staging=5`
3. Monitor error rates and performance in Application Insights
4. If good, increase to 20%, then 50%, then swap
5. If bad, route 0% back to staging and fix

### Health Check Integration

Configure a health check endpoint that App Service uses during swap warm-up:

```bash
az webapp config set \
  --name myapp --resource-group myRG \
  --generic-configurations '{"healthCheckPath": "/health"}'
```

During a swap, App Service pings the health check endpoint. If it returns a non-200 status, the swap is paused.

---

## Key Exam Points

1. Slots share the same App Service Plan — they don't add compute cost
2. Swap is atomic — production never goes down
3. After a swap, the old code is in the source slot (instant rollback)
4. Slot-sticky settings stay with the slot, not the code
5. Traffic routing enables canary deployments with percentage-based routing
6. Auto swap automatically swaps after deployment to a slot
7. Swap with preview is a two-phase swap for extra validation
8. Health check endpoints are validated during swap warm-up
