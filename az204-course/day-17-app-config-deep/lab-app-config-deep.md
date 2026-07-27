# Lab: Advanced App Configuration Patterns

## Implement Feature Flags, Snapshots, and Multi-Region Configuration

### Objective

By the end of this lab, you will be able to:

- Create feature flags with percentage rollout
- Implement time-window feature flags
- Use configuration snapshots
- Integrate App Configuration with Key Vault
- Build a .NET app that uses feature flags

### Architecture

```
┌─────────────────────────────────────────────────────────────────┐
│                        YOUR RESOURCES                           │
│                                                                 │
│  ┌──────────────────────┐          ┌──────────────────────┐    │
│  │   App Configuration  │          │     Key Vault        │    │
│  │                      │          │                      │    │
│  │  Feature Flags:      │          │  Secrets:            │    │
│  │  ├─ new-checkout     │    ┌────►├─ app-db-password     │    │
│  │  │  (simple ON/OFF)  │    │     │  api-secret-key      │    │
│  │  ├─ beta-search      │    │     │                      │    │
│  │  │  (25% rollout)    │    │     └──────────────────────┘    │
│  │  ├─ holiday-banner   │    │                                  │
│  │  │  (time-window)    │    │                                  │
│  │  ├─ premium-feature  │    │                                  │
│  │  │  (user targeting) │    │                                  │
│  │  │                   │    │                                  │
│  │  Config Keys:        │    │                                  │
│  │  ├─ app:timeout      │    │                                  │
│  │  ├─ app:endpoint     │    │                                  │
│  │  └─ app:secret-ref   │──┘                                    │
│  │  (Key Vault ref)     │                                       │
│  │                      │                                       │
│  │  Snapshot:           │                                       │
│  │  └─ pre-deploy-snap  │                                       │
│  └──────────┬───────────┘                                       │
│             │                                                   │
└─────────────┼───────────────────────────────────────────────────┘
              │
              ▼
┌─────────────────────────────────────────────────────────────────┐
│                  .NET 8 WEB APPLICATION                         │
│                                                                 │
│  ┌──────────────────────────────────────────────────────────┐  │
│  │  Program.cs                                              │  │
│  │  ├─ AddAzureAppConfiguration()                           │  │
│  │  ├─ AddFeatureManagement()                               │  │
│  │  └─ ConfigureKeyVault()                                  │  │
│  └──────────────────────────────────────────────────────────┘  │
│                                                                 │
│  ┌──────────────────┐  ┌──────────────────┐                    │
│  │  HomeController   │  │  FeaturesController│                   │
│  │  └─ Feature flags │  │  └─ Feature flags │                   │
│  │     checked in    │  │     checked in    │                   │
│  │     Razor views   │  │     API actions   │                   │
│  └──────────────────┘  └──────────────────┘                    │
│                                                                 │
│  ┌──────────────────────────────────────────────────────────┐  │
│  │  IFeatureManager  ──►  Feature flag evaluation            │  │
│  │  IConfiguration   ──►  Config value resolution            │  │
│  │  IFeatureManager  ──►  Custom filter (BetaUserFilter)     │  │
│  └──────────────────────────────────────────────────────────┘  │
└─────────────────────────────────────────────────────────────────┘
```

---

### Prerequisites

- Azure subscription with Contributor access
- .NET 8 SDK installed
- Azure CLI installed and logged in (`az login`)
- Code editor (Visual Studio or VS Code)

---

### Step 1: Create Resource Group and App Configuration Store

```bash
# Set variables
RESOURCE_GROUP="rg-appconfig-deep"
LOCATION="eastus"
APP_CONFIG_NAME="appconfig-deep-$(Get-Random -Maximum 9999)"
KEY_VAULT_NAME="kv-appconfig-$(Get-Random -Maximum 9999)"

# Create resource group
az group create \
  --name $RESOURCE_GROUP \
  --location $LOCATION

# Create App Configuration store (Standard tier for snapshots & replication)
az appconfig create \
  --name $APP_CONFIG_NAME \
  --resource-group $RESOURCE_GROUP \
  --location $LOCATION \
  --sku standard \
  --output json

# Get the connection string
APP_CONFIG_CONN=$(az appconfig credential list \
  --name $APP_CONFIG_NAME \
  --resource-group $RESOURCE_GROUP \
  --query "[?primaryKey].connectionString" \
  --output tsv)

echo "App Config Connection: $APP_CONFIG_CONN"
```

---

### Step 2: Create Feature Flags

