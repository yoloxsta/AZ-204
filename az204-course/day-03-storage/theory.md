# Day 03 — Azure Storage: Blobs & SDKs

## What You'll Learn Today

- Azure Storage account types and redundancy options
- Blob Storage: containers, blobs, access tiers
- Blob lifecycle management policies
- Azure Storage SDK for .NET
- Shared Access Signatures (SAS)

---

## 1. Azure Storage Accounts

A storage account provides a unique namespace for your Azure Storage data. Every object you store has an address that includes your account name.

### Storage Account Types

| Type | Services | Use Case |
|------|----------|----------|
| **Standard general-purpose v2** | Blob, Queue, Table, File | Most scenarios (recommended) |
| **Premium block blobs** | Blob (block blobs only) | High transaction rates, low latency |
| **Premium file shares** | File only | Enterprise file shares |
| **Premium page blobs** | Page blobs only | VM disks |

### Redundancy Options

| Option | Copies | Durability | Use Case |
|--------|--------|-----------|----------|
| **LRS** | 3 copies in one data center | 11 nines | Dev/test, non-critical |
| **ZRS** | 3 copies across 3 zones | 12 nines | High availability in one region |
| **GRS** | 6 copies (3 local + 3 remote region) | 16 nines | Disaster recovery |
| **GZRS** | 6 copies (3 zones + 3 remote) | 16 nines | Maximum durability |
| **RA-GRS** | GRS + read access to secondary | 16 nines | Read access during outage |
| **RA-GZRS** | GZRS + read access to secondary | 16 nines | Maximum availability |

---

## 2. Blob Storage

Blob Storage is optimized for storing massive amounts of unstructured data — text, binary, images, documents, video, backups.

### Blob Types

| Type | Use Case | Max Size |
|------|----------|----------|
| **Block blobs** | Files, documents, media | 190.7 TiB |
| **Append blobs** | Log files (append-only) | 195 GiB |
| **Page blobs** | VM disks, random read/write | 8 TiB |

For AZ-204, you'll mostly work with block blobs.

### Container and Blob Hierarchy

```
Storage Account
  └── Container (like a folder)
       ├── Blob1.jpg
       ├── Blob2.pdf
       └── folder1/Blob3.txt   (virtual directory via naming)
```

- A container holds a set of blobs
- Container names: lowercase, 3-63 characters
- Blobs can use `/` in names to simulate folders (virtual directories)

### Access Tiers

| Tier | Storage Cost | Access Cost | Use Case |
|------|-------------|-------------|----------|
| **Hot** | Higher | Lower | Frequently accessed data |
| **Cool** | Lower | Higher | Infrequently accessed (30+ days) |
| **Cold** | Even lower | Even higher | Rarely accessed (90+ days) |
| **Archive** | Lowest | Highest + rehydration time | Long-term backup (180+ days) |

- Hot and Cool can be set at the account level
- Any tier can be set at the individual blob level
- Archive blobs must be rehydrated to Hot or Cool before reading (takes hours)

### Lifecycle Management

Automate tier transitions and deletion with lifecycle policies:

```json
{
  "rules": [
    {
      "name": "move-to-cool",
      "type": "Lifecycle",
      "definition": {
        "filters": {
          "blobTypes": ["blockBlob"],
          "prefixMatch": ["logs/"]
        },
        "actions": {
          "baseBlob": {
            "tierToCool": { "daysAfterModificationGreaterThan": 30 },
            "tierToArchive": { "daysAfterModificationGreaterThan": 90 },
            "delete": { "daysAfterModificationGreaterThan": 365 }
          }
        }
      }
    }
  ]
}
```

---

## 3. Shared Access Signatures (SAS)

A SAS provides secure delegated access to resources without sharing your account key.

### SAS Types

| Type | Scope | Use Case |
|------|-------|----------|
| **Account SAS** | Entire storage account | Broad access to multiple services |
| **Service SAS** | Single service (Blob, Queue, etc.) | Access to specific service |
| **User delegation SAS** | Blob storage only, uses Entra ID | Most secure, recommended |

### SAS Components

A SAS token includes:
- **Resource** — what can be accessed
- **Permissions** — read, write, delete, list, etc.
- **Start/expiry time** — when the SAS is valid
- **IP restrictions** — optional IP range filter
- **Protocol** — HTTPS only or HTTP+HTTPS
- **Signature** — HMAC-SHA256 signed with account key

### Stored Access Policies

Instead of embedding all parameters in the SAS URL, you can create a stored access policy on the container and reference it. This lets you revoke access by modifying or deleting the policy.

---

## 4. Azure Storage SDK (.NET)

Key NuGet packages:
- `Azure.Storage.Blobs` — Blob operations
- `Azure.Storage.Queues` — Queue operations
- `Azure.Identity` — Authentication (DefaultAzureCredential)

Key classes:
- `BlobServiceClient` — account-level operations
- `BlobContainerClient` — container operations
- `BlobClient` — individual blob operations

---

## Key Takeaways

1. Use Standard general-purpose v2 for most scenarios
2. Choose redundancy based on durability and availability needs
3. Block blobs are the most common type for AZ-204
4. Access tiers optimize cost — Hot for frequent, Archive for long-term
5. Lifecycle policies automate tier transitions
6. User delegation SAS is the most secure SAS type
7. The .NET SDK uses a client hierarchy: Service → Container → Blob

---

Next: [Lab — Blob Storage](./lab-blob-storage.md)
