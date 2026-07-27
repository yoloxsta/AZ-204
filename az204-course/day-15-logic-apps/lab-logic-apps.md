# Lab: Azure Logic Apps

## Title
Build Automated Workflows with Azure Logic Apps

## Objective
- Create a Logic App with a recurrence trigger
- Process messages from Service Bus
- Call Azure Functions from Logic Apps
- Implement error handling with scopes
- Monitor Logic App runs

## Architecture

```
┌──────────────────────────────────────────────────────────────────────┐
│                     Resource Group: az204-day15-rg                    │
│                                                                      │
│  ┌─────────────────┐                                                │
│  │  Service Bus     │                                                │
│  │  Namespace       │                                                │
│  │  ┌────────────┐ │   Peek-Lock     ┌──────────────────────────┐  │
│  │  │ orders     │─┼────────────────►│    Logic App              │  │
│  │  │ queue      │ │                 │                           │  │
│  │  └────────────┘ │                 │  ┌─────────┐  ┌────────┐ │  │
│  └─────────────────┘                 │  │Recurrence│─►│ Get    │ │  │
│                                      │  │(every 5m)│  │Message │ │  │
│  ┌─────────────────┐                 │  └─────────┘  └───┬────┘ │  │
│  │  Azure SQL       │                 │                    │      │  │
│  │  Database        │◄────────────────│  ┌─────────┐  ┌───▼────┐ │  │
│  │  ┌────────────┐ │  INSERT         │  │ Write   │◄─│ Parse  │ │  │
│  │  │ Orders     │ │                 │  │ to SQL  │  │ JSON   │ │  │
│  │  │ table      │ │                 │  └────┬────┘  └────────┘ │  │
│  │  └────────────┘ │                 │       │                   │  │
│  └─────────────────┘                 │  ┌────▼────┐  ┌────────┐ │  │
│                                      │  │  Try    │  │ Call   │ │  │
│  ┌─────────────────┐                 │  │  Scope  │◄─│ Func   │ │  │
│  │  Azure Function  │                │  └────┬────┘  └────────┘ │  │
│  │  ┌────────────┐ │   HTTP          │       │                   │  │
│  │  │Transform   │◄┼─────────────────│  ┌────▼────┐             │  │
│  │  │Order       │ │                 │  │  Catch  │──► Office365│  │
│  │  └────────────┘ │                 │  │  Scope  │    Email    │  │
│  └─────────────────┘                 │  └─────────┘             │  │
│                                      └──────────────────────────┘  │
│  ┌─────────────────┐                 ┌──────────────────────────┐  │
│  │  Log Analytics   │◄────────────────│  Diagnostics             │  │
│  │  Workspace      │                 │  Settings                │  │
│  └─────────────────┘                 └──────────────────────────┘  │
└──────────────────────────────────────────────────────────────────────┘
```

## Prerequisites
- Azure CLI installed and logged in
- Azure Functions Core Tools v4 installed
- Access to create resources in your Azure subscription

---

## Step 1 — Create Resource Group and Service Bus Namespace

```bash
az group create --name az204-day15-rg --location eastus

# Create Service Bus namespace
az servicebus namespace create \
  --name az204-sb-<unique> \
  --resource-group az204-day15-rg \
  --location eastus \
  --sku Standard

# Create a queue
az servicebus queue create \
  --name orders \
  --namespace-name az204-sb-<unique> \
  --resource-group az204-day15-rg \
  --max-size 1024 \
  --default-message-time-to-live P14D

# Get the connection string
az servicebus namespace authorization-rule keys list \
  --name RootManageSharedAccessKey \
  --namespace-name az204-sb-<unique> \
  --resource-group az204-day15-rg \
  --query primaryConnectionString \
  --output tsv
```

Save the connection string — you will need it in later steps.

---

## Step 2 — Create an Azure SQL Database

