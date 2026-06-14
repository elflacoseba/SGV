# Design: Add API Project

## Technical Approach

Add `SGV.Api` as the first external read-only HTTP slice. The API uses traditional MVC controllers, Swagger/OpenAPI, and the existing Clean Architecture boundaries: controllers call application services, services use repository abstractions and `IUnitOfWork`, infrastructure implements those abstractions with `SgvDbContext` and Pomelo/MySQL. No authentication, authorization, Identity endpoints, writes, or schema redesign are introduced.

## Architecture Decisions

| Decision | Choice | Alternatives considered | Rationale |
|---|---|---|---|
| HTTP style | MVC controllers in `src/SGV.Api/Controllers/` | Minimal APIs | Proposal explicitly requires traditional controllers; controllers remain thin and testable. |
| Data contracts | Response DTOs in `src/SGV.Aplicacion/Consultas/*/Dtos/` | Return domain/EF entities | Prevents leaking audit fields, EF navigation shape, and future persistence changes. |
| Query layer | Application read services plus entity-specific repositories | Direct `DbContext` in controllers/services; broad generic-only repository | Keeps Clean boundaries while limiting the generic repository to simple reusable operations. Entity repositories own includes/projections/order. |
| Unit of Work | `IUnitOfWork` over `SaveChangesAsync` even though v1 is read-only | No UoW | Matches requested architecture and prepares later slices; read endpoints should not call save. |
| Anonymous user | Register API `IUsuarioActual` returning `null` user and request correlation id | Disable audit interceptor; require auth now | Keeps infrastructure composition valid without adding auth outside scope. |
| Swagger | Enable Swagger for local/dev v1 with read-only endpoints only | Hide Swagger until auth | Useful for first API validation; risk is bounded by no sensitive/admin/write endpoints. |

## Data Flow

```text
GET /api/v1/{resource}
  -> Controller
  -> Application read service
  -> Entity repository / IReadOnlyRepository<T>
  -> SgvDbContext (AsNoTracking, IsDeleted=false, IsActive=true where available)
  -> DTO list/detail response
```

`Puestos` may include `UnidadOrganizativa` and `Cargo` summary fields through `PuestoRepository`; other resources return flat DTOs unless specs later require hierarchy.

## File Changes

| File | Action | Description |
|---|---|---|
| `SGV.slnx` | Modify | Add `src/SGV.Api/SGV.Api.csproj`. |
| `src/SGV.Api/SGV.Api.csproj` | Create | ASP.NET Core API project targeting `net10.0`, referencing Application and Infrastructure. |
| `src/SGV.Api/Program.cs` | Create | Configure controllers, Swagger, ProblemDetails, MySQL connection, repositories, services, UoW, and anonymous user. |
| `src/SGV.Api/appsettings.Development.json` | Create | Local MySQL connection string placeholder. |
| `src/SGV.Api/Controllers/UnidadesOrganizativasController.cs` | Create | `GET /api/v1/unidades-organizativas`, `GET /api/v1/unidades-organizativas/{id}`. |
| `src/SGV.Api/Controllers/CargosController.cs` | Create | Read-only cargos endpoints. |
| `src/SGV.Api/Controllers/PuestosController.cs` | Create | Read-only puestos endpoints with related summaries. |
| `src/SGV.Api/Controllers/SkillsController.cs` | Create | Read-only skills mapped from `Habilidad`. |
| `src/SGV.Api/Seguridad/UsuarioActualAnonimo.cs` | Create | API adapter for `IUsuarioActual`. |
| `src/SGV.Aplicacion/Comun/Persistencia/IReadOnlyRepository.cs` | Create | Basic `GetByIdAsync`/`ListAsync` contract with cancellation. |
| `src/SGV.Aplicacion/Comun/Persistencia/IUnitOfWork.cs` | Create | `SaveChangesAsync` abstraction. |
| `src/SGV.Aplicacion/Organizacion/*` and `src/SGV.Aplicacion/Habilidades/*` | Create | DTOs, repository interfaces, and read services. |
| `src/SGV.Infraestructura/Persistencia/Repositorios/*` | Create | EF repositories using `AsNoTracking` and active/not-deleted filters. |
| `src/SGV.Infraestructura/DependencyInjection.cs` | Create | Scoped DI for `SgvDbContext`, interceptor, repositories, services, UoW. |
| `tests/SGV.Tests/Api/*` | Create | Controller/API contract tests. |
| `tests/SGV.Tests/Aplicacion/*` | Create | Service mapping and repository-call tests. |
| `tests/SGV.Tests/Persistencia/*RepositoryTests.cs` | Create | MySQL-backed repository integration tests. |

## Interfaces / Contracts

Contracts expose async methods with `CancellationToken`: `IReadOnlyRepository<T>`, `IUnidadOrganizativaRepository`, `ICargoRepository`, `IPuestoRepository`, `IHabilidadRepository`, `IUnitOfWork`, and read services such as `IUnidadesOrganizativasServicioConsulta`. DTOs include `Id`, `Codigo`, `Nombre`, optional descriptive fields, and related IDs/summaries where needed; they omit audit and Identity data.

## Testing Strategy

| Layer | What to Test | Approach |
|---|---|---|
| API | Routes, status codes, DTO shape, no auth requirement, Swagger registration | xUnit with `WebApplicationFactory` after adding `Microsoft.AspNetCore.Mvc.Testing`. |
| Application | Services map repository results to DTOs and return not-found cleanly | Strict TDD unit tests with fake repositories. |
| Infrastructure | Repositories filter soft-deleted/inactive data and use Pomelo-compatible queries | MySQL integration tests using existing `MySqlFactAttribute`. |

## Migration / Rollout

No data migration required. Roll out by adding the API project, applying existing migrations/seed to local MySQL, then running `dotnet build`, `dotnet test`, and launching Swagger locally. Rollback is removing `SGV.Api`, DI registrations, new application/infrastructure contracts, and tests.

## Open Questions

- [ ] Should Swagger be enabled only in Development, or also in non-production internal environments?
- [ ] Are endpoint collection responses expected to be unpaged for seed-sized local data, or should v1 include pagination immediately?
