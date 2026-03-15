# Day 11 — Monitoring & Logging

## What You'll Learn Today

- Azure Monitor overview
- Application Insights: instrumentation, telemetry, metrics
- Log Analytics and KQL (Kusto Query Language)
- Distributed tracing
- Alerts and action groups

---

## 1. Azure Monitor

Azure Monitor is the unified monitoring platform for all Azure resources. It collects metrics and logs from applications, infrastructure, and the platform.

### Data Types

| Type | Description | Example |
|------|-------------|---------|
| **Metrics** | Numeric time-series data | CPU %, request count, response time |
| **Logs** | Structured event data | Request traces, exceptions, custom events |

### Architecture

```
Data Sources                Azure Monitor              Consumers
───────────                ─────────────              ─────────
Applications ──►  ┌──────────────────────┐  ──► Dashboards
Infrastructure ──►│  Metrics │ Logs      │  ──► Alerts
Platform ──────►  │  (time   │ (Log      │  ──► Autoscale
Custom ────────►  │  series) │ Analytics)│  ──► Power BI
                  └──────────────────────┘  ──► REST API
```

---

## 2. Application Insights

Application Insights is the APM (Application Performance Management) feature of Azure Monitor. It monitors live applications.

### What It Collects

- **Requests** — HTTP requests to your app (URL, duration, status code)
- **Dependencies** — calls your app makes (SQL, HTTP, Redis)
- **Exceptions** — unhandled and caught exceptions
- **Page views** — browser-side telemetry
- **Custom events** — business events you define
- **Custom metrics** — numeric values you track
- **Traces** — diagnostic log messages

### Instrumentation

Two approaches:
- **Auto-instrumentation** — enable without code changes (agent-based)
- **SDK instrumentation** — add the SDK to your code for full control

### Connection String

Application Insights uses a connection string (not just an instrumentation key):
```
InstrumentationKey=xxx;IngestionEndpoint=https://eastus-1.in.applicationinsights.azure.com/
```

### Application Map

Visual representation of your application's components and their dependencies:
```
┌──────────┐     ┌──────────┐     ┌──────────┐
│ Frontend │────►│   API    │────►│ Database │
│ (React)  │     │ (App Svc)│     │(Cosmos)  │
└──────────┘     │          │────►│          │
                 │          │     └──────────┘
                 │          │────►┌──────────┐
                 └──────────┘     │  Redis   │
                                  └──────────┘
```

---

## 3. Log Analytics & KQL

Log Analytics is the query engine for Azure Monitor logs. You write queries in KQL (Kusto Query Language).

### Common KQL Queries

**Recent requests:**
```kql
requests
| where timestamp > ago(1h)
| summarize count() by resultCode
| order by count_ desc
```

**Slow requests:**
```kql
requests
| where duration > 1000
| project timestamp, name, url, duration, resultCode
| order by duration desc
| take 20
```

**Failed dependencies:**
```kql
dependencies
| where success == false
| summarize count() by target, type
| order by count_ desc
```

**Exceptions:**
```kql
exceptions
| where timestamp > ago(24h)
| summarize count() by type, outerMessage
| order by count_ desc
```

**Custom events:**
```kql
customEvents
| where name == "OrderPlaced"
| extend orderId = tostring(customDimensions.orderId)
| project timestamp, orderId, customDimensions
```

---

## 4. Distributed Tracing

Application Insights automatically correlates telemetry across services using operation IDs.

- **Operation ID** — unique ID for the entire request chain
- **Parent ID** — links child operations to their parent
- End-to-end transaction view shows the full call chain

---

## 5. Alerts

Alerts notify you when conditions are met.

| Alert Type | Trigger | Example |
|-----------|---------|---------|
| **Metric alert** | Metric crosses threshold | CPU > 80% for 5 min |
| **Log alert** | Log query returns results | Error count > 10 in 15 min |
| **Activity log alert** | Azure resource event | VM deleted, deployment failed |

**Action Groups** define what happens when an alert fires:
- Email, SMS, push notification
- Azure Function, Logic App, Webhook
- ITSM integration

---

## Key Takeaways

1. Application Insights monitors live apps — requests, dependencies, exceptions
2. Use the SDK for custom events and metrics
3. KQL is essential for querying logs — learn the basic patterns
4. Distributed tracing correlates requests across services automatically
5. Alerts + action groups = automated incident response
6. Application Map visualizes your architecture and dependencies

---

Next: [Lab — Application Insights](./lab-application-insights.md)
