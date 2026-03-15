# Lab: Application Insights

## Title
Instrument a Web App with Application Insights

## Objective
- Create an Application Insights resource
- Instrument a .NET web app with the SDK
- Track custom events and metrics
- Query telemetry with KQL
- Create an alert rule

## Architecture

```
┌──────────────────────────────────────────────────────────┐
│              Resource Group: az204-day11-rg               │
│                                                          │
│  ┌──────────────────┐    ┌──────────────────────────┐   │
│  │  App Service      │───►│  Application Insights   │   │
│  │  az204-mon-app    │    │  az204-appinsights      │   │
│  │                   │    │                          │   │
│  │  Sends:           │    │  Collects:               │   │
│  │  - Requests       │    │  - Request telemetry     │   │
│  │  - Dependencies   │    │  - Dependency tracking   │   │
│  │  - Custom events  │    │  - Custom events/metrics │   │
│  │  - Exceptions     │    │  - Exceptions            │   │
│  └──────────────────┘    │                          │   │
│                          │  ┌──────────────────┐    │   │
│                          │  │  Log Analytics   │    │   │
│                          │  │  Workspace       │    │   │
│                          │  │  (KQL queries)   │    │   │
│                          │  └──────────────────┘    │   │
│                          └──────────────────────────┘   │
│                                                          │
│  ┌──────────────────────────────────────────────────┐   │
│  │  Alert Rule: Error rate > 5 in 5 minutes         │   │
│  │  Action: Email notification                       │   │
│  └──────────────────────────────────────────────────┘   │
└──────────────────────────────────────────────────────────┘
```

## Prerequisites
- Azure CLI installed and logged in
- .NET 8 SDK installed

---

## Step 1 — Create Resources

```bash
az group create --name az204-day11-rg --location eastus

# Create Log Analytics workspace
az monitor log-analytics workspace create \
  --resource-group az204-day11-rg \
  --workspace-name az204-law \
  --location eastus

# Create Application Insights
WORKSPACE_ID=$(az monitor log-analytics workspace show \
  --resource-group az204-day11-rg \
  --workspace-name az204-law \
  --query id --output tsv)

az monitor app-insights component create \
  --app az204-appinsights \
  --resource-group az204-day11-rg \
  --location eastus \
  --workspace $WORKSPACE_ID
```

Get the connection string:
```bash
az monitor app-insights component show \
  --app az204-appinsights \
  --resource-group az204-day11-rg \
  --query connectionString \
  --output tsv
```

---

## Step 2 — Create the .NET Web App

```bash
mkdir az204-monitoring-lab
cd az204-monitoring-lab
dotnet new web -n MonitoringApp
cd MonitoringApp
dotnet add package Microsoft.ApplicationInsights.AspNetCore
```

Replace `Program.cs`:

```csharp
using Microsoft.ApplicationInsights;
using Microsoft.ApplicationInsights.DataContracts;

var builder = WebApplication.CreateBuilder(args);

// Add Application Insights
builder.Services.AddApplicationInsightsTelemetry(options =>
{
    options.ConnectionString = "<your-connection-string>";
});

var app = builder.Build();

// Home endpoint
app.MapGet("/", () => "Application Insights Lab - try /order, /slow, /error, /metrics");

// Simulated order endpoint with custom event
app.MapGet("/order", (TelemetryClient telemetry) =>
{
    var orderId = Guid.NewGuid().ToString("N")[..8];
    var total = Random.Shared.Next(10, 500);

    // Track custom event
    telemetry.TrackEvent("OrderPlaced", new Dictionary<string, string>
    {
        { "orderId", orderId },
        { "product", "Widget" }
    }, new Dictionary<string, double>
    {
        { "orderTotal", total }
    });

    // Track custom metric
    telemetry.GetMetric("OrderValue").TrackValue(total);

    return Results.Ok(new { orderId, total, status = "placed" });
});

// Slow endpoint (simulates dependency latency)
app.MapGet("/slow", async (TelemetryClient telemetry) =>
{
    using var operation = telemetry.StartOperation<DependencyTelemetry>("SlowDatabaseCall");
    operation.Telemetry.Type = "SQL";
    operation.Telemetry.Target = "mydb.database.windows.net";

    await Task.Delay(Random.Shared.Next(1000, 3000)); // Simulate slow query

    operation.Telemetry.Success = true;
    return Results.Ok(new { message = "Slow operation completed", duration = "1-3 seconds" });
});

// Error endpoint (simulates failures)
app.MapGet("/error", (TelemetryClient telemetry) =>
{
    try
    {
        throw new InvalidOperationException("Simulated error for Application Insights lab");
    }
    catch (Exception ex)
    {
        telemetry.TrackException(ex, new Dictionary<string, string>
        {
            { "endpoint", "/error" },
            { "severity", "high" }
        });
        return Results.Problem("Something went wrong!", statusCode: 500);
    }
});

// Metrics endpoint
app.MapGet("/metrics", (TelemetryClient telemetry) =>
{
    // Track multiple custom metrics
    telemetry.GetMetric("ActiveUsers").TrackValue(Random.Shared.Next(50, 200));
    telemetry.GetMetric("QueueDepth").TrackValue(Random.Shared.Next(0, 50));

    telemetry.TrackTrace("Metrics endpoint called", SeverityLevel.Information);

    return Results.Ok(new { message = "Custom metrics recorded" });
});

app.Run();
```

