# Day 14 — Final Project: Build a Cloud-Native Application

## Title
Build an Order Processing System Using All AZ-204 Services

## Objective
Build a complete cloud-native application that integrates all the services you've learned across the 14 days. This project simulates a real-world order processing system.

## Architecture

```
┌─────────────────────────────────────────────────────────────────────┐
│                    Order Processing System                          │
│                                                                     │
│  ┌──────────┐    ┌──────────────┐    ┌──────────────────────────┐  │
│  │  Client   │───►│  APIM        │───►│  App Service (API)      │  │
│  │  (curl)   │    │  Gateway     │    │  - Order endpoints      │  │
│  └──────────┘    │  - Rate limit│    │  - Managed Identity     │  │
│                  │  - JWT valid  │    │  - App Insights SDK     │  │
│                  └──────────────┘    └──────────┬───────────────┘  │
│                                                  │                  │
│                    ┌─────────────────────────────┼──────────┐       │
│                    │                             │          │       │
│                    ▼                             ▼          ▼       │
│           ┌──────────────┐            ┌──────────┐  ┌──────────┐  │
│           │  Cosmos DB   │            │Key Vault │  │  Redis   │  │
│           │  (Orders)    │            │(Secrets) │  │ (Cache)  │  │
│           │  PK:/userId  │            └──────────┘  └──────────┘  │
│           └──────┬───────┘                                        │
│                  │ Change Feed                                     │
│                  ▼                                                  │
│           ┌──────────────┐    ┌──────────────┐                    │
│           │Azure Function│───►│ Service Bus  │                    │
│           │(Change Feed) │    │ (order-topic)│                    │
│           └──────────────┘    └──────┬───────┘                    │
│                                      │                             │
│                          ┌───────────┼───────────┐                │
│                          ▼           ▼           ▼                │
│                   ┌──────────┐ ┌──────────┐ ┌──────────┐         │
│                   │Sub: Email│ │Sub: Stock│ │Sub: Audit│         │
│                   │Notifier  │ │Updater   │ │Logger    │         │
│                   └──────────┘ └──────────┘ └──────────┘         │
│                                                                    │
│           ┌──────────────┐    ┌──────────────┐                    │
│           │ Blob Storage │    │ App Insights │                    │
│           │ (Receipts)   │    │ (Monitoring) │                    │
│           └──────────────┘    └──────────────┘                    │
└─────────────────────────────────────────────────────────────────────┘
```

---

## Services Used (All AZ-204 Domains)

| Day | Service | Role in Project |
|-----|---------|----------------|
| 01 | Resource Groups, CLI | Infrastructure setup |
| 02 | App Service | REST API hosting |
| 02 | Azure Functions | Change feed processor |
| 03 | Blob Storage | Receipt storage |
| 04 | Cosmos DB | Order database |
| 05 | Container Apps | (Optional) containerized deployment |
| 06 | Managed Identity | Secure service-to-service auth |
| 07 | Event Grid | (Optional) blob event notifications |
| 08 | Service Bus | Order event processing |
| 09 | Redis Cache | Order caching |
| 10 | API Management | API gateway |
| 11 | Application Insights | Monitoring and logging |
| 12 | Key Vault | Secret management |
| 13 | Bicep | Infrastructure as code |

---

## Step 1 — Deploy Infrastructure with Bicep

Create `infra/main.bicep`:

