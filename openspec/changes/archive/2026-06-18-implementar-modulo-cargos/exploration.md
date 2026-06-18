## Exploration: Implementar el módulo de Cargos

### Current State

The Cargo capability already exists as a read-only resource across the Clean Architecture layers. Domain has `Cargo` under `SGV.Dominio.Organizacion`, persistence maps `CargoEntity` to the `Cargos` table, Infrastructure registers `CargoRepository`, Application exposes `ICargoServicioConsulta`/`CargoServicioConsulta`, and API exposes anonymous `GET /api/v1/cargos` plus `GET /api/v1/cargos/{id:guid}`. Existing OpenSpec requirements currently state that roles/cargos remain read-only, so a full Cargo module with write operations will require a spec change rather than only implementation.

### Affected Areas

- `src/SGV.Dominio/Organizacion/Cargo.cs` — existing aggregate/entity to extend or keep as the core model for Cargo invariants.
- `src/SGV.Aplicacion/Organizacion/Consultas/*Cargo*` — current read-only query service and repository contracts to preserve.
- `src/SGV.Api/Controllers/CargosController.cs` — currently read-only controller; write endpoints would mirror the organizational-unit CRUD style.
- `src/SGV.Infraestructura/Persistencia/Entidades/CargoEntity.cs` — EF persistence model with audit, soft-delete, `Codigo`, `Nombre`, `Nivel`, `Descripcion`, `IsActive`, and navigation collections.
- `src/SGV.Infraestructura/Persistencia/Configuraciones/CargoConfiguracion.cs` — MySQL/Pomelo mapping for `Cargos`, generated-column unique active code, and `Nombre` index.
- `src/SGV.Infraestructura/Persistencia/Repositorios/CargoRepository.cs` — currently read-only and filtering `IsActive`; write methods would need to be added if CRUD is in scope.
- `src/SGV.Infraestructura/Persistencia/Mapeos/*Mapper.cs` — currently maps Cargo from persistence to domain; write support would need domain-to-entity mapping for Cargo.
- `src/SGV.Infraestructura/DependencyInjection.cs` — registers Cargo query service/repository; command service would be registered here if introduced.
- `tests/SGV.Tests/Aplicacion/Organizacion/CargoServicioConsultaTests.cs` — existing application query test pattern.
- `tests/SGV.Tests/Api/CargosControllerTests.cs` — existing API read-only test pattern and fake service location.
- `tests/SGV.Tests/Persistencia/CargoRepositoryTests.cs` — existing MySQL repository test pattern.
- `openspec/specs/sgv-readonly-api/spec.md` — currently declares roles/cargos read-only, which conflicts with future Cargo writes unless modified by delta spec.
- `openspec/specs/sgv-database/spec.md` — already defines cargos as reusable role types and excludes direct coupling from concrete puestos.

### Approaches

1. **Promote Cargo to managed CRUD module** — keep the existing table/model and add create, update, soft-delete, reactivate, and optionally filtered/paginated queries for cargos.
   - Pros: mirrors `UnidadOrganizativa` CRUD, reuses existing schema, preserves read contracts, gives a complete module boundary now.
   - Cons: requires modifying the read-only API requirement for cargos; deletion/reactivation rules must define how future `Puesto` references are handled.
   - Effort: Medium

2. **Keep Cargo read-only and only formalize the module boundary** — add a Cargo-specific spec/proposal around the existing query behavior without write operations.
   - Pros: lowest risk and aligns with current `sgv-readonly-api` requirement.
   - Cons: does not satisfy the likely intent of “implement module” if management operations are expected.
   - Effort: Low

### Recommendation

Proceed with Approach 1 if “módulo de Cargos” means operational management. The proposal/spec should explicitly supersede the current read-only restriction for cargos only, preserve the existing query contract, and define Cargo writes independently from Habilidades and Puestos. The write model should mirror `UnidadOrganizativa` patterns: request records, FluentValidation, command result/error type, command service, repository write methods, UnitOfWork, API ProblemDetails, soft-delete/reactivate, and tests across Domain/Application/API/Persistence.

### Risks

- Current OpenSpec says roles/cargos are read-only; the next proposal must explicitly change that requirement for cargos.
- `Cargo` already has `Habilidades` and `Puestos` navigation collections plus `AgregarHabilidad`; this change must not expose skill or position management yet.
- Deleting a Cargo may conflict with current/future `Puesto` references. Even if Puestos are out of scope, the existing schema has `Puestos.CargoId`, so the spec must define whether Cargo deletion is blocked when active puestos exist or postponed until Puestos module integration.
- MySQL unique active code uses a generated column (`ActiveCodigoUnique`) because filtered indexes are unsupported; any code uniqueness tests and migrations must preserve that pattern.
- Existing `CargoRepository` is read-only; adding writes requires mapper support and tests to avoid leaking EF entities into Domain/Application.

### Ready for Proposal

Yes — tell the user that the codebase already contains a read-only Cargo slice and persistence table. The proposal should focus on converting Cargo into a managed module while explicitly excluding Habilidades and Puestos behavior, except for defensive non-goal boundaries and deletion rules involving existing references.
