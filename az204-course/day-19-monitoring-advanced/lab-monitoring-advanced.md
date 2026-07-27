# Lab: Advanced Monitoring and KQL

## Title
Master KQL Queries, Alerts, and Cost Management

## Objective
- Write KQL queries for common monitoring scenarios
- Create custom dashboards and workbooks
- Configure alert rules with dynamic thresholds
- Set up action groups with multiple notification types
- Analyze costs with Azure Cost Management

## Architecture

```
┌──────────────────────────────────────────────────────────────────────┐
│                  Resource Group: az204-day19-rg                       │
│                                                                      │
│  ┌──────────────────┐    ┌──────────────────────────────────┐       │
│  │  App Service      │───►│  Application Insights           │       │
│  │  az204-mon-app    │    │  az204-appinsights-adv          │       │
│  │                   │    │                                  │       │
│  │  Sends:           │    │  Collects:                       │       │
│  │  - Requests       │    │  - Request telemetry             │       │
│  │  - Dependencies   │    │  - Dependency tracking           │       │
│  │  - Custom events  │    │  - Custom events/metrics         │       │
│  │  - Exceptions     │    │  - Exceptions                    │       │
│  └──────────────────┘    │  - Live Metrics (real-time)      │       │
│                          │                                  │       │
│                          │  ┌────────────────────────────┐  │       │
│                          │  │  Log Analytics Workspace   │  │       │
│                          │  │  az204-law-adv             │  │       │
│                          │  │                            │  │       │
│                          │  │  KQL Queries:              │  │       │
│                          │  │  - Saved functions         │  │       │
│                          │  │  - Query packs             │  │       │
│                          │  └────────────────────────────┘  │       │
│                          └──────────────────────────────────┘       │
│                                                                      │
│  ┌──────────────────────────────────────────────────────────────┐   │
│  │  Alert Rules                                                  │   │
│  │  ┌────────────────────┐  ┌────────────────────────────────┐ │   │
│  │  │ Metric Alert        │  │ Log Alert                      │ │   │
│  │  │ Response time P95   │  │ Exception count > 10 in 15min │ │   │
│  │  │ > 2s for 5 min      │  │                                │ │   │
│  │  └─────────┬──────────┘  └──────────────┬─────────────────┘ │   │
│  └────────────┼─────────────────────────────┼───────────────────┘   │
│               │                             │                       │
│               ▼                             ▼                       │
│  ┌──────────────────────────────────────────────────────────────┐   │
│  │  Action Group: az204-alerts-adv                              │   │
│  │  - Email: admin@example.com                                  │   │
│  │  - Webhook: https://myapi.com/alerts                         │   │
│  └──────────────────────────────────────────────────────────────┘   │
│                                                                      │
│  ┌──────────────────────────────────────────────────────────────┐   │
│  │  Cost Management                                              │   │
│  │  Budget: $500/month with alerts at 50%, 75%, 100%           │   │
│  └──────────────────────────────────────────────────────────────┘   │
└──────────────────────────────────────────────────────────────────────┘
```

## Prerequisites
- Azure CLI installed and logged in
- .NET 8 SDK installed
- An Azure subscription with contributor access

---

## Step 1 — Create Log Analytics Workspace and Application Insights

```bash
# Create resource group
az group create --name az204-day19-rg --location eastus

# Create Log Analytics workspace with 90-day retention
az monitor log-analytics workspace create \
  --resource-group az204-day19-rg \
  --workspace-name az204-law-adv \
  --location eastus \
  --retention-time 90 \
  --sku PerGB2018

# Create Application Insights linked to the workspace
WORKSPACE_ID=$(az monitor log-analytics workspace show \
  --resource-group az204-day19-rg \
  --workspace-name az204-law-adv \
  --query id --output tsv)

az monitor app-insights component create \
  --app az204-appinsights-adv \
  --resource-group az204-day19-rg \
  --location eastus \
  --workspace $WORKSPACE_ID

# Get connection string and instrumentation key
az monitor app-insights component show \
  --app az204-appinsights-adv \
  --resource-group az204-day19-rg \
  --query connectionString --output tsv > appinsights-conn.txt

az monitor app-insights component show \
  --app az204-appinsights-adv \
  --resource-group az204-day19-rg \
  --query instrumentationKey --output tsv > appinsights-ikey.txt

echo "Connection string saved to appinsights-conn.txt"
cat appinsights-conn.txt
```

