# Propuesta: Frontend CRUD de Personas

## Intención

Personas tiene API completa pero carece de interfaz web. Los operadores necesitan gestionar personas (altas, bajas, edición, consulta) desde la shell web SGV, con el mismo patrón de segmentación activas/eliminadas que Cargos y Unidades Organizativas.

## Alcance

### Incluye
- Integration client tipado (`PersonaApiClient`) en `SGV.Web/Integration/Personas/`
- InputModel, ViewModel, FormHelpers, PostResultMapper por módulo
- `Index.cshtml` + `Index.cshtml.cs` — listado paginado con pestañas Activas / Eliminadas, búsqueda, ordenamiento, baja lógica y reactivación (rol Administrador)
- `Create.cshtml` + `Create.cshtml.cs` — formulario de creación con validación
- `Edit.cshtml` + `Edit.cshtml.cs` — formulario de edición con carga de datos existentes
- `Details.cshtml` + `Details.cshtml.cs` — vista de detalle readonly
- Typeahead/buscador de personas reutilizable (componente parcial o script)
- Wire-types de Personas en `SGV.Contracts.Personas` (DTOs, request records, command results)

### No incluye
- Frontend de habilidades de persona (personas/{id}/skills) — queda para cambio futuro
- Integración con Usuarios (asignar persona a usuario) — fuera de alcance
- Cambios en el backend de Personas (API, Aplicación, Dominio, Infraestructura)
- Tests de frontend (se crean en fase de implementación, no se planifican aquí)

## Capacidades

### Nuevas Capacidades
- `personas-web-frontend`: Razor Pages CRUD de Personas con listado segmentado, creación, edición, detalle, baja lógica y reactivación; más typeahead reutilizable

### Capacidades Modificadas
- Ninguna — es frontend nuevo sobre API existente; no cambia especificaciones de backend

## Enfoque

Calcar el patrón comprobado de `Cargos`: misma estructura de carpetas en `Pages/Organizacion/Personas/`, mismo `IApiClient` → `ApiClient` → `InputModel` → `ViewModel` → `PostResultMapper`, mismo manejo de segmentación, PRG, feedback y autorización por rol. Los wire-types actualmente en `SGV.Aplicacion.Personas` se replican en `SGV.Contracts.Personas` para que `SGV.Web` los consuma sin depender de la capa de aplicación.

## Áreas Afectadas

| Área | Impacto | Descripción |
|------|---------|-------------|
| `src/SGV.Contracts/Personas/` | Nuevo | DTOs, requests y command results consumidos por Web |
| `src/SGV.Web/Integration/Personas/` | Nuevo | `IPersonaApiClient`, `PersonaApiClient`, input/view-models, helpers |
| `src/SGV.Web/Pages/Organizacion/Personas/` | Nuevo | Index, Create, Edit, Details + partials |
| `src/SGV.Web/Program.cs` | Modificado | Registro DI de `IPersonaApiClient` + `HttpClient` |
| `src/SGV.Web/Pages/Organizacion/Personas/Shared/` | Nuevo | Partial del typeahead de personas |

## Riesgos

| Riesgo | Prob. | Mitigación |
|--------|-------|------------|
| Los wire-types de Personas están en `Aplicacion`, no en `Contracts` — migrarlos requiere crear nuevos records en `Contracts` (no modificar los existentes) | Media | Los records actuales de `Aplicacion` se mantienen intactos; `Contracts` define copias para consumo Web. El API serializa el mismo shape, no hay ruptura |
| La API de Personas no tiene endpoint `consulta` paginado como Cargos — listado actual sin paginación server-side | Alta | Evaluar en spec/design: añadir endpoint paginado o hacer paginación client-side. Si se agrega endpoint, es cambio backend menor dentro del mismo change |
| Typeahead requiere un endpoint de búsqueda rápida — la API actual solo tiene `GET /api/v1/personas` (listado completo) | Media | Usar listado completo con filtro client-side para el typeahead, o agregar query params de búsqueda al GET existente |

## Plan de Rollback

Eliminar la carpeta `Pages/Organizacion/Personas/` y el integration client de Personas; revertir registro DI en `Program.cs`; borrar `SGV.Contracts.Personas/`. No afecta datos ni API.

## Dependencias

- Backend de Personas ya operativo en API — ningún cambio externo requerido

## Criterios de Éxito

- [ ] Listado segmentado activas/eliminadas funciona con PRG, feedback y autorización
- [ ] Creación de persona válida redirige a Details con mensaje success
- [ ] Edición de persona existente persiste cambios y retorna al listado
- [ ] Baja lógica (DELETE) y reactivación (PATCH) funcionan desde el listado
- [ ] Typeahead muestra personas activas al tipear
- [ ] `dotnet build SGV.slnx` compila sin errores
