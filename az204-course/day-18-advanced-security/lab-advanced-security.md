# Lab: Advanced Security Patterns

## Title

Implement OAuth 2.0, B2C, and WAF Security

## Objective

- Configure Azure AD B2C user flows
- Implement OAuth 2.0 authorization code flow with MSAL
- Set up managed identity for App Service
- Configure Azure Front Door with WAF

## Prerequisites

- Azure subscription with Contributor access
- Azure CLI or Cloud Shell
- .NET 8 SDK installed
- Visual Studio or VS Code

## Architecture

```
┌─────────────────────────────────────────────────────────────────────┐
│                    Lab Architecture Overview                        │
├─────────────────────────────────────────────────────────────────────┤
│                                                                     │
│  ┌───────┐    ┌──────────────┐    ┌──────────────────────────┐    │
│  │ User  │───►│ Azure AD B2C │───►│ Azure App Service        │    │
│  │       │    │              │    │ (MSAL Auth)              │    │
│  └───────┘    └──────────────┘    └────────────┬─────────────┘    │
│                                                  │                  │
│                                       ┌──────────▼──────────┐     │
│                                       │ Managed Identity     │     │
│                                       └──────────┬──────────┘     │
│                                                  │                  │
│                              ┌────────────────────┼───────┐       │
│                              │                    │       │        │
│                    ┌─────────▼──┐  ┌─────────────▼┐  ┌──▼──────┐│
│                    │ Key Vault  │  │ Azure SQL    │  │ Storage ││
│                    └────────────┘  └──────────────┘  └─────────┘│
│                                                                     │
│  ┌──────────────────────────────────────────────────────────────┐  │
│  │ Azure Front Door + WAF (Global Load Balancing + Protection) │  │
│  └──────────────────────────────────────────────────────────────┘  │
│                                                                     │
└─────────────────────────────────────────────────────────────────────┘
```

## Estimated Time

90 minutes

---

## Step 1: Create Azure AD B2C Tenant

### 1.1 Create B2C Tenant via Azure CLI

```bash
# Variables
B2C_TENANT_NAME="myb2ctenant"
RESOURCE_GROUP="rg-b2c-lab"
LOCATION="eastus"

# Create resource group
az group create \
  --name $RESOURCE_GROUP \
  --location $LOCATION

# Create B2C tenant
az rest --method post \
  --uri "https://management.azure.com/providers/Microsoft.AzureActiveDirectory/b2cDirectories?api-version=2021-04-01" \
  --body '{
    "displayName": "MyB2CTenant",
    "countryCode": "US",
    "location": "United States",
    "sku": {
      "name": "A1",
      "tier": "Premium"
    },
    "properties": {
      "createTenantProperties": {
        "displayName": "MyB2CTenant",
        "countryCode": "US"
      }
    }
  }'
```

### 1.2 Switch to B2C Tenant

```bash
# List available tenants
az tenant list --query "[].{Name:displayName, ID:id}"

# Switch to B2C tenant
az account set --subscription "your-subscription-id"
az login --tenant "myb2ctenant.onmicrosoft.com"
```

### 1.3 Verify B2C Tenant

```bash
# Confirm you're in the B2C tenant
az account show --query "tenantId" -o tsv
```

**Expected output:**
```
b2c-tenant-guid-here
```

---

## Step 2: Create User Flows

### 2.1 Create Sign-Up/Sign-In User Flow

```bash
# Create sign-up/sign-in user flow
az rest --method post \
  --uri "https://graph.microsoft.com/beta/identity/b2cUserFlows" \
  --body '{
    "id": "B2C_1_signupsignin",
    "displayName": "Sign up and Sign in",
    "userFlowType": "signUpOrSignIn",
    "userFlowTypeEnabled": true
  }' \
  --headers "Content-Type=application/json"
```

### 2.2 Create Password Reset User Flow

