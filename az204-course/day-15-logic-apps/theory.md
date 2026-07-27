# Day 15 — Azure Logic Apps

## What You'll Learn Today

- Azure Logic Apps overview and when to use them
- Triggers and actions
- Connectors (standard, enterprise, custom)
- Integration with Azure services
- Enterprise Integration Packs (EIP)
- Logic Apps vs Power Automate vs Durable Functions

---

## 1. What Is Azure Logic Apps

Azure Logic Apps is a cloud-based platform for creating and running automated workflows that integrate apps, data, services, and systems. It is a fully managed, serverless integration service — you define the workflow declaratively, and Azure handles the execution, scaling, and infrastructure.

### Core Characteristics

- **Serverless** — no servers to manage; Azure provisions and scales compute automatically
- **Declarative designer** — build workflows visually in the browser or with code (JSON)
- **Managed connectors** — 200+ pre-built connectors to SaaS and Azure services
- **Pay-per-execution** — Consumption plan charges per action run; Standard plan offers fixed pricing
- **Enterprise-ready** — B2B integration, VNet support, private endpoints, and hybrid connectivity

### When to Use Logic Apps

| Scenario | Logic Apps | Alternatives |
|----------|-----------|--------------|
| Orchestrate multiple Azure services | ✅ Ideal | Durable Functions (code-heavy) |
| Scheduled batch processing | ✅ Recurrence trigger | Azure Functions (timer trigger) |
| B2B/EDI integration (AS2, X12, EDIFACT) | ✅ Built-in EIP | Custom middleware |
| Simple file processing pipeline | ✅ Blob trigger + actions | Event Grid + Functions |
| Long-running workflows (days/weeks) | ✅ Consumption plan, no timeout | Durable Functions (48h limit) |
| Complex branching with low-code | ✅ Visual designer | Code-based orchestration |

### Consumption vs Standard Plans

| Feature | Consumption Plan | Standard Plan |
|---------|-----------------|---------------|
| Pricing | Per execution + connector call | Fixed hourly cost |
| Execution timeout | Unlimited (stateful) | Unlimited (stateful), 5 min (stateless) |
| Instance memory | Shared | 3.5 GB per instance |
| VNet integration | Via integration service environment | Built-in |
| Connectors | Managed connectors only | Built-in + managed |
| Best for | Sporadic, event-driven workloads | High-volume, always-on workloads |

---

## 2. Triggers

A trigger starts the Logic App execution. Every workflow must have exactly one trigger.

### Built-In Triggers

| Trigger | Description | Use Case |
|---------|-------------|----------|
| **Recurrence** | Runs on a schedule (minutes, hours, days) | Daily report generation |
| **HTTP** | Receives HTTP requests (GET, POST, etc.) | Webhook receiver, API endpoint |
| **HTTP Webhook** | Registers a webhook with an external service | GitHub, Slack, Salesforce events |
| **Request** | Similar to HTTP but for synchronous callbacks | Return data to caller |
| **Event Grid** | Reacts to Event Grid events | Blob created, resource changes |
| **Service Bus** | Receives messages from a queue or topic | Process order messages |
| **Blob Storage** | Triggers when a blob is created or modified | File processing pipeline |
| **SQL** | Triggers on SQL database changes (via polling) | ETL on new rows |
| **SignalR** | Event-driven via Azure SignalR Service | Real-time notifications |
| **When a file is added (FTP)** | Triggers on new FTP files | Legacy file integration |

### Recurrence Trigger Configuration

```json
{
  "Recurrence": {
    "frequency": "Hour",
    "interval": 1,
    "startTime": "2026-07-28T08:00:00Z",
    "timeZone": "UTC",
    "schedule": {
      "hours": [8, 12, 17],
      "minutes": [0]
    }
  }
}
```

This runs at 08:00, 12:00, and 17:00 UTC every day.

### HTTP Trigger

The HTTP trigger exposes an endpoint that external systems can call:

```
POST https://logic-apis-xxxx.azureweums.net/workflows/.../triggers/manualTrigger/run?api-version=2019-05-01
```

