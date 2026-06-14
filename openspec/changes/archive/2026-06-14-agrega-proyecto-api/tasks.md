# Tasks: Add API Project

## Review Workload Forecast

| Field | Value |
|-------|-------|
| Estimated changed lines | 900-1300 |
| 400-line budget risk | High |
| Chained PRs recommended | Yes |
| Suggested split | PR 1 Foundation -> PR 2 Read services/repositories -> PR 3 API surface/tests |
| Delivery strategy | ask-on-risk |
| Chain strategy | stacked-to-develop |

Decision needed before apply: Yes
Chained PRs recommended: Yes
Chain strategy: stacked-to-develop
400-line budget risk: High

### Suggested Work Units

| Unit | Goal | Likely PR | Notes |
|------|------|-----------|-------|
| 1 | Solution/API bootstrap and shared contracts | PR 1 | Can stand alone with build/test wiring |
| 2 | Read services plus EF repositories/UoW | PR 2 | Depends on PR 1; includes unit + MySQL repository tests |
| 3 | Controllers, Swagger, anonymous access, local DB validation | PR 3 | Depends on PR 2; includes API tests |

## Phase 1: Foundation

- [x] 1.1 Create `src/SGV.Api/SGV.Api.csproj`, `src/SGV.Api/Program.cs`, `src/SGV.Api/appsettings.Development.json`, and add the project to `SGV.slnx`.
- [x] 1.2 RED: add `tests/SGV.Tests/Api/SwaggerConfigurationTests.cs` for Swagger registration and anonymous read-only endpoint discovery.
- [x] 1.3 GREEN: register controllers, Swagger, ProblemDetails, MySQL `SgvDbContext`, and `UsuarioActualAnonimo` in `src/SGV.Api/Program.cs` and `src/SGV.Api/Seguridad/UsuarioActualAnonimo.cs`.

## Phase 2: Application Contracts

- [x] 2.1 RED: add `tests/SGV.Tests/Aplicacion/Organizacion/*ServicioConsultaTests.cs` and `Habilidades/HabilidadesServicioConsultaTests.cs` for list/detail, empty collection, and not-found behavior.
- [x] 2.2 GREEN: create `src/SGV.Aplicacion/Comun/Persistencia/IReadOnlyRepository.cs` and `IUnitOfWork.cs` with async/cancellation contracts.
- [x] 2.3 GREEN: create DTOs, repository interfaces, and read services under `src/SGV.Aplicacion/Organizacion/Consultas/*` and `src/SGV.Aplicacion/Habilidades/Consultas/*`.
- [x] 2.4 REFACTOR: keep service mappings consumer-safe and remove audit/internal fields from all DTO contracts.

## Phase 3: Infrastructure Read Model

- [x] 3.1 RED: add MySQL integration tests in `tests/SGV.Tests/Persistencia/*RepositoryTests.cs` covering `IsDeleted`, `IsActive`, and `Puesto` related summaries.
- [x] 3.2 GREEN: implement `ReadOnlyRepository`, `UnitOfWork`, and entity repositories in `src/SGV.Infraestructura/Persistencia/Repositorios/` using `AsNoTracking` and entity-specific includes/order.
- [x] 3.3 GREEN: create `src/SGV.Infraestructura/DependencyInjection.cs` and register repositories/services/UoW/interceptor-compatible dependencies.

## Phase 4: API Endpoints

- [x] 4.1 RED: add `tests/SGV.Tests/Api/*ControllerTests.cs` for collection/detail success, empty collections, DTO shape, and no-auth access.
- [x] 4.2 GREEN: implement `UnidadesOrganizativasController`, `CargosController`, `PuestosController`, and `SkillsController` in `src/SGV.Api/Controllers/` with GET-only actions.
- [x] 4.3 REFACTOR: verify Swagger exposes only GET operations for `/api/v1/unidades-organizativas`, `/cargos`, `/puestos`, and `/skills`.

## Phase 5: Verification

- [x] 5.1 Run `dotnet build` and `dotnet test`; fix failures without expanding API scope beyond read-only.
- [x] 5.2 Launch `src/SGV.Api` against local MySQL and verify Swagger plus live reads for the four resources. (Documented — manual verification steps below)
