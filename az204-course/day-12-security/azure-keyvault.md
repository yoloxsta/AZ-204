# Azure Key Vault - အပြည့်စုံလမ်းညွှန်

## 📌 Azure Key Vault ဆိုတာဘာလဲ။ (What is Azure Key Vault?)

Azure Key Vault ဆိုတာ Microsoft Azure ရဲ့ cloud service တစ်ခုဖြစ်ပြီး အောက်ပါအရာများကို လုံခြုံစွာသိမ်းဆည်းနိုင်ပါတယ် -

- **Secrets** - Passwords, API keys, connection strings
- **Keys** - Encryption keys (RSA, EC)
- **Certificates** - SSL/TLS certificates
- **Storage Account Keys** - Azure Storage access keys

### Key Vault ရဲ့အဓိက Features များ

| Feature | ရှင်းလင်းချက် |
|---------|--------------|
| **Centralized Secret Management** | Secrets အားလုံးကို နေရာတစ်ခုတည်းမှာ စီမံခန့်ခွဲနိုင် |
| **Access Control** | RBAC နှင့် Access Policies ဖြင့် ခွင့်ပြုချက်ထိန်းချုပ် |
| **Secret Versioning** | Secret တစ်ခုချင်းစီအတွက် version များသိမ်းဆည်း |
| **Audit Logging** | မည်သူမည်သည့်အခါ အသုံးပြုခဲ့ကြောင်း log မှတ်တမ်း |
| **HSM Protection** | Hardware Security Module ဖြင့် ထပ်မံကာကွယ် |
| **Auto-Renewal** | Certificates များအတွက် အလိုအလျောက် သက်တမ်းတိုး |

---

## 🎯 Key Vault ကိုဘာလို့သုံးသင့်သလဲ။ (Why use Azure Key Vault?)

### ၁. လုံခြုံရေးဆိုင်ရာအကျိုးကျေးဇူးများ

```
❌ မကောင်းတဲ့အလေ့အကျင့်များ:
   - Code ထဲမှာ password တိုက်ရိုက်ရေးသားခြင်း (Hardcoded)
   - Config file များထဲမှာ secrets သိမ်းခြင်း
   - Git repository ထဲမှာ secrets commit လုပ်ခြင်း

✅ Key Vault အသုံးပြုခြင်း:
   - Secrets များကို လုံခြုံစွာ centralized လုပ်
   - Application code နှင့် secrets ကို ခွဲထုတ်
   - Automatic rotation ဖြင့် လုံခြုံရေးတိုးတက်
```

### ၂. လုပ်ငန်းဆိုင်ရာအကျိုးကျေးဇူးများ

- **Compliance** - SOC, ISO, HIPAA, GDPR စံနှုန်းများကို လိုက်နာနိုင်
- **Cost Reduction** - HSM infrastructure မလိုအပ်တော့
- **Developer Productivity** - Secrets management အတွက် အချိန်သက်သာ
- **Risk Mitigation** - Secret leakage အန္တရာယ်လျော့နည်း

### ၃. အသုံးပြုသင့်တဲ့အခြေအနေများ

| အသုံးပြုသင့်တဲ့အခြေအနေ | ဥပမာ |
|----------------------|--------|
| Database Connection Strings | SQL Database, PostgreSQL connection strings |
| API Keys | Third-party API keys (Stripe, SendGrid) |
| Storage Keys | Azure Storage Account access keys |
| Encryption Keys | Data encryption/decryption keys |
| SSL/TLS Certificates | Web app HTTPS certificates |
| Service Principal Credentials | Azure AD service principal secrets |

---

## 🛠️ Key Vault မိတ်ဆက်နှင့်အသုံးပြုပုံ (How to use Azure Key Vault?)

### အဆင့် ၁: Azure Portal မှာ Key Vault ဖန်တီးခြင်း

#### Step 1.1: Azure Portal သို့ဝင်ရောက်

```
1. https://portal.azure.com သို့ဝင်ရောက်ပါ
2. Search box မှာ "Key vaults" ရိုက်ရှာပါ
3. "Create" button ကိုနှိပ်ပါ
```

#### Step 1.2: Basic Settings များဖြည့်သွင်း

```yaml
Subscription: Your-Azure-Subscription
Resource Group: 
  - Create new: rg-keyvault-demo
  - သို့မဟုတ် existing resource group ရွေးပါ

Key vault name: kv-myapp-prod-001
  # အမည်သည် globally unique ဖြစ်ရမည်
  # 3-24 characters, alphanumeric နှင့် hyphens only

Region: Southeast Asia (သို့မဟုတ် သင့်နှစ်သက်ရာ region)
Pricing tier: 
  - Standard: Microsoft-managed HSM
  - Premium: HSM-backed keys (additional cost)
```

#### Step 1.3: Access Configuration

```yaml
Access configuration:
  # Option A: Vault access policy (Traditional)
  Vault access policy: Enabled
  
  # Option B: Azure role-based access control (Recommended)
  Azure role-based access control: Enabled

# Modern applications အတွက် RBAC ကို recommend လုပ်ပါတယ်
```

#### Step 1.4: Networking Settings

```yaml
Network access:
  - Allow public access from all networks: Development အတွက်
  - Allow public access from specific virtual networks: Production အတွက်
  - Disable public access: High security environments

Private endpoints:
  - Create private endpoint: Yes (Recommended for production)
```


### အဆင့် ၂: Azure CLI ဖြင့် Key Vault ဖန်တီးခြင်း

