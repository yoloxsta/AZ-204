# Developing for Storage on CDNs — Deep Dive

## Overview

This module covers Azure Content Delivery Network topics for the AZ-204 exam: CDN concepts, cache behavior control, and the .NET SDK.

---

## 1. Explore Azure Content Delivery Networks

Azure CDN caches static content at edge locations (Points of Presence / POPs) worldwide, reducing latency for end users.

### How CDN Works

```
User Request ──► Nearest Edge POP
                      │
                 Cache HIT? ──► Return cached content (fast)
                      │
                 Cache MISS ──► Fetch from origin server
                                    │
                               Cache at edge
                                    │
                               Return to user
```

### CDN Components

| Component | Description |
|-----------|-------------|
| **Profile** | A collection of CDN endpoints; defines the CDN provider |
| **Endpoint** | A public URL that serves cached content |
| **Origin** | The source server (Storage, App Service, custom URL) |
| **Origin Group** | Multiple origins for failover |
| **POP** | Edge location where content is cached |

### CDN Providers (SKUs)

| SKU | Features |
|-----|----------|
| **Standard Microsoft** | Basic caching, rules engine |
| **Standard Akamai** | Media optimization |
| **Standard Verizon** | Advanced analytics |
| **Premium Verizon** | Advanced rules engine, real-time analytics |
| **Azure Front Door** | Global load balancing + CDN (modern choice) |

---

## 2. Control Cache Behavior on Azure CDNs

### Caching Rules

CDN caching behavior is controlled by HTTP cache headers and CDN-specific rules.

#### HTTP Cache Headers

| Header | Description | Example |
|--------|-------------|---------|
| `Cache-Control` | Primary caching directive | `max-age=3600, public` |
| `Expires` | Absolute expiration date | `Thu, 15 Mar 2026 12:00:00 GMT` |
| `ETag` | Content version identifier | `"abc123"` |
| `Last-Modified` | When content last changed | `Wed, 14 Mar 2026 10:00:00 GMT` |

#### Cache-Control Directives

| Directive | Meaning |
|-----------|---------|
| `public` | Any cache can store the response |
| `private` | Only the browser can cache (not CDN) |
| `no-cache` | Must revalidate with origin before using cached copy |
| `no-store` | Don't cache at all |
| `max-age=N` | Cache for N seconds |
| `s-maxage=N` | Cache for N seconds (shared caches like CDN only) |

### CDN Caching Rules (3 Types)

1. **Global caching rules** — one rule for the entire endpoint
2. **Custom caching rules** — match on path or file extension
3. **Query string caching** — how query strings affect caching

#### Query String Caching Modes

| Mode | Behavior |
|------|----------|
| **Ignore query strings** | First request is cached; all subsequent requests get the same cached version regardless of query string |
| **Bypass caching for query strings** | Requests with query strings are not cached |
| **Cache every unique URL** | Each unique query string gets its own cached version |

### Setting Cache Rules via CLI

```bash
# Set default caching behavior
az cdn endpoint update \
  --name myendpoint \
  --profile-name myprofile \
  --resource-group myRG \
  --query-string-caching-behavior IgnoreQueryString

# Set default TTL (override origin headers)
az cdn endpoint rule add \
  --name myendpoint \
  --profile-name myprofile \
  --resource-group myRG \
  --rule-name "CacheImages" \
  --order 1 \
  --match-variable UrlFileExtension \
  --operator Equal \
  --match-values jpg png gif \
  --action-name CacheExpiration \
  --cache-behavior Override \
  --cache-duration "7.00:00:00"
```

### Cache Purging

Force removal of cached content before TTL expires:

```bash
# Purge specific paths
az cdn endpoint purge \
  --name myendpoint \
  --profile-name myprofile \
  --resource-group myRG \
  --content-paths "/images/*" "/css/style.css"

# Purge everything
az cdn endpoint purge \
  --name myendpoint \
  --profile-name myprofile \
  --resource-group myRG \
  --content-paths "/*"
```

### Pre-loading Content

Load content into the CDN cache before users request it:

```bash
az cdn endpoint load \
  --name myendpoint \
  --profile-name myprofile \
  --resource-group myRG \
  --content-paths "/images/hero.jpg" "/js/app.js"
```

