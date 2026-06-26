# Proposal: Implement Occupations Module

## Intent

Current SGV supports `Ocupacion` only at domain + EF storage level. It lacks an application slice, repository/mappers, API contracts, controller, DI wiring, and tests, so the repo cannot manage occupations as a historical assignment resource.

## Scope

### In Scope
- Add a standalone `api/v1/ocupaciones` module with list, detail, create, update, finalize, reactivate, and logical delete operations.
- Define lifecycle semantics for historical assignments between `Persona` and `Puesto`.
- Add application/infrastructure support, Swagger exposure, and test coverage for the module.

### Out of Scope
- Changes to vacancy workflows, permissions, or authentication.
- New schema fields unless later phases prove they are required.

## Non-goals

- Turning Ocupaciones into a simple catalog.
- Embedding occupation writes into `PersonasController` or `PuestosController`.
- Physical deletion of occupation records.

## Capabilities

### New Capabilities
- `ocupacion-management`: Historical occupation lifecycle, validation, and standalone API contract.

### Modified Capabilities
- `sgv-readonly-api`: add discoverable `/api/v1/ocupaciones` read/write endpoints and response contracts.
- `sgv-database`: document occupation lifecycle persistence rules, active uniqueness, and history preservation.

## Approach

Use a dedicated `OcupacionesController` backed by command/query services and repository abstractions. Treat `Ocupacion` as a historical assignment: create starts an active assignment, update edits only active records, finalize closes an assignment with `FechaFin`, reactivate restores a logically deleted or finalized record only if business validations still pass, and logical delete hides the record from active views without removing history.

## Affected Areas

| Area | Impact | Description |
|------|--------|-------------|
| `src/SGV.Dominio/Ocupaciones/Ocupacion.cs` | Modified | Add explicit update/reactivate/delete lifecycle rules. |
| `src/SGV.Aplicacion/Ocupaciones/**` | New | Requests, DTOs, validators, errors, command/query services, repository contract. |
| `src/SGV.Api/Controllers/OcupacionesController.cs` | New | Standalone HTTP resource. |
| `src/SGV.Infraestructura/Persistencia/**Ocupacion*` | Modified | Repository, mappers, and possible query support. |
| `src/SGV.Infraestructura/DependencyInjection.cs` | Modified | Register occupation services. |
| `tests/SGV.Tests/{Dominio,Api,Persistencia}/**` | Modified | Lifecycle, conflict, and Swagger coverage. |

## Risks

| Risk | Likelihood | Mitigation |
|------|------------|------------|
| Reactivation semantics conflict with active uniqueness or inactive Persona/Puesto | High | Lock rules in spec/design before coding and test `409` paths. |
| Domain lacks lifecycle methods beyond finalize | High | Extend domain first, then align app/API contracts. |
| Unnecessary migration churn | Medium | Reuse existing schema unless later phases prove a gap. |

## Rollback Plan

Revert the controller, application services, DI wiring, repository/mapping changes, and any new tests. Avoid schema changes unless later phases justify them.

## Dependencies

- Existing `Ocupaciones` table, uniqueness indexes, and enum persistence migration.
- Active `Persona` and `Puesto` records for create/reactivate validations.

## Success Criteria

- [ ] Proposal-backed specs can define the full occupation lifecycle without ambiguity.
- [ ] Later implementation can expose `/api/v1/ocupaciones` with documented list/detail/create/update/finalize/reactivate/delete behavior.
- [ ] Conflict cases for active uniqueness, inactive references, and finalized non-editability are testable.
