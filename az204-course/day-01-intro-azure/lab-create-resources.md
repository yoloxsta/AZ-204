# Lab: Create Your First Azure Resources

## Title
Setting Up Your Azure Development Environment and Creating Resources

## Objective
By the end of this lab, you will be able to:
- Log in to Azure using the CLI
- Create and manage resource groups
- Create a storage account
- Deploy resources using a Bicep template
- Verify deployments and clean up resources

## Architecture

```
┌─────────────────────────────────────────────┐
│              Azure Subscription              │
│                                              │
│  ┌────────────────────────────────────────┐  │
│  │     Resource Group: az204-day01-rg     │  │
│  │         (Location: eastus)             │  │
│  │                                        │  │
│  │  ┌──────────────────────────────────┐  │  │
│  │  │  Storage Account                 │  │  │
│  │  │  az204day01store<unique>         │  │  │
│  │  │  SKU: Standard_LRS              │  │  │
│  │  │  Kind: StorageV2                │  │  │
│  │  └──────────────────────────────────┘  │  │
│  │                                        │  │
│  └────────────────────────────────────────┘  │
│                                              │
└─────────────────────────────────────────────┘
```

In this lab, you create a resource group as a container, then deploy a storage account inside it — first with CLI commands, then with a Bicep template.

## Prerequisites

- An active Azure subscription (a free account works: https://azure.microsoft.com/free/)
- Azure CLI installed (version 2.50+)
- A terminal (Windows Terminal, bash, or Azure Cloud Shell)

Verify your CLI installation:
```bash
az --version
```

---

## Part 1: Azure CLI Login and Setup

### Step 1 — Log in to Azure

```bash
az login
```

This opens a browser window for authentication. After logging in, the CLI displays your subscriptions.

### Step 2 — Verify your subscription

```bash
az account show --output table
```

Expected output:
```
EnvironmentName    IsDefault    Name                State    TenantId
-----------------  -----------  ------------------  -------  ------------------------------------
AzureCloud         True         My Subscription     Enabled  xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx
```

### Step 3 — Set the default subscription (if you have multiple)

```bash
az account set --subscription "<your-subscription-name-or-id>"
```

### Step 4 — Register required resource providers

Some providers need to be registered before first use:

```bash
az provider register --namespace Microsoft.Storage
az provider register --namespace Microsoft.Web
az provider register --namespace Microsoft.DocumentDB
```

Check registration status:
```bash
az provider show --namespace Microsoft.Storage --query "registrationState" --output tsv
```

Expected output: `Registered`

---

## Part 2: Create Resources with Azure CLI

### Step 5 — Create a resource group

```bash
az group create \
  --name az204-day01-rg \
  --location eastus
```

Expected output (JSON):
```json
{
  "id": "/subscriptions/.../resourceGroups/az204-day01-rg",
  "location": "eastus",
  "name": "az204-day01-rg",
  "properties": {
    "provisioningState": "Succeeded"
  },
  "type": "Microsoft.Resources/resourceGroups"
}
```

### Step 6 — Verify the resource group exists

```bash
az group list --query "[?name=='az204-day01-rg']" --output table
```

### Step 7 — Create a storage account

Storage account names must be globally unique, 3-24 characters, lowercase letters and numbers only.

```bash
# Replace <unique-suffix> with something unique, e.g., your initials + random numbers
az storage account create \
  --name az204day01store<unique-suffix> \
  --resource-group az204-day01-rg \
  --location eastus \
  --sku Standard_LRS \
  --kind StorageV2
```

### Step 8 — Verify the storage account

```bash
az storage account list \
  --resource-group az204-day01-rg \
  --output table
```

Expected output:
```
Name                    ResourceGroup     Location    Kind       SkuName
----------------------  ----------------  ----------  ---------  -----------
az204day01storeXXX      az204-day01-rg    eastus      StorageV2  Standard_LRS
```

### Step 9 — Get storage account details

```bash
az storage account show \
  --name az204day01store<unique-suffix> \
  --resource-group az204-day01-rg \
  --query "{name:name, location:location, sku:sku.name, kind:kind, endpoints:primaryEndpoints}" \
  --output json
```

### Step 10 — List all resources in the resource group

```bash
az resource list \
  --resource-group az204-day01-rg \
  --output table
```

---

## Part 3: Deploy with Bicep

### Step 11 — Create a Bicep template

Create a file called `main.bicep` with the following content:

```bicep
@description('The name of the storage account')
param storageAccountName string

@description('The location for all resources')
param location string = resourceGroup().location

@allowed([
  'Standard_LRS'
  'Standard_GRS'
  'Standard_ZRS'
])
@description('The SKU of the storage account')
param skuName string = 'Standard_LRS'

resource storageAccount 'Microsoft.Storage/storageAccounts@2023-01-01' = {
  name: storageAccountName
  location: location
  sku: {
    name: skuName
  }
  kind: 'StorageV2'
  properties: {
    minimumTlsVersion: 'TLS1_2'
    supportsHttpsTrafficOnly: true
    accessTier: 'Hot'
  }
}

output storageAccountId string = storageAccount.id
output primaryBlobEndpoint string = storageAccount.properties.primaryEndpoints.blob
```

### Step 12 — Deploy the Bicep template

```bash
az deployment group create \
  --resource-group az204-day01-rg \
  --template-file main.bicep \
  --parameters storageAccountName=az204bicepstore<unique-suffix>
```

### Step 13 — Verify the Bicep deployment

```bash
az deployment group list \
  --resource-group az204-day01-rg \
  --output table
```

Check both storage accounts now exist:
```bash
az storage account list \
  --resource-group az204-day01-rg \
  --output table
```

You should see two storage accounts — one created via CLI, one via Bicep.

---

## Part 4: Explore with Tags and Queries

### Step 14 — Add tags to your resource group

```bash
az group update \
  --name az204-day01-rg \
  --tags environment=learning course=az204 day=01
```

### Step 15 — Add tags to a storage account

```bash
az storage account update \
  --name az204day01store<unique-suffix> \
  --resource-group az204-day01-rg \
  --tags environment=learning purpose=lab
```

### Step 16 — Query resources by tag

```bash
az resource list \
  --tag environment=learning \
  --output table
```

---

## Part 5: Cleanup

Always clean up resources after labs to avoid charges.

### Step 17 — Delete the resource group (and everything inside it)

```bash
az group delete \
  --name az204-day01-rg \
  --yes \
  --no-wait
```

The `--no-wait` flag returns immediately while deletion happens in the background.

### Step 18 — Verify deletion

```bash
az group list --query "[?name=='az204-day01-rg']" --output table
```

This should return empty results (may take a few minutes).

---

## Expected Outcomes

After completing this lab, you have:

| Task | Status |
|------|--------|
| Logged in to Azure CLI | ✅ |
| Created a resource group | ✅ |
| Created a storage account via CLI | ✅ |
| Deployed a storage account via Bicep | ✅ |
| Applied tags to resources | ✅ |
| Queried resources with JMESPath | ✅ |
| Cleaned up all resources | ✅ |

---

## Troubleshooting

| Issue | Solution |
|-------|----------|
| `az login` fails | Try `az login --use-device-code` for environments without a browser |
| Storage account name taken | Choose a more unique name (add random numbers) |
| "AuthorizationFailed" | Check you have Contributor role on the subscription |
| Bicep not recognized | Update CLI: `az upgrade` (Bicep is bundled with CLI 2.20+) |

---

Next: [Exercises](./exercises.md)
