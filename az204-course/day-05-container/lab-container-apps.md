# Lab: Containers — ACR, ACI & Container Apps

## Title
Build, Push, and Deploy Containerized Applications on Azure

## Objective
- Build a container image using ACR Tasks
- Run a container on Azure Container Instances
- Deploy a container app to Azure Container Apps
- Configure scaling rules and revisions

## Architecture

```
┌──────────────────────────────────────────────────────────┐
│              Resource Group: az204-day05-rg               │
│                                                          │
│  ┌──────────────┐    ┌──────────────┐                   │
│  │     ACR      │    │     ACI      │                   │
│  │ az204acr<u>  │───►│ my-container │                   │
│  │              │    │ (single run) │                   │
│  │ Image:       │    └──────────────┘                   │
│  │ myapi:v1     │                                       │
│  │ myapi:v2     │    ┌──────────────────────────────┐   │
│  │              │───►│  Container Apps Environment   │   │
│  └──────────────┘    │                              │   │
│                      │  ┌────────────────────────┐  │   │
│                      │  │ Container App: myapi   │  │   │
│                      │  │ Rev1: v1 (80%)         │  │   │
│                      │  │ Rev2: v2 (20%)         │  │   │
│                      │  │ Scale: 0-5 instances   │  │   │
│                      │  └────────────────────────┘  │   │
│                      └──────────────────────────────┘   │
└──────────────────────────────────────────────────────────┘
```