---

## 3. Interact with Azure CDN by Using .NET

### NuGet Package

```bash
dotnet add package Azure.ResourceManager.Cdn
dotnet add package Azure.Identity
```

### Managing CDN Resources with the SDK

```csharp
using Azure.Identity;
using Azure.ResourceManager;
using Azure.ResourceManager.Cdn;
using Azure.ResourceManager.Cdn.Models;

var credential = new DefaultAzureCredential();
var armClient = new ArmClient(credential);

// Get subscription
var subscription = await armClient.GetDefaultSubscriptionAsync();

// Get resource group
var resourceGroup = await subscription
    .GetResourceGroupAsync("myRG");

// List CDN profiles
await foreach (var profile in resourceGroup.Value.GetProfiles())
{
    Console.WriteLine($"Profile: {profile.Data.Name}, SKU: {profile.Data.Sku.Name}");

    // List endpoints in profile
    await foreach (var endpoint in profile.GetCdnEndpoints())
    {
        Console.WriteLine($"  Endpoint: {endpoint.Data.Name}");
        Console.WriteLine($"  Hostname: {endpoint.Data.HostName}");
        Console.WriteLine($"  State: {endpoint.Data.ResourceState}");
    }
}
```

### Purging Content Programmatically

```csharp
// Get endpoint
var profile = await resourceGroup.Value.GetProfileAsync("myprofile");
var endpoint = await profile.Value.GetCdnEndpointAsync("myendpoint");

// Purge content
var purgeContent = new PurgeContent(new[] { "/images/*", "/css/*" });
await endpoint.Value.PurgeContentAsync(
    Azure.WaitUntil.Completed,
    purgeContent);

Console.WriteLine("Purge completed.");
```

### Creating a CDN Profile and Endpoint

```csharp
// Create profile
var profileData = new ProfileData(
    Azure.Core.AzureLocation.EastUS,
    new CdnSku { Name = CdnSkuName.StandardMicrosoft });

var profileOperation = await resourceGroup.Value
    .GetProfiles()
    .CreateOrUpdateAsync(
        Azure.WaitUntil.Completed,
        "myprofile",
        profileData);

// Create endpoint with Blob Storage origin
var endpointData = new CdnEndpointData(Azure.Core.AzureLocation.EastUS)
{
    Origins =
    {
        new DeepCreatedOrigin("blob-origin")
        {
            HostName = "mystorageaccount.blob.core.windows.net"
        }
    },
    IsHttpAllowed = false,
    IsHttpsAllowed = true,
    QueryStringCachingBehavior = QueryStringCachingBehavior.IgnoreQueryString
};

var endpointOperation = await profileOperation.Value
    .GetCdnEndpoints()
    .CreateOrUpdateAsync(
        Azure.WaitUntil.Completed,
        "myendpoint",
        endpointData);

Console.WriteLine($"Endpoint created: {endpointOperation.Value.Data.HostName}");
```

---

## CDN with Azure Storage — Common Pattern

```
┌──────────┐     ┌──────────────┐     ┌──────────────────┐
│  Browser  │────►│  CDN Edge    │────►│  Blob Storage    │
│           │     │  POP         │     │  (Origin)        │
│           │     │              │     │                  │
│           │◄────│  Cached      │     │  /images/        │
│           │     │  Response    │     │  /css/           │
└──────────┘     └──────────────┘     │  /js/            │
                                      └──────────────────┘
```

Steps to set up:
1. Create a Storage Account with static website or blob container
2. Create a CDN profile and endpoint pointing to the storage account
3. Configure caching rules and custom domain (optional)
4. Access content via CDN URL: `https://myendpoint.azureedge.net/images/photo.jpg`

---

## Key Exam Points

1. CDN caches static content at edge POPs worldwide
2. `Cache-Control` headers from the origin control default caching behavior
3. CDN rules can override origin cache headers
4. Query string caching has three modes: ignore, bypass, cache unique
5. Purge removes cached content before TTL expires
6. Pre-load warms the cache before users request content
7. CDN endpoints have their own hostname: `*.azureedge.net`
8. Use `s-maxage` to set CDN-specific cache duration separate from browser cache
