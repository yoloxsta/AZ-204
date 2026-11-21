# Day 01 — Introduction to Azure for Developers

## What You'll Learn Today

- What Microsoft Azure is and how it fits into cloud computing
- How Azure organizes resources (subscriptions, resource groups, resources)
- Azure regions and availability zones
- Azure Resource Manager (ARM) — the control plane behind everything
- Azure CLI and Azure Portal basics
- How the AZ-204 exam is structured

---
## 1. What is Microsoft Azure?

Azure is Microsoft's cloud computing platform. It provides over 200 services that let you build, deploy, and manage applications across a global network of data centers.

As a developer, Azure gives you:

- **Compute** — run your code (VMs, App Service, Functions, Containers)
- **Storage** — store data (Blobs, Tables, Queues, Disks)
- **Databases** — managed databases (Cosmos DB, SQL Database)
- **Networking** — connect services (VNets, Load Balancers, DNS)
- **Identity** — authenticate users and apps (Entra ID, Managed Identities)
- **Messaging** — decouple services (Service Bus, Event Grid)

### Cloud Service Models

| Model | You Manage | Azure Manages | Example |
|-------|-----------|---------------|---------|
| **IaaS** | OS, runtime, app | Hardware, networking | Virtual Machines |
| **PaaS** | App code, config | OS, runtime, hardware | App Service, Functions |
| **SaaS** | Nothing (just use it) | Everything | Microsoft 365 |

For AZ-204, you'll mostly work with **PaaS** services — you write code, Azure handles the infrastructure.

---

## 2. Azure Resource Hierarchy

Azure organizes everything in a clear hierarchy:

```
Microsoft Entra ID Tenant
  └── Management Groups (optional)
       └── Subscriptions
            └── Resource Groups
                 └── Resources
```

### Subscriptions

A subscription is a billing boundary. Every resource you create lives inside a subscription. You can have multiple subscriptions to separate environments (dev, staging, prod) or teams.

### Resource Groups

A resource group is a logical container for related resources. Think of it as a folder.

Key rules:
- Every resource must belong to exactly one resource group
- A resource group has a **location** (region), but resources inside it can be in different regions
- Deleting a resource group deletes **everything** inside it
- Use resource groups to organize by application, environment, or lifecycle

**Example:** A web app might have a resource group containing:
- An App Service (the web app)
- A Storage Account (for file uploads)
- An Application Insights instance (for monitoring)
- A Key Vault (for secrets)

### Resources

A resource is a single service instance — a storage account, a database, a function app, etc. Each resource has:
- A **name** (must be unique within its scope)
- A **type** (e.g., `Microsoft.Storage/storageAccounts`)
- A **location** (Azure region)
- **Properties** (configuration specific to the resource type)

---

## 3. Azure Regions and Availability

### Regions

Azure has 60+ regions worldwide. A region is a set of data centers in a geographic area.

Examples:
- `eastus` — East US (Virginia)
- `westeurope` — West Europe (Netherlands)
- `southeastasia` — Southeast Asia (Singapore)

When choosing a region, consider:
- **Proximity to users** — lower latency
- **Service availability** — not all services are in all regions
- **Compliance** — data residency requirements
- **Cost** — pricing varies by region

### Availability Zones

Within a region, Azure has **availability zones** — physically separate data centers with independent power, cooling, and networking. There are typically 3 zones per region.

Use availability zones for high availability:
- **Zonal services** — pin a resource to a specific zone (e.g., a VM)
- **Zone-redundant services** — Azure automatically replicates across zones (e.g., Zone-Redundant Storage)

---

## 4. Azure Resource Manager (ARM)

ARM is the **deployment and management layer** for Azure. Every action you take — whether through the Portal, CLI, PowerShell, SDK, or REST API — goes through ARM.

```
┌──────────────┐  ┌──────────────┐  ┌──────────────┐
│ Azure Portal │  │  Azure CLI   │  │   REST API   │
└──────┬───────┘  └──────┬───────┘  └──────┬───────┘
       │                 │                 │
       └────────────┬────┘─────────────────┘
                    │
            ┌───────▼────────┐
            │  Azure Resource │
            │    Manager      │
            │   (ARM)         │
            └───────┬────────┘
                    │
       ┌────────────┼────────────┐
       │            │            │
  ┌────▼───┐  ┌────▼───┐  ┌────▼───┐
  │Compute │  │Storage │  │  DB    │
  │Provider│  │Provider│  │Provider│
  └────────┘  └────────┘  └────────┘
```

