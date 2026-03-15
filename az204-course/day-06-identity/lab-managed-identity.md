# Lab: Managed Identity & Authentication

## Title
Secure App-to-Service Communication with Managed Identity

## Objective
- Create an App Service with a system-assigned managed identity
- Grant the identity access to a Key Vault and Storage Account
- Write code that uses DefaultAzureCredential to access both services
- Register an app in Entra ID and acquire tokens with MSAL

## Architecture

```
┌──────────────────────────────────────────────────────────┐
│              Resource Group: az204-day06-rg               │
│                                                          │
│  ┌──────────────────┐                                    │
│  │  App Service      │──── System-Assigned ────┐         │
│  │  az204-id-app     │     Managed Identity    │         │
│  │  (.NET 8)         │                         │         │
│  └──────────────────┘                         │         │
│                                                │         │
│         Uses DefaultAzureCredential            │         │
│                    │                           │         │
│          ┌─────────┴──────────┐                │         │
│          ▼                    ▼                ▼         │
│  ┌──────────────┐    ┌──────────────┐  ┌────────────┐   │
│  │  Key Vault   │    │   Storage    │  │ Entra ID   │   │
│  │  az204kv<u>  │    │   Account    │  │ (RBAC)     │   │
│  │              │    │              │  └────────────┘   │
│  │  Secret:     │    │  Blob:       │                   │
│  │  "MySecret"  │    │  "data.txt"  │                   │
│  └──────────────┘    └──────────────┘                   │
└──────────────────────────────────────────────────────────┘
```

## Prerequisites
- Azure CLI installed and logged in
- .NET 8 SDK installed

---

## Step 1 — Create Resources

```bash
az group create --name az204-day06-rg --location eastus

# Create Key Vault
az keyvault create \
  --name az204kv<unique> \
  --resource-group az204-day06-rg \
  --location eastus \
  --enable-rbac-authorization true

# Create Storage Account
az storage account create \
  --name az204idstore<unique> \
  --resource-group az204-day06-rg \
  --location eastus \
  --sku Standard_LRS

# Create App Service Plan and Web App
az appservice plan create \
  --name az204-id-plan \
  --resource-group az204-day06-rg \
  --sku B1 \
  --is-linux

az webapp create \
  --name az204-id-app-<unique> \
  --resource-group az204-day06-rg \
  --plan az204-id-plan \
  --runtime "DOTNETCORE:8.0"
```

---

## Step 2 — Enable System-Assigned Managed Identity

```bash
az webapp identity assign \
  --name az204-id-app-<unique> \
  --resource-group az204-day06-rg
```

Save the `principalId` from the output — you'll need it for RBAC assignments.

```bash
PRINCIPAL_ID=$(az webapp identity show \
  --name az204-id-app-<unique> \
  --resource-group az204-day06-rg \
  --query principalId \
  --output tsv)

echo "Managed Identity Principal ID: $PRINCIPAL_ID"
```

---

## Step 3 — Grant RBAC Roles to the Managed Identity

Grant access to Key Vault:
```bash
KV_ID=$(az keyvault show --name az204kv<unique> --query id --output tsv)

az role assignment create \
  --assignee $PRINCIPAL_ID \
  --role "Key Vault Secrets User" \
  --scope $KV_ID
```

Grant access to Storage:
```bash
STORAGE_ID=$(az storage account show \
  --name az204idstore<unique> \
  --resource-group az204-day06-rg \
  --query id --output tsv)

az role assignment create \
  --assignee $PRINCIPAL_ID \
  --role "Storage Blob Data Contributor" \
  --scope $STORAGE_ID
```

---

## Step 4 — Add a Secret to Key Vault and a Blob to Storage

```bash
# Add secret
az keyvault secret set \
  --vault-name az204kv<unique> \
  --name MySecret \
  --value "SuperSecretValue-from-KeyVault"

# Create blob container and upload a file
az storage container create \
  --name data \
  --account-name az204idstore<unique> \
  --auth-mode login

echo "Hello from managed identity lab!" > testdata.txt

az storage blob upload \
  --container-name data \
  --name data.txt \
  --file testdata.txt \
  --account-name az204idstore<unique> \
  --auth-mode login
```

---

## Step 5 — Create the .NET Application

```bash
mkdir az204-identity-app
cd az204-identity-app
dotnet new web -n IdentityApp
cd IdentityApp
dotnet add package Azure.Identity
dotnet add package Azure.Security.KeyVault.Secrets
dotnet add package Azure.Storage.Blobs
```

Replace `Program.cs`:

