## Exploration: Implementar módulo UnidadOrganizativa. Implementar el CRUD completo.

### Current State
SGV already has a UnidadOrganizativa domain model, EF persistence entity, MySQL mapping, repository, query service, DTO, controller, and tests, but the public API is explicitly read-only. `UnidadesOrganizativasController` exposes only `GET /api/v1/unidades-organizativas` and `GET /api/v1/unidades-organizativas/{id}`. The current OpenSpec read-only API requires supported resources to not expose create, update, or delete operations, so complete CRUD is a behavior change that must modify or supersede that contract before implementation.

### Affected Areas
- `openspec/specs/sgv-readonly-api/spec.md` — currently forbids create/update/delete for organizational units; CRUD requires a spec change.
- `openspec/specs/sgv-database/spec.md` — defines hierarchy, self-parent prevention, GUID identifiers, and MySQL/Pomelo constraints relevant to writes.
- `src/SGV.Dominio/Organizacion/UnidadOrganizativa.cs` — already contains constructors and domain methods for data changes, parent changes, validity dates, and deactivation.
- `src/SGV.Aplicacion/Organizacion/Consultas/*` — currently query-only service/repository/DTO contracts; write use cases and request/command models are absent.
- `src/SGV.Api/Controllers/UnidadesOrganizativasController.cs` — currently read-only endpoints; would need POST/PUT/PATCH/DELETE behavior and response codes.
- `src/SGV.Infraestructura/Persistencia/Repositorios/UnidadOrganizativaRepository.cs` — currently inherits read-only repository and uses `AsNoTracking`; write persistence methods are absent.
- `src/SGV.Infraestructura/Persistencia/Configuraciones/UnidadOrganizativaConfiguracion.cs` — enforces unique active code, parent FK restrict delete, self-parent check, and indexes.
- `src/SGV.Infraestructura/DependencyInjection.cs` — currently registers read-only repositories/query services plus UnitOfWork; CRUD services/repositories would need registration.
- `tests/SGV.Tests/Api/UnidadesOrganizativasControllerTests.cs` — tests only read behavior and no authorization requirement; write endpoint tests are missing.
- `tests/SGV.Tests/Persistencia/UnidadOrganizativaRepositoryTests.cs` — covers read filters and lookup only; create/update/soft-delete constraints are missing.

### Approaches
1. **Use-case CRUD in Application layer** — add explicit create/update/delete application services or commands for UnidadOrganizativa, backed by a write repository and UnitOfWork, while keeping domain and EF boundaries intact.
   - Pros: Fits Clean Architecture, keeps API thin, centralizes validation/use-case behavior, preserves persistence model boundary.
   - Cons: More files and tests; likely exceeds the 400-line review budget if delivered as one PR.
   - Effort: High

2. **Controller-driven CRUD over DbContext/repository** — add write endpoints that directly call infrastructure persistence operations with minimal application abstraction.
   - Pros: Smaller initial implementation and faster to build.
   - Cons: Breaks architectural direction by putting use-case behavior near API/infrastructure, increases coupling, and makes validation/audit semantics harder to test cleanly.
   - Effort: Medium

### Recommendation
Use the application-layer use-case approach. The codebase already separates Domain, Application, Infrastructure, and API; current persistence specs require Domain to remain EF-agnostic and SGV tables to use `*Entity` infrastructure types. Because CRUD conflicts with the current read-only API spec and will probably exceed 400 changed lines with tests, the next phases should explicitly update the API capability contract and plan reviewable slices rather than attempting a single large implementation.

### Risks
- CRUD directly conflicts with `sgv-readonly-api` requirements that write operations must not be exposed.
- Parent changes need more than self-parent validation; moving a node under a descendant could create cycles unless explicitly checked.
- Delete semantics must be defined: hard delete is constrained by child units/puestos and FK restrict; soft delete aligns better with `IsDeleted` and current read filters.
- Unique active `Codigo` is enforced through a MySQL computed-column unique index; duplicate-code failures need predictable application/API handling.
- Full CRUD with API, application, infrastructure, migrations/snapshot checks if needed, and tests is likely over the 400-line review budget.

### Ready for Proposal
Yes — tell the user this is not just adding endpoints: it changes the existing read-only API contract. The proposal should define CRUD scope, soft-delete behavior, hierarchy cycle rules, validation/error contracts, and whether delivery should be split into reviewable slices because the estimated change exceeds the review budget.
