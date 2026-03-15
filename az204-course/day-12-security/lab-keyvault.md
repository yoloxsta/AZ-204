# Lab: Azure Key Vault & App Configuration

## Title
Manage Secrets and Configuration Securely

## Objective
- Create a Key Vault and manage secrets, keys, and certificates
- Access secrets from code using the SDK
- Create an App Configuration store with feature flags
- Use Key Vault references in App Configuration

## Architecture

```
┌──────────────────────────────────────────────────────────┐
│              Resource Group: az204-day12-rg               │
│                                                          │
│  ┌──────────────────┐                                    │
│  │  .NET Web App     │                                    │
│  │                   │──► App Configuration               │
│  │                   │    ├── App:Color = Blue            │
│  │                   │    ├── App:MaxItems = 100          │
│  │                   │    ├── App:DbConn = (KV ref) ─┐   │
│  │                   │    └── Feature: BetaFeature    │   │
│  │                   │                                │   │
│  │                   │──► Key Vault ◄─────────────────┘   │
│  │                   │    ├── Secret: DbConnection        │
│  │                   │    ├── Secret: ApiKey              │
│  │                   │    └── Key: EncryptionKey          │
│  └──────────────────┘                                    │
└──────────────────────────────────────────────────────────┘
```

## Prerequisites
- Azure CLI installed and logged in
- .NET 8 SDK installed

---

## Step 1 — Create Resources

```bash
az group create --name az204-day12-rg --location eastus

# Create Key Vault with RBAC
az keyvault create \
  --name az204kv<unique> \
  --resource-group az204-day12-rg \
  --location eastus \
  --enable-rbac-authorization true

# Create App Configuration
az appconfig create \
  --name az204appconfig<unique> \
  --resource-group az204-day12-rg \
  --location eastus \
  --sku Free
```

Grant yourself Key Vault access:
```bash
USER_ID=$(az ad signed-in-user show --query id --output tsv)
KV_ID=$(az keyvault show --name az204kv<unique> --query id --output tsv)

az role assignment create \
  --assignee $USER_ID \
  --role "Key Vault Administrator" \
  --scope $KV_ID
```

---

## Step 2 — Manage Secrets in Key Vault

Create secrets:
```bash
az keyvault secret set \
  --vault-name az204kv<unique> \
  --name DbConnection \
  --value "Server=myserver.database.windows.net;Database=mydb;User=admin;Password=S3cur3P@ss"

az keyvault secret set \
  --vault-name az204kv<unique> \
  --name ApiKey \
  --value "sk-abc123def456ghi789"

az keyvault secret set \
  --vault-name az204kv<unique> \
  --name StorageKey \
  --value "DefaultEndpointsProtocol=https;AccountName=myaccount;AccountKey=abc123=="
```

List secrets:
```bash
az keyvault secret list \
  --vault-name az204kv<unique> \
  --output table
```

Get a secret value:
```bash
az keyvault secret show \
  --vault-name az204kv<unique> \
  --name DbConnection \
  --query value \
  --output tsv
```

Update a secret (creates a new version):
```bash
az keyvault secret set \
  --vault-name az204kv<unique> \
  --name ApiKey \
  --value "sk-newkey-xyz789abc123"
```

List secret versions:
```bash
az keyvault secret list-versions \
  --vault-name az204kv<unique> \
  --name ApiKey \
  --output table
```

---

## Step 3 — Create a Key for Encryption

```bash
az keyvault key create \
  --vault-name az204kv<unique> \
  --name EncryptionKey \
  --kty RSA \
  --size 2048
```

List keys:
```bash
az keyvault key list \
  --vault-name az204kv<unique> \
  --output table
```

---

## Step 4 — Configure App Configuration

Add key-value pairs:
```bash
az appconfig kv set \
  --name az204appconfig<unique> \
  --key "App:Settings:Color" \
  --value "Blue" \
  --yes

az appconfig kv set \
  --name az204appconfig<unique> \
  --key "App:Settings:MaxItems" \
  --value "100" \
  --yes

az appconfig kv set \
  --name az204appconfig<unique> \
  --key "App:Settings:Environment" \
  --value "Production" \
  --label "Production" \
  --yes

az appconfig kv set \
  --name az204appconfig<unique> \
  --key "App:Settings:Environment" \
  --value "Staging" \
  --label "Staging" \
  --yes
```

Add a Key Vault reference:
```bash
SECRET_URI=$(az keyvault secret show \
  --vault-name az204kv<unique> \
  --name DbConnection \
  --query id --output tsv)

az appconfig kv set-keyvault \
  --name az204appconfig<unique> \
  --key "App:Settings:DbConnection" \
  --secret-identifier $SECRET_URI \
  --yes
```

