# Day 19 — Advanced Monitoring: KQL, Dashboards & Cost Management

## What You'll Learn Today

- KQL (Kusto Query Language) for log analytics
- Custom dashboards and workbooks
- Alert rules and action groups
- Live Metrics Stream
- Cost management and optimization
- Application Insights advanced features (sampling, adaptive sampling, correlation)

---

## 1. KQL Deep Dive

KQL (Kusto Query Language) is the query language for Azure Data Explorer, Log Analytics, and Application Insights. Mastering KQL is essential for effective monitoring.

### Query Structure

```
<SourceTable>
| <Filter>
| <Transform>
| <Project>
```

- Queries are **pipe-delimited** — each operator receives output from the previous one
- Table name is always **first**, not last (unlike SQL)
- KQL is **case-sensitive** for column and table names
- String values use **single quotes**, not double quotes

### Scalar vs Tabular Queries

| Type | Returns | Use Case |
|------|---------|----------|
| **Scalar** | Single value | `ago(1h)`, `now()`, `datetime_diff()` |
| **Tabular** | Table with rows/columns | Most queries (`requests`, `traces`) |

Most queries are tabular. Scalar expressions appear inside operators.

### Core Operators

#### `where` — Filter Rows

```kql
requests
| where timestamp > ago(1h)
| where resultCode == "200"
| where name startswith "GET"
```

Common filter patterns:

| Pattern | Example |
|---------|---------|
| Equality | `where name == "Login"` |
| Inequality | `where resultCode != "200"` |
| Contains | `where name contains "order"` |
| Startswith | `where name startswith "GET"` |
| Regex | `where name matches regex "^(GET\|POST)/api/.*"` |
| Has | `where cloud_RoleName has "prod"` (exact token match, case-insensitive) |
| Has_any | `where properties has_any ("error", "warning")` |

#### `extend` — Add Computed Columns

```kql
requests
| where timestamp > ago(24h)
| extend durationMs = duration * 1000
| extend hourOfDay = hourofday(timestamp)
| extend isSuccess = resultCode startswith "2"
| project timestamp, name, durationMs, hourOfDay, isSuccess
```

#### `project` — Select and Rename Columns

```kql
requests
| project 
    Time = timestamp,
    Endpoint = name,
    Status = resultCode,
    Duration_s = round(duration, 2)
| take 100
```

#### `summarize` — Aggregate Data

```kql
// Request count by endpoint
requests
| where timestamp > ago(1h)
| summarize RequestCount = count() by name
| order by RequestCount desc

// Average, P50, P95, P99 response times
requests
| where timestamp > ago(1h)
| summarize 
    AvgDuration = avg(duration),
    P50 = percentile(duration, 50),
    P95 = percentile(duration, 95),
    P99 = percentile(duration, 99)
    by name

// Error rate percentage
requests
| where timestamp > ago(1h)
| summarize 
    Total = count(),
    Errors = countif(resultCode != "200")
    by name
| extend ErrorRate = round(Errors * 100.0 / Total, 2)
| order by ErrorRate desc
```

#### `join` — Combine Tables

```kql
// Find requests that have associated exceptions
requests
| where timestamp > ago(1h)
| join kind=inner (
    exceptions
    | project exceptionId, operation_Id
) on operation_Id

// Left join — all requests, plus exception info if available
requests
| where timestamp > ago(1h)
| join kind=leftouter (
    exceptions
    | project exceptionId, operation_Id
) on operation_Id
| project timestamp, name, resultCode, exceptionId
```

Join types:

| Type | Description |
|------|-------------|
| `inner` | Only matching rows from both sides |
| `leftouter` | All left rows, matched right rows (or null) |
| `rightouter` | All right rows, matched left rows (or null) |
| `fullouter` | All rows from both sides |
| `anti` | Rows in left that have no match in right |
| `leftanti` | Same as `anti` (alias) |

#### `make-series` — Time Series Analysis

