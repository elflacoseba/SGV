# Apply Progress: agrega-proyecto-api

## Batch

- **Mode**: Strict TDD
- **Batch type**: Post-verify remediation
- **Branch**: `feat/agrega-proyecto-api/w3-controllers-verify`
- **Scope guard**: Limited to verify blockers (missing `apply-progress`, vacuous MySQL repository assertions, durable live verification evidence)

## Cumulative Work Units

| Work Unit | Commit / Source | Scope | Status |
|-----------|------------------|-------|--------|
| W1 | `ebbf24e` | API project bootstrap, Swagger, anonymous user, solution wiring | ✅ Complete |
| W2 | `e005394` | Application contracts, read services, EF repositories/UoW, MySQL repository tests | ✅ Complete |
| W3 | `859176f` | MVC controllers, API tests, Swagger verification wiring | ✅ Complete |
| W3 remediation | This batch | Fix verify blockers and persist strict TDD evidence | ✅ Complete |

## Task Status

| Task | Status | Evidence |
|------|--------|----------|
| 1.1 | ✅ | `src/SGV.Api/*`, `SGV.slnx`, commit `ebbf24e` |
| 1.2 | ✅ | `tests/SGV.Tests/Api/SwaggerConfigurationTests.cs` |
| 1.3 | ✅ | `src/SGV.Api/Program.cs`, `src/SGV.Api/Seguridad/UsuarioActualAnonimo.cs` |
| 2.1 | ✅ | `tests/SGV.Tests/Aplicacion/Organizacion/*ServicioConsultaTests.cs`, `tests/SGV.Tests/Aplicacion/Habilidades/HabilidadServicioConsultaTests.cs` |
| 2.2 | ✅ | `src/SGV.Aplicacion/Comun/Persistencia/IReadOnlyRepository.cs`, `IUnitOfWork.cs` |
| 2.3 | ✅ | `src/SGV.Aplicacion/Organizacion/Consultas/*`, `src/SGV.Aplicacion/Habilidades/Consultas/*` |
| 2.4 | ✅ | DTO contracts omit audit/internal persistence fields |
| 3.1 | ✅ | `tests/SGV.Tests/Persistencia/*RepositoryTests.cs`; remediation strengthened `PuestoRepositoryTests` and `UnidadOrganizativaRepositoryTests` |
| 3.2 | ✅ | `src/SGV.Infraestructura/Persistencia/Repositorios/*` |
| 3.3 | ✅ | `src/SGV.Infraestructura/DependencyInjection.cs` |
| 4.1 | ✅ | `tests/SGV.Tests/Api/*ControllerTests.cs` |
| 4.2 | ✅ | `src/SGV.Api/Controllers/*.cs` |
| 4.3 | ✅ | Swagger tests + live Swagger verification |
| 5.1 | ✅ | Fresh `dotnet build` + `dotnet test` passed in this remediation batch |
| 5.2 | ✅ | Durable manual verification evidence persisted below from executed verify run |

## TDD Cycle Evidence

