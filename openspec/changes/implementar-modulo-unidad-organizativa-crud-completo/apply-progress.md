# Apply Progress: Implement UnidadOrganizativa CRUD

**Change**: implementar-modulo-unidad-organizativa-crud-completo
**Mode**: Strict TDD
**Current batch**: PR 1 — spec deltas + application/persistence core + command unit tests
**Branch**: `feat/unidad-organizativa-crud-pr1`
**Base**: `develop`

## Completed Tasks

- [x] 1.1 Update `openspec/specs/sgv-readonly-api/spec.md` to allow organizational-unit writes while keeping roles, positions, and skills read-only.
- [x] 1.2 Update `openspec/specs/sgv-database/spec.md` with active-code uniqueness, soft-delete reuse, and descendant-cycle prevention.
- [x] 2.1 Create `src/SGV.Aplicacion/Organizacion/Comandos/UnidadOrganizativaRequests.cs` with request records for create, update, parent change, and delete intent.
- [x] 2.2 Create `src/SGV.Aplicacion/Organizacion/Comandos/IUnidadOrganizativaServicioComandos.cs` defining typed results and errors.
- [x] 2.3 Create `src/SGV.Aplicacion/Organizacion/Comandos/UnidadOrganizativaServicioComandos.cs` with validation, duplicate-code pre-checks, and descendant-cycle rejection.
- [x] 2.4 Modify `src/SGV.Aplicacion/Organizacion/Consultas/IUnidadOrganizativaRepository.cs` to add write-specific members.
- [x] 2.5 Modify `src/SGV.Infraestructura/Persistencia/Repositorios/UnidadOrganizativaRepository.cs` for tracked create/update/delete, duplicate checks, and descendant traversal.
- [x] 2.6 Modify `src/SGV.Infraestructura/DependencyInjection.cs` to register the command service and repository contract.
- [x] 4.1 Create `tests/SGV.Tests/Aplicacion/Organizacion/UnidadOrganizativaServicioComandosTests.cs` (RED): failing tests for validation, duplicate code, cycles, and no-commit failures.
- [x] 4.2 Make command-service tests pass (GREEN) and refactor.
- [x] 5.1 Run `dotnet build` and `dotnet test` and fix any failures.

## Files Changed

| File | Action | What Was Done |
|------|--------|---------------|
| `openspec/specs/sgv-readonly-api/spec.md` | Modified | Allowed organizational-unit writes; kept roles/positions/skills read-only. |
| `openspec/specs/sgv-database/spec.md` | Modified | Added active-code uniqueness, soft-delete reuse, descendant-cycle prevention. |
| `src/SGV.Aplicacion/Organizacion/Comandos/UnidadOrganizativaRequests.cs` | Created | Request records for create, update, parent-change. |
| `src/SGV.Aplicacion/Organizacion/Comandos/UnidadOrganizativaCommandResult.cs` | Created | Typed result/error contract for command operations. |
| `src/SGV.Aplicacion/Organizacion/Comandos/IUnidadOrganizativaServicioComandos.cs` | Created | Service contract. |
| `src/SGV.Aplicacion/Organizacion/Comandos/UnidadOrganizativaServicioComandos.cs` | Created | Use cases, validation, duplicate and cycle checks. |
| `src/SGV.Aplicacion/Organizacion/Consultas/IUnidadOrganizativaRepository.cs` | Modified | Added write members (`Add`, `Update`, `Delete`, `ExistsActiveCode`, `IsDescendant`). |
| `src/SGV.Infraestructura/Persistencia/Repositorios/UnidadOrganizativaRepository.cs` | Modified | Implemented tracked writes and hierarchy checks. |
| `src/SGV.Infraestructura/Persistencia/Mapeos/DomainToPersistenceMapper.cs` | Created | Domain-to-entity mapping for writes. |
| `src/SGV.Infraestructura/DependencyInjection.cs` | Modified | Registered command service. |
| `tests/SGV.Tests/Aplicacion/Organizacion/UnidadOrganizativaServicioComandosTests.cs` | Created | Unit tests with fakes for repository and UoW. |
| `tests/SGV.Tests/Aplicacion/Organizacion/UnidadOrganizativaServicioConsultaTests.cs` | Modified | Updated existing fake to satisfy expanded repository interface. |
| `openspec/changes/implementar-modulo-unidad-organizativa-crud-completo/tasks.md` | Modified | Marked PR 1 tasks complete. |