```bash
# 2a. Simple on/off feature flag
az appconfig kv set-key \
  --name $APP_CONFIG_NAME \
  --key "feature_management:feature_flags:new-checkout" \
  --value '{
    "id": "new-checkout",
    "enabled": true,
    "conditions": {
      "client_filters": []
    }
  }' \
  --content-type "application/vnd.microsoft.appconfig.featureflag+json;charset=utf-8"

# 2b. Percentage-based rollout (25% of traffic)
az appconfig kv set-key \
  --name $APP_CONFIG_NAME \
  --key "feature_management:feature_flags:beta-search" \
  --value '{
    "id": "beta-search",
    "enabled": true,
    "conditions": {
      "client_filters": [
        {
          "name": "Microsoft.Percentage",
          "parameters": {
            "Value": 25
          }
        }
      ]
    }
  }' \
  --content-type "application/vnd.microsoft.appconfig.featureflag+json;charset=utf-8"

# 2c. Time-window feature (active for 1 week)
START_DATE=$(date -u -d "+1 day" +"%Y-%m-%dT00:00:00Z")
END_DATE=$(date -u -d "+8 days" +"%Y-%m-%dT23:59:59Z")

az appconfig kv set-key \
  --name $APP_CONFIG_NAME \
  --key "feature_management:feature_flags:holiday-banner" \
  --value "{
    \"id\": \"holiday-banner\",
    \"enabled\": true,
    \"conditions\": {
      \"client_filters\": [
        {
          \"name\": \"Microsoft.TimeWindow\",
          \"parameters\": {
            \"Start\": \"$START_DATE\",
            \"End\": \"$END_DATE\"
          }
        }
      ]
    }
  }" \
  --content-type "application/vnd.microsoft.appconfig.featureflag+json;charset=utf-8"

# 2d. User targeting feature
az appconfig kv set-key \
  --name $APP_CONFIG_NAME \
  --key "feature_management:feature_flags:premium-feature" \
  --value '{
    "id": "premium-feature",
    "enabled": true,
    "conditions": {
      "client_filters": [
        {
          "name": "Microsoft.Targeting",
          "parameters": {
            "Audience": {
              "Users": ["admin@contoso.com"],
              "Groups": [
                {
                  "Name": "BetaTesters",
                  "Percentage": 50
                }
              ],
              "DefaultRolloutPercentage": 10
            }
          }
        }
      ]
    }
  }' \
  --content-type "application/vnd.microsoft.appconfig.featureflag+json;charset=utf-8"
```

---

### Step 3: Create Key Vault and Store a Secret

```bash
# Create Key Vault
az keyvault create \
  --name $KEY_VAULT_NAME \
  --resource-group $RESOURCE_GROUP \
  --location $LOCATION \
  --enable-rbac-authorization true

# Store a secret
az keyvault secret set \
  --vault-name $KEY_VAULT_NAME \
  --name "app-db-password" \
  --value "SuperSecretP@ssw0rd123!"

az keyvault secret set \
  --vault-name $KEY_VAULT_NAME \
  --name "api-secret-key" \
  --value "api-key-abc-def-ghi-jkl"

# Assign App Config Managed Identity access to Key Vault
APP_CONFIG_ID=$(az appconfig show \
  --name $APP_CONFIG_NAME \
  --resource-group $RESOURCE_GROUP \
  --query "identity.principalId" \
  --output tsv)

az role assignment create \
  --assignee $APP_CONFIG_ID \
  --role "Key Vault Secrets User" \
  --scope "/subscriptions/$(az account show --query id -o tsv)/resourceGroups/$RESOURCE_GROUP/providers/Microsoft.KeyVault/vaults/$KEY_VAULT_NAME"
```

---

### Step 4: Reference Key Vault Secret in App Configuration

```bash
# Get Key Vault URI
KEY_VAULT_URI=$(az keyvault show \
  --name $KEY_VAULT_NAME \
  --resource-group $RESOURCE_GROUP \
  --query "properties.vaultUri" \
  --output tsv)

# Set Key Vault reference in App Configuration
az appconfig kv set-key \
  --name $APP_CONFIG_NAME \
  --key "app:db-connection" \
  --value "@Microsoft.KeyVault(VaultName=$KEY_VAULT_NAME;SecretName=app-db-password)" \
  --content-type "application/vnd.microsoft.appconfig.keyvaultref+json;charset=utf-8"

az appconfig kv set-key \
  --name $APP_CONFIG_NAME \
  --key "app:api-key" \
  --value "@Microsoft.KeyVault(VaultName=$KEY_VAULT_NAME;SecretName=api-secret-key)" \
  --content-type "application/vnd.microsoft.appconfig.keyvaultref+json;charset=utf-8"

# Set regular config values
az appconfig kv set-key \
  --name $APP_CONFIG_NAME \
  --key "app:timeout" \
  --value "30"

az appconfig kv set-key \
  --name $APP_CONFIG_NAME \
  --key "app:endpoint" \
  --value "https://api.contoso.com/v1"

# Set sentinel key for cache refresh
az appconfig kv set-key \
  --name $APP_CONFIG_NAME \
  --key "settings:sentinel" \
  --value "1"
```

