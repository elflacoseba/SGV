# Tasks: Implement UnidadOrganizativa CRUD

## Review Workload Forecast

| Field | Value |
|-------|-------|
| Estimated changed lines | 600-900 |
| 400-line budget risk | High |
| Chained PRs recommended | Yes |
| Suggested split | PR 1: core + unit tests; PR 2: API + docs; PR 3: integration tests + verification |
| Delivery strategy | ask-on-risk |
| Chain strategy | pending |

Decision needed before apply: Yes
Chained PRs recommended: Yes
Chain strategy: pending
400-line budget risk: High

### Suggested Work Units

| Unit | Goal | Likely PR | Notes |
|------|------|-----------|-------|
| 1 | Application/persistence write core and command unit tests | PR 1 | Base: `main`; includes spec deltas |
| 2 | API endpoints, Swagger docs, and controller tests | PR 2 | Base: `main`; depends on PR 1 merged |
| 3 | MySQL integration tests and final verification | PR 3 | Base: `main`; depends on PR 2 merged |

## Phase 1: Spec Deltas

- [x] 1.1 Update `openspec/specs/sgv-readonly-api/spec.md` to allow organizational-unit writes while keeping roles, positions, and skills read-only.
- [x] 1.2 Update `openspec/specs/sgv-database/spec.md` with active-code uniqueness, soft-delete reuse, and descendant-cycle prevention.

## Phase 2: Application & Persistence Core

- [ ] 2.1 Create `src/SGV.Aplicacion/Organizacion/Comandos/UnidadOrganizativaRequests.cs` with request records for create, update, parent change, and delete intent.
- [ ] 2.2 Create `src/SGV.Aplicacion/Organizacion/Comandos/IUnidadOrganizativaServicioComandos.cs` defining typed results and errors.
- [ ] 2.3 Create `src/SGV.Aplicacion/Organizacion/Comandos/UnidadOrganizativaServicioComandos.cs` with validation, duplicate-code pre-checks, and descendant-cycle rejection.
- [ ] 2.4 Modify `src/SGV.Aplicacion/Organizacion/Consultas/IUnidadOrganizativaRepository.cs` to add write-specific members.
- [ ] 2.5 Modify `src/SGV.Infraestructura/Persistencia/Repositorios/UnidadOrganizativaRepository.cs` for tracked create/update/delete, duplicate checks, and descendant traversal.
- [ ] 2.6 Modify `src/SGV.Infraestructura/DependencyInjection.cs` to register the command service and repository contract.

## Phase 3: API Integration

- [ ] 3.1 Modify `src/SGV.Api/Controllers/UnidadesOrganizativasController.cs` to add `POST`, `PUT`, parent-change `PATCH`, and `DELETE` endpoints.
- [ ] 3.2 Map application errors to `ProblemDetails` with status `400`, `404`, or `409` consistently.
- [ ] 3.3 Modify `src/SGV.Api/Program.cs` to update Swagger title/description to include organizational-unit writes.

## Phase 4: Testing

- [ ] 4.1 Create `tests/SGV.Tests/Aplicacion/Organizacion/UnidadOrganizativaServicioComandosTests.cs` (RED): failing tests for validation, duplicate code, cycles, and no-commit failures.
- [ ] 4.2 Make command-service tests pass (GREEN) and refactor.
- [ ] 4.3 Modify `tests/SGV.Tests/Persistencia/UnidadOrganizativaRepositoryTests.cs` to cover MySQL writes, soft-delete code reuse, active-code uniqueness, and hierarchy checks.
- [ ] 4.4 Modify `tests/SGV.Tests/Api/UnidadesOrganizativasControllerTests.cs` to assert CRUD status codes and response contracts using a fake command service.
- [ ] 4.5 Modify `tests/SGV.Tests/Api/ApiWebApplicationFactory.cs` to register the fake command service for API tests.
- [ ] 4.6 Modify `tests/SGV.Tests/Api/SwaggerConfigurationTests.cs` to verify organizational-unit write actions are discoverable and other resources remain read-only.

## Phase 5: Verification & Rollout

- [ ] 5.1 Run `dotnet build` and `dotnet test` and fix any failures.
- [ ] 5.2 Verify no EF migration drift; create a separate migration slice if the current snapshot differs from the design assumptions.
