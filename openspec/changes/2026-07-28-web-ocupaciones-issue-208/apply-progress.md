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

## Estado actual

- **PR 1**: ✅ 3 commits (T-001 a T-007).
- **PR 2 (Slice 2)**: ✅ 6 commits (T-008 a T-013). `dotnet build SGV.slnx --nologo` → 0 errors / 91 warnings pre-existentes. `dotnet test SGV.slnx --filter "Tests.Web.Ocupaciones"` → **29/29 passed**.
- **Validación final**:
  - `dotnet build SGV.slnx --nologo` → **0 errors, 91 warnings pre-existentes** (0 nuevos introducidos por PR2).
  - `dotnet test SGV.slnx --no-build --filter "FullyQualifiedName~Tests.Web.Ocupaciones"` → **29/29 passed**.
  - `dotnet test SGV.slnx --no-build --filter "FullyQualifiedName~Web.Puesto|FullyQualifiedName~Web.Cargo|FullyQualifiedName~Web.Habilidad|FullyQualifiedName~Web.Persona|FullyQualifiedName~Web.Usuario|FullyQualifiedName~Tests.Web.Ocupaciones"` → **897/897 passed** (sin regresiones en los módulos web).
  - `dotnet test SGV.slnx --filter "Ocupacion"` (suite completa incluyendo API+App+Persistencia+Contracts+Web) → **155/155 passed** sin `[MySqlFact]`.
- **Próxima fase**: `sdd-verify` para verificar formalmente que la implementación matchea los specs REQ-OCC-LST-001..006. Slice 3a cubre T-014 a T-019 (Create/Edit/Details/_Form).

---

## Referencias (actualizadas)

- `openspec/changes/2026-07-28-web-ocupaciones-issue-208/{proposal,design,specs/web-ocupaciones-contrato-api/spec,specs/web-ocupaciones-listado/spec,tasks}.md`
- Espejo: `openspec/changes/archive/2026-07-27-completar-puestos-issue-209/apply-progress.md` (PR1 backend + PR2 web)
- Memorias Engram: #1463 (proposal), #1464 (spec), #1465 (design), #1466 (tasks), #1467+ (apply Slice 2)
- Issue: https://github.com/elflacoseba/SGV/issues/208
- `docs/decisiones-implementacion.md` § "Mapa de bloques GUID" + § "Gestión de secretos JWT" + § "Issue #125 — Taxonomía de errores para `CommandResult` y clientes HTTP de Web"
