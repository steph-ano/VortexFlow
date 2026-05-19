# Changelog - VortexFlow MVP

Todas las cambios notables de este proyecto se documentarán en este archivo.

El formato está basado en [Keep a Changelog](https://keepachangelog.com/es-ES/1.0.0/),
y este proyecto adhiere a [Semantic Versioning](https://semver.org/lang/es/).

## [1.0.0-beta] - 2026-05-18

### Agregado

#### Fase 0 - Infraestructura Base
- Docker Compose con PostgreSQL + TimescaleDB, Redis, RabbitMQ
- Configuración de red y volúmenes
- healthchecks para todos los servicios

#### Fase 1 - Backend .NET
- API REST con ASP.NET Core 8
- Entity Framework Core con migrations
- Autenticación JWT
- Endpoints para Campaigns, Posts, Trends, Users
- SignalR Hubs para tiempo real

#### Fase 2 - Frontend Vue 3
- Dashboard con gráficos ECharts
- Calendario editorial con FullCalendar
- UI con Glassmorphism y Tailwind CSS 4
- Gestión de estado con Pinia
- Rutas protegidas con Vue Router

#### Fase 3 - Background Jobs & Tiempo Real
- Hangfire para jobs programados
- SignalR para WebSockets en tiempo real
- MassTransit con RabbitMQ para mensajería
- Notificaciones en vivo

#### Fase 4 - Resiliencia
- Circuit Breakers con Polly
- Rate Limiting
- Retry policies
- Fallback handlers

#### Fase 5 - DevOps & Observabilidad
- GitHub Actions CI/CD completo
- Dockerfiles optimizados para .NET y Python
- OpenTelemetry en API y Worker
- Prometheus métricas personalizadas
- Grafana dashboards con 4 paneles
- Loki para logs
- Tempo para trazas
- k8s manifests para producción

#### Fase 6 - Validación & Lanzamiento
- Pruebas E2E con Cypress
- Checklist de regresión manual
- Guía de usuario completa
- docker-compose.beta.yml para testing

### Dependencias

#### Backend .NET
- .NET 8.0 SDK
- Entity Framework Core 8.0
- Swashbuckle para API docs
- Serilog para logging
- OpenTelemetry SDK

#### Backend Python
- Python 3.11
- FastAPI
- Celery + Redis
- OpenTelemetry
- Prometheus client
- httpx, tenacity, structlog

#### Frontend
- Vue 3.5+
- Vite 8
- TypeScript
- Tailwind CSS 4
- ECharts + vue-echarts
- FullCalendar
- Pinia
- Vue Router 4

### Imágenes GHCR

El pipeline publica automáticamente imágenes a GitHub Container Registry:
- `ghcr.io/steph-ano/vortexflow/vortexflow-api:latest`
- `ghcr.io/steph-ano/vortexflow/vortexflow-worker:latest`
- `ghcr.io/steph-ano/vortexflow/vortexflow-frontend:latest`

---

## [0.0.1-alpha] - 2025-?? (Pre-alpha)

Versiones iniciales de desarrollo (no documentadas).

---

## Cómo Contribuir

1. Haz fork del repositorio
2. Crea una rama (`git checkout -b feature/amazing`)
3. Commit tus cambios (`git commit -m 'Add amazing feature'`)
4. Push a la rama (`git push origin feature/amazing`)
5. Abre un Pull Request

## Autores

- Equipo de desarrollo VortexFlow

## Licencia

Este proyecto está bajo desarrollo propietario.