Create a feature flag:
```bash
az appconfig feature set \
  --name az204appconfig<unique> \
  --feature BetaFeature \
  --yes

az appconfig feature enable \
  --name az204appconfig<unique> \
  --feature BetaFeature \
  --yes
```

List all configuration:
```bash
az appconfig kv list \
  --name az204appconfig<unique> \
  --output table
```

---

## Step 5 — Create the .NET Application

```bash
mkdir az204-security-lab
cd az204-security-lab
dotnet new web -n SecurityApp
cd SecurityApp
dotnet add package Azure.Identity
dotnet add package Azure.Security.KeyVault.Secrets
dotnet add package Azure.Security.KeyVault.Keys
dotnet add package Microsoft.Azure.AppConfiguration.AspNetCore
dotnet add package Microsoft.FeatureManagement.AspNetCore
```

Replace `Program.cs`:

```csharp
using Azure.Identity;
using Azure.Security.KeyVault.Secrets;
using Microsoft.FeatureManagement;

var builder = WebApplication.CreateBuilder(args);

var credential = new DefaultAzureCredential();

// Add App Configuration
builder.Configuration.AddAzureAppConfiguration(options =>
{
    options.Connect(new Uri("https://az204appconfig<unique>.azconfig.io"), credential)
        .Select("App:Settings:*")
        .Select("App:Settings:*", "Production")
        .ConfigureKeyVault(kv => kv.SetCredential(credential))
        .UseFeatureFlags();
});

builder.Services.AddFeatureManagement();
var app = builder.Build();

app.MapGet("/", () => "Key Vault & App Config Lab");

// Read from App Configuration
app.MapGet("/config", (IConfiguration config) =>
{
    return Results.Ok(new
    {
        color = config["App:Settings:Color"],
        maxItems = config["App:Settings:MaxItems"],
        environment = config["App:Settings:Environment"],
        dbConnection = config["App:Settings:DbConnection"] // Resolved from Key Vault!
    });
});

// Read directly from Key Vault
app.MapGet("/secrets", async () =>
{
    var client = new SecretClient(
        new Uri("https://az204kv<unique>.vault.azure.net/"),
        credential);

    var dbConn = await client.GetSecretAsync("DbConnection");
    var apiKey = await client.GetSecretAsync("ApiKey");

    return Results.Ok(new
    {
        dbConnection = new { name = dbConn.Value.Name, version = dbConn.Value.Properties.Version },
        apiKey = new { name = apiKey.Value.Name, version = apiKey.Value.Properties.Version },
        message = "Secret values hidden for security — check Key Vault for values"
    });
});

// Feature flag check
app.MapGet("/features", async (IFeatureManager featureManager) =>
{
    bool betaEnabled = await featureManager.IsEnabledAsync("BetaFeature");
    return Results.Ok(new
    {
        betaFeature = betaEnabled,
        message = betaEnabled ? "Beta features are ON" : "Beta features are OFF"
    });
});

app.Run();
```

---

## Step 6 — Test the Application

```bash
dotnet run
```

```bash
curl http://localhost:5000/config
curl http://localhost:5000/secrets
curl http://localhost:5000/features
```

The `/config` endpoint shows settings from App Configuration, with the database connection string transparently resolved from Key Vault.

---

## Step 7 — Toggle Feature Flag

Disable the feature flag:
```bash
az appconfig feature disable \
  --name az204appconfig<unique> \
  --feature BetaFeature \
  --yes
```

Restart the app and check `/features` again — it should now show "Beta features are OFF".

---

## Step 8 — Soft-Delete and Purge Protection

Key Vault has soft-delete enabled by default (90-day retention):
```bash
# Delete a secret (soft-delete)
az keyvault secret delete \
  --vault-name az204kv<unique> \
  --name StorageKey

# List deleted secrets
az keyvault secret list-deleted \
  --vault-name az204kv<unique> \
  --output table

# Recover a deleted secret
az keyvault secret recover \
  --vault-name az204kv<unique> \
  --name StorageKey
```

---

## Cleanup

```bash
cd ../..
rm -rf az204-security-lab

az group delete --name az204-day12-rg --yes --no-wait
```

---

## Expected Outcomes

| Task | Status |
|------|--------|
| Created Key Vault with RBAC | ✅ |
| Managed secrets (create, read, update, version) | ✅ |
| Created encryption key | ✅ |
| Created App Configuration with key-values | ✅ |
| Added Key Vault reference in App Configuration | ✅ |
| Created and toggled feature flags | ✅ |
| Built .NET app reading from both services | ✅ |
| Demonstrated soft-delete and recovery | ✅ |

---

Next: [Day 13 — CI/CD](../day-13-ci-cd/theory.md)
