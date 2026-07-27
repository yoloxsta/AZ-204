# Day 05a — Azure Kubernetes Service (AKS)

## What You'll Learn Today

- Kubernetes fundamentals for Azure developers
- AKS cluster architecture (control plane, node pools, system/user pods)
- Deployments, Services, and Ingress
- Scaling: HPA, VNA, Cluster Autoscaler
- Application scaling with KEDA
- AKS networking (Azure CNI, kubenet, overlay)
- Storage: Persistent Volumes, Azure Disk, Azure Files
- Helm charts for package management
- When to use AKS vs Container Apps vs ACI

---

## 1. Kubernetes Fundamentals

Kubernetes (K8s) is an open-source container orchestration platform. AKS is Azure's managed Kubernetes service — Azure manages the control plane, you manage the nodes.

### Core Objects

| Object | Purpose | AZ-204 Focus |
|--------|---------|--------------|
| **Pod** | Smallest deployable unit — one or more containers sharing network/storage | Understand it runs containers |
| **Deployment** | Manages ReplicaSets, rolling updates, rollbacks | Primary way to deploy apps |
| **ReplicaSet** | Ensures N copies of a pod are running | Managed by Deployments |
| **Service** | Stable network endpoint for a set of pods | Load balancing, service discovery |
| **Namespace** | Virtual cluster within a physical cluster | Resource isolation |
| **Ingress** | HTTP/HTTPS routing to Services | External access, path-based routing |
| **ConfigMap** | Key-value pairs for non-secret config | Inject as env vars or files |
| **Secret** | Sensitive data (passwords, keys) | Base64 encoded, use Key Vault |
| **PersistentVolume (PV)** | Cluster-level storage | Backed by Azure Disk/Files |
| **PersistentVolumeClaim (PVC)** | Request for storage by a pod | Pods claim PVs via PVCs |
| **StatefulSet** | Deployment for stateful apps (databases) | Stable network IDs, ordered deploy |
| **DaemonSet** | One pod per node | Logging, monitoring agents |
| **Job/CronJob** | Run-to-completion or scheduled tasks | Batch processing |

### Pods

A pod wraps one or more containers. Containers in a pod share:
- The same IP address
- The same hostname
- The same volumes
- The same network namespace

```yaml
apiVersion: v1
kind: Pod
metadata:
  name: my-app
  labels:
    app: my-app
spec:
  containers:
  - name: web
    image: nginx:1.25
    ports:
    - containerPort: 80
  - name: sidecar
    image: busybox
    command: ["sh", "-c", "while true; do echo sidecar; sleep 30; done"]
```

In practice, you rarely create pods directly — you use Deployments.

### Deployments

A Deployment manages the desired state of your application. You declare what you want (image, replicas) and Kubernetes makes it happen.

```yaml
apiVersion: apps/v1
kind: Deployment
metadata:
  name: webapp
spec:
  replicas: 3
  selector:
    matchLabels:
      app: webapp
  template:
    metadata:
      labels:
        app: webapp
    spec:
      containers:
      - name: webapp
        image: mcr.microsoft.com/dotnet/samples:aspnetapp
        ports:
        - containerPort: 8080
        resources:
          requests:
            memory: "128Mi"
            cpu: "250m"
          limits:
            memory: "256Mi"
            cpu: "500m"
```

Key Deployment features:
- **Rolling updates** — new pods are created before old ones are deleted
- **Rollback** — `kubectl rollout undo deployment/webapp`
- **Scaling** — `kubectl scale deployment/webapp --replicas=5`

### Services

Services provide a stable IP address and DNS name for a set of pods.

| Service Type | Description | Use Case |
|-------------|-------------|----------|
| **ClusterIP** (default) | Internal IP only | Pod-to-pod communication |
| **NodePort** | Exposes on each node's IP at a static port | Development/testing |
| **LoadBalancer** | Azure Load Balancer provisioned | External access |
| **ExternalName** | Maps to a CNAME record | External service alias |

```yaml
apiVersion: v1
kind: Service
metadata:
  name: webapp-service
spec:
  type: LoadBalancer
  selector:
    app: webapp
  ports:
  - port: 80
    targetPort: 8080
    protocol: TCP
```

### Namespaces

Namespaces isolate resources within a cluster. AKS creates default namespaces:

| Namespace | Purpose |
|-----------|---------|
| `default` | Default namespace for user workloads |
| `kube-system` | Kubernetes system components |
| `kube-public` | Publicly readable cluster info |
| `aks-node-info` | AKS node metadata |

```bash
# Create a namespace
kubectl create namespace dev

# Deploy to a specific namespace
kubectl apply -f deployment.yaml -n dev

# List pods in a namespace
kubectl get pods -n dev
```

---

## 2. AKS Architecture

AKS is a managed Kubernetes control plane. Azure runs and maintains the Kubernetes API server, etcd, scheduler, and controller manager. You manage the node pools where your pods run.

### Cluster Architecture

```
┌──────────────────────────────────────────────────────────────────┐
│                        AKS Cluster                               │
│                                                                  │
│  ┌────────────────────────────────────────────────────────────┐  │
│  │              Control Plane (Azure-managed)                  │  │
│  │                                                            │  │
│  │  ┌──────────┐  ┌──────────┐  ┌──────────┐  ┌──────────┐  │  │
│  │  │ API      │  │ etcd     │  │Scheduler │  │Controller│  │  │
│  │  │ Server   │  │ (store)  │  │          │  │ Manager  │  │  │
│  │  └──────────┘  └──────────┘  └──────────┘  └──────────┘  │  │
│  │                                                            │  │
│  │  Cost: FREE (managed by Azure)                             │  │
│  └────────────────────────────────────────────────────────────┘  │
│                                                                  │
│  ┌────────────────────────────────────────────────────────────┐  │
│  │              Data Plane (user-managed)                      │  │
│  │                                                            │  │
│  │  ┌──────────────────────┐  ┌──────────────────────┐       │  │
│  │  │ System Node Pool      │  │ User Node Pool        │       │  │
│  │  │ (Azure-managed pods)  │  │ (Your application)    │       │  │
│  │  │                       │  │                       │       │  │
│  │  │ • kube-system         │  │ • Your Deployment     │       │  │
│  │  │ • coredns             │  │ • Your Services       │       │  │
│  │  │ • kube-proxy          │  │ • Ingress controller  │       │  │
│  │  │ • metrics-server      │  │ • Background jobs     │       │  │
│  │  │ • tunnelfront         │  │                       │       │  │
│  │  └──────────────────────┘  └──────────────────────┘       │  │
│  └────────────────────────────────────────────────────────────┘  │
└──────────────────────────────────────────────────────────────────┘
```

### Node Pools

| Pool Type | Purpose | Default | Min Size |
|-----------|---------|---------|----------|
| **System** | Runs AKS system pods (CoreDNS, kube-proxy, etc.) | 1 node | 1 node |
| **User** | Runs your application workloads | 0 nodes | 0 nodes |

- **System node pool** — must exist, runs critical cluster services, not tainted for user workloads
- **User node pool** — optional, for your application pods, can have different VM sizes, can be autoscaled independently
- You can create multiple user node pools with different VM sizes (e.g., GPU nodes, memory-optimized nodes)

### AKS VM SKUs

| Category | Examples | Use Case |
|----------|----------|----------|
| General purpose | Standard_D2s_v3, Standard_D4s_v5 | Most workloads |
| Compute optimized | Standard_F2s_v2, Standard_F4s_v2 | CPU-intensive apps |
| Memory optimized | Standard_E2s_v3, Standard_E4s_v5 | Databases, caching |
| GPU | Standard_NC6, Standard_ND96asr_v4 | ML/AI workloads |
| Burstable | Standard_B2s, Standard_B2ms | Dev/test, low traffic |

### Availability Zones

Deploy AKS nodes across availability zones for high availability:

```bash
az aks create \
  --name my-aks \
  --resource-group my-rg \
  --node-count 3 \
  --zones 1 2 3
```

| Zone Feature | Benefit |
|-------------|---------|
| Multi-zone node pools | Pods spread across zones |
| Zone-redundant storage | PVs survive zone failure |
| Zone-aware load balancing | Traffic distributed across zones |

---

## 3. AKS Networking

### Network Models