```bash
# Variables များသတ်မှတ်
$resourceGroup = "rg-keyvault-demo"
$location = "southeastasia"
$keyVaultName = "kv-myapp-prod-001"

# Resource Group ဖန်တီး
az group create --name $resourceGroup --location $location

# Key Vault ဖန်တီး
az keyvault create `
    --name $keyVaultName `
    --resource-group $resourceGroup `
    --location $location `
    --sku standard `
    --enable-rbac-authorization true

# Key Vault ID ရယူ
$keyVaultId = az keyvault show --name $keyVaultName --query id -o tsv
Write-Host "Key Vault ID: $keyVaultId"
```

### အဆင့် ၃: Secrets များသိမ်းဆည်းခြင်း

#### Portal မှာ Secret သိမ်းခြင်း

```
1. Key Vault > Secrets > Generate/Import
2. Upload options: Manual
3. Name: DatabaseConnectionString
4. Value: Server=tcp:myserver.database.windows.net,1433;Database=mydb;...
5. Content type: application/json (optional)
6. Activation date: (optional)
7. Expiration date: (optional)
8. Create နှိပ်ပါ
```

#### CLI ဖြင့် Secret သိမ်းခြင်း

```bash
# Database Connection String သိမ်းခြင်း
az keyvault secret set `
    --vault-name kv-myapp-prod-001 `
    --name DatabaseConnectionString `
    --value "Server=tcp:myserver.database.windows.net,1433;Database=mydb;User ID=admin;Password=P@ssw0rd123;"

# API Key သိမ်းခြင်း
az keyvault secret set `
    --vault-name kv-myapp-prod-001 `
    --name StripeApiKey `
    --value "sk_live_51H7h3K..."

# JSON format ဖြင့်သိမ်းခြင်း
az keyvault secret set `
    --vault-name kv-myapp-prod-001 `
    --name AppSettings `
    --value '{"ApiKey":"abc123","ApiSecret":"xyz789","Environment":"Production"}'
```

### အဆင့် ၄: Keys များဖန်တီးခြင်း

```bash
# RSA Key ဖန်တီး
az keyvault key create `
    --vault-name kv-myapp-prod-001 `
    --name MyEncryptionKey `
    --kty RSA `
    --size 2048

# EC Key ဖန်တီး (Elliptic Curve)
az keyvault key create `
    --vault-name kv-myapp-prod-001 `
    --name MySigningKey `
    --kty EC `
    --curve P-256

# Key အသုံးပြုပြီး encrypt/decrypt
$encrypted = az keyvault key encrypt `
    --vault-name kv-myapp-prod-001 `
    --name MyEncryptionKey `
    --value "SGVsbG8gV29ybGQ=" `
    --algorithm RSA-OAEP

Write-Host "Encrypted: $encrypted"
```

### အဆင့် ၅: Certificates များစီမံခန့်ခွဲခြင်း

```bash
# Self-signed certificate ဖန်တီး
az keyvault certificate create `
    --vault-name kv-myapp-prod-001 `
    --name MySSLCertificate `
    --policy "$(az keyvault certificate get-default-policy -o json)"

# Certificate အချက်အလက်ကြည့်ခြင်း
az keyvault certificate show `
    --vault-name kv-myapp-prod-001 `
    --name MySSLCertificate

# Certificate export
az keyvault certificate download `
    --vault-name kv-myapp-prod-001 `
    --name MySSLCertificate `
    --file mycert.pfx
```

---

## 👤 Access Control စီမံခန့်ခွဲခြင်း

### Method 1: RBAC (Role-Based Access Control) - Recommended

```bash
# Current user ကို Key Vault Secrets Officer role ပေးခြင်း
$currentUser = az ad signed-in-user show --query id -o tsv
$keyVaultId = az keyvault show --name kv-myapp-prod-001 --query id -o tsv

az role assignment create `
    --assignee $currentUser `
    --role "Key Vault Secrets Officer" `
    --scope $keyVaultId

# Service Principal အတွက် role ပေးခြင်း
$spId = "xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx"

az role assignment create `
    --assignee $spId `
    --role "Key Vault Secrets User" `
    --scope $keyVaultId
```

#### Key Vault RBAC Roles များ

| Role | ခွင့်ပြုချက်များ |
|------|----------------|
| **Key Vault Administrator** | Full access to all operations |
| **Key Vault Secrets Officer** | Create, read, update, delete secrets |
| **Key Vault Secrets User** | Read secrets only |
| **Key Vault Certificates Officer** | Manage certificates |
| **Key Vault Crypto Officer** | Manage keys and crypto operations |
| **Key Vault Crypto User** | Encrypt/decrypt operations |

### Method 2: Access Policies (Traditional)

```bash
# Access policy ပေးခြင်း
az keyvault set-policy `
    --name kv-myapp-prod-001 `
    --object-id $currentUser `
    --secret-permissions get list set delete backup restore recover purge `
    --key-permissions get list create delete encrypt decrypt wrapKey unwrapKey `
    --certificate-permissions get list create delete import

# Service Principal အတွက် access policy
az keyvault set-policy `
    --name kv-myapp-prod-001 `
    --spn "myapp-service-principal" `
    --secret-permissions get list
```


---

## 💻 Application Code မှ Key Vault အသုံးပြုခြင်း

### Prerequisites

```bash
# NuGet packages များ install
dotnet add package Azure.Identity
dotnet add package Azure.Security.KeyVault.Secrets
```

### Example 1: .NET Core Console Application

