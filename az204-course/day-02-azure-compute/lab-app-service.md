# Lab: Deploy a Web App to Azure App Service

## Title
Create, Deploy, and Manage an Azure App Service Web App

## Objective
- Create an App Service Plan and Web App
- Deploy a .NET application from a GitHub repo
- Configure application settings
- Create and use deployment slots
- Perform a slot swap for zero-downtime deployment

## Architecture

```
┌──────────────────────────────────────────────────────┐
│              Resource Group: az204-day02-rg           │
│                                                      │
│  ┌────────────────────────────────────────────────┐  │
│  │         App Service Plan: az204-asp (S1)       │  │
│  │                                                │  │
│  │  ┌──────────────────────────────────────────┐  │  │
│  │  │  Web App: az204-webapp-<unique>          │  │  │
│  │  │                                          │  │  │
│  │  │  Production Slot ◄──── swap ────► Staging│  │  │
│  │  │  (az204-webapp-<unique>.azurewebsites.net│) │  │
│  │  └──────────────────────────────────────────┘  │  │
│  └────────────────────────────────────────────────┘  │
└──────────────────────────────────────────────────────┘
```

## Prerequisites
- Azure CLI installed and logged in (`az login`)
- An active Azure subscription
- Git installed locally

---

## Step 1 — Create a Resource Group

```bash
az group create \
  --name az204-day02-rg \
  --location eastus
```

Verify:
```bash
az group show --name az204-day02-rg --query "{name:name, location:location}" --output table
```

---

## Step 2 — Create an App Service Plan

```bash
az appservice plan create \
  --name az204-asp \
  --resource-group az204-day02-rg \
  --sku S1 \
  --is-linux
```

This creates a Standard S1 Linux plan. S1 supports deployment slots and auto-scale.

Verify:
```bash
az appservice plan show \
  --name az204-asp \
  --resource-group az204-day02-rg \
  --query "{name:name, sku:sku.name, kind:kind, status:status}" \
  --output table
```

---

## Step 3 — Create a Web App

Replace `<unique>` with a unique suffix (e.g., your initials + random digits):

```bash
az webapp create \
  --name az204-webapp-<unique> \
  --resource-group az204-day02-rg \
  --plan az204-asp \
  --runtime "DOTNETCORE:8.0"
```

Verify the app is running:
```bash
az webapp show \
  --name az204-webapp-<unique> \
  --resource-group az204-day02-rg \
  --query "{name:name, state:state, defaultHostName:defaultHostName}" \
  --output table
```

Browse to `https://az204-webapp-<unique>.azurewebsites.net` — you should see a default placeholder page.

---

## Step 4 — Deploy Code from GitHub

We'll deploy a sample .NET app from Microsoft's sample repository:

```bash
az webapp deployment source config \
  --name az204-webapp-<unique> \
  --resource-group az204-day02-rg \
  --repo-url "https://github.com/Azure-Samples/dotnetcore-docs-hello-world" \
  --branch master \
  --manual-integration
```

Wait for deployment to complete (1-2 minutes), then browse to your app URL again. You should see "Hello World!" from the .NET sample app.

Verify deployment status:
```bash
az webapp deployment source show \
  --name az204-webapp-<unique> \
  --resource-group az204-day02-rg \
  --query "{repoUrl:repoUrl, branch:branch, isManualIntegration:isManualIntegration}" \
  --output table
```

---

## Step 5 — Configure Application Settings

Application settings are environment variables available to your app at runtime.

```bash
# Add a single setting
az webapp config appsettings set \
  --name az204-webapp-<unique> \
  --resource-group az204-day02-rg \
  --settings ENVIRONMENT=Production APP_VERSION=1.0.0

# View all settings
az webapp config appsettings list \
  --name az204-webapp-<unique> \
  --resource-group az204-day02-rg \
  --output table
```

Add a connection string:
```bash
az webapp config connection-string set \
  --name az204-webapp-<unique> \
  --resource-group az204-day02-rg \
  --connection-string-type SQLAzure \
  --settings DefaultConnection="Server=myserver;Database=mydb;User=admin;Password=placeholder"
```

---

## Step 6 — Enable Diagnostic Logging

```bash
# Enable application logging
az webapp log config \
  --name az204-webapp-<unique> \
  --resource-group az204-day02-rg \
  --application-logging filesystem \
  --level information

# Stream live logs (press Ctrl+C to stop)
az webapp log tail \
  --name az204-webapp-<unique> \
  --resource-group az204-day02-rg
```

---

## Step 7 — Create a Deployment Slot

Deployment slots let you deploy to a staging environment and swap to production with zero downtime.

```bash
az webapp deployment slot create \
  --name az204-webapp-<unique> \
  --resource-group az204-day02-rg \
  --slot staging
```

Verify the slot:
```bash
az webapp deployment slot list \
  --name az204-webapp-<unique> \
  --resource-group az204-day02-rg \
  --output table
```

The staging slot has its own URL: `https://az204-webapp-<unique>-staging.azurewebsites.net`

---

## Step 8 — Deploy Different Code to Staging

Set a different app setting on the staging slot to simulate a new version:

```bash
az webapp config appsettings set \
  --name az204-webapp-<unique> \
  --resource-group az204-day02-rg \
  --slot staging \
  --settings APP_VERSION=2.0.0
```

Deploy the same repo to staging (in a real scenario, you'd deploy a new branch):
```bash
az webapp deployment source config \
  --name az204-webapp-<unique> \
  --resource-group az204-day02-rg \
  --slot staging \
  --repo-url "https://github.com/Azure-Samples/dotnetcore-docs-hello-world" \
  --branch master \
  --manual-integration
```

---

## Step 9 — Swap Staging to Production

```bash
az webapp deployment slot swap \
  --name az204-webapp-<unique> \
  --resource-group az204-day02-rg \
  --slot staging \
  --target-slot production
```

After the swap:
- Production now has what was in staging
- Staging now has what was in production
- If something goes wrong, swap again to roll back

Verify the swap by checking app settings on production:
```bash
az webapp config appsettings list \
  --name az204-webapp-<unique> \
  --resource-group az204-day02-rg \
  --query "[?name=='APP_VERSION']" \
  --output table
```

---

## Step 10 — Scale the App

### Scale Up (change plan tier):
```bash
az appservice plan update \
  --name az204-asp \
  --resource-group az204-day02-rg \
  --sku P1v3
```

### Scale Out (add instances):
```bash
az webapp update \
  --name az204-webapp-<unique> \
  --resource-group az204-day02-rg \
  --set siteConfig.numberOfWorkers=3
```

### Configure Auto-Scale:
```bash
az monitor autoscale create \
  --resource-group az204-day02-rg \
  --resource az204-asp \
  --resource-type Microsoft.Web/serverfarms \
  --min-count 1 \
  --max-count 5 \
  --count 1

az monitor autoscale rule create \
  --resource-group az204-day02-rg \
  --autoscale-name az204-asp \
  --condition "CpuPercentage > 70 avg 5m" \
  --scale out 1
```

---

## Cleanup

```bash
az group delete --name az204-day02-rg --yes --no-wait
```

---

## Expected Outcomes

| Task | Status |
|------|--------|
| Created App Service Plan (S1 Linux) | ✅ |
| Created Web App with .NET 8.0 runtime | ✅ |
| Deployed code from GitHub | ✅ |
| Configured app settings and connection strings | ✅ |
| Enabled diagnostic logging | ✅ |
| Created staging deployment slot | ✅ |
| Performed slot swap | ✅ |
| Configured scaling | ✅ |

---

Next: [Lab — Azure Functions](./lab-functions.md)
