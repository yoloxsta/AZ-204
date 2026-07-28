#!/usr/bin/env bash
set -euo pipefail

# ─── Color Helpers ─────────────────────────────────────────────────
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
NC='\033[0m'

info()    { echo -e "${BLUE}[INFO]${NC} $*"; }
success() { echo -e "${GREEN}[OK]${NC} $*"; }
warn()    { echo -e "${YELLOW}[WARN]${NC} $*"; }
error()   { echo -e "${RED}[ERROR]${NC} $*" >&2; }

# ─── Preflight Checks ─────────────────────────────────────────────
command -v az >/dev/null 2>&1 || { error "Azure CLI is not installed. Install from https://aka.ms/azure-cli"; exit 1; }
command -v dotnet >/dev/null 2>&1 || { error ".NET 8 SDK is not installed. Install from https://dot.net/download"; exit 1; }

az account show >/dev/null 2>&1 || { error "Not logged into Azure CLI. Run 'az login' first."; exit 1; }

# ─── Prompt for Values ────────────────────────────────────────────
read -rp "Resource Group name [rg-orderflow-dev]: " RG_NAME
RG_NAME="${RG_NAME:-rg-orderflow-dev}"

read -rp "Azure Region [eastus]: " LOCATION
LOCATION="${LOCATION:-eastus}"

read -rp "Environment name [dev]: " ENV_NAME
ENV_NAME="${ENV_NAME:-dev}"

read -rp "Project name prefix [orderflow]: " PROJECT_NAME
PROJECT_NAME="${PROJECT_NAME:-orderflow}"

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJECT_DIR="$(dirname "$SCRIPT_DIR")"

info "Configuration:"
info "  Resource Group : $RG_NAME"
info "  Location        : $LOCATION"
info "  Environment     : $ENV_NAME"
info "  Project         : $PROJECT_NAME"
echo ""

# ─── Step 1: Create Resource Group ────────────────────────────────
info "Creating resource group '$RG_NAME'..."
az group create --name "$RG_NAME" --location "$LOCATION" --output none
success "Resource group created."

# ─── Step 2: Deploy Bicep Template ────────────────────────────────
info "Deploying Azure resources via Bicep template..."
DEPLOY_OUTPUT=$(az deployment group create \
  --resource-group "$RG_NAME" \
  --template-file "$SCRIPT_DIR/azure-resources.bicep" \
  --parameters \
    location="$LOCATION" \
    environmentName="$ENV_NAME" \
    projectName="$PROJECT_NAME" \
  --query 'properties.outputs' \
  --output json)

success "Azure resources deployed."

# Extract outputs
COSMOS_ENDPOINT=$(echo "$DEPLOY_OUTPUT" | jq -r '.cosmosEndpoint.value')
COSMOS_DB_NAME=$(echo "$DEPLOY_OUTPUT" | jq -r '.cosmosDatabaseName.value')
STORAGE_NAME=$(echo "$DEPLOY_OUTPUT" | jq -r '.storageAccountName.value')
KEYVAULT_URI=$(echo "$DEPLOY_OUTPUT" | jq -r '.keyVaultUri.value')
APPCONFIG_ENDPOINT=$(echo "$DEPLOY_OUTPUT" | jq -r '.appConfigEndpoint.value')
SB_NAMESPACE=$(echo "$DEPLOY_OUTPUT" | jq -r '.serviceBusNamespace.value')
SB_CONNECTION=$(echo "$DEPLOY_OUTPUT" | jq -r '.serviceBusConnectionString.value')
EG_ENDPOINT=$(echo "$DEPLOY_OUTPUT" | jq -r '.eventGridTopicEndpoint.value')
REDIS_CONN=$(echo "$DEPLOY_OUTPUT" | jq -r '.redisConnectionString.value')
APPINSIGHTS_KEY=$(echo "$DEPLOY_OUTPUT" | jq -r '.appInsightsInstrumentationKey.value')
APPINSIGHTS_CONN=$(echo "$DEPLOY_OUTPUT" | jq -r '.appInsightsConnectionString.value')
MI_CLIENT_ID=$(echo "$DEPLOY_OUTPUT" | jq -r '.managedIdentityClientId.value')

# Get storage account key
STORAGE_KEY=$(az storage account keys list \
  --resource-group "$RG_NAME" \
  --account-name "$STORAGE_NAME" \
  --query '[0].value' \
  --output tsv)

BLOB_CONN="DefaultEndpointsProtocol=https;AccountName=${STORAGE_NAME};AccountKey=${STORAGE_KEY};EndpointSuffix=core.windows.net"

info "  Cosmos Endpoint        : $COSMOS_ENDPOINT"
info "  Service Bus Namespace  : $SB_NAMESPACE"
info "  Event Grid Topic       : $EG_ENDPOINT"
info "  Key Vault URI          : $KEYVAULT_URI"
info "  App Config Endpoint    : $APPCONFIG_ENDPOINT"
echo ""

