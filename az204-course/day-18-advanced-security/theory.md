# Day 18 — Advanced Security: OAuth, B2C, and Network Security

## What You'll Learn Today

- OAuth 2.0 flows in depth (authorization code, client credentials, ROPC)
- Azure AD B2C (user flows, custom policies, progressive profiling)
- Managed Identity deep dive (system vs user-assigned)
- Azure Front Door + WAF
- Certificate-based authentication
- Conditional Access policies

---

## 1. OAuth 2.0 Flows Deep Dive

### Overview

| Flow | Use Case | User Interaction | Security |
|------|----------|-----------------|----------|
| Authorization Code + PKCE | Web/Mobile SPA apps | Yes | High |
| Client Credentials | Service-to-service | No | High (secret/cert) |
| ROPC (Resource Owner Password) | Legacy migration only | Yes | Low |
| On-Behalf-Of | Web API → downstream API | No | Medium |
| Device Code | CLI, IoT, smart TV | Yes | Medium |

### Authorization Code Flow with PKCE

```
┌─────────┐        ┌─────────┐        ┌───────────┐        ┌─────────┐
│ Browser  │──1──►  │ App     │──2──►  │ Azure AD  │        │         │
│          │        │         │        │           │        │         │
│          │◄─5──── │         │◄──3─── │           │        │         │
│          │        │         │──4────►│           │        │         │
│          │        │         │        │           │        │         │
│          │        │         │◄──6─── │           │        │         │
└─────────┘        └─────────┘        └───────────┘        └─────────┘

1. Generate code_verifier + code_challenge (S256)
2. Redirect to /authorize with challenge
3. User authenticates
4. AD returns authorization code
5. App exchanges code + verifier for tokens
6. AD validates and returns access_token + id_token + refresh_token
```

#### PKCE Implementation (C#)

```csharp
using System.Security.Cryptography;
using System.Text;

public class PkceHelper
{
    public static (string codeVerifier, string codeChallenge) GeneratePkce()
    {
        byte[] buffer = new byte[32];
        using var rng = RandomNumberGenerator.Create();
        rng.GetBytes(buffer);

        string codeVerifier = Convert.ToBase64String(buffer)
            .TrimEnd('=').Replace('+', '-').Replace('/', '_');

        using var sha256 = SHA256.Create();
        byte[] hash = sha256.ComputeHash(Encoding.ASCII.GetBytes(codeVerifier));
        string codeChallenge = Convert.ToBase64String(hash)
            .TrimEnd('=').Replace('+', '-').Replace('/', '_');

        return (codeVerifier, codeChallenge);
    }
}
```

#### Authorization Request

```
GET https://login.microsoftonline.com/{tenant}/oauth2/v2.0/authorize?
    client_id={client-id}
    &response_type=code
    &redirect_uri={redirect-uri}
    &response_mode=query
    &scope=openid profile email
    &state={state}
    &code_challenge={code-challenge}
    &code_challenge_method=S256
```

#### Token Exchange Request

```
POST https://login.microsoftonline.com/{tenant}/oauth2/v2.0/token
Content-Type: application/x-www-form-urlencoded

client_id={client-id}
&grant_type=authorization_code
&code={authorization-code}
&redirect_uri={redirect-uri}
&code_verifier={code-verifier}
```

### Client Credentials Flow

Used when no user is involved — service-to-service.

```csharp
var credential = new ClientSecretCredential(tenantId, clientId, clientSecret);
var tokenRequest = new TokenRequestContext(new[] { "https://graph.microsoft.com/.default" });
var token = await credential.GetTokenAsync(tokenRequest);
```

#### Certificate-Based Client Credentials

```csharp
var cert = new X509Certificate2("cert.pfx", password, X509KeyStorageFlags.MachineKeySet);
var credential = new ClientCertificateCredential(tenantId, clientId, cert);
var token = await credential.GetTokenAsync(new TokenRequestContext(new[] {
    "https://management.azure.com/.default"
}));
```

### Resource Owner Password Credentials (ROPC)

```csharp
// NOT recommended — use only for legacy migrations
var credential = new UsernamePasswordCredential(
    username, password, tenantId, clientId);
var token = await credential.GetTokenAsync(
    new TokenRequestContext(new[] { "https://graph.microsoft.com/.default" }));
```

