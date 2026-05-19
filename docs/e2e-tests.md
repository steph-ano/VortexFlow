# Pruebas E2E - VortexFlow

Este documento describe cómo ejecutar las pruebas end-to-end automatizadas del proyecto VortexFlow.

## Requisitos Previos

- Node.js 18+
- Cypress instalado (`npm install` en `/src/frontend`)
- Servidor de desarrollo ejecutándose (`npm run dev`)
- Servicios de backend (.NET API, Python Worker, PostgreSQL, Redis, RabbitMQ) en ejecución

## Ejecutar Pruebas E2E

### Método 1: Consola (Headless)

```bash
cd src/frontend
npm install
npm run test:e2e
```

### Método 2: Interactivo (GUI)

```bash
cd src/frontend
npm run test:e2e:open
```

Esto abrirá la interfaz gráfica de Cypress donde puedes:
- Ver la ejecución de pruebas en tiempo real
- Ejecutar pruebas individuales
- Inspectear elementos
- Revisar screenshots de errores

## Estructura de Tests

```
cypress/
├── e2e/
│   ├── 01-login.cy.ts          # Tests de autenticación
│   ├── 02-dashboard.cy.ts      # Tests del dashboard
│   ├── 03-campaigns.cy.ts     # Tests de gestión de campañas
│   ├── 04-calendar.cy.ts      # Tests del calendario editorial
│   └── 05-posts.cy.ts         # Tests de publicaciones
├── support/
│   ├── commands.ts            # Comandos personalizados
│   └── e2e.ts                # Configuración global
└── cypress.config.ts         # Configuración de Cypress
```

## Tests Incluidos

### 1. Login (01-login.cy.ts)
- ✅ Visualización de página de login
- ✅ Inicio de sesión exitoso
- ✅ Error con credenciales inválidas
- ✅ Validación de campos obligatorios
- ✅ Cierre de sesión

### 2. Dashboard (02-dashboard.cy.ts)
- ✅ Carga del dashboard sin errores
- ✅ Visualización de gráficos de tendencias
- ✅ Tarjetas de estadísticas por plataforma
- ✅ Sin errores en consola
- ✅ Indicador de tiempo real
- ✅ Cambio de rangos de tiempo
- ✅ Actualización manual de datos

### 3. Campañas (03-campaigns.cy.ts)
- ✅ Listado de campañas
- ✅ Apertura de modal de creación
- ✅ Creación de nueva campaña
- ✅ Edición de campaña existente
- ✅ Eliminación de campaña
- ✅ Filtrado por estado
- ✅ Búsqueda de campañas

### 4. Calendario Editorial (04-calendar.cy.ts)
- ✅ Carga de vista de calendario
- ✅ Visualización de grid
- ✅ Navegación entre meses
- ✅ Apertura de modal de creación desde día
- ✅ Programación de post para fecha futura
- ✅ Visualización de posts programados
- ✅ Filtrado por plataforma
- ✅ Cambio de vistas (día/semana/lista)

### 5. Publicaciones (05-posts.cy.ts)
- ✅ Listado de publicaciones
- ✅ Filtrado por estado
- ✅ Visualización de detalles
- ✅ Reintento de publicación fallida
- ✅ Previsualización de contenido
- ✅ Visualización de métricas
- ✅ Búsqueda de publicaciones
- ✅ Paginación

## Configuración

### Variables de Entorno

Crea un archivo `cypress.env.json` en la raíz del proyecto:

```json
{
  "baseUrl": "http://localhost:5173",
  "apiUrl": "http://localhost:5032",
  "adminEmail": "admin@vortexflow.local",
  "adminPassword": "Admin123!"
}
```

### Configuración de Cypress (cypress.config.ts)

```typescript
export default defineConfig({
  e2e: {
    baseUrl: 'http://localhost:5173',
    viewportWidth: 1280,
    viewportHeight: 720,
    video: false,
    screenshotOnRunFailure: true,
    defaultCommandTimeout: 10000,
  },
})
```

## Integración con CI/CD

Las pruebas E2E se pueden integrar en el pipeline de GitHub Actions. El flujo recomendado es:

1. **Build**: Compilar la aplicación
2. **Unit Tests**: Ejecutar pruebas unitarias
3. **E2E Tests**: Ejecutar pruebas E2E (después de deploy a staging)
4. **Deploy**: Desplegar a producción

### Ejemplo de Job en GitHub Actions

```yaml
e2e-tests:
  needs: deploy-staging
  runs-on: ubuntu-latest
  steps:
    - uses: actions/checkout@v4
    - uses: actions/setup-node@v4
      with:
        node-version: '20'
    - run: npm ci
      working-directory: src/frontend
    - run: npm run test:e2e
      working-directory: src/frontend
      env:
        CYPRESS_BASE_URL: ${{ steps.deploy.outputs.url }}
```

## Solución de Problemas

### Error: "Cannot connect to API"

Verifica que los servicios estén ejecutándose:
```bash
docker compose up -d
dotnet run --project src/backend-dotnet/VortexFlow.Api
```

### Error: "Test timed out"

Incrementa el timeout en `cypress.config.ts`:
```typescript
defaultCommandTimeout: 20000,
```

### Error: "Element not found"

Los selectores pueden haber cambiado. Actualiza los selectores en los archivos de test.

## Mejores Prácticas

1. **Selectores estables**: Usa `data-testid` cuando sea posible
2. **Wait explícitos**: Usa `cy.waitForElement()` para elementos que cargan dinámicamente
3. **Aserciones claras**: Evita aserciones demasiado vagas
4. **Datos de prueba**: Limpia datos después de cada test con `afterEach`
5. **Registro**: Agrega logs con `cy.log()` para debugging

## Contribuir

Para agregar nuevos tests:
1. Crear archivo `XX-nombre.cy.ts` en `cypress/e2e/`
2. Seguir la estructura de los tests existentes
3. Agregar descripciones claras con `it('should...')`
4. Ejecutar tests localmente antes de commit