| Task | Test File | Layer | Safety Net | RED | GREEN | TRIANGULATE | REFACTOR |
|------|-----------|-------|------------|-----|-------|-------------|----------|
| 1.1 | `tests/SGV.Tests/Api/SwaggerConfigurationTests.cs` | Integration | N/A (new project) | ⚠️ Reconstructed from task split and commit `ebbf24e`; no original apply artifact was persisted | ✅ Covered by current passing Swagger/API suite and fresh `dotnet build` | ✅ Swagger registration + anonymous discovery scenarios cover bootstrap behavior | ➖ Structural bootstrap only |
| 1.2 | `tests/SGV.Tests/Api/SwaggerConfigurationTests.cs` | Integration | N/A (new file) | ⚠️ Reconstructed from commit `ebbf24e` creating the RED test file before Program wiring | ✅ Current suite passes (`dotnet test` 68/68) | ✅ Covers GET-only Swagger doc plus anonymous access discovery | ✅ Swagger assertions extended in W3 |
| 1.3 | `tests/SGV.Tests/Api/SwaggerConfigurationTests.cs` | Integration | ⚠️ Reconstructed from commit sequence W1 | ⚠️ Reconstructed from task 1.2 -> 1.3 pairing | ✅ `Program.cs` wiring validated by current passing Swagger/API suite | ✅ Controller discovery and GET-only documentation prove DI/http pipeline works | ➖ No extra refactor evidence retained |
| 2.1 | `tests/SGV.Tests/Aplicacion/Organizacion/*ServicioConsultaTests.cs`, `tests/SGV.Tests/Aplicacion/Habilidades/HabilidadServicioConsultaTests.cs` | Unit | N/A (new files) | ⚠️ Reconstructed from commit `e005394` adding service tests before service implementations | ✅ Current suite passes (`dotnet test` 68/68) | ✅ List/detail, empty, and not-found scenarios covered across four services | ✅ DTO mapping kept consumer-safe |
| 2.2 | `tests/SGV.Tests/Aplicacion/Organizacion/*ServicioConsultaTests.cs`, `tests/SGV.Tests/Aplicacion/Habilidades/HabilidadServicioConsultaTests.cs` | Unit | ⚠️ Reconstructed from W2 commit sequence | ⚠️ Reconstructed from service tests depending on repository abstractions | ✅ Contracts compile and are exercised through service tests | ✅ Async list/detail contracts used across multiple service scenarios | ➖ Contract-only step |
| 2.3 | `tests/SGV.Tests/Aplicacion/Organizacion/*ServicioConsultaTests.cs`, `tests/SGV.Tests/Aplicacion/Habilidades/HabilidadServicioConsultaTests.cs` | Unit | ⚠️ Reconstructed from W2 commit sequence | ⚠️ Reconstructed from task 2.1 -> 2.3 pairing | ✅ Service and DTO behavior validated by current passing suite | ✅ Multiple DTO shapes and not-found/empty paths exercised | ✅ Mapping logic remained thin and explicit |
| 2.4 | `tests/SGV.Tests/Aplicacion/Organizacion/*ServicioConsultaTests.cs`, `tests/SGV.Tests/Aplicacion/Habilidades/HabilidadServicioConsultaTests.cs` | Unit | ⚠️ Reconstructed | ⚠️ Reconstructed from DTO-focused assertions | ✅ Current DTO contract tests pass | ✅ Consumer-safe shape checked across list/detail responses | ✅ Audit/internal fields stayed out of DTOs |
| 3.1 | `tests/SGV.Tests/Persistencia/CargoRepositoryTests.cs`, `tests/SGV.Tests/Persistencia/HabilidadRepositoryTests.cs`, `tests/SGV.Tests/Persistencia/PuestoRepositoryTests.cs`, `tests/SGV.Tests/Persistencia/UnidadOrganizativaRepositoryTests.cs` | Integration (MySQL) | ⚠️ Reconstructed for original W2; ✅ actual baseline for remediation was 6/6 targeted tests passing before edits | ✅ Remediation RED: strengthened assertions first, then targeted run failed 4/6 because local MySQL had no `UnidadesOrganizativas` or `Puestos` seed rows | ✅ Remediation GREEN: added deterministic MySQL test data and reran targeted repository tests -> 6/6 passing | ✅ Visible + inactive + deleted fixtures plus direct `GetByIdAsync` coverage now force real query paths and navigation loading | ✅ Shared repository test data helper removed duplication and ghost assertions |
| 3.2 | `tests/SGV.Tests/Persistencia/*RepositoryTests.cs` | Integration (MySQL) | ⚠️ Reconstructed from W2 commit sequence | ⚠️ Reconstructed from repository tests introduced in W2 | ✅ Current suite passes and exercises `AsNoTracking`, `IsDeleted`, `IsActive`, and navigation includes | ✅ Cargo/Habilidad ordering plus Unidad/Puesto filters cover different repository branches | ➖ No production refactor in remediation |
| 3.3 | `tests/SGV.Tests/Api/SwaggerConfigurationTests.cs`, `tests/SGV.Tests/Api/*ControllerTests.cs` | Integration | ⚠️ Reconstructed | ⚠️ Reconstructed from DI-dependent tests across W2/W3 | ✅ Current API suite passes with infrastructure registrations active | ✅ Multiple controllers/services consume the DI graph | ➖ No retained refactor log |
| 4.1 | `tests/SGV.Tests/Api/CargosControllerTests.cs`, `PuestosControllerTests.cs`, `SkillsControllerTests.cs`, `UnidadesOrganizativasControllerTests.cs` | Integration (HTTP/API) | N/A (new files) | ⚠️ Reconstructed from commit `859176f` adding controller tests before controllers | ✅ Current suite passes (`dotnet test` 68/68) | ✅ Collection/detail, empty, DTO shape, and anonymous access scenarios covered | ✅ Shared `ApiWebApplicationFactory` centralizes setup |
| 4.2 | `tests/SGV.Tests/Api/*ControllerTests.cs` | Integration (HTTP/API) | ⚠️ Reconstructed from W3 commit sequence | ⚠️ Reconstructed from task 4.1 -> 4.2 pairing | ✅ Controllers validated by current passing HTTP suite | ✅ Each resource has collection + detail paths; puestos include related summaries | ➖ No production refactor in remediation |
| 4.3 | `tests/SGV.Tests/Api/SwaggerConfigurationTests.cs` | Integration (HTTP/API) | ⚠️ Reconstructed | ⚠️ Reconstructed from W3 Swagger assertions | ✅ Swagger GET-only assertions pass in current suite | ✅ Documentation checked for endpoint presence and write exclusion | ✅ Swagger assertions were consolidated during W3 |
| 5.1 | Whole solution | Build + test | ✅ This remediation batch reran `dotnet build` and `dotnet test` after test fixes | ➖ Verification step, not a new RED task | ✅ `dotnet build` passed; `dotnet test` passed 68/68 | ✅ Targeted MySQL repository run (6/6) plus full suite (68/68) cover both narrow and broad execution | ➖ None |
| 5.2 | Live API / MySQL verification | Manual integration | ⚠️ Original live run happened in verify, not in original apply artifact | ➖ No new RED during remediation; blocker was missing durable evidence | ✅ Executed previously during verify and persisted durably below in this artifact | ✅ Swagger + four resource collections were checked independently | ➖ Evidence persistence only |