```csharp
// Program.cs
using Azure.Identity;
using Azure.Security.KeyVault.Secrets;
using System;

class Program
{
    static async Task Main(string[] args)
    {
        // Key Vault configuration
        string keyVaultUrl = "https://kv-myapp-prod-001.vault.azure.net/";
        
        // Create SecretClient using DefaultAzureCredential
        // This automatically tries multiple authentication methods:
        // 1. Environment variables
        // 2. Managed Identity
        // 3. Visual Studio
        // 4. Azure CLI
        // 5. Interactive browser
        var client = new SecretClient(
            new Uri(keyVaultUrl), 
            new DefaultAzureCredential()
        );

        try
        {
            // Get a secret
            KeyVaultSecret secret = await client.GetSecretAsync("DatabaseConnectionString");
            string connectionString = secret.Value;
            Console.WriteLine($"Connection String retrieved successfully!");
            Console.WriteLine($"Secret Version: {secret.Properties.Version}");
            Console.WriteLine($"Updated: {secret.Properties.UpdatedOn}");

            // Set a new secret
            await client.SetSecretAsync("NewApiKey", "my-api-key-12345");
            Console.WriteLine("New secret created successfully!");

            // List all secrets
            await foreach (SecretProperties secretProps in client.GetPropertiesOfSecretsAsync())
            {
                Console.WriteLine($"Secret: {secretProps.Name}");
                Console.WriteLine($"  Created: {secretProps.CreatedOn}");
                Console.WriteLine($"  Updated: {secretProps.UpdatedOn}");
                if (secretProps.ExpiresOn.HasValue)
                {
                    Console.WriteLine($"  Expires: {secretProps.ExpiresOn}");
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error: {ex.Message}");
        }
    }
}
```

### Example 2: ASP.NET Core Web API

```csharp
// appsettings.json
{
  "KeyVault": {
    "VaultUrl": "https://kv-myapp-prod-001.vault.azure.net/"
  }
}

// Program.cs
using Azure.Identity;
using Azure.Security.KeyVault.Secrets;

var builder = WebApplication.CreateBuilder(args);

// Add Key Vault Secret Client as singleton
string keyVaultUrl = builder.Configuration["KeyVault:VaultUrl"] 
    ?? throw new InvalidOperationException("KeyVault:VaultUrl not found");

builder.Services.AddSingleton<SecretClient>(sp =>
{
    return new SecretClient(
        new Uri(keyVaultUrl),
        new DefaultAzureCredential()
    );
});

// Add a service that uses Key Vault
builder.Services.AddScoped<ISecretService, KeyVaultSecretService>();

var app = builder.Build();

// Minimal API endpoint
app.MapGet("/api/config/{secretName}", async (
    string secretName, 
    SecretClient secretClient) =>
{
    try
    {
        var secret = await secretClient.GetSecretAsync(secretName);
        return Results.Ok(new { name = secretName, exists = true });
    }
    catch (Azure.RequestFailedException ex) when (ex.Status == 404)
    {
        return Results.NotFound(new { name = secretName, exists = false });
    }
});

app.Run();

// Secret Service Implementation
public interface ISecretService
{
    Task<string> GetSecretAsync(string secretName);
    Task SetSecretAsync(string secretName, string value);
}

public class KeyVaultSecretService : ISecretService
{
    private readonly SecretClient _secretClient;
    private readonly ILogger<KeyVaultSecretService> _logger;

    public KeyVaultSecretService(
        SecretClient secretClient,
        ILogger<KeyVaultSecretService> logger)
    {
        _secretClient = secretClient;
        _logger = logger;
    }

    public async Task<string> GetSecretAsync(string secretName)
    {
        try
        {
            KeyVaultSecret secret = await _secretClient.GetSecretAsync(secretName);
            _logger.LogInformation("Successfully retrieved secret: {SecretName}", secretName);
            return secret.Value;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to retrieve secret: {SecretName}", secretName);
            throw;
        }
    }

    public async Task SetSecretAsync(string secretName, string value)
    {
        try
        {
            await _secretClient.SetSecretAsync(secretName, value);
            _logger.LogInformation("Successfully set secret: {SecretName}", secretName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to set secret: {SecretName}", secretName);
            throw;
        }
    }
}
```


### Example 3: Configuration Provider (Automatic Secret Loading)

```csharp
// Program.cs - Using Key Vault as Configuration Source
using Azure.Identity;

var builder = WebApplication.CreateBuilder(args);

// Add Key Vault to configuration
string keyVaultUrl = builder.Configuration["KeyVault:VaultUrl"]!;

builder.Configuration.AddAzureKeyVault(
    new Uri(keyVaultUrl),
    new DefaultAzureCredential()
);

// Now secrets can be accessed like regular configuration
// Example: If Key Vault has secret "DatabaseConnectionString"
// It can be accessed as builder.Configuration["DatabaseConnectionString"]

var app = builder.Build();

app.MapGet("/api/database-status", (IConfiguration config) =>
{
    string? connectionString = config["DatabaseConnectionString"];
    bool hasConnectionString = !string.IsNullOrEmpty(connectionString);
    return Results.Ok(new { configured = hasConnectionString });
});

app.Run();
```

### Example 4: Caching Secrets for Performance

```csharp
// CachedSecretService.cs
using Azure.Identity;
using Azure.Security.KeyVault.Secrets;
using Microsoft.Extensions.Caching.Memory;

public class CachedSecretService
{
    private readonly SecretClient _secretClient;
    private readonly IMemoryCache _cache;
    private readonly TimeSpan _cacheDuration = TimeSpan.FromMinutes(30);

    public CachedSecretService(string keyVaultUrl, IMemoryCache cache)
    {
        _secretClient = new SecretClient(
            new Uri(keyVaultUrl),
            new DefaultAzureCredential()
        );
        _cache = cache;
    }

    public async Task<string> GetSecretAsync(string secretName)
    {
        string cacheKey = $"secret-{secretName}";

        // Try to get from cache first
        if (_cache.TryGetValue(cacheKey, out string? cachedValue) && cachedValue != null)
        {
            return cachedValue;
        }

        // Get from Key Vault
        KeyVaultSecret secret = await _secretClient.GetSecretAsync(secretName);

        // Cache the value
        var cacheOptions = new MemoryCacheEntryOptions()
            .SetAbsoluteExpiration(_cacheDuration)
            .SetSlidingExpiration(TimeSpan.FromMinutes(10));

        _cache.Set(cacheKey, secret.Value, cacheOptions);

        return secret.Value;
    }

    public async Task RefreshSecretAsync(string secretName)
    {
        string cacheKey = $"secret-{secretName}";
        _cache.Remove(cacheKey);
        await GetSecretAsync(secretName);
    }
}
```

