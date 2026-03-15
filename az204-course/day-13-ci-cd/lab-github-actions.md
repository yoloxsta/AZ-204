# Lab: CI/CD with GitHub Actions & Bicep

## Title
Build a CI/CD Pipeline for Azure App Service

## Objective
- Create a Bicep template for infrastructure
- Deploy infrastructure with Azure CLI
- Create a GitHub Actions workflow for CI/CD
- Deploy a .NET app to App Service via GitHub Actions
- Implement staging slot deployment with swap

## Architecture

```
┌──────────────────────────────────────────────────────────┐
│                    GitHub Repository                      │
│                                                          │
│  .github/workflows/deploy.yml                            │
│  ┌────────────────────────────────────────────────────┐  │
│  │  Trigger: push to main                             │  │
│  │                                                    │  │
│  │  Job 1: Build                                      │  │
│  │  ├── Checkout code                                 │  │
│  │  ├── Setup .NET 8                                  │  │
│  │  ├── Restore, Build, Test                          │  │
│  │  └── Publish artifact                              │  │
│  │                                                    │  │
│  │  Job 2: Deploy Infrastructure                      │  │
│  │  ├── Login to Azure                                │  │
│  │  └── Deploy Bicep template                         │  │
│  │                                                    │  │
│  │  Job 3: Deploy App                                 │  │
│  │  ├── Deploy to staging slot                        │  │
│  │  └── Swap staging → production                     │  │
│  └────────────────────────────────────────────────────┘  │
│                                                          │
│  infra/main.bicep                                        │
│  src/MyApp/                                              │
└──────────────────────────────────────────────────────────┘
         │
         ▼
┌──────────────────────────────────────────────────────────┐
│              Resource Group: az204-day13-rg               │
│                                                          │
│  App Service Plan (S1) ──► Web App ──► Staging Slot      │
└──────────────────────────────────────────────────────────┘
```

## Prerequisites
- Azure CLI installed and logged in
- GitHub account with a repository
- .NET 8 SDK installed

---

## Step 1 — Create the Project Structure

```bash
mkdir az204-cicd-lab
cd az204-cicd-lab
mkdir -p infra src .github/workflows
```

---

## Step 2 — Create the Bicep Template

Create `infra/main.bicep`:

```bicep
@description('Base name for all resources')
param appName string

@description('Location for all resources')
param location string = resourceGroup().location

@description('App Service Plan SKU')
param sku string = 'S1'

resource appServicePlan 'Microsoft.Web/serverfarms@2022-09-01' = {
  name: '${appName}-plan'
  location: location
  sku: {
    name: sku
  }
  kind: 'linux'
  properties: {
    reserved: true
  }
}

```bicep
resource webApp 'Microsoft.Web/sites@2022-09-01' = {
  name: appName
  location: location
  properties: {
    serverFarmId: appServicePlan.id
    siteConfig: {
      linuxFxVersion: 'DOTNETCORE|8.0'
      alwaysOn: true
    }
    httpsOnly: true
  }
}

resource stagingSlot 'Microsoft.Web/sites/slots@2022-09-01' = {
  parent: webApp
  name: 'staging'
  location: location
  properties: {
    serverFarmId: appServicePlan.id
    siteConfig: {
      linuxFxVersion: 'DOTNETCORE|8.0'
    }
  }
}

output appUrl string = 'https://${webApp.properties.defaultHostName}'
output stagingUrl string = 'https://${stagingSlot.properties.defaultHostName}'
output appName string = webApp.name
```

---

## Step 3 — Deploy Infrastructure with Bicep

```bash
az group create --name az204-day13-rg --location eastus

az deployment group create \
  --resource-group az204-day13-rg \
  --template-file infra/main.bicep \
  --parameters appName=az204cicd<unique>
```

Verify:
```bash
az deployment group show \
  --resource-group az204-day13-rg \
  --name main \
  --query "properties.outputs" \
  --output json
```

---

## Step 4 — Create the .NET Application

```bash
cd src
dotnet new web -n MyApp
cd MyApp
```

Replace `Program.cs`:

```csharp
var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();

app.MapGet("/", () => new
{
    message = "Hello from AZ-204 CI/CD Lab!",
    version = Environment.GetEnvironmentVariable("APP_VERSION") ?? "1.0.0",
    environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Production",
    timestamp = DateTime.UtcNow
});

app.MapGet("/health", () => Results.Ok(new { status = "healthy" }));

app.Run();
```

Test locally:
```bash
dotnet run
curl http://localhost:5000/
curl http://localhost:5000/health
```

---

## Step 5 — Create the GitHub Actions Workflow

Create `.github/workflows/deploy.yml`:

```yaml
name: Build and Deploy to Azure

