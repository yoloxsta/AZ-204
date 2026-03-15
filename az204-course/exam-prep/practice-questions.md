# AZ-204 Practice Questions

## Domain 1: Develop Azure Compute Solutions (25-30%)

### Question 1
You have an Azure App Service web app. You need to deploy a new version with zero downtime. What should you use?

A. Scale out the app to multiple instances  
B. Use deployment slots and perform a swap  
C. Stop the app, deploy, then start it  
D. Use a blue-green deployment with Azure Traffic Manager  

<details><summary>Answer</summary>B. Deployment slots allow you to deploy to a staging slot and swap to production with zero downtime. This is the built-in App Service feature for this scenario.</details>

---

### Question 2
You are creating an Azure Function that processes messages from a Service Bus queue. The function must process messages in order. What should you configure?

A. Set the function to use a Consumption plan  
B. Enable sessions on the Service Bus queue and use a session-enabled trigger  
C. Set the `maxConcurrentCalls` to 1  
D. Use a Timer trigger to poll the queue  

<details><summary>Answer</summary>B. Sessions in Service Bus guarantee FIFO ordering. The function trigger must be session-enabled to process messages in order within a session.</details>

---

### Question 3
You need to run a container in Azure for a short-lived batch job. The container should start, process data, and stop. Which service should you use?

A. Azure Kubernetes Service  
B. Azure Container Apps  
C. Azure Container Instances  
D. Azure App Service  

<details><summary>Answer</summary>C. ACI is designed for simple, short-lived container workloads. It starts quickly and you pay only for the execution time. No orchestration overhead.</details>

---

### Question 4
An Azure Function on the Consumption plan has a maximum default timeout of:

A. 1 minute  
B. 5 minutes  
C. 10 minutes  
D. 30 minutes  

<details><summary>Answer</summary>B. The default timeout for Consumption plan is 5 minutes. It can be increased to a maximum of 10 minutes via host.json.</details>

---

### Question 5
You need to deploy a new revision of an Azure Container App and route 20% of traffic to it. What feature do you use?

A. Deployment slots  
B. Traffic splitting with revisions  
C. Azure Traffic Manager  
D. Azure Front Door  

<details><summary>Answer</summary>B. Container Apps supports traffic splitting between revisions natively. You can set weight percentages for each revision.</details>

---

## Domain 2: Develop for Azure Storage (15-20%)

### Question 6
You need to store log files in Blob Storage. Files older than 30 days should move to Cool tier, and files older than 365 days should be deleted. What should you configure?

A. A scheduled Azure Function  
B. A lifecycle management policy  
C. An Event Grid subscription  
D. A Logic App with a timer trigger  

<details><summary>Answer</summary>B. Lifecycle management policies automate tier transitions and deletion based on age. This is the built-in, no-code solution.</details>

---

### Question 7
You need to grant a third-party application temporary read access to a specific blob. The access should expire in 1 hour. What is the most secure approach?

A. Share the storage account key  
B. Generate an account SAS token  
C. Generate a user delegation SAS token  
D. Make the blob container public  

<details><summary>Answer</summary>C. User delegation SAS is the most secure because it's signed with Entra ID credentials (not the account key). It provides the least privilege and is scoped to the specific blob.</details>

---

### Question 8
In Cosmos DB, you have a container with partition key `/userId`. Which query is most efficient?

A. `SELECT * FROM c WHERE c.category = 'Electronics'`  
B. `SELECT * FROM c WHERE c.userId = 'user1' AND c.category = 'Electronics'`  
C. `SELECT * FROM c ORDER BY c.price DESC`  
D. `SELECT COUNT(1) FROM c`  

<details><summary>Answer</summary>B. This query includes the partition key (`userId`), so it targets a single partition. All other queries require cross-partition fan-out.</details>

---

### Question 9
What is the default consistency level in Cosmos DB?

A. Strong  
B. Bounded Staleness  
C. Session  
D. Eventual  

<details><summary>Answer</summary>C. Session consistency is the default. It guarantees read-your-own-writes within a session, which is sufficient for most applications.</details>

---

