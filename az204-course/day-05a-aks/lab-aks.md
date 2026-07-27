# Lab: Azure Kubernetes Service (AKS)

## Title
Deploy and Scale a Microservices Application on AKS

## Objective
- Create an AKS cluster with Azure CLI
- Connect to the cluster with kubectl
- Deploy a sample microservices application using YAML
- Create Services (LoadBalancer) and Ingress
- Set up HorizontalPodAutoscaler
- Enable Cluster Autoscaler
- Install a Helm chart
- Scale and verify
- Clean up resources

## Architecture

```
┌──────────────────────────────────────────────────────────────────┐
│                     Resource Group: az204-day05a-rg               │
│                                                                  │
│  ┌────────────────────────────────────────────────────────────┐  │
│  │                     AKS Cluster                            │  │
│  │                                                             │  │
│  │  ┌──────────────────────────────────────────────────┐     │  │
│  │  │              System Node Pool                      │     │  │
│  │  │  (kube-system, coredns, kube-proxy)                │     │  │
│  │  └──────────────────────────────────────────────────┘     │  │
│  │                                                             │  │
│  │  ┌──────────────────────────────────────────────────┐     │  │
│  │  │              User Node Pool                       │     │  │
│  │  │                                                    │     │  │
│  │  │  ┌────────────┐  ┌────────────┐  ┌────────────┐ │     │  │
│  │  │  │ frontend   │  │ backend    │  │ nginx-ing  │ │     │  │
│  │  │  │ (x2 pods)  │  │ (x2 pods)  │  │ (x1 pod)   │ │     │  │
│  │  │  └────────────┘  └────────────┘  └────────────┘ │     │  │
│  │  └──────────────────────────────────────────────────┘     │  │
│  │                                                             │  │
│  │  ┌──────────────────────────────────────────────────┐     │  │
│  │  │              Services                              │     │  │
│  │  │                                                    │     │  │
│  │  │  frontend-svc ──► LoadBalancer ──► Public IP       │     │  │
│  │  │  backend-svc  ──► ClusterIP (internal)             │     │  │
│  │  │                                                    │     │  │
│  │  │  Ingress ──► nginx-ingress ──► frontend/backend    │     │  │
│  │  └──────────────────────────────────────────────────┘     │  │
│  │                                                             │  │
│  │  ┌──────────────────────────────────────────────────┐     │  │
│  │  │              Autoscaling                           │     │  │
│  │  │  HPA (frontend: CPU 70%)                           │     │  │
│  │  │  HPA (backend: CPU 70%)                            │     │  │
│  │  │  Cluster Autoscaler: 1-5 nodes                     │     │  │
│  │  └──────────────────────────────────────────────────┘     │  │
│  └────────────────────────────────────────────────────────────┘  │
│                                                                  │
│  ┌──────────────────────┐  ┌──────────────────────────────────┐ │
│  │      ACR              │  │        Azure Monitor              │ │
│  │  az204acr<unique>     │  │  (Log Analytics workspace)       │ │
│  └──────────────────────┘  └──────────────────────────────────┘ │
└──────────────────────────────────────────────────────────────────┘
```

## Prerequisites
- Azure CLI installed and logged in (`az login`)
- kubectl installed
- Helm 3 installed

---

## Step 1 — Create Resource Group and AKS Cluster

Create the resource group:
```bash
az group create \
  --name az204-day05a-rg \
  --location eastus
```

Create the AKS cluster with system and user node pools:
```bash
az aks create \
  --name az204-aks \
  --resource-group az204-day05a-rg \
  --node-count 2 \
  --node-vm-size Standard_D2s_v3 \
  --network-plugin azure \
  --enable-managed-identity \
  --generate-ssh-keys \
  --tags course=az204 day=05a
```

Verify the cluster:
```bash
az aks show \
  --name az204-aks \
  --resource-group az204-day05a-rg \
  --query "{name:name, status:powerState, fqdn:apiServerAddress, nodeCount:agentPoolProfiles[0].count}" \
  --output table
```

---

## Step 2 — Connect to the Cluster

Download the kubeconfig and set the context:
```bash
az aks get-credentials \
  --name az204-aks \
  --resource-group az204-day05a-rg \
  --overwrite-existing
```

Verify connection:
```bash
kubectl cluster-info
kubectl get nodes
```