---

## Step 2 — Deploy a Sample App with App Insights Integration

```bash
mkdir az204-monitoring-adv-lab
cd az204-monitoring-adv-lab
dotnet new web -n MonitoringAdvApp
cd MonitoringAdvApp
dotnet add package Microsoft.ApplicationInsights.AspNetCore
```

Replace `Program.cs`:

```csharp
using Microsoft.ApplicationInsights;
using Microsoft.ApplicationInsights.DataContracts;
using Microsoft.ApplicationInsights.Extensibility;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddApplicationInsightsTelemetry(options =>
{
    options.ConnectionString = System.IO.File.ReadAllText("../appinsights-conn.txt").Trim();
});

var app = builder.Build();

// Home
app.MapGet("/", () => "Advanced Monitoring Lab — try /api/orders, /api/slow, /api/error, /api/metrics");

// Simulated order endpoint — generates custom events
app.MapGet("/api/orders", (TelemetryClient tc) =>
{
    var orderId = Guid.NewGuid().ToString("N")[..8];
    var total = Random.Shared.Next(10, 500);
    var itemCount = Random.Shared.Next(1, 10);

    tc.TrackEvent("OrderPlaced", new Dictionary<string, string>
    {
        { "orderId", orderId },
        { "region", new[] { "eastus", "westus", "northeurope" }[Random.Shared.Next(3)] }
    }, new Dictionary<string, double>
    {
        { "orderTotal", total },
        { "itemCount", itemCount }
    });

    tc.GetMetric("OrderValue").TrackValue(total);
    tc.GetMetric("ItemsPerOrder").TrackValue(itemCount);

    return Results.Ok(new { orderId, total, itemCount });
});

// Simulated slow dependency
app.MapGet("/api/slow", async (TelemetryClient tc) =>
{
    var delay = Random.Shared.Next(100, 5000);
    using var op = tc.StartOperation<DependencyTelemetry>("SQL-Query");
    op.Telemetry.Type = "SQL";
    op.Telemetry.Target = "az204-db.database.windows.net";
    op.Telemetry.Data = "SELECT * FROM Orders WHERE Status = 'Pending'";

    await Task.Delay(delay);

    op.Telemetry.Success = delay < 2000;
    op.Telemetry.Duration = TimeSpan.FromMilliseconds(delay);

    return Results.Ok(new { queryDuration = delay, slow = delay > 2000 });
});

// Simulated error endpoint
app.MapGet("/api/error", (TelemetryClient tc) =>
{
    try
    {
        var errorType = Random.Shared.Next(3) switch
        {
            0 => new InvalidOperationException("Database connection timeout"),
            1 => new NullReferenceException("User object was null"),
            _ => new ArgumentException("Invalid parameter: userId")
        };
        throw errorType;
    }
    catch (Exception ex)
    {
        tc.TrackException(ex, new Dictionary<string, string>
        {
            { "endpoint", "/api/error" },
            { "severity", "high" }
        });
        return Results.Problem(ex.Message, statusCode: 500);
    }
});

// Custom metrics endpoint
app.MapGet("/api/metrics", (TelemetryClient tc) =>
{
    tc.GetMetric("ActiveUsers").TrackValue(Random.Shared.Next(50, 500));
    tc.GetMetric("QueueDepth").TrackValue(Random.Shared.Next(0, 100));
    tc.GetMetric("CacheHitRate").TrackValue(Random.Shared.Next(70, 100));

    tc.TrackTrace("Metrics recorded", SeverityLevel.Information);
    return Results.Ok(new { message = "Custom metrics recorded" });
});

app.Run();
```

---

## Step 3 — Generate Traffic and Wait for Telemetry

```bash
dotnet run --urls http://localhost:5000 &

# Generate diverse telemetry
for i in $(seq 1 30); do
  curl -s http://localhost:5000/api/orders > /dev/null
  curl -s http://localhost:5000/api/slow > /dev/null
  curl -s http://localhost:5000/api/error > /dev/null
  curl -s http://localhost:5000/api/metrics > /dev/null
  sleep 1
done

echo "Traffic generation complete. Waiting 2-3 minutes for telemetry..."
sleep 180
```