# ─── Step 3: Configure App Configuration Feature Flags ────────────
info "Setting App Configuration feature flags..."
az appconfig kv set \
  --endpoint "$APPCONFIG_ENDPOINT" \
  --key "feature-flags/EnableEmailNotifications" \
  --value '{"enabled":false,"conditions":[]}' \
  --content-type "application/json" \
  --label "feature-flag" \
  --output none 2>/dev/null || warn "Could not set feature flags (may need Data Owner role)."

az appconfig kv set \
  --endpoint "$APPCONFIG_ENDPOINT" \
  --key "OrderSettings/MaxItemsPerOrder" \
  --value "50" \
  --output none 2>/dev/null || true

az appconfig kv set \
  --endpoint "$APPCONFIG_ENDPOINT" \
  --key "OrderSettings/StaleOrderHours" \
  --value "24" \
  --output none 2>/dev/null || true

success "App Configuration values set."

# ─── Step 4: Set Key Vault Secrets ────────────────────────────────
info "Setting Key Vault secrets..."
az keyvault secret set --vault-name "$(basename "$KEYVAULT_URI" .vault.azure.net/)" --name "CosmosDb--AccountEndpoint" --value "$COSMOS_ENDPOINT" --output none
az keyvault secret set --vault-name "$(basename "$KEYVAULT_URI" .vault.azure.net/)" --name "CosmosDb--DatabaseName" --value "$COSMOS_DB_NAME" --output none
az keyvault secret set --vault-name "$(basename "$KEYVAULT_URI" .vault.azure.net/)" --name "BlobStorage--ConnectionString" --value "$BLOB_CONN" --output none
az keyvault secret set --vault-name "$(basename "$KEYVAULT_URI" .vault.azure.net/)" --name "ServiceBus--ConnectionString" --value "$SB_CONNECTION" --output none
az keyvault secret set --vault-name "$(basename "$KEYVAULT_URI" .vault.azure.net/)" --name "EventGrid--TopicEndpoint" --value "$EG_ENDPOINT" --output none
az keyvault secret set --vault-name "$(basename "$KEYVAULT_URI" .vault.azure.net/)" --name "Redis--ConnectionString" --value "$REDIS_CONN" --output none
az keyvault secret set --vault-name "$(basename "$KEYVAULT_URI" .vault.azure.net/)" --name "ApplicationInsights--ConnectionString" --value "$APPINSIGHTS_CONN" --output none
success "Key Vault secrets set."

# ─── Step 5: Build and Publish API ────────────────────────────────
info "Building OrderFlow.Api..."
dotnet build "$PROJECT_DIR/OrderFlow.Api/OrderFlow.Api.csproj" -c Release --verbosity quiet
success "OrderFlow.Api built successfully."

info "Publishing OrderFlow.Api..."
dotnet publish "$PROJECT_DIR/OrderFlow.Api/OrderFlow.Api.csproj" \
  -c Release \
  -o "$PROJECT_DIR/publish/api" \
  --verbosity quiet
success "OrderFlow.Api published."

# ─── Step 6: Build and Publish Functions ───────────────────────────
info "Building OrderFlow.Functions..."
dotnet build "$PROJECT_DIR/OrderFlow.Functions/OrderFlow.Functions.csproj" -c Release --verbosity quiet
success "OrderFlow.Functions built successfully."

info "Publishing OrderFlow.Functions..."
dotnet publish "$PROJECT_DIR/OrderFlow.Functions/OrderFlow.Functions.csproj" \
  -c Release \
  -o "$PROJECT_DIR/publish/functions" \
  --verbosity quiet
success "OrderFlow.Functions published."

# ─── Summary ──────────────────────────────────────────────────────
echo ""
echo -e "${GREEN}═══════════════════════════════════════════════════════════${NC}"
echo -e "${GREEN}  Deployment Complete!${NC}"
echo -e "${GREEN}═══════════════════════════════════════════════════════════${NC}"
echo ""
info "Next steps:"
info "  1. Run tests:        dotnet test $PROJECT_DIR/OrderFlow.Tests/"
info "  2. Run API locally:  cd $PROJECT_DIR/OrderFlow.Api && dotnet run"
info "  3. Run Functions:    cd $PROJECT_DIR/OrderFlow.Functions && func start"
info "  4. Deploy API:       az webapp create --resource-group $RG_NAME --plan <app-service-plan> --name ${PROJECT_NAME}-${ENV_NAME}-api --runtime 'DOTNET:8.0'"
info "  5. Deploy Functions: az functionapp create --resource-group $RG_NAME --consumption-plan-location $LOCATION --runtime dotnet-isolated --runtime-version 8 --functions-version 4 --name ${PROJECT_NAME}-${ENV_NAME}-functions --storage-account $STORAGE_NAME --assign-identity [system]"
echo ""
