# Day 17 — Advanced Azure App Configuration

## What You'll Learn Today

- Feature flag patterns (simple, percentage-based, time-window, custom)
- Configuration snapshots
- Multi-region configuration
- Configuration encryption with Key Vault
- App Configuration provider for .NET
- Feature management library
- Configuration drift detection

---

## 1. Feature Flags Deep Dive

Feature flags (also called feature toggles) enable you to decouple feature releases from code deployments. They allow progressive rollouts, A/B testing, and instant kill switches.

### 1.1 Simple On/Off Feature Flags

The most basic form — a boolean switch.

```json
{
  "feature_management": {
    "feature_flags": [
      {
        "id": "new-checkout-flow",
        "enabled": true,
        "conditions": {
          "client_filters": []
        }
      }
    ]
  }
}
```

**Use cases:**
- Dark launches (code deployed but hidden)
- Instant rollback (disable a problematic feature)
- Admin-only features

### 1.2 Percentage-Based Rollout

Gradually expose a feature to a percentage of users.

```json
{
  "id": "redesigned-dashboard",
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
}
```

**Rollout strategy:**

| Stage | Percentage | Duration | Action |
|-------|-----------|----------|--------|
| 1 | 5% | 2 days | Monitor telemetry |
| 2 | 25% | 3 days | Check error rates |
| 3 | 50% | 3 days | Validate performance |
| 4 | 75% | 2 days | Final validation |
| 5 | 100% | — | Full rollout |

### 1.3 Time-Window Features

Enable a feature only during a specific time period.

```json
{
  "id": "black-friday-banner",
  "enabled": true,
  "conditions": {
    "client_filters": [
      {
        "name": "Microsoft.TimeWindow",
        "parameters": {
          "Start": "2026-11-25T00:00:00Z",
          "End": "2026-11-30T23:59:59Z"
        }
      }
    ]
  }
}
```

**Common patterns:**

| Pattern | Description |
|---------|-------------|
| Scheduled release | Feature activates at a future date |
| Trial period | Feature available for limited time |
| Business hours | Feature active only during work hours |
| Seasonal | Holiday or event-specific features |

### 1.4 Custom Feature Filters

Build your own filter logic by implementing `IFeatureFilter`.

```json
{
  "id": "beta-api",
  "enabled": true,
  "conditions": {
    "client_filters": [
      {
        "name": "Custom.BetaUser",
        "parameters": {
          "AllowedEmails": "admin@contoso.com,dev@contoso.com"
        }
      }
    ]
  }
}
```

```csharp
public class BetaUserFilter : IFeatureFilter
{
    public Task<bool> EvaluateAsync(FeatureFilterEvaluationContext context)
    {
        var allowedEmails = context.Parameters["AllowedEmails"]
            .ToString().Split(',');

        var httpContextAccessor = context.HttpContext
            .RequestServices.GetRequiredService<IHttpContextAccessor>();

        var userEmail = httpContextAccessor.HttpContext?.User?
            .FindFirst(ClaimTypes.Email)?.Value;

        return Task.FromResult(
            allowedEmails.Contains(userEmail, StringComparer.OrdinalIgnoreCase));
    }
}
```

### 1.5 User Targeting

Target specific users, groups, or segments.

```json
{
  "id": "premium-analytics",
  "enabled": true,
  "conditions": {
    "client_filters": [
      {
        "name": "Microsoft.Targeting",
        "parameters": {
          "Audience": {
            "Users": ["user1@contoso.com", "user2@contoso.com"],
            "Groups": [
              {
                "Name": "Ring1",
                "Percentage": 50
              },
              {
                "Name": "Internal",
                "Percentage": 100
              }
            ],
            "DefaultRolloutPercentage": 10
          }
        }
      }
    ]
  }
}
```

**Targeting audience model:**

```
              ┌─────────────────────┐
              │   Feature Flag      │
              │  "premium-analytics" │
              └──────────┬──────────┘
                         │
              ┌──────────▼──────────┐
              │    Audience Rule    │
              └──────────┬──────────┘
                         │
          ┌──────────────┼──────────────┐
          │              │              │
    ┌─────▼─────┐ ┌─────▼─────┐ ┌─────▼─────┐
    │   Users   │ │  Groups   │ │  Default  │
    │ (explicit)│ │ (rollout) │ │ (fallback)│
    └───────────┘ └───────────┘ └───────────┘
```

