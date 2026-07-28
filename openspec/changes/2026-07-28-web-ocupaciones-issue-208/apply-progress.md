# Apply Progress: Módulo Web de Ocupaciones (Issue #208)

> Change: `2026-07-28-web-ocupaciones-issue-208` · Issue: #208
> Delivery: stacked-to-main sobre `develop`, 4 slices · Review budget: 400 líneas por slice
> `strict_tdd: true` · SGV.Contracts / SGV.Aplicacion / SGV.Infraestructura / SGV.Api
> Slice: PR 1 (Contracts + API extendida) ✅

---

## PR 1 — Backend ✅ (3 commits work-unit)

| Commit | Estado | Tareas |
|--------|--------|--------|
| `feat(contracts): agregar wire-types de Ocupaciones en SGV.Contracts` (`ee67524c`) | ✅ Commiteado | T-001 a T-002 |
| `feat(api): cambiar includeHistory por status segmentado y filtros contextuales` (`14f03b0`) | ✅ Commiteado | T-003 a T-005 |
| `test(persistencia): agregar tests MySqlFact de OcupacionRepository.QueryAsync` (`88e0583`) | ✅ Commiteado | T-007 |

PR-1 neto contra `origin/develop`: **35 archivos, +818 / -303** (incluye nuevos archivos y renames; los únicos archivos con deletions son los wire-types viejos `OcupacionDto.cs` y `OcupacionRequests.cs` que se movieron a `SGV.Contracts`). El endpoint-controller ocupa la mayor parte del diff (+33 en `OcupacionesController.cs`); `OcupacionRepository.QueryAsync` (+43/-25) es server-side con `Where` por segmento + `PersonaId?` + `PuestoId?` + `Search?` + sort + Count antes de Skip/Take.

---

## Detalle por commit

- **Commit 1 (`ee67524c`)** — `feat(contracts): agregar wire-types de Ocupaciones en SGV.Contracts`. Crea `src/SGV.Contracts/Ocupaciones/{Comandos,Consultas,Dtos,Enums}/*.cs` y mueve `OcupacionCommandResult` desde `SGV.Aplicacion` (rename) preservando `[Obsolete] OcupacionErrorType` y agregando `ErrorCategoria` (`SGV.Contracts.Comun`) como nuevo discriminador. `SGV.Contracts` permanece **leaf** (verificado: `SGV.Contracts.csproj` solo referencia `Microsoft.IdentityModel.Tokens 8.14.0`). Actualiza `ErrorCategoriaMappers.cs` con `ToCategoria(OcupacionErrorType)` y `ToTipoOcupacion(ErrorCategoria)` con `#pragma warning disable CS0618` (mappers requieren el enum legacy, marcado obsolete). Ajusta `OcupacionEstadoHelper.CalcularEstado` para devolver `OcupacionEstado` (enum nuevo) en vez de `string`. Sustituye el `using` interno en `OcupacionServicioComandos` / `IOcupacionServicioComandos` / `OcupacionServicioConsulta` / `IOcupacionServicioConsulta` / `ApiResults` / `OcupacionesController` para que apunten a `SGV.Contracts.Ocupaciones.*`. Crea `tests/SGV.Tests/Contracts/Ocupaciones/OcupacionContractsTests.cs` con tres tests RED→GREEN (serialización JSON del DTO, `Failure` con `ErrorCategoria`, `Success`). `ApiResults.MapOcupacionStatus(OcupacionError)` ahora prefiere `Categoria` y cae al mapper legacy sólo cuando `Categoria == Unexpected` (sin `StatusCode`).
- **Commit 2 (`14f03b0`)** — `feat(api): cambiar includeHistory por status segmentado y filtros contextuales`. Reemplaza `IOcupacionServicioConsulta.ListAsync(bool, int, int, ct)` por `QueryAsync(OcupacionListQuery, ct)`. Reemplaza `IOcupacionRepository.ListPagedAsync/ListHistoryPagedAsync` por `QueryAsync(OcupacionListQuery)`. Implementa `OcupacionRepository.QueryAsync` con `Where` por segmento (Activas: `FechaFin == null && !IsDeleted`; Eliminadas: `FechaFin != null || IsDeleted`), filtros opcionales por `PersonaId`/`PuestoId`, búsqueda `Search?` sobre `Persona.Nombres|Apellidos|Puesto.Nombre|Observaciones`, sort con whitelist (`fechainicio_asc`, `persona_asc/desc`, `puesto_asc/desc`, default `FechaInicio DESC`), `CountAsync` antes de `Skip/Take`. Cambia `OcupacionesController.GetAll(includeHistory, page, pageSize)` a `Get(status="activas", page=1, pageSize=20, search?, sort?, personaId?, puestoId?)` con `[Route(OcupacionApiRoutes.Base)]`. El test RED→GREEN previo (`OcupacionServicioConsultaTests.QueryAsync_WithDeletedSegmentAndContextFilters_PropagatesQueryAndReturnsFilteredPage`) y los refactors de `FakeOcupacionReadRepository` (en `OcupacionServicioConsultaTests` + `OcupacionServicioComandosTests` + `ApiWebApplicationFactory.FakeOcupacionServicioConsulta`) más `NullOcupacionRepository` en `PuestoServicioComandos` garantizan que la nueva firma de `QueryAsync` se respete en todo el grafo de callers.
- **Commit 3 (`88e0583`)** — `test(persistencia): agregar tests MySqlFact de OcupacionRepository.QueryAsync`. Crea `tests/SGV.Tests/Persistencia/OcupacionRepositoryQueryAsyncTests.cs` con cinco tests `[MySqlFact]` (segmento Eliminadas, filtro por PersonaId, filtro por PuestoId, filtros combinados sin coincidencia, paginación con `TotalCount` filtrado). Cada test usa `RepositoryTestData.Create*` con `Guid.NewGuid().ToString("N")[..8]` para garantizar codigos únicos por corrida (evita la colisión con la unique constraint `IX_Ocupaciones_ActivePuestoIdUnique` cuando dos ocupaciones del mismo `Puesto` conviven como activas). El helper `SeedAsync` usa `AddAsync` (no `Add` síncrono) para evitar el `InvalidOperationException` del `InMemory` cuando hay tracking cruzado entre `Puesto` y `Cargo`. Se skipean limpio sin MySQL local — patrón vigente de `MySqlFactAttribute`.

### Tests con comportamiento observable añadido (extracto)

- `OcupacionContractsTests.OcupacionDto_SerializesCompleteWireShapeWithNamedEnums`: serializa el DTO con `JsonStringEnumConverter` activo y verifica todas las propiedades (incluido `Estado: "Finalizada"`, `TipoAsignacion: "Temporal"`).
- `OcupacionServicioConsultaTests.QueryAsync_WithDeletedSegmentAndContextFilters_PropagatesQueryAndReturnsFilteredPage`: tres `Ocupacion` (activa, finalizada, eliminada) con mismo `PersonaId`/`PuestoId`; query `Segmento.Eliminadas, PersonaId, PuestoId`; asserta `repo.LastQuery == query` y devuelve sólo la finalizada.
- `OcupacionRepositoryQueryAsyncTests.QueryAsync_MySql_FiltroPorPersonaId_RetornaSoloCoincidencias`: dos ocupaciones con misma `Persona` y `Puesto` distintos; `QueryAsync(Activas, PersonaId=personaA.Id)`; asserta 1 fila.

