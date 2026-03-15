# AZ-204 Architecture Review

## Quick Reference: All Services at a Glance

### Compute

| Service | Type | Scale | Use Case |
|---------|------|-------|----------|
| App Service | PaaS Web Hosting | Manual/Auto (up to 30 instances) | Web apps, APIs |
| Azure Functions | Serverless | Auto (Consumption: 0 to 200) | Event-driven processing |
| Container Instances | Container | None (single run) | Batch jobs, testing |
| Container Apps | Serverless Containers | Auto (0 to N, KEDA-based) | Microservices |

### Storage

| Service | Type | Max Size | Use Case |
|---------|------|----------|----------|
| Blob Storage | Object store | 190.7 TiB per blob | Files, media, backups |
| Queue Storage | Message queue | 500 TB | Simple queuing |
| Table Storage | Key-value | 500 TB | Semi-structured data |
| Cosmos DB | NoSQL database | Unlimited | Global, low-latency data |

### Messaging & Events

| Service | Pattern | Throughput | Use Case |
|---------|---------|-----------|----------|
| Service Bus | Message broker | Moderate | Enterprise messaging, ordered processing |
| Event Grid | Event routing | Moderate | React to Azure events |
| Event Hubs | Event streaming | Millions/sec | Telemetry, IoT, analytics |
| Queue Storage | Simple queue | High | Basic decoupling |

### Security

| Service | Purpose | Key Feature |
|---------|---------|-------------|
| Key Vault | Secret management | RBAC, HSM-backed, versioning |
| Managed Identity | Passwordless auth | System/User-assigned, auto-managed |
| Entra ID | Identity platform | OAuth 2.0, MSAL, app registrations |
| App Configuration | Config management | Feature flags, Key Vault references |

### Monitoring

| Service | Purpose | Key Feature |
|---------|---------|-------------|
| Application Insights | APM | Request tracking, dependency mapping |
| Log Analytics | Log querying | KQL queries |
| Azure Monitor | Unified monitoring | Metrics, alerts, dashboards |

### Networking & API

| Service | Purpose | Key Feature |
|---------|---------|-------------|
| API Management | API gateway | Policies, rate limiting, developer portal |
| CDN | Content delivery | Edge caching, global distribution |

---

## Decision Trees

### Which Compute Service?

```
Need to run code?
├── Web app or API?
│   ├── Full web framework → App Service
│   └── Lightweight API → Azure Functions (HTTP trigger) or Container Apps
├── Event-driven / scheduled?
│   └── Azure Functions
├── Container-based?
│   ├── Simple, short-lived → Container Instances
│   └── Microservices, auto-scale → Container Apps
└── Need full control?
    └── Virtual Machines (not AZ-204 scope)
```

### Which Storage Service?

```
Need to store data?
├── Unstructured files (images, docs, logs)?
│   └── Blob Storage
├── Structured data with flexible schema?
│   ├── Global distribution needed → Cosmos DB
│   └── Simple key-value → Table Storage
├── Relational data?
│   └── Azure SQL (not primary AZ-204 focus)
└── Temporary messages?
    └── Queue Storage
```

### Which Messaging Service?

```
Need to decouple services?
├── React to Azure resource events?
│   └── Event Grid
├── High-throughput streaming (millions/sec)?
│   └── Event Hubs
├── Enterprise messaging with ordering/transactions?
│   └── Service Bus
└── Simple, cheap queuing?
    └── Queue Storage
```

### Which Authentication Approach?

```
Need to authenticate?
├── Service-to-Azure-service?
│   └── Managed Identity + DefaultAzureCredential
├── User sign-in (web app)?
│   └── MSAL + Authorization Code flow
├── Daemon/service (no user)?
│   └── MSAL + Client Credentials flow
└── Device without browser?
    └── MSAL + Device Code flow
```

---

## Key CLI Commands Cheat Sheet

### Resource Management
```bash
az group create --name <rg> --location <region>
az group delete --name <rg> --yes --no-wait
az deployment group create --resource-group <rg> --template-file <bicep>
```

### App Service
```bash
az webapp create --name <app> --resource-group <rg> --plan <plan> --runtime "DOTNETCORE:8.0"
az webapp deployment slot create --name <app> --resource-group <rg> --slot staging
az webapp deployment slot swap --name <app> --resource-group <rg> --slot staging
az webapp config appsettings set --name <app> --resource-group <rg> --settings KEY=VALUE
```