```kql
// Request count per 5-minute bucket over the last 24 hours
requests
| where timestamp > ago(24h)
| make-series Count = count() on timestamp from ago(24h) to now() step 5m
| render timechart

// P95 latency over time with default value for gaps
requests
| where timestamp > ago(7d)
| make-series 
    P95 = percentile(duration, 95) default=0 
    on timestamp from ago(7d) to now() step 1h
| render timechart

// Multiple series by endpoint
requests
| where timestamp > ago(24h)
| make-series 
    Count = count() default=0
    on timestamp from ago(24h) to now() step 5m
    by name
| render timechart
```

#### `render` — Visualization

```kql
// Time chart (default)
requests
| where timestamp > ago(1h)
| summarize count() by bin(timestamp, 5m)
| render timechart

// Bar chart
dependencies
| where timestamp > ago(1h)
| summarize count() by target
| render barchart

// Pie chart
requests
| summarize count() by resultCode
| render piechart

// Table (explicit)
requests
| take 100
| render table
```

### KQL Functions

Save reusable query logic as **saved functions** in Log Analytics:

```kql
// Define a function
// Name: FailedRequests
// Parameters: lookback:timespan = 1h
FailedRequests(lookback:timespan = 1h) {
    requests
    | where timestamp > ago(lookback)
    | where resultCode !startswith "2"
}

// Use the function
FailedRequests(ago(1h))
| summarize count() by name
```

### Advanced Patterns

#### Percentiles and Statistics

```kql
requests
| where timestamp > ago(1h)
| summarize 
    count = count(),
    avg = avg(duration),
    stdev = stdev(duration),
    min = min(duration),
    max = max(duration),
    p50 = percentile(duration, 50),
    p95 = percentile(duration, 95),
    p99 = percentile(duration, 99)
    by name
```

#### Anomaly Detection

```kql
requests
| where timestamp > ago(7d)
| make-series Count = count() on timestamp from ago(7d) to now() step 1h
| extend (anomalies, baseline, score) = series_decompose_anomalies(Count)
| render timechart
```

#### Forecasting

```kql
requests
| where timestamp > ago(30d)
| make-series Count = count() on timestamp from ago(30d) to now() step 1h
| extend (forecast, lower, upper) = series_eforecast(Count, 24h)
| render timechart
```

#### Moving Average

```kql
requests
| where timestamp > ago(7d)
| make-series Count = count() on timestamp from ago(7d) to now() step 1h
| extend Smooth = series_smooth(Count, 3)
| render timechart
```

---

## 2. Log Analytics Workspace

### Data Collection Rules (DCR)

Data Collection Rules control **what data** is collected and **where it goes**.

| Component | Purpose |
|-----------|---------|
| **Data sources** | Which logs/metrics to collect (Windows Events, Syslog, Performance counters) |
| **Transforms** | Filter or project data before ingestion |
| **Destinations** | One or more Log Analytics workspaces |

```
┌─────────────────┐     ┌─────────────────────────┐     ┌──────────────┐
│  Data Sources    │────►│  Data Collection Rule    │────►│  Destinations │
│                  │     │                          │     │              │
│  - VM Insights   │     │  Transform:              │     │  - Workspace │
│  - Syslog        │     │    filter where level != │     │    (primary) │
│  - perf counters │     │    "verbose"             │     │              │
│  - App Logs      │     │                          │     │  - Workspace │
│                  │     │                          │     │    (backup)  │
└─────────────────┘     └─────────────────────────┘     └──────────────┘
```

### Retention Policies

| Setting | Default | Range | Impact |
|---------|---------|-------|--------|
| **Log retention** | 30 days | 30–730 days | Longer = higher cost |
| **Archive retention** | 0 days (archive off) | 0–730 days | Archive to storage account |
| **Interactive retention** | 30 days | Up to 2 years | Queryable in Log Analytics |

```bash
# Set retention to 90 days
az monitor log-analytics workspace update \
  --resource-group my-rg \
  --workspace-name my-workspace \
  --retention-time 90
```

### Query Packs

Query packs are **collections of saved queries** that can be shared across workspaces and subscriptions.

```
┌──────────────────────────────────┐
│         Query Pack               │
│  ┌────────────────────────────┐  │
│  │  Saved Queries             │  │
│  │  - Slow Requests           │  │
│  │  - Error Rate by Endpoint  │  │
│  │  - Failed Dependencies     │  │
│  │  - Custom Metrics          │  │
│  └────────────────────────────┘  │
│                                  │
│  Scope: Subscription or Region   │
└──────────────────────────────────┘
```

