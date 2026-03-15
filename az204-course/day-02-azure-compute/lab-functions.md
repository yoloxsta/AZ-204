# Lab: Create and Deploy Azure Functions

## Title
Build Serverless Functions with Azure Functions

## Objective
- Create a Function App with the Consumption plan
- Create an HTTP-triggered function
- Create a Timer-triggered function
- Configure input and output bindings
- Test functions locally and in Azure

## Architecture

```
┌──────────────────────────────────────────────────────┐
│           Resource Group: az204-func-rg              │
│                                                      │
│  ┌──────────────┐    ┌────────────────────────────┐  │
│  │   Storage    │    │  Function App              │  │
│  │   Account    │◄───│  az204-func-<unique>       │  │
│  │  (required)  │    │                            │  │
│  └──────────────┘    │  ┌──────────────────────┐  │  │
│                      │  │ HTTP Trigger Function │  │  │
│                      │  │ GET/POST → Response   │  │  │
│                      │  └──────────────────────┘  │  │
│                      │  ┌──────────────────────┐  │  │
│                      │  │ Timer Trigger Function│  │  │
│                      │  │ Every 5 min → Log     │  │  │
│                      │  └──────────────────────┘  │  │
│                      │  ┌──────────────────────┐  │  │
│                      │  │ Queue Trigger Function│  │  │
│                      │  │ Queue msg → Blob out  │  │  │
│                      │  └──────────────────────┘  │  │
│                      │                            │  │
│                      │  Plan: Consumption (Y1)    │  │
│                      └────────────────────────────┘  │
└──────────────────────────────────────────────────────┘
```

## Prerequisites
- Azure CLI installed and logged in
- Azure Functions Core Tools v4 installed
- .NET 8 SDK installed
- Visual Studio Code with Azure Functions extension (optional)

Install Azure Functions Core Tools:
```bash
# Windows (winget)
winget install Microsoft.Azure.FunctionsCoreTools

# macOS
brew tap azure/functions
brew install azure-functions-core-tools@4

# Verify
func --version
```

---

## Step 1 — Create a Resource Group and Storage Account

Every Function App requires a storage account for internal state management.

```bash
az group create \
  --name az204-func-rg \
  --location eastus

az storage account create \
  --name az204funcstore<unique> \
  --resource-group az204-func-rg \
  --location eastus \
  --sku Standard_LRS
```

---

## Step 2 — Create a Function App in Azure

```bash
az functionapp create \
  --name az204-func-<unique> \
  --resource-group az204-func-rg \
  --storage-account az204funcstore<unique> \
  --consumption-plan-location eastus \
  --runtime dotnet-isolated \
  --runtime-version 8 \
  --functions-version 4 \
  --os-type Linux
```

Verify:
```bash
az functionapp show \
  --name az204-func-<unique> \
  --resource-group az204-func-rg \
  --query "{name:name, state:state, runtime:siteConfig.linuxFxVersion, defaultHostName:defaultHostName}" \
  --output table
```

---

## Step 3 — Create a Function Project Locally

```bash
# Create a new directory and initialize a Functions project
mkdir az204-functions
cd az204-functions

func init --worker-runtime dotnet-isolated --target-framework net8.0
```

This creates:
- `host.json` — global configuration for all functions
- `local.settings.json` — local environment settings
- `.csproj` — project file with Azure Functions SDK references

---

## Step 4 — Create an HTTP-Triggered Function

```bash
func new --name HttpExample --template "HTTP trigger" --authlevel anonymous
```

This creates `HttpExample.cs`. Open it and review the code:

```csharp
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using System.Net;

namespace az204_functions
{
    public class HttpExample
    {
        private readonly ILogger _logger;

        public HttpExample(ILoggerFactory loggerFactory)
        {
            _logger = loggerFactory.CreateLogger<HttpExample>();
        }

        [Function("HttpExample")]
        public HttpResponseData Run(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", "post")] HttpRequestData req)
        {
            _logger.LogInformation("C# HTTP trigger function processed a request.");

            var response = req.CreateResponse(HttpStatusCode.OK);
            response.Headers.Add("Content-Type", "text/plain; charset=utf-8");
            response.WriteString("Hello from Azure Functions!");
            return response;
        }
    }
}
```

---

## Step 5 — Test Locally

```bash
func start
```

The function starts locally. Test it:
```bash
curl http://localhost:7071/api/HttpExample
```