| Model | Description | Pod IPs | When to Use |
|-------|-------------|---------|-------------|
| **Azure CNI** | Pods get IPs from Azure VNet subnet | VNet IPs | When pods need VNet integration |
| **Azure CNI Overlay** | Pods get IPs from a private overlay CIDR | Overlay IPs | More pod IPs, simpler networking |
| **kubenet** | Pods get IPs from a logical network | Private IPs only | Fewer VNet IPs, simpler setup |

#### Azure CNI

```
┌─────────────────────────────────────────────────┐
│                  Azure VNet                      │
│                  10.0.0.0/16                     │
│                                                  │
│  ┌───────────────────────────────────────────┐  │
│  │           Subnet: aks-subnet              │  │
│  │           10.0.1.0/24                     │  │
│  │                                           │  │
│  │  ┌─────────┐ ┌─────────┐ ┌─────────┐    │  │
│  │  │ Node 1  │ │ Node 2  │ │ Node 3  │    │  │
│  │  │ .1.4    │ │ .1.5    │ │ .1.6    │    │  │
│  │  └─────────┘ └─────────┘ └─────────┘    │  │
│  │                                           │  │
│  │  Pod IPs also come from this subnet       │  │
│  │  e.g., .1.10, .1.11, .1.12...            │  │
│  └───────────────────────────────────────────┘  │
│                                                  │
│  ┌──────────────┐  ┌──────────────────────────┐ │
│  │ Load Balancer│  │ Other VNet resources      │ │
│  └──────────────┘  └──────────────────────────┘ │
└─────────────────────────────────────────────────┘
```

**Pros:** Pods get routable VNet IPs, direct VNet integration
**Cons:** Each pod consumes a VNet IP (fewer pods per subnet)

#### kubenet

```
┌─────────────────────────────────────────────────┐
│                  Azure VNet                      │
│                  10.0.0.0/16                     │
│                                                  │
│  ┌───────────────────────────────────────────┐  │
│  │           Subnet: aks-subnet              │  │
│  │           10.0.1.0/24                     │  │
│  │                                           │  │
│  │  ┌─────────┐ ┌─────────┐ ┌─────────┐    │  │
│  │  │ Node 1  │ │ Node 2  │ │ Node 3  │    │  │
│  │  │ .1.4    │ │ .1.5    │ │ .1.6    │    │  │
│  │  └─────────┘ └─────────┘ └─────────┘    │  │
│  │                                           │  │
│  │  Pod IPs are private (10.244.x.x)         │  │
│  │  Only node IPs are in VNet                │  │
│  └───────────────────────────────────────────┘  │
└─────────────────────────────────────────────────┘
```

**Pros:** More pods per subnet (VNet IPs only for nodes)
**Cons:** Pods not directly routable from VNet

### Load Balancer

AKS provisions an Azure Load Balancer when you create a Service of type LoadBalancer:

```yaml
apiVersion: v1
kind: Service
metadata:
  name: webapp
spec:
  type: LoadBalancer
  selector:
    app: webapp
  ports:
  - port: 80
    targetPort: 8080
```

### Ingress

An Ingress routes HTTP/HTTPS traffic to Services based on hostnames and paths:

```yaml
apiVersion: networking.k8s.io/v1
kind: Ingress
metadata:
  name: webapp-ingress
  annotations:
    nginx.ingress.kubernetes.io/ssl-redirect: "true"
spec:
  ingressClassName: nginx
  tls:
  - hosts:
    - webapp.example.com
    secretName: webapp-tls
  rules:
  - host: webapp.example.com
    http:
      paths:
      - path: /api
        pathType: Prefix
        backend:
          service:
            name: api-service
            port:
              number: 80
      - path: /
        pathType: Prefix
        backend:
          service:
            name: web-frontend
            port:
              number: 80
```

### Network Policies

Network Policies control traffic flow between pods:

```yaml
apiVersion: networking.k8s.io/v1
kind: NetworkPolicy
metadata:
  name: allow-only-frontend
  namespace: default
spec:
  podSelector:
    matchLabels:
      app: api
  policyTypes:
  - Ingress
  ingress:
  - from:
    - podSelector:
        matchLabels:
          app: frontend
    ports:
    - port: 8080
```

| Policy Type | Effect |
|-------------|--------|
| **Ingress** | Controls incoming traffic to pods |
| **Egress** | Controls outgoing traffic from pods |
| **Both** | Controls both directions |

---