### Saved Queries

Individual queries saved in a Log Analytics workspace:

```kql
// Saved query: "Top 10 Slow Endpoints"
requests
| where timestamp > ago(1h)
| summarize AvgDuration = avg(duration), Count = count() by name
| order by AvgDuration desc
| take 10
```

---

## 3. Custom Dashboards

### Azure Portal Dashboards

Dashboards are JSON-based visual layouts in the Azure portal.

| Widget Type | Description |
|-------------|-------------|
| **Metrics chart** | Time-series chart from Azure Monitor metrics |
| **Log query** | KQL query results as table or chart |
| **Resource group** | Grouped resource tiles |
| **ARM template** | Deploy button for ARM templates |
| **Markdown** | Static text or instructions |
| **Cost analysis** | Cost breakdown by service |

**Pinning a query to a dashboard:**

1. Run a KQL query in Log Analytics
2. Click "Pin to dashboard"
3. Choose existing or new dashboard
4. Configure refresh interval

### Azure Workbooks

Workbooks are **interactive reports** with rich visualizations.

```
┌─────────────────────────────────────────────────────────────┐
│  Workbook: Application Health Dashboard                     │
│                                                             │
│  ┌─────────────┐  ┌─────────────┐  ┌─────────────┐        │
│  │  Requests/s  │  │  Error Rate │  │  P95 Latency│        │
│  │  1,234      │  │  0.3%       │  │  45ms       │        │
│  │  ▲ +12%     │  │  ▼ -0.1%    │  │  ▲ +5ms     │        │
│  └─────────────┘  └─────────────┘  └─────────────┘        │
│                                                             │
│  ┌──────────────────────────────────────────────────────┐  │
│  │  Request Volume Over Time                             │  │
│  │  [Area chart from make-series query]                  │  │
│  └──────────────────────────────────────────────────────┘  │
│                                                             │
│  ┌──────────────────────────────────────────────────────┐  │
│  │  Top 10 Slowest Endpoints                             │  │
│  │  [Table with sparkline column]                        │  │
│  └──────────────────────────────────────────────────────┘  │
└─────────────────────────────────────────────────────────────┘
```

Workbook features:

- **KQL queries** as data sources
- **Parameters** for dynamic filtering (dropdowns, date pickers)
- **Visualizations**: charts, grids, tiles, maps, timers
- **Groups** for collapsible sections
- **Conditional visibility** based on parameter values

### KPI Widgets

KPI tiles show a single value with context:

| Component | Purpose |
|-----------|---------|
| **Value** | The metric (e.g., 99.9%) |
| **Target** | Goal (e.g., 99.95%) |
| **Status** | Good/Warning/Critical |
| **Trend** | Direction arrow (up/down/stable) |

---

## 4. Alert Rules

### Metric Alerts

Triggered when a metric crosses a threshold.

```bash
az monitor metrics alert create \
  --name "High CPU" \
  --resource-group my-rg \
  --scopes /subscriptions/.../providers/Microsoft.Compute/virtualMachines/myVM \
  --condition "avg Percentage CPU > 80" \
  --window-size 5m \
  --evaluation-frequency 1m \
  --action /subscriptions/.../actionGroups/myActionGroup \
  --description "VM CPU exceeds 80% for 5 minutes"
```

| Parameter | Purpose |
|-----------|---------|
| `--window-size` | Time window for aggregation |
| `--evaluation-frequency` | How often to check |
| `--condition` | Metric, aggregation type, threshold |

**Aggregation types:**

| Type | Use Case |
|------|----------|
| `Average` | CPU, memory, request duration |
| `Sum` | Request count, error count |
| `Count` | Number of data points |
| `Minimum` | Minimum value in window |
| `Maximum` | Maximum value in window |

### Dynamic Thresholds

Instead of static thresholds, dynamic thresholds use **machine learning** to learn normal patterns.