---

## 🐳 Docker Container မှ Key Vault အသုံးပြုခြင်း

### Method 1: Service Principal Authentication

#### Step 1: Service Principal ဖန်တီးခြင်း

```bash
# Service Principal ဖန်တီး
$sp = az ad sp create-for-rbac `
    --name "myapp-keyvault-sp" `
    --role "Key Vault Secrets User" `
    --scopes "/subscriptions/<subscription-id>/resourceGroups/rg-keyvault-demo/providers/Microsoft.KeyVault/vaults/kv-myapp-prod-001"

# Output မှ values များသိမ်းဆည်း
$tenantId = $sp.tenant
$clientId = $sp.appId
$clientSecret = $sp.password
```

#### Step 2: Dockerfile

```dockerfile
# Dockerfile
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 80
EXPOSE 443

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY ["MyKeyVaultApp.csproj", "./"]
RUN dotnet restore "MyKeyVaultApp.csproj"
COPY . .
RUN dotnet publish "MyKeyVaultApp.csproj" -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "MyKeyVaultApp.dll"]
```

#### Step 3: Docker Compose with Environment Variables

```yaml
# docker-compose.yml
version: '3.8'

services:
  myapp:
    build:
      context: .
      dockerfile: Dockerfile
    container_name: keyvault-demo-app
    ports:
      - "8080:80"
    environment:
      # Azure Service Principal credentials
      - AZURE_TENANT_ID=${AZURE_TENANT_ID}
      - AZURE_CLIENT_ID=${AZURE_CLIENT_ID}
      - AZURE_CLIENT_SECRET=${AZURE_CLIENT_SECRET}
      # Application settings
      - KeyVault__VaultUrl=https://kv-myapp-prod-001.vault.azure.net/
      - ASPNETCORE_ENVIRONMENT=Production
    restart: unless-stopped
    networks:
      - app-network

networks:
  app-network:
    driver: bridge
```

#### Step 4: Environment File (.env)

```bash
# .env - DO NOT commit to source control!
AZURE_TENANT_ID=xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx
AZURE_CLIENT_ID=xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx
AZURE_CLIENT_SECRET=your-client-secret-here
```

#### Step 5: Run Docker Container

```bash
# Build and run
docker-compose up -d --build

# Check logs
docker-compose logs -f myapp

# Test the application
curl http://localhost:8080/api/config/DatabaseConnectionString
```


### Method 2: Managed Identity (Azure Container Instances)

```yaml
# azure-container-instance.yaml
apiVersion: 2019-12-01
location: southeastasia
name: keyvault-demo-aci
properties:
  containers:
    - name: myapp
      image: myregistry.azurecr.io/myapp:latest
      ports:
        - port: 80
      environmentVariables:
        - name: KeyVault__VaultUrl
          value: https://kv-myapp-prod-001.vault.azure.net/
        - name: ASPNETCORE_ENVIRONMENT
          value: Production
      resources:
        requests:
          cpu: 1
          memoryInGB: 1
  osType: Linux
  ipAddress:
    type: Public
    ports:
      - protocol: TCP
        port: 80
  identity:
    type: SystemAssigned
type: Microsoft.ContainerInstance/containerGroups
```

```bash
# Deploy with Managed Identity
az container create --resource-group rg-keyvault-demo --file azure-container-instance.yaml

# Get container identity principal ID
$principalId = az container show `
    --resource-group rg-keyvault-demo `
    --name keyvault-demo-aci `
    --query identity.principalId -o tsv

# Grant Key Vault access to container identity
az role assignment create `
    --assignee $principalId `
    --role "Key Vault Secrets User" `
    --scope "/subscriptions/<sub-id>/resourceGroups/rg-keyvault-demo/providers/Microsoft.KeyVault/vaults/kv-myapp-prod-001"
```

---

## ☸️ Kubernetes မှ Key Vault အသုံးပြုခြင်း

### Method 1: Azure Key Vault Provider for Secrets Store CSI Driver

#### Step 1: AKS Cluster တွင် Add-on Enable လုပ်ခြင်း

```bash
# AKS cluster တွင် Secrets Store CSI Driver enable
az aks enable-addons `
    --addons azure-keyvault-secrets-provider `
    --name myAKSCluster `
    --resource-group rg-keyvault-demo

# Add-on installation ကို confirm လုပ်ခြင်း
kubectl get pods -n kube-system -l 'app in (secrets-store-csi-driver, secrets-store-provider-azure)'
```

#### Step 2: Managed Identity Setup

```bash
# AKS cluster ရဲ MSI get
$aksIdentity = az aks show `
    --resource-group rg-keyvault-demo `
    --name myAKSCluster `
    --query identityProfile.kubeletidentity.objectId -o tsv

# Grant Key Vault access to AKS identity
az role assignment create `
    --assignee $aksIdentity `
    --role "Key Vault Secrets User" `
    --scope "/subscriptions/<sub-id>/resourceGroups/rg-keyvault-demo/providers/Microsoft.KeyVault/vaults/kv-myapp-prod-001"
```