### Azure Functions
```bash
az functionapp create --name <func> --resource-group <rg> --storage-account <sa> --consumption-plan-location <region> --runtime dotnet-isolated --functions-version 4
func init --worker-runtime dotnet-isolated
func new --name <name> --template "HTTP trigger"
func azure functionapp publish <func>
```

### Storage
```bash
az storage account create --name <sa> --resource-group <rg> --sku Standard_LRS --kind StorageV2
az storage container create --name <container> --account-name <sa>
az storage blob upload --container-name <c> --name <blob> --file <file> --account-name <sa>
az storage blob set-tier --container-name <c> --name <blob> --account-name <sa> --tier Cool
```

### Cosmos DB
```bash
az cosmosdb create --name <account> --resource-group <rg> --kind GlobalDocumentDB
az cosmosdb sql database create --account-name <account> --resource-group <rg> --name <db>
az cosmosdb sql container create --account-name <account> --resource-group <rg> --database-name <db> --name <container> --partition-key-path "/pk"
```

### Key Vault
```bash
az keyvault create --name <kv> --resource-group <rg> --enable-rbac-authorization true
az keyvault secret set --vault-name <kv> --name <secret> --value <value>
az keyvault secret show --vault-name <kv> --name <secret> --query value --output tsv
```

### Service Bus
```bash
az servicebus namespace create --name <ns> --resource-group <rg> --sku Standard
az servicebus queue create --name <queue> --namespace-name <ns> --resource-group <rg>
az servicebus topic create --name <topic> --namespace-name <ns> --resource-group <rg>
```

### Container
```bash
az acr create --name <acr> --resource-group <rg> --sku Basic
az acr build --registry <acr> --image <name>:<tag> .
az container create --name <aci> --resource-group <rg> --image <image> --ports 80
az containerapp create --name <app> --resource-group <rg> --environment <env> --image <image> --target-port 8080 --ingress external
```

### Identity
```bash
az webapp identity assign --name <app> --resource-group <rg>
az role assignment create --assignee <principalId> --role "<role>" --scope <resourceId>
az ad app create --display-name "<name>"
```

### Monitoring
```bash
az monitor app-insights component create --app <name> --resource-group <rg> --location <region>
az monitor metrics alert create --name <alert> --resource-group <rg> --scopes <resourceId> --condition "<condition>"
```

---

## NuGet Packages Reference

| Package | Purpose |
|---------|---------|
| `Azure.Identity` | DefaultAzureCredential, managed identity |
| `Azure.Storage.Blobs` | Blob Storage SDK |
| `Azure.Security.KeyVault.Secrets` | Key Vault secrets |
| `Azure.Security.KeyVault.Keys` | Key Vault keys |
| `Azure.Messaging.ServiceBus` | Service Bus SDK |
| `Azure.Messaging.EventGrid` | Event Grid SDK |
| `Microsoft.Azure.Cosmos` | Cosmos DB SDK |
| `StackExchange.Redis` | Redis client |
| `Microsoft.Identity.Client` | MSAL authentication |
| `Microsoft.ApplicationInsights.AspNetCore` | App Insights for ASP.NET Core |
| `Microsoft.FeatureManagement.AspNetCore` | Feature flags |
| `Microsoft.Azure.AppConfiguration.AspNetCore` | App Configuration |

---

## Final Exam Checklist

- [ ] Can explain all 5 Cosmos DB consistency levels
- [ ] Know the difference between Event Grid, Event Hubs, and Service Bus
- [ ] Can write APIM policies (rate-limit, JWT validation, set-header)
- [ ] Understand managed identity types and DefaultAzureCredential chain
- [ ] Can write basic KQL queries for requests, exceptions, dependencies
- [ ] Know Blob Storage tiers and lifecycle management
- [ ] Understand App Service deployment slots and swap behavior
- [ ] Can create Azure Functions with different triggers and bindings
- [ ] Know SAS token types (account, service, user delegation)
- [ ] Can write Bicep templates with parameters, resources, and outputs
- [ ] Understand Container Apps revisions and traffic splitting
- [ ] Know Key Vault RBAC roles and secret versioning

Good luck on your AZ-204 exam!