```bash
az monitor metrics alert create \
  --name "Anomalous Request Rate" \
  --resource-group my-rg \
  --scopes $APP_INSIGHTS_ID \
  --condition "count requests/duration > dynamic medium" \
  --window-size 5m \
  --evaluation-frequency 5m \
  --action $ACTION_GROUP_ID
```

| Sensitivity | Description |
|-------------|-------------|
| `low` | Wider band, fewer alerts |
| `medium` | Balanced |
| `high` | Narrower band, more alerts |

**Dynamic thresholds:**

- Automatically detect patterns (daily, weekly seasonality)
- Handle trends and gradual changes
- Require **at least 3 days** of historical data
- Best for metrics with predictable patterns

### Log Alerts

Triggered when a KQL query returns results.

```bash
az monitor scheduled-query create \
  --name "Exception Spike" \
  --resource-group my-rg \
  --scopes $APP_INSIGHTS_ID \
  --condition "count > 10 where exceptions | summarize count() | where count_ > 10" \
  --window-size 15m \
  --evaluation-frequency 5m \
  --action $ACTION_GROUP_ID \
  --description "Alert when more than 10 exceptions in 15 minutes"
```

**Query schedules:**

| Setting | Purpose |
|---------|---------|
| Look-back | How far back to query (e.g., 15 min) |
| Frequency | How often to run |
| Time zone | For scheduled queries |

### Activity Log Alerts

Triggered by Azure resource events.

```bash
az monitor activity-log alert create \
  --name "VM Deleted" \
  --resource-group my-rg \
  --condition category=Administrative,operationName=Microsoft.Compute/virtualMachines/delete \
  --action-group $ACTION_GROUP_ID
```

Common activity log events:

| Event | Description |
|-------|-------------|
| `Microsoft.Compute/virtualMachines/delete` | VM deleted |
| `Microsoft.Resources/subscriptions/resourceGroups/delete` | Resource group deleted |
| `Microsoft.Authorization/roleAssignments/write` | Role assigned |
| `Microsoft.Storage/storageAccounts/delete` | Storage account deleted |

### Alert Processing Rules

Modify alert behavior without changing the alert rule itself:

| Action | Use Case |
|--------|----------|
| **Suppress notifications** | During maintenance windows |
| **Add action group** | Add extra recipients |
| **Remove action group** | Reduce noise |

---

## 5. Action Groups

Action Groups define **what happens** when an alert fires.

### Notification Types

| Type | Configuration |
|------|---------------|
| **Email** | Email address(es) |
| **SMS** | Phone number with country code |
| **Push** | Azure mobile app notification |
| **Webhook** | HTTP POST to URL with JSON payload |
| **Automation runbook** | Trigger a PowerShell or Python runbook |
| **ITSM** | Create or update ITSM ticket |
| **Logic App** | Trigger a Logic App |

### Webhook Payload

```json
{
  "schemaId": "AzureMonitorMetricAlert",
  "data": {
    "version": "2.0",
    "status": "Activated",
    "context": {
      "activityLog": {
        "resource": "subscriptions/.../providers/Microsoft.Compute/virtualMachines/myVM",
        "operationName": "Microsoft.Compute/virtualMachines/delete",
        "status": "Succeeded"
      }
    },
    "properties": {
      "key1": "value1"
    }
  }
}
```

### Creating Action Groups

```bash
# Email + SMS + Webhook
az monitor action-group create \
  --name critical-alerts \
  --resource-group my-rg \
  --short-name critAlerts \
  --action email admin admin@example.com \
  --action sms admin-sms +15551234567 \
  --action webhook webhook1 https://myapi.com/alerts

# ITSM integration
az monitor action-group create \
  --name itsm-alerts \
  --resource-group my-rg \
  --short-name itsmAlerts \
  --action itsm itsm-action \
    --itsm-workspace-id /subscriptions/.../Microsoft.OperationalInsights/workspaces/my-workspace \
    --itsm-connection-id "conn-123" \
    --itsm-ticket-class "incident" \
    --itsm-ticket-area "Azure" \
    --itsm-ticket-impact "high" \
    --itsm-ticket-urgency "high"
```

---

## 6. Live Metrics Stream

Live Metrics Stream provides **real-time, sub-second** monitoring without sampling.

### Features