---

## Step 4 — Write KQL Queries

Open the Azure Portal → Log Analytics Workspace → Logs and run these queries:

### 4.1 — Request Overview

```kql
requests
| where timestamp > ago(1h)
| summarize 
    TotalRequests = count(),
    Successful = countif(resultCode startswith "2"),
    Failed = countif(resultCode !startswith "2")
| extend SuccessRate = round(Successful * 100.0 / TotalRequests, 2)
| project TotalRequests, Successful, Failed, SuccessRate
```

### 4.2 — Requests by Endpoint with Percentiles

```kql
requests
| where timestamp > ago(1h)
| summarize 
    Count = count(),
    AvgDuration = avg(duration),
    P50 = percentile(duration, 50),
    P95 = percentile(duration, 95),
    P99 = percentile(duration, 99),
    ErrorRate = round(countif(resultCode !startswith "2") * 100.0 / count(), 2)
    by name
| order by P95 desc
```

### 4.3 — Failed Requests Analysis

```kql
requests
| where timestamp > ago(1h)
| where resultCode !startswith "2"
| summarize Count = count() by name, resultCode
| order by Count desc
| render barchart
```

### 4.4 — Slow Dependencies

```kql
dependencies
| where timestamp > ago(1h)
| where duration > 1000
| summarize 
    Count = count(),
    AvgDuration = avg(duration),
    MaxDuration = max(duration)
    by target, type, name
| order by MaxDuration desc
```

### 4.5 — Exception Analysis

```kql
exceptions
| where timestamp > ago(1h)
| summarize 
    Count = count(),
    FirstSeen = min(timestamp),
    LastSeen = max(timestamp)
    by type, outerMessage
| order by Count desc
```

### 4.6 — Request Volume Time Series

```kql
requests
| where timestamp > ago(24h)
| make-series 
    RequestCount = count() default=0
    on timestamp from ago(24h) to now() step 5m
| render timechart
```

### 4.7 — Custom Event Analysis

```kql
customEvents
| where name == "OrderPlaced"
| extend 
    orderId = tostring(customDimensions.orderId),
    region = tostring(customDimensions.region),
    orderTotal = todouble(customMeasurements.orderTotal),
    itemCount = todouble(customMeasurements.itemCount)
| summarize 
    TotalOrders = count(),
    TotalRevenue = sum(orderTotal),
    AvgOrderValue = avg(orderTotal)
    by region
| order by TotalRevenue desc
```

### 4.8 — Correlation: Requests with Exceptions

```kql
requests
| where timestamp > ago(1h)
| join kind=leftouter (
    exceptions
    | project exceptionId, outerMessage, operation_Id
) on operation_Id
| where isnotempty(exceptionId)
| project timestamp, name, resultCode, exceptionId, outerMessage
| take 100
```

---

## Step 5 — Create a Saved Function and Query Pack

### 5.1 — Create Saved Function

In Log Analytics → Logs, save this as a **function** named `FailedRequests`:

```kql
FailedRequests(lookback:timespan = 1h) {
    requests
    | where timestamp > ago(lookback)
    | where resultCode !startswith "2"
}
```

Now you can use it:
```kql
FailedRequests(ago(1h))
| summarize count() by name
```

### 5.2 — Create Query Pack

```bash
# Create a query pack
az monitor log-analytics query-pack create \
  --resource-group az204-day19-rg \
  --query-pack-name az204-query-pack \
  --location eastus

# Get the query pack ID
QP_ID=$(az monitor log-analytics query-pack show \
  --resource-group az204-day19-rg \
  --query-pack-name az204-query-pack \
  --query id --output tsv)

# Add a saved query to the pack
az monitor log-analytics query-pack query create \
  --query-pack-id $QP_ID \
  --body '{
    "displayName": "Top 10 Slow Endpoints",
    "query": "requests | where timestamp > ago(1h) | summarize AvgDuration = avg(duration), Count = count() by name | order by AvgDuration desc | take 10",
    "timeRange": { "relative": { "duration": 1, "timeUnit": "Hour" } }
  }'
```

---

## Step 6 — Build a Workbook with KQL Visualizations

### 6.1 — Create Workbook