You should see 2 nodes in Ready state:
```
NAME                       STATUS   ROLES   AGE   VERSION
aks-nodepool1-xxxxx-0      Ready    agent   1m    v1.28.x
aks-nodepool1-xxxxx-1      Ready    agent   1m    v1.28.x
```

---

## Step 3 — Deploy a Sample Application (Deployment YAML)

Create a directory for your manifests:
```bash
mkdir k8s-manifests
cd k8s-manifests
```

Create `frontend-deployment.yaml`:
```yaml
apiVersion: apps/v1
kind: Deployment
metadata:
  name: frontend
  labels:
    app: frontend
spec:
  replicas: 2
  selector:
    matchLabels:
      app: frontend
  template:
    metadata:
      labels:
        app: frontend
    spec:
      containers:
      - name: frontend
        image: nginx:1.25
        ports:
        - containerPort: 80
        resources:
          requests:
            memory: "64Mi"
            cpu: "100m"
          limits:
            memory: "128Mi"
            cpu: "250m"
```

Create `backend-deployment.yaml`:
```yaml
apiVersion: apps/v1
kind: Deployment
metadata:
  name: backend
  labels:
    app: backend
spec:
  replicas: 2
  selector:
    matchLabels:
      app: backend
  template:
    metadata:
      labels:
        app: backend
    spec:
      containers:
      - name: backend
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

Apply both:
```bash
kubectl apply -f frontend-deployment.yaml
kubectl apply -f backend-deployment.yaml
```

Verify pods are running:
```bash
kubectl get pods -l app=frontend
kubectl get pods -l app=backend
```

---

## Step 4 — Create Services

Create `services.yaml`:
```yaml
apiVersion: v1
kind: Service
metadata:
  name: frontend-svc
spec:
  type: LoadBalancer
  selector:
    app: frontend
  ports:
  - port: 80
    targetPort: 80
    protocol: TCP
---
apiVersion: v1
kind: Service
metadata:
  name: backend-svc
spec:
  type: ClusterIP
  selector:
    app: backend
  ports:
  - port: 8080
    targetPort: 8080
    protocol: TCP
```

Apply and verify:
```bash
kubectl apply -f services.yaml
kubectl get svc frontend-svc backend-svc
```

Wait for the frontend service to get an external IP:
```bash
kubectl get svc frontend-svc --watch
```

Once EXTERNAL-IP changes from `<pending>` to an IP, test:
```bash
curl http://<EXTERNAL-IP>
```

You should see the nginx welcome page.

---

## Step 5 — Create an Ingress with nginx-ingress Controller

Install the nginx ingress controller using Helm:
```bash
helm repo add ingress-nginx https://kubernetes.github.io/ingress-nginx
helm repo update

helm install nginx-ingress ingress-nginx/ingress-nginx \
  --set controller.replicaCount=1 \
  --namespace ingress-nginx --create-namespace
```

Wait for the ingress controller to be ready:
```bash
kubectl get pods -n ingress-nginx
kubectl get svc -n ingress-nginx
```

Create `ingress.yaml`:
```yaml
apiVersion: networking.k8s.io/v1
kind: Ingress
metadata:
  name: webapp-ingress
  annotations:
    nginx.ingress.kubernetes.io/rewrite-target: /
spec:
  ingressClassName: nginx
  rules:
  - http:
      paths:
      - path: /
        pathType: Prefix
        backend:
          service:
            name: frontend-svc
            port:
              number: 80
      - path: /api
        pathType: Prefix
        backend:
          service:
            name: backend-svc
            port:
              number: 8080
```

Apply and get the ingress IP:
```bash
kubectl apply -f ingress.yaml
kubectl get ingress webapp-ingress
```

Test via the ingress controller's external IP:
```bash
INGRESS_IP=$(kubectl get svc nginx-ingress-ingress-nginx-controller -n ingress-nginx -o jsonpath='{.status.loadBalancer.ingress[0].ip}')
curl http://${INGRESS_IP}/
curl http://${INGRESS_IP}/api
```

---

## Step 6 — Set up HorizontalPodAutoscaler

Create HPA for the frontend:
```bash
kubectl autoscale deployment frontend \
  --cpu-percent=70 \
  --min=2 \
  --max=8
```

Create HPA for the backend:
```bash
kubectl autoscale deployment backend \
  --cpu-percent=70 \
  --min=2 \
  --max=8