| Feature | Description |
|---------|-------------|
| **Real-time metrics** | Requests, response time, CPU — no sampling |
| **Request tracking** | See individual requests as they arrive |
| **Profiling** | CPU and memory profiles of live app |
| **Dependency tracking** | Real-time external calls |
| **Filtering** | Filter by request name, dependency target |
| **Alerting** | Add alerts directly from Live Metrics |

### When to Use Live Metrics

- **During deployment** — verify new version works
- **Incident investigation** — watch live traffic
- **Load testing** — real-time performance feedback
- **Demo/debug** — see requests as they happen

### Accessing Live Metrics

```
Azure Portal → Application Insights → Live Metrics
```

Or programmatically:
```csharp
// C# — configure live metrics
builder.Services.Configure<TelemetryConfiguration>(config =>
{
    var liveMetrics = new LiveMetricFeedConfiguration();
    config.SetMetricAggregationScope(MetricAggregationScope.Default, liveMetrics);
});
```

---

## 7. Application Insights Advanced

### Adaptive Sampling

Automatically adjusts the telemetry volume to stay within performance and cost targets.

| Setting | Default | Range |
|---------|---------|-------|
| **Max telemetry items per second** | 20 | 1–1000 |
| **Initial sampling percentage** | 33% | 0–100% |
| **Min sampling percentage** | 1% | 0–100% |

```csharp
builder.Services.AddApplicationInsightsTelemetry(options =>
{
    options.ConnectionString = connection;
});

// Configure adaptive sampling
builder.Services.Configure<TelemetryConfiguration>(config =>
{
    config.DefaultTelemetrySink.TelemetryProcessorChainBuilder
        .UseAdaptiveSampling(excludedTypes: "Event;Exception;Trace");
});
```

**Excluded types** (never sampled):

| Type | Reason |
|------|--------|
| `Exception` | Critical for debugging |
| `Event` | Business-critical events |
| `Trace` | Diagnostic logs |

### Custom Telemetry

Track domain-specific data alongside standard telemetry:

```csharp
// Custom event
telemetry.TrackEvent("OrderCompleted", 
    properties: new Dictionary<string, string> { ["orderId"] = orderId },
    metrics: new Dictionary<string, double> { ["orderTotal"] = 125.50 });

// Custom metric
telemetry.GetMetric("ActiveConnections").TrackValue(42);

// Custom dependency
telemetry.TrackDependency("Redis", "GET session:abc", startTime, duration, true);

// Track page view (browser)
telemetry.TrackPageView("Checkout");

// Initialize operation for distributed tracing
using var operation = telemetry.StartOperation<RequestTelemetry>("ProcessOrder");
```

### Availability Tests

External tests that check your app from different Azure regions.

| Test Type | Description |
|-----------|-------------|
| **URL ping** | Simple HTTP GET with status code check |
| **Multi-step** | Multi-request sequences (login, navigate, act) |
| **SSL** | Certificate expiry monitoring |
| **DNS** | DNS resolution checks |

```bash
az monitor app-insights availability-test create \
  --name "Homepage Check" \
  --resource-group my-rg \
  --app my-app-insights \
  --url https://myapp.azurewebsites.net \
  --frequency 300 \
  --timeout 30 \
  --expected-status-code 200 \
  --geo-locations eastus westus northeurope
```

### Application Insights Profiler

Continuous CPU profiling in production with minimal overhead.

| Feature | Description |
|---------|-------------|
| **CPU profiling** | See which functions consume CPU |
| **Heap profiling** | Memory allocation tracking |
| **Timeline** | See call stack over time |
| **Hot paths** | Identify bottlenecks |

```
┌─────────────────────────────────────────────────────────┐
│  Profiler Timeline                                      │
│                                                         │
│  ▓▓▓░░▓▓▓▓▓░░░▓▓▓▓▓▓▓░░░▓▓▓▓▓░░░▓▓▓▓▓░░░▓▓▓▓░░░     │
│  ─────────────────────────────────────────────────►     │
│                                                         │
│  ▓ = CPU active   ░ = CPU idle                          │
│                                                         │
│  Hot path: ProcessOrder → ValidateItems → DB.Insert     │
│  Total: 234ms | DB.Insert: 180ms (77%)                 │
└─────────────────────────────────────────────────────────┘
```

