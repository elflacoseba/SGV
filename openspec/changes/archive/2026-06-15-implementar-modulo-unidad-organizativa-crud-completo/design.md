# Design: Implement UnidadOrganizativa CRUD

## Technical Approach

Add write use cases in `SGV.Aplicacion` and keep `UnidadesOrganizativasController` thin. Infrastructure will extend the existing repository around `UnidadOrganizativaEntity`, using tracked EF Core queries only for writes and the current `IUnitOfWork` for commits. Reads continue to use `UnidadOrganizativaDto` and active filters. The design satisfies `unidad-organizativa-crud`, narrows the `sgv-readonly-api` exception to organizational units, and uses existing MySQL constraints from `sgv-database`.

Full implementation is expected to exceed the 400-line review budget because it spans API contracts, application use cases, repository writes, DI, and tests. It should be sliced before apply.

## Architecture Decisions

| Decision | Choice | Alternatives considered | Rationale |
|---|---|---|---|
| Use case boundary | Create `src/SGV.Aplicacion/Organizacion/Comandos` with explicit create, update, parent-change, and soft-delete services/contracts. | Put write logic in controller or DbContext directly. | Preserves Clean Architecture and keeps validation/transaction behavior testable outside HTTP. |
| Repository writes | Extend `IUnidadOrganizativaRepository` with write-specific members or split into `IUnidadOrganizativaWriteRepository`. | Reuse `IReadOnlyRepository` only. | Current repository is intentionally `AsNoTracking`; writes need tracked entity access, duplicate checks, and descendant checks. |
| Soft delete | Mark both `IsDeleted = true` and `IsActive = false` for delete. | Only call domain `Desactivar()`. | Reads filter both flags, but current unique active-code index is based on `IsDeleted`; setting only inactive would not free `Codigo`. |
| Error contract | Return application results/errors mapped by API to `400`, `404`, and `409` with `ProblemDetails`. | Throw raw exceptions to middleware. | Specs require predictable validation/conflict errors and no partial commits. |

## Data Flow

```text
HTTP request
  -> UnidadesOrganizativasController
  -> Aplicacion Organizacion Comandos service
  -> IUnidadOrganizativaRepository + IUnitOfWork
  -> SgvDbContext / UnidadesOrganizativas
  -> UnidadOrganizativaDto or ProblemDetails
```

Parent changes validate: target exists, parent exists when provided, parent is not self, and parent is not a descendant of the target before committing.

## File Changes

| File | Action | Description |
|------|--------|-------------|
| `src/SGV.Aplicacion/Organizacion/Comandos/UnidadOrganizativaRequests.cs` | Create | Command/request records for create, update, parent change, and delete intent. |
| `src/SGV.Aplicacion/Organizacion/Comandos/IUnidadOrganizativaServicioComandos.cs` | Create | Application write service contract returning success DTOs or typed errors. |
| `src/SGV.Aplicacion/Organizacion/Comandos/UnidadOrganizativaServicioComandos.cs` | Create | Use cases, validation orchestration, repository calls, and unit-of-work commit. |
| `src/SGV.Aplicacion/Organizacion/Consultas/IUnidadOrganizativaRepository.cs` | Modify | Add write support or replace with read/write-specific contracts for UnidadOrganizativa. |
| `src/SGV.Infraestructura/Persistencia/Repositorios/UnidadOrganizativaRepository.cs` | Modify | Add tracked create/update/delete queries, duplicate-code checks, descendant traversal checks, and entity/domain mapping. |
| `src/SGV.Infraestructura/DependencyInjection.cs` | Modify | Register command service and any new repository contract. |
| `src/SGV.Api/Controllers/UnidadesOrganizativasController.cs` | Modify | Add `POST`, `PUT`, parent-change `PATCH`, and `DELETE` endpoints; map application errors to HTTP responses. |
| `src/SGV.Api/Program.cs` | Modify | Update Swagger title/description from strictly read-only to include organizational-unit writes. |
| `tests/SGV.Tests/Aplicacion/Organizacion/UnidadOrganizativaServicioComandosTests.cs` | Create | Unit tests for write validation and no-commit failures. |
| `tests/SGV.Tests/Persistencia/UnidadOrganizativaRepositoryTests.cs` | Modify | Add MySQL coverage for unique active code, soft delete reuse, tracked updates, and hierarchy checks. |
| `tests/SGV.Tests/Api/UnidadesOrganizativasControllerTests.cs` | Modify | Add endpoint status/contract tests using fake command service. |
| `tests/SGV.Tests/Api/ApiWebApplicationFactory.cs` | Modify | Register fake command service for API tests. |
| `tests/SGV.Tests/Api/SwaggerConfigurationTests.cs` | Modify | Assert organizational-unit write actions are discoverable while other resources remain read-only. |

## Interfaces / Contracts

HTTP contracts should use request records at the API/application boundary and keep `UnidadOrganizativaDto` as the success response. Required operations:

- `POST /api/v1/unidades-organizativas` -> `201 Created` with DTO.
- `PUT /api/v1/unidades-organizativas/{id}` -> `200 OK` with DTO.
- `PATCH /api/v1/unidades-organizativas/{id}/unidad-padre` -> `200 OK` with DTO.
- `DELETE /api/v1/unidades-organizativas/{id}` -> `204 NoContent`.

Errors: validation/domain errors -> `400`; missing unit/parent -> `404`; duplicate active `Codigo` -> `409`; invalid descendant parent -> `400` or `409` consistently documented as `ProblemDetails`.

## Testing Strategy

| Layer | What to Test | Approach |
|-------|-------------|----------|
| Unit | Command service validation, duplicate pre-checks, parent cycle rejection, no `SaveChangesAsync` on failures. | xUnit fakes for repository and unit of work. |
| Integration | MySQL mapping, active-code uniqueness, soft-delete code reuse, tracked updates, hierarchy queries. | Existing `[MySqlFact]` repository tests against `SgvDbContextFactory`. |
| API | CRUD status codes, DTO/ProblemDetails contracts, Swagger discoverability, unrelated resources still read-only. | `WebApplicationFactory` with fake query/command services. |

## Migration / Rollout

No migration is expected because `UnidadesOrganizativas` already has hierarchy FK, self-parent check, soft-delete fields, and computed unique `ActiveCodigoUnique`. Verify the current migration snapshot before apply; if drift is found, add a separate migration slice.

Rollout should be chained: application/persistence write core first, then API endpoints/docs, then integration hardening.

## Open Questions

None.