#### Step 3: SecretProviderClass Definition

> ⚠️ **အရေးကြီး:** SecretProviderClass နှစ်မျိုးရှိပါတယ်။ **တစ်ခုတည်း** ရွေးချယ်ပါ (နှစ်ခုလုံး မသုံးပါနှင့်)။

##### Option A: Basic Version (File Mount Only)

```yaml
# secret-provider-class.yaml
apiVersion: secrets-store.csi.x-k8s.io/v1
kind: SecretProviderClass
metadata:
  name: azure-keyvault-secrets
  namespace: default
spec:
  provider: azure
  parameters:
    keyvaultName: "kv-myapp-prod-001"
    objects: |
      array:
        - |
          objectName: DatabaseConnectionString
          objectType: secret
          objectVersion: ""
        - |
          objectName: StripeApiKey
          objectType: secret
          objectVersion: ""
        - |
          objectName: MyEncryptionKey
          objectType: key
          objectVersion: ""
    tenantId: "xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx"
```

**Option A ကို ဘယ်အခါ သုံးသင့်လဲ:**
- Secrets တွေကို **file အနေနဲ့** pod ထဲမှာ mount လုပ်မယ်
- Environment variables မလိုပါ
- Example: `/mnt/secrets-store/DatabaseConnectionString` file အနေနဲ့ ရနိုင်မယ်

```bash
# Apply Option A
kubectl apply -f secret-provider-class.yaml
```

##### Option B: With Sync to Kubernetes Secrets

**Option B ကို ဘယ်အခါ သုံးသင့်လဲ:**
- Secrets တွေကို **Kubernetes Secret Object** အဖြစ် sync လုပ်ချင်တယ်
- Environment variables အနေနဲ့ သုံးချင်တယ်
- Multiple pods တွေမှာ တူညီတဲ့ secrets တွေ အသုံးပြုမယ်
- Existing applications မှာ `secretKeyRef` pattern သုံးထားတယ်

> 💡 **Recommendation:** Production မှာ Option B ကို သုံးတာ ပိုအဆင်ပြေပါတယ်။

```bash
# Apply Option B (နောက် Step 5 မှာ ပြထားပါတယ်)
kubectl apply -f secret-provider-class-with-sync.yaml
```


#### Step 4: Pod Definition with CSI Volume

```yaml
# pod-with-secrets.yaml
apiVersion: v1
kind: Pod
metadata:
  name: myapp-pod
  namespace: default
spec:
  containers:
    - name: myapp
      image: myregistry.azurecr.io/myapp:latest
      ports:
        - containerPort: 80
      volumeMounts:
        - name: secrets-store
          mountPath: "/mnt/secrets-store"
          readOnly: true
      env:
        # Mount secrets as environment variables
        - name: DATABASE_CONNECTION_STRING
          valueFrom:
            secretKeyRef:
              name: myapp-secrets
              key: DatabaseConnectionString
        - name: STRIPE_API_KEY
          valueFrom:
            secretKeyRef:
              name: myapp-secrets
              key: StripeApiKey
  volumes:
    - name: secrets-store
      csi:
        driver: secrets-store.csi.k8s.io
        readOnly: true
        volumeAttributes:
          secretProviderClass: "azure-keyvault-secrets"
```

#### Step 5: Sync Secrets to Kubernetes Secrets (Option B Only)

> ⚠️ **သတိ:** ဒါက **Option B** အတွက်ပဲ သက်ဆိုင်ပါတယ်။ Option A သုံးရင် ဒီ Step ကို ချန်လှပ်ပါ။

```yaml
# secret-provider-class-with-sync.yaml
apiVersion: secrets-store.csi.x-k8s.io/v1
kind: SecretProviderClass
metadata:
  name: azure-keyvault-secrets-sync
  namespace: default
spec:
  provider: azure
  parameters:
    keyvaultName: "kv-myapp-prod-001"
    objects: |
      array:
        - |
          objectName: DatabaseConnectionString
          objectType: secret
          objectVersion: ""
        - |
          objectName: StripeApiKey
          objectType: secret
          objectVersion: ""
    tenantId: "xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx"
  # ✅ secretObjects section - Key Vault secrets ကို Kubernetes Secret Object အဖြစ် sync လုပ်မယ်
  secretObjects:
    - secretName: myapp-secrets          # Kubernetes Secret နာမည်
      type: Opaque
      data:
        - objectName: DatabaseConnectionString    # Key Vault က secret name
          key: DatabaseConnectionString           # Kubernetes Secret မှာ key name
        - objectName: StripeApiKey
          key: StripeApiKey
```

```bash
# Apply SecretProviderClass with sync
kubectl apply -f secret-provider-class-with-sync.yaml

# Verify secrets are created automatically
kubectl get secrets myapp-secrets -o yaml
```

##### Sync အလုပ်ပုံ:

```
Azure Key Vault
    ↓
    ├── DatabaseConnectionString = "Server=myserver..."
    └── StripeApiKey = "sk_live_abc123..."
    
         ↓ (Auto Sync)
         
Kubernetes Secret Object
    ↓
    apiVersion: v1
    kind: Secret
    metadata:
      name: myapp-secrets
    type: Opaque
    data:
      DatabaseConnectionString: <base64>
      StripeApiKey: <base64>
    
         ↓ (Available in pods)
         
Environment Variables:
    DATABASE_CONNECTION_STRING = "Server=myserver..."
    STRIPE_API_KEY = "sk_live_abc123..."
```

### Method 2: Using Azure Workload Identity

#### Step 1: Enable OIDC Issuer on AKS