You can configure:
- **Method** — GET, POST, PUT, DELETE, etc.
- **Response** — return 200 OK with body, or 202 Accepted
- **Correlation** — match request/response across systems

---

## 3. Actions

Actions are the steps executed after the trigger fires. Logic Apps supports hundreds of actions across connectors.

### Control Flow Actions

| Action | Description |
|--------|-------------|
| **Condition** | If/else branching based on expressions |
| **Switch** | Multi-way branching (like switch/case) |
| **For each** | Iterate over an array (sequential) |
| **ForEach (parallel)** | Iterate with configurable parallelism |
| **Until** | Loop until a condition is true (with timeout) |
| **Scope** | Group actions into a logical unit (for error handling) |
| **Parallel branches** | Execute multiple paths concurrently |
| **Compose** | Create a reusable value or expression result |
| **Select** | Transform array elements using a mapping |
| **Filter array** | Filter array based on conditions |
| **Join** | Concatenate array elements with a delimiter |
| **Variables** | Initialize, set, and increment variables |

### Error Handling: Run After

By default, the next action runs only when the previous action succeeds. You can configure "Run After" to specify which outcomes trigger the next action:

| Outcome | Meaning |
|---------|---------|
| **is successful** | Previous action completed successfully |
| **has failed** | Previous action threw an error |
| **has timed out** | Previous action exceeded timeout |
| **is skipped** | Previous action was skipped (condition, etc.) |

### Try-Catch-Finally Pattern

```
┌─────────────────────────────────────────────┐
│  Try (Scope)                                │
│  ├── Get message from Service Bus           │
│  ├── Parse JSON                             │
│  └── Write to SQL Database                  │
│                                             │
│  Catch (Scope)  [Run After: has failed]     │
│  ├── Log error                              │
│  └── Send failure notification email        │
│                                             │
│  Finally (Scope)  [Run After: is skipped]   │
│  └── Complete/abandon message               │
└─────────────────────────────────────────────┘
```

### Retry Policies

Configure retry behavior for transient failures:

```json
{
  "RetryPolicy": {
    "type": "exponential",
    "count": 4,
    "interval": "PT7S",
    "minimumInterval": "PT5S",
    "maximumInterval": "PT1H"
  }
}
```

| Policy | Behavior |
|--------|----------|
| **exponential** | Backoff: 7s, 14s, 28s, 56s (default) |
| **fixed** | Same interval between retries |
| **none** | No retry — fail immediately |

---

## 4. Connectors

Connectors are pre-built APIs that Logic Apps use to communicate with services. They handle authentication, data mapping, and API versioning.

### Connector Types

| Type | Description | Examples |
|------|-------------|----------|
| **Built-in** | Runs inside the Logic App host; lowest latency | HTTP, Manual/Recurrence triggers, Workflow |
| **Standard** | Runs on the Logic App's compute; no extra cost | Azure Blob, Queue Storage, File System |
| **Managed** | Hosted by Azure; requires connection with credentials | SQL, Office 365, Salesforce, SAP |
| **Custom** | User-defined OpenAPI/Swagger specification | Internal APIs, third-party services |

### Connector Categories

**Azure Connectors:**
- Azure Blob Storage
- Azure Service Bus
- Azure SQL
- Azure Functions
- Azure Cosmos DB
- Azure Key Vault
- Azure Logic Apps (calling other Logic Apps)
- Azure Monitor / Log Analytics

**SaaS Connectors:**
- Office 365 (Outlook, SharePoint, Teams)
- Salesforce
- SAP
- Twitter/X
- Dropbox
- Slack
- GitHub
- SendGrid
- Twilio

**Enterprise Connectors (Premium):**
- SAP (OData and RFC)
- IBM DB2
- IBM MQ
- Oracle Database
- On-premises SQL Server
- File System (via on-premises data gateway)

### Standard vs Premium Connectors

| Feature | Standard Connector | Premium Connector |
|---------|-------------------|-------------------|
| Cost | Included in plan | Extra per-connector fee |
| Examples | HTTP, Recurrence, Blob, Queue | SQL, SAP, Oracle, File System |
| Performance | Standard throughput | Higher throughput, dedicated |
| Use case | Basic integrations | Enterprise, on-premises, high-frequency |