1. Go to **Azure Portal → Monitor → Workbooks**
2. Click **+ New**
3. Add the following components:

**Section 1: KPI Tiles**

```kql
requests
| where timestamp > ago(1h)
| summarize 
    Total = count(),
    Errors = countif(resultCode !startswith "2"),
    AvgDuration = avg(duration)
| extend ErrorRate = round(Errors * 100.0 / Total, 2)
| extend P95 = 0  // Replace with actual P95 query
```

**Section 2: Request Volume Chart**

```kql
requests
| where timestamp > ago(24h)
| make-series Count = count() on timestamp from ago(24h) to now() step 5m
| render timechart
```

**Section 3: Top Endpoints Table**

```kql
requests
| where timestamp > ago(1h)
| summarize 
    Requests = count(),
    AvgDuration = round(avg(duration), 2),
    P95 = round(percentile(duration, 95), 2),
    ErrorRate = round(countif(resultCode !startswith "2") * 100.0 / count(), 2)
    by name
| order by Requests desc
```

**Section 4: Exception Distribution**

```kql
exceptions
| where timestamp > ago(24h)
| summarize Count = count() by type
| render piechart
```

### 6.2 — Add Parameters

Add a **Time Range** parameter:
- Type: Time range picker
- Default: Last 1 hour
- Use in queries with `{timeRange}`

Update the first query to use the parameter:
```kql
requests
| where timestamp {timeRange}
| summarize 
    Total = count(),
    Errors = countif(resultCode !startswith "2"),
    AvgDuration = avg(duration)
| extend ErrorRate = round(Errors * 100.0 / Total, 2)
```

---

## Step 7 — Create a Metric Alert (Response Time)

```bash
APP_INSIGHTS_ID=$(az monitor app-insights component show \
  --app az204-appinsights-adv \
  --resource-group az204-day19-rg \
  --query id --output tsv)

# Create action group first
az monitor action-group create \
  --name az204-alerts-adv \
  --resource-group az204-day19-rg \
  --short-name az204alert \
  --action email admin-email admin@example.com

ACTION_GROUP_ID=$(az monitor action-group show \
  --name az204-alerts-adv \
  --resource-group az204-day19-rg \
  --query id --output tsv)

# Metric alert: Response time P95 > 2 seconds
az monitor metrics alert create \
  --name "High Response Time" \
  --resource-group az204-day19-rg \
  --scopes $APP_INSIGHTS_ID \
  --condition "avg duration/average > 2" \
  --window-size 5m \
  --evaluation-frequency 1m \
  --action $ACTION_GROUP_ID \
  --description "Alert when average response time exceeds 2 seconds for 5 minutes"
```

---

## Step 8 — Create a Log Alert (Exception Count)

```bash
# Log alert: More than 10 exceptions in 15 minutes
az monitor scheduled-query create \
  --name "Exception Spike" \
  --resource-group az204-day19-rg \
  --scopes $APP_INSIGHTS_ID \
  --condition "count > 10" \
  --condition-query "exceptions | summarize count()" \
  --window-size 15m \
  --evaluation-frequency 5m \
  --action $ACTION_GROUP_ID \
  --description "Alert when exception count exceeds 10 in 15 minutes"
```

---

## Step 9 — Configure Action Group (Email + Webhook)

```bash
# Create a comprehensive action group
az monitor action-group create \
  --name az204-alerts-full \
  --resource-group az204-day19-rg \
  --short-name az204full \
  --action email admin-email admin@example.com \
  --action sms admin-sms +15551234567 \
  --action webhook webhook1 https://httpbin.org/post \
  --action automation-runbook runbook1 \
    "/subscriptions/$(az account show --query id -o tsv)/resourceGroups/az204-day19-rg/providers/Microsoft.Automation/automationAccounts/myAccount/runbooks/RestartApp"

# Update alert rules to use the full action group
FULL_AG_ID=$(az monitor action-group show \
  --name az204-alerts-full \
  --resource-group az204-day19-rg \
  --query id --output tsv)

az monitor metrics alert update \
  --name "High Response Time" \
  --resource-group az204-day19-rg \
  --add-action $FULL_AG_ID

az monitor scheduled-query update \
  --name "Exception Spike" \
  --resource-group az204-day19-rg \
  --add-action $FULL_AG_ID
```