---

### Step 5: Build a .NET Web App with Feature Management

```bash
# Create the project
dotnet new webapp -n FeatureFlagDemo -f net8.0 --no-https
cd FeatureFlagDemo

# Add required NuGet packages
dotnet add package Microsoft.Azure.AppConfiguration.AspNetCore
dotnet add package Microsoft.FeatureManagement
dotnet add package Microsoft.FeatureManagement.AspNetCore
dotnet add package Azure.Identity
```

---

### Step 6: Configure App to Use App Configuration Provider

Replace the contents of `Program.cs`:

```csharp
using Azure.Identity;
using FeatureFlagDemo.Filters;
using Microsoft.FeatureManagement;

var builder = WebApplication.CreateBuilder(args);

builder.Configuration.AddAzureAppConfiguration(options =>
{
    options.Connect(builder.Configuration.GetConnectionString("AppConfig"))
        .Select("app:*", LabelFilter.Null)
        .Select("feature_management:*", LabelFilter.Null)
        .ConfigureKeyVault(keyVault =>
        {
            keyVault.Register(new DefaultAzureCredential());
        })
        .ConfigureRefresh(refresh =>
        {
            refresh.Register("settings:sentinel", refreshAll: true)
                   .SetCacheExpiration(TimeSpan.FromMinutes(1));
        });
});

builder.Services.AddAzureAppConfiguration();
builder.Services.AddFeatureManagement()
    .AddFeatureFilter<PercentageFilter>()
    .AddFeatureFilter<TimeWindowFilter>()
    .AddFeatureFilter<TargetingFilter>()
    .AddFeatureFilter<BetaUserFilter>();

builder.Services.AddControllersWithViews();
builder.Services.AddRazorPages();

var app = builder.Build();

app.UseAzureAppConfiguration();

app.UseRouting();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.MapRazorPages();

app.Run();
```

Update `appsettings.json`:

```json
{
  "ConnectionStrings": {
    "AppConfig": "YOUR_APP_CONFIG_CONNECTION_STRING_HERE"
  },
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning"
    }
  }
}
```

---

### Step 7: Implement IFeatureManager in Code

Create `Filters/BetaUserFilter.cs`:

```csharp
using Microsoft.FeatureManagement;
using System.Security.Claims;

namespace FeatureFlagDemo.Filters;

[FilterAlias("Custom.BetaUser")]
public class BetaUserFilter : IFeatureFilter
{
    public Task<bool> EvaluateAsync(FeatureFilterEvaluationContext context)
    {
        var allowedEmails = context.Parameters["AllowedEmails"]
            ?.ToString()
            ?.Split(',', StringSplitOptions.RemoveEmptyEntries)
            ?? Array.Empty<string>();

        var httpContextAccessor = context.HttpContext
            ?.RequestServices
            .GetRequiredService<IHttpContextAccessor>();

        var userEmail = httpContextAccessor?.HttpContext?.User?
            .FindFirst(ClaimTypes.Email)?.Value;

        if (string.IsNullOrEmpty(userEmail))
            return Task.FromResult(false);

        return Task.FromResult(
            allowedEmails.Contains(userEmail, StringComparer.OrdinalIgnoreCase));
    }
}
```

Create `Controllers/HomeController.cs`:

```csharp
using Microsoft.AspNetCore.Mvc;
using Microsoft.FeatureManagement;
using Microsoft.Extensions.Configuration;

namespace FeatureFlagDemo.Controllers;

public class HomeController : Controller
{
    private readonly IFeatureManager _featureManager;
    private readonly IConfiguration _configuration;

    public HomeController(
        IFeatureManager featureManager,
        IConfiguration configuration)
    {
        _featureManager = featureManager;
        _configuration = configuration;
    }

    public async Task<IActionResult> Index()
    {
        ViewBag.NewCheckout = await _featureManager.IsEnabledAsync("new-checkout");
        ViewBag.BetaSearch = await _featureManager.IsEnabledAsync("beta-search");
        ViewBag.HolidayBanner = await _featureManager.IsEnabledAsync("holiday-banner");
        ViewBag.PremiumFeature = await _featureManager.IsEnabledAsync("premium-feature");

        ViewBag.Timeout = _configuration["app:timeout"];
        ViewBag.Endpoint = _configuration["app:endpoint"];
        ViewBag.DbConnection = _configuration["app:db-connection"];
        ViewBag.ApiKey = _configuration["app:api-key"];

        return View();
    }
}
```

