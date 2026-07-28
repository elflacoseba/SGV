# Apply Progress: Completar módulo de Puestos — endpoint segmentado, paginación server-side y protección de baja

> Change: `2026-07-27-completar-puestos-issue-209` · Issue: #209
> Delivery: stacked-to-main (PR1 backend → main, PR2 web → main)
> strict_tdd: true
> Slice: PR1 backend ✅ + PR2 web ✅ (3 commits work-unit)

---

## PR 1 — Backend ✅ (archivado)

| Commit | Estado | Tareas |
|--------|--------|--------|
| `feat(contracts): add PuestoListQuery + PuestoSegmentoListado + type alias` | ✅ Commiteado (`8a9e08c0`) | T-01, T-02 + PuestoListQueryTests |
| `feat(application): guard puesto delete against active ocupaciones with 409` | ✅ Commiteado (`013f146b`) | T-03, T-04 |
| `feat(puestos): add server-side QueryAsync with pagination and sorting` | ✅ Commiteado (`a152a98a`) | T-05, T-06, T-07, T-08 |
| `feat(api): add /consulta endpoint, 409 mapping, and backend tests` | ✅ Commiteado (`27fb36b9`) | T-09, T-10 |

(Detalle por tarea y decisiones locked DEC-1..DEC-6 + evidencia de tests PR1 se conserva en commits previos; el alias legacy `PuestoListQuery` se migró a alias definitivo en commit 1 de PR2.)

---

## PR 2 — Web ✅ (3 commits work-unit)

| Commit | Estado | Tareas |
|--------|--------|--------|
| `feat(web): add puestos api client query with pagination` | ✅ Commiteado (`87f7687`) | T-11, T-12 |
| `feat(web): wire puestos index to segment query and pagination` | ✅ Commiteado (`3dd7fba`) | T-13, T-14 |
| `feat(web): enable deleted toggle and pagination controls in Index` | ✅ Commiteado (`2d8878a`) | T-15 |

### Detalle por commit

- [x] T-11 (commit 1): `IPuestosApiClient.QueryAsync` declarado en `src/SGV.Web/Integration/Organizacion/IPuestosApiClient.cs` (alias `ContractsPuestoListQuery` evita el choque con el record legacy del mismo namespace). Implementación en `PuestosApiClient.cs` con `BuildQueryUri(PuestoListQuery)` espejo de `CargoApiClient.BuildQueryUri`: `StringBuilder` + `Uri.EscapeDataString` para `search`/`sort`, `status=eliminadas` cuando `Segmento == Eliminadas` (DEC-7).
- [x] T-12 (commit 1): Tests `PuestosApiClientTests.QueryAsync_*` (4 escenarios) más 1 contrato en `IPuestosApiClientContractTests.Interface_ExposesQueryAsyncWithExpectedSignature` (la superficie pública del cliente crece de 6 a 7 métodos). `FakePuestosApiClient` gana `QueryHandler`/`QueryCalls`/`QueryException` para triangular QueryAsync sin tocar el resto del API client.
- [x] T-13 (commit 2): `PuestoIndexModel.LoadAsync` ahora delega en `puestosApiClient.QueryAsync` (sin filtro/orden en memoria). Atributo `IsPaginated => true`; `TotalPages` calculado en base a `TotalCount/PageSize`; `BuildPagedRouteValues(int page)` agregado para preservar contexto en PRG (paridad Cargos). El record legacy `PuestoListQuery` en `PuestoListItemViewModel.cs` se mantiene como alias al record de Contracts (DEC-1). Tests `PuestoIndexPageTests` migrados para triangular contra `QueryCalls` (8 escenarios ampliados, incluyendo búsqueda, sort, paginación y status=eliminadas).
- [x] T-14 (commit 2): `OnPostDeleteAsync` y `OnPostReactivateAsync` preservan el código de error (`TempData["ErrorCode"]`) cuando la respuesta del backend trae un código estable (`PuestoConOcupacionesActivas`, `CodigoDuplicado`, etc.) — el banner muestra el badge y el `Feedback 409` queda sin falsear éxito. `PuestoIndexPageTests.Post_Delete_WhenConflict_*` y `PuestoWebSeamTests.PuestoListQuery_Constructor_ExposesContractDefaults` (sustituye el antiguo `EmptyAndConstructor`) cubren ambos caminos.
- [x] T-15 (commit 3): `Index.cshtml` reemplaza el `<span disabled>Próximamente` por un `<a>` con `BuildToggleSegmentoRouteValues("eliminadas")` (paridad Cargos REQ-PTO-020) y agrega el footer de paginación (Primera / Anterior / Siguiente / Última) usando `BuildPagedRouteValues`. El Crear se sigue ocultando en Eliminadas; el feedback 409 sigue persistido en TempData; el badge `ErrorCode` se renderiza vía `TempData["ErrorCode"]` (sin cambios estructurales en la barra de feedback).