## 4. AKS Storage

### Storage Model

```
┌──────────────┐     ┌──────────────────┐     ┌──────────────────┐
│   Pod         │────►│ PersistentVolume  │────►│ Azure Storage    │
│   (claim)     │     │ Claim (PVC)       │     │ (Disk or Files)  │
└──────────────┘     └──────────────────┘     └──────────────────┘
       ▲                      ▲
       │                      │
  kubernetes resource    maps to PV
```

### PersistentVolumeClaim

```yaml
apiVersion: v1
kind: PersistentVolumeClaim
metadata:
  name: my-pvc
spec:
  accessModes:
  - ReadWriteOnce
  storageClassName: managed-csi
  resources:
    requests:
      storage: 10Gi
```

### StorageClasses

| StorageClass | Backing Store | Access Mode | Use Case |
|-------------|---------------|-------------|----------|
| `managed-csi` | Azure Disk (SSD) | ReadWriteOnce | Databases, single-node apps |
| `managed-csi-premium` | Azure Disk (Premium SSD) | ReadWriteOnce | High-performance databases |
| `managed-csi-zone-redundant` | Zone-redundant Azure Disk | ReadWriteOnce | HA across zones |
| `azurefile-csi` | Azure Files (SMB) | ReadWriteMany | Shared storage, multiple pods |
| `azurefile-csi-premium` | Azure Files (Premium) | ReadWriteMany | High-performance shared storage |

### Azure Disk vs Azure Files

| Feature | Azure Disk | Azure Files |
|---------|-----------|-------------|
| Access mode | ReadWriteOnce (one node) | ReadWriteMany (multiple nodes) |
| Protocol | Block storage | File storage (SMB/NFS) |
| Performance | Higher (IOPS) | Lower (throughput) |
| Use case | Database, single pod | Shared files, content |
| Snapshot | Yes | Yes |
| Max size | 64 TB | 100 TiB |

### Mounting Storage in a Pod

```yaml
apiVersion: apps/v1
kind: Deployment
metadata:
  name: my-app
spec:
  replicas: 1
  selector:
    matchLabels:
      app: my-app
  template:
    metadata:
      labels:
        app: my-app
    spec:
      containers:
      - name: app
        image: nginx
        volumeMounts:
        - name: my-volume
          mountPath: /data
      volumes:
      - name: my-volume
        persistentVolumeClaim:
          claimName: my-pvc
```

### CSI (Container Storage Interface) Drivers

AKS uses CSI drivers to interface with Azure storage:

| CSI Driver | Purpose |
|------------|---------|
| `disk.csi.azure.com` | Azure Disk |
| `file.csi.azure.com` | Azure Files |
| `blob.csi.azure.com` | Azure Blob Storage |

---

## 5. Scaling

### HorizontalPodAutoscaler (HPA)

HPA scales the number of pod replicas based on CPU, memory, or custom metrics.

```yaml
apiVersion: autoscaling/v2
kind: HorizontalPodAutoscaler
metadata:
  name: my-app-hpa
spec:
  scaleTargetRef:
    apiVersion: apps/v1
    kind: Deployment
    name: my-app
  minReplicas: 2
  maxReplicas: 10
  metrics:
  - type: Resource
    resource:
      name: cpu
      target:
        type: Utilization
        averageUtilization: 70
  - type: Resource
    resource:
      name: memory
      target:
        type: Utilization
        averageUtilization: 80
```

```bash
# Create HPA using kubectl
kubectl autoscale deployment my-app \
  --cpu-percent=70 \
  --min=2 \
  --max=10

# Check HPA status
kubectl get hpa
```

### Cluster Autoscaler

The Cluster Autoscaler adds or removes nodes based on pod scheduling needs:

```bash
az aks update \
  --name my-aks \
  --resource-group my-rg \
  --enable-cluster-autoscaler \
  --min-count 1 \
  --max-count 5
```

| Setting | Description |
|---------|-------------|
| `--min-count` | Minimum number of nodes |
| `--max-count` | Maximum number of nodes |
| `--scale-down-delay-after-add` | Time to wait before scaling down (default 10m) |
| `--scale-down-unneeded` | Time a node is unneeded before removal (default 10m) |

### KEDA (Kubernetes Event-Driven Autoscaling)

