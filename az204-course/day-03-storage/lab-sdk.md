# Lab: Azure Storage SDK for .NET

## Title
Programmatic Blob Operations with the Azure.Storage.Blobs SDK

## Objective
- Use the .NET SDK to interact with Blob Storage
- Create containers, upload, download, and list blobs programmatically
- Use DefaultAzureCredential for authentication
- Work with blob properties, metadata, and SAS tokens in code

## Architecture

```
┌──────────────────────────────────────────────┐
│  .NET Console App                            │
│                                              │
│  BlobServiceClient                           │
│    └── BlobContainerClient ("sdk-container") │
│         ├── Upload blob                      │
│         ├── Download blob                    │
│         ├── List blobs                       │
│         ├── Set metadata                     │
│         └── Generate SAS                     │
│                                              │
│  ──── connects to ────►                      │
│                                              │
│  Azure Storage Account (az204store<unique>)  │
└──────────────────────────────────────────────┘
```

## Prerequisites
- .NET 8 SDK installed
- Azure Storage account from the previous lab (or create a new one)
- Azure CLI logged in

---

## Step 1 — Create a .NET Console Project

```bash
mkdir az204-blob-sdk
cd az204-blob-sdk
dotnet new console -n BlobStorageApp
cd BlobStorageApp
```

Add the required NuGet packages:
```bash
dotnet add package Azure.Storage.Blobs
dotnet add package Azure.Identity
```

---

## Step 2 — Write the Blob Storage Application

Replace the contents of `Program.cs` with:

```csharp
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Azure.Identity;

// --- Configuration ---
string storageAccountName = "az204store<unique>"; // Replace with your account name
string containerName = "sdk-container";

```csharp
// You can also use a connection string instead:
// string connectionString = Environment.GetEnvironmentVariable("AZURE_STORAGE_CONNECTION_STRING")!;
// var blobServiceClient = new BlobServiceClient(connectionString);

// --- Connect using DefaultAzureCredential (recommended) ---
var blobServiceClient = new BlobServiceClient(
    new Uri($"https://{storageAccountName}.blob.core.windows.net"),
    new DefaultAzureCredential());

Console.WriteLine($"Connected to storage account: {storageAccountName}");

// --- 1. Create a container ---
Console.WriteLine($"\nCreating container: {containerName}");
BlobContainerClient containerClient = await blobServiceClient
    .CreateBlobContainerAsync(containerName);
Console.WriteLine($"Container created: {containerClient.Uri}");

// --- 2. Upload a blob ---
string localFilePath = "hello.txt";
await File.WriteAllTextAsync(localFilePath, "Hello from AZ-204 SDK lab!");

BlobClient blobClient = containerClient.GetBlobClient("uploads/hello.txt");
Console.WriteLine($"\nUploading: {localFilePath} -> {blobClient.Uri}");

await blobClient.UploadAsync(localFilePath, overwrite: true);
Console.WriteLine("Upload complete.");

// --- 3. Upload with metadata and headers ---
var uploadOptions = new BlobUploadOptions
{
    HttpHeaders = new BlobHttpHeaders
    {
        ContentType = "text/plain",
        CacheControl = "max-age=3600"
    },
    Metadata = new Dictionary<string, string>
    {
        { "author", "az204-student" },
        { "purpose", "sdk-lab" }
    }
};

string secondFile = "data.txt";
await File.WriteAllTextAsync(secondFile, "This file has custom metadata.");
BlobClient blobClient2 = containerClient.GetBlobClient("uploads/data.txt");
await blobClient2.UploadAsync(secondFile, uploadOptions);
Console.WriteLine("Uploaded data.txt with metadata.");

// --- 4. List all blobs ---
Console.WriteLine("\nListing blobs:");
await foreach (BlobItem blob in containerClient.GetBlobsAsync())
{
    Console.WriteLine($"  - {blob.Name} (Size: {blob.Properties.ContentLength} bytes, Tier: {blob.Properties.AccessTier})");
}

// --- 5. Download a blob ---
string downloadPath = "downloaded-hello.txt";
Console.WriteLine($"\nDownloading: {blobClient.Name} -> {downloadPath}");

BlobDownloadInfo download = await blobClient.DownloadAsync();
using (FileStream fs = File.OpenWrite(downloadPath))
{
    await download.Content.CopyToAsync(fs);
}
Console.WriteLine($"Downloaded. Content: {await File.ReadAllTextAsync(downloadPath)}");