```bash
# Create password reset user flow
az rest --method post \
  --uri "https://graph.microsoft.com/beta/identity/b2cUserFlows" \
  --body '{
    "id": "B2C_1_passwordreset",
    "displayName": "Password Reset",
    "userFlowType": "passwordReset",
    "userFlowTypeEnabled": true
  }' \
  --headers "Content-Type=application/json"
```

### 2.3 Configure User Flow Attributes

```bash
# Add user attributes to sign-up/sign-in flow
az rest --method patch \
  --uri "https://graph.microsoft.com/beta/identity/b2cUserFlows/B2C_1_signupsignin" \
  --body '{
    "userFlowAttributes": [
      {
        "id": "email",
        "required": true,
        "readOnly": false
      },
      {
        "id": "displayName",
        "required": true,
        "readOnly": false
      },
      {
        "id": "givenName",
        "required": true,
        "readOnly": false
      },
      {
        "id": "surname",
        "required": true,
        "readOnly": false
      }
    ]
  }' \
  --headers "Content-Type=application/json"
```

### 2.4 Enable MFA on User Flow

```bash
# Enable MFA for sign-up/sign-in
az rest --method patch \
  --uri "https://graph.microsoft.com/beta/identity/b2cUserFlows/B2C_1_signupsignin" \
  --body '{
    "authenticationConfiguration": {
      "signInPolicy": {
        "mfa": {
          "isEnabled": true
        }
      }
    }
  }' \
  --headers "Content-Type=application/json"
```

---

## Step 3: Register an Application in B2C

### 3.1 Create App Registration

```bash
# Create app registration
az rest --method post \
  --uri "https://graph.microsoft.com/beta/applications" \
  --body '{
    "displayName": "MyB2CWebApp",
    "signInAudience": "AzureADandPersonalMicrosoftAccount",
    "web": {
      "redirectUris": [
        "https://localhost:5001/signin-oidc",
        "https://myapp.azurewebsites.net/signin-oidc"
      ],
      "implicitGrantSettings": {
        "enableIdTokenIssuance": true,
        "enableAccessTokenIssuance": false
      }
    },
    "requiredResourceAccess": [
      {
        "resourceAppId": "00000000-0000-0000-0000-000000000000",
        "resourceAccess": [
          {
            "id": "e1fe6dd8-ba31-4d61-89e7-88639da4683d",
            "type": "Scope"
          }
        ]
      }
    ]
  }' \
  --headers "Content-Type=application/json"
```

### 3.2 Create Client Secret

```bash
# Get application ID
APP_ID=$(az rest --method get \
  --uri "https://graph.microsoft.com/beta/applications?$filter=displayName eq 'MyB2CWebApp'" \
  --query "value[0].id" -o tsv)

# Create client secret
az rest --method post \
  --uri "https://graph.microsoft.com/beta/applications/$APP_ID/addPassword" \
  --body '{
    "passwordCredential": {
      "displayName": "LabSecret",
      "endDateTime": "2027-01-01T00:00:00Z"
    }
  }' \
  --headers "Content-Type=application/json"
```

**Save the secret value from the response.**

### 3.3 Configure API Permissions

```bash
# Add Microsoft Graph permissions
az rest --method post \
  --uri "https://graph.microsoft.com/beta/applications/$APP_ID/addPassword" \
  --body '{
    "requiredResourceAccess": [
      {
        "resourceAppId": "00000000-0000-0000-0000-000000000000",
        "resourceAccess": [
          {
            "id": "e1fe6dd8-ba31-4d61-89e7-88639da4683d",
            "type": "Scope"
          },
          {
            "id": "0e263e50-5827-48a4-ba22-ffcdca9ccc28",
            "type": "Scope"
          }
        ]
      }
    ]
  }' \
  --headers "Content-Type=application/json"
```

---

## Step 4: Create a .NET Web App with MSAL Authentication

### 4.1 Create the Project

