# OrderFlow Deployment Guide

Step-by-step guide to deploy the OrderFlow project on an Azure VM (Ubuntu 22.04 LTS recommended).

---

## 1. Prerequisites

### Azure VM Setup

```bash
# Connect to your Azure VM
ssh azureuser@<vm-public-ip>
```

### Install .NET 8 SDK

```bash
# Add Microsoft package repository
wget https://packages.microsoft.com/config/ubuntu/22.04/packages-microsoft-prod.deb -O packages-microsoft-prod.deb
sudo dpkg -i packages-microsoft-prod.deb
rm packages-microsoft-prod.deb

# Install .NET 8 SDK
sudo apt-get update && sudo apt-get install -y dotnet-sdk-8.0

# Verify
dotnet --version
```

### Install Azure CLI

```bash
curl -sL https://aka.ms/InstallAzureCLI | sudo bash

# Login to Azure
az login
```

### Install Azure Functions Core Tools

```bash
curl -s https://packages.microsoft.com/keys/microsoft.asc | gpg --dearmor -o microsoft.gpg
sudo mv microsoft.gpg /etc/apt/trusted.gpg.d/microsoft.gpg
sudo sh -c 'echo "deb [arch=amd64] https://packages.microsoft.com/repos/microsoft-ubuntu-$(lsb_release -cs)-prod $(lsb_release -cs) main" > /etc/apt/sources.list.d/dotnetdev.list'
sudo apt-get update && sudo apt-get install -y azure-functions-core-tools-4

# Verify
func --version
```

### Install jq (for deploy script)

```bash
sudo apt-get install -y jq
```

---

## 2. Clone and Configure

```bash
# Clone the repository
git clone <repository-url> /home/azureuser/az204-course
cd /home/azureuser/az204-course/project-orderflow

# Review and update local settings if needed
cat OrderFlow.Functions/local.settings.json
```

---

## 3. Deploy Azure Resources

### Option A: Automated Deployment (Recommended)

```bash
chmod +x deploy/deploy.sh
./deploy/deploy.sh
```

Follow the prompts to enter:
- Resource Group name (default: `rg-orderflow-dev`)
- Azure Region (default: `eastus`)
- Environment name (default: `dev`)
- Project name (default: `orderflow`)

### Option B: Manual Deployment

```bash
# Create resource group
az group create --name rg-orderflow-dev --location eastus

# Deploy Bicep template
az deployment group create \
  --resource-group rg-orderflow-dev \
  --template-file deploy/azure-resources.bicep \
  --parameters environmentName=dev projectName=orderflow location=eastus

# Note the outputs for configuration
```

---

## 4. Build and Run the API

```bash
cd project-orderflow

# Restore packages
dotnet restore OrderFlow.Api/OrderFlow.Api.csproj

# Build
dotnet build OrderFlow.Api/OrderFlow.Api.csproj -c Release

# Run locally
cd OrderFlow.Api
dotnet run
```

The API will start at `https://localhost:5001` and `http://localhost:5000`.

### Verify

```bash
# Health check
curl -k https://localhost:5001/health

# Get orders
curl -k https://localhost:5001/api/orders

# Get products
curl -k https://localhost:5001/api/products
```

---

## 5. Build and Run Azure Functions

```bash
cd project-orderflow

# Build
dotnet build OrderFlow.Functions/OrderFlow.Functions.csproj -c Release

# Start Functions locally
cd OrderFlow.Functions
func start
```

Verify the following functions are registered:
- `OrderProcessorFunction` - Service Bus trigger on `order-queue`
- `OrderCleanupFunction` - Timer trigger (every 24 hours)
- `OrderEventFunction` - Event Grid trigger

---

## 6. Run Tests

```bash
cd project-orderflow

# Run all tests
dotnet test OrderFlow.Tests/OrderFlow.Tests.csproj --verbosity normal

# Run only unit tests
dotnet test OrderFlow.Tests/OrderFlow.Tests.csproj --filter "FullyQualifiedName~OrderTests"

# Run only integration tests
dotnet test OrderFlow.Tests/OrderFlow.Tests.csproj --filter "FullyQualifiedName~IntegrationTests"

# Generate code coverage report
dotnet test OrderFlow.Tests/OrderFlow.Tests.csproj /p:CollectCoverage=true /p:CoverletOutput=TestResults/
```

---

## 7. Test All Endpoints

```bash
API_BASE="https://localhost:5001"

# Health check
echo "=== Health Check ==="
curl -s "$API_BASE/health" | jq .

# List orders
echo "=== List Orders ==="
curl -s "$API_BASE/api/orders" | jq .

# Create an order
echo "=== Create Order ==="
curl -s -X POST "$API_BASE/api/orders" \
  -H "Content-Type: application/json" \
  -d '{
    "customerId": "test-customer-001",
    "items": [
      { "productId": "prod-001", "productName": "Cloud Programming Guide", "price": 39.99, "quantity": 1 },
      { "productId": "prod-002", "productName": "Azure Exam Prep", "price": 29.99, "quantity": 2 }
    ]
  }' | jq .

# List products
echo "=== List Products ==="
curl -s "$API_BASE/api/products" | jq .
```

---

## 8. Troubleshooting

### Common Issues

| Issue | Solution |
|-------|----------|
| `dotnet: command not found` | Run `export PATH=$PATH:$HOME/.dotnet` or add to `~/.bashrc` |
| `func: command not found` | Restart shell after installing Azure Functions Core Tools |
| `az: not logged in` | Run `az login --use-device-code` |
| Cosmos DB connection error | Verify the Cosmos DB account exists and check the endpoint in `local.settings.json` |
| Service Bus connection error | Check the Service Bus namespace and connection string |
| Port 5001 in use | Set `ASPNETCORE_URLS` env var or change the port in `launchSettings.json` |

### Useful Commands

```bash
# Check Azure CLI login status
az account show

# List resources in the resource group
az resource list --resource-group rg-orderflow-dev --output table

# View Cosmos DB data
az cosmosdb sql container show \
  --account-name orderflow-dev-cosmos \
  --database-name OrderFlowDb \
  --name Orders \
  --resource-group rg-orderflow-dev

# Stream Function logs
az functionapp log tail --name orderflow-dev-functions --resource-group rg-orderflow-dev

# Stream App Service logs
az webapp log tail --name orderflow-dev-api --resource-group rg-orderflow-dev
```

### Clean Up Resources

```bash
# Delete entire resource group
az group delete --name rg-orderflow-dev --yes --no-wait

# Or delete individual resources
az cosmosdb account delete --name orderflow-dev-cosmos --resource-group rg-orderflow-dev
```
