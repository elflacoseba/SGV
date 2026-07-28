# Design: Módulo Web de Ocupaciones — wire-types, cliente, Razor Pages y navegación cruzada

> Change: `2026-07-28-web-ocupaciones-issue-208` · Issue: #208 · Modo: **hybrid** (OpenSpec + Engram) · Delivery: stacked-to-main sobre `develop`, 4 slices · Review budget: **400 líneas por slice**
> Espejo: `openspec/changes/archive/2026-07-27-completar-puestos-issue-209/design.md`

## Visión general

El módulo lleva el ciclo CRUD de Ocupaciones a `SGV.Web` respetando la arquitectura vigente: `Dominio` no cambia, `SGV.Contracts` recibe los wire-types y se mantiene **leaf**, `SGV.Aplicacion` migra firmas internas, `SGV.Infraestructura` agrega `QueryAsync` server-side, `SGV.Api` ajusta `GET` (segmento + filtros), y `SGV.Web` gana un cliente tipado + cuatro Razor Pages + dos páginas de navegación cruzada (`PersonaOcupaciones`, `PuestoOcupaciones`).

```
Dominio (Ocupacion, TipoAsignacion)  ── sin cambios
        │
        ▼
Aplicacion (Comandos/Queries)  ── ListAsync(OcupacionListQuery) + QueryAsync
        │
        ▼
Infraestructura (OcupacionRepository.QueryAsync)  ── segmento + filtros + paginación
        │
        ▼
Api (OcupacionesController)  ── GET ?status=&personaId=&puestoId=
        │
        ▼
Contracts (leaf)  ── DTOs + Requests + OcupacionCommandResult{Categoria}
        │
        ▼
Web  ── IOcupacionApiClient + Index/Create/Edit/Details + PersonaOcupaciones + PuestoOcupaciones
```

## Cambios por capa

### Dominio (`SGV.Dominio/Ocupaciones/`)
**Sin cambios.** `Ocupacion` (entidad), `TipoAsignacion` (enum) y `Ocupacion.Actualizar/Finalizar/EliminarLogicamente/Reactivar` ya cumplen las reglas. Los tests `OcupacionTests` siguen verdes.

### Aplicación (`SGV.Aplicacion/Ocupaciones/`)
**Modificar** (sin mover de capa todavía — los call sites propios siguen importando desde acá):

| Archivo | Cambio |
|---|---|
| `OcupacionRequests.cs` | `CrearOcupacionRequest` y `ActualizarOcupacionRequest` mantienen shape. `FinalizarOcupacionRequest` agrega validación cliente `FechaFin >= FechaInicio` (REQ-OCC-FORM-007). |
| `OcupacionServicioComandos.cs` | Reemplaza `OcupacionErrorType` (NotFound/Conflict/Validation) por `ErrorCategoria` en todos los `Failure`. `OcupacionError` agrega `Categoria: ErrorCategoria`. Compat: `ErrorCategoriaMappers.ToTipoOcupacion` y `ToCategoria` se agregan a `SGV.Contracts/Comun/ErrorCategoriaMappers.cs` para preservar los enums legacy eliminados al cierre del change. |
| `IOcupacionServicioComandos.cs` / `OcupacionServicioComandos.cs` | `CrearAsync` / `ActualizarAsync` siguen con `OcupacionCommandResult`; los códigos funcionales (`PersonaYPuestoOcupados`, `PuestoOcupado`, `OcupacionNoEditable`, `OcupacionYaActiva`, `DatosInvalidos`, `PersonaNoEncontrada`, `PersonaInactiva`, `PuestoNoEncontrado`, `PuestoInactivo`) se preservan en `OcupacionError.Code`. |
| `IOcupacionServicioConsulta.cs` / `OcupacionServicioConsulta.cs` | **DEC-1**: `ListAsync(includeHistory, page, pageSize, ct)` se reemplaza por `QueryAsync(OcupacionListQuery ct)` que recibe `OcupacionListQuery` (Contracts) con `Segmento`, `PersonaId?`, `PuestoId?`. `GetByIdAsync` no cambia. |
| `IOcupacionRepository.cs` / `OcupacionRepository.cs` | Reemplaza `ListPagedAsync` / `ListHistoryPagedAsync` por `QueryAsync(OcupacionListQuery) → Task<(IReadOnlyList<Ocupacion>, int)>` server-side con `WHERE` por segmento, `WHERE` por `PersonaId?` / `PuestoId?`, ordenamiento (`FechaInicio DESC` default) y `Skip/Take`. Total antes de Skip. |
| `Consultas/Dtos/OcupacionDto.cs` | Se **mueve** a `SGV.Contracts/Ocupaciones/Consultas/Dtos/OcupacionDto.cs`. El DTO conserva el shape JSON observable (mismo orden de propiedades). Los call sites internos cambian el `using`. `Estado` migra de `string` ("Activo"/"Finalizado"/"Eliminado") a `OcupacionEstado` enum público para que la Web pueda ramificar por estado sin parsear strings. |
| `Comandos/OcupacionCommandResult.cs` | Se **mueve** a `SGV.Contracts/Ocupaciones/Comandos/OcupacionCommandResult.cs` con `Categoria: ErrorCategoria` añadida al `OcupacionError`. `OcupacionErrorType` queda en `SGV.Contracts` **marcado `[Obsolete]`** (source-compat) hasta el archivado. |

