# OrderFlow - AZ-204 Certification Project

A full-stack e-commerce order processing system built with .NET 8, demonstrating Azure services covered in the AZ-204 exam.

---

## Architecture

```
┌─────────────────────────────────────────────────────────────────────┐
│                         OrderFlow System                            │
├─────────────────────────────────────────────────────────────────────┤
│                                                                     │
│  ┌──────────┐    HTTP     ┌──────────────┐    SDK     ┌─────────┐  │
│  │  Client   │───────────▶│  OrderFlow   │──────────▶│ Cosmos  │  │
│  │ (Browser) │◀───────────│     Api      │◀──────────│   DB    │  │
│  └──────────┘             │ (.NET 8 Web) │           └─────────┘  │
│                           └──────┬───────┘                        │
│                                  │                                  │
│                    ┌─────────────┼─────────────┐                   │
│                    ▼             ▼             ▼                    │
│              ┌──────────┐ ┌──────────┐ ┌──────────┐               │
│              │  Redis   │ │  Key     │ │   App    │               │
│              │  Cache   │ │  Vault   │ │   Conf   │               │
│              └──────────┘ └──────────┘ └──────────┘               │
│                                                                     │
│  ┌──────────────┐  Queue  ┌──────────────────┐                    │
│  │  Service Bus  │◀───────│  OrderFlow        │                    │
│  │  (Topic/Queue)│───────▶│  Functions        │                    │
│  └──────────────┘        │  ┌──────────────┐ │                    │
│                          │  │OrderProcessor│ │                    │
│  ┌──────────────┐ Event │  │OrderCleanup  │ │                    │
│  │  Event Grid  │◀──────│  │OrderEvent    │ │                    │
│  │    Topic     │───────│  └──────────────┘ │                    │
│  └──────────────┘       └────────┬───────────┘                    │
│                                  │                                  │
│                                  ▼                                  │
│                           ┌──────────┐                             │
│                           │ Storage  │                             │
│                           │ Account  │                             │
│                           │  (Blob)  │                             │
│                           └──────────┘                             │
│                                                                     │
│                    ┌──────────────────┐                            │
│                    │ App Insights     │                            │
│                    │ (Monitoring)     │                            │
│                    └──────────────────┘                            │
└─────────────────────────────────────────────────────────────────────┘
```

---

## AZ-204 Services Covered

This project demonstrates **10 Azure services** mapped directly to AZ-204 exam domains.

| Azure Service | Exam Domain | Project File | Purpose |
|---|---|---|---|
| Azure Cosmos DB | Develop for Azure Storage | `OrderFlow.Api/Services/CosmosOrderRepository.cs` | Order & product data (serverless) |
| Azure Blob Storage | Develop for Azure Storage | `OrderFlow.Functions/Functions/OrderProcessorFunction.cs` | Order document storage |
| Azure Service Bus | Implement Azure Messaging | `OrderFlow.Functions/Functions/OrderProcessorFunction.cs` | Order queue processing, event topics |
| Azure Event Grid | Implement Azure Messaging | `OrderFlow.Functions/Functions/OrderEventFunction.cs` | Order status change events |
| Azure Key Vault | Implement Cloud Security | `OrderFlow.Api/Program.cs` | Secret management |
| Azure App Configuration | Implement Cloud Security | `OrderFlow.Api/Program.cs` | Feature flags, centralized config |
| Azure Redis Cache | Develop for Azure Cache | `OrderFlow.Api/Services/CachedProductService.cs` | Product data caching |
| Azure Functions | Implement Azure Functions | `OrderFlow.Functions/Functions/*` | Serverless processing (v4, isolated) |
| Managed Identity | Implement Cloud Security | `deploy/azure-resources.bicep` | Passwordless authentication |
| App Insights | Monitor, Troubleshoot, Optimize | All projects | Telemetry & distributed tracing |

---

## Why This Project

### Learning Objectives

- **Hands-on practice** with Azure SDKs (Cosmos DB, Service Bus, Event Grid, Storage)
- **Managed Identity** for passwordless authentication across all services
- **Infrastructure as Code** with Bicep templates
- **Serverless patterns** with Azure Functions v4 (isolated worker)
- **Unit and integration testing** with Moq, FluentAssertions, xUnit
- **CI/CD-ready** project structure with deployment automation

### Exam Relevance