```bash
# Create SQL Server
az sql server create \
  --name az204-sql-<unique> \
  --resource-group az204-day15-rg \
  --location eastus \
  --admin-user sqladmin \
  --admin-password 'P@ssw0rd1234!'

# Allow Azure services to access the server
az sql server firewall-rule create \
  --name AllowAzureServices \
  --server-name az204-sql-<unique> \
  --resource-group az204-day15-rg \
  --start-ip-address 0.0.0.0 \
  --end-ip-address 0.0.0.0

# Create database
az sql db create \
  --name OrdersDb \
  --server-name az204-sql-<unique> \
  --resource-group az204-day15-rg \
  --edition GeneralPurpose \
  --family Gen5 \
  --capacity 2 \
  --compute-model Serverless \
  --auto-pause-delay 60 \
  --min-capacity 0.5 \
  --max-size 2GB
```

Connect to the database and create the Orders table:

```sql
CREATE TABLE Orders (
    Id INT IDENTITY(1,1) PRIMARY KEY,
    OrderId NVARCHAR(50) NOT NULL,
    Product NVARCHAR(200) NOT NULL,
    Quantity INT NOT NULL,
    Total DECIMAL(18,2) NOT NULL,
    CustomerEmail NVARCHAR(200),
    CreatedAt DATETIME2 DEFAULT GETUTCDATE(),
    Status NVARCHAR(50) DEFAULT 'Processing'
);
```

---

## Step 3 — Create an Azure Function (Transform Service)

This function receives an order and returns a transformed version with calculated tax:

```bash
mkdir az204-transform-func
cd az204-transform-func
func init --worker-runtime dotnet-isolated --target-framework net8.0
```

Edit `Program.cs`:

```csharp
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace az204_transform_func;

public class TransformOrder
{
    private readonly ILogger _logger;

    public TransformOrder(ILoggerFactory loggerFactory)
    {
        _logger = loggerFactory.CreateLogger<TransformOrder>();
    }

    [Function("TransformOrder")]
    public async Task<HttpResponseData> Run(
        [HttpTrigger(AuthorizationLevel.Function, "post")] HttpRequestData req)
    {
        _logger.LogInformation("TransformOrder function processing request.");

        var requestBody = await new StreamReader(req.Body).ReadToEndAsync();
        var order = JsonSerializer.Deserialize<Order>(requestBody);

        var transformed = new TransformedOrder
        {
            OrderId = order.OrderId,
            Product = order.Product.ToUpper(),
            Quantity = order.Quantity,
            UnitPrice = order.Total / order.Quantity,
            Tax = order.Total * 0.08m,
            TotalWithTax = order.Total * 1.08m,
            ProcessedAt = DateTime.UtcNow
        };

        var response = req.CreateResponse(System.Net.HttpStatusCode.OK);
        await response.WriteStringAsync(JsonSerializer.Serialize(transformed));
        return response;
    }
}

public class Order
{
    public string OrderId { get; set; }
    public string Product { get; set; }
    public int Quantity { get; set; }
    public decimal Total { get; set; }
}

public class TransformedOrder
{
    public string OrderId { get; set; }
    public string Product { get; set; }
    public int Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal Tax { get; set; }
    public decimal TotalWithTax { get; set; }
    public DateTime ProcessedAt { get; set; }
}
```

Deploy the function:

```bash
az functionapp create \
  --name az204-transform-func-<unique> \
  --resource-group az204-day15-rg \
  --storage-account az204funcstore<unique> \
  --consumption-plan-location eastus \
  --runtime dotnet-isolated \
  --runtime-version 8 \
  --functions-version 4 \
  --os-type Linux

func azure functionapp publish az204-transform-func-<unique>
```

Get the function URL and key:

```bash
az functionapp function show \
  --name az204-transform-func-<unique> \
  --resource-group az204-day15-rg \
  --function-name TransformOrder \
  --query invokeUrlTemplate \
  --output tsv

az functionapp keys list \
  --name az204-transform-func-<unique> \
  --resource-group az204-day15-rg \
  --query functionKeys.default \
  --output tsv
```

---

## Step 4 — Create the Logic App

Create a Consumption Logic App via the Azure portal or CLI:

```bash
# Create a Consumption Logic App
az logic workflow create \
  --name az204-logic-app \
  --resource-group az204-day15-rg \
  --location eastus \
  --definition '{
    "$schema": "https://schema.management.azure.com/providers/Microsoft.Logic/workflows/2016-06-01/workflowdefinition.json#",
    "contentVersion": "1.0.0.0",
    "triggers": {
      "Recurrence": {
        "type": "Recurrence",
        "recurrence": {
          "frequency": "Minute",
          "interval": 5,
          "startTime": "2026-07-28T08:00:00Z"
        }
      }
    },
    "actions": {},
    "outputs": {}
  }'
```