| Risk | Mitigation |
|------|------------|
| Password exposure | Short-term migration only |
| No MFA support | Conditional Access blocks |
| Phishing risk | Disable when possible |

### On-Behalf-Of Flow

```csharp
// Middle-tier API receives token from client, exchanges it for downstream
var assertion = new ClientAssertion(confidentialClient.AppId, clientSecret);
var result = await confidentialClient
    .AcquireTokenOnBehalfOf(scopes, assertion)
    .ExecuteAsync();
```

### Device Code Flow

```csharp
var app = PublicClientApplicationBuilder
    .Create(clientId)
    .WithAuthority(AzureCloudInstance.AzurePublic, tenantId)
    .Build();

var result = await app.AcquireTokenWithDeviceCode(scopes, deviceCodeResult =>
{
    Console.WriteLine(deviceCodeResult.Message);
    return Task.CompletedTask;
}).ExecuteAsync();
```

---

## 2. MSAL Advanced

### Token Caching

```csharp
public class MsalTokenCache
{
    private readonly IConfidentialClientApp _app;
    private readonly IMemoryCache _cache;

    public async Task<string> GetAccessTokenAsync(string[] scopes)
    {
        var accounts = await _app.GetAccountsAsync();
        try
        {
            var result = await _app.AcquireTokenSilent(scopes, accounts.FirstOrDefault())
                .ExecuteAsync();
            return result.AccessToken;
        }
        catch (MsalUiRequiredException)
        {
            var result = await _app.AcquireTokenForClient(scopes)
                .ExecuteAsync();
            return result.AccessToken;
        }
    }
}
```

### Silent Refresh Pattern

```csharp
public async Task<string> GetTokenWithRefreshAsync()
{
    var accounts = await _app.GetAccountsAsync();
    var account = accounts.FirstOrDefault();

    try
    {
        // Try silent first (token cache + refresh token)
        var result = await _app.AcquireTokenSilent(scopes, account)
            .WithForceRefresh(false)
            .ExecuteAsync();
        return result.AccessToken;
    }
    catch (MsalServiceException ex) when (ex.ErrorCode == "invalid_grant")
    {
        // Refresh token expired or revoked — user must re-authenticate
        throw new UnauthorizedAccessException("Re-authentication required");
    }
}
```

### Multi-Account Management

```csharp
var accounts = await app.GetAccountsAsync();
foreach (var account in accounts)
{
    Console.WriteLine($"Home Account: {account.HomeAccountId}");
    Console.WriteLine($"Username: {account.Username}");
    Console.WriteLine($"Tenant: {account.TenantId}");
}

// Remove specific account
await app.RemoveAsync(account);
```

### Conditional Access Challenges

```csharp
try
{
    var result = await app.AcquireTokenInteractive(scopes)
        .ExecuteAsync();
}
catch (MsalServiceException ex) when (ex.ErrorCode == "interaction_required")
{
    // MFA or other CA policy triggered
    // Re-authenticate with full UI interaction
    var result = await app.AcquireTokenInteractive(scopes)
        .WithLoginHint(userEmail)
        .ExecuteAsync();
}
```

---

## 3. Azure AD B2C

### What Is Azure AD B2C?

Azure AD B2C is a separate tenant for customer identity management with:
- Custom branding
- Social identity providers (Google, Facebook, etc.)
- Progressive profiling
- Custom user journeys

### User Flows vs Custom Policies

| Feature | User Flows | Custom Policies |
|---------|-----------|----------------|
| Setup | Portal UI | XML-based (TrustFrameworkExtensions.xml) |
| Customization | Limited | Full control |
| Complexity | Low | High |
| Use Case | Standard scenarios | Complex journeys |
| MFA | Built-in | Custom enforcement |
| Progressive Profiling | Built-in | Custom claims |

### User Flows Configuration