Create `Controllers/FeaturesController.cs`:

```csharp
using Microsoft.AspNetCore.Mvc;
using Microsoft.FeatureManagement;

namespace FeatureFlagDemo.Controllers;

[ApiController]
[Route("api/[controller]")]
public class FeaturesController : Controller
{
    private readonly IFeatureManager _featureManager;

    public FeaturesController(IFeatureManager featureManager)
    {
        _featureManager = featureManager;
    }

    [HttpGet]
    public async Task<IActionResult> GetAllFlags()
    {
        var flags = new Dictionary<string, bool>();
        var featureNames = new[] { "new-checkout", "beta-search", "holiday-banner", "premium-feature" };

        foreach (var name in featureNames)
        {
            flags[name] = await _featureManager.IsEnabledAsync(name);
        }

        return Ok(flags);
    }

    [HttpGet("{flagName}")]
    public async Task<IActionResult> GetFlagStatus(string flagName)
    {
        var isEnabled = await _featureManager.IsEnabledAsync(flagName);
        return Ok(new { Feature = flagName, Enabled = isEnabled });
    }
}
```

Update `Views/Home/Index.cshtml`:

```html
@{
    ViewData["Title"] = "Feature Flag Demo";
}

<h1>Azure App Configuration - Feature Flags</h1>

<h2>Feature Flag Status</h2>
<table class="table table-bordered">
    <thead class="table-dark">
        <tr>
            <th>Feature</th>
            <th>Status</th>
            <th>Description</th>
        </tr>
    </thead>
    <tbody>
        <tr>
            <td>new-checkout</td>
            <td>
                @if (ViewBag.NewCheckout)
                {
                    <span class="badge bg-success">ENABLED</span>
                }
                else
                {
                    <span class="badge bg-secondary">DISABLED</span>
                }
            </td>
            <td>Simple on/off feature flag</td>
        </tr>
        <tr>
            <td>beta-search</td>
            <td>
                @if (ViewBag.BetaSearch)
                {
                    <span class="badge bg-success">ENABLED</span>
                }
                else
                {
                    <span class="badge bg-secondary">DISABLED</span>
                }
            </td>
            <td>Percentage rollout (25% of users)</td>
        </tr>
        <tr>
            <td>holiday-banner</td>
            <td>
                @if (ViewBag.HolidayBanner)
                {
                    <span class="badge bg-success">ENABLED</span>
                }
                else
                {
                    <span class="badge bg-secondary">DISABLED</span>
                }
            </td>
            <td>Time-window feature flag</td>
        </tr>
        <tr>
            <td>premium-feature</td>
            <td>
                @if (ViewBag.PremiumFeature)
                {
                    <span class="badge bg-success">ENABLED</span>
                }
                else
                {
                    <span class="badge bg-secondary">DISABLED</span>
                }
            </td>
            <td>User targeting feature flag</td>
        </tr>
    </tbody>
</table>

<h2>Configuration Values</h2>
<table class="table table-bordered">
    <thead class="table-dark">
        <tr>
            <th>Key</th>
            <th>Value</th>
            <th>Source</th>
        </tr>
    </thead>
    <tbody>
        <tr>
            <td>app:timeout</td>
            <td>@ViewBag.Timeout</td>
            <td>App Configuration</td>
        </tr>
        <tr>
            <td>app:endpoint</td>
            <td>@ViewBag.Endpoint</td>
            <td>App Configuration</td>
        </tr>
        <tr>
            <td>app:db-connection</td>
            <td>@ViewBag.DbConnection?.Substring(0, Math.Min(20, ViewBag.DbConnection?.Length ?? 0))...</td>
            <td>Key Vault via App Config</td>
        </tr>
        <tr>
            <td>app:api-key</td>
            <td>****</td>
            <td>Key Vault via App Config</td>
        </tr>
    </tbody>
</table>
```

---

### Step 8: Test Feature Flag Toggling

```bash
# Run the application
dotnet run

# Test in browser: http://localhost:5000
# Test API endpoint: http://localhost:5000/api/features
```

**Toggle a flag off and observe the change:**