```bash
dotnet new webapp -n B2CSecurityLab -o B2CSecurityLab
cd B2CSecurityLab
dotnet add package Microsoft.Identity.Web
dotnet add package Microsoft.Identity.Web.UI
dotnet add package Azure.Identity
dotnet add package Azure.Security.KeyVault.Secrets
```

### 4.2 Configure appsettings.json

```json
{
  "AzureAdB2C": {
    "Instance": "https://myb2ctenant.b2clogin.com",
    "Domain": "myb2ctenant.onmicrosoft.com",
    "ClientId": "your-app-client-id",
    "TenantId": "your-b2c-tenant-id",
    "SignUpSignInPolicyId": "B2C_1_signupsignin",
    "ResetPasswordPolicyId": "B2C_1_passwordreset",
    "EditProfilePolicyId": "B2C_1_editprofile",
    "CallbackPath": "/signin-oidc",
    "AllowWebApiToBeAuthorizedByACL": true
  },
  "KeyVault": {
    "VaultUri": "https://mykeyvault.vault.azure.net/"
  },
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning"
    }
  }
}
```

### 4.3 Configure Program.cs

```csharp
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.Identity.Web;
using Microsoft.Identity.Web.UI;
using Azure.Identity;

var builder = WebApplication.CreateBuilder(args);

// Add B2C authentication
builder.Services.AddAuthentication(OpenIdConnectDefaults.AuthenticationScheme)
    .AddMicrosoftIdentityWebApp(builder.Configuration.GetSection("AzureAdB2C"));

builder.Services.AddControllersWithViews();
builder.Services.AddRazorPages();

// Add Key Vault client with managed identity
builder.Services.AddSingleton(new SecretClient(
    new Uri(builder.Configuration["KeyVault:VaultUri"]!),
    new DefaultAzureCredential()));

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");
app.MapRazorPages();

app.Run();
```

### 4.4 Create Protected Controller

```csharp
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Azure.Security.KeyVault.Secrets;

namespace B2CSecurityLab.Controllers;

[Authorize]
public class SecureController : Controller
{
    private readonly SecretClient _secretClient;
    private readonly ILogger<SecureController> _logger;

    public SecureController(SecretClient secretClient, ILogger<SecureController> logger)
    {
        _secretClient = secretClient;
        _logger = logger;
    }

    public async Task<IActionResult> Index()
    {
        // Get user claims
        var userName = User.Identity?.Name;
        var claims = User.Claims.Select(c => new { c.Type, c.Value });

        ViewBag.UserName = userName;
        ViewBag.Claims = claims;

        return View();
    }

    [Authorize(Policy = "AdminOnly")]
    public async Task<IActionResult> Admin()
    {
        // Retrieve secret from Key Vault using managed identity
        try
        {
            var secret = await _secretClient.GetSecretAsync("app-secret");
            ViewBag.SecretValue = secret.Value.Value;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to retrieve secret");
            ViewBag.SecretValue = "Error retrieving secret";
        }

        return View();
    }
}
```

### 4.5 Create Authorization Policy

```csharp
// Add to Program.cs
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("AdminOnly", policy =>
        policy.RequireClaim("jobTitle", "Administrator"));
});
```

---

## Step 5: Implement Authorization Code Flow with PKCE

### 5.1 Manual PKCE Implementation