KEDA scales based on external events like queue length, not just CPU/memory:

```yaml
apiVersion: keda.sh/v1alpha1
kind: ScaledObject
metadata:
  name: queue-consumer
spec:
  scaleTargetRef:
    name: queue-consumer
  minReplicaCount: 0
  maxReplicaCount: 20
  triggers:
  - type: azure-servicebus
    metadata:
      queueName: orders
      messageCount: "5"
      namespace: my-namespace
```

| KEDA Scaler | Metric |
|-------------|--------|
| `azure-servicebus` | Queue/topic message count |
| `azure-queue` | Azure Queue Storage message count |
| `azure-blob` | Blob count in container |
| `azure-monitor` | Azure Monitor metric |
| `http` | HTTP request rate |
| `cron` | Schedule-based scaling |

### Scaling Comparison

| Mechanism | Scales | Trigger | Use Case |
|-----------|--------|---------|----------|
| **HPA** | Pod count | CPU, memory, custom | Web apps, APIs |
| **Cluster Autoscaler** | Node count | Pending pods | Variable workload |
| **KEDA** | Pod count | Events, queues, schedules | Event-driven workloads |
| **VNA (Vertical Pod Autoscaler)** | Pod resources | CPU/memory request | Right-sizing pods |

---

## 6. AKS Security

### Azure AD Integration

```bash
# Create AKS cluster with Azure AD integration
az aks create \
  --name my-aks \
  --resource-group my-rg \
  --enable-aad \
  --aad-admin-group-object-ids <group-id> \
  --aad-tenant-id <tenant-id>
```

| Role | Permissions |
|------|------------|
| **Azure Kubernetes Service Cluster Admin** | Full cluster admin |
| **Azure Kubernetes Service RBAC Reader** | Read-only access |
| **Azure Kubernetes Service RBAC Writer** | Create/update resources |
| **Azure Kubernetes Service RBAC Admin** | Full namespace admin |

### Pod Security

AKS provides pod security through:
- **Pod Security Standards** (Restricted, Baseline, Privileged)
- **Network Policies** (restrict pod-to-pod traffic)
- **Container image scanning** (with Microsoft Defender for Containers)

```yaml
# Example: Pod with security context
apiVersion: v1
kind: Pod
metadata:
  name: secure-pod
spec:
  securityContext:
    runAsNonRoot: true
    runAsUser: 1000
    fsGroup: 2000
  containers:
  - name: app
    image: nginx
    securityContext:
      allowPrivilegeEscalation: false
      readOnlyRootFilesystem: true
      capabilities:
        drop:
          - ALL
```

### Key Vault CSI Driver

Mount secrets from Azure Key Vault directly into pods:

```bash
# Enable Key Vault CSI driver on AKS
az aks enable-addons \
  --name my-aks \
  --resource-group my-rg \
  --addons azure-keyvault-secrets-provider
```

```yaml
apiVersion: secrets-store.csi.x.k8s.io/v1
kind: SecretProviderClass
metadata:
  name: azure-kv
spec:
  provider: azure
  parameters:
    usePodIdentity: "true"
    keyvaultName: my-keyvault
    objects: |
      array:
        - |
          objectName: db-password
          objectType: secret
    tenantId: <tenant-id>
```

### Managed Identities

| Identity Type | Scope | Use Case |
|--------------|-------|----------|
| **System-assigned** | One identity per cluster | Simple scenarios |
| **User-assigned** | Reusable across resources | Multiple AKS clusters, shared identity |

---

## 7. Helm

Helm is a package manager for Kubernetes. A Helm chart is a collection of YAML files that describe a set of Kubernetes resources.

### Key Concepts

| Concept | Description |
|---------|-------------|
| **Chart** | A package of Kubernetes resource templates |
| **Repository** | A collection of charts (e.g., Bitnami) |
| **Release** | A specific deployment of a chart with a name |
| **Values** | Configuration parameters for a chart |

### Helm Commands

```bash
# Add a repository
helm repo add bitnami https://charts.bitnami.com/bitnami
helm repo update

# Search for charts
helm search repo nginx

# Install a chart
helm install my-release bitnami/nginx \
  --set service.type=LoadBalancer \
  --set replicaCount=3

# List installed releases
helm list

# Upgrade a release
helm upgrade my-release bitnami/nginx \
  --set replicaCount=5

# Rollback a release
helm rollback my-release 1

# Uninstall a release
helm uninstall my-release
```

