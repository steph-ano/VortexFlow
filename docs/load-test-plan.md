# Plan de Pruebas de Carga y Caos (VortexFlow)

## 1. Objetivos (SLIs / SLOs)
- **SLI 1:** Tiempo de respuesta del endpoint de tendencias (`/api/trends/current`).
  - **SLO:** 95% de las peticiones deben ser atendidas en < 200ms en condiciones normales, y < 2000ms bajo carga o degradación.
- **SLI 2:** Tasa de errores (HTTP 500+).
  - **SLO:** < 1% de errores durante operaciones normales o de estrés.

## 2. Herramientas
- **k6:** Para pruebas de carga y estrés (`tests/load/dashboard-load.js`).
- **Docker Compose:** Para simulaciones de caos (`docker compose stop <service>`).

## 3. Escenarios de Carga (k6)
El script de k6 está configurado para:
1. Escalar hasta **500 usuarios concurrentes** (VUs) en 2 minutos.
2. Mantener la carga de 500 VUs por 3 minutos.
3. Descender la carga a 0 durante 1 minuto.

Durante la prueba, cada usuario simulará interactuar con el Dashboard, pidiendo las tendencias más recientes e intentando calendarizar una publicación falsa.

**Ejecución:**
```bash
k6 run tests/load/dashboard-load.js
```

## 4. Pruebas de Caos (Resiliencia)
Mientras se ejecuta el script de k6 a una carga media (50-100 VUs), forzaremos caídas para verificar el comportamiento de los Circuit Breakers.

### Escenario A: Caída de Redis (Caché Principal)
1. **Comando:** `docker compose stop redis`
2. **Resultado Esperado:** 
   - El `RedisTrendCache` en .NET lanzará un `RedisConnectionException`.
   - El Circuit Breaker de Polly interceptará el fallo y cambiará al `MemoryCache` y a la base de datos `PostgreSQL`.
   - Las métricas de k6 mostrarán un aumento leve en el tiempo de respuesta, pero la tasa de error (HTTP 500) debe mantenerse por debajo del 1%.
3. **Recuperación:** `docker compose start redis` -> El Circuit Breaker debe hacer *Half-Open* y recuperar la conexión.

### Escenario B: Caída de RabbitMQ (Message Broker)
1. **Comando:** `docker compose stop rabbitmq`
2. **Resultado Esperado:**
   - Celery dejará de recibir tareas programadas de scraping, pero la UI seguirá operando.
   - El proceso de publicación en Python (`app.tasks.publishing`) fallará al escribir en RabbitMQ.
   - `tenacity` interceptará el fallo y usará el fallback HTTP hacia `.NET`.
   - `.NET` recibirá el payload en `/api/trends/ingest` y actualizará SignalR / Base de datos exitosamente.
3. **Recuperación:** `docker compose start rabbitmq`.