// --- 6. Read blob properties and metadata ---
BlobProperties properties = await blobClient2.GetPropertiesAsync();
Console.WriteLine($"\nProperties of data.txt:");
Console.WriteLine($"  Content Type: {properties.ContentType}");
Console.WriteLine($"  Content Length: {properties.ContentLength}");
Console.WriteLine($"  Last Modified: {properties.LastModified}");
Console.WriteLine($"  Metadata:");
foreach (var kvp in properties.Metadata)
{
    Console.WriteLine($"    {kvp.Key} = {kvp.Value}");
}

// --- 7. Set blob access tier ---
Console.WriteLine("\nSetting data.txt to Cool tier...");
await blobClient2.SetAccessTierAsync(AccessTier.Cool);
Console.WriteLine("Tier changed to Cool.");

// --- 8. Copy a blob ---
BlobClient copyTarget = containerClient.GetBlobClient("copies/hello-copy.txt");
Console.WriteLine($"\nCopying {blobClient.Name} -> {copyTarget.Name}");
await copyTarget.StartCopyFromUriAsync(blobClient.Uri);
Console.WriteLine("Copy started.");

// --- 9. Delete blobs and container ---
Console.WriteLine("\nCleaning up...");
await blobClient.DeleteIfExistsAsync();
await blobClient2.DeleteIfExistsAsync();
await copyTarget.DeleteIfExistsAsync();
await containerClient.DeleteIfExistsAsync();
Console.WriteLine("All resources deleted.");

// Clean up local files
File.Delete(localFilePath);
File.Delete(secondFile);
File.Delete(downloadPath);

Console.WriteLine("\nLab complete!");
```

---

## Step 3 — Assign RBAC Role (for DefaultAzureCredential)

If using `DefaultAzureCredential`, your Azure CLI identity needs the `Storage Blob Data Contributor` role:

```bash
# Get your user object ID
USER_ID=$(az ad signed-in-user show --query id --output tsv)

# Get the storage account resource ID
STORAGE_ID=$(az storage account show \
  --name az204store<unique> \
  --resource-group az204-day03-rg \
  --query id --output tsv)

# Assign the role
az role assignment create \
  --assignee $USER_ID \
  --role "Storage Blob Data Contributor" \
  --scope $STORAGE_ID
```

Wait 1-2 minutes for the role assignment to propagate.

---

## Step 4 — Run the Application

```bash
dotnet run
```

Expected output:
```
Connected to storage account: az204store<unique>

Creating container: sdk-container
Container created: https://az204store<unique>.blob.core.windows.net/sdk-container

Uploading: hello.txt -> .../uploads/hello.txt
Upload complete.
Uploaded data.txt with metadata.

Listing blobs:
  - uploads/data.txt (Size: 30 bytes, Tier: Hot)
  - uploads/hello.txt (Size: 26 bytes, Tier: Hot)

Downloading: uploads/hello.txt -> downloaded-hello.txt
Downloaded. Content: Hello from AZ-204 SDK lab!

Properties of data.txt:
  Content Type: text/plain
  Content Length: 30
  Last Modified: 2026-03-14T...
  Metadata:
    author = az204-student
    purpose = sdk-lab

Setting data.txt to Cool tier...
Tier changed to Cool.

Copying uploads/hello.txt -> copies/hello-copy.txt
Copy started.

Cleaning up...
All resources deleted.

Lab complete!
```

---

## Step 5 — Alternative: Use Connection String

If you prefer connection strings over DefaultAzureCredential, replace the client initialization:

```csharp
// Instead of DefaultAzureCredential:
string connectionString = Environment.GetEnvironmentVariable("AZURE_STORAGE_CONNECTION_STRING")!;
var blobServiceClient = new BlobServiceClient(connectionString);
```

---

## Cleanup

```bash
cd ../..
rm -rf az204-blob-sdk
```

---

## Expected Outcomes

| Task | Status |
|------|--------|
| Created .NET project with Azure SDK packages | ✅ |
| Connected to storage with DefaultAzureCredential | ✅ |
| Created container programmatically | ✅ |
| Uploaded blobs with metadata | ✅ |
| Listed blobs in container | ✅ |
| Downloaded blob to local file | ✅ |
| Read blob properties and metadata | ✅ |
| Changed blob access tier | ✅ |
| Copied a blob | ✅ |
| Cleaned up all resources | ✅ |

---

Next: [Day 04 — Azure Cosmos DB](../day-04-cosmosdb/theory.md)