---

## Step 10 — Set Up Azure Cost Management Budget

```bash
# Create a budget with multiple threshold alerts
SUBSCRIPTION_ID=$(az account show --query id --output tsv)

az budgets create \
  --budget-name "Monthly-Monitoring-Lab" \
  --resource-group az204-day19-rg \
  --amount 500 \
  --time-grain Monthly \
  --start-date 2026-01-01T00:00:00Z \
  --end-date 2026-12-31T23:59:59Z \
  --notifications '{
    "alert-50": {
      "enabled": true,
      "operator": "GreaterThanOrEqualTo",
      "threshold": 50,
      "contactEmails": ["admin@example.com"],
      "contactGroups": [],
      "contactRoles": ["Owner"]
    },
    "alert-75": {
      "enabled": true,
      "operator": "GreaterThanOrEqualTo",
      "threshold": 75,
      "contactEmails": ["admin@example.com"],
      "contactGroups": [],
      "contactRoles": ["Owner"]
    },
    "alert-100": {
      "enabled": true,
      "operator": "GreaterThanOrEqualTo",
      "threshold": 100,
      "contactEmails": ["admin@example.com"],
      "contactGroups": [],
      "contactRoles": ["Owner"]
    }
  }'
```

---

## Step 11 — Review Cost Recommendations

1. Go to **Azure Portal → Cost Management + Billing → Cost Management**
2. Select your subscription
3. Click **Advisor recommendations** to review:
   - Right-sizing opportunities
   - Reserved instance recommendations
   - Idle resource cleanup
4. Click **Cost analysis** and apply these views:
   - Cost by service
   - Cost by resource
   - Daily costs trend
5. Set up a **cost export** to storage:

```bash
# Create a storage account for cost exports
az storage account create \
  --name az204costexport \
  --resource-group az204-day19-rg \
  --location eastus \
  --sku Standard_LRS

# Create an export schedule
STORAGE_ID=$(az storage account show \
  --name az204costexport \
  --resource-group az204-day19-rg \
  --query id --output tsv)

az costmanagement export create \
  --name "DailyCostExport" \
  --scope "subscriptions/$SUBSCRIPTION_ID" \
  --schedule-status Active \
  --schedule-recurrence Daily \
  --recurrence-period from=2026-01-01T00:00:00Z,to=2026-12-31T23:59:59Z \
  --export-type ActualCost \
  --storage-account-id $STORAGE_ID \
  --storage-container exports \
  --timeframe MonthToDate \
  --recurrence-interval 1
```

---

## Step 12 — Clean Up Resources

```bash
# Stop the local app
kill %1 2>/dev/null || true

# Remove the temp directory
cd ../..
rm -rf az204-monitoring-adv-lab

# Delete local files
rm -f appinsights-conn.txt appinsights-ikey.txt

# Delete the resource group and all resources
az group delete --name az204-day19-rg --yes --no-wait
```

---

## Expected Outcomes

| Task | Status |
|------|--------|
| Created Log Analytics workspace with 90-day retention | ✅ |
| Created Application Insights with workspace integration | ✅ |
| Deployed .NET app with telemetry instrumentation | ✅ |
| Generated diverse traffic (requests, errors, slow deps) | ✅ |
| Wrote KQL queries for requests, dependencies, exceptions | ✅ |
| Created time-series chart with make-series | ✅ |
| Analyzed custom events with KQL | ✅ |
| Created saved function in Log Analytics | ✅ |
| Created query pack with saved queries | ✅ |
| Built a workbook with KQL visualizations | ✅ |
| Created metric alert for response time | ✅ |
| Created log alert for exception count | ✅ |
| Configured action group with email + webhook | ✅ |
| Set up cost management budget with thresholds | ✅ |
| Reviewed cost recommendations | ✅ |
| Configured cost export to storage | ✅ |

---

## Bonus Challenges

1. **Create an anomaly detection query** using `series_decompose_anomalies`
2. **Add a forecast query** using `series_eforecast`
3. **Create a dynamic threshold alert** instead of static
4. **Build a workbook parameter** that filters queries by endpoint name
5. **Create a cost allocation view** using Azure resource tags

---

Next: [Day 20 — Final Project](../day-14-final-project/)