---

## 2. Feature Management Library

The `Microsoft.FeatureManagement` NuGet package provides a robust API for feature flag consumption in .NET applications.

### 2.1 IFeatureManager

Core interface for checking feature state.

```csharp
//注入
public class MyService
{
    private readonly IFeatureManager _featureManager;

    public MyService(IFeatureManager featureManager)
    {
        _featureManager = featureManager;
    }

    public async Task ProcessRequest()
    {
        if (await _featureManager.IsEnabledAsync("new-checkout-flow"))
        {
            // New checkout logic
        }
        else
        {
            // Legacy checkout logic
        }
    }
}
```

### 2.2 FeatureFlagAttribute

Declarative feature gating on MVC actions/controllers.

```csharp
[FeatureFlag("admin-dashboard")]
[ApiController]
[Route("api/[controller]")]
public class AdminController : ControllerBase
{
    [HttpGet("users")]
    public IActionResult GetUsers()
    {
        return Ok(new[] { "user1", "user2" });
    }

    [HttpPost("users")]
    [FeatureFlag("user-management")]
    public IActionResult CreateUser([FromBody] UserDto user)
    {
        return CreatedAtAction(nameof(GetUsers), user);
    }
}
```

### 2.3 MVC Integration

```csharp
// Program.cs
builder.Services.AddFeatureManagement();

// Controller
public class HomeController : Controller
{
    private readonly IFeatureManager _featureManager;

    public HomeController(IFeatureManager featureManager)
    {
        _featureManager = featureManager;
    }

    public async Task<IActionResult> Index()
    {
        ViewBag.ShowNewUI = await _featureManager
            .IsEnabledAsync("redesigned-dashboard");
        return View();
    }
}
```

### 2.4 Razor Views

```html
@* Views/Home/Index.cshtml *@

@if (Feature.IsEnabled("new-checkout-flow"))
{
    <partial name="_NewCheckoutPartial" />
}
else
{
    <partial name="_LegacyCheckoutPartial" />
}
```

```html
@* Inject for use in layouts *@
@inject IFeatureManager FeatureManager

@if (await FeatureManager.IsEnabledAsync("dark-mode"))
{
    <link rel="stylesheet" href="~/css/dark.css" />
}
```

---

## 3. Configuration Snapshots

Snapshots capture the state of all configuration settings at a point in time. They act as a "save point" for your configuration.

### 3.1 What They Are

| Aspect | Description |
|--------|-------------|
| Purpose | Preserve configuration state for recovery |
| Scope | All key-values in the store at creation time |
| Versioning | Each snapshot has a unique version number |
| Immutable | Once created, snapshot content cannot be changed |

### 3.2 When to Use Them

- Before a major configuration change (regression safety net)
- Compliance requirements (audit trail)
- Deployment rollback scenarios
- Environment cloning (dev → staging)

### 3.3 Creating Snapshots

**Azure Portal:**
1. Navigate to App Configuration → **Snapshots**
2. Click **Create**
3. Provide a snapshot name and optional description

**Azure CLI:**

```bash
az appconfig snapshot create \
  --name "pre-release-v2" \
  --store "my-app-config" \
  --resource-group "my-rg" \
  --description "Snapshot before v2 release"
```

**REST API:**

```http
PUT https://my-app-config.azconfig.io/snapshots/pre-release-v2?api-version=1.0
Content-Type: application/json

{
  "status": "ready",
  "filters": [
    {
      "key": "*",
      "label": "production"
    }
  ]
}
```

### 3.4 Restoring from Snapshots

```bash
# List available snapshots
az appconfig snapshot list \
  --store "my-app-config" \
  --resource-group "my-rg"

# Restore snapshot contents back to config store
az appconfig snapshot restore \
  --name "pre-release-v2" \
  --store "my-app-config" \
  --resource-group "my-rg"
```

**Restore process:**

```
┌──────────────┐     restore     ┌──────────────┐
│   Snapshot   │ ──────────────► │ Config Store │
│ (immutable)  │                 │  (current)   │
└──────────────┘                 └──────┬───────┘
                                        │
                               ┌────────▼────────┐
                               │  App Instances   │
                               │ (auto-reload)    │
                               └─────────────────┘
```