```bash
# Disable new-checkout flag
az appconfig kv set-key \
  --name $APP_CONFIG_NAME \
  --key "feature_management:feature_flags:new-checkout" \
  --value '{
    "id": "new-checkout",
    "enabled": false,
    "conditions": {
      "client_filters": []
    }
  }' \
  --content-type "application/vnd.microsoft.appconfig.featureflag+json;charset=utf-8"

# Wait 1-2 minutes for cache to refresh, then refresh the browser
# The new-checkout flag should now show DISABLED
```

**Update the sentinel key to force immediate refresh:**

```bash
az appconfig kv set-key \
  --name $APP_CONFIG_NAME \
  --key "settings:sentinel" \
  --value "$(Get-Random)"
```

---

### Step 9: Create and Restore Configuration Snapshot

```bash
# Create a snapshot of current configuration
az appconfig snapshot create \
  --name "pre-deploy-snapshot-$(date +%Y%m%d)" \
  --store $APP_CONFIG_NAME \
  --resource-group $RESOURCE_GROUP \
  --description "Snapshot before major config change"

# List snapshots
az appconfig snapshot list \
  --store $APP_CONFIG_NAME \
  --resource-group $RESOURCE_GROUP \
  --output table

# Simulate a breaking change
az appconfig kv set-key \
  --name $APP_CONFIG_NAME \
  --key "app:timeout" \
  --value "999"

az appconfig kv set-key \
  --name $APP_CONFIG_NAME \
  --key "app:endpoint" \
  --value "https://broken-endpoint.invalid"

# Verify the breaking changes
az appconfig kv show-key \
  --name $APP_CONFIG_NAME \
  --key "app:timeout"

az appconfig kv show-key \
  --name $APP_CONFIG_NAME \
  --key "app:endpoint"

# Restore from snapshot
SNAPSHOT_NAME=$(az appconfig snapshot list \
  --store $APP_CONFIG_NAME \
  --resource-group $RESOURCE_GROUP \
  --query "[0].name" \
  --output tsv)

az appconfig snapshot restore \
  --name $SNAPSHOT_NAME \
  --store $APP_CONFIG_NAME \
  --resource-group $RESOURCE_GROUP

# Verify restoration
az appconfig kv show-key \
  --name $APP_CONFIG_NAME \
  --key "app:timeout"

az appconfig kv show-key \
  --name $APP_CONFIG_NAME \
  --key "app:endpoint"
```

---

### Step 10: Test Multi-Region Failover

```bash
# Add a replica in West US 2
az appconfig replica create \
  --name "${APP_CONFIG_NAME}-replica1" \
  --endpoint $APP_CONFIG_NAME \
  --resource-group $RESOURCE_GROUP \
  --location "westus2"

# List replicas
az appconfig replica list \
  --store $APP_CONFIG_NAME \
  --resource-group $RESOURCE_GROUP \
  --output table

# Verify replication by reading from the replica
az appconfig kv show-key \
  --name $APP_CONFIG_NAME \
  --key "app:timeout"

# Note: Reads are automatically distributed across replicas
# The application connects using the same connection string
# but reads may be served from the nearest replica
```

---

### Step 11: Clean Up Resources

```bash
# Delete the resource group (removes all resources)
az group delete \
  --name $RESOURCE_GROUP \
  --yes \
  --no-wait

echo "Resource group $RESOURCE_GROUP deletion initiated."
```

---

### Verification Checklist

| Task | Expected Result |
|------|----------------|
| App Configuration store created | Standard SKU, East US |
| Feature flags created | 4 flags (simple, percentage, time-window, targeting) |
| Key Vault created with secrets | 2 secrets (db-password, api-key) |
| Key Vault references set | 2 references resolved correctly |
| .NET app runs | Displays feature flags and config values |
| Flag toggling works | Changes reflect within cache expiration |
| Snapshot created | Snapshot visible in portal |
| Snapshot restore works | Config values reverted to snapshot state |
| Replica created | Replica visible in replica list |
| Resources cleaned up | Resource group deleted |

---

### Common Issues & Troubleshooting

| Issue | Cause | Solution |
|-------|-------|----------|
| Feature flag not loading | Incorrect content type | Ensure content type is `application/vnd.microsoft.appconfig.featureflag+json;charset=utf-8` |
| Key Vault reference shows error | Identity not authorized | Assign `Key Vault Secrets User` role to App Config managed identity |
| Config not refreshing | Sentinel key not updated | Update sentinel key and ensure `ConfigureRefresh` is called |
| Percentage filter always false | Insufficient test iterations | Run multiple requests to see varied results |
| Snapshot restore fails | Insufficient permissions | Ensure you have `App Configuration Data Contributor` role |