on:
  push:
    branches: [main]
  workflow_dispatch:

env:
  AZURE_WEBAPP_NAME: az204cicd<unique>
  DOTNET_VERSION: '8.0.x'

jobs:
  build:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4

      - name: Setup .NET
        uses: actions/setup-dotnet@v4
        with:
          dotnet-version: ${{ env.DOTNET_VERSION }}

      - name: Restore dependencies
        run: dotnet restore
        working-directory: src/MyApp

      - name: Build
        run: dotnet build --configuration Release --no-restore
        working-directory: src/MyApp

      - name: Publish
        run: dotnet publish --configuration Release --output ./publish
        working-directory: src/MyApp

      - name: Upload artifact
        uses: actions/upload-artifact@v4
        with:
          name: webapp
          path: src/MyApp/publish

  deploy-infra:
    runs-on: ubuntu-latest
    needs: build
    steps:
      - uses: actions/checkout@v4

      - name: Login to Azure
        uses: azure/login@v2
        with:
          creds: ${{ secrets.AZURE_CREDENTIALS }}

      - name: Deploy Bicep
        uses: azure/arm-deploy@v2
        with:
          resourceGroupName: az204-day13-rg
          template: infra/main.bicep
          parameters: appName=${{ env.AZURE_WEBAPP_NAME }}

  deploy-app:
    runs-on: ubuntu-latest
    needs: [build, deploy-infra]
    steps:
      - name: Download artifact
        uses: actions/download-artifact@v4
        with:
          name: webapp

      - name: Login to Azure
        uses: azure/login@v2
        with:
          creds: ${{ secrets.AZURE_CREDENTIALS }}

      - name: Deploy to staging slot
        uses: azure/webapps-deploy@v3
        with:
          app-name: ${{ env.AZURE_WEBAPP_NAME }}
          slot-name: staging
          package: .

      - name: Swap staging to production
        run: |
          az webapp deployment slot swap \
            --name ${{ env.AZURE_WEBAPP_NAME }} \
            --resource-group az204-day13-rg \
            --slot staging \
            --target-slot production
```

---

## Step 6 — Create Azure Service Principal for GitHub

```bash
SUBSCRIPTION_ID=$(az account show --query id --output tsv)

az ad sp create-for-rbac \
  --name "az204-github-actions" \
  --role contributor \
  --scopes /subscriptions/$SUBSCRIPTION_ID/resourceGroups/az204-day13-rg \
  --json-auth
```

Copy the entire JSON output. In your GitHub repository:
1. Go to Settings → Secrets and variables → Actions
2. Click "New repository secret"
3. Name: `AZURE_CREDENTIALS`
4. Value: paste the JSON output

---

## Step 7 — Push and Trigger the Pipeline

```bash
cd ../..  # Back to az204-cicd-lab root
git init
git add .
git commit -m "Initial commit: AZ-204 CI/CD lab"
git remote add origin https://github.com/<your-username>/<your-repo>.git
git branch -M main
git push -u origin main
```

The push to `main` triggers the workflow. Monitor it in GitHub → Actions tab.

---

## Step 8 — Verify Deployment

```bash
# Check production
curl https://az204cicd<unique>.azurewebsites.net/

# Check health endpoint
curl https://az204cicd<unique>.azurewebsites.net/health
```

---

## Step 9 — Manual Deployment (Alternative)

If you don't want to use GitHub Actions, deploy manually:

```bash
cd src/MyApp
dotnet publish -c Release -o ./publish

cd publish
zip -r ../../deploy.zip .
cd ../..

# Deploy to staging
az webapp deployment source config-zip \
  --name az204cicd<unique> \
  --resource-group az204-day13-rg \
  --slot staging \
  --src deploy.zip

# Swap to production
az webapp deployment slot swap \
  --name az204cicd<unique> \
  --resource-group az204-day13-rg \
  --slot staging \
  --target-slot production
```

---

## Cleanup

```bash
# Delete service principal
SP_ID=$(az ad sp list --display-name "az204-github-actions" --query "[0].id" --output tsv)
az ad sp delete --id $SP_ID

# Delete resources
az group delete --name az204-day13-rg --yes --no-wait

# Delete local files
cd ..
rm -rf az204-cicd-lab
```

---

## Expected Outcomes

| Task | Status |
|------|--------|
| Created Bicep template for App Service + staging slot | ✅ |
| Deployed infrastructure with Bicep | ✅ |
| Created .NET web application | ✅ |
| Created GitHub Actions workflow (build, deploy-infra, deploy-app) | ✅ |
| Created service principal for GitHub authentication | ✅ |
| Deployed to staging slot and swapped to production | ✅ |

---

Next: [Day 14 — Final Project](../day-14-final-project/build-cloud-application.md)