```csharp
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace B2CSecurityLab.Services;

public class PkceOAuthService
{
    private readonly HttpClient _httpClient;
    private readonly IConfiguration _configuration;

    public PkceOAuthService(HttpClient httpClient, IConfiguration configuration)
    {
        _httpClient = httpClient;
        _configuration = configuration;
    }

    public (string codeVerifier, string codeChallenge) GeneratePkceValues()
    {
        byte[] buffer = new byte[32];
        using var rng = RandomNumberGenerator.Create();
        rng.GetBytes(buffer);

        string codeVerifier = Convert.ToBase64String(buffer)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');

        using var sha256 = SHA256.Create();
        byte[] hash = sha256.ComputeHash(Encoding.ASCII.GetBytes(codeVerifier));
        string codeChallenge = Convert.ToBase64String(hash)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');

        return (codeVerifier, codeChallenge);
    }

    public string BuildAuthorizationUrl(string codeChallenge, string state)
    {
        var tenant = _configuration["AzureAdB2C:Domain"];
        var clientId = _configuration["AzureAdB2C:ClientId"];
        var redirectUri = _configuration["AzureAdB2C:CallbackPath"];
        var policy = _configuration["AzureAdB2C:SignUpSignInPolicyId"];

        return $"https://{tenant}.b2clogin.com/{tenant}/oauth2/v2.0/authorize" +
               $"?client_id={clientId}" +
               $"&response_type=code" +
               $"&redirect_uri={Uri.EscapeDataString(redirectUri)}" +
               $"&response_mode=query" +
               $"&scope=openid profile email" +
               $"&state={state}" +
               $"&code_challenge={codeChallenge}" +
               $"&code_challenge_method=S256" +
               $"&p={policy}";
    }

    public async Task<OAuthTokenResponse> ExchangeCodeForTokensAsync(
        string code, string codeVerifier)
    {
        var tenant = _configuration["AzureAdB2C:Domain"];
        var clientId = _configuration["AzureAdB2C:ClientId"];
        var redirectUri = _configuration["AzureAdB2C:CallbackPath"];
        var secret = _configuration["AzureAdB2C:ClientSecret"];

        var requestBody = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["client_id"] = clientId,
            ["client_secret"] = secret!,
            ["grant_type"] = "authorization_code",
            ["code"] = code,
            ["redirect_uri"] = redirectUri!,
            ["code_verifier"] = codeVerifier,
            ["scope"] = "openid profile email"
        });

        var response = await _httpClient.PostAsync(
            $"https://{tenant}.b2clogin.com/{tenant}/oauth2/v2.0/token",
            requestBody);

        var json = await response.Content.ReadAsStringAsync();
        return JsonSerializer.Deserialize<OAuthTokenResponse>(json)!;
    }
}

public record OAuthTokenResponse(
    string AccessToken,
    string IdToken,
    string RefreshToken,
    int ExpiresIn,
    string TokenType);
```

### 5.2 Create OAuth Controller

```csharp
using Microsoft.AspNetCore.Mvc;

namespace B2CSecurityLab.Controllers;

public class OAuthController : Controller
{
    private readonly PkceOAuthService _oauthService;

    public OAuthController(PkceOAuthService oauthService)
    {
        _oauthService = oauthService;
    }

    public IActionResult Authorize()
    {
        var (codeVerifier, codeChallenge) = _oauthService.GeneratePkceValues();
        var state = Guid.NewGuid().ToString();

        // Store codeVerifier in session
        HttpContext.Session.SetString("code_verifier", codeVerifier);
        HttpContext.Session.SetString("oauth_state", state);

        var authUrl = _oauthService.BuildAuthorizationUrl(codeChallenge, state);
        return Redirect(authUrl);
    }

    public async Task<IActionResult> Callback(string code, string state)
    {
        var storedState = HttpContext.Session.GetString("oauth_state");
        if (state != storedState)
        {
            return BadRequest("Invalid state parameter");
        }

        var codeVerifier = HttpContext.Session.GetString("code_verifier")!;
        var tokens = await _oauthService.ExchangeCodeForTokensAsync(code, codeVerifier);

        // Store tokens securely
        HttpContext.Session.SetString("access_token", tokens.AccessToken);
        HttpContext.Session.SetString("id_token", tokens.IdToken);

        return RedirectToAction("Index", "Secure");
    }
}
```

---

## Step 6: Create App Service with System-Assigned Managed Identity

### 6.1 Create App Service Plan and App