### Question 10
A point read in Cosmos DB (reading a single 1KB item by ID and partition key) costs approximately:

A. 0.1 RU  
B. 1 RU  
C. 5 RUs  
D. 10 RUs  

<details><summary>Answer</summary>B. A point read of a 1KB item costs approximately 1 RU. This is the most efficient operation in Cosmos DB.</details>

---

## Domain 3: Implement Azure Security (20-25%)

### Question 11
You have an App Service that needs to access Key Vault secrets. What is the recommended approach?

A. Store the Key Vault connection string in app settings  
B. Use a managed identity and RBAC  
C. Store the Key Vault access key in environment variables  
D. Use a shared access signature  

<details><summary>Answer</summary>B. Managed identity with RBAC eliminates the need for any credentials in code or configuration. The identity is managed by Azure.</details>

---

### Question 12
Which `DefaultAzureCredential` source is used when running in Azure App Service with a system-assigned managed identity?

A. Environment credential  
B. Managed Identity credential  
C. Azure CLI credential  
D. Interactive browser credential  

<details><summary>Answer</summary>B. In Azure, DefaultAzureCredential detects the managed identity and uses it. Locally, it falls back to Azure CLI or other developer credentials.</details>

---

### Question 13
You need a daemon application (no user interaction) to call Microsoft Graph API. Which OAuth 2.0 flow should you use?

A. Authorization Code flow  
B. Device Code flow  
C. Client Credentials flow  
D. Implicit flow  

<details><summary>Answer</summary>C. Client Credentials flow is for service-to-service communication where no user is present. The app authenticates with its own identity.</details>

---

### Question 14
You store a secret in Key Vault and update it. What happens to the old value?

A. It is permanently deleted  
B. It is overwritten  
C. A new version is created; the old version is retained  
D. It is moved to a backup vault  

<details><summary>Answer</summary>C. Key Vault maintains versions of secrets. Updating creates a new version. Old versions remain accessible by their version ID.</details>

---

## Domain 4: Monitor, Troubleshoot, and Optimize (15-20%)

### Question 15
You want to find all HTTP requests that took longer than 2 seconds in the last hour. Which KQL query is correct?

A. `requests | where duration > 2000 and timestamp > ago(1h)`  
B. `traces | where message contains "slow" | take 10`  
C. `dependencies | where duration > 2 | project name, duration`  
D. `exceptions | where timestamp > ago(1h)`  

<details><summary>Answer</summary>A. The `requests` table contains HTTP request telemetry. Duration is in milliseconds, so 2000ms = 2 seconds. The `ago(1h)` function filters to the last hour.</details>

---

### Question 16
In the cache-aside pattern, what happens on a cache miss?

A. The cache automatically loads data from the database  
B. The application reads from the database and stores the result in the cache  
C. The request fails with a 404 error  
D. The cache returns stale data  

<details><summary>Answer</summary>B. In cache-aside, the application is responsible for loading data from the database on a cache miss and storing it in the cache for future requests.</details>

---

### Question 17
You need to create an alert that fires when the average response time exceeds 3 seconds for 5 consecutive minutes. What type of alert should you create?

A. Activity log alert  
B. Metric alert  
C. Log alert  
D. Smart detection alert  

<details><summary>Answer</summary>B. Metric alerts evaluate numeric metrics against thresholds over time windows. Response time is a metric that can be evaluated with a 5-minute window.</details>

---

## Domain 5: Connect to and Consume Azure Services (15-20%)

### Question 18
You need to react to blob creation events in a storage account. Which service should you use?

A. Azure Service Bus  
B. Azure Event Grid  
C. Azure Event Hubs  
D. Azure Queue Storage  

<details><summary>Answer</summary>B. Event Grid has built-in system topics for Azure Storage events (BlobCreated, BlobDeleted). It's designed for reactive event routing.</details>

---

### Question 19
You have a Service Bus topic with two subscriptions. Subscription A should receive all messages. Subscription B should only receive messages where the `priority` property equals `high`. What should you configure on Subscription B?

A. A correlation filter matching `priority = high`  
B. A SQL filter: `priority = 'high'`  
C. A boolean filter set to true  
D. No filter (subscriptions filter automatically)  

