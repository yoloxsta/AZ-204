# Implementing Shared Access Signatures — Deep Dive

## Overview

This module covers SAS tokens in detail as a standalone AZ-204 topic: types, when to use them, stored access policies, and implementation.

---

## 1. Discover Shared Access Signatures

A Shared Access Signature (SAS) is a URI that grants restricted access to Azure Storage resources. It lets you grant access without sharing your account keys.

### SAS URI Format

```
https://myaccount.blob.core.windows.net/mycontainer/myblob.txt
  ?sv=2023-01-01           ← Storage service version
  &st=2026-03-15T00:00:00Z ← Start time
  &se=2026-03-16T00:00:00Z ← Expiry time
  &sr=b                    ← Resource type (b=blob, c=container)
  &sp=r                    ← Permissions (r=read)
  &sig=<signature>         ← HMAC-SHA256 signature
```

### Three Types of SAS

| Type | Signed With | Scope | Security |
|------|------------|-------|----------|
| **User Delegation SAS** | Entra ID credentials | Blob Storage only | Most secure |
| **Service SAS** | Account key | Single service (Blob, Queue, Table, File) | Medium |
| **Account SAS** | Account key | One or more services | Least restrictive |

### User Delegation SAS (Recommended)

- Signed with Entra ID credentials (not account key)
- If the account key is compromised, user delegation SAS tokens are unaffected
- Supports Blob Storage only
- Maximum validity: 7 days
- Requires the `Storage Blob Delegator` role

---

## 2. Choose When to Use Shared Access Signatures

### When to Use SAS

| Scenario | SAS Type |
|----------|----------|
| Grant temporary read access to a blob | User Delegation SAS |
| Allow a client to upload directly to storage | Service SAS with write permission |
| Grant access to multiple services | Account SAS |
| Provide access from a browser (no backend) | SAS with CORS configured |

### When NOT to Use SAS

| Scenario | Better Alternative |
|----------|-------------------|
| Server-to-server access | Managed Identity + RBAC |
| Long-term access | RBAC role assignment |
| Full account access | Use account keys (carefully) |
| Access from Azure services | Managed Identity |

### Security Best Practices

1. Always use HTTPS (`spr=https`)
2. Set the shortest possible expiry time
3. Grant minimum required permissions
4. Use User Delegation SAS when possible
5. Use stored access policies for revocability
6. Restrict IP addresses when possible (`sip=168.1.5.60-168.1.5.70`)
7. Never distribute SAS tokens in URLs that could be logged

---

## 3. Explore Stored Access Policies

A stored access policy provides server-side control over SAS tokens. Instead of embedding all parameters in the SAS URI, you reference a policy.

### Benefits

- **Revocation** — delete or modify the policy to invalidate all SAS tokens that reference it
- **Centralized management** — change permissions or expiry without regenerating tokens
- **Audit** — easier to track which policies grant what access

### Limitations

- Maximum 5 stored access policies per container, queue, table, or share
- Only available for Service SAS (not User Delegation or Account SAS)

### Create a Stored Access Policy

```bash
# Create a read-only policy valid for 7 days
az storage container policy create \
  --container-name mycontainer \
  --name read-policy \
  --account-name mystorageaccount \
  --permissions rl \
  --expiry $(date -u -d "+7 days" +%Y-%m-%dT%H:%MZ)

# Generate SAS using the policy
az storage blob generate-sas \
  --container-name mycontainer \
  --name myblob.txt \
  --account-name mystorageaccount \
  --policy-name read-policy \
  --output tsv
```

### Revoke Access

```bash
# Modify the policy (invalidates existing SAS tokens)
az storage container policy update \
  --container-name mycontainer \
  --name read-policy \
  --account-name mystorageaccount \
  --permissions ""  # Remove all permissions

# Or delete the policy entirely
az storage container policy delete \
  --container-name mycontainer \
  --name read-policy \
  --account-name mystorageaccount
```

---

## 4. Implement SAS Tokens in Code

### Generate a User Delegation SAS (.NET)

```csharp
using Azure.Identity;
using Azure.Storage.Blobs;
using Azure.Storage.Sas;

var credential = new DefaultAzureCredential();
var blobServiceClient = new BlobServiceClient(
    new Uri("https://mystorageaccount.blob.core.windows.net"),
    credential);

// Get user delegation key (valid for up to 7 days)
var delegationKey = await blobServiceClient.GetUserDelegationKeyAsync(
    DateTimeOffset.UtcNow,
    DateTimeOffset.UtcNow.AddHours(1));

// Build SAS for a specific blob
var sasBuilder = new BlobSasBuilder
{
    BlobContainerName = "mycontainer",
    BlobName = "myblob.txt",
    Resource = "b",  // b = blob
    StartsOn = DateTimeOffset.UtcNow,
    ExpiresOn = DateTimeOffset.UtcNow.AddHours(1)
};
sasBuilder.SetPermissions(BlobSasPermissions.Read);

// Generate the SAS token
var sasToken = sasBuilder.ToSasQueryParameters(
    delegationKey, blobServiceClient.AccountName);

var sasUri = $"{blobServiceClient.Uri}mycontainer/myblob.txt?{sasToken}";
Console.WriteLine($"SAS URI: {sasUri}");
```

### Generate a Service SAS (.NET)

```csharp
using Azure.Storage;
using Azure.Storage.Blobs;
using Azure.Storage.Sas;

var accountKey = "<your-account-key>";
var credential = new StorageSharedKeyCredential("mystorageaccount", accountKey);

var blobServiceClient = new BlobServiceClient(
    new Uri("https://mystorageaccount.blob.core.windows.net"),
    credential);

var containerClient = blobServiceClient.GetBlobContainerClient("mycontainer");
var blobClient = containerClient.GetBlobClient("myblob.txt");

// Generate SAS if the client was created with shared key
if (blobClient.CanGenerateSasUri)
{
    var sasBuilder = new BlobSasBuilder
    {
        BlobContainerName = "mycontainer",
        BlobName = "myblob.txt",
        Resource = "b",
        ExpiresOn = DateTimeOffset.UtcNow.AddHours(1)
    };
    sasBuilder.SetPermissions(BlobSasPermissions.Read | BlobSasPermissions.Write);

    Uri sasUri = blobClient.GenerateSasUri(sasBuilder);
    Console.WriteLine($"SAS URI: {sasUri}");
}
```

### Validate and Use a SAS URI

```csharp
// Use a SAS URI to access a blob (no credentials needed)
var sasUri = new Uri("https://mystorageaccount.blob.core.windows.net/mycontainer/myblob.txt?sv=...");
var blobClient = new BlobClient(sasUri);

var download = await blobClient.DownloadContentAsync();
Console.WriteLine(download.Value.Content.ToString());
```

---

## Key Exam Points

1. User Delegation SAS is the most secure — signed with Entra ID, not account key
2. User Delegation SAS is limited to Blob Storage and 7-day maximum validity
3. Stored access policies enable revocation of Service SAS tokens
4. Maximum 5 stored access policies per container/queue/table/share
5. Always use HTTPS and minimum permissions
6. Account SAS grants access across multiple services — use sparingly
7. If an account key is regenerated, all SAS tokens signed with that key are invalidated
8. `BlobSasBuilder` in .NET is the primary class for generating SAS tokens
