# Lab: Azure API Management

## Title
Create and Configure an API Management Instance with Policies

## Objective
- Create an APIM instance (Consumption tier)
- Import an API from an OpenAPI spec
- Apply policies: rate limiting, headers, caching
- Create products and subscriptions
- Test APIs with subscription keys

## Architecture

```
┌──────────────────────────────────────────────────────────┐
│              Resource Group: az204-day10-rg               │
│                                                          │
│  ┌────────────────────────────────────────────────────┐  │
│  │  API Management: az204apim<unique>                 │  │
│  │  Tier: Consumption                                 │  │
│  │                                                    │  │
│  │  Products:                                         │  │
│  │  ├── Free (rate limit: 5 calls/min)               │  │
│  │  └── Premium (rate limit: 100 calls/min)          │  │
│  │                                                    │  │
│  │  APIs:                                             │  │
│  │  └── Conference API (imported from OpenAPI)        │  │
│  │       ├── GET /sessions                            │  │
│  │       ├── GET /speakers                            │  │
│  │       └── Policies: rate-limit, set-header, cache  │  │
│  └────────────────────────────────────────────────────┘  │
└──────────────────────────────────────────────────────────┘
```

## Prerequisites
- Azure CLI installed and logged in

---

## Step 1 — Create APIM Instance

```bash
az group create --name az204-day10-rg --location eastus

az apim create \
  --name az204apim<unique> \
  --resource-group az204-day10-rg \
  --publisher-name "AZ204 Lab" \
  --publisher-email "admin@example.com" \
  --sku-name Consumption \
  --location eastus
```

Consumption tier deploys in ~1 minute. Verify:
```bash
az apim show \
  --name az204apim<unique> \
  --resource-group az204-day10-rg \
  --query "{name:name, gatewayUrl:gatewayUrl, sku:sku.name}" \
  --output table
```

---

## Step 2 — Import a Sample API

Import the Conference API from Microsoft's sample OpenAPI spec:
```bash
az apim api import \
  --resource-group az204-day10-rg \
  --service-name az204apim<unique> \
  --api-id conference-api \
  --display-name "Conference API" \
  --path conference \
  --specification-format OpenApiJson \
  --specification-url "https://conferenceapi.azurewebsites.net/?format=json" \
  --service-url "https://conferenceapi.azurewebsites.net"
```

Verify the API was imported:
```bash
az apim api list \
  --resource-group az204-day10-rg \
  --service-name az204apim<unique> \
  --output table
```

List operations:
```bash
az apim api operation list \
  --resource-group az204-day10-rg \
  --service-name az204apim<unique> \
  --api-id conference-api \
  --output table
```

---

## Step 3 — Apply Policies

Apply an API-level policy with rate limiting and custom headers:

```bash
az apim api policy create \
  --resource-group az204-day10-rg \
  --service-name az204apim<unique> \
  --api-id conference-api \
  --xml-policy '<policies>
    <inbound>
        <base />
        <rate-limit calls="10" renewal-period="60" />
        <set-header name="X-Request-Source" exists-action="override">
            <value>APIM-Gateway</value>
        </set-header>
    </inbound>
    <backend>
        <base />
    </backend>
    <outbound>
        <base />
        <set-header name="X-Powered-By" exists-action="delete" />
        <set-header name="X-APIM-Lab" exists-action="override">
            <value>Day10</value>
        </set-header>
    </outbound>
    <on-error>
        <base />
    </on-error>
</policies>'
```

This policy:
- Limits to 10 calls per 60 seconds
- Adds `X-Request-Source` header to backend requests
- Removes `X-Powered-By` from responses
- Adds `X-APIM-Lab` header to responses

---

## Step 4 — Create Products

Create a Free product:
```bash
az apim product create \
  --resource-group az204-day10-rg \
  --service-name az204apim<unique> \
  --product-id free-tier \
  --display-name "Free Tier" \
  --description "Free access with rate limiting" \
  --subscription-required true \
  --approval-required false \
  --subscriptions-limit 1 \
  --state published
```