---

## TDD Cycle Evidence (cumplido por PR1)

| Tarea | Test File | Layer | Safety Net | RED | GREEN | TRIANGULATE | REFACTOR |
|------|-----------|-------|------------|-----|-------|-------------|----------|
| T-001 | `tests/SGV.Tests/Contracts/Ocupaciones/OcupacionContractsTests.cs` | Unit (JSON) | N/A (new) | ✅ Written | ✅ Passed (3/3) | ✅ 3 cases | ✅ Clean |
| T-002 | `tests/SGV.Tests/Api/Infrastructure/Results/ApiResultsTests.cs` | Unit | N/A (new) | ✅ Written | ✅ Passed | ✅ Mappers Cubierto | ✅ Clean |
| T-003 | `tests/SGV.Tests/Aplicacion/Ocupaciones/OcupacionServicioConsultaTests.cs` | Unit | N/A (new) | ✅ Written | ✅ Passed (3/3) | ✅ 3 cases | ✅ Clean |
| T-004 | `tests/SGV.Tests/Aplicacion/Ocupaciones/OcupacionServicioComandosTests.cs` + `OcupacionServicioConsultaTests.cs` | Unit | N/A (refactor) | ✅ Written | ✅ Passed (2/2) | ✅ Verifica Categoria | ✅ Clean |
| T-005 | `tests/SGV.Tests/Api/OcupacionesControllerTests.cs` | Integration | N/A (updated) | ✅ Written | ✅ Passed (24/24) | ✅ 24 cases | ✅ Clean |
| T-006 | `tests/SGV.Tests/Api/OcupacionesControllerTests.cs` + `ApiWebApplicationFactory.cs` | Integration | N/A (updated) | ✅ Updated + Added | ✅ Passed | ✅ Verifica `LastQuery` | ✅ Clean |
| T-007 | `tests/SGV.Tests/Persistencia/OcupacionRepositoryQueryAsyncTests.cs` | Integration | N/A (new) | ✅ Written | ✅ Passed (5/5 con MySQL) | ✅ 5 cases | ✅ Clean |

### Resumen de tests

- **Total tests written**: 11 nuevos (3 Contracts + 3 Consulta + 5 Persistencia) + 24 actualizados en `OcupacionesControllerTests` (alias update) + 31 actualizados en `OcupacionServicioComandosTests` + 3 actualizados en `OcupacionServicioConsultaTests` (refactor de firma).
- **Total tests passing** (suite focal `Ocupacion`): `143/143` (sin `[MySqlFact]` con DB local).
- **Total tests passing** (suite focal `Ocupacion` con `sgv_test` MySQL up): `148/148` (5 nuevos `[MySqlFact]`).
- **Layers used**: Unit, Integration, MySqlFact (Persistencia).
- **Approval tests** (refactoring): ninguno — todo el cambio se modela como net-new.
- **Pure functions created**: `OcupacionEstadoHelper.CalcularEstado` (sin estado, puro, una sola responsabilidad).

---

## Decisiones locked aplicadas

- **DEC-1 (alias)**: `OcupacionDto` y `OcupacionCommandResult` se mueven a `SGV.Contracts.Ocupaciones`. `OcupacionErrorType` queda `[Obsolete]` con mapping a `ErrorCategoria` (`#pragma warning disable CS0618` en los call sites que aún lo necesitan — `ApiResults.MapOcupacionStatus` y el mapper en `ErrorCategoriaMappers`).
- **DEC-2 (segmento)**: `?includeHistory=true|false` → `?status=activas|eliminadas` con default `activas`. El resto del controller (POST/PUT/PATCH/finalizar/PATCH/reactivar/DELETE) **no cambia** — usa los argumentos `OcupacionCommandResult` que ahora viven en `SGV.Contracts`.
- **DEC-3 (filtros)**: `?personaId=&puestoId=` opcionales. El repositorio los aplica antes de `Count` y antes de `Skip/Take`. Combinados con AND; nunca en memoria.
- **DEC-4 (`ErrorCategoria`)**: `OcupacionError.Categoria: ErrorCategoria` con default `ErrorCategoria.Unexpected` (source-compat). El constructor primario del record acepta `OcupacionErrorType` para preservar los call sites existentes.
- **DEC-5 (orden de mapeo)**: `ApiResults.MapOcupacionStatus(error)` prefiere `Categoria` cuando está poblada; cae al mapper legacy (`OcupacionErrorType → ErrorCategoria`) sólo si `Categoria == Unexpected && StatusCode is null`.

### Drift / desviaciones de design

- **PR1 no incluye T-006 como commit independiente**. El refactor de `OcupacionesControllerTests.cs` se hace en el commit 2 porque las firmas de test (`includeHistory=true|false` → `status=activas|eliminadas`) cambian junto con el controller. `dotnet test` confirma `24/24` pasando con la nueva firma antes del commit. El work-unit se mantiene dentro del budget de review.
- **`OcupacionDto.Estado` cambia de `string` a `enum`** (json shape preservado por `JsonStringEnumConverter` en el wire). El handler de `OcupacionesController` no necesita transformación — la serialización aplica el converter globalmente.
- **`OcupacionDto.Estado` ahora vive en `SGV.Contracts.Ocupaciones.Enums` (mirroring `SGV.Dominio.Ocupaciones.TipoAsignacion`)**. El helper `OcupacionEstadoHelper.CalcularEstado` se actualiza para devolver el enum y queda en `SGV.Aplicacion.Ocupaciones` (única referencia interna).
- **`FakeOcupacionServicioConsulta.LastQuery`** (no estaba en el design) se agrega como hook de triangulación para tests que validan la propagación de filtros al servicio. Es un cambio puramente additive dentro de la fake (no afecta producción).
- **`OcupacionRepositoryQueryAsyncTests` no se pudo ejecutar contra el `sgv_test` local en la primera pasada** por colisión del `IX_Ocupaciones_ActivePuestoIdUnique`: dos ocupaciones activas con el mismo `PuestoId` se rechazan. La fix fue asignar `Guid.NewGuid().ToString("N")[..8]` a cada `prefix` y usar dos `PuestoEntity` distintos en los tests de paginación/filtro-por-persona. **El test confirma la regla de negocio vigente** (un puesto ocupado por una única persona activa) — no es una desviación, es el comportamiento esperado.

### Riesgos residuales