```
┌──────────────────────────────────────────────────────────────┐
│                    Azure AD B2C User Flow                    │
├──────────────────────────────────────────────────────────────┤
│                                                              │
│  ┌──────────┐    ┌──────────┐    ┌──────────────────┐       │
│  │ Sign-Up  │───►│ Verify   │───►│ Profile Created  │       │
│  └──────────┘    │ Email    │    └──────────────────┘       │
│                  └──────────┘                                │
│  ┌──────────┐    ┌──────────┐    ┌──────────────────┐       │
│  │ Sign-In  │───►│ Verify   │───►│ Token Issued     │       │
│  └──────────┘    │ MFA      │    └──────────────────┘       │
│                  └──────────┘                                │
│  ┌──────────┐    ┌──────────┐    ┌──────────────────┐       │
│  │ Forgot   │───►│ Verify   │───►│ Reset Password   │       │
│  │ Password │    │ Email    │    └──────────────────┘       │
│  └──────────┘    └──────────┘                                │
│                                                              │
└──────────────────────────────────────────────────────────────┘
```

### Progressive Profiling

Collect user information gradually across sign-in sessions:

```json
{
  "claim": "jobTitle",
  "displayName": "Job Title",
  "supportSignUp": true,
  "required": false,
  "inputClaimMapping": [],
  "userInputType": "textBox",
  "dataProvider": {
    "metadata": {
      "property": "jobTitle"
    }
  }
}
```

### Custom UI Branding

```json
{
  "pageLayout": {
    "cover": {
      "displayName": "Welcome to My App",
      "filePath": "cover.html",
      "uri": "https://mycdn.com/b2c/cover.html"
    },
    "footer": {
      "filePath": "footer.html",
      "uri": "https://mycdn.com/b2c/footer.html"
    }
  }
}
```

### B2C Application Registration

```csharp
// Program.cs — ASP.NET Core with B2C
builder.Services.AddAuthentication(OpenIdConnectDefaults.AuthenticationScheme)
    .AddMicrosoftIdentityWebApp(builder.Configuration.GetSection("AzureAdB2C"));

// appsettings.json
{
  "AzureAdB2C": {
    "Instance": "https://myb2ctenant.b2clogin.com",
    "ClientId": "your-client-id",
    "Domain": "myb2ctenant.onmicrosoft.com",
    "SignedOutCallbackPath": "/signout/B2C_1_signupsignin",
    "SignUpSignInPolicyId": "B2C_1_signupsignin",
    "ResetPasswordPolicyId": "B2C_1_passwordreset",
    "EditProfilePolicyId": "B2C_1_editprofile"
  }
}
```

### Custom Policy XML Structure

```xml
<TrustFrameworkPolicy xmlns:xsi="www.w3.org/2001/XMLSchema-instance"
    xmlns:xsd="www.w3.org/2001/XMLSchema"
    xmlns="http://schemas.microsoft.com/online/2014/10/cp"
    PolicySchemaVersion="0.1.0.0"
    TenantId="myb2ctenant.onmicrosoft.com"
    PolicyId="B2C_1A_signup_signin_custom"
    PublicPolicyUri="https://myb2ctenant.onmicrosoft.com/B2C_1A_signup_signin_custom">

  <BuildingBlocks>
    <ClaimsSchema>
      <ClaimType Id="objectId">
        <DisplayName>User Object ID</DisplayName>
        <DataType>string</DataType>
      </ClaimType>
      <ClaimType Id="emailAddress">
        <DisplayName>Email Address</DisplayName>
        <DataType>string</DataType>
      </ClaimType>
    </ClaimsSchema>
  </BuildingBlocks>

  <RelyingParty>
    <DefaultUserJourney ReferenceId="SignUpOrSignIn" />
    <TechnicalProfile IdPolicyModel="JwtIssuer">
      <Metadata>
        <Item Key="token_lifetime_minutes">60</Item>
        <Item Key="id_token_lifetime_minutes">60</Item>
      </Metadata>
    </TechnicalProfile>
  </RelyingParty>

  <UserJourneys>
    <UserJourney Id="SignUpOrSignIn">
      <OrchestrationSteps>
        <OrchestrationStep Order="1" Type="CombinedSignInAndSignUp"
            ContentDefinitionRef="api.signuporsignin" />
        <OrchestrationStep Order="2" Type="ClaimsExchange">
          <ClaimsExchanges>
            <ClaimsExchange Id="EmailVerification"
                TechnicalProfileReferenceId="EmailVerification" />
          </ClaimsExchanges>
        </OrchestrationStep>
      </OrchestrationSteps>
    </UserJourney>
  </UserJourneys>
</TrustFrameworkPolicy>
```

