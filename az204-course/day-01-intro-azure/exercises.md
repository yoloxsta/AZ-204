# Day 01 — Exercises

Test your understanding of Azure fundamentals. Try to complete these without looking at the theory notes.

---

## Exercise 1: Resource Group Management

Create three resource groups in different regions, then list them all in a table.

```bash
# Create resource groups in three different regions
az group create --name ex01-rg-east --location eastus
az group create --name ex01-rg-west --location westus2
az group create --name ex01-rg-europe --location westeurope

# List all resource groups in table format
az group list --output table
```

**Your task:** After creating them, write a JMESPath query that returns only the resource groups whose names start with `ex01`.

<details>
<summary>Solution</summary>

```bash
az group list --query "[?starts_with(name, 'ex01')]" --output table
```
</details>

---

## Exercise 2: Storage Account with Specific Configuration

Create a storage account with the following requirements:
- Name: `ex02storage<your-initials><random>`
- Resource group: `ex01-rg-east`
- SKU: Standard_GRS (geo-redundant)
- Kind: StorageV2
- Access tier: Cool
- Minimum TLS version: 1.2

<details>
<summary>Solution</summary>

```bash
az storage account create \
  --name ex02storagejd42 \
  --resource-group ex01-rg-east \
  --location eastus \
  --sku Standard_GRS \
  --kind StorageV2 \
  --access-tier Cool \
  --min-tls-version TLS1_2
```
</details>

---

## Exercise 3: Bicep Template with Parameters

Write a Bicep template that creates:
1. A storage account
2. Accepts parameters for: name, location, and SKU
3. Has a default value for SKU (`Standard_LRS`)
4. Outputs the blob endpoint URL

Deploy it to `ex01-rg-west`.

<details>
<summary>Solution</summary>

**File: exercise3.bicep**
```bicep
param storageAccountName string
param location string = resourceGroup().location
param skuName string = 'Standard_LRS'

resource sa 'Microsoft.Storage/storageAccounts@2023-01-01' = {
  name: storageAccountName
  location: location
  sku: {
    name: skuName
  }
  kind: 'StorageV2'
  properties: {
    supportsHttpsTrafficOnly: true
    minimumTlsVersion: 'TLS1_2'
  }
}

output blobEndpoint string = sa.properties.primaryEndpoints.blob
```

**Deploy:**
```bash
az deployment group create \
  --resource-group ex01-rg-west \
  --template-file exercise3.bicep \
  --parameters storageAccountName=ex03storejd42
```
</details>

---

## Exercise 4: Tagging and Querying

1. Tag all three resource groups with `course=az204` and `exercise=day01`
2. Tag the storage account from Exercise 2 with `tier=cool` and `replication=grs`
3. Write a CLI command to find all resources tagged with `course=az204`

<details>
<summary>Solution</summary>

```bash
# Tag resource groups
az group update --name ex01-rg-east --tags course=az204 exercise=day01
az group update --name ex01-rg-west --tags course=az204 exercise=day01
az group update --name ex01-rg-europe --tags course=az204 exercise=day01

# Tag storage account
az storage account update \
  --name ex02storagejd42 \
  --resource-group ex01-rg-east \
  --tags tier=cool replication=grs course=az204

# Find all resources with the tag
az resource list --tag course=az204 --output table
```
</details>

---

## Exercise 5: Explore and Clean Up

1. List all resources across all resource groups that start with `ex01`
2. Show the deployment history for `ex01-rg-west`
3. Delete all three resource groups in a single script (use `--no-wait` for parallel deletion)

<details>
<summary>Solution</summary>

```bash
# List resources in all ex01 resource groups
az resource list --resource-group ex01-rg-east --output table
az resource list --resource-group ex01-rg-west --output table
az resource list --resource-group ex01-rg-europe --output table

# Show deployment history
az deployment group list --resource-group ex01-rg-west --output table

# Delete all resource groups (parallel)
az group delete --name ex01-rg-east --yes --no-wait
az group delete --name ex01-rg-west --yes --no-wait
az group delete --name ex01-rg-europe --yes --no-wait
```
</details>

---

## Quick Quiz

1. **What is the maximum number of resource groups a subscription can have?**
   <details><summary>Answer</summary>980 per subscription</details>

2. **Can a resource group contain resources from different regions?**
   <details><summary>Answer</summary>Yes. The resource group's location only determines where its metadata is stored.</details>

3. **What happens when you delete a resource group?**
   <details><summary>Answer</summary>All resources inside it are deleted.</details>

4. **What does ARM stand for and what is its role?**
   <details><summary>Answer</summary>Azure Resource Manager. It's the deployment and management layer that processes all requests to Azure services, regardless of the tool used (Portal, CLI, SDK, REST API).</details>

5. **Why must storage account names be globally unique?**
   <details><summary>Answer</summary>Because the storage account name becomes part of the public DNS endpoint (e.g., `https://<name>.blob.core.windows.net`). DNS names must be globally unique.</details>

---

## Checklist

Before moving to Day 02, make sure you can:

- [ ] Log in to Azure CLI and switch subscriptions
- [ ] Create, list, and delete resource groups
- [ ] Create a storage account with specific configuration
- [ ] Write and deploy a basic Bicep template
- [ ] Use JMESPath queries to filter CLI output
- [ ] Apply and query tags on resources
- [ ] Clean up resources to avoid charges

---

Next: [Day 02 — Azure Compute: App Service & Functions](../day-02-azure-compute/theory.md)
