# Proposal: Auditoría — Filtros Select para Entidad y Operación

## Intent

El listado de auditoría (`/auditorias`) tiene hoy los filtros `Entidad` y `Operación` como `<input type="text">`, lo que obliga a adivinar o recordar valores exactos. Además, el filtro `Usuario` busca por `UserId` (GUID técnico) en vez de `UserName`. Este cambio convierte esos dos filtros en `<select>` poblados dinámicamente y alinea el filtro de usuario con el nombre visible.

## Scope

### In Scope
- Endpoint `GET /api/v1/auditorias/filter-options` (admin) → `{ entityNames: string[], operations: string[] }`
- `AuditoriaApiClient.GetFilterOptionsAsync` + `AuditoriaFilterOptions` DTO en `SGV.Contracts`
- Reemplazo de `<input>` por `<select>` con opción "Todos" para `entityName` y `operation` en `Index.cshtml`
- Toolbar envuelta en `.card` con borde suave
- Filtro `Usuario`: `UserId` → `UserName` en la query; parámetro renombrado en `AuditoriaListQuery`, controller y `IndexModel`
- Fallback no bloqueante en `IndexModel` si el endpoint de opciones falla

### Out of Scope
- Typeahead / autocomplete (sin valor claro por pocos valores distintos)
- Filtro `CorrelationId` sigue siendo input de texto
- Filtro `Usuario` como `<select>` (sin catálogo accesible)
- Nuevas columnas, índices o migraciones

## Capabilities

### Modified Capabilities
- **`auditoria-query`** (`openspec/specs/auditoria-query/spec.md`): el filtro `userId` se renombra a `userName` y pasa de filtrar por GUID técnico a filtrar por `u.UserName`; el nuevo endpoint `filter-options` devuelve los valores disponibles para poblar los selects. Los escenarios afectados son los de "Filtros combinables de consulta" y "Shell web admin-only".

### New Capabilities
- Ninguna nueva capability. `AuditoriaFilterOptions` es un DTO wire en `SGV.Contracts` (contrato entre API y Web), no una capability de dominio.

## Approach

1. **API**: `AuditoriasController` agrega `GET /api/v1/auditorias/filter-options` con `[Authorize(Roles = Administrador)]`. Usa `AsNoTracking()` + `SELECT DISTINCT EntityName/Operation` sin exponer old/new values (respeta D-2).
2. **Infraestructura**: `AuditoriaServicioConsulta` implementa el método y ajusta el filtro de usuario de `x.a.UserId == userId` a `x.u.UserName == userName` (reutiliza el LEFT JOIN existente con `AspNetUsers`).
3. **Contracts**: `AuditoriaFilterOptions` DTO en `src/SGV.Contracts/Auditoria/`; `AuditoriaListQuery.UserId` renombrado a `UserName`.
4. **Web**: `Index.cshtml` reemplaza los inputs por `<select>` dentro de un `<div class="card">`; selección automática dispara submit. `Index.cshtml.cs` precarga `FilterOptions` con fallback a inputs + mensaje no bloqueante.
5. **Tests**: actualizar `AuditoriasControllerTests` (nuevo endpoint + filtro `userName`) y `AuditoriaServicioConsultaTests`.

## Affected Areas

| Path | Impact | Description |
|------|--------|-------------|
| `src/SGV.Contracts/Auditoria/AuditoriaFilterOptions.cs` | New | DTO record con `EntityNames` y `Operations` |
| `src/SGV.Contracts/Auditoria/AuditoriaListQuery.cs` | Modified | Renombra `UserId` → `UserName` |
| `src/SGV.Aplicacion/Auditoria/IAuditoriaServicioConsulta.cs` | Modified | Nuevo `GetFilterOptionsAsync`; firma con `userName` |
| `src/SGV.Infraestructura/Persistencia/AuditoriaServicioConsulta.cs` | Modified | `SELECT DISTINCT` + filtro `u.UserName` |
| `src/SGV.Api/Controllers/AuditoriasController.cs` | Modified | Endpoint `GET /api/v1/auditorias/filter-options` |
| `src/SGV.Web/Integration/Auditoria/IAuditoriaApiClient.cs` | Modified | `GetFilterOptionsAsync` |
| `src/SGV.Web/Integration/Auditoria/AuditoriaApiClient.cs` | Modified | Implementación HTTP |
| `src/SGV.Web/Pages/Auditorias/Index.cshtml` | Modified | `<select>` + `.card` toolbar |
| `src/SGV.Web/Pages/Auditorias/Index.cshtml.cs` | Modified | Carga opciones + renombra route value `userId` → `userName` |
| `tests/SGV.Tests/Api/AuditoriasControllerTests.cs` | Modified | Tests del endpoint + filtro `userName` |
| `tests/SGV.Tests/Aplicacion/Auditoria/AuditoriaServicioConsultaTests.cs` | Modified | Test filtro `UserName` |
| `tests/SGV.Tests/Web/Auditoria/AuditoriasIndexTests.cs` | Modified | Test render selects y placeholder |

## Risks

| Risk | Likelihood | Mitigation |
|------|------------|------------|
| Breaking change `userId` → `userName` en query string | Med | Consumer único (SGV.Web); deployan juntos; documentar en PR summary |
| `filter-options` costly en tablas grandes | Baja | `AsNoTracking()` + `DISTINCT`; cacheo futuro si crece (fuera de scope) |
| PageModel rompe si endpoint falla | Baja | Fallback a inputs + mensaje no bloqueante via TempData/ViewData |
| Tests legacy rompen por renombrado de propiedad | Baja | Actualizar en el mismo PR |

## Rollback Plan

Tres ejes independientes (uno por PR si se encadenan):
1. Revert endpoint `filter-options`: borrar el handler. PageModel cae al fallback; listado sigue funcionando.
2. Revert `userId` → `userName`: restaurar parámetro en query, controller y route values.
3. Revert de la UI: volver a `<input>` simple sin tocar backend.
Como PR único: `git revert` del merge commit.

## Dependencies

- El endpoint necesita el LEFT JOIN existente con `AspNetUsers` (ya presente en `AuditoriaServicioConsulta` tras el cambio archivado del 2026-07-31).

## Success Criteria

- [ ] `GET /api/v1/auditorias/filter-options` devuelve `{ entityNames, operations }` no vacíos.
- [ ] `GET /api/v1/auditorias?userName=juan` filtra correctamente; `?userId=…` legacy ya no filtra.
- [ ] UI muestra selects poblados + opción "Todos" que limpia el filtro.
- [ ] Toolbar agrupada en `.card`.
- [ ] Si la API de opciones falla, la página sigue con inputs de texto + mensaje no bloqueante.
- [ ] `dotnet build SGV.slnx` y `dotnet test SGV.slnx` en verde.
- [ ] Sin regresiones en otros verticales.
