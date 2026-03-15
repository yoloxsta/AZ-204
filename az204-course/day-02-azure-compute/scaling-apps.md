# Scaling Apps in Azure App Service — Deep Dive

## Overview

This module covers the 6 key scaling topics for Azure App Service that appear on the AZ-204 exam.

---

## 1. Examine Autoscale Factors

Autoscale adjusts the number of instances running your app based on rules you define. It's available on Standard (S1+) and Premium tiers.

### Scale Up vs Scale Out

| Action | What Changes | How |
|--------|-------------|-----|
| **Scale Up** | Instance size (CPU, RAM) | Change App Service Plan SKU |
| **Scale Out** | Number of instances | Add more identical instances |

Scale up is vertical scaling — a bigger machine. Scale out is horizontal scaling — more machines.

```bash
# Scale up (change SKU)
az appservice plan update --name myplan --resource-group myRG --sku P1v3

# Scale out (manual — set instance count)
az appservice plan update --name myplan --resource-group myRG --number-of-workers 3
```

---

## 2. Identify Autoscale Factors

Autoscale decisions are based on metrics. Common metrics:

| Metric | Source | Use Case |
|--------|--------|----------|
| **CPU Percentage** | App Service Plan | General load |
| **Memory Percentage** | App Service Plan | Memory-intensive apps |
| **HTTP Queue Length** | App Service | Request backlog |
| **Data In / Data Out** | App Service | Network-bound apps |
| **Disk Queue Length** | App Service | I/O-bound apps |
| **Custom metric** | Application Insights | Business-specific |

You can also scale based on:
- **Schedule** — scale to 5 instances during business hours, 2 at night
- **External metrics** — Service Bus queue depth, Event Hub backlog

---

## 3. Enable Autoscale in App Service

Autoscale is configured through Azure Monitor autoscale settings.

```bash
# Create autoscale setting
az monitor autoscale create \
  --resource-group myRG \
  --resource myplan \
  --resource-type Microsoft.Web/serverfarms \
  --name myautoscale \
  --min-count 1 \
  --max-count 10 \
  --count 2
```

### Autoscale Profiles

A profile defines the instance limits and rules for a specific condition:

- **Default profile** — always active unless overridden
- **Recurring schedule** — specific days/times (e.g., weekdays 9-5)
- **Fixed date** — specific date range (e.g., Black Friday)

```bash
# Add a recurring schedule profile
az monitor autoscale profile create \
  --resource-group myRG \
  --autoscale-name myautoscale \
  --name "Business Hours" \
  --min-count 3 \
  --max-count 10 \
  --count 5 \
  --recurrence week Mon Tue Wed Thu Fri \
  --start "09:00" \
  --end "18:00" \
  --timezone "Eastern Standard Time"
```

---

## 4. Explore Autoscale Best Practices

### Rule Design
- Always pair a scale-out rule with a scale-in rule
- Use different thresholds for scale-out and scale-in to avoid "flapping"
  - Scale out at CPU > 70%
  - Scale in at CPU < 40% (not 70%)
- Use the **average** aggregation for most metrics (not max or min)
- Set a sufficient **cool-down period** (default 5 minutes) to let metrics stabilize

### Instance Limits
- Set a reasonable **minimum** (at least 2 for availability)
- Set a **maximum** based on your budget and backend capacity
- The **default** count is used when metrics can't be read

### Notifications
- Configure email or webhook notifications for scale events
- Monitor autoscale activity in the Activity Log

```bash
# Add a scale-out rule
az monitor autoscale rule create \
  --resource-group myRG \
  --autoscale-name myautoscale \
  --condition "CpuPercentage > 70 avg 5m" \
  --scale out 2

# Add a scale-in rule (pair with above)
az monitor autoscale rule create \
  --resource-group myRG \
  --autoscale-name myautoscale \
  --condition "CpuPercentage < 40 avg 5m" \
  --scale in 1
```

---

## 5. Scale Up vs Scale Out — When to Use Each

| Scenario | Recommendation |
|----------|---------------|
| App needs more CPU/RAM per request | Scale up |
| App handles many concurrent requests | Scale out |
| Traffic is predictable and steady | Scale up to right size |
| Traffic is variable/spiky | Scale out with autoscale |
| App is stateless | Scale out (preferred) |
| App has in-memory state | Scale up (or externalize state) |

For AZ-204, scale out with autoscale is the primary focus. Stateless apps that externalize session state (to Redis, for example) scale out most effectively.

---

## 6. Configure Autoscale Rules and Conditions

### Rule Components

Each autoscale rule has:
- **Metric source** — which resource to monitor
- **Metric name** — which metric (CPU, memory, queue length)
- **Time aggregation** — how to aggregate over the time window (Average, Min, Max, Sum, Count)
- **Operator** — comparison (>, <, >=, <=, ==, !=)
- **Threshold** — the value to compare against
- **Duration** — time window for evaluation (e.g., 5 minutes)
- **Action** — increase or decrease count (by amount or percentage)
- **Cool down** — wait time after scaling before evaluating again

### Example: Complete Autoscale Configuration

```bash
# Create autoscale with default profile
az monitor autoscale create \
  --resource-group myRG \
  --resource myplan \
  --resource-type Microsoft.Web/serverfarms \
  --min-count 2 --max-count 10 --count 2

# Scale out when CPU > 70% for 5 minutes
az monitor autoscale rule create \
  --resource-group myRG \
  --autoscale-name myplan \
  --condition "CpuPercentage > 70 avg 5m" \
  --scale out 2 \
  --cooldown 5

# Scale out when HTTP queue > 100 for 5 minutes
az monitor autoscale rule create \
  --resource-group myRG \
  --autoscale-name myplan \
  --condition "HttpQueueLength > 100 avg 5m" \
  --scale out 1

# Scale in when CPU < 30% for 10 minutes
az monitor autoscale rule create \
  --resource-group myRG \
  --autoscale-name myplan \
  --condition "CpuPercentage < 30 avg 10m" \
  --scale in 1 \
  --cooldown 10
```

---

## Key Exam Points

1. Autoscale requires Standard (S1) tier or higher
2. Always pair scale-out rules with scale-in rules
3. Use different thresholds for out/in to prevent flapping
4. Cool-down period prevents rapid scaling oscillation
5. Default profile is used when no schedule matches
6. Autoscale evaluates ALL rules — if ANY scale-out rule is met, it scales out; ALL scale-in rules must be met to scale in
7. Maximum instance count is a hard limit — autoscale never exceeds it
