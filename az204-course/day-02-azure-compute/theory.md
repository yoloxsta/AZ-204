# Day 02 — Azure Compute: App Service & Azure Functions

## What You'll Learn Today

- Azure App Service: what it is, how it works, plans, deployment slots
- Azure Functions: serverless compute, triggers, bindings
- When to use App Service vs Functions
- Deployment strategies for both services

---

## 1. Azure App Service

### What It Is

Azure App Service is a fully managed PaaS for hosting web applications, REST APIs, and mobile backends. You deploy your code or container and Azure handles the infrastructure — OS patching, scaling, load balancing.

### Supported Runtimes

- .NET / .NET Core
- Java
- Node.js
- Python
- PHP
- Ruby
- Custom containers (Docker)

### App Service Plans

An App Service Plan defines the compute resources for your app. Think of it as the VM underneath.

| Tier | Use Case | Features |
|------|----------|----------|
| **Free (F1)** | Testing | 1 GB disk, no custom domain, no scale |
| **Basic (B1-B3)** | Dev/test | Custom domains, manual scale up to 3 instances |
| **Standard (S1-S3)** | Production | Auto-scale, deployment slots, VNet integration |
| **Premium (P1v3-P3v3)** | High perf | More memory/CPU, up to 30 instances |
| **Isolated (I1v2-I3v2)** | Compliance | Dedicated environment (App Service Environment) |

### Key Concepts for AZ-204

**Deployment Slots:**
- Separate live environments within the same app (e.g., staging, production)
- You can swap slots with zero downtime
- Available in Standard tier and above
- Each slot has its own URL: `myapp-staging.azurewebsites.net`

**Application Settings & Connection Strings:**
- Environment variables for your app
- Can be "slot sticky" (stay with the slot during swap) or swap with the app
- Override values in `appsettings.json` or `web.config`

**Scaling:**
- **Scale Up** — change the App Service Plan tier (more CPU/RAM)
- **Scale Out** — add more instances (horizontal scaling)
- Auto-scale rules based on metrics (CPU, memory, HTTP queue length)

**Deployment Methods:**
- Git push (local git or GitHub/Azure Repos)
- ZIP deploy
- Docker container
- FTP
- Azure DevOps / GitHub Actions

### Architecture

```
                    ┌─────────────────────────┐
                    │     Azure App Service    │
                    │                          │
  Internet ──────► │  ┌───────────────────┐   │
                    │  │  Production Slot  │   │
                    │  │  myapp.azure...   │   │
                    │  └───────────────────┘   │
                    │          ▲ swap ▼         │
                    │  ┌───────────────────┐   │
                    │  │   Staging Slot    │   │
                    │  │  myapp-stg.az...  │   │
                    │  └───────────────────┘   │
                    │                          │
                    │  App Service Plan (S1)   │
                    └─────────────────────────┘
```

---

## 2. Azure Functions

### What It Is

Azure Functions is a serverless compute service. You write small pieces of code (functions) that run in response to events. You pay only for execution time.

### Key Concepts

**Triggers** — what causes a function to run:
- HTTP request
- Timer (CRON schedule)
- Blob storage event
- Queue message
- Cosmos DB change feed
- Event Grid event
- Service Bus message

**Bindings** — declarative connections to data:
- **Input bindings** — read data into your function
- **Output bindings** — write data from your function
- Avoid boilerplate connection code

**Example:** A function triggered by a queue message, with an input binding to Cosmos DB and an output binding to Blob Storage.

### Hosting Plans

| Plan | Scaling | Timeout | Use Case |
|------|---------|---------|----------|
| **Consumption** | Auto (0 to many) | 5 min (max 10) | Event-driven, low traffic |
| **Premium** | Pre-warmed instances | 60 min (unlimited) | No cold start, VNet |
| **Dedicated** | Manual/auto on App Service Plan | Unlimited | Already have App Service Plan |

### Durable Functions

An extension for writing stateful workflows in serverless:
- **Orchestrator functions** — define the workflow
- **Activity functions** — the work units
- **Entity functions** — stateful entities
- Patterns: function chaining, fan-out/fan-in, async HTTP APIs, monitoring, human interaction

### Architecture

```
  Event Source          Azure Functions          Output
  ───────────          ───────────────          ──────
  HTTP Request    ──►  ┌──────────────┐   ──►  HTTP Response
  Queue Message   ──►  │   Function   │   ──►  Blob Storage
  Timer (CRON)    ──►  │    App       │   ──►  Queue Message
  Blob Created    ──►  │              │   ──►  Cosmos DB
  Cosmos Change   ──►  └──────────────┘   ──►  SendGrid Email
```

---

## 3. When to Use What

| Scenario | Use |
|----------|-----|
| Full web application with UI | App Service |
| REST API with consistent traffic | App Service |
| Event-driven processing | Azure Functions |
| Scheduled background jobs | Azure Functions (Timer) |
| Microservices with HTTP endpoints | Either (Functions for lightweight) |
| Long-running workflows | Durable Functions |
| Need deployment slots | App Service (or Functions Premium) |

---

## Key Takeaways

1. App Service is PaaS for web apps — you choose the plan, deploy code, Azure handles the rest
2. Deployment slots enable zero-downtime deployments
3. Azure Functions is serverless — event-driven, pay-per-execution
4. Triggers start functions, bindings connect to data sources
5. Consumption plan scales to zero, Premium plan avoids cold starts
6. Durable Functions add stateful orchestration to serverless

---

## Deep Dives

For detailed coverage of specific AZ-204 exam topics, see:
- [Configuring Web App Settings (7 Topics)](./configuring-web-apps.md)
- [Scaling Apps in Azure App Service (6 Topics)](./scaling-apps.md)
- [Exploring App Service Deployment Slots (6 Topics)](./deployment-slots.md)

---

Next: [Lab — Deploy an App Service](./lab-app-service.md)