```bicep
param projectName string = 'az204final'
param location string = resourceGroup().location
param uniqueSuffix string = uniqueString(resourceGroup().id)

// Storage Account
resource storageAccount 'Microsoft.Storage/storageAccounts@2023-01-01' = {
  name: '${projectName}store${uniqueSuffix}'
  location: location
  sku: { name: 'Standard_LRS' }
  kind: 'StorageV2'
}

// Cosmos DB
resource cosmosAccount 'Microsoft.DocumentDB/databaseAccounts@2023-04-15' = {
  name: '${projectName}cosmos${uniqueSuffix}'
  location: location
  properties: {
    databaseAccountOfferType: 'Standard'
    consistencyPolicy: { defaultConsistencyLevel: 'Session' }
    locations: [{ locationName: location, failoverPriority: 0 }]
  }
}

resource cosmosDb 'Microsoft.DocumentDB/databaseAccounts/sqlDatabases@2023-04-15' = {
  parent: cosmosAccount
  name: 'OrdersDB'
  properties: { resource: { id: 'OrdersDB' } }
}

resource cosmosContainer 'Microsoft.DocumentDB/databaseAccounts/sqlDatabases/containers@2023-04-15' = {
  parent: cosmosDb
  name: 'Orders'
  properties: {
    resource: {
      id: 'Orders'
      partitionKey: { paths: ['/userId'], kind: 'Hash' }
    }
    options: { throughput: 400 }
  }
}

// Service Bus
resource serviceBus 'Microsoft.ServiceBus/namespaces@2022-10-01-preview' = {
  name: '${projectName}sb${uniqueSuffix}'
  location: location
  sku: { name: 'Standard', tier: 'Standard' }
}

resource sbTopic 'Microsoft.ServiceBus/namespaces/topics@2022-10-01-preview' = {
  parent: serviceBus
  name: 'order-events'
}

resource sbSubAll 'Microsoft.ServiceBus/namespaces/topics/subscriptions@2022-10-01-preview' = {
  parent: sbTopic
  name: 'all-events'
}

// Key Vault
resource keyVault 'Microsoft.KeyVault/vaults@2023-07-01' = {
  name: '${projectName}kv${uniqueSuffix}'
  location: location
  properties: {
    sku: { family: 'A', name: 'standard' }
    tenantId: subscription().tenantId
    enableRbacAuthorization: true
  }
}

// App Service
resource appPlan 'Microsoft.Web/serverfarms@2022-09-01' = {
  name: '${projectName}-plan'
  location: location
  sku: { name: 'S1' }
  kind: 'linux'
  properties: { reserved: true }
}

resource webApp 'Microsoft.Web/sites@2022-09-01' = {
  name: '${projectName}app${uniqueSuffix}'
  location: location
  identity: { type: 'SystemAssigned' }
  properties: {
    serverFarmId: appPlan.id
    siteConfig: { linuxFxVersion: 'DOTNETCORE|8.0' }
    httpsOnly: true
  }
}

// Application Insights
resource logAnalytics 'Microsoft.OperationalInsights/workspaces@2022-10-01' = {
  name: '${projectName}-law'
  location: location
}

resource appInsights 'Microsoft.Insights/components@2020-02-02' = {
  name: '${projectName}-insights'
  location: location
  kind: 'web'
  properties: {
    Application_Type: 'web'
    WorkspaceResourceId: logAnalytics.id
  }
}

// Outputs
output webAppName string = webApp.name
output webAppIdentityId string = webApp.identity.principalId
output cosmosEndpoint string = cosmosAccount.properties.documentEndpoint
output keyVaultUri string = keyVault.properties.vaultUri
output storageName string = storageAccount.name
output serviceBusNamespace string = serviceBus.name
output appInsightsConnectionString string = appInsights.properties.ConnectionString
```

Deploy:
```bash
az group create --name az204-final-rg --location eastus

az deployment group create \
  --resource-group az204-final-rg \
  --template-file infra/main.bicep \
  --query "properties.outputs" \
  --output json
```

Save the outputs — you'll need them for RBAC assignments and app configuration.

---

## Step 2 — Assign RBAC Roles to Managed Identity

```bash
# Get the managed identity principal ID from the deployment output
PRINCIPAL_ID=<webAppIdentityId-from-output>

# Cosmos DB Data Contributor
COSMOS_ID=$(az cosmosdb show --name <cosmos-name> --resource-group az204-final-rg --query id --output tsv)
az role assignment create --assignee $PRINCIPAL_ID --role "Cosmos DB Built-in Data Contributor" --scope $COSMOS_ID

# Key Vault Secrets User
KV_ID=$(az keyvault show --name <kv-name> --query id --output tsv)
az role assignment create --assignee $PRINCIPAL_ID --role "Key Vault Secrets User" --scope $KV_ID

# Storage Blob Data Contributor
STORAGE_ID=$(az storage account show --name <storage-name> --resource-group az204-final-rg --query id --output tsv)
az role assignment create --assignee $PRINCIPAL_ID --role "Storage Blob Data Contributor" --scope $STORAGE_ID

# Service Bus Data Sender
SB_ID=$(az servicebus namespace show --name <sb-name> --resource-group az204-final-rg --query id --output tsv)
az role assignment create --assignee $PRINCIPAL_ID --role "Azure Service Bus Data Sender" --scope $SB_ID
```