### On-Premises Data Gateway

To connect to on-premises resources (SQL Server, File System, SharePoint on-prem):

```
Logic App ──► Managed Connector ──► On-Premises Gateway ──► SQL Server
                  (Azure)              (your network)
```

1. Install the gateway on a machine in your network
2. Register it with Azure AD
3. Configure the managed connector to use the gateway
4. Provide on-premises credentials

### Custom Connectors

Define a custom connector using an OpenAPI (Swagger) specification:

```json
{
  "swagger": "2.0",
  "info": {
    "title": "My Custom API",
    "version": "1.0"
  },
  "host": "myapi.example.com",
  "basePath": "/api/v1",
  "schemes": ["https"],
  "paths": {
    "/orders/{id}": {
      "get": {
        "operationId": "GetOrder",
        "parameters": [
          {
            "name": "id",
            "in": "path",
            "type": "string",
            "required": true
          }
        ],
        "responses": {
          "200": { "description": "Order retrieved" }
        }
      }
    }
  }
}
```

---

## 5. Enterprise Integration (B2B)

Logic Apps provides built-in support for Business-to-Business (B2B) integration using the Enterprise Integration Pack (EIP).

### B2B Protocols

| Protocol | Description |
|----------|-------------|
| **AS2** | Applicability Statement 2 — HTTP-based, used for EDI over the internet |
| **X12** | ANSI X12 — North American EDI standard (orders, invoices, etc.) |
| **EDIFACT** | EDI for Administration, Commerce, and Transport — international standard |
| **Flat File** | Custom delimited/fixed-width formats |
| **XML** | Standard XML schemas |

### Integration Account

An Integration Account is required for B2B features. It stores:

- **Schemas** — XML schemas (XSD) that define message structure
- **Maps** — XSLT transformations between schemas
- **Agreements** — B2B partner configurations (AS2/X12/EDIFACT settings)
- **Certificates** — AS2 signing/encryption certificates
- **Partners** — Trading partner identifiers and connection details

### B2B Workflow Example

```
Partner (AS2) ──► Logic App ──► Decode AS2 Message
                    │
                    ├── Validate against X12 schema
                    ├── Transform with XSLT map
                    ├── Route based on document type
                    └── Send to ERP system
```

### AS2 Properties

```json
{
  "as2From": "PARTNER_A",
  "as2To": "MY_ORGANIZATION",
  "messageId": "unique-message-id",
  "subject": "Purchase Order",
  "dispositionNotificationTo": "https://myendpoint/api/notifications",
  "receiptDeliveryOption": "async"
}
```

---

## 6. Logic Apps vs Power Automate vs Durable Functions