### Snapshot Debugger

Captures a **full snapshot** of your application at the point of an exception, including variable values and call stack.

| Feature | Description |
|---------|-------------|
| **Snippets** | Code around the exception line |
| **Variables** | Local and parameter values at exception time |
| **Call stack** | Full stack trace with source context |
| **Point-and-click** | Enable from portal |

---

## 8. Cost Management

### Azure Cost Management + Billing

The central portal for monitoring, allocating, and optimizing cloud costs.

```
┌─────────────────────────────────────────────────────────────┐
│              Azure Cost Management                          │
│                                                             │
│  ┌──────────────┐  ┌──────────────┐  ┌──────────────┐     │
│  │  Cost Analysis │  │  Budgets     │  │  Recommendations│  │
│  │              │  │              │  │              │     │
│  │  Charts,     │  │  Set limits, │  │  Right-sizing,│     │
│  │  filters,    │  │  alerts at   │  │  reservations,│     │
│  │  export      │  │  50%, 75%,   │  │  waste       │     │
│  │              │  │  100%        │  │  cleanup     │     │
│  └──────────────┘  └──────────────┘  └──────────────┘     │
│                                                             │
│  ┌──────────────┐  ┌──────────────┐  ┌──────────────┐     │
│  │  Exports      │  │  Cost Alerts │  │  Advisor      │     │
│  │              │  │              │  │              │     │
│  │  Schedule CSV│  │  Budget      │  │  Personalized│     │
│  │  or JSON to  │  │  threshold   │  │  cost        │     │
│  │  storage     │  │  notifications│ │  recommendations│  │
│  └──────────────┘  └──────────────┘  └──────────────┘     │
└─────────────────────────────────────────────────────────────┘
```

### Cost Analysis Views

| View | Purpose |
|------|---------|
| **Cost by service** | Breakdown by Azure service |
| **Cost by resource** | Individual resource costs |
| **Cost by location** | Regional cost distribution |
| **Cost by tag** | Business unit, environment, team |
| **Daily costs** | Cost trend over time |

### Budgets

Set spending limits with alerts at configurable thresholds:

```bash
# Create a budget with alerts at 50%, 75%, 100%
az monitor billing budget create \
  --budget-name monthly-budget \
  --amount 1000 \
  --resource-group my-rg \
  --time-grain Monthly \
  --start-date 2026-01-01T00:00:00Z \
  --end-date 2026-12-31T23:59:59Z \
  --notifications '[{"enabled":true,"operator":"GreaterThanOrEqualTo","threshold":50,"actionGroup":"/subscriptions/.../actionGroups/budget-alerts"},{"enabled":true,"operator":"GreaterThanOrEqualTo","threshold":75,"actionGroup":"/subscriptions/.../actionGroups/budget-alerts"},{"enabled":true,"operator":"GreaterThanOrEqualTo","threshold":100,"actionGroup":"/subscriptions/.../actionGroups/budget-alerts"}]'
```

### Cost Optimization Recommendations

Azure Advisor provides personalized recommendations:

| Category | Recommendation |
|----------|----------------|
| **Right-sizing** | Downsize underutilized VMs |
| **Reserved Instances** | Purchase reservations for steady workloads |
| **Spot Instances** | Use spot VMs for non-critical workloads |
| **Storage** | Move old data to cooler tiers |
| **Idle resources** | Delete unused IPs, disks, VMs |
| **Shut-down** | Auto-shutdown dev/test VMs |

---

## 9. Cost Optimization Strategies

### Right-Sizing

Match resource capacity to actual usage.

| Metric | Threshold | Action |
|--------|-----------|--------|
| CPU < 5% sustained | 7 days | Downsize VM |
| CPU > 80% sustained | 7 days | Upsize VM |
| Memory < 20% used | 7 days | Reduce RAM |
| Disk IOPS < 10% | 7 days | Lower disk tier |

### Autoscaling

Scale out/in based on demand instead of running at peak capacity 24/7.