<details><summary>Answer</summary>B. A SQL filter with the expression `priority = 'high'` filters messages based on application properties. Correlation filters work too but SQL filters are more flexible.</details>

---

### Question 20
In Azure API Management, you need to limit API calls to 100 per minute per subscription. Where do you configure this?

A. In the backend service  
B. In an inbound policy using `<rate-limit>`  
C. In an outbound policy  
D. In the product settings  

<details><summary>Answer</summary>B. The `<rate-limit>` policy is applied in the inbound section. It limits the number of calls per subscription within a renewal period.</details>

---

### Question 21
What is the difference between Event Grid and Event Hubs?

A. Event Grid is for streaming; Event Hubs is for notifications  
B. Event Grid is for reactive events (push); Event Hubs is for streaming (pull)  
C. They are the same service with different names  
D. Event Grid supports ordering; Event Hubs does not  

<details><summary>Answer</summary>B. Event Grid pushes event notifications to subscribers (reactive). Event Hubs ingests high-throughput event streams that consumers pull from (streaming).</details>

---

### Question 22
A Service Bus message has been abandoned 3 times (max delivery count = 3). What happens next?

A. The message is deleted  
B. The message is sent to the dead-letter queue  
C. The message is retried indefinitely  
D. The message is returned to the sender  

<details><summary>Answer</summary>B. When a message exceeds the max delivery count, it's automatically moved to the dead-letter queue (DLQ) for investigation.</details>

---

## Scenario-Based Questions

### Question 23
You are building a web application that:
- Stores user data in Cosmos DB
- Caches frequently accessed data in Redis
- Sends notifications via Service Bus
- Stores files in Blob Storage
- Must not have any secrets in code

Which combination of services and patterns should you use?

<details><summary>Answer</summary>

1. **Managed Identity** on the App Service for authentication to all services
2. **DefaultAzureCredential** in code — works with managed identity in Azure and CLI locally
3. **Key Vault** to store any secrets that can't use managed identity (e.g., third-party API keys)
4. **RBAC roles** assigned to the managed identity:
   - Cosmos DB Built-in Data Contributor
   - Storage Blob Data Contributor
   - Azure Service Bus Data Sender
   - Key Vault Secrets User
5. **App Configuration** (optional) for centralized settings with Key Vault references

</details>

---

### Question 24
Your Cosmos DB container uses `/country` as the partition key. You notice that 80% of your data has `country = "US"`. What is the problem and how do you fix it?

<details><summary>Answer</summary>

**Problem:** Hot partition. The "US" partition receives disproportionate traffic and data, leading to throttling and uneven resource utilization.

**Fix:** Choose a partition key with higher cardinality and more even distribution, such as `/userId` or a synthetic key like `/country-userId`. This distributes data and requests more evenly across partitions.

</details>

---

### Question 25
You deploy a new version of your API and users report errors. You need to quickly roll back. You used deployment slots. What do you do?

<details><summary>Answer</summary>

Swap the slots again:
```bash
az webapp deployment slot swap \
  --name myapp \
  --resource-group myRG \
  --slot staging \
  --target-slot production
```

After the original swap, the old production code is now in the staging slot. Swapping again restores the previous version to production immediately.

</details>

---

## Tips for Exam Day

1. **Read questions carefully** — many questions have subtle differences in the options
2. **Eliminate wrong answers** — usually 2 options are clearly wrong
3. **Know the CLI commands** — `az webapp`, `az functionapp`, `az cosmosdb`, `az keyvault`
4. **Understand policies** — APIM policy XML structure (inbound/outbound/backend/on-error)
5. **Know consistency levels** — Strong > Bounded Staleness > Session > Consistent Prefix > Eventual
6. **Managed Identity is always preferred** over connection strings and keys
7. **Partition key choice** — high cardinality, even distribution, used in queries
8. **SAS types** — User delegation SAS is most secure
9. **Time management** — ~1.5 minutes per question, flag and return to difficult ones
10. **Case studies** — read the requirements carefully, they often have specific constraints
