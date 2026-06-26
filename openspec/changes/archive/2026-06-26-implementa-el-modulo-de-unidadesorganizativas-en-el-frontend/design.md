# Design: Implementa el módulo de UnidadesOrganizativas en el frontend

## Technical Approach

Se agregará un listado autenticado en `SGV.Web` bajo Razor Pages, reutilizando la shell actual, un cliente HTTP tipado y la baseline visual Inspinia `Complete Custom Table`. La página hará SSR del estado inicial con `OnGetAsync` y resolverá la eliminación con `OnPostDeleteAsync`; la búsqueda, paginación y el refresco posterior a eliminar se harán por recarga GET para mantener el patrón actual del proyecto y simplificar pruebas.

## Architecture Decisions

| Decisión | Alternativas consideradas | Decisión | Rationale |
|---|---|---|---|
| Cliente HTTP del módulo | Llamar `HttpClient` directo desde PageModel | Crear `Integration/Organizacion/IUnidadOrganizativaApiClient` + `UnidadOrganizativaApiClient` | Mantiene PageModels delgados y replica el patrón ya usado por `AuthApiClient`. |
| Forma de interacción | Tabla 100% client-side con fetch/AJAX | SSR con querystring + POST handler para delete | Encaja mejor con Razor Pages, antiforgery nativo y tests existentes con `WebApplicationFactory`. |
| Ordenamiento visible | Extender backend con sort server-side | Ordenar solo `Items` de la página visible | El contrato `GET /consulta` hoy solo ordena por `Codigo` en repositorio y no recibe sort; el diseño explicita esta limitación para no inventar backend. |
| Confirmación de eliminación | `confirm()` nativo del browser | SweetAlert2 cargado como asset del sitio | Cumple spec, mantiene consistencia visual Inspinia y deja reusable el plugin para módulos futuros. |

## Data Flow

### Carga del listado

`GET /organizacion/unidades-organizativas?page=2&search=dir&sort=nombre_desc`
→ `IndexModel.OnGetAsync`
→ `IUnidadOrganizativaApiClient.QueryAsync(query)`
→ `GET /api/v1/unidades-organizativas/consulta?page=2&pageSize=...&search=dir`
→ `PagedResult<UnidadOrganizativaDto>`
→ ordenamiento en memoria de `Items` visibles
→ render Razor.

### Eliminación

Click eliminar → SweetAlert2 confirma → submit `POST ?handler=Delete`
→ `IndexModel.OnPostDeleteAsync(id, page, search, sort)`
→ `DeleteAsync(id)`
→ `204`: `RedirectToPage` con mismos filtros y ajuste de página si quedó vacía.
→ `404/409/5xx`: `TempData`/mensaje visible + recarga manteniendo la fila.

## File Changes

| File | Action | Description |
|---|---|---|
| `src/SGV.Web/Pages/Organizacion/UnidadesOrganizativas/Index.cshtml` | Create | Tabla, buscador, paginador, estado vacío/error, formulario oculto para delete, hooks JS. |
| `src/SGV.Web/Pages/Organizacion/UnidadesOrganizativas/Index.cshtml.cs` | Create | `[Authorize]`, binding de query, carga inicial, ordenamiento visible y handler de eliminación. |
| `src/SGV.Web/Integration/Organizacion/IUnidadOrganizativaApiClient.cs` | Create | Contrato tipado para consulta paginada y eliminación. |
| `src/SGV.Web/Integration/Organizacion/UnidadOrganizativaApiClient.cs` | Create | Consume `/consulta` y `DELETE /{id}`, traduce `404/409` a resultado usable por la UI. |
| `src/SGV.Web/Integration/Organizacion/UnidadOrganizativaListItemViewModel.cs` | Create | View model web con campos listables + metadata de vigencia/padre. |
| `src/SGV.Web/Program.cs` | Modify | Registro DI del cliente del módulo. |
| `src/SGV.Web/Pages/Shared/Partials/_Sidenav.cshtml` | Modify | Agrega entrada `Unidades Organizativas` y estado activo. |
| `src/SGV.Web/package.json` / `src/SGV.Web/plugins.config.js` | Modify | Incorporan SweetAlert2 al pipeline de assets. |
| `tests/SGV.Tests/Web/SgvWebApplicationFactory.cs` | Modify | Permite override del nuevo cliente/handler además de auth. |
| `tests/SGV.Tests/Web/UnidadOrganizativaWebTests.cs` | Create | Casos del listado, navegación y eliminación. |

## Interfaces / Contracts

```csharp
public sealed record UnidadOrganizativaListQuery(int Page, int PageSize, string? Search, string? Sort);
public sealed record UnidadOrganizativaDeleteResult(bool Succeeded, int? StatusCode, string? Code, string? Message);
```

`QueryAsync` devolverá `PagedResult<UnidadOrganizativaDto>`; la UI mapeará a view models y calculará `TotalPages`. `Sort` NO viaja al backend en este corte.

## Testing Strategy

| Layer | What to Test | Approach |
|---|---|---|
| Unit-ish web | Ordenamiento visible, cálculo de página y mapeo de error | Tests del PageModel con cliente fake o respuesta stub. |
| Web integration | redirect anónimo, menú visible, carga OK, vacío, error, delete cancelado/éxito/409 | `WebApplicationFactory` con cliente autenticado y dobles del módulo. La cancelación se valida por ausencia de POST. |
| Asset/build | SweetAlert2 disponible | `bun run build` cuando se implemente el cambio. |

## Migration / Rollout

No migration required. El rollout queda acotado a `SGV.Web`; depende de que `SGV.Api` siga exponiendo `/consulta` y `DELETE`.

## Open Questions

- [ ] Ninguna bloqueante para `sdd-tasks`; el principal límite funcional ya está decidido: ordenamiento solo sobre la página visible.