---

## 4. Multi-Region Configuration

### 4.1 Geo-Replication

App Configuration supports up to **5 replicas** per instance. Replicas provide:

| Capability | Description |
|------------|-------------|
| Read throughput | Distribute read traffic across replicas |
| Low latency | Users read from nearest replica |
| Resilience | Write to primary; reads available from replicas |

### 4.2 Setup

```bash
# Create primary instance
az appconfig create \
  --name "my-app-config" \
  --resource-group "my-rg" \
  --location "eastus" \
  --sku "standard"

# Add replica
az appconfig replica create \
  --name "my-app-config-replica1" \
  --endpoint "my-app-config" \
  --resource-group "my-rg" \
  --location "westus2"
```

### 4.3 Failover

```
                    ┌────────────────────┐
                    │   Load Balancer    │
                    └─────────┬──────────┘
                              │
              ┌───────────────┼───────────────┐
              │               │               │
     ┌────────▼───────┐ ┌────▼──────┐ ┌─────▼───────┐
     │ Primary (East) │ │ Replica   │ │ Replica     │
     │   Read/Write   │ │ (West)    │ │ (EU West)   │
     │                │ │  Read     │ │   Read      │
     └────────────────┘ └──────────┘ └─────────────┘
```

### 4.4 Conflict Resolution

| Scenario | Resolution |
|----------|------------|
| Primary fails | Promote a replica to primary |
| Write conflict | Only primary accepts writes |
| Network partition | Reads continue from reachable replicas |
| Region outage | DNS failover to healthy replica |

```bash
# Promote replica to primary
az appconfig replica list \
  --store "my-app-config" \
  --resource-group "my-rg"
```

---

## 5. Configuration Encryption

### 5.1 Key Vault References

Embed secrets directly in App Configuration without exposing values.

```bash
# Set a Key Vault reference
az appconfig kv set-key \
  --name "my-app-config" \
  --key "ConnectionStrings:DbPassword" \
  --value "@Microsoft.KeyVault(VaultName=my-vault;SecretName=db-password)" \
  --content-type "application/vnd.microsoft.appconfig.keyvaultref+json;charset=utf-8"
```

**Resolution flow:**

```
App Configuration          Key Vault
┌─────────────────┐       ┌─────────────────┐
│ Key: DB_PASS    │       │ Secret:         │
│ Value:          │       │  db-password    │
│ @Microsoft.     │──────►│                 │
│  KeyVault(      │ fetch │  Value:         │
│   VaultName=... │◄──────│   "S3cr3t!"    │
│   SecretName=..)│       │                 │
└─────────────────┘       └─────────────────┘
```

### 5.2 Encryption at Rest

| Layer | Mechanism |
|-------|-----------|
| Storage | AES-256 encryption (platform-managed) |
| Transit | TLS 1.2+ |
| Customer-managed | Azure Key Vault CMK (Standard tier) |
| At-rest encryption | Automatic for all tiers |

### 5.3 Client-Side Encryption

```csharp
// Encrypt before storing
var encrypted = ProtectedData.Protect(
    Encoding.UTF8.GetBytes(secretValue),
    optionalEntropy: null,
    scope: DataProtectionScope.CurrentUser);

var base64Encrypted = Convert.ToBase64String(encrypted);

await configClient.SetConfigurationSettingAsync(
    new ConfigurationSetting("secure:key", base64Encrypted));
```

---

## 6. App Configuration Provider for .NET

### 6.1 Microsoft.Azure.AppConfiguration.AspNetCore

```bash
dotnet add package Microsoft.Azure.AppConfiguration.AspNetCore
dotnet add package Microsoft.Azure.AppConfiguration.ConfigurationFactory
```

### 6.2 IConfigurationBuilder

```csharp
// Program.cs
var builder = WebApplication.CreateBuilder(args);

builder.Configuration.AddAzureAppConfiguration(options =>
{
    options.Connect(builder.Configuration.GetConnectionString("AppConfig"))
        .Select("application:*", LabelFilter.Null)
        .Select("feature_flags:*", "production")
        .ConfigureKeyVault(kv => kv.RegisterSecretManager())
        .ConfigureRefresh(refresh =>
        {
            refresh.Register("settings:sentinel", refreshAll: true)
                   .SetCacheExpiration(TimeSpan.FromMinutes(5));
        });
});

var app = builder.Build();
```

