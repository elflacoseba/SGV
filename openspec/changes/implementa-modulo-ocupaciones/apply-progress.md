# Apply Progress: implementa-modulo-ocupaciones

## Status
**Phase**: Apply — Unit 1 (Domain + Command Foundation)
**Progress**: 3/3 tasks complete
**Mode**: Strict TDD
**Delivery**: Chained PR (stacked-to-main), PR #1 of chain

## TDD Cycle Evidence

| Task | Test File | Layer | Safety Net | RED | GREEN | TRIANGULATE | REFACTOR |
|------|-----------|-------|------------|-----|-------|-------------|----------|
| 1.1 | `tests/SGV.Tests/Dominio/Ocupaciones/OcupacionTests.cs` | Unit | ✅ 14/14 | ✅ Written | ✅ 25/25 | ✅ 11 cases (3 per behavior) | ✅ Clean |
| 1.2 | `src/SGV.Dominio/Ocupaciones/Ocupacion.cs` | Unit | N/A (modified existing) | ✅ Referenced by 1.1 | ✅ 25/25 | ✅ Covered by 1.1 | ✅ Guard extracted |
| 1.3 | `src/SGV.Aplicacion/Ocupaciones/**` | N/A (contracts) | N/A (new files) | N/A (contracts only) | ✅ Builds | ➖ Structural only | ➖ None needed |

## Completed Tasks

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

## Files Changed

| File | Action | What Was Done |
|------|--------|---------------|
| `tests/SGV.Tests/Dominio/Ocupaciones/OcupacionTests.cs` | Modified | Added 11 TDD lifecycle tests + helpers |
| `src/SGV.Dominio/Ocupaciones/Ocupacion.cs` | Modified | Added lifecycle methods + EsVigente fix |
| `src/SGV.Aplicacion/Ocupaciones/Comandos/OcupacionRequests.cs` | Created | Command request records |
| `src/SGV.Aplicacion/Ocupaciones/Comandos/OcupacionCommandResult.cs` | Created | Typed result + error types |
| `src/SGV.Aplicacion/Ocupaciones/Comandos/Validaciones/CrearOcupacionRequestValidator.cs` | Created | FluentValidation rules |
| `src/SGV.Aplicacion/Ocupaciones/Comandos/Validaciones/ActualizarOcupacionRequestValidator.cs` | Created | FluentValidation rules |
| `src/SGV.Aplicacion/Ocupaciones/Comandos/Validaciones/FinalizarOcupacionRequestValidator.cs` | Created | FluentValidation rules |
| `src/SGV.Aplicacion/Ocupaciones/Comandos/IOcupacionServicioComandos.cs` | Created | Command service interface |
| `src/SGV.Aplicacion/Ocupaciones/Consultas/Dtos/OcupacionDto.cs` | Created | Consumer-safe DTO |
| `src/SGV.Aplicacion/Ocupaciones/Consultas/IOcupacionServicioConsulta.cs` | Created | Query service interface |
| `src/SGV.Aplicacion/Ocupaciones/Consultas/IOcupacionRepository.cs` | Created | Repository contract |

## Deviations from Design

None — implementation matches design. The `Actualizar` method signature matches `ActualizarOcupacionRequest` fields per design spec.

## Issues Found

None.

## Remaining Tasks (Unit 2+)

- [ ] 2.1 RED: Application command service tests (404/409, finalized-not-editable, collisions, reactivation)
- [ ] 2.2 GREEN: Application command service implementation
- [ ] 2.3 RED/GREEN: Application query service tests and implementation
- [ ] 3.1: Infrastructure repository + mappers
- [ ] 3.2: Persistence repository tests
- [ ] 3.3: Verify existing migration compatibility
- [ ] 4.1: API controller
- [ ] 4.2: DI registration + factory fakes
- [ ] 4.3: API + Swagger tests
- [ ] 5.1-5.2: End-to-end verification

## Workload / PR Boundary

- **Mode**: Chained PR slice (stacked-to-main)
- **Current work unit**: Unit 1 — Domain + Command Foundation
- **Boundary**: Domain entity lifecycle + application contracts only
- **Estimated review budget**: ~250 lines changed (domain 60, tests 120, contracts 70)