```csharp
using Azure.Identity;
using Azure.Security.KeyVault.Secrets;
using Azure.Storage.Blobs;

var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();

var credential = new DefaultAzureCredential();

app.MapGet("/", () => "Managed Identity Lab - use /secret and /blob endpoints");

app.MapGet("/secret", async () =>
{
    var client = new SecretClient(
        new Uri("https://az204kv<unique>.vault.azure.net/"),
        credential);

    KeyVaultSecret secret = await client.GetSecretAsync("MySecret");
    return Results.Ok(new
    {
        secretName = secret.Name,
        secretValue = secret.Value,
        message = "Retrieved from Key Vault using Managed Identity!"
    });
});

app.MapGet("/blob", async () =>
{
    var blobServiceClient = new BlobServiceClient(
        new Uri("https://az204idstore<unique>.blob.core.windows.net"),
        credential);

    var containerClient = blobServiceClient.GetBlobContainerClient("data");
    var blobClient = containerClient.GetBlobClient("data.txt");

    var download = await blobClient.DownloadContentAsync();
    string content = download.Value.Content.ToString();

    return Results.Ok(new
    {
        blobName = "data.txt",
        content = content,
        message = "Retrieved from Blob Storage using Managed Identity!"
    });
});

app.Run();
```

---

## Step 6 — Test Locally

Locally, `DefaultAzureCredential` uses your Azure CLI credentials.

First, grant yourself the same roles:
```bash
USER_ID=$(az ad signed-in-user show --query id --output tsv)

az role assignment create --assignee $USER_ID --role "Key Vault Secrets User" --scope $KV_ID
az role assignment create --assignee $USER_ID --role "Storage Blob Data Contributor" --scope $STORAGE_ID
```

Run the app:
```bash
dotnet run
```

Test endpoints:
```bash
curl http://localhost:5000/secret
curl http://localhost:5000/blob
```

---

## Step 7 — Deploy to App Service

```bash
dotnet publish -c Release -o ./publish

cd publish
zip -r ../deploy.zip .
cd ..

az webapp deployment source config-zip \
  --name az204-id-app-<unique> \
  --resource-group az204-day06-rg \
  --src deploy.zip
```

Test in Azure:
```bash
curl https://az204-id-app-<unique>.azurewebsites.net/secret
curl https://az204-id-app-<unique>.azurewebsites.net/blob
```

Both endpoints should return data — no connection strings or secrets in your code.

---

## Step 8 — Register an App and Use MSAL (Client Credentials)

Register an app in Entra ID:
```bash
APP_REG=$(az ad app create \
  --display-name "az204-msal-demo" \
  --query appId \
  --output tsv)

echo "Application ID: $APP_REG"

# Create a client secret
az ad app credential reset \
  --id $APP_REG \
  --display-name "lab-secret" \
  --years 1
```

Save the `appId` and `password` from the output.

Get your tenant ID:
```bash
TENANT_ID=$(az account show --query tenantId --output tsv)
echo "Tenant ID: $TENANT_ID"
```

Create a simple MSAL console app:
```bash
mkdir ../msal-demo
cd ../msal-demo
dotnet new console -n MsalDemo
cd MsalDemo
dotnet add package Microsoft.Identity.Client
```

Replace `Program.cs`:

```csharp
using Microsoft.Identity.Client;

string clientId = "<your-app-id>";
string clientSecret = "<your-client-secret>";
string tenantId = "<your-tenant-id>";
string authority = $"https://login.microsoftonline.com/{tenantId}";

// Build confidential client (for service-to-service)
var app = ConfidentialClientApplicationBuilder
    .Create(clientId)
    .WithClientSecret(clientSecret)
    .WithAuthority(authority)
    .Build();

// Acquire token for Microsoft Graph
string[] scopes = { "https://graph.microsoft.com/.default" };

var result = await app.AcquireTokenForClient(scopes).ExecuteAsync();

Console.WriteLine($"Token acquired!");
Console.WriteLine($"Token type: {result.TokenType}");
Console.WriteLine($"Expires on: {result.ExpiresOn}");
Console.WriteLine($"Scopes: {string.Join(", ", result.Scopes)}");
Console.WriteLine($"Token (first 50 chars): {result.AccessToken[..50]}...");
```

Run it:
```bash
dotnet run
```

Expected output:
```
Token acquired!
Token type: Bearer
Expires on: 3/14/2026 ...
Scopes: https://graph.microsoft.com/.default
Token (first 50 chars): eyJ0eXAiOiJKV1QiLCJhbGciOiJSUzI1NiIsIng1dCI6...
```

---

## Cleanup

```bash
# Delete app registration
az ad app delete --id $APP_REG

# Delete local files
cd ../..
rm -rf az204-identity-app msal-demo testdata.txt

# Delete Azure resources
az group delete --name az204-day06-rg --yes --no-wait
```

---

## Expected Outcomes

| Task | Status |
|------|--------|
| Created App Service with system-assigned managed identity | ✅ |
| Granted RBAC roles to managed identity | ✅ |
| Accessed Key Vault secret via managed identity | ✅ |
| Accessed Blob Storage via managed identity | ✅ |
| Deployed app to Azure — no secrets in code | ✅ |
| Registered app in Entra ID | ✅ |
| Acquired token with MSAL client credentials flow | ✅ |

---

Next: [Day 07 — Event-Based Solutions](../day-07-event-based/theory.md)
