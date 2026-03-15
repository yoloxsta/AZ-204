# Day 10 — API Management

## What You'll Learn Today

- What Azure API Management (APIM) is and why it's used
- APIM components: gateway, portal, management plane
- Policies: inbound, backend, outbound, on-error
- Products, subscriptions, and rate limiting
- API versioning and revisions

---

## 1. What is Azure API Management?

APIM is a gateway that sits in front of your APIs. It provides a single entry point for all API consumers and handles cross-cutting concerns.

### Why Use APIM?

- **Security** — authentication, authorization, IP filtering
- **Rate limiting** — protect backends from overload
- **Transformation** — modify requests/responses (headers, body, format)
- **Monitoring** — analytics, logging, Application Insights integration
- **Developer portal** — self-service API documentation and key management
- **Versioning** — manage multiple API versions

### Architecture

```
                    ┌─────────────────────────────┐
  API Consumers ──► │     API Management Gateway   │
  (Apps, SPAs,      │                              │
   Mobile)          │  ┌────────────────────────┐  │
                    │  │  Policies              │  │
                    │  │  - Rate limiting        │  │
                    │  │  - JWT validation       │  │
                    │  │  - Caching              │  │
                    │  │  - Transformation       │  │
                    │  └────────────────────────┘  │
                    │              │                │
                    └──────────────┼────────────────┘
                                   │
                    ┌──────────────┼──────────────┐
                    │              │              │
               ┌────▼───┐   ┌────▼───┐   ┌─────▼────┐
               │ API 1  │   │ API 2  │   │  API 3   │
               │(App Svc│   │(Func)  │   │(External)│
               └────────┘   └────────┘   └──────────┘
```

### Tiers

| Tier | Use Case | Gateway | Developer Portal |
|------|----------|---------|-----------------|
| **Consumption** | Serverless, low traffic | Shared | No |
| **Developer** | Dev/test | Dedicated | Yes |
| **Basic** | Small production | Dedicated | No |
| **Standard** | Production | Dedicated | Yes |
| **Premium** | Multi-region, VNet | Dedicated | Yes |

---

## 2. Policies

Policies are XML statements that modify API behavior. They execute at four stages:

```xml
<policies>
    <inbound>    <!-- Before sending to backend --> </inbound>
    <backend>    <!-- When forwarding to backend --> </backend>
    <outbound>   <!-- Before sending to client --> </outbound>
    <on-error>   <!-- When an error occurs --> </on-error>
</policies>
```

### Common Policies

**Rate Limiting:**
```xml
<inbound>
    <rate-limit calls="10" renewal-period="60" />
</inbound>
```

**JWT Validation:**
```xml
<inbound>
    <validate-jwt header-name="Authorization" require-scheme="Bearer">
        <openid-config url="https://login.microsoftonline.com/{tenant}/.well-known/openid-configuration" />
        <required-claims>
            <claim name="aud" match="all">
                <value>{client-id}</value>
            </claim>
        </required-claims>
    </validate-jwt>
</inbound>
```

**Set Header:**
```xml
<outbound>
    <set-header name="X-Custom-Header" exists-action="override">
        <value>MyValue</value>
    </set-header>
</outbound>
```

**Response Caching:**
```xml
<inbound>
    <cache-lookup vary-by-developer="false" vary-by-developer-groups="false" />
</inbound>
<outbound>
    <cache-store duration="3600" />
</outbound>
```

**Rewrite URL:**
```xml
<inbound>
    <rewrite-uri template="/api/v2/{path}" />
</inbound>
```

---

## 3. Products and Subscriptions

- **Product** — a bundle of APIs offered to developers (e.g., "Free", "Premium")
- **Subscription** — a developer signs up for a product and gets a subscription key
- **Subscription key** — sent in the `Ocp-Apim-Subscription-Key` header or as a query parameter

---

## 4. Versioning and Revisions

**Versions** — breaking changes (v1, v2):
- URL path: `/api/v1/products`
- Header: `Api-Version: v1`
- Query string: `/api/products?api-version=v1`

**Revisions** — non-breaking changes (like a draft):
- Make changes to a revision without affecting the live API
- When ready, make the revision current

---

## Key Takeaways

1. APIM is a gateway for cross-cutting concerns — security, rate limiting, transformation
2. Policies execute in four stages: inbound, backend, outbound, on-error
3. Rate limiting and JWT validation are the most common policies for AZ-204
4. Products bundle APIs, subscriptions provide access keys
5. Versions for breaking changes, revisions for non-breaking changes

---

Next: [Lab — API Management](./lab-api-management.md)