> **Tip:** It is usually easier to build the workflow in the Azure Portal visual designer first, then export the definition for code review. The following steps describe what to configure in the designer.

---

## Step 5 — Add Service Bus Connector (Peek-Lock Message)

In the Logic App designer, add an action after the Recurrence trigger:

1. Click **+ New step**
2. Search for **Service Bus**
3. Select the **Peek-Lock message** action
4. Configure:
   - **Connection** — create a new connection using the Service Bus connection string
   - **Namespace** — `az204-sb-<unique>`
   - **Queue** — `orders`
   - **Maximum messages to peek** — 1

The action should look like:

```
Recurrence (every 5 min)
    │
    ▼
Peek-Lock message (orders queue)
```

---

## Step 6 — Parse JSON Message and Transform Data

### Initialize Variables

Add a **Compose** action to build the message body:

```json
{
  "OrderId": "ORD-001",
  "Product": "Laptop Pro",
  "Quantity": 2,
  "Total": 2599.98,
  "CustomerEmail": "customer@example.com"
}
```

### Parse JSON

Add a **Parse JSON** action:

1. **Content** — use `body('Peek-lock_message')?['body']`
2. **Schema:**

```json
{
  "type": "object",
  "properties": {
    "OrderId": { "type": "string" },
    "Product": { "type": "string" },
    "Quantity": { "type": "integer" },
    "Total": { "type": "number" },
    "CustomerEmail": { "type": "string" }
  }
}
```

### Call Azure Function

Add an **HTTP** action to call the TransformOrder function:

- **Method** — POST
- **URI** — the function URL from Step 3
- **Headers** — `x-functions-key: <your-function-key>`
- **Body** — use the parsed JSON output:

```json
{
  "OrderId": "@{body('Parse_JSON')?['OrderId']}",
  "Product": "@{body('Parse_JSON')?['Product']}",
  "Quantity": @{body('Parse_JSON')?['Quantity']},
  "Total": @{body('Parse_JSON')?['Total']}
}
```

---

## Step 7 — Implement Error Handling (Scope + Try-Catch)

### Wrap Steps 5-6 in a Try Scope

1. Add a **Scope** action named `Try`
2. Move the Service Bus peek-lock, Parse JSON, and HTTP actions inside this scope

### Add a Catch Scope

1. Add another **Scope** action named `Catch`
2. Configure **Run After** — set it to run when the `Try` scope **has failed**

Inside the Catch scope:

**Compose — Error Details:**
```
Try Scope Failed: @{result('Try')?['error']?['message']}
Timestamp: @{utcNow()}
```

**Send Email (Office 365 Outlook connector):**
- **To:** `admin@yourdomain.com`
- **Subject:** `Logic App Failure — @{utcNow()}`
- **Body:** Use the Compose output from above

### Add a Finally Scope

1. Add a **Scope** action named `Finally`
2. Configure **Run After** — set it to run when the `Catch` scope completes (success, failure, or skipped)

Inside the Finally scope:

**Complete Service Bus Message:**
- Action: **Complete message**
- Use the lock token from the Peek-Lock action: `@{body('Peek-lock_message')?['lockToken']}`

---

## Step 8 — Add Write to SQL Database

Inside the Try scope, after the HTTP action, add:

1. **Initialize variable** — `TransformedResult` (type: String)
2. Set its value to the HTTP response body: `@{body('HTTP')}`
3. **Parse JSON** — parse the `TransformedResult` variable
4. **SQL Server — Insert Row:**
   - **Connection:** create new connection to `az204-sql-<unique>.database.windows.net`
   - **Database:** `OrdersDb`
   - **Table:** `dbo.Orders`
   - **OrderId:** `@{body('Parse_JSON_2')?['OrderId']}`
   - **Product:** `@{body('Parse_JSON_2')?['Product']}`
   - **Quantity:** `@{body('Parse_JSON_2')?['Quantity']}`
   - **Total:** `@{body('Parse_JSON_2')?['TotalWithTax']}`
   - **CustomerEmail:** `@{body('Parse_JSON')?['CustomerEmail']}`
   - **Status:** `Processed`

---