### Custom Helm Chart Structure

```
my-chart/
  Chart.yaml          # Chart metadata
  values.yaml         # Default configuration values
  templates/
    deployment.yaml   # Kubernetes Deployment template
    service.yaml      # Kubernetes Service template
    ingress.yaml      # Kubernetes Ingress template
    _helpers.tpl      # Template helpers
  charts/             # Dependencies
```

### values.yaml Example

```yaml
replicaCount: 3

image:
  repository: myregistry.azurecr.io/myapp
  tag: "1.0"
  pullPolicy: IfNotPresent

service:
  type: LoadBalancer
  port: 80

ingress:
  enabled: true
  className: nginx
  hosts:
    - host: myapp.example.com
      paths:
        - path: /
          pathType: Prefix
```

---

## 8. AKS vs Container Apps vs ACI

| Feature | AKS | Container Apps | ACI |
|---------|-----|---------------|-----|
| **Complexity** | High | Medium | Low |
| **Orchestration** | Full Kubernetes | Managed (K8s underneath) | None |
| **Scaling** | HPA, KEDA, Cluster Autoscaler | Auto-scale (to zero) | Manual |
| **Networking** | Azure CNI, kubenet | Managed VNet integration | Isolated |
| **Ingress** | Ingress controller (manual) | Built-in HTTPS | Basic (DNS label) |
| **State** | StatefulSets, PVs | Stateless preferred | Stateless |
| **Cost** | VM-based (always running) | Per-second (scale to 0) | Per-second |
| **When to use** | Full K8s control needed | Serverless microservices | Simple container runs |
| **Learning curve** | Steep | Moderate | Low |
| **Vendor lock-in** | Low (standard K8s) | Medium (Azure-specific) | Medium (Azure-specific) |

### Decision Matrix

| Requirement | Recommended Service |
|-------------|-------------------|
| Quick test of a container | ACI |
| Microservices with auto-scaling | Container Apps |
| Full Kubernetes control | AKS |
| Event-driven scaling | Container Apps or AKS + KEDA |
| Stateful workloads (databases) | AKS |
| Scale to zero | Container Apps |
| Existing Kubernetes expertise | AKS |
| CI/CD pipeline agent | ACI |

---

## 9. Architecture Diagram

