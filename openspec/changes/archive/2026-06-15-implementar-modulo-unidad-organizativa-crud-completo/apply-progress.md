# Apply Progress: Implement UnidadOrganizativa CRUD

**Change**: implementar-modulo-unidad-organizativa-crud-completo
**Mode**: Strict TDD
**Current batch**: PR 2 — API integration + controller tests
**Branch**: `feat/unidad-organizativa-crud-pr2`
**Base**: `feat/unidad-organizativa-crud-pr1` (PR 2 branch from PR 1)
**Delivery**: `size:exception` (maintainer-accepted)

## Completed Tasks

### PR 1 (completed in previous batch)
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

### PR 2 (completed in this batch)
- [x] 3.1 Modify `src/SGV.Api/Controllers/UnidadesOrganizativasController.cs` to add `POST`, `PUT`, parent-change `PATCH`, and `DELETE` endpoints.
- [x] 3.2 Map application errors to `ProblemDetails` with status `400`, `404`, or `409` consistently.
- [x] 3.3 Modify `src/SGV.Api/Program.cs` to update Swagger title/description to include organizational-unit writes.
- [x] 4.3 Modify `tests/SGV.Tests/Persistencia/UnidadOrganizativaRepositoryTests.cs` to cover MySQL writes, soft-delete code reuse, active-code uniqueness, and hierarchy checks.
- [x] 4.4 Modify `tests/SGV.Tests/Api/UnidadesOrganizativasControllerTests.cs` to assert CRUD status codes and response contracts using a fake command service.
- [x] 4.5 Modify `tests/SGV.Tests/Api/ApiWebApplicationFactory.cs` to register the fake command service for API tests.
- [x] 4.6 Modify `tests/SGV.Tests/Api/SwaggerConfigurationTests.cs` to verify organizational-unit write actions are discoverable and other resources remain read-only.
- [x] 5.2 Verify no EF migration drift; create a separate migration slice if the current snapshot differs from the design assumptions.

## Files Changed (PR 2)

| File | Action | What Was Done |
|------|--------|---------------|
| `src/SGV.Api/Controllers/UnidadesOrganizativasController.cs` | Modified | Added POST, PUT, PATCH (parent-change), DELETE endpoints with ProblemDetails error mapping to 400/404/409. |
| `src/SGV.Api/Program.cs` | Modified | Updated Swagger title from "SGV Read-Only API" to "SGV API" with description reflecting write capabilities. |
| `tests/SGV.Tests/Api/ApiWebApplicationFactory.cs` | Modified | Added `FakeUnidadOrganizativaServicioComandos` with delegate-based handlers and registered `IUnidadOrganizativaServicioComandos` in factory. |
| `tests/SGV.Tests/Api/UnidadesOrganizativasControllerTests.cs` | Modified | Added 9 CRUD tests: POST (valid→201, validation→400, duplicate→409), PUT (valid→200, not found→404), PATCH (valid→200, self-parent→400), DELETE (existing→204, not found→404). |
| `tests/SGV.Tests/Api/SwaggerConfigurationTests.cs` | Modified | Added `NonOrgResources_OnlyExposeGetOperations` (replaces the all-GET assertion) and `UnidadesOrganizativas_ExposesWriteOperations`. Updated title assertion to "SGV API". |
| `tests/SGV.Tests/Persistencia/UnidadOrganizativaRepositoryTests.cs` | Modified | Added 7 MySQL integration tests: AddAsync, UpdateAsync, DeleteAsync, ExistsActiveCodeAsync (duplicate true + exclude self false), IsDescendantAsync (true + false), SoftDelete reuses code. |
| `openspec/changes/implementar-modulo-unidad-organizativa-crud-completo/tasks.md` | Modified | Marked PR 2 tasks complete. |

## TDD Cycle Evidence