## Step 9 — Send a Test Message to Service Bus

```bash
az servicebus queue message send \
  --namespace-name az204-sb-<unique> \
  --queue-name orders \
  --resource-group az204-day15-rg \
  --body '{
    "OrderId": "ORD-001",
    "Product": "Laptop Pro",
    "Quantity": 2,
    "Total": 2599.98,
    "CustomerEmail": "customer@example.com"
  }'
```

Send another message:

```bash
az servicebus queue message send \
  --namespace-name az204-sb-<unique> \
  --queue-name orders \
  --resource-group az204-day15-rg \
  --body '{
    "OrderId": "ORD-002",
    "Product": "Wireless Mouse",
    "Quantity": 5,
    "Total": 149.95,
    "CustomerEmail": "buyer@example.com"
  }'
```

---

## Step 10 — Test the Workflow End-to-End

1. **Enable** the Logic App in the portal (or it runs automatically on the next recurrence)
2. Wait for the next scheduled run (up to 5 minutes)
3. Check the **Run History** in the Logic App blade
4. Verify:
   - Message was received from Service Bus
   - JSON was parsed successfully
   - Azure Function was called and returned transformed data
   - Row was inserted into the SQL database
5. Query the database to confirm:

```sql
SELECT * FROM Orders ORDER BY CreatedAt DESC;
```

Expected output:

| Id | OrderId | Product | Quantity | Total | CustomerEmail | CreatedAt | Status |
|----|---------|---------|----------|-------|---------------|-----------|--------|
| 1 | ORD-001 | LAPTOP PRO | 2 | 2807.98 | customer@example.com | 2026-07-28 08:00:xx | Processed |
| 2 | ORD-002 | WIRELESS MOUSE | 5 | 161.95 | buyer@example.com | 2026-07-28 08:00:xx | Processed |

---

## Step 11 — View Run History and Diagnostics

### Run History

1. Go to the Logic App in the portal
2. Click **Runs history** in the left menu
3. Click a specific run to see:
   - Trigger input/output
   - Each action's input, output, and status
   - Duration and timestamps
   - Error details (if any)

### Enable Diagnostics

1. Go to the Logic App → **Diagnostic settings** → **Add diagnostic setting**
2. Select:
   - **Send to Log Analytics** → choose your workspace
   - Categories: `WorkflowExecutionHistory`, `IntegrationAccountAssemblyProcessing`
3. Save

### Query with KQL

After logs have been ingested (may take 5-15 minutes):

```kusto
// View all run statuses
AzureDiagnostics
| where ResourceProvider == "MICROSOFT.LOGICAPPS"
| where Resource == "az204-logic-app"
| where ActionType_s == "WorkflowTriggered"
| project TimeGenerated, Status_s, RunId_g
| order by TimeGenerated desc

// Find failed runs
AzureDiagnostics
| where ResourceProvider == "MICROSOFT.LOGICAPPS"
| where Resource == "az204-logic-app"
| where Status_s == "Failed"
| project TimeGenerated, RunId_g, Error_s

// Action duration breakdown
AzureDiagnostics
| where ResourceProvider == "MICROSOFT.LOGICAPPS"
| where Resource == "az204-logic-app"
| where ActionType_s == "ActionCompleted"
| summarize AvgDuration = avg(DurationMs_d) by ActionName_s
| order by AvgDuration desc
```

---

## Cleanup

```bash
cd .. && rm -rf az204-transform-func

az group delete --name az204-day15-rg --yes --no-wait
```

---

## Expected Outcomes

| Task | Status |
|------|--------|
| Created Service Bus namespace and orders queue | ✅ |
| Created Azure SQL Database with Orders table | ✅ |
| Created Azure Function for order transformation | ✅ |
| Created Logic App with Recurrence trigger | ✅ |
| Configured Service Bus peek-lock message action | ✅ |
| Parsed JSON and called Azure Function | ✅ |
| Implemented try-catch-finally error handling with scopes | ✅ |
| Sent email notification on failure | ✅ |
| Wrote transformed order to SQL Database | ✅ |
| Sent test messages and verified end-to-end flow | ✅ |
| Viewed run history and enabled diagnostics | ✅ |

---

Next: [Day 16 — Integration Patterns](../day-16-integration-patterns/theory.md)