```
┌────────────────────────────────────────────────────────────────────────┐
│                         Azure Cloud                                    │
│                                                                        │
│  ┌──────────────────────────────────────────────────────────────────┐  │
│  │                     Azure Load Balancer                           │  │
│  │                    (Standard SKU, public IP)                      │  │
│  └─────────────────────────────┬────────────────────────────────────┘  │
│                                │                                       │
│  ┌─────────────────────────────▼────────────────────────────────────┐  │
│  │                        AKS Cluster                                │  │
│  │                                                                   │  │
│  │  ┌──────────────────────────────────────────────────────────┐    │  │
│  │  │                  Ingress Controller                       │    │  │
│  │  │              (nginx-ingress or Traefik)                   │    │  │
│  │  └─────────────────────────┬────────────────────────────────┘    │  │
│  │                            │                                      │  │
│  │  ┌─────────────────────────▼────────────────────────────────┐    │  │
│  │  │                  Kubernetes Services                      │    │  │
│  │  │                                                           │    │  │
│  │  │  ┌──────────────┐  ┌──────────────┐  ┌──────────────┐  │    │  │
│  │  │  │ web-service  │  │ api-service  │  │ worker-svc   │  │    │  │
│  │  │  │ (ClusterIP)  │  │ (ClusterIP)  │  │ (ClusterIP)  │  │    │  │
│  │  │  └──────┬───────┘  └──────┬───────┘  └──────┬───────┘  │    │  │
│  │  └─────────┼─────────────────┼─────────────────┼───────────┘    │  │
│  │            │                 │                 │                  │  │
│  │  ┌─────────▼─────────────────▼─────────────────▼───────────┐    │  │
│  │  │                     Pods                                  │    │  │
│  │  │                                                          │    │  │
│  │  │  ┌────────────┐ ┌────────────┐ ┌────────────────────┐  │    │  │
│  │  │  │ web (x3)   │ │ api (x5)   │ │ worker (x2)        │  │    │  │
│  │  │  │ nginx      │ │ dotnet     │ │ processor          │  │    │  │
│  │  │  │ port: 80   │ │ port: 8080 │ │ port: 9090         │  │    │  │
│  │  │  └────────────┘ └────────────┘ └────────────────────┘  │    │  │
│  │  └──────────────────────────────────────────────────────────┘    │  │
│  │                                                                   │  │
│  │  ┌──────────────────────────────────────────────────────────┐    │  │
│  │  │                   Scaling                                 │    │  │
│  │  │  HPA (web, api)  │  KEDA (worker)  │  Cluster Autoscaler│    │  │
│  │  └──────────────────────────────────────────────────────────┘    │  │
│  │                                                                   │  │
│  │  ┌──────────────────────────────────────────────────────────┐    │  │
│  │  │                   Storage                                 │    │  │
│  │  │  PVC ──► Azure Disk (api data)                            │    │  │
│  │  │  PVC ──► Azure Files (shared config)                      │    │  │
│  │  └──────────────────────────────────────────────────────────┘    │  │
│  └──────────────────────────────────────────────────────────────────┘  │
│                                                                        │
│  ┌──────────────────────────────────────────────────────────────────┐  │
│  │                     Supporting Services                           │  │
│  │                                                                   │  │
│  │  ┌──────────┐  ┌──────────┐  ┌──────────┐  ┌──────────────┐   │  │
│  │  │   ACR    │  │ Key Vault│  │ Azure AD │  │ Log Analytics│   │  │
│  │  │ (images) │  │(secrets) │  │  (auth)  │  │  (monitoring)│   │  │
│  │  └──────────┘  └──────────┘  └──────────┘  └──────────────┘   │  │
│  └──────────────────────────────────────────────────────────────────┘  │
└────────────────────────────────────────────────────────────────────────┘
```

---

## 10. Key Concepts for AZ-204

### Exam Tips

1. **AKS is managed K8s** — Azure handles the control plane; you manage node pools
2. **Free control plane** — you only pay for the worker nodes (VMs)
3. **Azure CNI vs kubenet** — Azure CNI gives pods routable VNet IPs; kubenet uses overlay
4. **HPA vs Cluster Autoscaler** — HPA scales pods; Cluster Autoscaler scales nodes
5. **KEDA** — for event-driven scaling (queue length, not CPU)
6. **Azure Disk** — single-node access (ReadWriteOnce); **Azure Files** — multi-node access (ReadWriteMany)
7. **Helm** — package manager; a chart is a set of templates + values
8. **AKS vs Container Apps** — use Container Apps for serverless microservices; use AKS for full Kubernetes control
9. **AKS vs ACI** — ACI for simple container runs; AKS for orchestrated workloads
10. **Network Policies** — control pod-to-pod traffic (required for exam scenarios)

### Quick Reference Commands

```bash
# Create AKS cluster
az aks create --name my-aks --resource-group my-rg --node-count 3 --generate-ssh-keys

# Connect to cluster
az aks get-credentials --name my-aks --resource-group my-rg

# Check nodes
kubectl get nodes

# Check pods
kubectl get pods --all-namespaces

# View cluster info
kubectl cluster-info

# Enable monitoring
az aks enable-addons --name my-aks --resource-group my-rg --addons monitoring

# Scale node pool
az aks nodepool scale --name mypool --cluster-name my-aks --resource-group my-rg --node-count 5

# Add a user node pool
az aks nodepool add --name userpool --cluster-name my-aks --resource-group my-rg --node-count 2 --mode User
```

---

## Key Takeaways

1. AKS is a managed Kubernetes service — Azure manages the control plane, you manage the workloads
2. Use Deployments for stateless apps, StatefulSets for stateful workloads
3. Services provide stable networking; Ingress handles HTTP routing
4. HPA scales pods; Cluster Autoscaler scales nodes; KEDA scales on events
5. Azure Disk for single-node storage, Azure Files for shared storage
6. Helm simplifies Kubernetes application deployment and management
7. Container Apps is simpler but less flexible than AKS — know when to use each

---

Next: [Lab — Deploy and Scale on AKS](./lab-aks.md)