## Prerequisites
- Azure CLI installed and logged in
- Docker Desktop installed (optional — we'll use ACR Tasks for cloud builds)

---

## Step 1 — Create Resource Group and ACR

```bash
az group create \
  --name az204-day05-rg \
  --location eastus

az acr create \
  --name az204acr<unique> \
  --resource-group az204-day05-rg \
  --sku Basic \
  --admin-enabled true
```

Verify:
```bash
az acr show \
  --name az204acr<unique> \
  --query "{name:name, loginServer:loginServer, sku:sku.name}" \
  --output table
```

---

## Step 2 — Create a Sample Application

Create a project directory:
```bash
mkdir az204-container-app
cd az204-container-app
```

Create `app.js`:
```javascript
const http = require('http');
const os = require('os');

const port = process.env.PORT || 8080;
const version = process.env.APP_VERSION || '1.0';

const server = http.createServer((req, res) => {
    res.writeHead(200, { 'Content-Type': 'application/json' });
    res.end(JSON.stringify({
        message: `Hello from AZ-204 Container Lab!`,
        version: version,
        hostname: os.hostname(),
        timestamp: new Date().toISOString()
    }));
});

server.listen(port, () => {
    console.log(`Server running on port ${port}, version ${version}`);
});
```

Create `Dockerfile`:
```dockerfile
FROM node:20-alpine
WORKDIR /app
COPY app.js .
ENV PORT=8080
EXPOSE 8080
CMD ["node", "app.js"]
```

---

## Step 3 — Build Image with ACR Tasks (Cloud Build)

No local Docker needed — ACR builds the image in the cloud:

```bash
az acr build \
  --registry az204acr<unique> \
  --image myapi:v1 \
  --file Dockerfile \
  .
```

Verify the image is in the registry:
```bash
az acr repository list \
  --name az204acr<unique> \
  --output table

az acr repository show-tags \
  --name az204acr<unique> \
  --repository myapi \
  --output table
```

---

## Step 4 — Run Container on ACI

Get ACR credentials:
```bash
ACR_LOGIN_SERVER=$(az acr show --name az204acr<unique> --query loginServer --output tsv)
ACR_PASSWORD=$(az acr credential show --name az204acr<unique> --query "passwords[0].value" --output tsv)
```

Create a container instance:
```bash
az container create \
  --name myapi-container \
  --resource-group az204-day05-rg \
  --image ${ACR_LOGIN_SERVER}/myapi:v1 \
  --registry-login-server $ACR_LOGIN_SERVER \
  --registry-username az204acr<unique> \
  --registry-password $ACR_PASSWORD \
  --dns-name-label az204-aci-<unique> \
  --ports 8080 \
  --cpu 1 \
  --memory 1 \
  --environment-variables APP_VERSION=1.0 \
  --restart-policy OnFailure
```

Wait for it to start, then verify:
```bash
az container show \
  --name myapi-container \
  --resource-group az204-day05-rg \
  --query "{name:name, state:instanceView.state, fqdn:ipAddress.fqdn, ip:ipAddress.ip}" \
  --output table
```

Test the container:
```bash
FQDN=$(az container show --name myapi-container --resource-group az204-day05-rg --query ipAddress.fqdn --output tsv)
curl http://${FQDN}:8080
```

View container logs:
```bash
az container logs \
  --name myapi-container \
  --resource-group az204-day05-rg
```

---

## Step 5 — Deploy to Azure Container Apps

Install the Container Apps extension:
```bash
az extension add --name containerapp --upgrade
az provider register --namespace Microsoft.App
az provider register --namespace Microsoft.OperationalInsights
```

Create a Container Apps environment:
```bash
az containerapp env create \
  --name az204-env \
  --resource-group az204-day05-rg \
  --location eastus
```

Deploy the container app:
```bash
az containerapp create \
  --name myapi-app \
  --resource-group az204-day05-rg \
  --environment az204-env \
  --image ${ACR_LOGIN_SERVER}/myapi:v1 \
  --registry-server $ACR_LOGIN_SERVER \
  --registry-username az204acr<unique> \
  --registry-password $ACR_PASSWORD \
  --target-port 8080 \
  --ingress external \
  --min-replicas 0 \
  --max-replicas 5 \
  --env-vars APP_VERSION=1.0
```

Get the app URL:
```bash
az containerapp show \
  --name myapi-app \
  --resource-group az204-day05-rg \
  --query "properties.configuration.ingress.fqdn" \
  --output tsv
```

Test it:
```bash
APP_URL=$(az containerapp show --name myapi-app --resource-group az204-day05-rg --query "properties.configuration.ingress.fqdn" --output tsv)
curl https://${APP_URL}
```

---

## Step 6 — Deploy a New Revision (v2)

Update `app.js` to change the version, then build v2:
```bash
# Update the version in app.js (or set via env var)
az acr build \
  --registry az204acr<unique> \
  --image myapi:v2 \
  --file Dockerfile \
  .
```

Create a new revision with traffic splitting:
```bash
az containerapp update \
  --name myapi-app \
  --resource-group az204-day05-rg \
  --image ${ACR_LOGIN_SERVER}/myapi:v2 \
  --env-vars APP_VERSION=2.0 \
  --revision-suffix v2
```

Split traffic between revisions (80/20):
```bash
# Get revision names
az containerapp revision list \
  --name myapi-app \
  --resource-group az204-day05-rg \
  --query "[].name" \
  --output table

# Split traffic
az containerapp ingress traffic set \
  --name myapi-app \
  --resource-group az204-day05-rg \
  --revision-weight myapi-app--v2=20 latest=80
```

Test multiple times to see traffic distribution:
```bash
for i in $(seq 1 10); do curl -s https://${APP_URL} | jq .version; done
```

---

## Step 7 — Configure Scaling Rules

Add an HTTP-based scaling rule:
```bash
az containerapp update \
  --name myapi-app \
  --resource-group az204-day05-rg \
  --min-replicas 1 \
  --max-replicas 10 \
  --scale-rule-name http-rule \
  --scale-rule-type http \
  --scale-rule-http-concurrency 50
```

This scales up when concurrent HTTP requests exceed 50 per instance.

View current replicas:
```bash
az containerapp revision list \
  --name myapi-app \
  --resource-group az204-day05-rg \
  --query "[].{name:name, replicas:properties.replicas, active:properties.active, trafficWeight:properties.trafficWeight}" \
  --output table
```

---

## Step 8 — View Logs

```bash
az containerapp logs show \
  --name myapi-app \
  --resource-group az204-day05-rg \
  --type console \
  --follow
```

Press `Ctrl+C` to stop following logs.

---

## Cleanup

```bash
cd ..
rm -rf az204-container-app

az container delete --name myapi-container --resource-group az204-day05-rg --yes
az group delete --name az204-day05-rg --yes --no-wait
```

---

## Expected Outcomes

| Task | Status |
|------|--------|
| Created ACR and built image with ACR Tasks | ✅ |
| Ran container on ACI | ✅ |
| Deployed to Container Apps with external ingress | ✅ |
| Created a new revision (v2) | ✅ |
| Configured traffic splitting (80/20) | ✅ |
| Set up HTTP-based auto-scaling | ✅ |
| Viewed container logs | ✅ |

---

Next: [Day 06 — Identity & Authentication](../day-06-identity/theory.md)