```bash
# Variables
APP_NAME="b2clab-$(date +%s)"
RESOURCE_GROUP="rg-b2c-lab"
LOCATION="eastus"

# Create App Service Plan
az appservice plan create \
  --name "plan-b2clab" \
  --resource-group $RESOURCE_GROUP \
  --location $LOCATION \
  --sku B1 \
  --is-linux

# Create Web App with managed identity
az webapp create \
  --name $APP_NAME \
  --resource-group $RESOURCE_GROUP \
  --plan "plan-b2clab" \
  --runtime "DOTNET|8.0" \
  --assign-identity "[system]"
```

### 6.2 Configure App Settings

```bash
# Set app settings
az webapp config appsettings set \
  --name $APP_NAME \
  --resource-group $RESOURCE_GROUP \
  --settings \
    "AzureAdB2C__Instance=https://myb2ctenant.b2clogin.com" \
    "AzureAdB2C__Domain=myb2ctenant.onmicrosoft.com" \
    "AzureAdB2C__ClientId=your-client-id" \
    "AzureAdB2C__SignUpSignInPolicyId=B2C_1_signupsignin" \
    "AzureAdB2C__ResetPasswordPolicyId=B2C_1_passwordreset" \
    "KeyVault__VaultUri=https://mykeyvault.vault.azure.net/"
```

### 6.3 Get Managed Identity Principal ID

```bash
# Get the managed identity principal ID
PRINCIPAL_ID=$(az webapp show \
  --name $APP_NAME \
  --resource-group $RESOURCE_GROUP \
  --query "identity.principalId" -o tsv)

echo "Managed Identity Principal ID: $PRINCIPAL_ID"
```

---

## Step 7: Grant Managed Identity Access to Key Vault

### 7.1 Create Key Vault

```bash
# Create Key Vault
az keyvault create \
  --name "kv-b2clab" \
  --resource-group $RESOURCE_GROUP \
  --location $LOCATION \
  --sku standard

# Store a secret
az keyvault secret set \
  --vault-name "kv-b2clab" \
  --name "app-secret" \
  --value "SuperSecretValue123!"
```

### 7.2 Grant Access to Managed Identity

```bash
# Grant Key Vault access to managed identity
az keyvault set-policy \
  --name "kv-b2clab" \
  --resource-group $RESOURCE_GROUP \
  --object-id $PRINCIPAL_ID \
  --secret-permissions get list
```

### 7.3 Verify Access

```bash
# Test access from App Service
az webapp ssh \
  --name $APP_NAME \
  --resource-group $RESOURCE_GROUP

# Inside the container, run:
curl -H "Metadata:true" \
  "http://169.254.169.254/metadata/identity/oauth2/token?api-version=2018-02-01&resource=https://vault.azure.net"
```

---

## Step 8: Create Azure Front Door with WAF Policy

### 8.1 Create WAF Policy

```bash
# Create WAF Policy
az network front-door waf-policy create \
  --name "waf-b2clab" \
  --resource-group $RESOURCE_GROUP \
  --location global \
  --sku Standard_AzureFrontDoor \
  --mode Prevention \
  --state Enabled
```

### 8.2 Add Custom WAF Rules

```bash
# Add rate limiting rule
az network front-door waf-policy rule create \
  --policy-name "waf-b2clab" \
  --resource-group $RESOURCE_GROUP \
  --name "RateLimitRule" \
  --priority 1 \
  --rule-type RateLimitRule \
  --rate-limit-duration-in-minutes 1 \
  --rate-limit-threshold 100 \
  --action Block
```

### 8.3 Add Geo-Filtering Rule

```bash
# Add geo-filtering rule
az network front-door waf-policy managed-rule rule-set add \
  --policy-name "waf-b2clab" \
  --resource-group $RESOURCE_GROUP \
  --type Microsoft_DefaultRuleSet \
  --version 2.1

az network front-door waf-policy managed-rule rule-set rule-group add \
  --policy-name "waf-b2clab" \
  --resource-group $RESOURCE_GROUP \
  --type Microsoft_DefaultRuleSet \
  --version 2.1 \
  --rule-group-name "SQLI"
```

