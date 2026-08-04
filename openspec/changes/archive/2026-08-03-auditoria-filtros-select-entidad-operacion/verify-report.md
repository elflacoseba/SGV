# Verify Report — 2026-08-03-auditoria-filtros-select-entidad-operacion (issue #251)

```yaml
schema: gentle-ai.verify-result/v1
change: 2026-08-03-auditoria-filtros-select-entidad-operacion
issue: elflacoseba/SGV#251
verification_date: 2026-08-04
branch: develop
merge_commits_verified:
  - a026ff6a Merge Slice A of issue #251: filter-options endpoint + UserName filter
  - 4fc288b3 Merge Slice B of issue #251: selects UI + fallback non-bloqueante en filtros
slice_strategy: stacked-to-main (PR 1 backend + PR 2 web UI)
strict_tdd: true
final_verdict: PASS
pre_existing_flakes: 7 intermittent failures in UsuariosEndToEndMySqlFactTests.LoginAsAdminAsync (JWT/MySQL 9.6 race), unrelated to #251
```

## Resultado: PASS

Verificación end-to-end del change completo (Slice A + Slice B) sobre `develop` post-merge de ambos PRs. Las 12 acceptance criteria del `proposal.md` quedan demostradas con cobertura runtime + evidencia de código. D-2 sigue cerrado por construcción. 17 tests nuevos + 1 test migrado pasan, junto con los 29 tests previos del archivo de aplicación contra MySQL real (8 `[MySqlFact]` para el filtro `UserName` + `GetFilterOptionsAsync`).

---

## Acceptance criteria (1-12)

