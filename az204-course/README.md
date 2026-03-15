# AZ-204: Developing Solutions for Microsoft Azure — Complete Learning Path

## Course Overview

This is a 14-day hands-on learning path designed to prepare you for the **Microsoft AZ-204** certification exam. Each day covers a specific exam domain with theory, architecture explanations, step-by-step labs, and practice exercises.

The course structure follows the official Microsoft Learn modules for AZ-204.

## AZ-204 Exam Domains & Weight

| Domain | Weight | Days |
|--------|--------|------|
| Develop Azure compute solutions | 25-30% | Day 02, 05 |
| Develop for Azure storage | 15-20% | Day 03, 04 |
| Implement Azure security | 20-25% | Day 06, 12 |
| Monitor, troubleshoot, and optimize Azure solutions | 15-20% | Day 09, 11 |
| Connect to and consume Azure services and third-party services | 15-20% | Day 07, 08, 10 |

## Course Content — Mapped to Official AZ-204 Modules

### Day 01 — Introduction to Azure for Developers
- Azure fundamentals, resource groups, regions, ARM, Azure CLI
- Lab: Create your first resources via CLI

### Day 02 — Azure Compute: App Service & Functions

#### Exploring Azure App Service
- Examine Azure App Service
- Examine App Service plans
- Deploy to App Service
- Explore authentication and authorization in App Service

#### Configuring Web App Settings (7 Topics)
- Configure application settings
- Configure general settings
- Configure path mappings
- Enable diagnostic logging
- Configure certificates
- Manage app features (feature flags)
- Configure connection strings

#### Scaling Apps in Azure App Service (6 Topics)
- Examine autoscale factors
- Identify autoscale factors
- Enable autoscale in App Service
- Explore autoscale best practices
- Scale up vs scale out
- Configure autoscale rules and conditions

#### Exploring App Service Deployment Slots (6 Topics)
- Explore staging environments
- Examine slot swapping
- Swap deployment slots
- Route traffic in App Service
- Configure slot-sticky (deployment slot) settings
- Roll back and test in production

#### Exploring Azure Functions
- Discover Azure Functions
- Compare Azure Functions hosting options
- Scale Azure Functions
- Explore Azure Functions development

#### Developing Azure Functions (5 Topics)
- Explore Azure Functions development
- Create triggers and bindings
- Connect functions to Azure services
- Durable Functions patterns
- Durable Functions types and features

- Labs: Deploy a web app, create a Function App

### Day 03 — Azure Storage: Blobs & SDKs

#### Exploring Azure Blob Storage
- Azure Storage account overview
- Explore Azure Blob Storage
- Discover Azure Blob Storage resource types
- Explore Azure Storage security features

#### Managing Azure Blob Storage Lifecycle
- Explore the Azure Blob Storage lifecycle
- Discover Blob Storage lifecycle policies
- Implement Blob Storage lifecycle policies
- Rehydrate blob data from the archive tier

#### Working With Azure Blob Storage
- Explore Azure Blob Storage client library
- Create a client object
- Manage container properties and metadata by using .NET
- Set and retrieve properties and metadata for blob resources by using REST
- Interact with data using the Azure SDK

- Labs: Upload/download blobs, use the .NET SDK

### Day 04 — Azure Cosmos DB

#### Exploring Azure Cosmos DB
- Identify key benefits of Azure Cosmos DB
- Explore the resource hierarchy
- Explore consistency levels
- Choose the right consistency level
- Explore supported APIs

#### Working With Azure Cosmos DB
- Explore Microsoft .NET SDK v3 for Azure Cosmos DB
- Create stored procedures
- Create triggers and user-defined functions
- Explore change feed in Azure Cosmos DB
- Implement change feed patterns

- Lab: Create a Cosmos DB account, query data with SDK

### Day 05 — Containers: ACI, ACR & Container Apps

#### Managing Container Images in Azure Container Registry
- Discover the Azure Container Registry
- Explore storage capabilities
- Build and manage containers with tasks
- Explore elements of a Dockerfile

#### Running Container Images in Azure Container Instances
- Explore Azure Container Instances
- Run containerized tasks with restart policies
- Set environment variables in container instances
- Mount an Azure file share in Azure Container Instances

