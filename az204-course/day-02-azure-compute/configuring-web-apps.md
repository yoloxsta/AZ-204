# Configuring Web App Settings — Deep Dive

## Overview

This module covers the 7 key configuration areas for Azure App Service web apps that appear on the AZ-204 exam.

---

## 1. Application Settings

Application settings are environment variables passed to your app at runtime. They override values in `appsettings.json` (for .NET) or equivalent config files.

```bash
# Set a single setting
az webapp config appsettings set \
  --name myapp --resource-group myRG \
  --settings DB_HOST=myserver.database.windows.net

# Set multiple settings
az webapp config appsettings set \
  --name myapp --resource-group myRG \
  --settings KEY1=value1 KEY2=value2

# List all settings
az webapp config appsettings list \
  --name myapp --resource-group myRG --output table

# Delete a setting
az webapp config appsettings delete \
  --name myapp --resource-group myRG \
  --setting-names KEY1
```

In .NET, access via `Environment.GetEnvironmentVariable("DB_HOST")` or `IConfiguration`.

In Node.js, access via `process.env.DB_HOST`.

### Slot-Sticky Settings

Settings can be marked as "slot sticky" — they stay with the slot during a swap instead of moving with the app code.

Use cases for sticky settings:
- `ASPNETCORE_ENVIRONMENT` = "Staging" on the staging slot
- Database connection strings pointing to a staging database
- Feature flags specific to an environment

```bash
az webapp config appsettings set \
  --name myapp --resource-group myRG \
  --slot-settings ASPNETCORE_ENVIRONMENT=Staging
```

---

## 2. General Settings

General settings control the runtime stack, platform, and debugging options.

```bash
# Set runtime stack
az webapp config set \
  --name myapp --resource-group myRG \
  --linux-fx-version "DOTNETCORE|8.0"

# Enable/disable features
az webapp config set \
  --name myapp --resource-group myRG \
  --always-on true \
  --http20-enabled true \
  --min-tls-version 1.2 \
  --ftps-state Disabled
```

Key general settings:
- **Stack and version** — .NET, Node, Python, Java, PHP
- **Platform** — 32-bit vs 64-bit
- **Always On** — keeps the app loaded (required for WebJobs, recommended for production)
- **HTTP version** — HTTP/1.1 or HTTP/2
- **ARR affinity** — session affinity (disable for stateless apps for better load distribution)
- **Minimum TLS version** — enforce TLS 1.2
- **FTPS state** — Disabled, FtpsOnly, or AllAllowed

---

## 3. Path Mappings

Path mappings configure handler mappings and virtual directories.

### Handler Mappings (Windows)
Map file extensions to custom script processors:
- Extension: `*.php`
- Script processor: `D:\home\site\wwwroot\bin\php-cgi.exe`

### Virtual Applications and Directories
Map URL paths to physical directories:
- Virtual path: `/api`
- Physical path: `site\wwwroot\api`
- Mark as application: Yes (runs as a separate application)

### Azure Storage Mounts (Linux/Container)
Mount Azure Storage as a local directory in your container:

```bash
az webapp config storage-account add \
  --name myapp --resource-group myRG \
  --custom-id MyStorageMount \
  --storage-type AzureBlob \
  --share-name mycontainer \
  --account-name mystorageaccount \
  --access-key <key> \
  --mount-path /mnt/storage
```

---

## 4. Diagnostic Logging

App Service supports several log types:

| Log Type | Platform | Storage | Description |
|----------|----------|---------|-------------|
| Application logging | Windows/Linux | File system, Blob | App-generated logs |
| Web server logging | Windows | File system, Blob | Raw HTTP request data |
| Detailed error messages | Windows | File system | HTML error pages |
| Failed request tracing | Windows | File system | Detailed trace of failed requests |
| Deployment logging | Windows/Linux | File system | Deployment process logs |

```bash
# Enable application logging
az webapp log config \
  --name myapp --resource-group myRG \
  --application-logging filesystem \
  --level information

# Enable web server logging
az webapp log config \
  --name myapp --resource-group myRG \
  --web-server-logging filesystem

# Stream live logs
az webapp log tail --name myapp --resource-group myRG

# Download logs as ZIP
az webapp log download --name myapp --resource-group myRG --log-file logs.zip
```

Log levels: Error, Warning, Information, Verbose.

File system logging auto-disables after 12 hours. Use Blob storage for persistent logging.

---

## 5. Certificates

App Service supports TLS/SSL certificates for custom domains.

### Certificate Options

| Option | Use Case |
|--------|----------|
| App Service Managed Certificate | Free, auto-renewed, basic scenarios |
| App Service Certificate | Purchased through Azure, stored in Key Vault |
| Third-party certificate | Upload your own PFX/PEM |
| Key Vault certificate | Reference a certificate stored in Key Vault |

```bash
# Upload a PFX certificate
az webapp config ssl upload \
  --name myapp --resource-group myRG \
  --certificate-file mycert.pfx \
  --certificate-password <password>

# Create a managed certificate (free)
az webapp config ssl create \
  --name myapp --resource-group myRG \
  --hostname www.example.com

# Bind certificate to custom domain
az webapp config ssl bind \
  --name myapp --resource-group myRG \
  --certificate-thumbprint <thumbprint> \
  --ssl-type SNI
```

### TLS Mutual Authentication (mTLS)
Require client certificates for authentication:

```bash
az webapp update \
  --name myapp --resource-group myRG \
  --set clientCertEnabled=true \
  --set clientCertMode=Required
```

---

## 6. Feature Management

App Service integrates with Azure App Configuration for feature flags, but also supports basic feature management through application settings.

Pattern: Use app settings as simple feature toggles:
```bash
az webapp config appsettings set \
  --name myapp --resource-group myRG \
  --settings FEATURE_NEW_UI=true FEATURE_BETA_API=false
```

For advanced feature management (percentage rollout, targeting, time windows), use Azure App Configuration with the Feature Management SDK (covered in Day 12).

---

## 7. Connection Strings

Connection strings are similar to app settings but have a specific type and are exposed differently to the runtime.

```bash
az webapp config connection-string set \
  --name myapp --resource-group myRG \
  --connection-string-type SQLAzure \
  --settings DefaultConnection="Server=tcp:myserver.database.windows.net;Database=mydb;..."
```

Connection string types: SQLAzure, SQLServer, MySQL, PostgreSQL, Custom, NotificationHub, ServiceBus, EventHub, DocDb, RedisCache.

In .NET, connection strings are available via `IConfiguration.GetConnectionString("DefaultConnection")`.

Connection strings can also be slot-sticky:
```bash
az webapp config connection-string set \
  --name myapp --resource-group myRG \
  --slot staging \
  --slot-settings StagingDb="Server=staging-server;..."
```

---

## Key Exam Points

1. App settings override `appsettings.json` values at runtime
2. Slot-sticky settings stay with the slot during swaps
3. Always On should be enabled for production apps and WebJobs
4. File system logging auto-disables after 12 hours
5. Managed certificates are free but limited to standard domains
6. Connection strings have types that affect how they're exposed to the runtime
7. ARR affinity should be disabled for stateless apps