---

## 4. Managed Identity Deep Dive

### System-Assigned vs User-Assigned

| Feature | System-Assigned | User-Assigned |
|---------|----------------|---------------|
| Lifecycle | Tied to resource | Independent |
| Multiple resources | One-to-one | One-to-many |
| Cross-subscription | No | Yes |
| Removal | Deletes with resource | Manual deletion |
| Re-creation | New identity | Same identity |

### SDK Usage

```csharp
// .NET — Using DefaultAzureCredential
using Azure.Identity;

var credential = new DefaultAzureCredential();

// Access Key Vault
var client = new SecretClient(
    new Uri("https://myvault.vault.azure.net/"),
    credential);

var secret = await client.GetSecretAsync("my-secret");
Console.WriteLine(secret.Value.Value);
```

### REST API Token Acquisition

```http
GET http://169.254.169.254/metadata/identity/oauth2/token
    ?api-version=2018-02-01
    &resource=https://vault.azure.net
Metadata: true
```

#### Response

```json
{
  "access_token": "eyJ0eXAiOiJKV1QiLCJhbGciOiJSUzI1NiIsIn...",
  "expires_in": "3599",
  "expires_on": "1586984701",
  "resource": "https://vault.azure.net",
  "token_type": "Bearer"
}
```

### Multi-Tenant Managed Identity

```csharp
// User-assigned identity across subscriptions
var credential = new ManagedIdentityCredential("user-assigned-client-id");
var token = await credential.GetTokenAsync(new TokenRequestContext(new[] {
    "https://management.azure.com/.default"
}));
```

---

## 5. Azure Front Door + WAF

### Architecture

```
┌─────────────────────────────────────────────────────────────────┐
│                      Azure Front Door                          │
├─────────────────────────────────────────────────────────────────┤
│                                                                 │
│  ┌──────────┐    ┌──────────┐    ┌──────────┐                  │
│  │ User     │───►│ WAF      │───►│ Routing  │                  │
│  │ Request  │    │ Rules    │    │ Engine   │                  │
│  └──────────┘    └──────────┘    └────┬─────┘                  │
│                                       │                         │
│              ┌────────────────────────┼────────────────┐       │
│              │                        │                │       │
│              ▼                        ▼                ▼       │
│  ┌──────────────────┐  ┌──────────────────┐  ┌────────────┐  │
│  │ Origin Group 1   │  │ Origin Group 2   │  │ Health     │  │
│  │ (East US)        │  │ (West Europe)    │  │ Probes     │  │
│  └──────────────────┘  └──────────────────┘  └────────────┘  │
│                                                                 │
└─────────────────────────────────────────────────────────────────┘
```

### WAF Policy Configuration

```json
{
  "properties": {
    "policySettings": {
      "mode": "Prevention",
      "state": "Enabled"
    },
    "managedRules": {
      "managedRuleSets": [
        {
          "ruleSetType": "Microsoft_DefaultRuleSet",
          "ruleSetVersion": "2.1",
          "ruleGroupOverrides": []
        }
      ]
    },
    "customRules": [
      {
        "name": "RateLimitRule",
        "ruleType": "RateLimitRule",
        "priority": 1,
        "rateLimitDurationInMinutes": 1,
        "rateLimitThreshold": 100,
        "matchConditions": [
          {
            "matchVariable": "RemoteAddr",
            "operator": "GeoMatch",
            "matchValue": ["US", "CA"]
          }
        ],
        "action": "Block"
      },
      {
        "name": "BlockSuspiciousUA",
        "ruleType": "MatchRule",
        "priority": 2,
        "matchConditions": [
          {
            "matchVariable": "RequestHeader",
            "selector": "User-Agent",
            "operator": "Contains",
            "matchValue": ["bot", "crawler"]
          }
        ],
        "action": "Block"
      }
    ]
  }
}
```

### ARM Template for Front Door + WAF