Create a Premium product:
```bash
az apim product create \
  --resource-group az204-day10-rg \
  --service-name az204apim<unique> \
  --product-id premium-tier \
  --display-name "Premium Tier" \
  --description "Premium access with higher limits" \
  --subscription-required true \
  --approval-required false \
  --subscriptions-limit 5 \
  --state published
```

Add the Conference API to both products:
```bash
az apim product api add \
  --resource-group az204-day10-rg \
  --service-name az204apim<unique> \
  --product-id free-tier \
  --api-id conference-api

az apim product api add \
  --resource-group az204-day10-rg \
  --service-name az204apim<unique> \
  --product-id premium-tier \
  --api-id conference-api
```

---

## Step 5 — Create a Subscription

```bash
az apim subscription create \
  --resource-group az204-day10-rg \
  --service-name az204apim<unique> \
  --subscription-id test-sub \
  --display-name "Test Subscription" \
  --scope "/products/free-tier" \
  --state active
```

Get the subscription key:
```bash
az apim subscription keys list \
  --resource-group az204-day10-rg \
  --service-name az204apim<unique> \
  --subscription-id test-sub \
  --query primaryKey \
  --output tsv
```

---

## Step 6 — Test the API

Get the gateway URL:
```bash
GATEWAY_URL=$(az apim show \
  --name az204apim<unique> \
  --resource-group az204-day10-rg \
  --query gatewayUrl --output tsv)

SUB_KEY=$(az apim subscription keys list \
  --resource-group az204-day10-rg \
  --service-name az204apim<unique> \
  --subscription-id test-sub \
  --query primaryKey --output tsv)
```

Call the API:
```bash
# Get sessions
curl -s "${GATEWAY_URL}/conference/sessions" \
  -H "Ocp-Apim-Subscription-Key: ${SUB_KEY}" | head -c 500

# Get speakers
curl -s "${GATEWAY_URL}/conference/speakers" \
  -H "Ocp-Apim-Subscription-Key: ${SUB_KEY}" | head -c 500

# Check response headers
curl -sI "${GATEWAY_URL}/conference/sessions" \
  -H "Ocp-Apim-Subscription-Key: ${SUB_KEY}"
```

You should see the `X-APIM-Lab: Day10` header in the response and no `X-Powered-By` header.

Test without subscription key (should fail):
```bash
curl -s "${GATEWAY_URL}/conference/sessions"
```

Expected: `401 Unauthorized` — subscription key is required.

Test rate limiting (send 15 rapid requests):
```bash
for i in $(seq 1 15); do
  STATUS=$(curl -s -o /dev/null -w "%{http_code}" \
    "${GATEWAY_URL}/conference/sessions" \
    -H "Ocp-Apim-Subscription-Key: ${SUB_KEY}")
  echo "Request $i: HTTP $STATUS"
done
```

After 10 requests, you should see `429 Too Many Requests`.

---

## Step 7 — Apply Product-Level Policy

Apply a stricter rate limit to the Free product:
```bash
az apim product policy create \
  --resource-group az204-day10-rg \
  --service-name az204apim<unique> \
  --product-id free-tier \
  --xml-policy '<policies>
    <inbound>
        <base />
        <rate-limit calls="5" renewal-period="60" />
    </inbound>
    <backend><base /></backend>
    <outbound><base /></outbound>
    <on-error><base /></on-error>
</policies>'
```

Now the Free tier is limited to 5 calls/minute (overrides the API-level 10 calls/minute).

---

## Cleanup

```bash
az group delete --name az204-day10-rg --yes --no-wait
```

---

## Expected Outcomes

| Task | Status |
|------|--------|
| Created APIM instance (Consumption) | ✅ |
| Imported API from OpenAPI spec | ✅ |
| Applied rate limiting policy | ✅ |
| Applied header transformation policies | ✅ |
| Created Free and Premium products | ✅ |
| Created subscription and got key | ✅ |
| Tested API with subscription key | ✅ |
| Verified rate limiting (429 response) | ✅ |
| Applied product-level policy | ✅ |

---

Next: [Day 11 — Monitoring & Logging](../day-11-monitoring/theory.md)