| Strategy | Benefit |
|----------|---------|
| **Scale out** | Handle traffic spikes |
| **Scale in** | Reduce cost during low traffic |
| **Scheduled** | Predictable patterns |
| **Metric-based** | Reactive to actual load |

### Reserved Capacity

Pay upfront for 1 or 3 years for significant discounts.

| Resource | 1-Year Savings | 3-Year Savings |
|----------|----------------|----------------|
| VMs | ~30% | ~50-60% |
| App Service | ~35% | ~55% |
| SQL Database | ~30% | ~50% |
| Cosmos DB | ~30% | ~50% |
| Storage | ~20% | ~35% |

### Spot Instances

Use unused Azure capacity at steep discounts (up to 90%).

| Use Case | Suitable? |
|----------|-----------|
| Batch processing | Yes |
| Dev/test | Yes |
| CI/CD agents | Yes |
| Stateless APIs | Maybe (with fallback) |
| Databases | No |
| Critical workloads | No |

### Storage Tiering

Move data to cheaper storage as it ages.

```
┌──────────┐    ┌──────────┐    ┌──────────┐    ┌──────────┐
│   Hot    │───►│   Cool   │───►│  Cold    │───►│ Archive  │
│  (0-30d) │    │ (30-90d) │    │(90-365d) │    │ (>365d)  │
│          │    │          │    │          │    │          │
│  $/GB:   │    │  $/GB:   │    │  $/GB:   │    │  $/GB:   │
│  highest │    │  medium  │    │  low     │    │  lowest  │
└──────────┘    └──────────┘    └──────────┘    └──────────┘
```

---

## 10. Architecture Diagram

```
┌──────────────────────────────────────────────────────────────────────────┐
│                        Advanced Monitoring Architecture                  │
│                                                                          │
│  ┌────────────────────────────────────────────────────────────────────┐  │
│  │  Application Layer                                                 │  │
│  │  ┌──────────┐  ┌──────────┐  ┌──────────┐  ┌──────────┐        │  │
│  │  │ App Svc  │  │ Function │  │ AKS Pod  │  │ Container│        │  │
│  │  │          │  │  App     │  │          │  │  App     │        │  │
│  │  └────┬─────┘  └────┬─────┘  └────┬─────┘  └────┬─────┘        │  │
│  └───────┼──────────────┼──────────────┼──────────────┼──────────────┘  │
│          │              │              │              │                  │
│          ▼              ▼              ▼              ▼                  │
│  ┌────────────────────────────────────────────────────────────────────┐  │
│  │  Instrumentation Layer                                             │  │
│  │  ┌──────────────────────────────────────────────────────────────┐  │  │
│  │  │  Application Insights SDK / Auto-Instrumentation             │  │  │
│  │  │  - Request tracking    - Dependency tracking                 │  │  │
│  │  │  - Exception tracking  - Custom telemetry                   │  │  │
│  │  │  - Live Metrics        - Profiling                          │  │  │
│  │  └──────────────────────────┬───────────────────────────────────┘  │  │
│  └─────────────────────────────┼──────────────────────────────────────┘  │
│                                │                                        │
│                                ▼                                        │
│  ┌────────────────────────────────────────────────────────────────────┐  │
│  │  Collection Layer                                                  │  │
│  │  ┌──────────────────────┐    ┌──────────────────────┐            │  │
│  │  │  Application Insights │    │  Log Analytics        │            │  │
│  │  │  (telemetry endpoint) │───►│  Workspace            │            │  │
│  │  └──────────────────────┘    │                      │            │  │
│  │                              │  ┌──────────────────┐│            │  │
│  │  ┌──────────────────────┐    │  │  KQL Engine      ││            │  │
│  │  │  Data Collection Rules│───►│  │  - Query packs   ││            │  │
│  │  │  - VM metrics         │    │  │  - Saved queries ││            │  │
│  │  │  - Platform logs      │    │  │  - Functions     ││            │  │
│  │  │  - Resource logs      │    │  └──────────────────┘│            │  │
│  │  └──────────────────────┘    └──────────────────────┘            │  │
│  └────────────────────────────────────────────────────────────────────┘  │
│                                │                                        │
│           ┌────────────────────┼────────────────────┐                   │
│           ▼                    ▼                    ▼                   │
│  ┌─────────────────┐ ┌─────────────────┐ ┌─────────────────┐          │
│  │   Dashboards    │ │   Alerts &      │ │ Cost Management  │          │
│  │   & Workbooks   │ │   Action Groups │ │                  │          │
│  │                 │ │                 │ │  - Budgets       │          │
│  │  - KPI widgets  │ │  - Metric alerts│ │  - Recommendations│         │
│  │  - KQL charts   │ │  - Log alerts   │ │  - Exports       │          │
│  │  - Parameters   │ │  - Dynamic      │ │  - Analysis      │          │
│  │  - Shared view  │ │  - Activity log │ │  - Advisor       │          │
│  └─────────────────┘ └─────────────────┘ └─────────────────┘          │
│           │                    │                    │                   │
│           ▼                    ▼                    ▼                   │
│  ┌────────────────────────────────────────────────────────────────────┐  │
│  │  Consumers                                                         │  │
│  │  ┌──────────┐  ┌──────────┐  ┌──────────┐  ┌──────────┐        │  │
│  │  │  Teams   │  │  Slack /  │  │  ITSM    │  │  Power BI │        │  │
│  │  │  Email   │  │  Webhook  │  │  Systems │  │          │        │  │
│  │  └──────────┘  └──────────┘  └──────────┘  └──────────┘        │  │
│  └────────────────────────────────────────────────────────────────────┘  │
└──────────────────────────────────────────────────────────────────────────┘
```