```json
{
  "$schema": "https://schema.management.azure.com/schemas/2019-04-01/deploymentTemplate.json#",
  "contentVersion": "1.0.0.0",
  "resources": [
    {
      "type": "Microsoft.Network/frontDoorWebApplicationFirewallPolicies",
      "apiVersion": "2022-05-01",
      "name": "myWafPolicy",
      "location": "Global",
      "sku": {
        "name": "Standard_AzureFrontDoor"
      },
      "properties": {
        "policySettings": {
          "mode": "Prevention",
          "state": "Enabled",
          "requestBodyCheck": true,
          "fileUploadLimitInMb": 100
        },
        "managedRules": {
          "managedRuleSets": [
            {
              "ruleSetType": "Microsoft_DefaultRuleSet",
              "ruleSetVersion": "2.1"
            }
          ]
        }
      }
    }
  ]
}
```

---

## 6. Certificate-Based Authentication

### Client Certificates in App Service

```
┌─────────────────────────────────────────────────────────┐
│                  Client Certificate Auth                │
├─────────────────────────────────────────────────────────┤
│                                                         │
│  ┌─────────┐    ┌─────────────┐    ┌───────────────┐  │
│  │ Client  │───►│ mTLS Handshake│───►│ Azure AD     │  │
│  │ App     │    │ (cert sent)  │    │ Validates    │  │
│  │         │    │              │    │ Certificate  │  │
│  └─────────┘    └─────────────┘    └───────────────┘  │
│                                                         │
└─────────────────────────────────────────────────────────┘
```

### Programmatic Certificate Selection

```csharp
using System.Security.Cryptography.X509Certificates;

public static X509Certificate2 FindCertificate(string thumbprint)
{
    using var store = new X509Store(StoreName.My, StoreLocation.CurrentUser);
    store.Open(OpenFlags.ReadOnly);
    var certs = store.Certificates.Find(
        X509FindType.FindByThumbprint,
        thumbprint,
        validOnly: true);

    return certs.Count > 0
        ? certs[0]
        : throw new InvalidOperationException("Certificate not found");
}
```

### Mutual TLS (mTLS) Configuration

```json
// Azure Front Door mTLS settings
{
  "properties": {
    "frontDoorId": "/subscriptions/{sub}/resourceGroups/{rg}/providers/Microsoft.Network/frontDoors/{fd}",
    "certificateName": "clientCert",
    "minimumTlsVersion": "TLS12",
    "trustedClientCertificates": [
      {
        "id": "/subscriptions/{sub}/resourceGroups/{rg}/providers/Microsoft.Network/frontDoorCertificates/{cert}"
      }
    ]
  }
}
```

---

## 7. Conditional Access

### Policy Structure

```
┌─────────────────────────────────────────────────────────────────┐
│                    Conditional Access Policy                   │
├─────────────────────────────────────────────────────────────────┤
│                                                                 │
│  IF (signals)                           THEN (controls)        │
│  ┌─────────────┐                       ┌─────────────────┐    │
│  │ User        │                       │ Require MFA     │    │
│  │ App         │──── Evaluates ───────►│ Block Access    │    │
│  │ Location    │                       │ Require Device  │    │
│  │ Device      │                       │ Compliance      │    │
│  │ Risk Level  │                       │ Require Policy  │    │
│  └─────────────┘                       └─────────────────┘    │
│                                                                 │
└─────────────────────────────────────────────────────────────────┘
```

### Policy Examples

| Policy Name | Conditions | Controls |
|-------------|-----------|----------|
| Block Legacy Auth | Client apps = Browser | Block |
| Require MFA for Admins | User = Admin roles | Require MFA |
| Compliant Device Only | Device = Not compliant | Block |
| High Risk Sign-in | Risk = High | Require MFA + Password Reset |
| Location Block | Location = Untrusted countries | Block |
| App Protection | App = Not protected | Block |

### Graph API Policy Creation

```http
POST https://graph.microsoft.com/v1.0/identity/conditionalAccess/policies
Content-Type: application/json

{
  "displayName": "Require MFA for All Users",
  "state": "enabled",
  "conditions": {
    "users": {
      "includeUsers": ["All"]
    },
    "applications": {
      "includeApplications": ["All"]
    },
    "clientAppTypes": ["browser", "mobileAppsAndDesktopClients"]
  },
  "grantControls": {
    "operator": "OR",
    "builtInControls": ["mfa"]
  }
}
```

