# Patrones de Resiliencia en VortexFlow

Este documento describe la arquitectura de tolerancia a fallos, degradación elegante y manejo de errores (Fase 4).

## 1. Patrón Circuit Breaker (Polly & Tenacity)
El sistema evita saturar servicios que están caídos utilizando interruptores de circuito.
- **Backend .NET (Polly):** El acceso a Redis está protegido por una política de Polly (`AsyncCircuitBreakerPolicy`). Si ocurren 3 fallos consecutivos (`RedisConnectionException` o `RedisTimeoutException`), el circuito se abre por 30 segundos.
- **Backend Python (Tenacity):** Las funciones de publicación y almacenamiento en caché están decoradas con `@retry` con un backoff exponencial, lo que permite reintentos espaciados en el tiempo.

## 2. Fallbacks y Degradación Elegante
Cuando el Circuit Breaker de Redis se abre en .NET, el sistema no falla, sino que se degrada de manera elegante:
1. Intenta leer de **IMemoryCache** (caché L1, vida útil 1 minuto).
2. Si no hay datos, consulta los *snapshots* directamente en la base de datos **PostgreSQL**.
3. Al escribir, si Redis está caído, escribe directamente en **IMemoryCache** para mantener los datos recientes en el nodo local.

## 3. Fallback de Mensajería (RabbitMQ -> HTTP)
En Python, el publicador primario de eventos de tendencias usa RabbitMQ (`kombu`). Si el clúster de RabbitMQ falla:
1. Tenacity captura la excepción de RabbitMQ.
2. El sistema redirige el mensaje a través de una petición síncrona HTTP `POST` hacia el endpoint `/api/trends/ingest` de .NET.
3. El endpoint HTTP también está protegido con reintentos para lidiar con parpadeos de red.

## 4. Colas de Mensajes Muertos (Dead-Letter Queues)
- **RabbitMQ (MassTransit):** Por defecto, si un consumidor de MassTransit en .NET falla de manera consecutiva (luego de consumir todos los reintentos automáticos), el mensaje se mueve a una cola terminada en `_error` (ej. `trends_processed_queue_error`).
- **Hangfire:** Los trabajos programados que fallan de manera crítica se marcan como "Failed" y se almacenan en PostgreSQL. La UI puede leerlos a través del endpoint `/api/admin/dead-letters` para reintentos manuales.

## 5. Prevención de Ataques de Fuerza Bruta (Rate Limiting)
- Los endpoints críticos de la API de .NET (ej. login y autenticación) están protegidos por el middleware `RateLimiter` utilizando el algoritmo **Fixed Window Limiter** (5 peticiones por minuto por IP), previniendo saturación de los recursos de la base de datos por ataques DDoS.