```bash
# Get AKS cluster info
$aksCluster = az aks show `
    --resource-group rg-keyvault-demo `
    --name myAKSCluster

# Enable OIDC issuer
az aks update `
    --resource-group rg-keyvault-demo `
    --name myAKSCluster `
    --enable-oidc-issuer

# Get OIDC issuer URL
$oidcIssuerUrl = az aks show `
    --resource-group rg-keyvault-demo `
    --name myAKSCluster `
    --query oidcIssuerProfile.issuerUrl -o tsv

Write-Host "OIDC Issuer URL: $oidcIssuerUrl"
```

#### Step 2: Create Managed Identity and Federated Credential

```bash
# Create User Assigned Managed Identity
$identity = az identity create `
    --name myapp-workload-identity `
    --resource-group rg-keyvault-demo `
    --location southeastasia

$identityClientId = $identity.clientId
$identityPrincipalId = $identity.principalId

# Grant Key Vault access
az role assignment create `
    --assignee $identityPrincipalId `
    --role "Key Vault Secrets User" `
    --scope "/subscriptions/<sub-id>/resourceGroups/rg-keyvault-demo/providers/Microsoft.KeyVault/vaults/kv-myapp-prod-001"

# Create federated credential
az identity federated-credential create `
    --name myapp-federated-credential `
    --identity-name myapp-workload-identity `
    --resource-group rg-keyvault-demo `
    --issuer $oidcIssuerUrl `
    --subject system:serviceaccount:default:myapp-service-account
```


#### Step 3: Create Kubernetes Service Account

```yaml
# service-account.yaml
apiVersion: v1
kind: ServiceAccount
metadata:
  name: myapp-service-account
  namespace: default
  annotations:
    azure.workload.identity/client-id: "<identity-client-id>"
    azure.workload.identity/tenant-id: "<tenant-id>"
```

```bash
# Apply service account
kubectl apply -f service-account.yaml
```

#### Step 4: Deploy Application with Workload Identity

```yaml
# deployment-workload-identity.yaml
apiVersion: apps/v1
kind: Deployment
metadata:
  name: myapp-deployment
  namespace: default
spec:
  replicas: 2
  selector:
    matchLabels:
      app: myapp
  template:
    metadata:
      labels:
        app: myapp
        azure.workload.identity/use: "true"
    spec:
      serviceAccountName: myapp-service-account
      containers:
        - name: myapp
          image: myregistry.azurecr.io/myapp:latest
          ports:
            - containerPort: 80
          env:
            - name: KeyVault__VaultUrl
              value: "https://kv-myapp-prod-001.vault.azure.net/"
            - name: AZURE_CLIENT_ID
              valueFrom:
                fieldRef:
                  fieldPath: metadata.annotations['azure.workload.identity/client-id']
          resources:
            requests:
              cpu: "100m"
              memory: "128Mi"
            limits:
              cpu: "500m"
              memory: "512Mi"
---
apiVersion: v1
kind: Service
metadata:
  name: myapp-service
  namespace: default
spec:
  type: LoadBalancer
  ports:
    - port: 80
      targetPort: 80
  selector:
    app: myapp
```

```bash
# Apply deployment
kubectl apply -f deployment-workload-identity.yaml

# Verify pods are running
kubectl get pods -l app=myapp

# Check logs
kubectl logs -l app=myapp -f
```

---

## 🔐 Best Practices များ

### ၁. Secret Rotation Strategy

```bash
# Secret တစ်ခုအတွက် auto-rotation enable လုပ်ခြင်း
# Note: Key Vault itself doesn't auto-rotate, but you can use Azure Functions

# Sample rotation function trigger
az keyvault secret set-attributes `
    --vault-name kv-myapp-prod-001 `
    --name DatabaseConnectionString `
    --expires "$(date -d '+90 days' -u +%Y-%m-%dT%H:%M:%SZ)"
```

```csharp
// Rotation Handler (Azure Function)
public static class SecretRotationFunction
{
    [FunctionName("RotateSecret")]
    public static async Task Run(
        [TimerTrigger("0 0 0 * * *")] TimerInfo myTimer,
        ILogger log)
    {
        string keyVaultUrl = Environment.GetEnvironmentVariable("KeyVaultUrl")!;
        var client = new SecretClient(new Uri(keyVaultUrl), new DefaultAzureCredential());
        
        // Get current secret
        var currentSecret = await client.GetSecretAsync("DatabaseConnectionString");
        
        // Generate new password
        string newPassword = GenerateSecurePassword();
        
        // Update database password (pseudo-code)
        // await UpdateDatabasePasswordAsync(newPassword);
        
        // Create new version of secret
        await client.SetSecretAsync("DatabaseConnectionString", 
            currentSecret.Value.Value.Replace("Password=old", $"Password={newPassword}"));
        
        log.LogInformation($"Secret rotated at: {DateTime.Now}");
    }
    
    private static string GenerateSecurePassword()
    {
        using var rng = RandomNumberGenerator.Create();
        var bytes = new byte[32];
        rng.GetBytes(bytes);
        return Convert.ToBase64String(bytes);
    }
}
```

### ၂. Access Control Best Practices

```yaml
# Recommended Access Pattern:

Development Environment:
  - Developers: Key Vault Secrets User (read-only)
  - CI/CD Pipeline: Key Vault Secrets Officer
  - Application: Key Vault Secrets User
  
Production Environment:
  - Developers: No direct access
  - Operations Team: Key Vault Reader (audit only)
  - Application (Managed Identity): Key Vault Secrets User
  - Automated Systems: Key Vault Secrets Officer

# Principle of Least Privilege:
  - Give minimum required permissions
  - Use separate Key Vaults per environment
  - Enable audit logging for all access
```