---

## Step 3 — Store Secrets in Key Vault

```bash
# Store Cosmos DB key
COSMOS_KEY=$(az cosmosdb keys list --name <cosmos-name> --resource-group az204-final-rg --query primaryMasterKey --output tsv)
az keyvault secret set --vault-name <kv-name> --name CosmosKey --value "$COSMOS_KEY"

# Store Service Bus connection string
SB_CONN=$(az servicebus namespace authorization-rule keys list --name RootManageSharedAccessKey --namespace-name <sb-name> --resource-group az204-final-rg --query primaryConnectionString --output tsv)
az keyvault secret set --vault-name <kv-name> --name ServiceBusConnection --value "$SB_CONN"
```

---

## Step 4 — Build the Order API

Create the .NET project:
```bash
dotnet new web -n OrderApi
cd OrderApi
dotnet add package Microsoft.Azure.Cosmos
dotnet add package Azure.Messaging.ServiceBus
dotnet add package Azure.Storage.Blobs
dotnet add package Azure.Security.KeyVault.Secrets
dotnet add package Azure.Identity
dotnet add package StackExchange.Redis
dotnet add package Microsoft.ApplicationInsights.AspNetCore
```

Create `Program.cs` with endpoints for:
- `POST /orders` — create an order (write to Cosmos DB, send to Service Bus, cache in Redis)
- `GET /orders/{userId}` — get orders for a user (check Redis cache first, then Cosmos DB)
- `GET /orders/{userId}/{orderId}` — get a specific order
- `GET /health` — health check

This is your capstone project — use the patterns from all previous days to build the API. The key integration points are:

1. **Managed Identity** (Day 06) for all service authentication
2. **Cosmos DB** (Day 04) as the primary data store
3. **Redis Cache** (Day 09) for the cache-aside pattern
4. **Service Bus** (Day 08) to publish order events
5. **Blob Storage** (Day 03) to store order receipts
6. **Key Vault** (Day 12) for secret management
7. **Application Insights** (Day 11) for monitoring

---

## Step 5 — Deploy and Test

```bash
dotnet publish -c Release -o ./publish
cd publish && zip -r ../deploy.zip . && cd ..

az webapp deployment source config-zip \
  --name <webapp-name> \
  --resource-group az204-final-rg \
  --src deploy.zip
```

Test:
```bash
APP_URL=https://<webapp-name>.azurewebsites.net

# Health check
curl $APP_URL/health

# Create an order
curl -X POST $APP_URL/orders \
  -H "Content-Type: application/json" \
  -d '{"userId": "user1", "product": "Laptop", "quantity": 1, "price": 1299.99}'

# Get orders
curl $APP_URL/orders/user1
```

---

## Step 6 — Verify All Integrations

| Service | How to Verify |
|---------|--------------|
| Cosmos DB | Check Data Explorer in Portal — orders should appear |
| Service Bus | Check topic metrics — messages should be published |
| Redis | Second GET request should be faster (cache hit) |
| Blob Storage | Check container for receipt files |
| Key Vault | App starts without hardcoded secrets |
| App Insights | Check Live Metrics and Application Map |
| Managed Identity | No connection strings in app settings |

---

## Cleanup

```bash
az group delete --name az204-final-rg --yes --no-wait
```

---

## What You've Built

A production-grade cloud-native application that demonstrates:
- Serverless and PaaS compute
- NoSQL data storage with proper partitioning
- Asynchronous messaging with pub-sub
- Caching for performance
- Security with managed identities and Key Vault
- Monitoring with Application Insights
- Infrastructure as code with Bicep

This covers every AZ-204 exam domain. You're ready for the exam.

---

Next: [Exam Prep — Practice Questions](../exam-prep/practice-questions.md)
