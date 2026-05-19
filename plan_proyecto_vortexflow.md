# Plan de Proyecto: NexusTrend v1.0

## Visión General
Construir una plataforma corporativa de analítica de tendencias y automatización de contenido con arquitectura híbrida (.NET + Python + Angular/Vue), aplicando Domain-Driven Design, resiliencia empresarial y experiencia de usuario premium.

---

## Fase 0 – Fundación
**Objetivo:** Alinear arquitectura, entornos y contratos antes de picar código.

### Épica 0.1 – Definición de contratos entre Bounded Contexts
- Diseñar el esquema de mensajería: evento `TrendsProcessed` con payload JSON (hashtags, métricas, fuente, timestamp).
- Definir el contrato del endpoint REST/gRPC en .NET para recibir datos de Python (como respaldo a mensajería).
- Establecer formato de prefijos de clave en Redis: `trend:current:{platform}:{hashtag}` y `trend:history:{id}`.

### Épica 0.2 – Configuración de entornos
- Docker Compose local con: PostgreSQL (con TimescaleDB opcional), Redis, RabbitMQ (o Redis Pub/Sub), y los dos servicios.
- Configurar variables de entorno y secretos con archivos .env (local) y plan para Vault en producción.
- Crear repositorios Git, pipeline CI/CD inicial (GitHub Actions / GitLab CI) con lint, build y tests unitarios.

**Entregable:** Documento de Arquitectura de Software (ADR) con diagrama C4 de contenedores y contratos API/mensajes.

---

## Fase 1 – Núcleo Transaccional (.NET) y Persistencia
**Objetivo:** Construir el “Cerebro” con seguridad, usuarios, campañas y calendario editorial.

### Épica 1.1 – Modelo de dominio y base de datos
- Definir entidades: User, Campaign, ScheduledPost, TrendSnapshot (con JSONB para metadatos flexibles).
- Configurar EF Core con migraciones, usando índices GIN sobre columnas JSONB (si el dashboard filtra tendencias por hashtag desde BD).
- Implementar `TenantId` en entidades (si SaaS multi-tenant) y filtro global en EF Core.
- Crear repositorios con patrón Repository, bajo principios de DDD (agregados raíz).

### Épica 1.2 – Autenticación y autorización
- Implementar Identity (ASP.NET Core Identity) para gestión de usuarios y login JWT.
- Definir roles/políticas: Admin, Editor, Viewer.
- Endpoints de registro, login, refresh token.
- Proteger API con middleware de autenticación.

### Épica 1.3 – Calendario editorial y publicación programada
- CRUD de campañas y publicaciones programadas (ScheduledPost) con fechas programadas y estado.
- Integrar Hangfire: encolar trabajos de publicación. Almacenar JobId asociado al ScheduledPost.
- Implementar recurrencia opcional (con Hangfire Recurring Jobs) y panel de monitoreo (Hangfire Dashboard).
- Endpoints para reencolar tareas fallidas manualmente (opción desde UI administrativa).

### Épica 1.4 – Consumidor de mensajes de tendencias
- Configurar consumidor RabbitMQ (o Redis Pub/Sub) para eventos `TrendsProcessed`.
- Al recibir, almacenar en tabla `TrendSnapshots` (series temporales) y refrescar caché Redis con alta prioridad (invalidar/actualizar claves `trend:current`).
- Si el mensajero falla, implementar endpoint HTTP alternativo para recibir lotes (backup).

**Entregable:** API REST de .NET completamente funcional con endpoints de usuarios, campañas, publicaciones, y almacenamiento histórico de tendencias.

---

## Fase 2 – Especialista Táctico (Python) y Estrategia de Datos
**Objetivo:** Scraping, procesamiento analítico y entrega resiliente de tendencias.

### Épica 2.1 – Infraestructura de scraping y colas
- Elegir Celery + Redis/RabbitMQ como sistema de tareas asíncronas.
- Implementar workers de scraping: por fuente (Twitter/X, Instagram, TikTok) usando APIs oficiales y, solo donde sea legal, scraping respetuoso (con rate limiting y user-agents rotativos).
- Definir tareas periódicas (Celery Beat) para recolección programada.

### Épica 2.2 – Procesamiento de tendencias
- Parseo de respuestas API a modelo interno de tendencia (hashtag, volumen, sentimiento, entidades).
- Almacenar resultados crudos en Redis con TTL corto, y publicar evento `TrendsProcessed` al broker.
- Si el broker falla, enviar batch vía HTTP al endpoint de respaldo en .NET.
- Implementar bloqueo distribuido (Redis Lock) para evitar scraping duplicado al escalar workers.

### Épica 2.3 – Monitorización y ética
- Dashboard Flower para Celery (visibilidad de tareas de scraping).
- Registrar logs estructurados (structlog) en formato JSON.
- Implementar módulo de cumplimiento: guardar metadata de origen (API key usada, timestamp, respeto de rate limits) para auditoría.
- Configurar backoff exponencial y circuit breaker (tenacity) en llamadas a APIs externas.

### Épica 2.4 – Entrega de datos
- Crear API FastAPI con endpoint de estado y un endpoint de entrega manual (para pruebas).
- Asegurar que el servicio Python sea stateless y reciba configuraciones por variables de entorno.

**Entregable:** Servicio Python que recolecta, analiza y entrega tendencias de forma fiable y trazable.

---

## Fase 3 – Frontend Premium y Experiencia de Usuario
**Objetivo:** Construir el centro de comando con dashboard en tiempo real y editor Kanban/Calendario.