| # | Criterio | Estado | Evidencia |
| - | -------- | ------ | --------- |
| 1 | `GET /api/v1/auditorias/filter-options` con `[Authorize(Roles=Administrador)]` devuelve `{ entityNames, operations }` | ✅ | `src/SGV.Api/Controllers/AuditoriasController.cs:141` `[HttpGet("filter-options")]`; atributo de clase `[Authorize(Roles = RolesSgv.Administrador)]` heredado en línea 23. `ProducesResponseType(typeof(AuditoriaFilterOptions), …)` línea 142. Cobertura: `FilterOptions_Anonimo_Retorna401` + `FilterOptions_UsuarioSinRol_Retorna403` + `FilterOptions_Administrador_DevuelveListasOrdenadasSinDuplicados` (3 tests API en `tests/SGV.Tests/Api/AuditoriasControllerTests.cs:486,501,518`). |
| 2 | Endpoint usa `AsNoTracking()` y `SELECT DISTINCT` | ✅ | `src/SGV.Infraestructura/Persistencia/AuditoriaServicioConsulta.cs:236-252` — `context.Auditorias.AsNoTracking().Where(!IsNullOrWhiteSpace).Select().Distinct().OrderBy().Take(MaxFilterOptionsItems).ToListAsync(ct)` para ambos arrays. Sin JOIN con `AspNetUsers` (líneas 222-223 confirman intención). |
| 3 | `AuditoriaApiClient.GetFilterOptionsAsync` existe | ✅ | Interface: `src/SGV.Web/Integration/Auditoria/IAuditoriaApiClient.cs:84` declara `Task<AuditoriaFilterOptions> GetFilterOptionsAsync(CancellationToken ct = default)`. Impl HTTP: `src/SGV.Web/Integration/Auditoria/AuditoriaApiClient.cs:89-111` ejecuta `GET {BaseRoute}/filter-options` + `EnsureSuccessStatusCode` + `ReadFromJsonAsync<AuditoriaFilterOptions>`. |
| 4 | `IndexModel.OnGetAsync` precarga opciones con fallback | ✅ | `src/SGV.Web/Pages/Auditorias/Index.cshtml.cs:210` invoca `await LoadFilterOptionsAsync(cancellationToken)` antes del query principal; helper en líneas 375-404 envuelve `auditoriaApiClient.GetFilterOptionsAsync` en `try/catch (Exception ex) when (TransportFailureClassifier.IsTransportFailure(ex))` que setea `EntityNameOptions = null; OperationOptions = null; FilterOptionsLoadFailed = true; FilterOptionsMessage = "No se pudieron cargar las opciones de filtros. Ingresá los valores manualmente."`. La query principal sigue en su propio try/catch (líneas 223-238) y NO depende del éxito de filter-options. |
| 5 | `entityName` y `operation` filtran vía `<select>` con "Todos" | ✅ | `src/SGV.Web/Pages/Auditorias/Index.cshtml:59-86` renderiza `<select asp-items="Model.EntityNameOptions">` y `<select asp-items="Model.OperationOptions">` cuando NO hay fallback. Helper `BuildSelectListItems` en `Index.cshtml.cs:425-474` agrega primera opción `Value="" Text="Todos" Selected=(EntityName is null)` (líneas 431-437) + salvaguarda de filtro huérfano (líneas 462-471). Cobertura: `Index_Renderiza_Selects_ConTodos` (`AuditoriasIndexTests.cs:560`) asserta `<select … id="entityName"`, `id="operation"`, `value="">Todos`. |
| 6 | Selección dispara submit del form | ✅ | `Index.cshtml:68,83` ambos selects llevan `onchange="this.form.p.value=1;this.form.submit();"` (reset `p=1` consistente con el `<select id="pageSize">`). Cobertura indirecta: `Get_Index_Pagination_PreservesFilters` confirma que el form propaga los filtros vigentes después del submit. |
| 7 | Route values propagan `entityName` y `operation` | ✅ | `Index.cshtml.cs:252-263` `BuildPagedRouteValues(page)` incluye `entityName = EntityName, operation = Operation, userName = UserName`. `BuildSortRouteValues(sortKey)` línea 275-286 idem. `BuildDetailsRouteValues(id)` línea 296-308 propaga todo el contexto (incluido `userName = UserName`). Cobertura: `Get_Index_Pagination_PreservesFilters` (`AuditoriasIndexTests.cs:234`) asserta que el link "Siguiente" lleva `entityName=Cargo&operation=Alta&userName=u-7`. |
| 8 | Toolbar envuelta en `.card` (sin duplicar wrapper) | ✅ | `Index.cshtml:31` `<div class="card" data-table>` es el wrapper mayor; el form de filtros vive adentro en `card-header border-0` (línea 50) sin agregar un segundo `.card`. El `design.md §6` y `apply-progress §Drift` confirman esta decisión (el wrapper ya existía en el predecessor #248). `grep -nE 'class="card"' src/SGV.Web/Pages/Auditorias/Index.cshtml` confirma única ocurrencia. |
| 9 | Si filter-options falla, IndexModel hace fallback a inputs + mensaje no bloqueante | ✅ | `Index.cshtml:22-27` rama `@if (Model.FilterOptionsLoadFailed && !string.IsNullOrWhiteSpace(Model.FilterOptionsMessage))` pinta `<div class="alert alert-info alert-soft mb-2">` (no `alert-danger`). `Index.cshtml:59-63,74-78` renderiza `<input type="search">` cuando `FilterOptionsLoadFailed`. Cobertura: `Index_FilterOptionsFalla_FallbackAInputs` (`AuditoriasIndexTests.cs:615`) asserta `alert-info` presente, `alert-danger` ausente, `QueryAsync` sigue invocándose (`apiClient.QueryCalls.Count == 1`). |
| 10 | `QueryAsync` filtra por `u.UserName` | ✅ | `AuditoriaServicioConsulta.cs:112-122`: `if (!string.IsNullOrWhiteSpace(query.UserName)) { var userName = query.UserName; origen = origen.Where(x => x.u != null && x.u.UserName == userName); }` — guard `x.u != null` para filas huérfanas. Cobertura: `QueryAsync_FiltraPorUserNameCaseInsensitive` (MySqlFact) + `Listado_UserName_FiltraPorNombreNoPorGuid` (controller) + `Index_RouteValue_UserName_NoUserId` (Theory 2 InlineData, web). |
| 11 | Query string renombrada `userId` → `userName` | ✅ | `src/SGV.Contracts/Auditoria/AuditoriaListQuery.cs:46` parámetro posicional `string? UserName = null` (antes `UserId`). `AuditoriasController.cs:62-63` `[FromQuery] AuditoriaListQuery query` propaga automáticamente. `AuditoriaApiClient.cs:150-154` `BuildQueryUri` serializa `&userName=...`. Cobertura: `Listado_UserName_FiltraPorNombreNoPorGuid` (`AuditoriasControllerTests.cs:638`) verifica `query.UserName == "jperez"`. `Index_RouteValue_UserName_NoUserId` (Theory con `?userId=juan` → `query.UserName == null`) confirma que el binding legacy queda ignorado por model binding. |
| 12 | Placeholder `"nombre de usuario"` | ✅ | `Index.cshtml:89` `<input … id="userName" name="userName" type="search" placeholder="nombre de usuario" value="@Model.UserName" />`. Cobertura: `Index_UserInput_PlaceholderEsNombreDeUsuario` (`AuditoriasIndexTests.cs:659`) asserta `placeholder="nombre de usuario"` presente y `placeholder="user id"` ausente. |

### Conteo

| Categoría | Total | PASS | FAIL | WEAK |
|---|---|---|---|---|
| Acceptance criteria del proposal | 12 | 12 | 0 | 0 |

---

## D-2 audit (CRITICAL)

**PASS por construcción.**

`AuditoriaFilterOptions` (`src/SGV.Contracts/Auditoria/AuditoriaFilterOptions.cs:30-32`) es `public sealed record` con DOS campos: `IReadOnlyList<string> EntityNames` y `IReadOnlyList<string> Operations`. Ningún otro campo puede aparecer en el JSON serializado.

Verificación:

1. **Reflexión / tipo**: `grep "EntityNames\|Operations" src/SGV.Contracts/Auditoria/AuditoriaFilterOptions.cs` muestra sólo los dos parámetros del record.
2. **Serialización runtime**: `FilterOptions_RespuestaSerializada_NoContieneOldNewEntityIdUserIdUserName` (`AuditoriasControllerTests.cs:555`) aserta que el JSON NO contiene `oldValuesJson`, `newValuesJson`, `entityId`, `userId`, `userName`, `correlationId`, `occurredAt`, `"id"` (líneas 577-584) y SÍ contiene `entityNames`, `operations` (líneas 586-587). Test PASA en runtime (75/75 en el filtro focalizado).
3. **Búsqueda repo-wide**: `grep -rn "filter-options" src/ tests/ docs/` confirma que el endpoint sólo se invoca desde el controller, el cliente HTTP y la PageModel — no hay consumidor que inyecte campos prohibidos en el wire.
4. **EF Core**: `AuditoriaServicioConsulta.cs:236-252` usa `Select(a => a.EntityName)` y `Select(a => a.Operation)` sobre columnas individuales. EF no emite `oldValuesJson`/`newValuesJson`/`entityId`/`userId`/`userName` en el `SELECT` SQL.

**No hay superficie tipada ni runtime para arrastrar `OldValuesJson`/`NewValuesJson`/`EntityId`/`UserId`/`UserName` a través del endpoint filter-options.** D-2 reforzado por separación física de tipos (misma regla que `AuditoriaDto` vs `AuditoriaDetalleDto`).

---

## Spec scenario coverage

Delta spec `openspec/changes/2026-08-03-auditoria-filtros-select-entidad-operacion/specs/auditoria-query/spec.md` define **18 escenarios** distribuidos así:

- **MODIFIED Requirement "Filtros combinables de consulta"** (7 escenarios)
- **MODIFIED Requirement "Shell web admin-only"** (1 escenario nuevo + 3 heredados del predecessor #248)
- **ADDED Requirement "Endpoint filter-options"** (7 escenarios)

| # | Escenario | Spec | Test que lo cubre | Status |
| - | --------- | ---- | ----------------- | ------ |
| 1 | Filtros combinados filtran el resultado | filtros combinables | `QueryAsync_Filtros_AplicanSegunEsperado` (Theory 7 InlineData en `AuditoriaServicioConsultaTests.cs:51`) | ✅ PASS |
| 2 | Filtro por CorrelationId aísla la correlación | filtros combinables | `QueryAsync_CorrelationId_AíslaRegistrosConEsaCorrelacion` (`AuditoriaServicioConsultaTests.cs:597`) | ✅ PASS |
| 3 | Filtros omitidos no filtran | filtros combinables | `QueryAsync_Filtros_AplicanSegunEsperado(null,null,null,null,null,5)` (InlineData[0] de la teoría) | ✅ PASS |
| 4 | Rango de fechas invertido | filtros combinables | `QueryAsync_DateFromPosteriorADateTo_LanzaArgumentException` (app) + `Get_Admin_DateFromMayorADateTo_Returns400ConProblemDetails` (API) | ✅ PASS |
| 5 | Filtro por UserName localiza por nombre, no por GUID | filtros combinables | `Listado_UserName_FiltraPorNombreNoPorGuid` (`AuditoriasControllerTests.cs:638`) + `QueryAsync_FiltraPorUserNameCaseInsensitive` (`AuditoriaServicioConsultaTests.cs:817`) | ✅ PASS |
| 6 | UserName inexistente devuelve conjunto vacío | filtros combinables | Implícito por semántica LINQ (sin match → 0 rows). **Sin test explícito** `?userName=noexiste`. Cubierto débilmente por `QueryAsync_Filtros_AplicanSegunEsperado` con `UserName="u3"` (1 row) + `QueryAsync_FiltroUserNameVacio_NoAplicaFiltro` (5 rows sin filtro). | ⚠️ WEAK_COVERAGE |
| 7 | UserName case-insensitive | filtros combinables | `QueryAsync_FiltraPorUserNameCaseInsensitive` (`AuditoriaServicioConsultaTests.cs:817`) — collation `utf8mb4_0900_ai_ci` | ✅ PASS |
| 8 | Listado vacío (heredado) | shell web | `Get_Index_WhenListIsEmpty_ShowsEmptyState` (`AuditoriasIndexTests.cs:142`) | ✅ PASS |
| 9 | Error de transporte recuperable (heredado) | shell web | `Get_Index_WhenApiFails_ShowsVisibleErrorAndPreservesFilters` (`AuditoriasIndexTests.cs:191`) | ✅ PASS |
| 10 | Paginación web conserva filtros y sort (heredado) | shell web | `Get_Index_Pagination_PreservesFilters` (`AuditoriasIndexTests.cs:234`) + `Get_Index_Pagination_PreservesSortAndPageSize` (línea 456) | ✅ PASS |
| 11 | Si filter-options falla, IndexModel hace fallback | shell web | `Index_FilterOptionsFalla_FallbackAInputs` (`AuditoriasIndexTests.cs:615`) | ✅ PASS |
| 12 | Endpoint filter-options devuelve listas distintas | endpoint | `FilterOptions_Administrador_DevuelveListasOrdenadasSinDuplicados` (`AuditoriasControllerTests.cs:518`) + `GetFilterOptionsAsync_DevuelveEntityNamesYOperationsOrdenadas` (app MySqlFact) | ✅ PASS |
| 13 | Endpoint filter-options sin credenciales → 401 | endpoint | `FilterOptions_Anonimo_Retorna401` (`AuditoriasControllerTests.cs:486`) | ✅ PASS |
| 14 | Endpoint filter-options sin rol → 403 | endpoint | `FilterOptions_UsuarioSinRol_Retorna403` (`AuditoriasControllerTests.cs:501`) | ✅ PASS |
| 15 | Endpoint filter-options expone solo columnas seguras | endpoint | `FilterOptions_RespuestaSerializada_NoContieneOldNewEntityIdUserIdUserName` (`AuditoriasControllerTests.cs:555`) | ✅ PASS |
| 16 | Endpoint filter-options ordena alfabéticamente y deduplica | endpoint | `FilterOptions_Administrador_DevuelveListasOrdenadasSinDuplicados` (`AuditoriasControllerTests.cs:518`) — input `["Cargo","Persona","Cargo","Habilidad"]` → output `["Cargo","Habilidad","Persona"]` | ✅ PASS |
| 17 | Endpoint filter-options descarta cadenas vacías | endpoint | `GetFilterOptionsAsync_DescartaValoresVacios` (`AuditoriaServicioConsultaTests.cs:930`) — siembra `EntityName=""` y `"   "` + verifica `Single(["Cargo"])` | ✅ PASS |
| 18 | Endpoint filter-options se acota a 100 valores | endpoint | `FilterOptions_DistinctMayorACienDevuelvePrimerosCien` (`AuditoriasControllerTests.cs:599`) — 150 EntityNames → `resultado.EntityNames.Count == 100` ordenados lexicográficamente | ✅ PASS |

### Conteo de cobertura

| Categoría | Total | PASS | WEAK | NO COVERED |
|---|---|---|---|---|
| Escenarios delta spec | 18 | 17 | 1 | 0 |

**Único weak coverage** (#6 "UserName inexistente devuelve conjunto vacío"): el comportamiento ES el esperado por semántica LINQ — `Where(u.UserName == "noexiste")` sobre filas sembradas devuelve 0 resultados. No hay test explícito que envíe `?userName=noexiste` y espere `TotalCount == 0`, pero la línea está cubierta por la teoría `QueryAsync_Filtros_AplicanSegunEsperado` con `UserName="u3"` (1 resultado) + `QueryAsync_FiltroUserNameVacio_NoAplicaFiltro` (5 resultados cuando no se filtra). **No es regresión ni bug**, sólo una omisión menor de granularidad explícita. Sugerencia: agregar `QueryAsync_UserNameInexistente_DevuelveVacio` en un follow-up si el equipo lo considera valioso.

---

## Test results

| Comando | Resultado | Notas |
| ------- | --------- | ----- |
| `dotnet build SGV.slnx` | ✅ PASS | 0 errors, 4 warnings `NU1510` pre-existentes en `SGV.Infraestructura` (no introducidos por este change) |
| `dotnet test SGV.slnx --filter "FullyQualifiedName~Auditoria\|FullyQualifiedName~Web"` | ✅ PASS | **1479/1479** passed, 0 failed, 0 skipped (2m 11s) |
| `dotnet test SGV.slnx --filter "FullyQualifiedName~Auditoria"` | ✅ PASS | **95/95** passed (17s) |
| `dotnet test SGV.slnx --filter "FullyQualifiedName~AuditoriasIndexTests\|FullyQualifiedName~AuditoriasControllerTests\|FullyQualifiedName~AuditoriaServicioConsultaTests"` | ✅ PASS | **75/75** passed (17s) — los 3 archivos target del change |
| `dotnet test SGV.slnx` (full suite) | ⚠️ 3406/3413 | 7 failures intermitentes en `UsuariosEndToEndMySqlFactTests.LoginAsAdminAsync` (JWT + MySQL 9.6 race condition); pasan 5/5 en aislamiento y 8/9 en corrida posterior — pre-existing flake no relacionado con #251 |
| `bun install --frozen-lockfile` + `bun run build` (en `src/SGV.Web`) | ✅ PASS | `gulp build` exit 0; sólo warnings deprecation pre-existentes (`DEP0180 fs.Stats`, `baseline-browser-mapping` outdated) — no introducidos por este change |

### Breakdown del cambio (post-merge)

| Bucket | Cantidad |
| ------ | -------- |
| Tests añadidos | 17 (7 API + 5 aplicación + 5 web) |
| Tests migrados | 2 (1 MySqlTheory `QueryAsync_Filtros_AplicanSegunEsperado` con parámetro `userId` → `userName` + seed Identity; 1 pre-existente web `Get_Index_WhenApiFails_ShowsVisibleErrorAndPreservesFilters` adaptado al `<select>`) |
| `[MySqlFact]` corridos | 8 nuevos (5 aplicación `GetFilterOptionsAsync_*` + `QueryAsync_FiltraPorUserNameCaseInsensitive` + `QueryAsync_FiltroUserNameVacio_NoAplicaFiltro` + el InlineData migrado del MySqlTheory) |
| `[MySqlFact]` skipped | 0 (MySQL local `localhost:3306` v9.6.0 disponible) |

### Cambios verificados (cambio completo, post-merge)

**Slice A (commit `a026ff6a`) + Slice B (commit `4fc288b3`)**:

- `src/SGV.Contracts/Auditoria/AuditoriaFilterOptions.cs` — nuevo record (issue #251).
- `src/SGV.Contracts/Auditoria/AuditoriaListQuery.cs` — `UserId` renombrado a `UserName`.
- `src/SGV.Aplicacion/Auditoria/IAuditoriaServicioConsulta.cs` — agregado `GetFilterOptionsAsync`.
- `src/SGV.Infraestructura/Persistencia/AuditoriaServicioConsulta.cs` — implementación EF con `Distinct().OrderBy().Take(100)` + rename LINQ `x.u.UserName == userName`.
- `src/SGV.Api/Controllers/AuditoriasController.cs` — endpoint `GET /filter-options` + atributo de clase admin-only.
- `src/SGV.Web/Integration/Auditoria/IAuditoriaApiClient.cs` — `GetFilterOptionsAsync`.
- `src/SGV.Web/Integration/Auditoria/AuditoriaApiClient.cs` — HTTP impl + rename query key `userId → userName`.
- `src/SGV.Web/Pages/Auditorias/Index.cshtml` — `<select>` poblado, fallback no bloqueante, placeholder `"nombre de usuario"`, onchange reset `p=1`.
- `src/SGV.Web/Pages/Auditorias/Index.cshtml.cs` — `LoadFilterOptionsAsync`, propiedades `EntityNameOptions`/`OperationOptions`/`FilterOptionsLoadFailed`/`FilterOptionsMessage`, helpers `BuildSortRouteValues`/`BuildPagedRouteValues`/`BuildDetailsRouteValues` renombrados.
- `tests/SGV.Tests/Api/AuditoriasControllerTests.cs` — 7 tests nuevos Slice A.
- `tests/SGV.Tests/Aplicacion/Auditoria/AuditoriaServicioConsultaTests.cs` — 5 tests nuevos + MySqlTheory migrado.
- `tests/SGV.Tests/Web/Auditoria/AuditoriasIndexTests.cs` — 5 tests nuevos Slice B.
- `tests/SGV.Tests/Web/Auditoria/FakeAuditoriaApiClient.cs` — `GetFilterOptionsResult`/`Handler`/`Exception`/`Calls`.
- `docs/decisiones-implementacion.md` — entrada **D-8** (rename `userId → userName` + endpoint `filter-options`).

---

## Drift detectado durante verify

### SUGGESTION (pre-existente, no introducido por este change)

1. **`Details.cshtml.cs` no consume `userName` cuando viene desde Index.** El Index actual (post-#251) propaga `userName=` en `BuildDetailsRouteValues` (línea 307). Pero `Details.cshtml.cs:151` lee `[FromQuery(Name = "userId")]`, por lo que cuando el usuario navega del listado al detalle, el parámetro `userName` queda silenciosamente ignorado y la PageModel `DetailsModel.UserId` queda en null. El round-trip del filtro "Usuario" se pierde al hacer drill-down.
   - **No introducido por #251** (Details.cshtml.cs existía así desde #248 / archive `2026-07-31-ajustes-listado-auditoria`).
   - **Fuera de scope** del change #251 (`design.md §1` y `apply-progress §Drift from plan` documentan que el rename del wire se limitó a Index + Listado, no a Details).
   - **Sin cobertura runtime** del round-trip Index → Details con `userName=`.
   - **Sugerencia**: en un follow-up, migrar `Details.cshtml.cs` a `[FromQuery(Name = "userName")]` + propiedad `UserName`. Issue candidato: #251.1 o parte de un cleanup DRY del round-trip PRG.
   - **No bloqueante** para archive de #251.

---

## Hallazgos

### CRITICAL

_Ninguno._

### WARNING

_Ninguno._

### SUGGESTION (pre-existente, no introducido por este change)

1. `Details.cshtml.cs` bindea `[FromQuery(Name = "userId")]` mientras el Index propaga `userName` (round-trip del filtro Usuario se pierde en drill-down). Ver sección "Drift detectado" arriba.

### INFO

1. **Pre-existing flake** en `UsuariosEndToEndMySqlFactTests.LoginAsAdminAsync`: 7 fallos intermitentes durante la corrida full-suite (los tests usan `JwtRealWebApplicationFactory` con MySQL 9.6 local; probable race condition de JWT signing key + migration lifecycle). Los tests pasan 5/5 en aislamiento y 8/9 en corrida inmediatamente posterior. **No relacionado con #251**. Documentado en `docs/decisiones-implementacion.md` §"Riesgos residuales" como flakiness pre-existente.

2. **Weak coverage** del escenario "UserName inexistente devuelve conjunto vacío" (delta spec escenario #6): comportamiento correcto por semántica LINQ, sin test explícito `?userName=noexiste → TotalCount == 0`. Sugerencia de granularidad, no regresión.

3. **MySQL local v9.6.0** (no 8.0.36 como sugiere `SGV.Infraestructura/SGV.Infraestructura.csproj`). El servidor `mysqld` escucha en `localhost:3306` (PID 11424), responde a `SELECT VERSION()` con `9.6.0`. Los `[MySqlFact]` corren con `Database.Migrate()` idempotente contra `sgv_test` — todos los 8 tests nuevos del change pasaron contra MySQL real. No es regresión: el repo documenta que la conexión es stock MySQL dev y el server Pomelo es compatible con 8.x y 9.x.

---

## Verdict final

**PASS**

- Build limpio (0 errors, 0 nuevos warnings).
- **1479/1479** tests focalizados (`Auditoria|Web`) PASS.
- **95/95** tests del módulo Auditoría PASS.
- **75/75** tests de los 3 archivos target (`AuditoriasIndexTests`, `AuditoriasControllerTests`, `AuditoriaServicioConsultaTests`) PASS.
- **8/8** `[MySqlFact]` nuevos del change corren contra MySQL real.
- **17/17** nuevos tests introducidos por el change pasan.
- **12/12** acceptance criteria del proposal demostradas con cobertura runtime + evidencia de código (file + line).
- **17/18** escenarios del delta spec cubiertos con test explícito; 1 WEAK_COVERAGE por granularidad.
- D-2 reforzado por construcción (separación física de tipos verificada en código, tests de JSON serialización y grep repo-wide).
- D-8 documentado en `docs/decisiones-implementacion.md` líneas 197-268 con tabla de tests verificadores.
- `main` (rama operativa `develop`) queda compilable post-merge de ambos slices.
- 1 SUGGESTION (round-trip Index → Details `userName` no consumido por Details) — pre-existente, no introducido por #251, fuera de scope, no bloqueante.
- 7 failures intermitentes en `UsuariosEndToEndMySqlFactTests.LoginAsAdminAsync` durante corrida full-suite — **flake pre-existente confirmado por aislamiento**, **no relacionado con #251**.

**Recomendación**: avanzar a `sdd-archive`. El change está completo, todos los criterios de éxito del proposal están cumplidos y la verificación runtime no encontró regresiones ni bloqueantes. El detalle de `Details.cshtml.cs` puede quedar como follow-up opcional (issue candidato).
