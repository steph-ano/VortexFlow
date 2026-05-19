# ADR-0001: Fundación Arquitectónica de VortexFlow

## Contexto

VortexFlow es un proyecto de analítica de tendencias y automatización de contenido diseñado para escalar y ofrecer alta disponibilidad. Se requiere establecer una base sólida que soporte procesamiento asíncrono pesado, persistencia de alta velocidad para series temporales y una experiencia en tiempo real para el frontend.

## Decisiones

Para la arquitectura base, se han tomado las siguientes decisiones:

1. **Separación en dos Bounded Contexts**:
   - **Cerebro Transaccional (.NET 8 / C#)**: Responsable de las reglas de negocio, seguridad, APIs principales, coordinación del sistema y entrega de datos al frontend.
   - **Especialista Táctico (Python 3.11 / FastAPI)**: Dedicado a la analítica de datos, machine learning, procesamiento de lenguaje natural y scraping pesado.

2. **Comunicación asíncrona**:
   - Se utilizará **RabbitMQ** como message broker para orquestar la comunicación desacoplada. 
   - El evento principal que conectará ambos contextos será `TrendsProcessed`, facilitando que el Especialista Táctico notifique al Cerebro Transaccional los resultados de su análisis sin bloqueos.

3. **Persistencia políglota**:
   - **PostgreSQL con TimescaleDB**: Servirá como la fuente principal de verdad, optimizada para almacenar datos de series temporales de manera eficiente (ideal para historial de tendencias).
   - **Redis**: Actuará como caché de alta velocidad en memoria para acelerar las lecturas frecuentes y manejar datos efímeros.

4. **Estrategia de resiliencia**:
   - Se implementarán patrones como **Circuit Breaker** en .NET para la comunicación con Redis, con fallback automático a PostgreSQL.
   - Configuración de Dead-Letter Queues (DLQ) en RabbitMQ con reintentos para manejar fallos de procesamiento de mensajes.

5. **Automatización y Real-time**:
   - Uso de **Hangfire** en el entorno .NET para la gestión y programación de tareas asíncronas y publicación de contenidos.
   - Uso de **SignalR** para empujar actualizaciones en tiempo real al frontend, manteniendo al usuario informado de nuevas tendencias de forma instantánea.

## Consecuencias

### Ventajas
- **Escalabilidad independiente**: Cada backend puede escalar en función de sus cargas de trabajo (ej. más instancias de Python para procesamiento pesado, más de .NET para tráfico HTTP).
- **Desacoplamiento**: Si el Especialista Táctico se cae, el Cerebro Transaccional puede seguir sirviendo el dashboard.
- **Rendimiento**: Combinación de TimescaleDB y Redis proporciona la mejor velocidad y estructuración temporal.

### Riesgos y Compromisos
- **Complejidad operativa**: Mantener múltiples lenguajes (C#, Python, JS/TS) y bases de datos requiere mayor experiencia de DevOps y equipo multidisciplinar.
- **Consistencia eventual**: Debido a la comunicación asíncrona por RabbitMQ, los datos pueden tardar milisegundos o segundos en reflejarse en todos lados.