### 6.3 Caching

The provider caches configuration values in memory to minimize calls to App Configuration.

| Setting | Default | Description |
|---------|---------|-------------|
| Cache expiration | 30 seconds | How long values are cached |
| Sentinel key | — | Key that triggers cache invalidation |
| Refresh interval | 5 minutes | Minimum time between refresh checks |

### 6.4 Reload

```csharp
// Enable configuration reload
builder.Configuration.AddAzureAppConfiguration(options =>
{
    options.Connect(connectionString)
        .ConfigureRefresh(refresh =>
        {
            refresh.Register("sentinel", refreshAll: true);
        });
});

// Middleware to trigger refresh
app.UseAzureAppConfiguration();

// Read updated values (automatically refreshed)
app.MapGet("/config/{key}", (string key, IConfiguration config) =>
{
    return Results.Ok(config[key]);
});
```

---

## 7. Labels and Namespaces

### 7.1 Label-Based Environment Separation

Labels provide a namespace within a single key.

```
┌─────────────────────────────────────────────────┐
│  Key                    │ Label      │ Value    │
├─────────────────────────┼────────────┼──────────┤
│ app:timeout            │ dev        │ 30       │
│ app:timeout            │ staging    │ 15       │
│ app:timeout            │ production │ 5        │
│ app:endpoint           │ dev        │ http://..│
│ app:endpoint           │ staging    │ http://..│
│ app:endpoint           │ production │ https://.│
└─────────────────────────────────────────────────┘
```

### 7.2 Hierarchical Keys

Organize configuration using key separators.

```
application:frontend:theme          = dark
application:frontend:language       = en
application:backend:timeout         = 30
application:backend:max-retries     = 3
application:logging:level           = Information
```

**Separator options:**

| Character | Example | Recommendation |
|-----------|---------|----------------|
| `:` (colon) | `app:section:key` | Recommended for .NET |
| `/` (slash) | `app/section/key` | Common in other platforms |
| `__` (double underscore) | `app__section__key` | Env variable compatible |
| `.` (dot) | `app.section.key` | Legacy .NET config |

### 7.3 Hierarchical Mapping

```csharp
// Bind to strongly-typed config
public class AppSettings
{
    public FrontendSettings Frontend { get; set; }
    public BackendSettings Backend { get; set; }
    public LoggingSettings Logging { get; set; }
}

builder.Configuration.GetSection("application").Bind(appSettings);
```

---

## 8. Configuration Drift Detection

### 8.1 Monitoring Changes

```bash
# Enable diagnostic logging
az monitor diagnostic-settings create \
  --name "appconfig-diagnostics" \
  --resource "/subscriptions/{sub}/resourceGroups/{rg}/providers/Microsoft.AppConfiguration/configurationStores/{name}" \
  --workspace "/subscriptions/{sub}/resourceGroups/{rg}/providers/Microsoft.OperationalInsights/workspaces/{la}" \
  --logs '[
    {
      "category": "OperationStatus",
      "enabled": true,
      "retentionPolicy": { "enabled": true, "days": 30 }
    }
  ]'
```

### 8.2 Alerts

```bash
# Create alert for configuration changes
az monitor metrics alert create \
  --name "config-changed-alert" \
  --resource-group "my-rg" \
  --scopes "/subscriptions/{sub}/resourceGroups/{rg}/providers/Microsoft.AppConfiguration/configurationStores/my-app-config" \
  --condition "count requests/count > 0" \
  --description "Alert when configuration is modified" \
  --action "/subscriptions/{sub}/resourceGroups/{rg}/providers/microsoft.insights/actionGroups/my-action-group"
```

### 8.3 Audit Logs

| Log Category | Event Types |
|-------------|-------------|
| OperationStatus | Create, Update, Delete, Read |
| Audit | Key modifications, Label changes |
| ResourceHealth | Availability, Performance |

