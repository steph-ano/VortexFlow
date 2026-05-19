# Guía de Usuario - VortexFlow

VortexFlow es una plataforma de analítica de tendencias y automatización de contenido corporativo. Esta guía te ayudará a usar las funcionalidades principales del sistema.

## Cómo Acceder al Sistema

### Credenciales por Defecto

| Rol | Email | Contraseña |
|-----|-------|------------|
| Administrador | admin@vortexflow.local | Admin123! |
| Editor | editor@vortexflow.local | Editor123! |
| Viewer | viewer@vortexflow.local | Viewer123! |

### Pasos de Acceso

1. Abre tu navegador e ingresa a: `http://localhost:5173`
2. Ingresa tu correo electrónico
3. Ingresa tu contraseña
4. Click en "Iniciar Sesión"
5. Serás redirigido al Dashboard

## Roles de Usuario

### Administrador
- Acceso completo a todas las funcionalidades
- Gestión de usuarios
- Configuración del sistema
- Todas las operaciones de CRUD

### Editor
- Crear y editar campañas
- Programar publicaciones
- Ver dashboard y estadísticas
- Reintentar publicaciones fallidas

### Viewer
- Solo visualización
- Ver dashboard y calendario
- No puede crear ni modificar contenido

## Dashboard de Tendencias

El dashboard muestra información en tiempo real sobre tendencias en redes sociales.

### Componentes

1. **Gráficos de Tendencias**
   - Visualización de volumen de menciones por plataforma
   - Evolución temporal de hashtags
   - Comparativas entre plataformas

2. **Tarjetas de Estadísticas**
   - Twitter: Menciones, engagement, alcance
   - Instagram: Publicaciones, stories, reels
   - LinkedIn: Reactions, comentarios, shares
   - Facebook: Interacciones, alcance

3. **Indicador de Conexión**
   - Muestra estado de conexión en tiempo real
   - Verde: Conectado
   - Rojo: Desconectado

### Filtros de Tiempo

- **24 horas**: Últimas 24 horas
- **7 días**: Última semana
- **30 días**: Último mes
- **Personalizado**: Rango de fechas específico

### Actualización de Datos

Los datos se actualizan automáticamente cada 30 segundos. También puedes usar el botón de "Actualizar" para obtener datos inmediatos.

## Gestión de Campañas

### Crear una Campaña

1. Navega a **Campañas** en el menú lateral
2. Click en **+ Nueva Campaña**
3. Completa el formulario:
   - **Nombre**: Nombre descriptivo de la campaña
   - **Descripción**: Detalles de la campaña
   - **Plataforma**: Selecciona una o varias (Twitter, Instagram, LinkedIn, Facebook)
   - **Hashtags**: Lista de hashtags a rastrear
4. Click en **Guardar**

### Editar una Campaña

1. En la lista de campañas, busca la campaña deseada
2. Click en el botón de edición
3. Modifica los campos necesarios
4. Click en **Guardar Cambios**

### Eliminar una Campaña

1. En la lista de campañas, busca la campaña a eliminar
2. Click en el botón de eliminación
3. Confirma la eliminación en el diálogo

### Estados de Campaña

| Estado | Descripción |
|--------|-------------|
| Activa | La campaña está ejecutándose y recolectando datos |
| Pausada | La campaña está temporalmente detenida |
| Completada | La campaña terminó su duración programada |
| Borrador | Campaña no publicada aún |

## Calendario Editorial

El calendario te permite programar y visualizar publicaciones programadas.

### Vista del Calendario

- **Vista Mes**: Ver todo el mes
- **Vista Semana**: Vista detallada de la semana
- **Vista Lista**: Lista de publicaciones programadas

### Programar una Publicación

1. Navega a **Calendario**
2. Click en el día deseado
3. Se abrirá el modal de creación
4. Completa:
   - **Contenido**: Texto de la publicación
   - **Plataforma**: Selecciona la red social
   - **Fecha y Hora**: Selecciona cuándo publicar
   - **Campaña**: Asocia a una campaña (opcional)
5. Click en **Programar**

### Reorganizar Publicaciones

Si el calendario soporta drag & drop:
1. Mantén presionado sobre una publicación
2. Arrastra a una nueva fecha/hora
3. Suelta para confirmar el cambio

### Filtrar por Plataforma

Usa el selector de plataforma para ver solo las publicaciones de una red social específica.

## Publicaciones

### Listado de Publicaciones

En la sección **Publicaciones** puedes ver todas las publicaciones creadas.

### Estados de Publicación

| Estado | Descripción |
|--------|-------------|
| Pendiente | Esperando ser publicada |
| Publicada | Ya publicada exitosamente |
| Fallida | Error al publicar (puede reintentarse) |
| Programada | Fecha futura de publicación |

### Reintentar una Publicación Fallida

Cuando una publicación falla:

1. Filtra por estado "Fallida"
2. Busca la publicación problemática
3. Click en el botón **Reintentar**
4. El sistema intentará publicar nuevamente

### Ver Detalles de Publicación

1. Click en cualquier publicación
2. Se mostrará un modal con:
   - Contenido completo
   - Plataforma
   - Fecha de creación
   - Fecha de publicación
   - Métricas (si ya fue publicada)
   - Logs de error (si falló)

## Solución de Problemas

### No puedo iniciar sesión
- Verifica que las credenciales sean correctas
- Confirma que los servicios estén activos
- Limpia la caché del navegador

### El dashboard no carga datos
- Verifica que el worker de Python esté ejecutándose
- Revisa la conexión con Redis
- Confirma que haya datos en la base de datos

### Las publicaciones fallan constantemente
- Verifica las credenciales de las APIs de redes sociales
- Revisa los logs en la sección de publicaciones
- Verifica la conexión con RabbitMQ

### La conexión en tiempo real no funciona
- Confirma que SignalR esté configurado
- Verifica la conexión WebSocket
- Revisa la consola del navegador para errores

## Atajos de Teclado

| Atajo | Acción |
|-------|--------|
| `Ctrl + K` | Buscar |
| `Ctrl + N` | Nueva campaña |
| `Ctrl + /` | Mostrar ayuda |
| `Esc` | Cerrar modales |

## Contacto y Soporte

Para problemas técnicos o consultas:
- Email: soporte@vortexflow.local
- Documentación: `/docs`
- Issues: GitHub Issues

---

**Versión**: 1.0.0 (MVP)
**Última actualización**: Mayo 2026