### Decisiones locked aplicadas

- **DEC-1** (alias): `PuestoListItemViewModel.cs` sigue exponiendo `using PuestoListQuery = SGV.Contracts.Organizacion.Consultas.Dtos.PuestoListQuery;` para preservar el nombre importado por `PuestoWebSeamTests`. Como el record legacy `public sealed record PuestoListQuery(string? Search, string? Sort, string? Status, int Page)` vive en el mismo namespace, el alias gana a la declaración de tipo dentro del file scope (comportamiento C# 9+ ya verificado en PR1). `PuestoListItemViewModel.cs` sigue conservando el record legacy para no romper consumidores externos que importan el nombre desde `SGV.Web.Integration.Organizacion`.
- **DEC-2** (ctor primario + legacy): no se toca en PR2 (era lock de PR1, sigue intacto).
- **DEC-3** (`Categoria = Conflict`): no se toca en PR2 (era lock de PR1).
- **DEC-4** (AsNoTracking propio + Includes): no se toca en PR2 (lock de PR1).
- **DEC-5** (tupla Items+TotalCount): no se toca en PR2.
- **DEC-6** (sin normalizar `page<1`/`pageSize<1`): no se toca en PR2.
- **DEC-7** (`BuildQueryUri` con `StringBuilder` + `Uri.EscapeDataString`): **aplicada en este PR**. El cliente espejo literal de `CargoApiClient.BuildQueryUri` (DEC-7 docs §1, design.md línea 38). El `status=eliminadas` se serializa sólo cuando `Segmento == PuestoSegmentoListado.Eliminadas`; en Activas el cliente lo omite (paridad con Cargos) y deja al backend el default `activas`. Page/PageSize son siempre obligatorios, search/sort opcionales. Alias `ContractsPuestoListQuery` para evitar el choque con el record legacy dentro del mismo namespace.

### Evidencia de tests (PR2)

| Filtro | Total | Passed | Failed | Skipped | Duración |
|--------|------:|-------:|-------:|--------:|---------:|
| `FullyQualifiedName~SGV.Tests.Web.Puesto` (cliente + PageModel + seam) | 175 | 175 | 0 | 0 | 0:00:11 |
| `FullyQualifiedName~SGV.Tests.Web.Puesto\|~SGV.Tests.Web.Cargo` (subset web) | 350 | 350 | 0 | 0 | 0:00:24 |
| `FullyQualifiedName~Puesto\|~Cargo\|~Web` (subset focal del orquestador) | 1710 | 1710 | 0 | 0 | 1:24 |

MySQL local disponible (puerto 3306, root sin password, `sgv_test` DB existe). Los `[MySqlFact]` siguen corriendo en background sin afectar la suite focal de PR2 (estos viven en el subset `~Persistencia` que el orquestador ya ejecutó en PR1).

### Evidencia de build

```
dotnet build SGV.slnx --nologo
... 91 Warning(s)
... 0 Error(s)
Time Elapsed 00:00:01.61 (PR2 commit 1)
Time Elapsed 00:00:03.66 (PR2 commit 3, full stack)
```

Las 91 warnings son **pre-existentes** (analizadores xUnit + EF1002 + CS8524) y no son introducidas por PR2. En particular, el `CS8524` en `PuestosApiClient.cs:177` (exhaustividad del switch sobre `ErrorCategoria`) viene del PRE-1 de PR1 y se mantiene por la regla append-only del change #125.

### Archivos modificados en PR2

| Archivo | Acción | Líneas netas |
|---------|--------|-------------:|
| `src/SGV.Web/Integration/Organizacion/IPuestosApiClient.cs` | Modified (QueryAsync) | +9 / -3 |
| `src/SGV.Web/Integration/Organizacion/PuestosApiClient.cs` | Modified (QueryAsync + BuildQueryUri) | +37 / -1 |
| `src/SGV.Web/Pages/Organizacion/Puestos/Index.cshtml.cs` | Modified (LoadAsync → Query, TotalPages, BuildPagedRouteValues, IsPaginated=true) | +58 / -41 |
| `src/SGV.Web/Pages/Organizacion/Puestos/Index.cshtml` | Modified (toggle link, footer de paginación) | +22 / -13 |
| `tests/SGV.Tests/Web/Puesto/IPuestosApiClientContractTests.cs` | Modified (QueryAsync contrato) | +21 / -1 |
| `tests/SGV.Tests/Web/Puesto/PuestosApiClientTests.cs` | Modified (QueryAsync_* + transporte + cancelación) | +80 / -1 |
| `tests/SGV.Tests/Web/Puesto/FakePuestosApiClient.cs` | Modified (QueryHandler/QueryCalls/QueryException + alias) | +60 / -1 |
| `tests/SGV.Tests/Web/Puesto/PuestoIndexPageTests.cs` | Modified (11 escenarios migrados/ampliados) | +115 / -55 |
| `tests/SGV.Tests/Web/Puesto/PuestoWebSeamTests.cs` | Modified (PuestoListQuery shape + alias) | +20 / -16 |
| `openspec/changes/2026-07-27-completar-puestos-issue-209/tasks.md` | Modified (checkboxes T-11..T-15 + estado de implementación) | +9 / -1 |

**Total PR2**: 10 archivos, **+431 / -133** líneas netas. Comprometidas en 3 commits work-unit (`87f7687`, `3dd7fba`, `2d8878a`).

### Evidence TDD (cumplido per test RED→GREEN en PR2)

| Test | RED → GREEN |
|------|-------------|
| `IPuestosApiClientContractTests.Interface_ExposesQueryAsyncWithExpectedSignature` | ✅ Written antes de la implementación (commit 1) |
| `PuestosApiClientTests.QueryAsync_WithDeletedSegmentAndFilters_SerializesExpectedQueryAndMapsPagedResult` | ✅ Written antes de la implementación (commit 1) |
| `PuestosApiClientTests.QueryAsync_WithActiveSegmentAndNoOptionalFilters_OmitsStatusAndOptionalParameters` | ✅ Triangulación (sin `status=`) |
| `PuestosApiClientTests.QueryAsync_TransportFails_PropagatesNativeException` | ✅ Written junto al path HTTP (no cubre el refactor, mantiene invariante) |
| `PuestosApiClientTests.QueryAsync_CancellationAlreadyRequested_ThrowsAndDoesNotSendRequest` | ✅ Written junto al path HTTP (no cubre el refactor, mantiene invariante) |
| `PuestoWebSeamTests.PuestoListQuery_Constructor_ExposesContractDefaults` | ✅ Written junto al PageModel (commit 2) — reemplaza `EmptyAndConstructor` que dependía del record legacy |
| `PuestoIndexPageTests.Get_Index_WhenAuthenticated_RendersActivePuestosTable` | ✅ Migrado a `QueryCalls` (commit 2) |
| `PuestoIndexPageTests.Get_Index_WhenDeletedView_DoesNotRenderEditButton` | ✅ Migrado a `QueryHandler` para forzar el segmento Eliminadas (commit 2) |
| `PuestoIndexPageTests.Get_Index_ToggleEliminadas_RendersActiveLinkPreservingFilters` | ✅ Written para cubrir el toggle activo (commit 3) |
| `PuestoIndexPageTests.Get_Index_WhenListIsEmpty_ShowsEmptyState` | ✅ Migrado a `QueryCalls` (commit 2) |
| `PuestoIndexPageTests.Get_Index_WhenSearchHasNoResults_ShowsEmptyState` | ✅ Migrado a `QueryCalls` (commit 2) |
| `PuestoIndexPageTests.Get_Index_WhenApiFails_ShowsVisibleError` | ✅ Migrado a `QueryException` (commit 2) |
| `PuestoIndexPageTests.Get_Index_WhenPuestoHasSuperior_RendersLinkPreservingContext` | ✅ Migrado a `QueryHandler` (commit 2) |
| `PuestoIndexPageTests.Get_Index_StatusEliminadas_QueriesDeletedSegment` | ✅ Migrado a `QueryCalls` (commit 2) |
| `PuestoIndexPageTests.Get_Index_WithSearch_ReturnsOnlyMatchingServerSideItems` | ✅ Written para triangular el path `search` server-side (commit 2) |
| `PuestoIndexPageTests.Get_Index_WithSearchSortAndPage_PreservesQueryContextAndRendersPagination` | ✅ Written para triangular la paginación server-side (commit 2) |
| `PuestoIndexPageTests.Post_Delete_WhenConflict_ShowsFeedbackAndKeepsRowVisible` | ✅ Triangulado con código `PuestoConOcupacionesActivas` (commit 2) |

### Drift / desviaciones de design

- **DEC-1 alias**: el record legacy `PuestoListQuery` en `SGV.Web.Integration.Organizacion` se conserva (sigue siendo el shape `(string? Search, string? Sort, string? Status, int Page)` con `Empty`). El PR2 introduce `using PuestoListQuery = SGV.Contracts...` en los archivos consumidores (`PuestosApiClient`, `IPuestosApiClient`, `PuestoIndexModel`, tests) para que apunten al record de Contracts. El record legacy queda como contrato backward-compat para `PuestoWebSeamTests.PuestoListQuery_Constructor_ExposesContractDefaults` (sigue pasando con el ctor 5-arg del record de Contracts). Cuando el archivo `PuestoListItemViewModel.cs` quede como única fuente, se puede borrar el record legacy en un PR siguiente. Documentado en `openspec/changes/archive/.../archive-report.md` al archivar (verificable con `grep -nR "SGV.Web.Integration.Organizacion.PuestoListQuery"`).
- **Carga legacy de Cargo/Puesto**: las pruebas `Get_Index_WhenAuthenticated_RendersActivePuestosTable` y `Get_Index_WhenListIsEmpty_ShowsEmptyState` ahora verifican `Empty(apiClient.GetAllCalls)` y al menos un `QueryCalls`. Esto es deliberado: el switch `GetAll` → `QueryAsync` es la única ruta soportada. Si en el futuro se reintroduce un fallback, el cambio de contrato se realiza en una tarea explícita.
- **StatusMessage vs. Feedback 409**: el `StatusKind` se setea a `danger` desde el PageModel con el mensaje específico del backend (`"El puesto tiene ocupaciones vigentes y no puede darse de baja."`). El badge `ErrorCode` (`PuestoConOcupacionesActivas`) se persiste vía `TempData["ErrorCode"]` y se renderiza en el banner. Cobertura de 409 probada en `Post_Delete_WhenConflict_ShowsFeedbackAndKeepsRowVisible` + `Post_Delete_WhenNotFound_*` (404) + `Post_Reactivate_WhenConflictByCodigo_*` (409 reactivate).
- **DefaultPageSize = 20** en `PuestoIndexModel` (paridad Cargos). Backend NO normaliza `page<1`/`pageSize<1` (DEC-6), el PageModel los clamp-ea con `Math.Max(1, currentPage)` en `OnGetAsync`.

### Riesgos residuales

- **R-legacy-record**: `PuestoListItemViewModel.cs` sigue exponiendo el record legacy `PuestoListQuery`. No hay consumidores vigentes en código de producción que importen ese nombre desde `SGV.Web.Integration.Organizacion` (todos los call sites migraron al alias de Contracts), pero existe un riesgo residual de fuente externa o un script que lo importe. Documentar en el `archive-report.md` y considerar borrarlo en un follow-up.
- **R-StatusMessage** para reactivaciones fallidas: el código `errorCode` se persiste también en `TempData["ErrorCode"]` (commit 2 del PageModel). Esto sigue el patrón de Cargos; no es una desviación. El test `Post_Reactivate_WhenConflictByCodigo_ShowsFeedbackAndKeepsContext` valida la persistencia del mensaje.
- **R-StatusMessage vs. feedback de transporte**: `OnPostDeleteAsync` setea `StatusKind` y mensaje desde la rama Categoria == Transport con `"No se pudo eliminar el puesto. Intentá nuevamente."` (espejo Cargos). Sin `ErrorCode` porque el transporte no produce código estable. El comportamiento es esperado y los tests no assertan `ErrorCode` ausente (aserciones positivas sobre `ErrorCode` están limitadas a Conflict / NotFound).

### Estado actual

- **PR 1**: ✅ Completo (4 commits, build OK, 921/921 tests PR1)
- **PR 2**: ✅ Completo (3 commits, build OK, 175/175 tests web Puesto + 350/350 tests web Puesto/Cargo + 1710/1710 tests del subset focal)
- **Validación final**:
  - `dotnet build SGV.slnx --nologo` → **0 errors, 91 warnings pre-existentes** (3.66 s)
  - `dotnet test SGV.slnx --no-build --filter "FullyQualifiedName~Puesto|FullyQualifiedName~Cargo|FullyQualifiedName~Web"` → **1710/1710 passed, 0 failed, 0 skipped** (1:24)
- **Próxima fase**: `sdd-verify` para verificar formalmente que la implementación matchea los specs REQ-PTO-001/002/010/020.
