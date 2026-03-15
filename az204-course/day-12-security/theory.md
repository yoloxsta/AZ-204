# Day 12 — Security: Key Vault & App Configuration

## What You'll Learn Today

- Azure Key Vault: secrets, keys, and certificates
- Key Vault access: RBAC vs access policies
- Azure App Configuration: centralized configuration, feature flags
- Secure application patterns

---

## 1. Azure Key Vault

Key Vault is a cloud service for securely storing and accessing secrets, encryption keys, and certificates.

### What It Stores

| Type | Description | Example |
|------|-------------|---------|
| **Secrets** | Sensitive strings | Connection strings, API keys, passwords |
| **Keys** | Cryptographic keys | Encryption keys, signing keys |
| **Certificates** | X.509 certificates | TLS/SSL certificates |

### Why Use Key Vault?

- **Centralized secret management** — one place for all secrets
- **Access control** — RBAC or access policies
- **Audit logging** — track who accessed what and when
- **Secret rotation** — automate secret updates
- **HSM-backed** — hardware security modules for key protection

### Access Models

| Model | Description | Recommended |
|-------|-------------|-------------|
| **RBAC** | Azure role-based access control | Yes (modern) |
| **Access Policies** | Vault-level permissions per identity | Legacy |

Key RBAC roles:
- `Key Vault Secrets User` — read secrets
- `Key Vault Secrets Officer` — read/write/delete secrets
- `Key Vault Administrator` — full access
- `Key Vault Crypto User` — use keys for encrypt/decrypt
- `Key Vault Certificates Officer` — manage certificates

### Secret Versioning

Every secret has versions. When you update a secret, a new version is created:
```
https://myvault.vault.azure.net/secrets/MySecret           → latest version
https://myvault.vault.azure.net/secrets/MySecret/abc123...  → specific version
```

---

## 2. Azure App Configuration

App Configuration is a centralized service for managing application settings and feature flags.

### Why Use It?

- **Centralized** — one place for all app settings across environments
- **Feature flags** — enable/disable features without redeployment
- **Dynamic configuration** — update settings without restarting apps
- **Key-value store** — with labels for environment separation
- **Integration** — works with Key Vault references

### Key-Value with Labels

```
Key                    Label        Value
─────────────────────  ──────────   ─────────────
App:Settings:Color     Production   Blue
App:Settings:Color     Staging      Red
App:Settings:MaxItems  (none)       100
```

### Feature Flags

```json
{
  "id": "Beta",
  "description": "Beta features",
  "enabled": true,
  "conditions": {
    "client_filters": [
      {
        "name": "Percentage",
        "parameters": { "Value": 50 }
      }
    ]
  }
}
```

### Key Vault References in App Configuration

Store the secret in Key Vault, reference it from App Configuration:
```json
{
  "uri": "https://myvault.vault.azure.net/secrets/DatabasePassword"
}
```

Your app reads from App Configuration, which transparently resolves the Key Vault reference.

---

## 3. Secure Application Patterns

### Pattern 1: Managed Identity + Key Vault
```
App Service ──(managed identity)──► Key Vault ──► Secret
```
No credentials in code or config.

### Pattern 2: App Configuration + Key Vault References
```
App ──► App Configuration ──(reference)──► Key Vault ──► Secret
```
Centralized config with secure secret storage.

### Pattern 3: Key Vault + Certificate for mTLS
```
App ──► Key Vault ──► Certificate ──► Mutual TLS connection
```

---

## Key Takeaways

1. Key Vault stores secrets, keys, and certificates securely
2. Use RBAC (not access policies) for Key Vault access control
3. Managed Identity + Key Vault = no secrets in code
4. App Configuration centralizes settings with labels for environments
5. Feature flags enable/disable features without redeployment
6. Key Vault references in App Configuration combine both services

---

Next: [Lab — Key Vault](./lab-keyvault.md)