| Task / Behavior | Test File | Layer | Safety Net | RED | GREEN | TRIANGULATE | REFACTOR |
|-----------------|-----------|-------|------------|-----|-------|-------------|----------|
| 3.1/3.2 POST create | `UnidadesOrganizativasControllerTests.cs` | API | ✅ 90/90 baseline | ✅ Written first | ✅ Passed | ✅ 3 cases (valid→201, validation→400, duplicate→409) | ➖ None needed |
| 3.1/3.2 PUT update | `UnidadesOrganizativasControllerTests.cs` | API | ✅ 90/90 baseline | ✅ Written first | ✅ Passed | ✅ 2 cases (valid→200, not found→404) | ➖ None needed |
| 3.1/3.2 PATCH parent change | `UnidadesOrganizativasControllerTests.cs` | API | ✅ 90/90 baseline | ✅ Written first | ✅ Passed | ✅ 2 cases (valid→200, self-parent→400) | ➖ None needed |
| 3.1/3.2 DELETE soft delete | `UnidadesOrganizativasControllerTests.cs` | API | ✅ 90/90 baseline | ✅ Written first | ✅ Passed | ✅ 2 cases (existing→204, not found→404) | ➖ None needed |
| 4.6 Write ops discoverable | `SwaggerConfigurationTests.cs` | API | ✅ 90/90 baseline | ✅ Written first | ✅ Passed | ✅ 3 path assertions (collection, item, parent) | ➖ None needed |
| 4.6 Read-only resources | `SwaggerConfigurationTests.cs` | API | ✅ 90/90 baseline | ✅ Written first | ✅ Passed | ✅ Skipped org path, verified GET-only on others | ➖ None needed |
| 4.3 MySQL add | `UnidadOrganizativaRepositoryTests.cs` | Integration | ✅ 90/90 baseline | ✅ Written first | ✅ Passed | ➖ Single (create+verify patterns) | ➖ None needed |
| 4.3 MySQL update/delete | `UnidadOrganizativaRepositoryTests.cs` | Integration | ✅ 90/90 baseline | ✅ Written first | ✅ Passed | ✅ 3 cases (update, soft-delete, code reuse) | ➖ None needed |
| 4.3 MySQL hierarchy | `UnidadOrganizativaRepositoryTests.cs` | Integration | ✅ 90/90 baseline | ✅ Written first | ✅ Passed | ✅ 2 cases (descendant true, descendant false) | ➖ None needed |

### Test Summary
- **Total tests written this batch**: 18
- **Total tests passing**: 108 (90 previous + 18 new)
- **Layers used**: API (14: 5 existing + 9 new), Integration (11: 3 existing MySQL + 8 new MySQL), Unit (13 existing)
- **Approval tests**: None — no refactoring tasks
- **Pure functions created**: `ToProblemResult` (error mapping)

## Deviations from Design

None — implementation matches design. The `ToProblemResult` private method in the controller maps `UnidadOrganizativaErrorType` values to HTTP status codes as specified.

## Issues Found

1. ASP.NET Core returns `405 MethodNotAllowed` for routes without matching HTTP verb handlers, and `404 NotFound` for unmapped path segments. Both were confirmed during the RED phase — the new controller routes resolved them all.
2. MySQL was available in the local test environment (8 of 8 new MySQL tests ran and passed). No test skipping occurred.
3. No EF migration drift detected — `dotnet ef migrations has-pending-model-changes` confirmed the snapshot matches the current model.

## Remaining Tasks

None — all 19 tasks are now complete.

## Workload / PR Boundary

- **Mode**: Chained PR slice (stacked-to-main) with `size:exception`
- **Current work unit**: PR 2 — API integration + controller tests
- **Boundary**: Starts from `feat/unidad-organizativa-crud-pr1`; ends with all controller tests passing, Swagger documenting write operations, and migration drift verified.
- **Estimated review budget impact**: PR 2 adds ~500 lines (controller + tests + factory). Within `size:exception`.

## Status

**19/19 tasks complete. Ready for verify.**