### 8.4 Create Front Door

```bash
# Create Front Door profile
az network front-door create \
  --name "fd-b2clab" \
  --resource-group $RESOURCE_GROUP \
  --sku Standard_AzureFrontDoor \
  --waf-policy "waf-b2clab"

# Create origin group
az network front-door origin-group create \
  --front-door-name "fd-b2clab" \
  --resource-group $RESOURCE_GROUP \
  --name "origin-group-1" \
  --probe-path "/" \
  --probe-interval 30 \
  --probe-protocol HTTPS \
  --probe-request-type GET

# Create origin (your App Service)
az network front-door origin create \
  --front-door-name "fd-b2clab" \
  --origin-group-name "origin-group-1" \
  --resource-group $RESOURCE_GROUP \
  --name "app-service-origin" \
  --host-name $APP_NAME.azurewebsites.net \
  --origin-host-header $APP_NAME.azurewebsites.net \
  --priority 1 \
  --weight 1000

# Create routing rule
az network front-door routing-rule create \
  --front-door-name "fd-b2clab" \
  --resource-group $RESOURCE_GROUP \
  --name "routing-rule-1" \
  --frontend-endpoint-name "default" \
  --accepted-protocols HTTPS \
  --patterns-to-match "/*" \
  --route-type Forward \
  --origin-group "origin-group-1" \
  --forwarding-protocol HTTPSOnly
```

### 8.5 Configure WAF in Prevention Mode

```bash
# Update WAF policy to Prevention mode
az network front-door waf-policy update \
  --name "waf-b2clab" \
  --resource-group $RESOURCE_GROUP \
  --mode Prevention \
  --state Enabled
```

---

## Step 9: Configure WAF Custom Rules

### 9.1 Create Custom Rule for SQL Injection

```bash
# Create SQL injection protection rule
az network front-door waf-policy rule create \
  --policy-name "waf-b2clab" \
  --resource-group $RESOURCE_GROUP \
  --name "SQLInjectionProtection" \
  --priority 2 \
  --rule-type MatchRule \
  --action Block \
  --match-variable RequestUri \
  --operator Contains \
  --match-value "SELECT" "INSERT" "UPDATE" "DELETE" "DROP" "UNION"
```

### 9.2 Create Bot Protection Rule

```bash
# Block known bot user agents
az network front-door waf-policy rule create \
  --policy-name "waf-b2clab" \
  --resource-group $RESOURCE_GROUP \
  --name "BlockBots" \
  --priority 3 \
  --rule-type MatchRule \
  --action Block \
  --match-variable RequestHeader \
  --selector "User-Agent" \
  --operator Contains \
  --match-value "bot" "crawler" "spider"
```

### 9.3 Create IP Allowlist Rule

```bash
# Allow only specific IPs
az network front-door waf-policy rule create \
  --policy-name "waf-b2clab" \
  --resource-group $RESOURCE_GROUP \
  --name "AllowTrustedIPs" \
  --priority 4 \
  --rule-type MatchRule \
  --action Allow \
  --match-variable RemoteAddr \
  --operator IPMatch \
  --match-value "203.0.113.0/24" "198.51.100.0/24"
```

---

## Step 10: Test End-to-End Security Flow

### 10.1 Test B2C Sign-Up Flow

```bash
# Open browser and navigate to B2C sign-up
FRONT_DOOR_URL="https://fd-b2clab.azurefd.net"
echo "Navigate to: $FRONT_DOOR_URL"
echo "Click 'Sign up now' and complete registration"
```

### 10.2 Test OAuth Flow

```bash
# Test the OAuth endpoint
curl -v "https://$FRONT_DOOR_URL/OAuth/Authorize" \
  --max-redirs 0 \
  -o /dev/null
```

