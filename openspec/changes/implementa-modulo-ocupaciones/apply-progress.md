# Apply Progress: implementa-modulo-ocupaciones

## Status
**Phase**: Apply — Unit 2 (Application Services — Phase 2)
**Progress**: 6/6 tasks complete (Phase 1 + Phase 2)
**Mode**: Strict TDD
**Delivery**: Chained PR (stacked-to-main), PR #2 of chain

## TDD Cycle Evidence

| Task | Test File | Layer | Safety Net | RED | GREEN | TRIANGULATE | REFACTOR |
|------|-----------|-------|------------|-----|-------|-------------|----------|
| 1.1 | `tests/SGV.Tests/Dominio/Ocupaciones/OcupacionTests.cs` | Unit | ✅ 14/14 | ✅ Written | ✅ 25/25 | ✅ 11 cases (3 per behavior) | ✅ Clean |
| 1.2 | `src/SGV.Dominio/Ocupaciones/Ocupacion.cs` | Unit | N/A (modified existing) | ✅ Referenced by 1.1 | ✅ 25/25 | ✅ Covered by 1.1 | ✅ Guard extracted |
| 1.3 | `src/SGV.Aplicacion/Ocupaciones/**` | N/A (contracts) | N/A (new files) | N/A (contracts only) | ✅ Builds | ➖ Structural only | ➖ None needed |
| 2.1 | `tests/SGV.Tests/Aplicacion/Ocupaciones/OcupacionServicioComandosTests.cs` | Application | ✅ 500/500 | ✅ Written | ✅ 22/22 | ✅ 22 cases (7 create, 4 update, 3 finalize, 3 delete, 5 reactivate) | ✅ Clean |
| 2.2 | `src/SGV.Aplicacion/Ocupaciones/Comandos/OcupacionServicioComandos.cs` | Application | N/A (new) | ✅ Referenced by 2.1 | ✅ 22/22 | ✅ Covered by 2.1 | ✅ Helper extraction |
| 2.3 | `tests/SGV.Tests/Aplicacion/Ocupaciones/OcupacionServicioConsultaTests.cs` + `src/SGV.Aplicacion/Ocupaciones/Consultas/OcupacionServicioConsulta.cs` | Application | ✅ 500/500 | ✅ Written | ✅ 7/7 | ✅ 3 list + 4 detail | ➖ None needed |

## Completed Tasks

### Phase 1 (Domain + Command Foundation)

- [x] 1.1 RED: Extended `OcupacionTests.cs` with 11 new tests covering:
  - `Actualizar`: happy path update, reject on finalized, reject on deleted
  - `Finalizar`: guard against already-finalized, guard against deleted
  - `EliminarLogicamente`: happy path soft-delete, reject on finalized, reject on already-deleted
  - `Reactivar`: restore from finalized, restore from deleted, reject when already active
  - Added helper factory methods `CrearOcupacionActiva()` and `CrearOcupacionFinalizada()`

- [x] 1.2 GREEN: Updated `Ocupacion.cs` with:
  - `EsVigente` now checks both `FechaFin is null && !IsDeleted`
  - `RequerirEditable()` private guard for finalized/deleted state
  - `Actualizar(Guid, Guid, DateOnly, TipoAsignacion, string?)` with editable guard and date coherency
  - `Finalizar(...)` — added editable guard (existing signature unchanged)
  - `EliminarLogicamente()` — sets IsDeleted, DeletedAt
  - `Reactivar()` — clears FechaFin, IsDeleted, DeletedAt; blocks if already active

- [x] 1.3 Created application contracts:
  - `OcupacionRequests.cs`: `CrearOcupacionRequest`, `ActualizarOcupacionRequest`, `FinalizarOcupacionRequest`
  - `OcupacionCommandResult.cs`: `OcupacionErrorType`, `OcupacionError`, `OcupacionCommandResult`
  - Validators: `CrearOcupacionRequestValidator`, `ActualizarOcupacionRequestValidator`, `FinalizarOcupacionRequestValidator`
  - `IOcupacionServicioComandos`: CrearAsync, ActualizarAsync, FinalizarAsync, EliminarAsync, ReactivarAsync
  - `IOcupacionServicioConsulta`: ListAsync(includeHistory), GetByIdAsync
  - `IOcupacionRepository`: AddAsync, GetByIdForUpdateAsync, GetByIdIncludingHistoryAsync, UpdateAsync, ExistsActiveByPuestoAsync, ExistsActiveByPersonaYPuestoAsync
  - `OcupacionDto`: Consumer-safe DTO with computed Estado string