```

Verify HPAs:
```bash
kubectl get hpa
```

Output:
```
NAME       REFERENCE         TARGETS        MINPODS   MAXPODS   REPLICAS
frontend   Deployment/frontend  3%/70%      2         8         2
backend    Deployment/backend   5%/70%      2         8         2
```

To test HPA scaling, generate load:
```bash
# Install a simple load generator
kubectl run load-generator --image=busybox --restart=Never -- \
  sh -c "while true; do wget -qO- http://frontend-svc:80; done"

# Watch HPA in another terminal
kubectl get hpa frontend --watch
```

After a few minutes, you should see REPLICAS increase above 2.

Stop the load generator:
```bash
kubectl delete pod load-generator
```

---

## Step 7 — Enable Cluster Autoscaler

Update the AKS cluster to enable Cluster Autoscaler:
```bash
az aks update \
  --name az204-aks \
  --resource-group az204-day05a-rg \
  --enable-cluster-autoscaler \
  --min-count 1 \
  --max-count 5
```

Verify autoscaler is enabled:
```bash
az aks show \
  --name az204-aks \
  --resource-group az204-day05a-rg \
  --query "agentPoolProfiles[0].{autoScalerProfile:autoscalerProfile, min:count, max:maxCount}" \
  --output table
```

The Cluster Autoscaler will now automatically add nodes when pods cannot be scheduled and remove idle nodes.

---

## Step 8 — Install a Helm Chart (Bitnami nginx)

```bash
helm repo add bitnami https://charts.bitnami.com/bitnami
helm repo update

helm install my-nginx bitnami/nginx \
  --set service.type=LoadBalancer \
  --set replicaCount=2
```

Verify the Helm release:
```bash
helm list
kubectl get pods -l app.kubernetes.io/name=nginx
kubectl get svc my-nginx-nginx
```

Check the nginx service external IP:
```bash
kubectl get svc my-nginx-nginx --watch
curl http://<EXTERNAL-IP>
```

Upgrade the release:
```bash
helm upgrade my-nginx bitnami/nginx \
  --set service.type=LoadBalancer \
  --set replicaCount=4
```

Verify the upgrade:
```bash
kubectl get pods -l app.kubernetes.io/name=nginx
```

You should now see 4 nginx pods.

---

## Step 9 — Scale and Verify

Scale the frontend deployment manually:
```bash
kubectl scale deployment frontend --replicas=6
kubectl get deployment frontend
kubectl get pods -l app=frontend
```

Check all resources:
```bash
# All deployments
kubectl get deployments

# All services
kubectl get svc

# All ingresses
kubectl get ingress

# All HPAs
kubectl get hpa

# All pods across namespaces
kubectl get pods --all-namespaces
```

View logs from a frontend pod:
```bash
POD_NAME=$(kubectl get pods -l app=frontend -o jsonpath='{.items[0].metadata.name}')
kubectl logs ${POD_NAME}
```

---

## Step 10 — Clean Up Resources

Delete Kubernetes resources:
```bash
kubectl delete -f frontend-deployment.yaml
kubectl delete -f backend-deployment.yaml
kubectl delete -f services.yaml
kubectl delete -f ingress.yaml
kubectl delete hpa frontend backend
helm uninstall my-nginx
helm uninstall nginx-ingress -n ingress-nginx
kubectl delete namespace ingress-nginx
```

Delete the AKS cluster and resource group:
```bash
az aks delete \
  --name az204-aks \
  --resource-group az204-day05a-rg \
  --yes --no-wait

az group delete \
  --name az204-day05a-rg \
  --yes --no-wait
```

---

## Expected Outcomes

| Task | Status |
|------|--------|
| Created AKS cluster with 2 nodes | ✅ |
| Connected to cluster with kubectl | ✅ |
| Deployed frontend and backend pods | ✅ |
| Created LoadBalancer and ClusterIP services | ✅ |
| Installed nginx ingress controller | ✅ |
| Created Ingress for path-based routing | ✅ |
| Set up HPA for CPU-based scaling | ✅ |
| Enabled Cluster Autoscaler (1-5 nodes) | ✅ |
| Installed bitnami/nginx via Helm | ✅ |
| Scaled deployment to 6 replicas | ✅ |
| Viewed pod logs | ✅ |
| Cleaned up all resources | ✅ |

---

Next: [Day 06 — Identity & Authentication](../day-06-identity/theory.md)
