# Regression Checklist - VortexFlow MVP

Este documento lista los flujos críticos que deben verificarse manualmente antes de cada release.

## Autenticación

| # | Test Case | Pasos | Resultado Esperado | Prioridad |
|---|-----------|-------|-------------------|-----------|
| 1.1 | Login exitoso | Ingresar admin@vortexflow.local / Admin123! | Redirección al dashboard | Alta |
| 1.2 | Login fallido | Credenciales incorrectas | Mensaje de error visible | Alta |
| 1.3 | Sesión expirada | Esperar expiration token | Redirect a login | Alta |
| 1.4 | Logout | Click en logout | Redirect a login | Media |

## Dashboard

| # | Test Case | Pasos | Resultado Esperado | Prioridad |
|---|-----------|-------|-------------------|-----------|
| 2.1 | Carga de dashboard | Navegar a /dashboard | Dashboard visible sin errores | Alta |
| 2.2 | Gráficos cargan | Esperar 5s | Gráficos con datos visibles | Alta |
| 2.3 | Estadísticas por plataforma | Verificar Twitter/Instagram/LinkedIn | Cards con números | Alta |
| 2.4 | Indicador de conexión | Verificar en header | Shows "Conectado" o similar | Media |
| 2.5 | Cambio de rango de tiempo | Cambiar a 24h/7d/30d | Gráficos se actualizan | Media |
| 2.6 | Actualización automática | Esperar 30s | Datos se actualizan | Baja |

## Gestión de Campañas

| # | Test Case | Pasos | Resultado Esperado | Prioridad |
|---|-----------|-------|-------------------|-----------|
| 3.1 | Listar campañas | Navegar a /campaigns | Tabla con campañas visible | Alta |
| 3.2 | Crear campaña | Click crear + completar formulario | Campaña aparece en lista | Alta |
| 3.3 | Editar campaña | Click editar + modificar + guardar | Cambios reflejados | Alta |
| 3.4 | Eliminar campaña | Click eliminar + confirmar | Campaña no aparece | Alta |
| 3.5 | Filtrar por estado | Seleccionar "Activa" | Solo activas visibles | Media |
| 3.6 | Buscar campaña | Escribir nombre en búsqueda | Resultados filtrados | Media |

## Calendario Editorial

| # | Test Case | Pasos | Resultado Esperado | Prioridad |
|---|-----------|-------|-------------------|-----------|
| 4.1 | Carga de calendario | Navegar a /calendar | Calendario visible | Alta |
| 4.2 | Navegación meses | Click next/prev | Mes cambia correctamente | Media |
| 4.3 | Crear post desde día | Click en día + completar | Post aparece en día | Alta |
| 4.4 | Programar para futuro | Seleccionar fecha futura | Post programado | Alta |
| 4.5 | Drag & drop | Mover post a otro día | Post rescheduleado | Media |
| 4.6 | Filtrar plataforma | Seleccionar Twitter | Solo Twitter visibles | Media |
| 4.7 | Cambiar vista | Cambiar a semana/lista | Vista cambia | Baja |

## Publicaciones

| # | Test Case | Pasos | Resultado Esperado | Prioridad |
|---|-----------|-------|-------------------|-----------|
| 5.1 | Listar publicaciones | Navegar a /posts | Tabla visible | Alta |
| 5.2 | Filtrar por estado | Seleccionar "Fallida" | Solo fallidas visibles | Alta |
| 5.3 | Ver detalles | Click en publicación | Modal con detalles | Media |
| 5.4 | Reintentar fallida | Click retry en fallida | Post se reintenta | Alta |
| 5.5 | Previsualizar | Hover en post | Preview visible | Media |

## Resiliencia

| # | Test Case | Pasos | Resultado Esperado | Prioridad |
|---|-----------|-------|-------------------|-----------|
| 6.1 | Redis caído | Detener Redis + usar app | App funciona (circuit open) | Alta |
| 6.2 | API caída | Detener API + usar worker | Graceful error | Alta |
| 6.3 | Reconexión SignalR | Cortar red + reestablecer | Reconexión automática | Alta |

## Tiempo Real

| # | Test Case | Pasos | Resultado Esperado | Prioridad |
|---|-----------|-------|-------------------|-----------|
| 7.1 | Notificaciones en vivo | Crear campaña desde otro tab | Notificación en tiempo real | Alta |
| 7.2 | Actualización dashboard | Publicar desde worker | Dashboard se actualiza | Alta |

## Rendimiento

| # | Test Case | Pasos | Resultado Esperado | Prioridad |
|---|-----------|-------|-------------------|-----------|
| 8.1 | Carga inicial | Medir tiempo hasta interactivo | < 3 segundos | Media |
| 8.2 | Navegación entre páginas | Cambiar de vista | < 500ms | Media |
| 8.3 | Carga de gráficos | Esperar rendering | Sin freeze | Media |

## Checklist de Release

Antes de cada release, ejecutar:

- [ ] Todos los tests E2E automatizados pasan
- [ ] Regression checklist completado al 100%
- [ ] Sin errores críticos en logs de producción
- [ ] Timeouts dentro de umbrales
- [ ] Validación de seguridad completada
- [ ] Documentación actualizada
- [ ] CHANGELOG actualizado

## Severidad de Defectos

| Prioridad | Descripción | SLA |
|-----------|-------------|-----|
| Crítica | Login roto, no se puede acceder | Fix inmediato |
| Alta | Flujo principal no funciona | 24 horas |
| Media | Funcionalidad secundaria afectada | 1 semana |
| Baja | Mejora o cosmetic | Próxima iteración |