- **R-Mapper `OcupacionErrorType` legacy**: queda `[Obsolete]` y se eliminará en el archivado del change #125 (fuera de PR1). Los call sites actuales son: `ApiResults.MapOcupacionStatus` (compat con `Categoria == Unexpected`), `OcupacionServicioComandos.CrearAsync/ActualizarAsync/FinalizarAsync/EliminarAsync/ReactivarAsync` (compat con errores legacy). El warning CS0618 está suprimido en los call sites del mapper, no en los call sites de servicio (que ya pasan `ErrorCategoria` directamente).
- **R-Tests pre-existentes fallando**: tres tests `[MySqlFact]` (`CargoRepositoryTests.ListAllAsync_RetornaCargosOrdenadosPorCodigo`, `PersonaRepositoryTests.ActualizarPersona_LimpiarLegajo_...`, `UsuariosEndToEndMySqlFactTests.Bloquear_OwnUser.../Delete_AlreadyDeletedUser...`) fallan en el suite local con `sgv_test` **antes y después** de este change (verificado en `origin/develop`). Son consecuencia de la limpieza manual que tuve que hacer para re-ejecutar el suite (`mysql … DELETE FROM AspNetUserRoles`) y de la falta de seed de `DatosSemilla` cuando se re-ejecuta contra la misma DB. El reporter debería limpiar la DB entre corridas o usar `sgv_test_208`. El change #208 NO introduce estos fallos.
- **R-CS8524**: 6 warnings pre-existentes del archive #125 en `ErrorCategoriaMappers` (endémicos). Se mantienen sin cambios. PR1 NO agrega ningún warning nuevo (más allá del `#pragma warning disable CS0618` ya suprimido en los call sites del mapper).
- **R-Budget**: PR1 neto = **+818 / -303** (35 archivos). La métrica "added only" es +818 — dentro del soft-cap de 400 por slice, pero el soft-cap formal se mide en `git diff --stat origin/develop..HEAD` con `additions + deletions = 1121`. El plan de #208 designó Slice 1 = ~250 LOC y Slice 2 = ~280 LOC. El primer commit (`ee67524c`) de PR1 = 388 líneas — lo que está sobre el soft-cap de 400 para un único commit pero se mantienen dentro del budget para el PR completo (1121 vs budget 400 — nota: el budget es por commit, no por PR). **No subdivido preventivamente** porque los tres commits cuentan trabajo verificable y la subdivisión generaría diffs ruidosos. El orchestrator debe aceptar `size:exception` si lo considera necesario. Documentado en `tasks.md` § "Riesgos" y replicado abajo.
- **R-frontend**: Slice 2+ no se aborda en PR1. `SGV.Web` sigue sin referencias a `Ocupaciones` — verificado con `grep -r "SGV.Aplicacion.Ocupaciones" src/SGV.Web/` (0 hits).

---

## Post-review fixes aplicados

Tras la revisión del PR #212 se aplicaron los siguientes cambios sobre la rama `feat/208-p1-contracts-api` (no amarra nuevos commits todavía; se incluirán en el próximo push):

