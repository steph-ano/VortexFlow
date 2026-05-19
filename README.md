# VortexFlow

VortexFlow (anteriormente conocido como NexusTrend) es una plataforma avanzada de analítica de tendencias y automatización de contenido corporativo. Está diseñada para capturar, procesar y presentar insights de mercado en tiempo real mediante una arquitectura de microservicios robusta y escalable.

## Tecnologías Principales

- **Cerebro Transaccional**: C# .NET 8, ASP.NET Core
- **Especialista Táctico**: Python 3.11, FastAPI
- **Frontend**: Vue 3 + TypeScript (Dashboard futurista y reactivo)
- **Base de Datos**: PostgreSQL con TimescaleDB (para analítica de series temporales)
- **Caché**: Redis
- **Message Broker**: RabbitMQ
- **Automatización y Tiempo Real**: Hangfire (Backups/Jobs) y SignalR (WebSockets)
- **Infraestructura**: Docker, Docker Compose, GitHub Actions

## Requisitos Previos

Para ejecutar y desarrollar en este proyecto, necesitas instalar:
- Docker y Docker Compose
- .NET 8 SDK
- Python 3.11 y Poetry
- Node.js 20

## Estructura del Repositorio

- `/src/backend-dotnet`: API REST principal y cerebro del sistema.
- `/src/backend-python`: API especializada en scraping, IA y analítica pesada.
- `/src/frontend`: Single Page Application (Dashboard).
- `/contracts`: Esquemas JSON para validación de eventos de mensajería (ej. RabbitMQ).
- `/docker`: Archivos Dockerfile y utilidades.
- `/docs/adr`: Architecture Decision Records (ADR).
- `.github/workflows`: Pipelines de CI/CD.

## Cómo levantar el entorno de desarrollo local

La Fase 0 define la infraestructura base. Para iniciar los servicios de soporte (PostgreSQL, Redis, RabbitMQ):

1. Clona el repositorio y navega a la raíz del proyecto.
2. Levanta la infraestructura con Docker Compose:
   ```bash
   docker compose up -d
   ```
3. Esto iniciará:
   - PostgreSQL + TimescaleDB (Puerto `5432`)
   - Redis (Puerto `6379`)
   - RabbitMQ (Puerto `5672` para AMQP y `15672` para el panel de gestión web)

### Siguientes Pasos (Fase 1)

En la Fase 1, se añadirá el código funcional. Los comandos típicos que se usarán serán:

- **Backend .NET**: 
  ```bash
  cd src/backend-dotnet
  dotnet run
  ```
- **Backend Python**:
  ```bash
  cd src/backend-python
  poetry run uvicorn main:app --reload
  ```
- **Frontend Vue**:
  ```bash
  cd src/frontend
  npm install
  npm run dev
  ```

## Despliegue

- [Guía de Despliegue](./docs/deployment.md) — local con Docker Compose, Kubernetes, y observabilidad.
- [ADR-0001: Fundación Arquitectónica](./docs/adr/0001-foundation.md).

## Observabilidad (Fase 5)

VortexFlow incluye un stack completo de observabilidad:

| Componente       | Rol                                  |
|------------------|--------------------------------------|
| OpenTelemetry    | Tracing, métricas y logs desde API y worker |
| Prometheus       | Almacenamiento de métricas           |
| Grafana          | Dashboards visuales                  |
| Loki             | Agregación de logs                   |
| Tempo            | Trazas distribuidas                  |
| OTLP Collector   | Pipeline de telemetría               |

Para iniciar: `docker compose -f docker-compose.observability.yml up -d`

## Estado del Proyecto

- ✅ Fase 0: Infraestructura base (Docker, PostgreSQL, Redis, RabbitMQ)
- ✅ Fase 1: Backend .NET (API REST, JWT, EF Core)
- ✅ Fase 2: Frontend Vue 3 (Dashboard, Calendario, Glassmorphism)
- ✅ Fase 3: Background Jobs & Tiempo Real (Hangfire, SignalR, MassTransit)
- ✅ Fase 4: Resiliencia (Circuit Breakers, Rate Limiting, Polly)
- ✅ Fase 5: DevOps & Observabilidad (K8s, OTLP, Grafana, CI/CD completo)