### Infraestructura (`SGV.Infraestructura/Persistencia/`)
- `Repositorios/OcupacionRepository.cs::QueryAsync` — espejo de `PuestoRepository.QueryAsync` (archivado): `AsNoTracking().Include(Persona).Include(Puesto)`, `Where` por segmento (`Activas`: `FechaFin == null && !IsDeleted`; `Eliminadas`: `FechaFin != null || IsDeleted`), `Where` opcional por `PersonaId` / `PuestoId`, `OrderByDescending(FechaInicio)`, `Count` antes de `Skip/Take`. `Count > 0` y `TotalCount` reflejan filtros activos.
- `Configuraciones/OcupacionConfiguracion.cs` — **sin cambios**. Los índices `IX_Ocupaciones_(PuestoId|PersonaId)_FechaInicio_FechaFin` y los únicos `ActivePuestoIdUnique` / `ActivePersonaPuestoUnique` ya cubren los filtros. **No se requiere migración aditiva** (riesgo #3 del proposal, mitigado).

### API (`SGV.Api/Controllers/OcupacionesController.cs`)
- `GetAll(includeHistory, page, pageSize, ct)` → `Get(status, page, pageSize, personaId, puestoId, ct)`. `status` se parsea a `OcupacionSegmentoListado` (`"activas"` default, `"eliminadas"` para historial). `personaId`/`puestoId` opcionales `Guid?`. El resto del controller (POST/PUT/PATCH/finalizar/PATCH/reactivar/DELETE) **no cambia** — usa los argumentos `OcupacionCommandResult` que ahora viven en `SGV.Contracts`.

### Contracts (`SGV.Contracts/Ocupaciones/`) — **NUEVO**
Subcarpetas `Consultas/Dtos/`, `Comandos/`, `Enums/`. Wire-types en español para los nombres visibles y en inglés para identificadores:

```csharp
// Enums
public enum OcupacionEstado { Vigente = 0, Finalizada = 1, Eliminada = 2 }
public enum OcupacionSegmentoListado { Activas = 0, Eliminadas = 1 }
public enum OcupacionTipoAsignacion { Permanente = 0, Interina = 1, Temporal = 2 } // espejo de Dominio

// DTO read
public sealed record OcupacionDto(
    Guid Id, Guid PersonaId, string PersonaNombre,
    Guid PuestoId, string PuestoNombre,
    DateOnly FechaInicio, DateOnly? FechaFin,
    OcupacionTipoAsignacion TipoAsignacion,
    string? Observaciones, OcupacionEstado Estado);

// Query
public sealed record OcupacionListQuery(
    int Page, int PageSize, string? Search, string? Sort,
    OcupacionSegmentoListado Segmento = OcupacionSegmentoListado.Activas,
    Guid? PersonaId = null, Guid? PuestoId = null);

// Requests sin cambios de shape (Provider=Occupaciones)
public sealed record CrearOcupacionRequest(Guid PersonaId, Guid PuestoId, DateOnly FechaInicio, OcupacionTipoAsignacion TipoAsignacion, string? Observaciones = null);
public sealed record ActualizarOcupacionRequest(...);
public sealed record FinalizarOcupacionRequest(DateOnly FechaFin, string? Observaciones = null);

// CommandResult vive acá
public sealed record OcupacionError(string Code, string Message, ErrorCategoria Categoria);
public sealed record OcupacionCommandResult(
    bool IsSuccess, OcupacionDto? Value, OcupacionError? Error,
    IReadOnlyDictionary<string, string[]>? FieldErrors = null);

// Constantes de ruta
public static class OcupacionApiRoutes
{
    public const string Base = "/api/v1/ocupaciones";
    public const string ById = "/{id:guid}";
    public const string Finalize = "/{id:guid}/finalizar";
    public const string Reactivate = "/{id:guid}/reactivar";
}
```

`SGV.Contracts.csproj` no agrega dependencias. Sigue siendo **leaf**.

### Web Integration (`SGV.Web/Integration/Ocupaciones/`) — **NUEVO**
- `IOcupacionApiClient.cs` — métodos: `ListarAsync(OcupacionListQuery, ct)`, `ObtenerPorIdAsync(Guid, ct)`, `CrearAsync(CrearOcupacionRequest, ct)`, `ActualizarAsync(Guid, ActualizarOcupacionRequest, ct)`, `FinalizarAsync(Guid, FinalizarOcupacionRequest, ct)`, `EliminarAsync(Guid, ct)`, `ReactivarAsync(Guid, ct)`. Todos retornan `OcupacionCommandResult` salvo `ListarAsync` (`PagedResult<OcupacionDto>`) y `ObtenerPorIdAsync` (`OcupacionDto?`).
- `OcupacionApiClient.cs` — `HttpClient` tipado, base `OcupacionApiRoutes.Base`, `BuildQueryUri` con `StringBuilder` + `Uri.EscapeDataString` (espejo `PuestosApiClient`). `ToCommandResultAsync` reutiliza `ApiProblemReader` + `CommandResultMapper.Map`. `MapCategoriaToLegacyType` reducido a `NotFound/Conflict/Validation` (compat con `OcupacionErrorType`).
- ViewModels: `OcupacionListItemViewModel` (Id, PersonaId, PersonaNombre, PuestoId, PuestoNombre, Fechas, Tipo, Observaciones, Estado, EsVigente), `OcupacionInputModel` (PersonaId, PuestoId, FechaInicio, TipoAsignacion, Observaciones; `[Required]`/`[StringLength]`), `OcupacionDetailsViewModel` (DTO + flags `EsVigente`, `EsAdministrador`).

### Web Pages (`SGV.Web/Pages/Organizacion/Ocupaciones/`) — **NUEVO**
- `Index.cshtml/cs` — `[Authorize]`. `OnGetAsync(p, search, sort, status, personaId?, puestoId?, ct)` normaliza segmento vía `OcupacionSegmentoListado`, delega a `IOcupacionApiClient.ListarAsync(OcupacionListQuery)`, renderiza tabla con grilla, paginación (`BuildPagedRouteValues`), sort (`GetSortRoute`), toggle Activas/Eliminadas (`BuildToggleSegmentoRouteValues`), acciones Ver/Editar/Eliminar/Reactivar gated por `EsAdministrador` y `Estado`. PRG con `PageFeedback` + `TempData`.
- `Create.cshtml/cs` — `[Authorize(Roles=Administrador)]`. Carga `PersonaOptions` + `PuestoOptions` (catálogos existentes vía `IPersonaApiClient`/`IPuestosApiClient`); pre-carga `PersonaId`/`PuestoId` desde query. POST → `CrearAsync`. Mapea 409 (`PersonaYPuestoOcupados`/`PuestoOcupado`) a `ModelState` por campo. 400 → `FieldErrors`. PRG al Index.
- `Edit.cshtml/cs` — gate Admin + `EsVigente`. Solo `PersonaId`, `PuestoId`, `FechaInicio`, `TipoAsignacion`, `Observaciones` (espejo de Create). 409 si el Puesto cambió a ocupado por otro.
- `Details.cshtml/cs` — `[Authorize]`. `OnGetAsync(id, ct)`. Acciones `OnPostFinalizarAsync`, `OnPostEliminarAsync`, `OnPostReactivarAsync` (PRG). SweetAlert2 para confirmación (paridad Puesto.Details).
- `_Form.cshtml` — partial compartido con `asp-for` bindando contra `OcupacionInputModel`. Selects de `Persona` y `Puesto`.

### Web Pages — Navegación cruzada — **NUEVO**
- `Pages/Personas/PersonaOcupaciones.cshtml/cs` — `[Authorize]` aceptando no-admin (paridad `PersonaHabilidades`). `OnGetAsync(personaId, ct)` verifica `persona.IsActive`, construye `OcupacionListQuery(PersonaId=personaId, Segmento=Activas)`, renderiza tabla sin toggle. Botón "Nueva ocupación" gated por Admin con `?personaId=` query para que `Create` lo precargue. Botón "Volver" → `/personas/detalles/{id}`.
- `Pages/Organizacion/Puestos/PuestoOcupaciones.cshtml/cs` — espejo para `Puesto.Details`. Filtro fijo `PuestoId`, `Segmento=Activas`.

Las páginas `Personas/Details.cshtml` y `Puestos/Details.cshtml` suman un botón "Ver ocupaciones" cuando `Persona.IsActive` / `Puesto.IsActive`, con `asp-page` links.

## Patrón del cliente HTTP

Espejo de `IPuestosApiClient` + `web-apiclient-transport-contract`:

```csharp
public interface IOcupacionApiClient
{
    Task<PagedResult<OcupacionDto>> ListarAsync(OcupacionListQuery query, CancellationToken ct = default);
    Task<OcupacionDto?> ObtenerPorIdAsync(Guid id, CancellationToken ct = default);
    Task<OcupacionCommandResult> CrearAsync(CrearOcupacionRequest request, CancellationToken ct = default);
    Task<OcupacionCommandResult> ActualizarAsync(Guid id, ActualizarOcupacionRequest request, CancellationToken ct = default);
    Task<OcupacionCommandResult> FinalizarAsync(Guid id, FinalizarOcupacionRequest request, CancellationToken ct = default);
    Task<OcupacionCommandResult> EliminarAsync(Guid id, CancellationToken ct = default);
    Task<OcupacionCommandResult> ReactivarAsync(Guid id, CancellationToken ct = default);
}
```

DI en `Program.cs`:

```csharp
builder.Services.AddHttpClient<IOcupacionApiClient, OcupacionApiClient>((sp, client) =>
{
    var options = sp.GetRequiredService<IOptions<SgvApiOptions>>().Value;
    client.BaseAddress = new Uri(options.BaseUrl, UriKind.Absolute);
    client.Timeout = TimeSpan.FromSeconds(10);
})
.AddHttpMessageHandler(sp => sp.GetRequiredService<ApiBearerTokenHandler>());
```

## Manejo de errores

| Origen | HTTP | `ErrorCategoria` | UX (`PageFeedback` + `ErrorCategoryMapper`) |
|---|---|---|---|
| BadRequest / `ValidationProblemDetails` | 400/422 | `Validation` | `ModelState` por campo (ej. `CodigoDuplicado`/`PersonaYPuestoOcupados`/`PuestoOcupado`); mensaje funcional para campos sin map directo. |
| Sin bearer | 401 | `Unauthorized` | `authRedirector.TryRedirectToLogin(Request.Path)`; fallback `PageFeedback.UnauthorizedMessage`. |
| Autenticado sin rol Admin (write) | 403 | `Forbidden` | `Forbid()` (PageModel ya retorna `Forbid`); mensaje `PageFeedback.ForbiddenMessage` para 403 del API. |
| Id inexistente | 404 | `NotFound` | `PageFeedback.NotFoundDeleteMessage`; "El recurso ya no está disponible." para Details. |
| Colisión (`PersonaYPuestoOcupados`, `PuestoOcupado`, `OcupacionNoEditable`, `OcupacionYaActiva`) | 409 | `Conflict` | Sin falsa éxito; feedback con `Code` (badge TempData). Create no limpia input. |
| `HttpRequestException` / `TaskCanceledException` / `JsonException` | — | `Transport` (via `TransportFailureClassifier`) | `LoadErrorMessage` para `Index`; `ErrorMessage = PageFeedback.TransportMessage` para forms; `PageFeedback.SetDanger(TempData, ...)` tras PRG. |
| Otro status no exitoso | 3xx/4xx no listado | `Unexpected` | `PageFeedback.UnexpectedMessage`; `TempData["ErrorCode"]` con `code` del backend. |

## Paginación y filtrado server-side

Query string final: `GET /api/v1/ocupaciones?status={activas|eliminadas}&page={n}&pageSize={20}&search={q}&sort={expr}&personaId={guid}&puestoId={guid}`. Defaults: `status=activas`, `page=1`, `pageSize=20`. `PagedResult<OcupacionDto>` (existente en `SGV.Contracts/Organizacion/Consultas/Dtos/PagedResult.cs` — reusar ese tipo, no crear `OcupacionPagedResult`). `Index` compone vía `OcupacionListQuery` (Contracts). La página cruzada (`PersonaOcupaciones`/`PuestoOcupaciones`) **fija** `Segmento=Activas` y `PersonaId` o `PuestoId`; ignora `status` externo (R-OCC-NAV-004).

## Navegación cruzada y preservación de contexto

- `PersonaOcupaciones` y `PuestoOcupaciones` se enlazan desde `Personas/Details.cshtml` y `Puestos/Details.cshtml` sólo cuando la entidad está activa. El enlace usa `asp-page` con `asp-route-id`.
- `ReturnUrl` se transporta como query string (`?returnPersonaId=...` / `?returnPuestoId=...`) — el botón "Volver" siempre va al `Details` dueño.
- `ReturnNavigationContext` ya cubre `page/search/sort/returnStatus`; las páginas cruzadas lo reusan para que el listado de origen recuerde su segmento.
- `Create` desde página cruzada: query `?personaId={guid}` o `?puestoId={guid}` pre-carga el selector del id dueño; el otro selector queda editable. Path `POST /api/v1/ocupaciones` no necesita rate nuevo.

## Cambios en API existentes (breaking)

| Cambio | Breaking | Estrategia |
|---|---|---|
| `?includeHistory` → `?status=activas\|eliminadas` | Sí (wire) | Único cliente conocido: `SGV.Web` (este PR). Actualizar `OcupacionesControllerTests.GetAll_IncludeHistory_ReturnsAllIncludingFinalized` → `GetAll_StatusEliminadas_ReturnsAllIncludingFinalized`. Actualizar `OcupacionServicioConsulta.ListAsync`/`OcupacionRepository.QueryAsync` en mismo Slice 1. Documentar en `docs/decisiones-implementacion.md`. |
| `OcupacionDto` `Estado: string` → `Estado: OcupacionEstado` | Sí (wire) | JSON shape: `Estado` queda `"Vigente"`/`"Finalizada"`/`"Eliminada"` (string). Compat: `OcupacionEstadoHelper.CalcularEstado` queda en `SGV.Aplicacion` mapeando a `OcupacionEstado`; el cliente serializa con `JsonStringEnumConverter` (web default). |
| `OcupacionDto` y `OcupacionCommandResult` migran de `SGV.Aplicacion` a `SGV.Contracts` | Sí (source) | Cambio atómico: archivos movidos, no duplicados. `using` actualizados en `OcupacionServicioComandos`, `OcupacionServicioConsulta`, `OcupacionesController`, `OcupacionServiceComandosTests`, `OcupacionServicioConsultaTests`, `OcupacionesControllerTests`. |
| `IOcupacionServicioConsulta.ListAsync(bool, int, int, ct)` → `QueryAsync(OcupacionListQuery, ct)` | Sí (source) | Mismo PR. Test interno `OcupacionServicioConsultaTests` actualizado. |
| `IOcupacionRepository.ListPagedAsync/ListHistoryPagedAsync` → `QueryAsync(OcupacionListQuery)` | Sí (source) | Mismo PR. `[MySqlFact]` en `OcupacionRepositoryQueryTests` reescrito. |

## Estrategia de tests TDD

| Capa | Alcance | Approach |
|---|---|---|
| Dominio | `Ocupacion.Actualizar/Finalizar/EliminarLogicamente/Reactivar` (transiciones) | `OcupacionTests` (existentes) — verificar que no se rompen. |
| Aplicación | `OcupacionServicioComandos` con errores `PersonaYPuestoOcupados`/`PuestoOcupado`/`OcupacionNoEditable`/`OcupacionYaActiva` | `OcupacionServicioComandosTests` (reforzar). `OcupacionServicioConsultaTests` cubre `QueryAsync` con segmento + filtros. |
| Persistencia | `[MySqlFact]` `OcupacionRepositoryQueryTests` — segmento, filtros PersonaId/PuestoId, sort, paginación, TotalCount filtrado. | `[MySqlFact]`, 4-6 tests. |
| API | `OcupacionesControllerTests` actualizado: `Get_StatusEliminadas_ReturnsAll`, `Get_Default_ReturnsActive`, `Get_PersonaId_Filters`, `Get_PuestoId_Filters`, `Get_PersonaIdPuestoId_ReturnsIntersection`, `Get_Anonymous_Returns401`, `Post/Edit/Finalize/Reactivate/Delete` 201/200/200/200/204, `Post_Conflict_409`, `Post_FieldErrors_400`, `Post_NotFound_404`, `Post_NoAdmin_403`, `Finalize_Finalizada_409`. | `ApiWebApplicationFactory`. |
| Web API client | `OcupacionApiClientTests` + `IOcupacionApiClientContractTests` — `ListarAsync_BuildsUri`, `StatusEliminadas_Serializa`, `MapCategoriaToLegacyType_AllBranches`. | Unit. |
| Web Pages | `OcupacionIndexPageTests`, `OcupacionCreatePageTests`, `OcupacionEditPageTests`, `OcupacionDetailsPageTests`, `PersonaOcupacionesPageTests`, `PuestoOcupacionesPageTests`, `FakeOcupacionApiClientTests` — render, paginación, PRG, errores por campo, 401/403/404/409/timeout, navegación cruzada, preservación de contexto. | `SgvWebApplicationFactory` + `FakeOcupacionApiClient`. |
| Contratos | `OcupacionListQueryTests` (defaults), `OcupacionCommandResultTests` (success/failure/failure-with-fields). | Unit. |
| Compilación | `dotnet build SGV.slnx` con `grep -r "SGV.Aplicacion\|SGV.Api\|SGV.Infraestructura" src/SGV.Web/Integration/Ocupaciones src/SGV.Web/Pages/Organizacion/Ocupaciones` debe seguir retornando cero hits. | Manual en CI. |

Cantidad esperada: ~30-40 tests nuevos, alineado con #209.

## Cambios en `_Sidenav.cshtml`

Insertar un nuevo colapsable en `~/Pages/Shared/Partials/_Sidenav.cshtml` después del bloque `puestos` (línea ~170). Helper local para `ocupacionesGroupActive`/`ListadoActive`/`NuevaActive` (mismo patrón que `puestos`). Ícono `ti ti-history`. Sub-ítems: `Listado` (visible para todo autenticado) y `Nueva` (sólo `esAdministrador`). El item padre OCUPACIONES se muestra a todo autenticado.

## Plan de slices (resumen técnico)

| Slice | Archivos principales | Dependencias | LOC est. | Riesgo de budget |
|---|---|---|---|---|
| **1** Contracts + API extendida | `SGV.Contracts/Ocupaciones/**` (~10 archivos), `OcupacionesController.Get`, `OcupacionServicioConsulta.QueryAsync`, `OcupacionRepository.QueryAsync`, `OcupacionConfiguracion` (sin cambios), `OcupacionesControllerTests` actualizado, `OcupacionListQueryTests`, `OcupacionCommandResultTests`, `OcupacionServicioConsultaTests` actualizado, `OcupacionRepositoryQueryTests` (`[MySqlFact]`). | Tests API existentes | ~250 | Bajo |
| **2** Cliente Web + Listado | `IOcupacionApiClient`, `OcupacionApiClient`, `OcupacionListItemViewModel`, `OcupacionInputModel`, `OcupacionDetailsViewModel`, `Pages/Organizacion/Ocupaciones/Index.{cshtml,cshtml.cs}`, `Program.cs` (registro DI), `_Sidenav.cshtml`, `FakeOcupacionApiClient`, `OcupacionApiClientTests`, `OcupacionIndexPageTests`, `IOcupacionApiClientContractTests`. | Slice 1 mergeado | ~280 | Bajo |
| **3a** Formularios CRUD | `Pages/Organizacion/Ocupaciones/{Create,Edit,Details}.{cshtml,cshtml.cs}` + `_Form.cshtml`, `OcupacionCreatePageTests`, `OcupacionEditPageTests`, `OcupacionDetailsPageTests`, `OcupacionCommandResultTests` (extensión). | Slice 2 mergeado | ~390 | **Medio** — subdivisión preventiva 3a-Form (Create+Edit) / 3a-Details sólo si excede 380. |
| **3b** Navegación cruzada | `Pages/Personas/PersonaOcupaciones.{cshtml,cshtml.cs}`, `Pages/Organizacion/Puestos/PuestoOcupaciones.{cshtml,cshtml.cs}`, links en `Personas/Details.cshtml` + `Puestos/Details.cshtml`, `PersonaOcupacionesPageTests`, `PuestoOcupacionesPageTests`. | Slice 2 mergeado (paralelo a 3a) | ~200 | Bajo |

## Catálogos y dependencias

`SGV.Web` consume **exclusivamente** `SGV.Contracts` (no tocar `SGV.Web.csproj`). Los catálogos `Persona` y `Puesto` que alimentan los `<select>` de Create/Detail vienen de los clientes vigentes (`IPersonaApiClient.GetAllAsync` / `IPuestosApiClient.GetAllAsync`). Sin nuevos NuGet.

## Compatibilidad

- **Sin migraciones de BD.** Los índices `IX_Ocupaciones_(PuestoId|PersonaId)_FechaInicio_FechaFin` y los únicos `ActivePuestoIdUnique` / `ActivePersonaPuestoUnique` cubren los `Where` de `QueryAsync`. Si `SHOW INDEX FROM Ocupaciones` muestra gap, se agrega índice compuesto en una migración aditiva idéntica a la decisión del archivado #209 (mitigación documentada).
- **Sin breaking en columna `Estado`**: `OcupacionEstado` se serializa como string (`JsonStringEnumConverter` por default). Wire observable: `"Vigente"` / `"Finalizada"` / `"Eliminada"`.
- **API authorization preservada**: `[Authorize]` en el controller, `[Authorize(Roles=Administrador)]` en todos los writes. Tests 401/403 confirman.

## Tests de integración con MySQL

`MySqlFactAttribute` y `TestSgvDbContextFactory` ya implementados (riesgo #6 del proposal). Los tests `[MySqlFact]` de `OcupacionRepositoryQueryTests` siguen el patrón vigente: `Database.Migrate()` idempotente, skip limpio sin MySQL local.

## Riesgos residuales

- **R1 (Preservado):** Slice 3a cerca de 400 → subdivisión 3a-Form / 3a-Details si excede 380 líneas antes del PR. NO se reduce alcance.
- **R2 (Nuevo):** `OcupacionEstado` como `enum` en `OcupacionDto` requiere `JsonStringEnumConverter` consistente con `OcupacionTipoAsignacion`. Verificar que `OcupacionApiClient` (System.Net.Http.Json) serializa enums como string por default; si no, agregar `JsonOptions` con `JsonStringEnumConverter`. Riesgo bajo (mismo patrón que `PuestoDto` con `Guid?`).
- **R3 (Nuevo):** `OcupacionCommandResult.Failure(OcupacionError)` con `Categoria` debe ramificar por `Categoria` y NO por `Code` en el cliente real. El `MapCategoriaToLegacyType` queda como compat con `OcupacionErrorType` legacy. Cubierto por `OcupacionApiClientTests.AllBranches`.
- **R4 (Nuevo):** `PersonaOcupaciones` y `PuestoOcupaciones` no muestran el toggle Eliminadas. Verificar en HTML renderizado (test inspecciona `I.HtmlDocument` y no encuentra control de toggle). Cubierto por `PersonaOcupacionesPageTests.NoToggleEliminadas`.
- **R5 (Nuevo):** `PersonaOcupaciones` debe gatear `Persona.IsActive` antes de listar; `PuestoOcupaciones` igual con `Puesto.IsActive`. Cubierto por tests `PersonaInactiva_RedirectsToNotFound` / `PuestoInactivo_RedirectsToNotFound`.
- **R6 (Migración):** Sin migraciones aditivas. Confirmar en `verify-report.md` con `SHOW INDEX FROM Ocupaciones`.
- **R7 (Tests):** `SgvWebApplicationFactory` registra `FakeOcupacionApiClient` vía `ConfigureTestServices` (paridad `FakePuestosApiClient`). Documentado en `tests/SGV.Tests/Web/_Shared/`.

## Plan de delivery (stacked-to-main)

Mismo patrón que #209: cada PR targeta `develop`, se mergea en orden, y el cierre de #208 ocurre al mergear el último. Pr-1 backend (Slice 1), PR-2 cliente+listado (Slice 2), PR-3a forms (Slice 3a) y PR-3b cross-nav (Slice 3b) pueden mergearse en cualquier orden una vez Slice 2 esté en `develop`. Los commits responden a `work-unit-commits` (TDD strict: RED → GREEN → REFACTOR por capa).

## Referencias

- `openspec/changes/2026-07-28-web-ocupaciones-issue-208/proposal.md` (decisiones locked).
- `openspec/changes/2026-07-28-web-ocupaciones-issue-208/specs/web-ocupaciones-contrato-api/spec.md` (REQ-OCC-API-001..006).
- `openspec/changes/2026-07-28-web-ocupaciones-issue-208/specs/web-ocupaciones-listado/spec.md` (REQ-OCC-LST-001..006).
- `openspec/changes/2026-07-28-web-ocupaciones-issue-208/specs/web-ocupaciones-crear-editar/spec.md` (REQ-OCC-FORM-001..008).
- `openspec/changes/2026-07-28-web-ocupaciones-issue-208/specs/web-ocupaciones-navegacion-contextual/spec.md` (REQ-OCC-NAV-001..006).
- `openspec/specs/web-apiclient-transport-contract/spec.md` (contrato transversal cliente HTTP).
- `openspec/changes/archive/2026-07-27-completar-puestos-issue-209/design.md` (espejo).
- `openspec/changes/archive/2026-07-10-extraer-contratos-sgv/` (precedente `SGV.Contracts` leaf).
- Memorias Engram: `sdd-preflight-issue-208` (#1461), `issue-208-explore-state` (#1462), `architecture/sdd-issue-208-proposal` (#1463), `sdd/2026-07-28-web-ocupaciones-issue-208/spec` (#1464).
- Issue #208: https://github.com/elflacoseba/SGV/issues/208.
- `docs/decisiones-implementacion.md` § "Mapa de bloques GUID" + § "Gestión de secretos JWT" (§ deuda #125 — `OcupacionCommandResult` migrada a `ErrorCategoria` en este PR).
- Tests espejo: `tests/SGV.Tests/Web/Puesto/{PuestoIndexPageTests,PuestoCreatePageTests,PuestoEditPageTests,PuestoDetailsPageTests,FakePuestosApiClient}.cs` y `tests/SGV.Tests/Web/Cargo/FakeCargoApiClient.cs`.