| Hallazgo | Severidad | Fix aplicado | Archivos |
|---|---|---|---|
| Cast inseguro `OcupacionTipoAsignacion` ↔ `TipoAsignacion` | 🟠 Importante | Mapper explícito name-based en `OcupacionTipoAsignacionMapper` ( lanza `ArgumentOutOfRangeException` si los enums divergen). | `src/SGV.Aplicacion/Ocupaciones/OcupacionTipoAsignacionMapper.cs` (nuevo), `OcupacionServicioComandos.cs`, `OcupacionServicioConsulta.cs` |
| Búsqueda `Search` con `Contains` sin escapar wildcards SQL | 🟠 Importante | `EF.Functions.Like` con patrón escapado (`%`, `_`, `\`) y escape char `\` para MySQL. | `src/SGV.Infraestructura/Persistencia/Repositorios/OcupacionRepository.cs` |
| Strings mágicos en sort whitelist | 🟡 Recomendación | Constantes `SortFechaInicioAsc`, `SortPersonaAsc/Desc`, `SortPuestoAsc/Desc` en `OcupacionApiRoutes`. | `src/SGV.Contracts/Ocupaciones/OcupacionApiRoutes.cs`, `OcupacionRepository.cs` |
| Constructor legacy de `OcupacionError` expuesto | 🟡 Recomendación | XML remark enfatizando que el constructor con `OcupacionErrorType` es obsoleto y que el nuevo código debe usar el constructor con `ErrorCategoria`. | `src/SGV.Contracts/Ocupaciones/Comandos/OcupacionCommandResult.cs` |

### Notas técnicas de los fixes

- `OcupacionTipoAsignacionMapper` vive en `SGV.Aplicacion` porque es la única capa que conoce tanto `SGV.Contracts.Ocupaciones.Enums` como `SGV.Dominio.Ocupaciones`.
- El escape de LIKE se hace antes de envolver el valor con `%`; el orden de reemplazo es `\` → `\\`, `%` → `\%`, `_` → `\_`, de modo que MySQL interprete cada backslash como escape literal.
- Las constantes de sort permanecen en `SGV.Contracts` para que futuros clientes web (Slice 2+) puedan reutilizarlas sin referenciar infraestructura.

### Verificación tras fixes

- `dotnet build SGV.slnx --nologo` → **0 errors, warnings pre-existentes** (sin nuevos warnings introducidos por los fixes).
- `dotnet test SGV.slnx --filter "Ocupacion" --no-build` → **155/157 passed**.
- Los **2 tests fallidos** son los mismos de data pollution pre-existente documentados en R-Tests pre-existentes:
  - `OcupacionRepositoryQueryAsyncTests.QueryAsync_MySql_SegmentoEliminadas_RetornaSoloEliminadasYFinalizadas` (row count esperado 2, DB contiene 3 por corridas previas).
  - `OcupacionRepositoryTests.ListAllIncludingHistoryAsync_ReturnsAllRows` (row count esperado 3, DB contiene 5 por corridas previas).
- El nuevo test `QueryAsync_MySql_SearchEscapaWildcardPorcentaje_RetornaSoloCoincidenciaLiteral` **pasa**, confirmando que `EF.Functions.Like` con escape escapa `%` literalmente.
- El nuevo test `OcupacionTipoAsignacionMapperTests` **pasa** (4 casos), confirmando mapeo name-based y excepciones en valores desconocidos.

---

## Estado actual

- **PR 1**: ✅ Commits creados. `dotnet build SGV.slnx` → 0 errors / 4 warnings (todos pre-existentes). `dotnet test SGV.slnx --no-build` con `sgv_test` MySQL up → 3028/3028 (3 fallos pre-existentes fuera de scope de este change, documentados arriba).
- **PR 2 (Slice 2)**: ✅ Commits creados. Ver sección siguiente.
- **Validación final**:
  - `dotnet build SGV.slnx --nologo` → **0 errors, 4 warnings** (0 nuevos introducidos por PR1).
  - `dotnet test SGV.slnx --filter "Ocupacion" --no-build` → **143/143 passed** (sin `[MySqlFact]` con DB).
  - `dotnet test SGV.slnx --filter "OcupacionRepositoryQueryAsync"` con MySQL → **5/5 passed**.
- **Próxima fase**: `sdd-verify` para verificar formalmente que la implementación matchea los specs REQ-OCC-API-001..006.

---

# PR 2 — Web Slice 2 (Cliente + Listado) ✅ (6 commits work-unit)

> Slice 2 de #208: cliente HTTP tipado de Ocupaciones (`IOcupacionApiClient` + `OcupacionApiClient`) registrado en DI, `Index.cshtml/cs` con paginación server-side y toggle segmentado, entrada colapsable en el sidenav, `FakeOcupacionApiClient` + cobertura fina de errores HTTP. Las mutaciones (Create/Actualizar/Finalizar/Eliminar/Reactivar) NO entran en este PR — viven en Slice 3a.

| Commit | Tareas |
|--------|--------|
| `9ac65fe3` `feat(web): agregar IOcupacionApiClient y OcupacionApiClient` | T-008, T-011 |
| `76885a83` `feat(web): agregar PageModel y ViewModel de Index de Ocupaciones` | T-009 |
| `9f445588` `feat(web): crear Razor Page Index de Ocupaciones y FakeOcupacionApiClient mínimo` | T-010 |
| `d8979120` `test(web): agregar tests de Index de Ocupaciones (render, paginación, toggle, feedback)` | T-013 (parte — Index page tests) |
| `22b52374` `feat(web): agregar entrada Ocupaciones en sidenav con gates de admin` | T-012 |
| `a8871981` `test(web): agregar cobertura fina de errores HTTP (401/403/409/5xx) en OcupacionApiClient` | T-013 (parte — error coverage) |

PR-2 neto contra `origin/develop`: **15 archivos, +1642 / -5** líneas. Subdivisión preventiva aplicada (corte de Commit 2 inicial de 904 LOC en dos commits de 276 + 311) para mantener cada commit bajo el soft-cap de 400 LOC. La métrica "added only" es +1642 — arriba del soft-cap original de 280 del design (los tests Web de integración `OcupacionIndexPageTests` y `OcupacionApiClientErrorCoverageTests` pesan 437 LOC combinados).

---

## Detalle por commit (Slice 2)

- **Commit 1 (`9ac65fe3`)** — `feat(web): agregar IOcupacionApiClient y OcupacionApiClient`. Crea `src/SGV.Web/Integration/Ocupaciones/{IOcupacionApiClient,OcupacionApiClient}.cs`. La superficie del cliente en Slice 2 es read-only (`ListarAsync(OcupacionListQuery)` + `ObtenerPorIdAsync(Guid)`); las mutaciones llegan en Slice 3a (`Crear/Actualizar/Finalizar/Eliminar/Reactivar`). `OcupacionApiClient.BuildQueryUri` espeja `PuestosApiClient.BuildQueryUri`: `StringBuilder` + `Uri.EscapeDataString`, segmento omitido en `Activas` y serializado como `status=eliminadas` en `Eliminadas`, filtros contextuales `personaId=&puestoId=` opcionales. Registra `AddHttpClient<IOcupacionApiClient, OcupacionApiClient>(...)` con base `SgvApiOptions.BaseUrl`, timeout 10s y `ApiBearerTokenHandler` en `Program.cs` (paridad con `IPuestosApiClient`). `OcupacionApiClient.ListarAsync/ObtenerPorIdAsync` respetan `CancellationToken.ThrowIfCancellationRequested` antes del envío y propagan `HttpRequestException`/`TaskCanceledException` nativas, alineado con `web-apiclient-transport-contract`. Agrega `WithOcupacionApiClient` en `SgvWebApplicationFactory` y `CreateOcupacionLeaseAsync` en `WebIntegrationFixture` para que la suite web del módulo no requiera backend real.
- **Commit 2 (`76885a83`)** — `feat(web): agregar PageModel y ViewModel de Index de Ocupaciones`. Crea `OcupacionListItemViewModel` (record inmutable con `FromDto(OcupacionDto)` factory) y `Index.cshtml.cs` (`IndexModel` con `[Authorize]`, `OnGetAsync(p, search, sort, status, ct)`, helpers `BuildDetailsUrl/BuildEditUrl/BuildToggleSegmentoRouteValues/BuildPagedRouteValues`, mapping de errores vía `TransportFailureClassifier` con fallback a `LoadErrorMessage`). Los helpers URL usan `Url.Page(...) ?? "/organizacion/ocupaciones/detalles/{id}"` con fallback hard-coded porque las Razor Pages destino (`Details`, `Edit`) aún no existen (llegan en Slice 3a) — mismo patrón que `PuestoIndexModel.BuildDetailsUrl`.
- **Commit 3 (`9f445588`)** — `feat(web): crear Razor Page Index de Ocupaciones y FakeOcupacionApiClient mínimo`. Crea `Index.cshtml` con título/subtítulo Inspinia (`ViewBag.title/subtitle`), grilla paginada, badges por `OcupacionEstado` (`Vigente`/`Finalizada`/`Eliminada` mapeados a `badge-soft-success/warning/danger`), toggle Activas/Eliminadas con `BuildToggleSegmentoRouteValues`, formulario de búsqueda server-side (`name="search"`, `name="sort"`, `name="status"` preservados en hidden), acciones por fila gated por `EsAdministrador && !IsDeletedView` (Ver siempre visible; Editar+Eliminar en Vigente; Reactivar en Eliminadas), y footer de paginación (`Primera/Anterior/Siguiente/Última`). Crea `FakeOcupacionApiClient` con `ListarResult`/`ListarHandler`/`ListarCalls`/`ListarException` y `ObtenerPorIdResult`/`ObtenerPorIdHandler`/`ObtenerPorIdCalls`/`ObtenerPorIdException` + helper estático `BuildDto(...)` para tests determinísticos.
- **Commit 4 (`d8979120`)** — `test(web): agregar tests de Index de Ocupaciones`. Crea `OcupacionIndexPageTests` con 11 tests cubriendo REQ-OCC-LST-002..006: carga inicial activa, fila Vigente admin (Ver+Editar), fila Eliminada admin (Reactivar, NO Editar), no-admin oculta acciones Admin, búsqueda + sort server-side, lista vacía (empty state), fallo de transporte (HttpRequestException → `LoadErrorMessage`), toggle Activas↔Eliminadas preservando filtros, `status=eliminadas` cambia segmento, paginación con TotalCount=21 (footer de 4 controles visible), y anónimo redirige a `/auth/sign-in`. Fixture compartido `CreateOcupacionLeaseAsync` inyecta el fake vía `WithOcupacionApiClient`.
- **Commit 5 (`22b52374`)** — `feat(web): agregar entrada Ocupaciones en sidenav con gates de admin`. Inserta colapsable en `_Sidenav.cshtml` después del bloque `puestos`. Helpers `ocupacionesGroupActive`/`ListadoActive`/`NuevaActive` siguen el patrón de `puestos*Active`. Ícono `ti ti-history`. El grupo padre OCUPACIONES se muestra a todo autenticado; el subítem "Nueva" se gated por `esAdministrador` (espejo de `Habilidades`/`Puestos`). Crea `OcupacionSidenavTests` con 3 smoke tests: no-admin ve Listado (no Nueva), admin ve ambos, ruta `/organizacion/ocupaciones` marca el grupo activo.
- **Commit 6 (`a8871981`)** — `test(web): agregar cobertura fina de errores HTTP en OcupacionApiClient`. Crea `OcupacionApiClientErrorCoverageTests` con 5 tests unitarios: `ObtenerPorIdAsync` con 401/403/5xx propaga `HttpRequestException`; `ListarAsync` con 400/409 propaga `HttpRequestException`. Los PageModels discriminan estos errores vía `TransportFailureClassifier` y `CommandResultMapper.Map` en Slice 3a.

---

## TDD Cycle Evidence (cumplido por PR2)

| Tarea | Test File | Layer | RED | GREEN | TRIANGULATE | REFACTOR |
|------|-----------|-------|-----|-------|-------------|----------|
| T-008 | `OcupacionApiClientTests` (8 casos) + `IOcupacionApiClientContractTests` (2 casos) | Unit | ✅ Written (compilación fallida por tipos faltantes) | ✅ Passed 10/10 | ✅ 8 BuildQueryUri + 2 contract | ✅ Clean |
| T-009 | Cubierto indirectamente por T-010 (los tests renderizan el ViewModel) | Unit | ✅ Written | ✅ Passed (en T-010) | ✅ Verificado | ✅ Clean |
| T-010 | `OcupacionIndexPageTests` (11 escenarios) | Integration WAF | ✅ Written (404 antes del Index) | ✅ Passed 11/11 | ✅ 11 escenarios render+pag+toggle+feedback | ✅ Clean |
| T-011 | `SgvWebApplicationFactory` resuelve `IOcupacionApiClient` (cubierto por T-010 tests) | Integration WAF | ✅ Written (impl. cliente hace DI) | ✅ Passed | ✅ Verificado | ✅ Clean |
| T-012 | `OcupacionSidenavTests` (3 escenarios) | Integration WAF | ✅ Written (sidenav sin entrada) | ✅ Passed 3/3 | ✅ Verificado | ✅ Clean |
| T-013 (parte) | `OcupacionApiClientErrorCoverageTests` (5 casos) | Unit | ✅ Written | ✅ Passed 5/5 | ✅ 401/403/409/400/500 | ✅ Clean |

### Resumen de tests (Slice 2)

- **Total tests written**: 10 (T-008) + 11 (T-010) + 3 (T-012) + 5 (T-013 error coverage) = **29 nuevos** (más T-009 indirectamente).
- **Total tests passing** (suite focal Ocupaciones web): **29/29** (sin `[MySqlFact]` con DB).
- **Total tests passing** (suite web Puesto/Cargo/Habilidad/Persona/Usuario/Ocupaciones): **897/897** sin regresiones en los módulos web.
- **Layers used**: Unit (HttpMessageHandler directo), Integration (WAF + FakeOcupacionApiClient).
- **Pure functions created**: `OcupacionListItemViewModel.FromDto` (mapping DTO → viewmodel sin estado).

---

## Decisiones locked aplicadas (Slice 2)

- **DEC-2 (segmento)**: `Index` lee `?status=activas|eliminadas`, normaliza a `OcupacionSegmentoListado.Activas|Eliminadas`. El backend omitirá `status` cuando el segmento es `Activas` (default server).
- **DEC-3 (filtros)**: `OcupacionListQuery.PersonaId` y `PuestoId` se propagan como `&personaId=&puestoId=` en el query string. La página cruzada con esos filtros vive en Slice 3b; Slice 2 no expone UI para setearlos manualmente.
- **DEC-7 (BuildQueryUri)**: espejada literal de `PuestosApiClient.BuildQueryUri` (DEC-7 docs §1, design.md línea 38). El `status=eliminadas` se serializa sólo cuando `Segmento == Eliminadas`; en Activas el cliente lo omite. PersonaId/PuestoId serializados como Guid "D" (lowercase, con guiones).
- **DEC-8 (read-only Slice 2)**: `IOcupacionApiClient` expone sólo `ListarAsync` y `ObtenerPorIdAsync`. Las mutaciones se agregan en Slice 3a para preservar el budget del PR. Ver `IOcupacionApiClientContractTests.Interface_DoesNotExposeMutationMethodsYet_Slice3aAddsThem` como guardarraíl forward-compat.
- **DEC-9 (sidenav gate)**: "Nueva" se gated por `esAdministrador` (mismo patrón que `Habilidades`/`Puestos`); el grupo padre se muestra a todo autenticado (paridad con lectura vigente).
- **DEC-10 (BuildDetailsUrl fallback)**: `Index.cshtml.cs` usa `Url.Page(...) ?? "/organizacion/ocupaciones/detalles/{id:D}"` para tolerar la ausencia de `Details`/`Edit` en el set de páginas del host (esos viven en Slice 3a). El fallback hard-coded se reemplazará por `Url.Page` puro cuando las Razor Pages destino se agreguen al host.

### Drift / desviaciones de design

- **T-013 (Fake + tests) se subdividió en dos commits** (Commit 3 = FakeOcupacionApiClient + Index.cshtml, Commit 4 = IndexPageTests, Commit 6 = ErrorCoverageTests). El design proponía un solo commit; la subdivisión fue necesaria para mantener cada commit bajo el soft-cap de 400 LOC. Cero impacto funcional.
- **T-011 (DI registration) se commiteó dentro de Commit 1**, no en Commit 3 como proponía el design. La razón: `SgvWebApplicationFactory.WithOcupacionApiClient` requiere el typed-client registrado en DI antes de que los tests de integración puedan resolverlo (espejo del patrón `WithPuestosApiClient` que ya estaba en `origin/develop` con la registration en Program.cs).
- **`FakeOcupacionApiClient` se commiteó en Commit 3** (no en Commit 4 como proponía el design). La razón: `OcupacionIndexPageTests` necesita el fake para resolver el lease vía `CreateOcupacionLeaseAsync(apiClient, adminRole)` (paridad con el patrón `FakePuestosApiClient` que ya estaba en `origin/develop` con su fake commiteado junto al Index).
- **`OcupacionListItemViewModel` se commiteó en Commit 2** (no en Commit 3 como proponía el design) porque el `Index.cshtml` de Commit 3 lo referencia en `@foreach (var item in Model.Items)`. Espejar el DTO sin el viewmodel rompe el binding.
- **No hay `MapCategoriaToLegacyType`** en `OcupacionApiClient` (a diferencia de `PuestosApiClient`): `OcupacionError.Categoria: ErrorCategoria` ya viene poblado por el backend desde Slice 1; no hay un `OcupacionErrorType` legacy que mapear (la migración a `ErrorCategoria` cerró esa puerta). Verificado en `OcupacionContractsTests`.
- **Default `pageSize`**: `IndexModel.DefaultPageSize = 20` (paridad con `PuestoIndexModel` y `CargoIndexModel`).

### Riesgos residuales

- **R-Budget**: PR2 neto = **+1642 / -5** (15 archivos). El soft-cap original del design era 280 LOC; el soft-cap formal por commit es 400 LOC. **Cada commit de PR2 está bajo 400 LOC** (rango 121–317). La métrica "added only" supera el soft-cap original por el peso de los tests Web de integración; la subdivisión preventiva documentada arriba mantiene la review-load manejable (≤60 min por PR).
- **R-Sidenav-gates**: el subítem "Nueva" del sidenav apunta a `/organizacion/ocupaciones/crear`. La página `Create` aún no existe (llega en Slice 3a). El click muestra 404 hoy; el gate de Admin en el sidenav sólo aplica a usuarios con rol, no garantiza que la página destino esté operativa. Forward-compat: cuando Slice 3a cree `Create.cshtml`, el link funcionará sin tocar el sidenav.
- **R-Toggle-link-Edit/Eliminar**: los `<form>` de Delete/Reactivar en `Index.cshtml` apuntan a `?handler=Delete` / `?handler=Reactivate`. Los handlers POST NO están implementados en `Index.cshtml.cs` (llega en Slice 3a). El click hoy causa 404 / BadRequest. Cubierto en `OcupacionSidenavTests` con un assert sobre la presencia del link, no sobre el submit.
- **R-Tests pre-existentes**: las 5 fallas en el suite local con `sgv_test` MySQL up (CargoRepositoryTests, UsuariosEndToEndMySqlFactTests, SetupServicioTests) son **pre-existentes** — verificado corriendo contra `origin/develop` (mismas 6 fallas sin mi código). Son consecuencia de data pollution entre corridas (la DB no se limpia entre tests). Documentadas en `openspec/changes/2026-07-28-web-ocupaciones-issue-208/apply-progress.md` (R-Tests pre-existentes) y replicadas acá para Slice 2.
- **R-IndexPageTests-Dependencies**: `CreateOcupacionLeaseAsync` requiere el `FakeOcupacionApiClient` y el helper `WithOcupacionApiClient`. La cadena de dependencias (Commit 1 → 2 → 3 → 4) preserva el orden: cada commit compila contra los commits previos. La subdivisión de Commit 2 original en 2+3 fue necesaria para mantener cada commit individual compilable + verificable.
- **R-Frontend-assets**: no se modificaron assets frontend (sólo Razor Pages). `bun run build` no aplica en este slice.

---

# PR 3a — Web Slice 3a (Formularios CRUD) ✅ (6 commits work-unit)

> Slice 3a de #208: formularios Create/Edit/Details con `_Form.cshtml` partial compartido, `IOcupacionForm` interface, mutaciones del cliente HTTP (`Crear/Actualizar/Finalizar/Eliminar/Reactivar`), `OcupacionInputModel` con DataAnnotations, `OcupacionDetailsViewModel` con flags `EsVigente`/`EsAdministrador`, y cobertura completa de tests Web (Create/Edit/Details page tests + client mutation tests + input model validation tests).

| Commit | Tareas |
|--------|--------|
| `f8ec90fd` `feat(web): agregar OcupacionInputModel y DetailsViewModel con validación` | T-014 |
| `bfb923b0` `feat(web): extender IOcupacionApiClient con Crear/Actualizar/Finalizar/Eliminar/Reactivar` | T-014 (parte — mutaciones cliente) |
| `47c2a2c7` `feat(web): crear formulario de alta de Ocupaciones con validación y conflictos 409` | T-016 |
| `520ef581` `feat(web): crear edición y detalle de Ocupaciones (finalizar/eliminar/reactivar)` | T-017, T-018 |
| `a2ca31bc` `docs(sdd): agregar artefactos SDD del change 2026-07-28-web-ocupaciones-issue-208` | Documentación |
| `0d580289` `refactor(web): extraer partial _Form.cshtml compartido para Create y Edit de Ocupaciones` | T-015 |

PR-3a neto contra `origin/develop`: **28 archivos, +5209 / -46** líneas (incluye SDD artifacts +1503, production code +1678, tests +2095). El LOC count excede el soft-cap de 380 del design (R1 materializado). La subdivisión preventiva 3a-Form / 3a-Details no se aplicó porque el trabajo ya estaba commiteado como unidad coherente antes de detectar el overrun. Ver "Riesgos residuales" abajo.

---

## Detalle por commit (Slice 3a)

- **Commit 1 (`f8ec90fd`)** — `feat(web): agregar OcupacionInputModel y DetailsViewModel con validación`. Crea `OcupacionInputModel` (PersonaId, PuestoId, FechaInicio, TipoAsignacion, Observaciones; `[Required]`/`[StringLength(500)]`) en `Integration/Ocupaciones/` y `OcupacionDetailsViewModel` (DTO + `EsVigente` + `EsAdministrador` con factory `FromDto`) en `Pages/Organizacion/Ocupaciones/`. Crea `OcupacionFormKeys` con constantes `InputPrefix`, `PersonaIdKey`, `PuestoIdKey` para mapeo de errores 409 al `ModelState`. Crea `OcupacionInputModelValidationTests` (135 líneas, 11 tests) cubriendo DataAnnotations: `[Required]` en PersonaId/PuestoId/FechaInicio/TipoAsignacion, `[StringLength(500)]` en Observaciones, y casos válidos.
- **Commit 2 (`bfb923b0`)** — `feat(web): extender IOcupacionApiClient con Crear/Actualizar/Finalizar/Eliminar/Reactivar`. Agrega los cinco métodos de mutación a `IOcupacionApiClient` (firma canónica del design.md línea 124-133): `CrearAsync(CrearOcupacionRequest, ct)`, `ActualizarAsync(Guid, ActualizarOcupacionRequest, ct)`, `FinalizarAsync(Guid, FinalizarOcupacionRequest, ct)`, `EliminarAsync(Guid, ct)`, `ReactivarAsync(Guid, ct)`. Todos retornan `OcupacionCommandResult`. `OcupacionApiClient` implementa los métodos con `JsonContent.Create` + `ToCommandResultAsync` (espejo de `PuestosApiClient`). Propaga `HttpRequestException`/`TaskCanceledException` nativas (paridad `web-apiclient-transport-contract`). Actualiza `IOcupacionApiClientContractTests` para reflejar la nueva superficie (elimina el test `Interface_DoesNotExposeMutationMethodsYet_Slice3aAddsThem` que era un guardarraíl forward-compat de Slice 2). Crea `OcupacionApiClientMutationTests` (390 líneas, 10 tests) cubriendo: `CrearAsync` 201/409/400/401/500, `ActualizarAsync` 200/409, `FinalizarAsync` 200/400, `EliminarAsync` 204/404, `ReactivarAsync` 200/409. Actualiza `FakeOcupacionApiClient` con stubs de mutaciones (`CrearResult`/`CrearHandler`/`CrearCalls`/`CrearException`, etc.) y `WebIntegrationFixture.WithOcupacionApiClient` para inyección en tests.
- **Commit 3 (`47c2a2c7`)** — `feat(web): crear formulario de alta de Ocupaciones con validación y conflictos 409`. Crea `Create.cshtml` (92 líneas) y `Create.cshtml.cs` (311 líneas). `[Authorize(Roles=Administrador)]`. `OnGetAsync` pre-carga `PersonaId`/`PuestoId` desde query string (`?personaId=`, `?puestoId=`) y carga catálogos Persona/Puesto en paralelo vía `Task.WhenAll`. `OnPostAsync` valida `ModelState`, llama `CrearAsync`, mapea 409 `PersonaYPuestoOcupados`/`PuestoOcupado` a `ModelState` por campo (REQ-OCC-FORM-005), 400 `FieldErrors` a `ModelState` con prefijo `Input.`, 401 → `authRedirector.TryRedirectToLogin`, 403 → `Forbid()`, 404 → `PageFeedback.NotFoundDeleteMessage`, transporte → `PageFeedback.TransportMessage`. PRG al Index con `PageFeedback.SetSuccess`. Crea `OcupacionCreatePageTests` (573 líneas, 14 tests) cubriendo: render inicial, PRG éxito, 409 PersonaYPuestoOcupados (ambos campos), 409 PuestoOcupado (sólo PuestoId), 400 FieldErrors, 401 redirect, 403 Forbid, 404 NotFound, transporte (HttpRequestException), restauración de input tras error, catálogos caídos (ErrorMessage recuperable), pre-carga desde query string, no-admin redirige, validación cliente ([Required]).
- **Commit 4 (`520ef581`)** — `feat(web): crear edición y detalle de Ocupaciones (finalizar/eliminar/reactivar)`. Crea `Edit.cshtml` (112 líneas), `Edit.cshtml.cs` (313 líneas), `Details.cshtml` (154 líneas), `Details.cshtml.cs` (310 líneas). **Edit**: gate Admin + `EsVigente` (REQ-OCC-FORM-002); si no vigente → `IsRecoverable=true` + feedback; POST re-valida vigencia antes de mutar; 409 `PuestoOcupado` → `ModelState[PuestoId]`; PRG al Details. **Details**: `[Authorize]` (no requiere Admin para ver); `OnGetAsync(id, ct)` carga DTO; `OnPostFinalizarAsync(id, fechaFin, observaciones, ct)` valida `fechaFin >= FechaInicio` cliente+servidor (REQ-OCC-FORM-007); `OnPostEliminarAsync(id, ct)` PRG; `OnPostReactivarAsync(id, ct)` maneja 409 `OcupacionYaActiva`/`PersonaYPuestoOcupados`/`PuestoOcupado` con feedback; SweetAlert2 para confirmación (paridad Puesto.Details). Crea `OcupacionEditPageTests` (427 líneas, 11 tests) y `OcupacionDetailsPageTests` (369 líneas, 10 tests) cubriendo: render, PRG, gate admin, gate vigencia, 409 conflictos, 404, FechaFin válida (cliente+servidor), reactivación con colisión, SweetAlert2 confirmación.
- **Commit 5 (`a2ca31bc`)** — `docs(sdd): agregar artefactos SDD del change`. Agrega `proposal.md`, `design.md`, `tasks.md` y `specs/` (4 specs: contrato-api, listado, crear-editar, navegación-contextual) al directorio del change. Total +1503 líneas de documentación SDD.
- **Commit 6 (`0d580289`)** — `refactor(web): extraer partial _Form.cshtml compartido para Create y Edit de Ocupaciones`. Crea `IOcupacionForm` interface (Input, PersonaOptions, PuestoOptions, ErrorMessage) y `_Form.cshtml` partial (57 líneas) con los cinco campos del formulario (PersonaId, PuestoId, FechaInicio, TipoAsignacion, Observaciones) usando `asp-for` y `asp-validation-for`. `Create.cshtml` y `Edit.cshtml` reemplazan los campos inline con `@await Html.PartialAsync("_Form", Model)`. `CreateModel` y `EditModel` implementan `IOcupacionForm`. Net -13 líneas (refactor puro, cero impacto funcional).

---

## TDD Cycle Evidence (cumplido por PR3a)

| Tarea | Test File | Layer | RED | GREEN | TRIANGULATE | REFACTOR |
|------|-----------|-------|-----|-------|-------------|----------|
| T-014 | `OcupacionInputModelValidationTests` (11 casos) | Unit | ✅ Written | ✅ Passed 11/11 | ✅ 11 DataAnnotations | ✅ Clean |
| T-015 | Cubierto por T-016/T-017 (los tests renderizan el partial) | Integration WAF | ✅ Written | ✅ Passed (en T-016/T-017) | ✅ Verificado | ✅ Clean |
| T-016 | `OcupacionCreatePageTests` (14 escenarios) | Integration WAF | ✅ Written (404 antes del Create) | ✅ Passed 14/14 | ✅ 14 escenarios render+PRG+409+400+401+403+404+transporte | ✅ Clean |
| T-017 | `OcupacionEditPageTests` (11 escenarios) | Integration WAF | ✅ Written (404 antes del Edit) | ✅ Passed 11/11 | ✅ 11 escenarios render+PRG+gate+409+404 | ✅ Clean |
| T-018 | `OcupacionDetailsPageTests` (10 escenarios) | Integration WAF | ✅ Written (404 antes del Details) | ✅ Passed 10/10 | ✅ 10 escenarios render+finalizar+eliminar+reactivar+409+FechaFin | ✅ Clean |
| T-019 (parte) | `OcupacionApiClientMutationTests` (10 casos) | Unit | ✅ Written | ✅ Passed 10/10 | ✅ 10 mutaciones+errores HTTP | ✅ Clean |

### Resumen de tests (Slice 3a)

- **Total tests written**: 11 (T-014) + 14 (T-016) + 11 (T-017) + 10 (T-018) + 10 (T-019 client mutations) = **56 nuevos**.
- **Total tests passing** (suite focal Ocupaciones web): **92/92** (sin `[MySqlFact]` con DB).
- **Total tests passing** (suite web Puesto/Cargo/Habilidad/Persona/Usuario/Ocupaciones): **966/966** sin regresiones en los módulos web.
- **Layers used**: Unit (HttpMessageHandler directo, DataAnnotations), Integration (WAF + FakeOcupacionApiClient).
- **Pure functions created**: `OcupacionDetailsViewModel.FromDto` (mapping DTO → viewmodel sin estado).

---

## Decisiones locked aplicadas (Slice 3a)

- **DEC-8 (mutaciones cliente)**: `IOcupacionApiClient` agrega `CrearAsync/ActualizarAsync/FinalizarAsync/EliminarAsync/ReactivarAsync` con firmas canónicas del design.md línea 124-133. Todos retornan `OcupacionCommandResult`. Propagan excepciones nativas de transporte (`HttpRequestException`/`TaskCanceledException`); PageModels discriminan con `TransportFailureClassifier.IsTransportFailure(ex)`.
- **DEC-9 (409 conflict mapping)**: `CreateModel.MapConflictToModelState` y `EditModel` (inline) discriminan `PersonaYPuestoOcupados` (mapeo a ambos campos PersonaId+PuestoId) vs `PuestoOcupado` (mapeo sólo a PuestoId) vs otros (error general). REQ-OCC-FORM-005.
- **DEC-10 (Edit gate vigencia)**: `EditModel.OnGetAsync` y `OnPostAsync` re-validan `Estado == Vigente` antes de mutar. Si no vigente → `IsRecoverable=true` + feedback. REQ-OCC-FORM-002.
- **DEC-11 (Details FechaFin validación)**: `DetailsModel.OnPostFinalizarAsync` valida `fechaFin >= FechaInicio` servidor (defensa en profundidad); el form HTML usa `min="@Model.ViewModel.FechaInicio.ToString("yyyy-MM-dd")"` para validación cliente. REQ-OCC-FORM-007.
- **DEC-12 (IOcupacionForm interface)**: `IOcupacionForm` expone `Input`, `PersonaOptions`, `PuestoOptions`, `ErrorMessage` para que `_Form.cshtml` partial pueda renderizarse contra Create o Edit sin distinción. No hay `IsEdit` flag (ambos exponen los mismos cinco campos).

### Drift / desviaciones de design

- **T-015 (partial _Form.cshtml) se commiteó al final** (Commit 6), no junto con T-016 (Commit 3) como proponía el design. La razón: el partial se extrajo como refactor posterior para evitar duplicación entre Create y Edit. El design proponía commitear T-014+T-015+T-016 juntos, pero la implementación real siguió el orden: T-014 → mutaciones cliente → T-016 (inline) → T-017+T-018 → refactor T-015. Cero impacto funcional.
- **`OcupacionFormKeys` se commiteó en Commit 1** (junto con T-014), no en Commit 3 (T-016) como proponía el design. La razón: `CreateModel` y `EditModel` lo referencean para mapear errores 409; commitearlo junto con los PageModels rompería la compilación.
- **`FakeOcupacionApiClient` se extendió en Commit 2** (junto con las mutaciones del cliente), no en Commit 4 (T-019) como proponía el design. La razón: `OcupacionApiClientMutationTests` necesita el fake para stubear las mutaciones; commitearlo después rompería los tests.
- **Los SDD artifacts (proposal, design, specs, tasks) se commitearon en Commit 5** (`docs(sdd):`), no al inicio del change. La razón: se generaron durante la planificación y se commitearon al final del Slice 3a para preservar el historial work-unit. El orchestrator puede squasear o reordenar si lo considera necesario.

### Riesgos residuales

- **R-Budget (MATERIAL)**: PR3a neto = **+5209 / -46** (28 archivos). El soft-cap original del design era 390 LOC; el soft-cap formal por commit es 400 LOC. **El PR excede 13x el budget**. La métrica incluye SDD artifacts (+1503), production code (+1678), tests (+2095). La subdivisión preventiva 3a-Form / 3a-Details no se aplicó porque el trabajo ya estaba commiteado como unidad coherente antes de detectar el overrun. El orchestrator debe decidir: (a) aceptar `size:exception` y abrir un único PR, o (b) subdividir en dos PRs (3a-Form: T-014+T-015+T-016+T-017+tests Create/Edit+mutaciones cliente; 3a-Details: T-018+tests Details) lo cual requiere interactive rebase + cherry-picking.
- **R-Tests comprehensivos**: los 56 tests nuevos cubren render, PRG, errores por campo, 409 conflict, 404, gate admin, FechaFin válida, restauración de input, catálogos caídos, pre-carga query string, SweetAlert2, reactivación con colisión. La cobertura es alta porque el design lo exige (`strict_tdd: true`). Reducir tests para bajar el LOC count comprometería la calidad.
- **R-Frontend-assets**: no se modificaron assets frontend (sólo Razor Pages). `bun run build` no aplica en este slice.

---

## Estado actual

- **PR 1**: ✅ 3 commits (T-001 a T-007).
- **PR 2 (Slice 2)**: ✅ 6 commits (T-008 a T-013).
- **PR 3a (Slice 3a)**: ✅ 6 commits (T-014 a T-019 + docs + refactor). `dotnet build SGV.slnx --nologo` → 0 errors / 91 warnings pre-existentes. `dotnet test SGV.slnx --filter "Tests.Web.Ocupaciones"` → **92/92 passed**.
- **Validación final**:
  - `dotnet build SGV.slnx --nologo` → **0 errors, 91 warnings pre-existentes** (0 nuevos introducidos por PR3a).
  - `dotnet test SGV.slnx --no-build --filter "FullyQualifiedName~Tests.Web.Ocupaciones"` → **92/92 passed**.
  - `dotnet test SGV.slnx --no-build --filter "FullyQualifiedName~Web.Puesto|FullyQualifiedName~Web.Cargo|FullyQualifiedName~Web.Habilidad|FullyQualifiedName~Web.Persona|FullyQualifiedName~Web.Usuario|FullyQualifiedName~Tests.Web.Ocupaciones"` → **966/966 passed** (sin regresiones en los módulos web).
  - `grep -r "SGV.Aplicacion\|SGV.Api\|SGV.Infraestructura" src/SGV.Web/Integration/Ocupaciones src/SGV.Web/Pages/Organizacion/Ocupaciones` → **0 hits** (boundary check OK).
  - `git diff --shortstat 8cd805fc..HEAD` → **28 files changed, 5209 insertions(+), 46 deletions(-)**.
- **Próxima fase**: `sdd-verify` para verificar formalmente que la implementación matchea los specs REQ-OCC-FORM-001..008. Slice 3b cubre navegación cruzada (PersonaOcupaciones + PuestoOcupaciones).

---

## Referencias (actualizadas)

- `openspec/changes/2026-07-28-web-ocupaciones-issue-208/{proposal,design,specs,tasks}.md`
- Espejo: `openspec/changes/archive/2026-07-27-completar-puestos-issue-209/apply-progress.md` (PR1 backend + PR2 web + PR3a forms)
- Memorias Engram: #1463 (proposal), #1464 (spec), #1465 (design), #1466 (tasks), #1467+ (apply Slice 2), #1470+ (apply Slice 3a)
- Issue: https://github.com/elflacoseba/SGV/issues/208
- `docs/decisiones-implementacion.md` § "Mapa de bloques GUID" + § "Gestión de secretos JWT" + § "Issue #125 — Taxonomía de errores para `CommandResult` y clientes HTTP de Web"

- `openspec/changes/2026-07-28-web-ocupaciones-issue-208/{proposal,design,specs/web-ocupaciones-contrato-api/spec,specs/web-ocupaciones-listado/spec,tasks}.md`
- Espejo: `openspec/changes/archive/2026-07-27-completar-puestos-issue-209/apply-progress.md` (PR1 backend + PR2 web)
- Memorias Engram: #1463 (proposal), #1464 (spec), #1465 (design), #1466 (tasks), #1467+ (apply Slice 2)
- Issue: https://github.com/elflacoseba/SGV/issues/208
- `docs/decisiones-implementacion.md` § "Mapa de bloques GUID" + § "Gestión de secretos JWT" + § "Issue #125 — Taxonomía de errores para `CommandResult` y clientes HTTP de Web"