## Test Summary

- **Total remediation tests written/strengthened**: 4 MySQL integration tests strengthened, 1 shared helper added
- **Targeted remediation run**: `dotnet test --filter "FullyQualifiedName~SGV.Tests.Persistencia.PuestoRepositoryTests|FullyQualifiedName~SGV.Tests.Persistencia.UnidadOrganizativaRepositoryTests"`
  - Baseline safety net before edits: ✅ 6/6 passed
  - RED after assertion strengthening: ❌ 4/6 failed (`Assert.NotEmpty()` on empty live collections)
  - GREEN after deterministic test seeding: ✅ 6/6 passed
- **Full suite after remediation**: ✅ 68/68 passed
- **Fresh build after remediation**: ✅ `dotnet build` passed
- **Layers in current change set**: Unit (service tests), Integration HTTP/API, Integration MySQL
- **Approval/refactor note**: Original W1-W3 evidence was reconstructed from commits and current artifacts because the initial apply batch did not persist `apply-progress`

## Durable Manual Verification Evidence

### Source of evidence

The live API/MySQL verification below was executed during the verify phase and recorded in `openspec/changes/agrega-proyecto-api/verify-report.md`. This remediation batch did **not** rerun the live API because the blocker was missing durable persistence of that evidence, not a new runtime regression.

### Executed command

```text
ASPNETCORE_ENVIRONMENT=Development dotnet run --project src/SGV.Api/SGV.Api.csproj
```

### Verified requests and results

| Check | Result | Notes |
|------|--------|-------|
| `GET /swagger/v1/swagger.json` | ✅ 200 | Swagger available locally |
| `GET /api/v1/unidades-organizativas` | ✅ 200 | Returned `[]` on the local MySQL state used in verify |
| `GET /api/v1/cargos` | ✅ 200 | Returned persisted non-empty data |
| `GET /api/v1/puestos` | ✅ 200 | Returned `[]` on the local MySQL state used in verify |
| `GET /api/v1/skills` | ✅ 200 | Returned persisted non-empty data |

### Interpretation

- The live run proved the API starts against local MySQL, Swagger is reachable, and anonymous GET requests succeed for all four read-only resources.
- The local verify database contained persisted rows for `cargos` and `skills`, and empty collections for `unidades-organizativas` and `puestos`.
- This remediation batch closes the durability gap by preserving that manual evidence here and closes the assertion-quality gap by making MySQL repository tests create their own deterministic `UnidadOrganizativa` and `Puesto` data.

## Remediation Files Changed

| File | Action | Purpose |
|------|--------|---------|
| `tests/SGV.Tests/Persistencia/UnidadOrganizativaRepositoryTests.cs` | Modified | Removed vacuous guards and seeded deterministic MySQL data for `IsActive`, `IsDeleted`, and `GetByIdAsync` assertions |
| `tests/SGV.Tests/Persistencia/PuestoRepositoryTests.cs` | Modified | Removed ghost loop / vacuous guards and seeded deterministic MySQL data for includes and `GetByIdAsync` assertions |
| `tests/SGV.Tests/Persistencia/RepositoryTestData.cs` | Added | Shared deterministic MySQL test entity factory for remediation |
| `openspec/changes/agrega-proyecto-api/apply-progress.md` | Added | Durable cumulative strict TDD and manual verification evidence |

## Current Status

- **Tasks complete**: 15 / 15
- **Remediation blockers addressed**: 3 / 3
- **Ready for**: `sdd-verify`