## TDD Cycle Evidence

| Task / Behavior | Test File | Layer | Safety Net | RED | GREEN | TRIANGULATE | REFACTOR |
|-----------------|-----------|-------|------------|-----|-------|-------------|----------|
| 4.1/4.2 Create unit | `UnidadOrganizativaServicioComandosTests.cs` | Unit | ✅ 59/59 baseline | ✅ Written first | ✅ Passed | ✅ 4 cases (valid, duplicate, missing parent, invalid date) | ✅ Unified exception handling |
| 4.1/4.2 Update unit | `UnidadOrganizativaServicioComandosTests.cs` | Unit | ✅ 59/59 baseline | ✅ Written first | ✅ Passed | ✅ 3 cases (valid, duplicate, not found) | ✅ Unified exception handling |
| 4.1/4.2 Parent change | `UnidadOrganizativaServicioComandosTests.cs` | Unit | ✅ 59/59 baseline | ✅ Written first | ✅ Passed | ✅ 4 cases (valid, self-parent, descendant, missing parent) | ✅ Pre-check before domain call |
| 4.1/4.2 Soft delete | `UnidadOrganizativaServicioComandosTests.cs` | Unit | ✅ 59/59 baseline | ✅ Written first | ✅ Passed | ✅ 2 cases (existing, not found) | ✅ Marked domain inactive before persistence |

### Test Summary
- **Total tests written**: 13
- **Total tests passing**: 13
- **Layers used**: Unit (13)
- **Approval tests**: None — no refactoring tasks
- **Pure functions created**: `MapToDto`

## Deviations from Design

None — implementation matches design. The error contract uses `UnidadOrganizativaCommandResult` with typed errors (`NotFound`, `Conflict`, `Validation`) as recommended.

## Issues Found

1. The domain method `DefinirVigencia` throws `InvalidOperationException`, while `CambiarDatos` throws `ArgumentException`. The command service catches both exception types and maps them to `Validation` errors. This was discovered during the TDD cycle when the invalid-date test failed with `InvalidOperationException` instead of the expected `ArgumentException`.
2. No EF migration drift was detected. The existing `UnidadesOrganizativas` table, computed `ActiveCodigoUnique` index, and self-parent check constraint already satisfy the new spec requirements.

## Remaining Tasks

- [ ] 3.1 Modify `src/SGV.Api/Controllers/UnidadesOrganizativasController.cs` to add `POST`, `PUT`, parent-change `PATCH`, and `DELETE` endpoints.
- [ ] 3.2 Map application errors to `ProblemDetails` with status `400`, `404`, or `409` consistently.
- [ ] 3.3 Modify `src/SGV.Api/Program.cs` to update Swagger title/description to include organizational-unit writes.
- [ ] 4.3 Modify `tests/SGV.Tests/Persistencia/UnidadOrganizativaRepositoryTests.cs` to cover MySQL writes, soft-delete code reuse, active-code uniqueness, and hierarchy checks.
- [ ] 4.4 Modify `tests/SGV.Tests/Api/UnidadesOrganizativasControllerTests.cs` to assert CRUD status codes and response contracts using a fake command service.
- [ ] 4.5 Modify `tests/SGV.Tests/Api/ApiWebApplicationFactory.cs` to register the fake command service for API tests.
- [ ] 4.6 Modify `tests/SGV.Tests/Api/SwaggerConfigurationTests.cs` to verify organizational-unit write actions are discoverable and other resources remain read-only.
- [ ] 5.2 Verify no EF migration drift; create a separate migration slice if the current snapshot differs from the design assumptions.

## Workload / PR Boundary

- **Mode**: Chained PR slice (stacked-to-main)
- **Current work unit**: PR 1 — Application/persistence write core + command unit tests + spec deltas
- **Boundary**: Starts from `develop`; ends with command-service unit tests passing and spec deltas committed.
- **Estimated review budget impact**: PR 1 adds ~1,250 lines including spec artifacts and tests. This is above the 400-line ideal; the core code + tests alone are ~800 lines. The remaining API, integration tests, and Swagger verification are intentionally left for PR 2/3.

## Status

11/19 tasks complete. Ready for next batch (PR 2: API integration and controller tests).