Every file in this project maps to a specific AZ-204 exam objective:

- **Develop for Azure Cloud (25-30%)**: Cosmos DB, Blob Storage, Redis Cache
- **Implement Azure Functions (15-20%)**: Timer, Service Bus, Event Grid triggers
- **Implement Azure Messaging (15-20%)**: Service Bus queues/topics, Event Grid
- **Implement Cloud Security (20-25%)**: Key Vault, App Configuration, Managed Identity
- **Monitor, Troubleshoot, and Optimize (15-20%)**: App Insights, logging

---

## Project Structure

```
project-orderflow/
├── OrderFlow.Api/                    # ASP.NET Core 8 Web API
│   ├── Controllers/                  # API controllers
│   ├── Models/                       # Domain models
│   ├── Services/                     # Business logic & data access
│   ├── Program.cs                    # App startup & DI
│   ├── appsettings.json              # Dev configuration
│   └── appsettings.Production.json   # Prod configuration
├── OrderFlow.Functions/              # Azure Functions v4 (isolated)
│   ├── Functions/
│   │   ├── OrderProcessorFunction.cs # Service Bus trigger
│   │   ├── OrderCleanupFunction.cs   # Timer trigger
│   │   └── OrderEventFunction.cs     # Event Grid trigger
│   ├── Program.cs                    # Functions host builder
│   └── local.settings.json           # Local dev settings
├── OrderFlow.Tests/                  # Unit & integration tests
│   ├── OrderTests.cs                 # Domain model unit tests
│   └── IntegrationTests.cs           # API integration tests
├── deploy/
│   ├── azure-resources.bicep         # Infrastructure as Code
│   ├── deploy.sh                     # Deployment automation
│   └── DEPLOY.md                     # Step-by-step deploy guide
└── README.md                         # This file
```

---

## How to Set Up

### Prerequisites

- [.NET 8 SDK](https://dot.net/download)
- [Azure CLI](https://docs.microsoft.com/cli/azure/install-azure-cli)
- [Azure Functions Core Tools v4](https://learn.microsoft.com/azure/azure-functions/functions-run-local)
- [Azure Cosmos DB Emulator](https://docs.microsoft.com/azure/cosmos-db/local-emulator) (for local dev)

### Quick Start

```bash
# Clone the repo
git clone <repo-url>
cd az204-course/project-orderflow

# Run tests
dotnet test OrderFlow.Tests/OrderFlow.Tests.csproj

# Run the API
cd OrderFlow.Api
dotnet run

# Run Azure Functions (in another terminal)
cd ../OrderFlow.Functions
func start
```

### Full Azure Deployment

```bash
# Deploy all Azure resources
chmod +x deploy/deploy.sh
./deploy/deploy.sh

# See deploy/DEPLOY.md for detailed instructions
```

---

## API Endpoint Reference

| Method | Endpoint | Description | Request Body |
|---|---|---|---|
| `GET` | `/api/orders` | List all orders | - |
| `GET` | `/api/orders/{id}` | Get order by ID | - |
| `POST` | `/api/orders` | Create new order | `{ customerId, items[] }` |
| `GET` | `/api/products` | List all products | - |
| `GET` | `/api/products/{id}` | Get product by ID | - |
| `GET` | `/health` | Health check | - |

### Sample Order Request

```json
POST /api/orders
{
  "customerId": "customer-001",
  "items": [
    {
      "productId": "prod-001",
      "productName": "AZ-204 Study Guide",
      "price": 39.99,
      "quantity": 2
    }
  ]
}
```

---

## Configuration

All settings are centralized with environment-specific overrides:

| Setting | Description | Source |
|---|---|---|
| `CosmosDb:AccountEndpoint` | Cosmos DB endpoint | Key Vault / appsettings |
| `BlobStorage:ConnectionString` | Storage account connection | Key Vault / env var |
| `ServiceBus:ConnectionString` | Service Bus namespace | Key Vault / env var |
| `EventGrid:TopicKey` | Event Grid auth key | Key Vault / env var |
| `KeyVault:VaultUri` | Key Vault URI | Config / env var |
| `Redis:ConnectionString` | Redis cache connection | Config / env var |
| `ApplicationInsights:ConnectionString` | Telemetry connection | Config / env var |

Production settings read from environment variables (with `__` separator) and Azure App Configuration.

---

## License

Educational project for AZ-204 certification study.