---

## 11. Key Concepts for AZ-204

### Must-Know

| Concept | Details |
|---------|---------|
| **KQL operators** | `where`, `extend`, `project`, `summarize`, `join`, `make-series`, `render` |
| **KQL functions** | Scalar (single value), tabular (table result) |
| **Saved functions** | Reusable query logic in Log Analytics |
| **Data Collection Rules** | Control what data is collected and where it goes |
| **Metric alerts** | Threshold-based or dynamic |
| **Log alerts** | KQL-based alerting |
| **Activity log alerts** | Azure resource event alerts |
| **Action groups** | Email, SMS, webhook, ITSM, runbook |
| **Adaptive sampling** | Automatic telemetry volume reduction |
| **Live Metrics** | Real-time, sub-second monitoring |
| **Application Insights Profiler** | CPU and memory profiling |
| **Snapshot Debugger** | Exception-time code snapshots |
| **Cost Management** | Budgets, recommendations, exports |
| **Reserved capacity** | Pre-pay for discounts |
| **Spot instances** | Discounted unused capacity |

### Common KQL Exam Patterns

```kql
-- Request success rate
requests
| summarize Total=count(), Success=countif(resultCode startswith "2")
| extend SuccessRate = round(Success * 100.0 / Total, 2)

-- Slow dependencies
dependencies
| where duration > 1000
| summarize count() by target, type
| order by count_ desc

-- Exception trends
exceptions
| make-series Count=count() on timestamp from ago(7d) to now() step 1h
| render timechart

-- Custom event analysis
customEvents
| where name == "Purchase"
| extend Amount = todouble(customMeasurements.amount)
| summarize TotalAmount=sum(Amount), Count=count() by bin(timestamp, 1d)

-- Join requests and dependencies
requests
| join kind=leftouter (
    dependencies
    | project operation_Id, dependencyTarget=target, dependencyDuration=duration
) on operation_Id
| where dependencyDuration > 500
| project timestamp, name, dependencyTarget, dependencyDuration
```

### Quiz Yourself

1. What is the difference between `where` and `project`?
2. How do you create a time-series chart with KQL?
3. When should you use dynamic thresholds vs static thresholds?
4. What telemetry types are excluded from adaptive sampling by default?
5. What is the difference between a metric alert and a log alert?
6. How do you set up a budget with multiple alert thresholds?
7. What is the benefit of reserved capacity vs pay-as-you-go?
8. When should you use Spot VMs?
9. What does Live Metrics Stream show that the portal dashboard doesn't?
10. How do Data Collection Rules differ from diagnostic settings?

---

Next: [Lab — Advanced Monitoring](./lab-monitoring-advanced.md)