---

## 8. Network Security

### Network Security Groups

| Priority | Name | Source | Destination | Port | Action |
|----------|------|--------|-------------|------|--------|
| 100 | AllowHTTPS | Internet | * | 443 | Allow |
| 200 | AllowSSH | 10.0.0.0/16 | * | 22 | Allow |
| 300 | DenyAll | * | * | * | Deny |

### Azure Firewall

```
┌──────────────────────────────────────────────────────────────┐
│                      VNet with Azure Firewall               │
├──────────────────────────────────────────────────────────────┤
│                                                              │
│  ┌──────────┐    ┌──────────────┐    ┌────────────────┐    │
│  │ Web Sub  │───►│ Azure        │───►│ Internet       │    │
│  │          │    │ Firewall     │    │                │    │
│  └──────────┘    │              │    └────────────────┘    │
│  ┌──────────┐    │  - App Rules │                           │
│  │ App Sub  │───►│  - Nat Rules │    ┌────────────────┐    │
│  │          │    │  - Net Rules │───►│ Private        │    │
│  └──────────┘    └──────────────┘    │ Endpoints      │    │
│                                      └────────────────┘    │
│  ┌──────────┐                                                │
│  │ Data Sub │                                                │
│  └──────────┘                                                │
│                                                              │
└──────────────────────────────────────────────────────────────┘
```

### Private Endpoints

```
┌──────────────────────────────────────────────────────────────┐
│                     Private Endpoints                        │
├──────────────────────────────────────────────────────────────┤
│                                                              │
│  ┌─────────┐    ┌──────────┐    ┌──────────────┐          │
│  │ App     │───►│ Private  │───►│ PaaS Service │          │
│  │ Service │    │ Endpoint │    │ (SQL, KV)    │          │
│  └─────────┘    └──────────┘    └──────────────┘          │
│       │                                                       │
│       │     ┌──────────────┐                                │
│       └────►│ Private DNS  │                                │
│             │ Zone         │                                │
│             └──────────────┘                                │
│                                                              │
└──────────────────────────────────────────────────────────────┘
```

### Service Endpoints

```json
// Enable Service Endpoint for SQL on a subnet
{
  "properties": {
    "addressPrefix": "10.0.1.0/24",
    "serviceEndpoints": [
      {
        "service": "Microsoft.Sql",
        "locations": ["eastus", "eastus2"]
      },
      {
        "service": "Microsoft.KeyVault",
        "locations": ["eastus"]
      }
    ]
  }
}
```

---

## 9. Security Best Practices for AZ-204

### Defense in Depth

```
┌─────────────────────────────────────────────────────────────────┐
│                     Defense in Depth                            │
├─────────────────────────────────────────────────────────────────┤
│                                                                 │
│  Layer 1: Perimeter    ───  DDoS Protection, WAF              │
│  Layer 2: Network      ───  NSGs, Azure Firewall              │
│  Layer 3: Access       ───  Azure AD, Conditional Access      │
│  Layer 4: Application  ───  Input validation, RBAC            │
│  Layer 5: Data         ───  Encryption at rest/transit        │
│  Layer 6: Monitoring   ───  Azure Monitor, Sentinel           │
│                                                                 │
└─────────────────────────────────────────────────────────────────┘
```

### Least Privilege

```json
{
  "RoleDefinition": {
    "RoleName": "Key Vault Secret User",
    "Assignee": "managed-identity-principal-id",
    "Actions": [
      "Microsoft.KeyVault/vaults/secrets/read"
    ],
    "NotActions": [
      "Microsoft.KeyVault/vaults/secrets/delete",
      "Microsoft.KeyVault/vaults/keys/write"
    ]
  }
}
```

### Zero Trust Principles

| Principle | Implementation |
|-----------|---------------|
| Verify explicitly | Conditional Access, MFA |
| Use least privilege | RBAC, PIM |
| Assume breach | Network segmentation, encryption |

---

## 10. Complete Architecture Diagram

