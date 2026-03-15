# Lab: Azure Blob Storage Operations

## Title
Create, Upload, Download, and Manage Blobs with Azure CLI

## Objective
- Create a storage account and blob containers
- Upload and download blobs
- Set access tiers on blobs
- Configure lifecycle management policies
- Generate SAS tokens
- Work with blob metadata and properties

## Architecture

```
┌──────────────────────────────────────────────────────┐
│           Resource Group: az204-day03-rg              │
│                                                      │
│  ┌────────────────────────────────────────────────┐  │
│  │  Storage Account: az204store<unique>           │  │
│  │  Kind: StorageV2 | SKU: Standard_LRS          │  │
│  │                                                │  │
│  │  ┌─────────────────┐  ┌─────────────────────┐ │  │
│  │  │ images (container│  │ logs (container)    │ │  │
│  │  │ Access: Private) │  │ Access: Private)    │ │  │
│  │  │                  │  │                     │ │  │
│  │  │ photo1.jpg (Hot) │  │ app.log (Cool)      │ │  │
│  │  │ photo2.png (Hot) │  │ error.log (Cool)    │ │  │
│  │  └─────────────────┘  └─────────────────────┘ │  │
│  │                                                │  │
│  │  Lifecycle Policy: logs/ → Cool@30d → Del@365d │  │
│  └────────────────────────────────────────────────┘  │
└──────────────────────────────────────────────────────┘
```

## Prerequisites
- Azure CLI installed and logged in
- A few sample files to upload (create them in the steps below)

---

## Step 1 — Create Resource Group and Storage Account

```bash
az group create \
  --name az204-day03-rg \
  --location eastus

az storage account create \
  --name az204store<unique> \
  --resource-group az204-day03-rg \
  --location eastus \
  --sku Standard_LRS \
  --kind StorageV2 \
  --access-tier Hot \
  --min-tls-version TLS1_2
```