### ၃. Network Security

```bash
# Enable firewall rules
az keyvault network-rule add `
    --name kv-myapp-prod-001 `
    --ip-address "203.0.113.0/24" `
    --action Allow

# Enable private endpoint
az network private-endpoint create `
    --name kv-private-endpoint `
    --resource-group rg-keyvault-demo `
    --vnet-name myVNet `
    --subnet mySubnet `
    --private-connection-resource-id "/subscriptions/<sub-id>/resourceGroups/rg-keyvault-demo/providers/Microsoft.KeyVault/vaults/kv-myapp-prod-001" `
    --group-id vault `
    --connection-name kv-connection

# Disable public access
az keyvault update `
    --name kv-myapp-prod-001 `
    --public-network-access Disabled
```

### ၄. Monitoring and Alerting

```bash
# Enable diagnostic settings
az monitor diagnostic-settings create `
    --name kv-diagnostics `
    --resource "/subscriptions/<sub-id>/resourceGroups/rg-keyvault-demo/providers/Microsoft.KeyVault/vaults/kv-myapp-prod-001" `
    --logs '[{"category": "AuditEvent","enabled": true}]' `
    --metrics '[{"category": "AllMetrics","enabled": true}]' `
    --workspace "/subscriptions/<sub-id>/resourcegroups/rg-keyvault-demo/providers/microsoft.operationalinsights/workspaces/myLogAnalyticsWorkspace"
```

```kusto
// KQL Query for monitoring Key Vault access
AzureDiagnostics
| where ResourceType == "VAULTS"
| where OperationName == "SecretGet"
| project TimeGenerated, CallerIPAddress, identity_claim_upn_s, Resource, ResultSignature
| order by TimeGenerated desc
```

```json
// Azure Monitor Alert Rule
{
  "type": "Microsoft.Insights/scheduledQueryRules",
  "apiVersion": "2021-08-01",
  "name": "keyvault-unauthorized-access",
  "location": "southeastasia",
  "properties": {
    "description": "Alert on unauthorized Key Vault access attempts",
    "enabled": true,
    "query": "AzureDiagnostics | where ResourceType == 'VAULTS' | where ResultSignature == 'Unauthorized'",
    "severity": 2,
    "frequency": "PT5M",
    "timeWindow": "PT5M",
    "trigger": {
      "threshold": 1,
      "operator": "GreaterThan"
    },
    "actions": {
      "actionGroups": ["/subscriptions/<sub-id>/resourcegroups/rg-keyvault-demo/providers/microsoft.insights/actiongroups/myActionGroup"]
    }
  }
}
```


---

## 📊 Complete Demo Project Structure

```
KeyVaultDemo/
├── src/
│   ├── KeyVaultDemo.Api/
│   │   ├── Controllers/
│   │   │   └── SecretsController.cs
│   │   ├── Services/
│   │   │   ├── ISecretService.cs
│   │   │   └── KeyVaultSecretService.cs
│   │   ├── Program.cs
│   │   ├── appsettings.json
│   │   └── KeyVaultDemo.Api.csproj
│   └── KeyVaultDemo.Functions/
│       ├── Functions/
│       │   └── SecretRotationFunction.cs
│       ├── Program.cs
│       ├── local.settings.json
│       └── KeyVaultDemo.Functions.csproj
├── k8s/
│   ├── namespace.yaml
│   ├── secret-provider-class.yaml
│   ├── service-account.yaml
│   ├── deployment.yaml
│   └── service.yaml
├── docker/
│   ├── Dockerfile
│   └── docker-compose.yml
├── scripts/
│   ├── setup-keyvault.ps1
│   ├── create-secrets.ps1
│   └── deploy-to-aks.ps1
├── infrastructure/
│   └── keyvault.bicep
└── README.md
```

### KeyVaultDemo.Api/Program.cs

```csharp
using Azure.Identity;
using Azure.Security.KeyVault.Secrets;

var builder = WebApplication.CreateBuilder(args);

// Add Key Vault configuration
var keyVaultUrl = builder.Configuration["KeyVault:VaultUrl"] 
    ?? throw new InvalidOperationException("KeyVault:VaultUrl is required");

builder.Configuration.AddAzureKeyVault(
    new Uri(keyVaultUrl),
    new DefaultAzureCredential()
);

// Add services
builder.Services.AddSingleton<SecretClient>(sp =>
{
    return new SecretClient(
        new Uri(keyVaultUrl),
        new DefaultAzureCredential()
    );
});

builder.Services.AddScoped<ISecretService, KeyVaultSecretService>();

var app = builder.Build();

app.MapGet("/health", () => Results.Ok(new { status = "healthy", timestamp = DateTime.UtcNow }));

app.MapGet("/api/secrets/{name}", async (string name, ISecretService secretService) =>
{
    try
    {
        var value = await secretService.GetSecretAsync(name);
        return Results.Ok(new { name, retrieved = true, valueLength = value.Length });
    }
    catch (Exception ex)
    {
        return Results.Problem(ex.Message, statusCode: 500);
    }
});

app.MapPost("/api/secrets/{name}", async (string name, string value, ISecretService secretService) =>
{
    await secretService.SetSecretAsync(name, value);
    return Results.Created($"/api/secrets/{name}", new { name, created = true });
});

app.Run();
```

### KeyVaultDemo.Api/Services/KeyVaultSecretService.cs

```csharp
using Azure.Security.KeyVault.Secrets;

public interface ISecretService
{
    Task<string> GetSecretAsync(string secretName);
    Task SetSecretAsync(string secretName, string value);
    Task DeleteSecretAsync(string secretName);
}

