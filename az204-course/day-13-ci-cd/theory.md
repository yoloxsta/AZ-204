# Day 13 — CI/CD & Infrastructure as Code

## What You'll Learn Today

- CI/CD concepts for Azure developers
- GitHub Actions for Azure deployments
- ARM templates and Bicep
- Azure DevOps Pipelines overview
- Deployment strategies

---

## 1. CI/CD Concepts

**Continuous Integration (CI):**
- Automatically build and test code on every commit
- Catch bugs early, ensure code quality
- Run unit tests, linting, security scans

**Continuous Deployment (CD):**
- Automatically deploy to environments after CI passes
- Deploy to staging first, then production
- Use approval gates for production deployments

### Pipeline Stages

```
Code Push ──► Build ──► Test ──► Deploy to Staging ──► Deploy to Production
                                      │                       │
                                  Auto-deploy            Manual approval
```

---

## 2. GitHub Actions

GitHub Actions is a CI/CD platform built into GitHub. Workflows are defined in YAML files.

### Key Concepts

- **Workflow** — automated process defined in `.github/workflows/*.yml`
- **Trigger** — what starts the workflow (push, PR, schedule, manual)
- **Job** — a set of steps that run on the same runner
- **Step** — a single task (run a command, use an action)
- **Action** — a reusable unit of code (e.g., `actions/checkout`, `azure/login`)
- **Secret** — encrypted variable for sensitive data

### Azure-Specific Actions

| Action | Purpose |
|--------|---------|
| `azure/login` | Authenticate to Azure |
| `azure/webapps-deploy` | Deploy to App Service |
| `azure/functions-action` | Deploy to Azure Functions |
| `azure/docker-login` | Login to ACR |
| `azure/arm-deploy` | Deploy ARM/Bicep templates |

---

## 3. ARM Templates & Bicep

### ARM Templates (JSON)

Declarative infrastructure as code. Verbose but powerful.

```json
{
  "$schema": "https://schema.management.azure.com/schemas/2019-04-01/deploymentTemplate.json#",
  "contentVersion": "1.0.0.0",
  "parameters": {
    "appName": { "type": "string" }
  },
  "resources": [
    {
      "type": "Microsoft.Web/serverfarms",
      "apiVersion": "2022-09-01",
      "name": "[concat(parameters('appName'), '-plan')]",
      "location": "[resourceGroup().location]",
      "sku": { "name": "B1" },
      "kind": "linux",
      "properties": { "reserved": true }
    }
  ]
}
```

### Bicep

Bicep compiles to ARM JSON but is much cleaner:

```bicep
param appName string
param location string = resourceGroup().location

resource appServicePlan 'Microsoft.Web/serverfarms@2022-09-01' = {
  name: '${appName}-plan'
  location: location
  sku: { name: 'B1' }
  kind: 'linux'
  properties: { reserved: true }
}

resource webApp 'Microsoft.Web/sites@2022-09-01' = {
  name: appName
  location: location
  properties: {
    serverFarmId: appServicePlan.id
    siteConfig: {
      linuxFxVersion: 'DOTNETCORE|8.0'
    }
  }
}

output appUrl string = 'https://${webApp.properties.defaultHostName}'
```

### Bicep Features

- **Parameters** — input values
- **Variables** — computed values
- **Resources** — Azure resources to create
- **Modules** — reusable Bicep files
- **Outputs** — values to return after deployment
- **Conditions** — deploy resources conditionally
- **Loops** — create multiple resources

---

## 4. Deployment Strategies

| Strategy | Description | Risk |
|----------|-------------|------|
| **Blue-Green** | Two identical environments, swap traffic | Low |
| **Canary** | Route small % of traffic to new version | Low |
| **Rolling** | Gradually replace instances | Medium |
| **Recreate** | Stop old, start new | High (downtime) |

App Service deployment slots enable blue-green and canary deployments.

---

## Key Takeaways

1. GitHub Actions workflows are YAML files in `.github/workflows/`
2. Use `azure/login` with a service principal or OIDC for authentication
3. Bicep is the recommended IaC language for Azure (compiles to ARM JSON)
4. Deployment slots enable zero-downtime deployments
5. Store secrets in GitHub Secrets, not in workflow files
6. Use approval gates for production deployments

---

Next: [Lab — GitHub Actions](./lab-github-actions.md)