### Phase 2 (Application Services) ✅

- [x] 2.1 RED: Added 22 command service tests covering:
  - **CrearAsync (7 tests)**: datos válidos, persona inexistente (404), persona inactiva (409), puesto inexistente (404), puesto inactivo (409), puesto único conflictivo (409), persona+y puesto único conflictivo (409)
  - **ActualizarAsync (4 tests)**: activo → éxito, inexistente (404), finalizada (409), eliminada (409)
  - **FinalizarAsync (3 tests)**: activo → éxito, inexistente (404), ya finalizada (409)
  - **EliminarAsync (3 tests)**: activo → éxito, inexistente (404), ya eliminada (409)
  - **ReactivarAsync (5 tests)**: desde finalizado, desde eliminado, inexistente (404), puesto conflictivo (409), ya activa (409)

- [x] 2.2 GREEN: Implemented `OcupacionServicioComandos` with:
  - Full reference validation: persona y puesto existence (404) vs active state (409) via `GetByIdIncludingDeletedAsync`
  - Uniqueness checks: `ExistsActiveByPuestoAsync` and `ExistsActiveByPersonaYPuestoAsync`
  - Domain method orchestration: `Actualizar`, `Finalizar`, `EliminarLogicamente`, `Reactivar`
  - DTO mapping with computed `Estado` string ("Activo", "Finalizado", "Eliminado")
  - 404 vs 409 distinction: load via `GetByIdIncludingHistoryAsync` first for 404, check `EsVigente` for 409
  - Validation via existing FluentValidation validators

- [x] 2.3 RED/GREEN: Added `OcupacionServicioConsulta` and 7 tests:
  - `ListAsync` default: active only
  - `ListAsync(includeHistory: true)`: all rows
  - `ListAsync` empty: empty list
  - `GetByIdAsync` for active, finalized, deleted rows — all return DTO
  - `GetByIdAsync` for nonexistent — returns null
  - Added `ListAllIncludingHistoryAsync` to `IOcupacionRepository` interface

## Files Changed (Phase 2)

| File | Action | What Was Done |
|------|--------|---------------|
| `src/SGV.Aplicacion/Ocupaciones/Comandos/OcupacionServicioComandos.cs` | Created | Command service: Crear, Actualizar, Finalizar, Eliminar, Reactivar with full reference validation |
| `src/SGV.Aplicacion/Ocupaciones/Consultas/OcupacionServicioConsulta.cs` | Created | Query service: ListAsync(includeHistory), GetByIdAsync |
| `tests/SGV.Tests/Aplicacion/Ocupaciones/OcupacionServicioComandosTests.cs` | Created | 22 TDD command service tests with inline fakes |
| `tests/SGV.Tests/Aplicacion/Ocupaciones/OcupacionServicioConsultaTests.cs` | Created | 7 TDD query service tests with inline fake |
| `src/SGV.Aplicacion/Ocupaciones/Consultas/IOcupacionRepository.cs` | Modified | Added `ListAllIncludingHistoryAsync` contract |
| `openspec/changes/implementa-modulo-ocupaciones/tasks.md` | Modified | Tasks 2.1-2.3 marked complete |

## Deviations from Design

None — implementation matches design. Key design decisions preserved:
- Reactivation reuses same row (clears FechaFin, IsDeleted, audit fields)
- Reference validation uses `GetByIdIncludingDeletedAsync` for 404 vs 409 distinction
- Query detail reads bypass soft-delete filter via `GetByIdIncludingHistoryAsync`
- Consumed `EsVigente` domain property for active-state checks

## Issues Found

None. One minor design note: `ListAllIncludingHistoryAsync` returns ALL persisted rows (including logically deleted), since "non-physically-deleted" includes all database rows — the project never physically deletes records.

## Remaining Tasks (Phase 3+)

- [ ] 3.1: Infrastructure repository + mappers
- [ ] 3.2: Persistence repository tests
- [ ] 3.3: Verify existing migration compatibility
- [ ] 4.1: API controller
- [ ] 4.2: DI registration + factory fakes
- [ ] 4.3: API + Swagger tests
- [ ] 5.1-5.2: End-to-end verification

## Workload / PR Boundary

- **Mode**: Chained PR slice (stacked-to-main)
- **Current work unit**: Unit 2 — Application Services (Phase 2)
- **Boundary**: Command + query service implementations and tests
- **Estimated review budget**: ~1387 lines (tests 867, application code 510, contracts 10)