public class KeyVaultSecretService : ISecretService
{
    private readonly SecretClient _secretClient;
    private readonly ILogger<KeyVaultSecretService> _logger;

    public KeyVaultSecretService(
        SecretClient secretClient,
        ILogger<KeyVaultSecretService> logger)
    {
        _secretClient = secretClient;
        _logger = logger;
    }

    public async Task<string> GetSecretAsync(string secretName)
    {
        try
        {
            var secret = await _secretClient.GetSecretAsync(secretName);
            _logger.LogInformation("Retrieved secret: {SecretName}", secretName);
            return secret.Value;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to retrieve secret: {SecretName}", secretName);
            throw;
        }
    }

    public async Task SetSecretAsync(string secretName, string value)
    {
        try
        {
            await _secretClient.SetSecretAsync(secretName, value);
            _logger.LogInformation("Set secret: {SecretName}", secretName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to set secret: {SecretName}", secretName);
            throw;
        }
    }

    public async Task DeleteSecretAsync(string secretName)
    {
        try
        {
            var operation = await _secretClient.StartDeleteSecretAsync(secretName);
            await operation.WaitForCompletionAsync();
            _logger.LogInformation("Deleted secret: {SecretName}", secretName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete secret: {SecretName}", secretName);
            throw;
        }
    }
}
```

### infrastructure/keyvault.bicep

```bicep
@description('Name of the Key Vault')
param keyVaultName string

@description('Location for all resources')
param location string = resourceGroup().location

@description('Azure AD tenant ID')
param tenantId string = subscription().tenantId

@description('Object ID of the user or service principal to grant access')
param accessPolicyObjectId string

var defaultAccessPolicy = {
  tenantId: tenantId
  objectId: accessPolicyObjectId
  permissions: {
    keys: [
      'get'
      'list'
      'encrypt'
      'decrypt'
    ]
    secrets: [
      'get'
      'list'
      'set'
      'delete'
    ]
    certificates: [
      'get'
      'list'
    ]
  }
}

resource keyVault 'Microsoft.KeyVault/vaults@2023-02-01' = {
  name: keyVaultName
  location: location
  properties: {
    tenantId: tenantId
    sku: {
      name: 'standard'
      family: 'A'
    }
    enableRbacAuthorization: true
    enableSoftDelete: true
    softDeleteRetentionInDays: 90
    enablePurgeProtection: true
    accessPolicies: accessPolicyObjectId != '' ? [defaultAccessPolicy] : []
    networkAcls: {
      defaultAction: 'Allow'
      bypass: 'AzureServices'
    }
  }
}

output keyVaultId string = keyVault.id
output keyVaultUri string = keyVault.properties.vaultUri
```

### scripts/setup-keyvault.ps1

```powershell
# Setup-KeyVault.ps1
param(
    [Parameter(Mandatory=$true)]
    [string]$ResourceGroupName,
    
    [Parameter(Mandatory=$true)]
    [string]$KeyVaultName,
    
    [Parameter(Mandatory=$true)]
    [string]$Location
)

# Login to Azure
# Connect-AzAccount

# Create resource group if not exists
$rg = Get-AzResourceGroup -Name $ResourceGroupName -ErrorAction SilentlyContinue
if (-not $rg) {
    New-AzResourceGroup -Name $ResourceGroupName -Location $Location
    Write-Host "Created resource group: $ResourceGroupName"
}

# Deploy Key Vault using Bicep
$templateParams = @{
    keyVaultName = $KeyVaultName
    location = $Location
    accessPolicyObjectId = (Get-AzADUser -SignedIn).Id
}

$deployment = New-AzResourceGroupDeployment `
    -ResourceGroupName $ResourceGroupName `
    -TemplateFile "../infrastructure/keyvault.bicep" `
    -TemplateParameterObject $templateParams

Write-Host "Key Vault created successfully!"
Write-Host "Key Vault ID: $($deployment.Outputs.keyVaultId.Value)"
Write-Host "Key Vault URI: $($deployment.Outputs.keyVaultUri.Value)"
```

---

## 🎓 Summary

Azure Key Vault ဆိုတာ cloud applications များအတွက် secrets, keys, နှင့် certificates များကို လုံခြုံစွာသိမ်းဆည်းနိုင်တဲ့ centralized service တစ်ခုဖြစ်ပါတယ်။

### Key Takeaways:

1. **Security First** - Secrets များကို code ထဲမှာ မသိမ်းပဲ Key Vault မှာ သိမ်းဆည်းပါ
2. **Managed Identity** - Production မှာ Managed Identity ကို အသုံးပြုပါ (Service Principal မဟုတ်ပဲ)
3. **RBAC over Access Policies** - Modern applications အတွက် RBAC ကို အသုံးပြုပါ
4. **Caching** - Performance အတွက် secrets များကို cache လုပ်ပါ
5. **Monitoring** - Key Vault access များကို monitor လုပ်ပါ
6. **Rotation** - Secrets များကို အမြဲတမ်း rotate လုပ်ပါ
7. **Network Security** - Production မှာ private endpoints ကို အသုံးပြုပါ

---

## 📚 References

- [Azure Key Vault Documentation](https://docs.microsoft.com/azure/key-vault/)
- [Azure Key Vault Best Practices](https://docs.microsoft.com/azure/key-vault/general/best-practices)
- [Azure Identity SDK](https://docs.microsoft.com/dotnet/api/overview/azure/identity-readme)
- [Secrets Store CSI Driver](https://github.com/Azure/secrets-store-csi-driver-provider-azure)
- [Azure Workload Identity](https://azure.github.io/azure-workload-identity/)
