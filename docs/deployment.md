# Despliegue de VortexFlow

## 1. Despliegue Local con Docker Compose

### Prerrequisitos
- Docker & Docker Compose v2
- Git

### Infraestructura base (PostgreSQL, Redis, RabbitMQ)
```bash
docker compose up -d
```

### Aplicaciones
```bash
# Construir y levantar todos los servicios
docker compose --profile app up -d
```

### Verificar
```bash
curl http://localhost:5032/health
curl http://localhost:8000/health
```

Acceder al frontend en http://localhost:5173.

---

## 2. Observabilidad (local)

Levantar Prometheus + Grafana + Loki + Tempo + OpenTelemetry Collector:

```bash
# Crear la red compartida (si no existe)
docker network create vortexflow-network

# Levantar stack de observabilidad
docker compose -f docker-compose.observability.yml up -d
```

### Servicios

| Servicio          | Puerto | URL                        |
|-------------------|--------|----------------------------|
| Grafana           | 3000   | http://localhost:3000      |
| Prometheus        | 9090   | http://localhost:9090      |
| Loki              | 3100   | http://localhost:3100      |
| Tempo             | 3200   | http://localhost:3200      |
| OTLP Collector    | 4317   | grpc://localhost:4317      |

Credenciales Grafana: `admin` / `admin`

### Dashboard
Una vez logueado en Grafana, el dashboard **VortexFlow** se carga automáticamente desde el provisioning. Contiene:

1. **API Latency** — Percentiles p50/p95/p99 de latencia de endpoints
2. **Tendencias por minuto** — Tasa de tendencias ingeridas
3. **Publicaciones exitosas/fallidas** — Conteo acumulado
4. **Circuit Breakers** — Estado de los circuit breakers en Redis (Closed/Open/Half-Open)

---

## 3. Despliegue en Kubernetes

### Prerrequisitos
- Cluster Kubernetes (kind, minikube, o cloud)
- kubectl configurado
- Ingress Controller (nginx-ingress)

### Pasos

```bash
# 1. Crear namespace y recursos
kubectl apply -f k8s/namespace.yaml

# 2. ConfigMap y Secrets
kubectl apply -f k8s/configmaps.yaml

# Editar secrets.yaml con valores reales antes de aplicar
kubectl apply -f k8s/secrets.yaml

# 3. Infraestructura (PostgreSQL, Redis, RabbitMQ)
kubectl apply -f k8s/infrastructure.yaml

# 4. Servicios de aplicación
kubectl apply -f k8s/services.yaml

# 5. Ingress
kubectl apply -f k8s/ingress.yaml
```

### Verificar
```bash
kubectl get all -n vortexflow
kubectl get ingress -n vortexflow
```

### Notas
- Las imágenes se publican en `ghcr.io` automáticamente en cada push a `main` (ver CI/CD).
- Para producción, reemplazar los placeholders en `secrets.yaml` (o usar External Secrets Operator).
- El Ingress asume un controlador nginx-ingress. Ajustar según el proveedor cloud.

---

## 4. CI/CD

El pipeline de GitHub Actions ejecuta:

1. **Build & Test** — .NET, Python, y frontend (en paralelo)
2. **Docker Build & Push** — Solo en push a `main`:
   - Construye imágenes para API, Worker y Frontend
   - Publica en `ghcr.io` con tags `latest` y `sha-XXXXXX`
   - Escanea con **Trivy** (falla si hay vulnerabilidades CRITICAL o HIGH)
   - Valida manifiestos Kubernetes con `kubectl apply --dry-run`