Expected output: `Hello from Azure Functions!`

Test with a query parameter:
```bash
curl "http://localhost:7071/api/HttpExample?name=AZ204"
```

Press `Ctrl+C` to stop the local runtime.

---

## Step 6 — Create a Timer-Triggered Function

```bash
func new --name TimerExample --template "Timer trigger"
```

Edit `TimerExample.cs` to run every 5 minutes:

```csharp
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace az204_functions
{
    public class TimerExample
    {
        private readonly ILogger _logger;

        public TimerExample(ILoggerFactory loggerFactory)
        {
            _logger = loggerFactory.CreateLogger<TimerExample>();
        }

        [Function("TimerExample")]
        public void Run(
            [TimerTrigger("0 */5 * * * *")] TimerInfo myTimer)
        {
            _logger.LogInformation($"Timer trigger executed at: {DateTime.Now}");

            if (myTimer.ScheduleStatus is not null)
            {
                _logger.LogInformation($"Next timer schedule at: {myTimer.ScheduleStatus.Next}");
            }
        }
    }
}
```

CRON expression `0 */5 * * * *` means: every 5 minutes.

Common CRON patterns:
| Expression | Meaning |
|-----------|---------|
| `0 */5 * * * *` | Every 5 minutes |
| `0 0 * * * *` | Every hour |
| `0 0 9 * * *` | Every day at 9:00 AM |
| `0 0 0 * * 1` | Every Monday at midnight |

---

## Step 7 — Create a Queue-Triggered Function with Blob Output Binding

```bash
func new --name QueueToBlob --template "Queue trigger"
```

Edit `QueueToBlob.cs`:

```csharp
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace az204_functions
{
    public class QueueToBlob
    {
        private readonly ILogger _logger;

        public QueueToBlob(ILoggerFactory loggerFactory)
        {
            _logger = loggerFactory.CreateLogger<QueueToBlob>();
        }

        [Function("QueueToBlob")]
        [BlobOutput("output-container/{id}.txt", Connection = "AzureWebJobsStorage")]
        public string Run(
            [QueueTrigger("input-queue", Connection = "AzureWebJobsStorage")] QueueMessage message)
        {
            _logger.LogInformation($"Queue trigger processed: {message.Body}");

            // The return value is written to the blob output binding
            return $"Processed message: {message.Body} at {DateTime.UtcNow}";
        }
    }

    public class QueueMessage
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string Body { get; set; } = string.Empty;
    }
}
```

This function:
1. Triggers when a message arrives in `input-queue`
2. Writes the processed result to a blob in `output-container`

---

## Step 8 — Deploy to Azure

```bash
func azure functionapp publish az204-func-<unique>
```

Verify deployment:
```bash
az functionapp function list \
  --name az204-func-<unique> \
  --resource-group az204-func-rg \
  --output table
```

Test the HTTP function in Azure:
```bash
curl https://az204-func-<unique>.azurewebsites.net/api/HttpExample
```

---

## Step 9 — Configure Application Settings in Azure

```bash
az functionapp config appsettings set \
  --name az204-func-<unique> \
  --resource-group az204-func-rg \
  --settings CUSTOM_SETTING=MyValue

# View all settings
az functionapp config appsettings list \
  --name az204-func-<unique> \
  --resource-group az204-func-rg \
  --output table
```

---

## Step 10 — Monitor Function Execution

```bash
# View recent function invocations
az monitor app-insights query \
  --app az204-func-<unique> \
  --resource-group az204-func-rg \
  --analytics-query "requests | take 10"
```

Or check the function logs:
```bash
az webapp log tail \
  --name az204-func-<unique> \
  --resource-group az204-func-rg
```

---

## Cleanup

```bash
az group delete --name az204-func-rg --yes --no-wait
```

Also clean up local files:
```bash
cd ..
rm -rf az204-functions
```

---

## Expected Outcomes

| Task | Status |
|------|--------|
| Created Function App on Consumption plan | ✅ |
| Created HTTP-triggered function | ✅ |
| Tested function locally | ✅ |
| Created Timer-triggered function | ✅ |
| Created Queue-triggered function with Blob output | ✅ |
| Deployed to Azure | ✅ |
| Configured app settings | ✅ |

---

Next: [Day 03 — Azure Storage](../day-03-storage/theory.md)