```kusto
// Kusto query for config changes
AzureDiagnostics
| where ResourceProvider == "MICROSOFT.APPCONFIGURATION"
| where Category == "OperationStatus"
| where OperationName in ("Write", "Delete")
| project TimeGenerated, OperationName, Resource, CallerIpAddress, ResultType
| order by TimeGenerated desc
```

---

## 9. Integration Patterns

### 9.1 App Config + Key Vault

```
┌──────────────────┐
│  App Config      │
│                  │
│  Key: DB_CONN    │──────────┐
│  Key: API_KEY    │────┐    │
│  Key: SECRET_REF │─┐  │    │
│   (@KeyVault)    │ │  │    │
└──────────────────┘ │  │    │
                     │  │    │
              ┌──────▼──▼────▼──────┐
              │     Key Vault       │
              │                     │
              │  Secret: db-conn    │
              │  Secret: api-key    │
              └──────────┬──────────┘
                         │
              ┌──────────▼──────────┐
              │   .NET Application  │
              │                     │
              │  Resolved values    │
              └─────────────────────┘
```

### 9.2 App Config + Feature Flags + Deployment Slots

```
┌─────────────────────────────────────────────────┐
│               Azure App Service                 │
│                                                 │
│  ┌──────────┐    ┌──────────┐                   │
│  │Production│    │  Staging │                   │
│  │  Slot    │    │   Slot   │                   │
│  └────┬─────┘    └────┬─────┘                   │
│       │               │                         │
└───────┼───────────────┼─────────────────────────┘
        │               │
        ▼               ▼
┌──────────────┐ ┌──────────────┐
│ App Config   │ │ App Config   │
│ Label: prod  │ │ Label: stage │
│              │ │              │
│ Feature A: ✓ │ │ Feature A: ✗ │
│ Feature B: ✗ │ │ Feature B: ✓ │
└──────────────┘ └──────────────┘
```

---

## 10. Architecture Diagram

```
┌─────────────────────────────────────────────────────────────────────────┐
│                        AZURE APP CONFIGURATION                         │
│                                                                         │
│  ┌─────────────────────────────────────────────────────────────────┐   │
│  │                    Feature Flags                                │   │
│  │  ┌─────────────┐ ┌─────────────┐ ┌─────────────┐              │   │
│  │  │ new-checkout│ │  redesign   │ │  premium    │              │   │
│  │  │ ON/OFF      │ │ 25% rollout │ │  targeting  │              │   │
│  │  └──────┬──────┘ └──────┬──────┘ └──────┬──────┘              │   │
│  └─────────┼───────────────┼───────────────┼─────────────────────┘   │
│            │               │               │                         │
│  ┌─────────┼───────────────┼───────────────┼─────────────────────┐   │
│  │         │  Configuration Keys & Labels  │                     │   │
│  │  ┌──────▼──────┐ ┌──────▼──────┐ ┌─────▼──────┐             │   │
│  │  │ app:timeout │ │ app:endpoint│ │ app:db     │             │   │
│  │  │ dev=30      │ │ prod=https  │ │ @KeyVault  │             │   │
│  │  │ prod=5      │ │             │ │            │             │   │
│  │  └─────────────┘ └─────────────┘ └─────┬──────┘             │   │
│  └────────────────────────────────────────┼─────────────────────┘   │
│                                           │                         │
│  ┌────────────────────────────────────────┼─────────────────────┐   │
│  │              Snapshots                 │                     │   │
│  │  ┌─────────────────┐ ┌─────────────────▼───┐                │   │
│  │  │ pre-release-v1  │ │ pre-release-v2      │                │   │
│  │  │ (immutable)     │ │ (immutable)         │                │   │
│  │  └─────────────────┘ └─────────────────────┘                │   │
│  └──────────────────────────────────────────────────────────────┘   │
└───────────────────────────────┬─────────────────────────────────────┘
                                │
        ┌───────────────────────┼───────────────────────┐
        │                       │                       │
        ▼                       ▼                       ▼
┌───────────────┐     ┌───────────────┐       ┌───────────────┐
│  Key Vault    │     │  Geo-Replica  │       │  Geo-Replica  │
│  (secrets)    │     │  (East US)    │       │  (West EU)    │
│               │     │  PRIMARY      │       │  READ-ONLY    │
│  db-password  │     └───────┬───────┘       └───────┬───────┘
│  api-key      │             │                       │
│  cert-thumb   │             └───────────┬───────────┘
└───────┬───────┘                         │
        │                                 │
        ▼                                 ▼
┌─────────────────────────────────────────────────────────────────┐
│                     .NET APPLICATIONS                           │
│                                                                 │
│  ┌──────────────┐  ┌──────────────┐  ┌──────────────┐         │
│  │  Instance 1  │  │  Instance 2  │  │  Instance 3  │         │
│  │  (East US)   │  │  (East US)   │  │  (West EU)   │         │
│  │              │  │              │  │              │         │
│  │ IFeatureMgr  │  │ IFeatureMgr  │  │ IFeatureMgr  │         │
│  │ IConfiguration│  │ IConfiguration│  │ IConfiguration│        │
│  │ Cache: 5min  │  │ Cache: 5min  │  │ Cache: 5min  │         │
│  └──────────────┘  └──────────────┘  └──────────────┘         │
│                                                                 │
│  Features: ─────────────────────────────────────────────────   │
│  ├── new-checkout-flow: ON       (all instances)              │
│  ├── redesigned-dashboard: 25%   (traffic split)              │
│  └── premium-analytics: internal (targeting rule)              │
└─────────────────────────────────────────────────────────────────┘
```

