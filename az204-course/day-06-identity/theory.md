# Day 06 — Identity & Authentication

## What You'll Learn Today

- Microsoft Identity Platform and OAuth 2.0 / OpenID Connect
- Microsoft Entra ID (formerly Azure AD)
- App registrations and service principals
- MSAL (Microsoft Authentication Library)
- Managed Identities (system-assigned and user-assigned)
- Microsoft Graph API basics

---
## 1. Microsoft Identity Platform

The Microsoft Identity Platform is the authentication and authorization service for Azure. It implements OAuth 2.0 and OpenID Connect.

### Key Concepts

- **Tenant** — an instance of Microsoft Entra ID representing an organization
- **App Registration** — registers your app with the identity platform
- **Service Principal** — the identity object created in each tenant where the app is used
- **Tokens** — ID tokens (authentication), access tokens (authorization), refresh tokens

### OAuth 2.0 Flows

| Flow | Use Case |
|------|----------|
| **Authorization Code** | Web apps, SPAs (with PKCE) |
| **Client Credentials** | Daemon/service apps (no user) |
| **On-Behalf-Of** | API calling another API on behalf of user |
| **Device Code** | Devices without browsers (IoT, CLI) |

---

## 2. App Registrations

When you register an app in Entra ID, you get:

- **Application (client) ID** — unique identifier for your app
- **Directory (tenant) ID** — identifies the Entra ID tenant
- **Client secret or certificate** — credential for confidential apps
- **Redirect URIs** — where tokens are sent after authentication

### Permissions

| Type | Description | Requires |
|------|-------------|----------|
| **Delegated** | App acts on behalf of a signed-in user | User consent |
| **Application** | App acts as itself (no user) | Admin consent |

---

## 3. MSAL (Microsoft Authentication Library)

MSAL handles token acquisition, caching, and refresh. Available for .NET, JavaScript, Python, Java, and more.

### Key Classes (.NET)

- `PublicClientApplicationBuilder` — for desktop/mobile apps (user interaction)
- `ConfidentialClientApplicationBuilder` — for web apps/APIs (has a secret)

### Token Cache

MSAL automatically caches tokens and refreshes them when expired. You don't need to manage token lifecycle manually.

---

## 4. Managed Identities

Managed identities eliminate the need for credentials in your code. Azure manages the identity lifecycle.

### Types

| Type | Created With | Lifecycle | Use Case |
|------|-------------|-----------|----------|
| **System-assigned** | The resource | Tied to resource (deleted with it) | Single resource access |
| **User-assigned** | Standalone resource | Independent | Shared across resources |

### How It Works

```
┌──────────────┐     1. Request token      ┌──────────────┐
│  Your App    │ ──────────────────────────►│  Azure AD    │
│  (App Service│                            │  (Entra ID)  │
│   or VM)     │ ◄──────────────────────────│              │
│              │     2. Return token         └──────────────┘
│              │
│              │     3. Use token            ┌──────────────┐
│              │ ──────────────────────────►│  Key Vault   │
│              │                            │  Storage     │
│              │ ◄──────────────────────────│  Cosmos DB   │
└──────────────┘     4. Return data          └──────────────┘
```

Your code uses `DefaultAzureCredential` from the `Azure.Identity` package — it automatically detects the managed identity.

### DefaultAzureCredential Chain

`DefaultAzureCredential` tries these in order:
1. Environment variables
2. Workload Identity
3. Managed Identity
4. Azure CLI
5. Azure PowerShell
6. Azure Developer CLI
7. Interactive browser

This means the same code works locally (using CLI credentials) and in Azure (using managed identity).

---

## 5. Microsoft Graph

Microsoft Graph is the API for accessing Microsoft 365 data — users, groups, mail, calendar, files, etc.

Endpoint: `https://graph.microsoft.com/v1.0/`

Common operations:
- `GET /me` — current user profile
- `GET /users` — list users
- `POST /me/sendMail` — send email
- `GET /me/drive/root/children` — list OneDrive files

---

## Key Takeaways

1. Register apps in Entra ID to get client ID and tenant ID
2. Use MSAL for token acquisition — it handles caching and refresh
3. Managed identities eliminate secrets — system-assigned is simplest
4. `DefaultAzureCredential` works everywhere — local dev and production
5. Client credentials flow is for service-to-service (no user)
6. Microsoft Graph is the unified API for Microsoft 365

---

## Deep Dives

For detailed coverage of specific AZ-204 exam topics, see:
- [Implementing Shared Access Signatures — Deep Dive](./shared-access-signatures.md)
- [Exploring Microsoft Graph — Deep Dive](./microsoft-graph.md)

---

Next: [Lab — Managed Identity](./lab-managed-identity.md)