| Feature | Logic Apps | Power Automate | Durable Functions |
|---------|-----------|----------------|-------------------|
| **Target audience** | Developers, architects | Business users, power users | Developers |
| **Designer** | Visual + code (JSON) | Visual only | Code (C#, JavaScript) |
| **Pricing** | Consumption/Standard | Per-user/Per-flow | Consumption-based |
| **Connectors** | 200+ (managed + built-in) | 400+ (includes Power Platform) | Code + bindings |
| **B2B/EDI** | ✅ Native EIP support | ❌ Not available | ❌ Custom implementation |
| **Long-running** | ✅ Unlimited (stateful) | ✅ Long-running flows | ⚠️ 48h max (durable task) |
| **State management** | ✅ Built-in (run history) | ✅ Built-in | ✅ Explicit orchestration state |
| **Custom code** | ⚠️ Azure Functions action | ❌ Limited | ✅ Full code control |
| **VNet support** | ✅ Standard plan | ⚠️ Via on-prem gateway | ✅ VNet integration |
| **Monitoring** | ✅ Run history, Log Analytics | ✅ Flow analytics | ✅ Application Insights |
| **Complex logic** | ✅ Expressions + conditions | ⚠️ Limited branching | ✅ Full programming model |
| **On-premises** | ✅ Via data gateway | ✅ Via data gateway | ✅ Direct (if in VNet) |

### Decision Guide

```
Need B2B/EDI? ──────────► Logic Apps
    │
    No
    │
Need complex code logic? ──► Durable Functions
    │
    No
    │
Business users building? ──► Power Automate
    │
    No
    │
Need managed connectors? ──► Logic Apps
```

---

## 7. Error Handling

Robust error handling is critical for production Logic Apps.

### Run After Configuration

By default, actions only run on success. The "Run After" setting lets you control which outcomes trigger downstream actions.

```
Action A ──► [Success] ──► Action B
         ──► [Failure] ──► Action C (notification)
         ──► [Timeout] ──► Action D (alert)
         ──► [Skipped]  ──► Action E (cleanup)
```

### Scopes for Error Handling

Scopes group actions and provide a boundary for error handling:

```
┌─── Try Scope ──────────────────────┐
│  Step 1: Receive message           │
│  Step 2: Process message           │
│  Step 3: Send to database          │
└────────────────────────────────────┘
         │ (on failure)
         ▼
┌─── Catch Scope ────────────────────┐
│  Step 4: Log error details         │
│  Step 5: Send alert email          │
│  Step 6: Add to dead-letter queue  │
└────────────────────────────────────┘
         │ (always)
         ▼
┌─── Finally Scope ──────────────────┐
│  Step 7: Complete/abandon message  │
└────────────────────────────────────┘
```

### Compensation Pattern

When a multi-step operation fails partway through, you may need to undo completed steps:

```
┌─── Try Scope ──────────────────────┐
│  Step 1: Debit account ($100)      │
│  Step 2: Create order              │
│  Step 3: Send confirmation ──FAIL  │
└────────────────────────────────────┘
         │
         ▼
┌─── Catch Scope ────────────────────┐
│  Step 4: Log error                 │
│  Step 5: Credit account ($100)     │  ← Compensation
│  Step 6: Cancel order              │  ← Compensation
│  Step 7: Notify operations team    │
└────────────────────────────────────┘
```

### Expressions for Error Handling

```javascript
// Check if an action succeeded
if(equals(actions('Get_Message')?['status'], 'Succeeded'), 'yes', 'no')

// Get error message from failed action
result('Process_Message')?['error']?['message']

// Check run history status
if(equals(triggerOutputs()?['status'], 'Failed'), 'handle failure', 'continue')
```

---

## 8. Security

### Managed Identities

Logic Apps support system-assigned and user-assigned managed identities:

```json
{
  "identity": {
    "type": "SystemAssigned"
  }
}
```

Use managed identities to authenticate to:
- Azure Key Vault (secrets, certificates)
- Azure Service Bus
- Azure SQL (with Azure AD authentication)
- Azure Blob Storage
- Any Azure service that supports Azure AD

### Access Control

| Mechanism | Description |
|-----------|-------------|
| **RBAC** | Assign roles to the Logic App's managed identity |
| **API keys** | HTTP trigger endpoints can require a subscription key |
| **Azure AD** | OAuth 2.0 authentication for HTTP triggers |
| **IP restrictions** | Allow only specific IP ranges to call HTTP endpoints |
| **Private endpoints** | Expose Logic App only within a VNet |

### VNet Integration

```
Internet ──► [Public IP] ──► Logic App (Standard plan)
                                    │
                                    ▼
                              VNet ──► Private Endpoints
                                   ──► On-Premises Gateway
```

Standard plan Logic Apps can:
- Deploy into a VNet
- Use private endpoints for outbound traffic
- Access resources in private networks
- Restrict inbound traffic via NSGs

---

## 9. Monitoring and Diagnostics

### Run History

Every Logic App execution is recorded in the run history:
- **Consumption plan** — last 30 days
- **Standard plan** — last 90 days (configurable)

Each run shows:
- Trigger input/output
- Action inputs, outputs, and duration
- Status (Succeeded, Failed, Cancelled)
- Error details for failed actions

### Diagnostic Logging

Enable diagnostic settings to send logs to:

| Destination | Description |
|-------------|-------------|
| **Log Analytics** | Query with KQL, build dashboards |
| **Storage Account** | Long-term archival, cost-effective |
| **Event Hub** | Stream to external monitoring tools |
| **Application Insights** | End-to-end distributed tracing |

### KQL Queries for Monitoring

```kusto
// Failed runs in the last 24 hours
AzureDiagnostics
| where ResourceProvider == "MICROSOFT.LOGICAPPS"
| where Status_s == "Failed"
| where TimeGenerated > ago(24h)
| project TimeGenerated, RunId_g, WorkflowName_s, Error_s
| order by TimeGenerated desc

// Average action duration
AzureDiagnostics
| where ResourceProvider == "MICROSOFT.LOGICAPPS"
| where ActionType_s == "ActionCompleted"
| summarize AvgDuration = avg(DurationMs_d) by ActionName_s
| order by AvgDuration desc

// Trigger frequency by hour
AzureDiagnostics
| where ResourceProvider == "MICROSOFT.LOGICAPPS"
| where ActionType_s == "TriggerFired"
| summarize Count = count() by bin(TimeGenerated, 1h)
| render timechart
```

### Alerting

Set up alerts in Azure Monitor:
- Failed run rate exceeds threshold
- Run duration exceeds expected time
- Connector connection failures
- Throttling warnings

---

## 10. Architecture

### Typical Enterprise Integration Architecture

```
┌─────────────────────────────────────────────────────────────────────────┐
│                         Azure Subscription                              │
│                                                                         │
│  ┌──────────────┐     ┌─────────────────────────────────────────────┐  │
│  │  Event Grid   │────►│              Logic App                      │  │
│  │  (blob created)│     │                                             │  │
│  └──────────────┘     │  ┌───┐  ┌──────┐  ┌───┐  ┌───────────┐   │  │
│                       │  │ T │─►│Parse │─►│T/F│─►│ Process   │   │  │
│  ┌──────────────┐     │  │   │  │JSON  │  │   │  │ + Transform│   │  │
│  │  Service Bus  │────►│  └───┘  └──────┘  └───┘  └─────┬─────┘   │  │
│  │  (messages)  │     │                                  │          │  │
│  └──────────────┘     │                    ┌─────────────┤          │  │
│                       │                    │             │          │  │
│  ┌──────────────┐     │               ┌────▼───┐  ┌─────▼─────┐  │  │
│  │  Recurrence   │────►│               │  SQL   │  │   Blob    │  │  │
│  │  (schedule)  │     │               │Database│  │  Storage  │  │  │
│  └──────────────┘     │               └────────┘  └───────────┘  │  │
│                       │                                             │  │
│  ┌──────────────┐     │  ┌──────────┐  ┌──────────┐              │  │
│  │  External API │────►│  │ Office   │  │  Azure   │              │  │
│  │  (webhook)   │     │  │ 365      │  │Function  │              │  │
│  └──────────────┘     │  │(email)   │  │(custom   │              │  │
│                       │  └──────────┘  │ logic)   │              │  │
│  ┌──────────────┐     │                └──────────┘              │  │
│  │  App Service  │────►│                                          │  │
│  │  (REST API)  │     └─────────────────────────────────────────────┘  │
│  └──────────────┘                                                       │
│                                                                         │
│  ┌──────────────┐     ┌──────────────────┐  ┌──────────────────────┐  │
│  │  Key Vault    │────►│  Log Analytics    │  │  Integration Account │  │
│  │  (secrets)   │     │  (monitoring)     │  │  (B2B schemas/maps)  │  │
│  └──────────────┘     └──────────────────┘  └──────────────────────┘  │
│                                                                         │
└─────────────────────────────────────────────────────────────────────────┘

LEGEND:
  T/F  = Condition (true/false branch)
  ──►  = Data flow direction
```

### Data Flow Summary

1. **Event Grid** triggers Logic App when a blob is uploaded
2. **Service Bus** delivers messages from external systems
3. **Recurrence** runs scheduled batch jobs
4. **HTTP** receives webhook calls from external APIs
5. Logic App **parses, transforms, and routes** data
6. Data written to **SQL Database** and **Blob Storage**
7. **Office 365** sends notifications on success/failure
8. **Azure Functions** handle custom business logic
9. **Key Vault** provides secrets and certificates
10. **Log Analytics** captures all run history for monitoring

---

## 11. Key Concepts for AZ-204

### Must-Know Topics

1. **Triggers** — Know the difference between Recurrence, HTTP, Service Bus, Event Grid, and Blob triggers
2. **Actions** — Understand conditions, loops (For each, Until), parallel branches, and scopes
3. **Connectors** — Know when to use built-in vs standard vs managed vs custom connectors
4. **Expressions** — Be comfortable with Logic Apps expression language (functions, conditions, type conversions)
5. **Error handling** — Configure Run After, use scopes for try-catch-finally, implement compensation
6. **B2B integration** — Understand Integration Accounts, AS2, X12, EDIFACT (awareness level)
7. **Security** — Managed identities, VNet integration, private endpoints, RBAC
8. **Monitoring** — Run history, diagnostic settings, KQL queries for troubleshooting
9. **Consumption vs Standard** — Know the trade-offs (pricing, features, deployment)

### Common Exam Patterns

- **Pattern: Fan-out/fan-in** — Parallel branches in Logic App, aggregate results with a termination condition
- **Pattern: Message processing** — Service Bus trigger → parse → process → complete message
- **Pattern: Error notification** — Try scope with failed run-after → catch scope sends email
- **Pattern: Scheduled integration** — Recurrence trigger → call external API → store results
- **Pattern: Hybrid connectivity** — Standard Logic App in VNet → on-premises gateway → SQL Server

### Expression Cheat Sheet

| Expression | Description |
|------------|-------------|
| `triggerOutputs()` | Access trigger output |
| `body('Action_Name')` | Access action output body |
| `string()` | Convert to string |
| `int()` | Convert to integer |
| `float()` | Convert to float |
| `bool()` | Convert to boolean |
| `if(condition, true, false)` | Conditional expression |
| `equals(a, b)` | Equality check |
| `and(expr1, expr2)` | Logical AND |
| `or(expr1, expr2)` | Logical OR |
| `not(expr)` | Logical NOT |
| `length(string_or_array)` | Length of string or array |
| `substring(str, start, length)` | Extract substring |
| `replace(str, old, new)` | Replace characters |
| `toUpper(str)` / `toLower(str)` | Case conversion |
| `trim(str)` | Remove whitespace |
| `concat(str1, str2)` | Concatenate strings |
| `split(str, delimiter)` | Split string to array |
| `first(array)` / `last(array)` | First/last element |
| `createArray(val1, val2, ...)` | Create array |
| `add(a, b)` / `sub(a, b)` / `mul(a, b)` / `div(a, b)` | Math operations |
| `utcNow()` | Current UTC timestamp |
| `convertTimeZone(time, src, dst)` | Convert time zones |
| `json(string)` | Parse JSON string |
| `xml(content)` | Parse XML content |
| `xpath(xml, expression)` | Query XML with XPath |

---

## Key Takeaways

1. Logic Apps is a serverless workflow automation platform — declarative, visual designer, 200+ connectors
2. Triggers start workflows — Recurrence, HTTP, Service Bus, Event Grid, and Blob are the most common
3. Actions are workflow steps — conditions, loops, parallel branches, and scopes provide control flow
4. Connectors come in types — built-in, standard, managed, and custom; premium connectors cost extra
5. B2B integration uses Integration Accounts with schemas, maps, and agreements (AS2, X12, EDIFACT)
6. Logic Apps vs Power Automate vs Durable Functions — each targets different audiences and use cases
7. Error handling uses scopes for try-catch-finally, Run After for outcome-based routing, and retry policies
8. Security relies on managed identities, RBAC, VNet integration, and private endpoints
9. Monitoring uses run history, Log Analytics, and KQL queries for diagnostics

---

Next: [Lab — Logic Apps](./lab-logic-apps.md)