### Épica 3.1 – Setup y diseño UI
- Elegir framework (Vue 3 + Pinia o Angular). Configurar con TypeScript, SCSS, y librerías de componentes base (Tailwind o Vuetify).
- Implementar sistema de diseño con Modo Oscuro, efectos glassmorphism (backdrop-filter, bordes semitransparentes), usando variables CSS para personalización.
- Asegurar accesibilidad WCAG 2.1 AA y rendimiento (lazy loading, análisis Lighthouse).

### Épica 3.2 – Autenticación y enrutado
- Login, registro, guard de rutas basado en roles.
- Almacenar JWT en HttpOnly cookie (o en memoria con refresh automático).
- Interceptor para añadir token a peticiones.

### Épica 3.3 – Dashboard de tendencias
- Conectar a API .NET para obtener lista de tendencias actuales (que a su vez toma de Redis).
- Gráficos de fluctuación (Chart.js, ECharts) con actualización en tiempo real vía SignalR: suscribirse a hub .NET que emite eventos cuando llegan nuevos `TrendSnapshots`.
- Filtros por plataforma, fecha, tipo de contenido.

### Épica 3.4 – Calendario editorial y Kanban
- Visualización tipo calendario (FullCalendar) con arrastre de publicaciones desde un panel lateral.
- Estado drag & drop con Pinia/NgRx: cambios locales optimistas, confirmación mediante API.
- Panel de administración de publicaciones fallidas, posibilidad de reencolar (botón que llama a endpoint .NET para reactivar Hangfire job).
- Filtros, búsqueda y paginación.

**Entregable:** SPA desplegable que ofrece visualización en tiempo real y gestión de contenido con interacción fluida.

---

## Fase 4 – Seguridad, Resiliencia y Escalado
**Objetivo:** Blindar el sistema para producción, manejar fallos y garantizar operación continua.

### Épica 4.1 – Gestión de secretos y configuración
- Migrar secretos a Vault (local dev con Vault en Docker, prod con servicio gestionado).
- Configurar escaneo de vulnerabilidades en dependencias (Dependabot, Snyk).

### Épica 4.2 – Degradación elegante
- Implementar Circuit Breaker en .NET para Redis: si Redis está caído, servir tendencias desde PostgreSQL (consulta a `TrendSnapshots` reciente).
- Middleware de reintentos y timeout en llamadas HTTP entre servicios.
- Cache fallback en memoria local con expiración corta (MemoryCache en .NET).

### Épica 4.3 – Colas muertas y reintentos manuales
- Configurar dead-letter queue en RabbitMQ para eventos de tendencias no procesados.
- Crear UI admin (parte del frontend) para listar tareas de publicación fallidas en Hangfire y dar opción de reintentar / editar payload.
- Implementar reintentos automáticos con backoff y límite en Hangfire y Celery.

### Épica 4.4 – Pruebas de carga y caos
- Pruebas con k6 o Locust simulando 500 usuarios concurrentes en dashboard y 10,000 publicaciones programadas.
- Simular caída de Redis, verificar fallback a BD.
- Simular latencia en APIs externas, verificar que el sistema no colapsa.

**Entregable:** Informe de resiliencia con SLIs/SLOs básicos, y configuración de alertas (Prometheus + Grafana).

---

## Fase 5 – DevOps, Observabilidad y Despliegue
**Objetivo:** Preparar infraestructura automatizada y monitoreo profesional.

### Épica 5.1 – Contenerización y orquestación
- Dockerfiles optimizados multi-stage para .NET y Python.
- Helm charts o manifests Kubernetes para ambos servicios, Redis y RabbitMQ (usar Bitnami charts).
- Configurar Ingress (Nginx ingress controller) con TLS y reglas de enrutamiento.

### Épica 5.2 – CI/CD
- Pipeline: build, test, análisis de código (SonarQube), escaneo de imágenes, deploy a staging.
- Estrategia de ramas: trunk-based con feature flags.
- Automatizar migraciones de EF Core como parte del deploy de .NET (init container).

### Épica 5.3 – Observabilidad
- Centralizar logs con Serilog (sink Elasticsearch) y visualización en Kibana / Grafana Loki.
- Instrumentación con OpenTelemetry: tracing distribuido entre .NET, Python y llamadas a Redis.
- Dashboard Grafana con métricas de negocio: tendencias recolectadas por hora, publicaciones exitosas/fallidas, latencia de API.

**Entregable:** Ambientes staging/productivo operativos, con pipelines automatizadas y dashboards de observabilidad.

---

## Fase 6 – Validación y Lanzamiento MVP
**Objetivo:** Pulir funcionalidad, corregir bugs y liberar versión estable.

### Épica 6.1 – Pruebas funcionales E2E
- Automatizar flujos críticos con Cypress/Playwright: login, visualización de tendencias, creación de campaña, arrastre y programación.
- Pruebas de regresión manuales por QA.

### Épica 6.2 – Documentación y onboarding
- Generar Swagger (OpenAPI) para .NET y FastAPI.
- Crear guía de desarrollo local, manual de usuario y videos cortos.

### Épica 6.3 – Lanzamiento beta
- Desplegar en producción limitada, monitorizar comportamiento.
- Recoger feedback de usuarios piloto y ajustar prioridades.

**Entregable:** MVP estable y operativo en producción controlada.

---

## Fase 7 – Evolución Post-MVP (Opcional)
Épicas documentadas para el backlog futuro:

- **Motor de Recomendación:** Modelo predictivo con ML.NET o Python scikit-learn que sugiera el mejor horario de publicación y tipo de contenido basado en rendimiento pasado.
- **Clasificación automática con NLP:** Integrar Hugging Face / spaCy para análisis de sentimiento y extracción de entidades, enriqueciendo las tendencias automáticamente.
- **Simulador de Impacto:** UI que, al arrastrar un post, muestra una estimación de alcance potencial.
- **Soporte para más fuentes:** YouTube, LinkedIn, Reddit.