---

## Step 3 — Test Locally

```bash
dotnet run
```

Generate telemetry by hitting the endpoints:
```bash
# Generate various telemetry
curl http://localhost:5000/order
curl http://localhost:5000/order
curl http://localhost:5000/order
curl http://localhost:5000/slow
curl http://localhost:5000/error
curl http://localhost:5000/metrics
```

Wait 2-3 minutes for telemetry to appear in Application Insights.

---

## Step 4 — Query Telemetry with KQL

Open the Azure Portal → Application Insights → Logs, or use the CLI:

```bash
# Query recent requests
az monitor app-insights query \
  --app az204-appinsights \
  --resource-group az204-day11-rg \
  --analytics-query "requests | where timestamp > ago(1h) | project timestamp, name, duration, resultCode | order by timestamp desc | take 10"

# Query custom events
az monitor app-insights query \
  --app az204-appinsights \
  --resource-group az204-day11-rg \
  --analytics-query "customEvents | where name == 'OrderPlaced' | extend orderId = tostring(customDimensions.orderId), total = todouble(customMeasurements.orderTotal) | project timestamp, orderId, total"

# Query exceptions
az monitor app-insights query \
  --app az204-appinsights \
  --resource-group az204-day11-rg \
  --analytics-query "exceptions | where timestamp > ago(1h) | project timestamp, type, outerMessage, customDimensions"

# Query slow dependencies
az monitor app-insights query \
  --app az204-appinsights \
  --resource-group az204-day11-rg \
  --analytics-query "dependencies | where duration > 1000 | project timestamp, name, target, duration, success"
```

---

## Step 5 — Create an Alert Rule

Create an action group (email notification):
```bash
az monitor action-group create \
  --name az204-alerts \
  --resource-group az204-day11-rg \
  --short-name az204alert \
  --action email admin-email admin@example.com
```

Create a metric alert for high error rate:
```bash
APP_INSIGHTS_ID=$(az monitor app-insights component show \
  --app az204-appinsights \
  --resource-group az204-day11-rg \
  --query id --output tsv)

ACTION_GROUP_ID=$(az monitor action-group show \
  --name az204-alerts \
  --resource-group az204-day11-rg \
  --query id --output tsv)

az monitor metrics alert create \
  --name "High Error Rate" \
  --resource-group az204-day11-rg \
  --scopes $APP_INSIGHTS_ID \
  --condition "count requests/failed > 5" \
  --window-size 5m \
  --evaluation-frequency 1m \
  --action $ACTION_GROUP_ID \
  --description "Alert when more than 5 failed requests in 5 minutes"
```

Test the alert by generating errors:
```bash
for i in $(seq 1 10); do curl -s http://localhost:5000/error > /dev/null; done
```

---

## Step 6 — Deploy to App Service (Optional)

```bash
az appservice plan create \
  --name az204-mon-plan \
  --resource-group az204-day11-rg \
  --sku B1 --is-linux

az webapp create \
  --name az204-mon-app-<unique> \
  --resource-group az204-day11-rg \
  --plan az204-mon-plan \
  --runtime "DOTNETCORE:8.0"

dotnet publish -c Release -o ./publish
cd publish && zip -r ../deploy.zip . && cd ..

az webapp deployment source config-zip \
  --name az204-mon-app-<unique> \
  --resource-group az204-day11-rg \
  --src deploy.zip
```

---

## Cleanup

```bash
cd ../..
rm -rf az204-monitoring-lab

az group delete --name az204-day11-rg --yes --no-wait
```

---

## Expected Outcomes

| Task | Status |
|------|--------|
| Created Application Insights with Log Analytics | ✅ |
| Instrumented .NET app with SDK | ✅ |
| Tracked custom events and metrics | ✅ |
| Simulated slow dependencies | ✅ |
| Tracked exceptions | ✅ |
| Queried telemetry with KQL | ✅ |
| Created alert rule with action group | ✅ |

---

Next: [Day 12 — Security: Key Vault](../day-12-security/theory.md)
