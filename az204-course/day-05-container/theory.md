# Day 05 — Containers: ACI, ACR & Container Apps

## What You'll Learn Today

- Docker fundamentals for Azure developers
- Azure Container Registry (ACR)
- Azure Container Instances (ACI)
- Azure Container Apps
- When to use each container service

---
## 1. Docker Fundamentals

A container packages your application code, runtime, libraries, and dependencies into a single portable unit.

### Key Concepts

- **Image** — a read-only template with instructions for creating a container
- **Container** — a running instance of an image
- **Dockerfile** — a text file with instructions to build an image
- **Registry** — a repository for storing and distributing images

### Dockerfile Example

```dockerfile
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 8080

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY ["MyApp.csproj", "."]
RUN dotnet restore
COPY . .
RUN dotnet publish -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "MyApp.dll"]
```

This is a multi-stage build: the SDK image builds the app, the smaller runtime image runs it.

---

## 2. Azure Container Registry (ACR)

ACR is a managed Docker registry for storing and managing container images.

### SKUs

| SKU | Storage | Use Case |
|-----|---------|----------|
| **Basic** | 10 GiB | Dev/test |
| **Standard** | 100 GiB | Production |
| **Premium** | 500 GiB | Geo-replication, private link |

### Key Features
- **ACR Tasks** — build images in the cloud (no local Docker needed)
- **Geo-replication** — replicate registry to multiple regions (Premium)
- **Managed identity** — authenticate without passwords
- **Webhooks** — trigger actions on image push/delete

---

## 3. Azure Container Instances (ACI)

ACI is the fastest way to run a container in Azure. No VMs, no orchestration — just run a container.

### When to Use ACI

- Quick testing and prototyping
- Batch jobs and task automation
- CI/CD build agents
- Simple single-container workloads
- Sidecar containers (container groups)

### Limitations

- No auto-scaling
- No built-in load balancing
- No rolling deployments
- Limited orchestration

---

## 4. Azure Container Apps

Container Apps is a serverless container platform built on Kubernetes (but you don't manage Kubernetes).

### Key Features

- **Auto-scaling** — scale to zero, scale based on HTTP traffic, queue length, or custom metrics
- **Revisions** — deploy new versions with traffic splitting
- **Dapr integration** — built-in microservices building blocks
- **Ingress** — built-in HTTPS ingress with custom domains
- **Managed identity** — authenticate to other Azure services

### When to Use Container Apps

- Microservices
- APIs and web apps
- Event-driven processing
- Background workers
- Any workload that benefits from auto-scaling

### Architecture

```
┌─────────────────────────────────────────────┐
│        Container Apps Environment            │
│        (shared virtual network)              │
│                                              │
│  ┌──────────────┐  ┌──────────────────────┐ │
│  │ Container App│  │ Container App        │ │
│  │ (API)        │  │ (Worker)             │ │
│  │              │  │                      │ │
│  │ Rev 1: 80%  │  │ Scale: 0-10          │ │
│  │ Rev 2: 20%  │  │ Trigger: Queue depth │ │
│  │              │  │                      │ │
│  │ Ingress: ON │  │ Ingress: OFF         │ │
│  └──────────────┘  └──────────────────────┘ │
└─────────────────────────────────────────────┘
```

---

## 5. Comparison

| Feature | ACI | Container Apps | AKS |
|---------|-----|---------------|-----|
| Complexity | Low | Medium | High |
| Auto-scale | No | Yes (to zero) | Yes |
| Orchestration | No | Managed | Full Kubernetes |
| Ingress | Basic | Built-in HTTPS | Ingress controller |
| Cost model | Per second | Per second (scale to 0) | VM-based |
| Use case | Simple tasks | Microservices | Full control |

For AZ-204, focus on ACI and Container Apps. AKS is not on the exam.

---

## Key Takeaways

1. ACR stores your container images — use ACR Tasks to build in the cloud
2. ACI is the simplest way to run a container — no orchestration
3. Container Apps is serverless containers with auto-scaling and revisions
4. Container Apps can scale to zero — you only pay when running
5. Use multi-stage Dockerfiles to keep images small

---

Next: [Lab — Container Apps](./lab-container-apps.md)
