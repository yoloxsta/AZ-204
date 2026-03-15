# Exploring Microsoft Graph — Deep Dive

## Overview

Microsoft Graph is the unified API gateway to data and intelligence in Microsoft 365, Windows, and Enterprise Mobility + Security.

---

## 1. Discover Microsoft Graph

### What is Microsoft Graph?

Microsoft Graph exposes REST APIs and client libraries to access data across Microsoft cloud services:

- **Users and Groups** — Entra ID directory data
- **Mail and Calendar** — Outlook data
- **Files** — OneDrive and SharePoint
- **Teams** — channels, messages, meetings
- **Security** — alerts, secure scores

### Endpoint

```
https://graph.microsoft.com/v1.0/    ← stable, production
https://graph.microsoft.com/beta/    ← preview features
```

### Architecture

```
Your App ──► Microsoft Graph API ──► Microsoft 365 Services
                                      ├── Entra ID (users, groups)
                                      ├── Outlook (mail, calendar)
                                      ├── OneDrive (files)
                                      ├── SharePoint (sites)
                                      ├── Teams (channels)
                                      └── Security (alerts)
```

---

## 2. Query Microsoft Graph by Using REST and SDKs

### Common REST Endpoints

| Operation | Method | Endpoint |
|-----------|--------|----------|
| Get current user | GET | `/me` |
| List users | GET | `/users` |
| Get user by ID | GET | `/users/{id}` |
| Get user's mail | GET | `/me/messages` |
| Send mail | POST | `/me/sendMail` |
| List files | GET | `/me/drive/root/children` |
| Get group members | GET | `/groups/{id}/members` |

### Using REST with curl

```bash
# Get current user profile (requires access token)
curl -H "Authorization: Bearer <access-token>" \
  https://graph.microsoft.com/v1.0/me

# List users (requires User.Read.All permission)
curl -H "Authorization: Bearer <access-token>" \
  "https://graph.microsoft.com/v1.0/users?\$top=5&\$select=displayName,mail"
```

### Query Parameters

| Parameter | Description | Example |
|-----------|-------------|---------|
| `$select` | Choose specific properties | `$select=displayName,mail` |
| `$filter` | Filter results | `$filter=department eq 'Engineering'` |
| `$orderby` | Sort results | `$orderby=displayName` |
| `$top` | Limit number of results | `$top=10` |
| `$skip` | Skip N results (paging) | `$skip=20` |
| `$count` | Include count of results | `$count=true` |
| `$expand` | Include related resources | `$expand=manager` |

### Using the .NET SDK

```bash
dotnet add package Microsoft.Graph
dotnet add package Azure.Identity
```

```csharp
using Azure.Identity;
using Microsoft.Graph;

var credential = new ClientSecretCredential(
    "<tenant-id>", "<client-id>", "<client-secret>");

var graphClient = new GraphServiceClient(credential);

// Get current user (delegated) or app info
var users = await graphClient.Users
    .GetAsync(config =>
    {
        config.QueryParameters.Top = 5;
        config.QueryParameters.Select = new[] { "displayName", "mail" };
    });

foreach (var user in users.Value)
{
    Console.WriteLine($"{user.DisplayName} - {user.Mail}");
}
```

### Handling Pagination

Graph API returns paginated results. Use `@odata.nextLink` to get the next page:

```csharp
var usersResponse = await graphClient.Users.GetAsync();

// Process first page
foreach (var user in usersResponse.Value)
    Console.WriteLine(user.DisplayName);

// Get next pages
var pageIterator = PageIterator<User, UserCollectionResponse>
    .CreatePageIterator(graphClient, usersResponse, (user) =>
    {
        Console.WriteLine(user.DisplayName);
        return true; // continue iterating
    });

await pageIterator.IterateAsync();
```

---

## 3. Apply Best Practices to Microsoft Graph

### Authentication
- Use `DefaultAzureCredential` for Azure-hosted apps
- Use `ClientSecretCredential` or `ClientCertificateCredential` for daemon apps
- Request minimum required permissions (principle of least privilege)

### Performance
- Use `$select` to request only needed properties (reduces payload)
- Use `$filter` server-side instead of filtering client-side
- Use batch requests to combine multiple calls into one HTTP request
- Handle throttling (HTTP 429) with retry-after headers

### Batch Requests

Combine up to 20 requests in a single HTTP call:

```csharp
var batchContent = new BatchRequestContentCollection(graphClient);

var userRequest = graphClient.Me.ToGetRequestInformation();
var mailRequest = graphClient.Me.Messages.ToGetRequestInformation(config =>
{
    config.QueryParameters.Top = 5;
});

var userRequestId = await batchContent.AddBatchRequestStepAsync(userRequest);
var mailRequestId = await batchContent.AddBatchRequestStepAsync(mailRequest);

var batchResponse = await graphClient.Batch.PostAsync(batchContent);

var user = await batchResponse.GetResponseByIdAsync<User>(userRequestId);
var messages = await batchResponse.GetResponseByIdAsync<MessageCollectionResponse>(mailRequestId);
```

### Throttling

Microsoft Graph returns HTTP 429 when rate limits are exceeded:
- Check the `Retry-After` header for wait time
- Implement exponential backoff
- The SDK handles basic retry automatically

### Permissions

| Type | Description | Example |
|------|-------------|---------|
| Delegated | On behalf of signed-in user | `User.Read`, `Mail.Send` |
| Application | App acts as itself | `User.Read.All`, `Mail.Read` |

Application permissions require admin consent. Delegated permissions may require user or admin consent depending on the scope.

---

## Key Exam Points

1. Microsoft Graph endpoint: `https://graph.microsoft.com/v1.0/`
2. Use `$select` to minimize response payload
3. Use `$filter` for server-side filtering
4. Batch requests combine up to 20 calls into one HTTP request
5. Handle HTTP 429 (throttling) with retry-after
6. Delegated permissions act on behalf of a user; application permissions act as the app
7. The .NET SDK (`Microsoft.Graph`) wraps the REST API with strongly-typed models