#### Implementing Azure Container Apps
- Explore Azure Container Apps
- Explore containers in Azure Container Apps
- Implement authentication and authorization
- Manage revisions and secrets
- Explore Dapr integration with Azure Container Apps

- Lab: Deploy a containerized app

### Day 06 — Identity & Authentication

#### Exploring the Microsoft Identity Platform
- Explore the Microsoft identity platform
- Explore service principals
- Discover permissions and consent
- Discover conditional access

#### Implementing Authentication Using MSAL
- Explore the Microsoft Authentication Library
- Initialize client applications
- Interactive, integrated Windows, username/password flows
- Acquire tokens silently and with device code flow

#### Implementing Shared Access Signatures
- Discover shared access signatures
- Choose when to use shared access signatures
- Explore stored access policies
- Implement SAS tokens in code

#### Exploring Microsoft Graph
- Discover Microsoft Graph
- Query Microsoft Graph by using REST and SDKs
- Apply best practices to Microsoft Graph

#### Implementing Managed Identities
- Explore managed identities
- Discover the managed identities authentication flow
- Configure managed identities
- Acquire an access token

- Lab: Configure managed identity, authenticate an app

### Day 07 — Event-Based Solutions

#### Exploring Azure Event Grid
- Explore Azure Event Grid
- Discover event schemas
- Explore event delivery durability
- Control access to events
- Receive events by using webhooks
- Filter events

#### Exploring Azure Event Hubs
- Discover Azure Event Hubs
- Explore Event Hubs Capture
- Scale your processing application
- Control access to events
- Perform common operations with the Event Hubs client library

- Lab: Create Event Grid subscriptions, react to blob events

### Day 08 — Message-Based Solutions

#### Discovering Azure Message Queues
- Choose a message queue solution
- Explore Azure Service Bus
- Discover Service Bus queues, topics, and subscriptions
- Explore Service Bus message payloads and serialization
- Send and receive messages from a Service Bus queue by using .NET
- Explore Azure Queue Storage
- Create and manage Azure Queue Storage and messages by using .NET

- Lab: Send and receive messages with Service Bus

### Day 09 — Caching & Content Delivery

#### Developing Azure Cache for Redis
- Explore Azure Cache for Redis
- Configure Azure Cache for Redis
- Interact with Azure Cache for Redis by using .NET

#### Developing for Storage on CDNs
- Explore Azure Content Delivery Networks
- Control cache behavior on Azure Content Delivery Networks
- Interact with Azure Content Delivery Networks by using .NET

- Lab: Implement Redis caching in an application

### Day 10 — API Management

#### Exploring API Management
- Discover the API Management service
- Explore API gateways
- Explore API Management policies
- Create advanced policies
- Secure APIs by using subscriptions
- Secure APIs by using certificates

- Lab: Create and configure an API Management instance

### Day 11 — Monitoring & Logging

#### Monitoring App Performance
- Explore Azure Monitor
- Explore Application Insights
- Discover log-based metrics
- Instrument an app for monitoring
- Select an availability test
- Troubleshoot app performance by using Application Map

- Lab: Instrument an app with Application Insights

### Day 12 — Security: Key Vault & App Configuration

#### Implementing Azure Key Vault
- Explore Azure Key Vault
- Discover Azure Key Vault best practices
- Authenticate to Azure Key Vault
- Exercise: Set and retrieve a secret from Azure Key Vault by using .NET

#### Implementing Azure App Configuration
- Explore Azure App Configuration
- Create paired keys and values
- Manage application features (feature flags)
- Secure app configuration data

- Lab: Store and retrieve secrets from Key Vault

### Day 13 — CI/CD & Infrastructure as Code
- GitHub Actions, Azure DevOps, Bicep, ARM templates
- Lab: Build a CI/CD pipeline with GitHub Actions

### Day 14 — Final Project & Exam Prep
- Build a complete cloud-native application using all learned services
- Practice questions and architecture review

## Prerequisites

- An active Azure subscription (free tier works)
- Azure CLI installed locally
- Visual Studio Code with Azure extensions
- Basic knowledge of C# or Python
- Docker Desktop (for Day 05)

## How to Use This Course

1. Follow the days in order — each builds on the previous
2. Read the theory first, then do the labs
3. Complete the exercises before moving on
4. Use the exam-prep folder for final review

Good luck on your AZ-204 journey!