### 10.3 Test Managed Identity Access

```bash
# SSH into App Service and test Key Vault access
az webapp ssh --name $APP_NAME --resource-group $RESOURCE_GROUP

# Inside container, run diagnostic script:
cat << 'EOF' > /tmp/test-identity.sh
#!/bin/bash
echo "=== Testing Managed Identity ==="

# Get token
TOKEN=$(curl -s -H "Metadata:true" \
  "http://169.254.169.254/metadata/identity/oauth2/token?api-version=2018-02-01&resource=https://vault.azure.net" \
  | jq -r '.access_token')

echo "Token acquired: ${TOKEN:0:20}..."

# Access Key Vault
SECRET=$(curl -s \
  -H "Authorization: Bearer $TOKEN" \
  "https://kv-b2clab.vault.azure.net/secrets/app-secret?api-version=7.4" \
  | jq -r '.value')

echo "Secret value: $SECRET"
EOF

chmod +x /tmp/test-identity.sh
/tmp/test-identity.sh
```

### 10.4 Test WAF Protection

```bash
# Test SQL injection protection (should be blocked)
curl -v "https://$FRONT_DOOR_URL/?id=1%20UNION%20SELECT%20*%20FROM%20users" \
  -o /dev/null \
  -w "HTTP Status: %{http_code}\n"

# Test rate limiting
for i in {1..150}; do
  curl -s "https://$FRONT_DOOR_URL/" -o /dev/null &
done
wait
echo "Rate limit test completed"
```

### 10.5 Test Conditional Access

```bash
# Verify Conditional Access policy effects
az rest --method get \
  --uri "https://graph.microsoft.com/beta/identity/conditionalAccess/policies" \
  --query "value[].{Name:displayName, State:state}" -o table
```

---

## Step 11: Clean Up Resources

### 11.1 Delete All Resources

```bash
# Delete resource group (removes all resources)
az group delete \
  --name $RESOURCE_GROUP \
  --yes \
  --no-wait

echo "Resource group deletion initiated"
```

### 11.2 Delete B2C Tenant (if no longer needed)

```bash
# List B2C tenants
az rest --method get \
  --uri "https://management.azure.com/providers/Microsoft.AzureActiveDirectory/b2cDirectories?api-version=2021-04-01" \
  --query "value[].{Name:displayName, ID:id}"

# Delete B2C tenant (takes time)
az rest --method delete \
  --uri "https://management.azure.com/providers/Microsoft.AzureActiveDirectory/b2cDirectories/{tenant-id}?api-version=2021-04-01"
```

### 11.3 Verify Cleanup

```bash
# Verify resources are deleted
az group show --name $RESOURCE_GROUP --query "properties.provisioningState" -o tsv
```

---

## Lab Summary

| Component | Purpose | Status |
|-----------|---------|--------|
| Azure AD B2C Tenant | Customer identity | Created |
| User Flows | Sign-up/sign-in, password reset | Configured |
| App Registration | OAuth client | Registered |
| .NET Web App | MSAL authentication | Deployed |
| Managed Identity | Key Vault access | Configured |
| Azure Front Door | Global load balancing | Created |
| WAF Policy | Web protection | Enabled |

## Key Takeaways

1. **B2C provides customer identity management** separate from organizational Azure AD
2. **PKCE prevents authorization code interception** — always use it with SPAs and mobile apps
3. **Managed Identity eliminates secrets** — use it for all Azure resource access
4. **WAF protects against common attacks** — SQL injection, XSS, bots
5. **Azure Front Door provides global reach** with built-in DDoS protection
6. **Conditional Access adds risk-based controls** — location, device, user signals
7. **Defense in depth** requires multiple security layers working together

## Next Steps

- Implement Azure AD B2C custom policies
- Add Azure Application Insights monitoring
- Configure Azure Sentinel for security analytics
- Implement Azure Key Vault with HSM backing
- Set up Azure AD Privileged Identity Management