Get the connection string (you'll need it for later steps):
```bash
az storage account show-connection-string \
  --name az204store<unique> \
  --resource-group az204-day03-rg \
  --query connectionString \
  --output tsv
```

Store it in an environment variable:
```bash
# Bash
export AZURE_STORAGE_CONNECTION_STRING="<paste-connection-string>"

# PowerShell
$env:AZURE_STORAGE_CONNECTION_STRING="<paste-connection-string>"

# CMD
set AZURE_STORAGE_CONNECTION_STRING=<paste-connection-string>
```

---

## Step 2 — Create Blob Containers

```bash
az storage container create \
  --name images \
  --account-name az204store<unique> \
  --public-access off

az storage container create \
  --name logs \
  --account-name az204store<unique> \
  --public-access off
```

Verify:
```bash
az storage container list \
  --account-name az204store<unique> \
  --output table
```

---

## Step 3 — Create Sample Files and Upload Blobs

Create sample files locally:
```bash
echo "This is a sample text file for AZ-204 lab" > sample.txt
echo "Log entry 1: Application started" > app.log
echo "Error entry 1: Something went wrong" > error.log
```

Upload to the images container:
```bash
az storage blob upload \
  --container-name images \
  --name documents/sample.txt \
  --file sample.txt \
  --account-name az204store<unique>
```

Upload to the logs container:
```bash
az storage blob upload \
  --container-name logs \
  --name app.log \
  --file app.log \
  --account-name az204store<unique>

az storage blob upload \
  --container-name logs \
  --name error.log \
  --file error.log \
  --account-name az204store<unique>
```

Upload multiple files with `upload-batch`:
```bash
# Create a directory with multiple files
mkdir upload-test
echo "file1 content" > upload-test/file1.txt
echo "file2 content" > upload-test/file2.txt
echo "file3 content" > upload-test/file3.txt

az storage blob upload-batch \
  --destination images \
  --source upload-test \
  --account-name az204store<unique> \
  --pattern "*.txt"
```

---

## Step 4 — List and Download Blobs

List blobs in a container:
```bash
az storage blob list \
  --container-name images \
  --account-name az204store<unique> \
  --output table
```

Download a blob:
```bash
az storage blob download \
  --container-name images \
  --name documents/sample.txt \
  --file downloaded-sample.txt \
  --account-name az204store<unique>
```

Verify the download:
```bash
# Bash/PowerShell
cat downloaded-sample.txt

# CMD
type downloaded-sample.txt
```

---

## Step 5 — Set Blob Properties and Metadata

Set metadata on a blob:
```bash
az storage blob metadata update \
  --container-name images \
  --name documents/sample.txt \
  --account-name az204store<unique> \
  --metadata author=az204student project=learning
```

Read metadata:
```bash
az storage blob metadata show \
  --container-name images \
  --name documents/sample.txt \
  --account-name az204store<unique> \
  --output json
```

Show blob properties (content type, size, tier, etc.):
```bash
az storage blob show \
  --container-name images \
  --name documents/sample.txt \
  --account-name az204store<unique> \
  --query "{name:name, contentType:properties.contentSettings.contentType, size:properties.contentLength, tier:properties.blobTier, lastModified:properties.lastModified}" \
  --output table
```

---

## Step 6 — Change Access Tiers

Set a blob to Cool tier:
```bash
az storage blob set-tier \
  --container-name logs \
  --name app.log \
  --account-name az204store<unique> \
  --tier Cool
```

Set a blob to Archive tier:
```bash
az storage blob set-tier \
  --container-name logs \
  --name error.log \
  --account-name az204store<unique> \
  --tier Archive
```

Verify tiers:
```bash
az storage blob list \
  --container-name logs \
  --account-name az204store<unique> \
  --query "[].{name:name, tier:properties.blobTier}" \
  --output table
```

Rehydrate an archived blob (this takes hours in real scenarios):
```bash
az storage blob set-tier \
  --container-name logs \
  --name error.log \
  --account-name az204store<unique> \
  --tier Hot
```

---

## Step 7 — Configure Lifecycle Management Policy

Create a lifecycle policy file `lifecycle-policy.json`:

```json
{
  "rules": [
    {
      "enabled": true,
      "name": "move-logs-to-cool",
      "type": "Lifecycle",
      "definition": {
        "filters": {
          "blobTypes": ["blockBlob"],
          "prefixMatch": ["logs/"]
        },
        "actions": {
          "baseBlob": {
            "tierToCool": {
              "daysAfterModificationGreaterThan": 30
            },
            "tierToArchive": {
              "daysAfterModificationGreaterThan": 90
            },
            "delete": {
              "daysAfterModificationGreaterThan": 365
            }
          }
        }
      }
    }
  ]
}
```

Apply the policy:
```bash
az storage account management-policy create \
  --account-name az204store<unique> \
  --resource-group az204-day03-rg \
  --policy @lifecycle-policy.json
```

Verify:
```bash
az storage account management-policy show \
  --account-name az204store<unique> \
  --resource-group az204-day03-rg \
  --output json
```

---

## Step 8 — Generate SAS Tokens

Generate an account-level SAS:
```bash
# Get account key
ACCOUNT_KEY=$(az storage account keys list \
  --account-name az204store<unique> \
  --resource-group az204-day03-rg \
  --query "[0].value" \
  --output tsv)

# Generate SAS token (valid for 1 hour)
az storage account generate-sas \
  --account-name az204store<unique> \
  --account-key $ACCOUNT_KEY \
  --services b \
  --resource-types sco \
  --permissions rl \
  --expiry $(date -u -d "+1 hour" +%Y-%m-%dT%H:%MZ) \
  --output tsv
```

Generate a blob-level SAS:
```bash
az storage blob generate-sas \
  --container-name images \
  --name documents/sample.txt \
  --account-name az204store<unique> \
  --account-key $ACCOUNT_KEY \
  --permissions r \
  --expiry $(date -u -d "+1 hour" +%Y-%m-%dT%H:%MZ) \
  --output tsv
```

The SAS token can be appended to the blob URL to grant temporary access:
```
https://az204store<unique>.blob.core.windows.net/images/documents/sample.txt?<sas-token>
```

---

## Step 9 — Create a Stored Access Policy

```bash
az storage container policy create \
  --container-name images \
  --name read-policy \
  --account-name az204store<unique> \
  --permissions rl \
  --expiry $(date -u -d "+7 days" +%Y-%m-%dT%H:%MZ)
```

Generate a SAS using the stored policy:
```bash
az storage blob generate-sas \
  --container-name images \
  --name documents/sample.txt \
  --account-name az204store<unique> \
  --policy-name read-policy \
  --output tsv
```

To revoke access, delete or modify the policy:
```bash
az storage container policy delete \
  --container-name images \
  --name read-policy \
  --account-name az204store<unique>
```

---

## Step 10 — Copy and Delete Blobs

Copy a blob within the same account:
```bash
az storage blob copy start \
  --source-container images \
  --source-blob documents/sample.txt \
  --destination-container logs \
  --destination-blob backup/sample-backup.txt \
  --account-name az204store<unique>
```

Delete a blob:
```bash
az storage blob delete \
  --container-name images \
  --name documents/sample.txt \
  --account-name az204store<unique>
```

Delete a container and all its blobs:
```bash
az storage container delete \
  --name logs \
  --account-name az204store<unique>
```

---

## Cleanup

```bash
# Clean up local files
rm -f sample.txt app.log error.log downloaded-sample.txt lifecycle-policy.json
rm -rf upload-test

# Delete all Azure resources
az group delete --name az204-day03-rg --yes --no-wait
```

---

## Expected Outcomes

| Task | Status |
|------|--------|
| Created storage account with specific config | ✅ |
| Created blob containers | ✅ |
| Uploaded and downloaded blobs | ✅ |
| Set blob metadata and properties | ✅ |
| Changed access tiers (Hot, Cool, Archive) | ✅ |
| Configured lifecycle management policy | ✅ |
| Generated SAS tokens | ✅ |
| Created stored access policy | ✅ |
| Copied and deleted blobs | ✅ |

---

Next: [Lab — Azure Storage SDK](./lab-sdk.md)