---

## 11. Key Concepts for AZ-204

### Exam Topics Summary

| Topic | Details |
|-------|---------|
| Feature Flags | Simple, percentage, time-window, custom filters, targeting |
| Feature Management | IFeatureManager, FeatureFlagAttribute, MVC/Razor integration |
| Snapshots | Creation, restoration, use cases |
| Multi-Region | Geo-replication, failover, conflict resolution |
| Encryption | Key Vault references, CMK, at-rest encryption |
| Labels | Environment separation, hierarchical keys |
| Drift Detection | Diagnostic logs, alerts, audit queries |
| Provider | AddAzureAppConfiguration, caching, reload, sentinel keys |

### Common Exam Scenarios

| Scenario | Solution |
|----------|----------|
| Gradually roll out to 10% of users | Percentage-based feature flag |
| Feature active only during holidays | Time-window feature flag |
| Store database password securely | Key Vault reference in App Config |
| Capture config before release | Create a snapshot |
| Detect unauthorized config changes | Diagnostic settings + alert rules |
| Load config from nearest region | Geo-replicated App Config + replica |
| Auto-reload config without restart | Sentinel key + ConfigureRefresh |
| Target specific users | Targeting feature filter |
| Separate dev/prod configs | Label-based filtering |
| Prevent config regression | Snapshot + restore workflow |

### Key Commands Cheat Sheet

| Task | Command |
|------|---------|
| Create App Config | `az appconfig create` |
| Set key-value | `az appconfig kv set-key` |
| List feature flags | `az appconfig kv list --key "feature_management/*"` |
| Create snapshot | `az appconfig snapshot create` |
| Restore snapshot | `az appconfig snapshot restore` |
| Add replica | `az appconfig replica create` |
| Set Key Vault ref | `az appconfig kv set-key --value "@Microsoft.KeyVault(...)"` |
| Enable diagnostics | `az monitor diagnostic-settings create` |

### .NET Package Reference

| Package | Purpose |
|---------|---------|
| `Microsoft.Azure.AppConfiguration.AspNetCore` | ASP.NET Core integration |
| `Microsoft.FeatureManagement` | Feature flag management |
| `Microsoft.FeatureManagement.AspNetCore` | MVC/Razor feature gating |
| `Azure.Identity` | DefaultAzureCredential for auth |

### Best Practices

1. **Use sentinel keys** for cache invalidation (e.g., `settings:sentinel`)
2. **Set short cache expiration** (1-5 minutes) for critical settings
3. **Use labels** for environment separation, not separate stores
4. **Create snapshots** before major changes for rollback capability
5. **Enable geo-replication** for production workloads
6. **Reference Key Vault** for secrets instead of storing them directly
7. **Use hierarchical keys** (`:` separator) for organized configuration
8. **Monitor drift** with diagnostic logs and alerts
9. **Test feature flags** in staging before production rollout
10. **Document snapshot purposes** for team visibility