### Why ARM Matters

- **Consistent management layer** — same API regardless of how you interact with Azure
- **Declarative templates** — define what you want, ARM figures out how to create it
- **Dependency management** — ARM understands resource dependencies and creates them in the right order
- **RBAC integration** — access control is built into ARM
- **Tagging** — organize and track costs with metadata tags
- **Idempotent deployments** — deploy the same template multiple times safely

### ARM Templates & Bicep

ARM templates are JSON files that define your infrastructure. **Bicep** is a cleaner, more readable language that compiles to ARM JSON.

**ARM Template (JSON):**
```json
{
  "$schema": "https://schema.management.azure.com/schemas/2019-04-01/deploymentTemplate.json#",
  "contentVersion": "1.0.0.0",
  "resources": [
    {
      "type": "Microsoft.Storage/storageAccounts",
      "apiVersion": "2023-01-01",
      "name": "mystorageaccount204",
      "location": "[resourceGroup().location]",
      "sku": { "name": "Standard_LRS" },
      "kind": "StorageV2"
    }
  ]
}
```

**Bicep equivalent:**
```bicep
resource storageAccount 'Microsoft.Storage/storageAccounts@2023-01-01' = {
  name: 'mystorageaccount204'
  location: resourceGroup().location
  sku: {
    name: 'Standard_LRS'
  }
  kind: 'StorageV2'
}
```

Bicep is much easier to read and write. For AZ-204, you should be comfortable with both formats.

---

## 5. Azure CLI Fundamentals

The Azure CLI (`az`) is a command-line tool for managing Azure resources. It works on Windows, macOS, and Linux.

### Installation

```bash
# Windows (winget)
winget install Microsoft.AzureCLI

# macOS (Homebrew)
brew install azure-cli

# Linux (Ubuntu/Debian)
curl -sL https://aka.ms/InstallAzureCLIDeb | sudo bash
```

### Core Commands

```bash
# Login to Azure
az login

# Set your default subscription
az account set --subscription "My Subscription"

# List subscriptions
az account list --output table

# Create a resource group
az group create --name myResourceGroup --location eastus

# List resource groups
az group list --output table

# Delete a resource group
az group delete --name myResourceGroup --yes --no-wait
```

### CLI Output Formats

```bash
# Table format (human-readable)
az group list --output table

# JSON format (default, good for scripting)
az group list --output json

# TSV format (tab-separated, good for piping)
az group list --query "[].name" --output tsv
```

### JMESPath Queries

The `--query` parameter uses JMESPath to filter output:

```bash
# Get just the names of all resource groups
az group list --query "[].name" --output tsv

# Get resource groups in eastus
az group list --query "[?location=='eastus']" --output table

# Get a specific property
az storage account show --name myaccount --resource-group myRG --query "primaryEndpoints.blob" --output tsv
```

---

## 6. Azure Portal Overview

The Azure Portal (https://portal.azure.com) is the web-based UI for managing Azure. Key areas:

- **Dashboard** — customizable home page with pinned resources
- **All services** — browse all Azure services
- **Resource groups** — view and manage resource groups
- **Cloud Shell** — built-in terminal (Bash or PowerShell) right in the browser
- **Cost Management** — track spending

For AZ-204, you'll use both the Portal and CLI. The exam tests CLI commands and SDK code, so don't rely only on the Portal.

---

## 7. AZ-204 Exam Structure

| Area | Weight |
|------|--------|
| Develop Azure compute solutions | 25–30% |
| Develop for Azure storage | 15–20% |
| Implement Azure security | 20–25% |
| Monitor, troubleshoot, and optimize | 15–20% |
| Connect to and consume Azure services | 15–20% |

- **Format:** Multiple choice, drag-and-drop, case studies
- **Duration:** 100 minutes
- **Passing score:** 700/1000
- **Cost:** ~$165 USD

---

## Key Takeaways

1. Azure organizes resources in a hierarchy: Tenant → Subscription → Resource Group → Resource
2. ARM is the control plane — everything goes through it
3. Bicep is the modern way to define infrastructure as code
4. Azure CLI is essential for AZ-204 — learn the command patterns
5. Choose regions based on latency, compliance, and service availability
6. The exam is heavily PaaS-focused — App Service, Functions, Cosmos DB, Storage, etc.

---

Next: [Lab — Create Your First Azure Resources](./lab-create-resources.md)