```
┌─────────────────────────────────────────────────────────────────────────┐
│                      AZ-204 Security Architecture                      │
├─────────────────────────────────────────────────────────────────────────┤
│                                                                         │
│                         ┌───────────────────┐                          │
│                         │    Azure AD B2C   │                          │
│                         │  (Customer Auth)  │                          │
│                         └────────┬──────────┘                          │
│                                  │                                       │
│  ┌───────┐              ┌────────▼──────────┐                          │
│  │ User  │─────────────►│ Azure Front Door  │                          │
│  │       │              │    + WAF Policy   │                          │
│  └───────┘              └────────┬──────────┘                          │
│                                  │                                       │
│                    ┌─────────────┼─────────────┐                       │
│                    │             │             │                        │
│            ┌───────▼──┐  ┌──────▼───┐  ┌─────▼──────┐                │
│            │ App      │  │ App      │  │ App        │                │
│            │ Service  │  │ Service  │  │ Service    │                │
│            │ (East)   │  │ (West)   │  │ (EU)       │                │
│            └────┬─────┘  └────┬─────┘  └────┬───────┘                │
│                 │              │              │                         │
│                 └──────────────┼──────────────┘                        │
│                                │                                        │
│                    ┌───────────▼───────────────┐                      │
│                    │    Managed Identity        │                      │
│                    │    (System-Assigned)       │                      │
│                    └───────────┬───────────────┘                      │
│                                │                                        │
│                    ┌───────────▼───────────────┐                      │
│                    │      Azure Key Vault      │                      │
│                    │   (Secrets + Certificates)│                      │
│                    └───────────┬───────────────┘                      │
│                                │                                        │
│                    ┌───────────▼───────────────┐                      │
│                    │    Azure SQL Database      │                      │
│                    │    (Encrypted + Audited)   │                      │
│                    └───────────────────────────┘                      │
│                                                                         │
│  ┌─────────────────────────────────────────────────────────────────┐   │
│  │ Azure Monitor + Security Center + Sentinel (Monitoring Layer)  │   │
│  └─────────────────────────────────────────────────────────────────┘   │
│                                                                         │
└─────────────────────────────────────────────────────────────────────────┘
```

---

## 11. Key Concepts for AZ-204

### Quick Reference Table

| Concept | Description | Exam Weight |
|---------|-------------|-------------|
| OAuth 2.0 | Delegated authorization protocol | High |
| MSAL | Token acquisition library | High |
| Azure AD B2C | Customer identity management | Medium |
| Managed Identity | Eliminate credential management | High |
| Azure Front Door | Global load balancing + WAF | Medium |
| WAF | Web application firewall | Medium |
| Conditional Access | Risk-based access control | High |
| Private Endpoints | Private network access to PaaS | High |
| Client Certificates | Certificate-based authentication | Medium |

### Exam Tips

1. **Always use Authorization Code Flow with PKCE** — never implicit flow
2. **Managed Identity is free** — use it instead of connection strings
3. **DefaultAzureCredential** handles multiple auth methods automatically
4. **Azure Front Door WAF** uses OWASP rule sets — know the difference between Detection and Prevention modes
5. **Conditional Access** evaluates signals in real-time — understand the policy structure
6. **Private Endpoints** use Private DNS Zones — know the FQDN format
7. **Client Credentials** use `.default` scope for service-to-service
8. **B2C Custom Policies** are XML-based — user flows are portal-based
9. **mTLS** requires both client and server certificates
10. **Defense in Depth** — never rely on a single security control

### Important Ports

| Port | Protocol | Service |
|------|----------|---------|
| 443 | HTTPS | Web traffic |
| 80 | HTTP | Redirect to HTTPS |
| 1433 | TDS | SQL Server |
| 3389 | RDP | Remote Desktop |
| 22 | SSH | Linux |

### Azure AD Endpoint Formats

| Endpoint | Format |
|----------|--------|
| v2.0 | `https://login.microsoftonline.com/{tenant}/oauth2/v2.0/` |
| v1.0 | `https://login.microsoftonline.com/{tenant}/` |
| B2C | `https://{tenant}.b2clogin.com/{tenant}.onmicrosoft.com/{policy}/` |
| Graph | `https://graph.microsoft.com/v1.0/` |
| Management | `https://management.azure.com/` |
