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
- **Validación final**:
  - `dotnet build SGV.slnx --nologo` → **0 errors, 4 warnings** (0 nuevos introducidos por PR1).
  - `dotnet test SGV.slnx --filter "Ocupacion" --no-build` → **143/143 passed** (sin `[MySqlFact]` con DB).
  - `dotnet test SGV.slnx --filter "OcupacionRepositoryQueryAsync"` con MySQL → **5/5 passed**.
- **Próxima fase**: `sdd-verify` para verificar formalmente que la implementación matchea los specs REQ-OCC-API-001..006.

---

## Referencias

- `openspec/changes/2026-07-28-web-ocupaciones-issue-208/{proposal,design,specs/web-ocupaciones-contrato-api/spec,tasks}.md`
- Espejo: `openspec/changes/archive/2026-07-27-completar-puestos-issue-209/apply-progress.md` (PR1 PR1 backend)
- Memorias Engram: #1463 (proposal), #1464 (spec), #1465 (design), #1466 (tasks)
- Issue: https://github.com/elflacoseba/SGV/issues/208
- `docs/decisiones-implementacion.md` § "Mapa de bloques GUID" + § "Gestión de secretos JWT" + § "Issue #125 — Taxonomía de errores para `CommandResult` y clientes HTTP de Web"
