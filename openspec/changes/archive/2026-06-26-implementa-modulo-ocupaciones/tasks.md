# Tasks: Implement Occupations Module

## Review Workload Forecast

| Field | Value |
|-------|-------|
| Estimated changed lines | 900-1300 |
| 800-line budget risk | High |
| 400-line budget risk | High |
| Chained PRs recommended | Yes |
| Suggested split | Unit 1 -> Unit 2 -> Unit 3 |
| Delivery strategy | ask-always |
| Chain strategy | pending |

Decision needed before apply: Yes
Chained PRs recommended: Yes
Chain strategy: pending
400-line budget risk: High

### Suggested Work Units

| Unit | Goal | Likely PR | Verification |
|------|------|-----------|--------------|
| 1 | Domain + command foundations | PR 1 | Domain + command tests green |
| 2 | Persistence + read endpoints | PR 2 | MySQL repo + read API tests green |
| 3 | Lifecycle actions + Swagger polish | PR 3 | API conflict + Swagger tests green |

## Phase 1: Domain and Command Foundation

- [x] 1.1 RED: extend `tests/SGV.Tests/Dominio/Ocupaciones/OcupacionTests.cs` for update/finalize/delete/reactivate invariants and non-editable finalized rows.
- [x] 1.2 GREEN: update `src/SGV.Dominio/Ocupaciones/Ocupacion.cs` with `Actualizar(...)`, guarded `Finalizar(...)`, `EliminarLogicamente()`, and `Reactivar()` using active-state rules.
- [x] 1.3 Create `src/SGV.Aplicacion/Ocupaciones/Comandos/*` and `Consultas/*` contracts: requests, DTOs, errors, validators, `IOcupacionServicioComandos`, `IOcupacionServicioConsulta`, `IOcupacionRepository`.

## Phase 2: Application Services

- [x] 2.1 RED: add `tests/SGV.Tests/Aplicacion/Ocupaciones/OcupacionServicioComandosTests.cs` for `404` vs `409`, finalized-not-editable, uniqueness collisions, and same-row reactivation.
- [x] 2.2 GREEN: implement `src/SGV.Aplicacion/Ocupaciones/Comandos/OcupacionServicioComandos.cs` with `IPersonaRepository`, `IPuestoRepository`, `IOcupacionRepository`, and `IUnitOfWork` orchestration.
- [x] 2.3 RED/GREEN: add `tests/SGV.Tests/Aplicacion/Ocupaciones/OcupacionServicioConsultaTests.cs` and implement `src/SGV.Aplicacion/Ocupaciones/Consultas/OcupacionServicioConsulta.cs` for active-only list, `includeHistory`, and detail reads.

## Phase 3: Infrastructure and Persistence

- [x] 3.1 Implement `src/SGV.Infraestructura/Persistencia/Repositorios/OcupacionRepository.cs` plus mapper updates in `Mapeos/PersistenceToDomainMapper.cs` and `Mapeos/DomainToPersistenceMapper.cs`.
- [x] 3.2 RED/GREEN: add `tests/SGV.Tests/Persistencia/OcupacionRepositoryTests.cs` for active/history queries, soft-delete/reactivation persistence, and unique-index conflict behavior.
- [x] 3.3 Verify `src/SGV.Infraestructura/Persistencia/Configuraciones/OcupacionConfiguracion.cs` and migration `20260624153353_ConvertirTipoAsignacionAEnumYActualizarUnicidad.cs` need no new schema change; do not add a migration.

## Phase 4: API and Swagger

- [x] 4.1 Create `src/SGV.Api/Controllers/OcupacionesController.cs` with `GET`, `POST`, `PUT`, `PATCH /finalizar`, `PATCH /reactivar`, and `DELETE`, mirroring existing ProblemDetails patterns.
- [x] 4.2 Register repository/services/validators in `src/SGV.Infraestructura/DependencyInjection.cs` and extend `tests/SGV.Tests/Api/ApiWebApplicationFactory.cs` fakes for occupation services.
- [x] 4.3 RED/GREEN: add `tests/SGV.Tests/Api/OcupacionesControllerTests.cs` and update `tests/SGV.Tests/Api/SwaggerConfigurationTests.cs` for paths, `includeHistory`, `404`, and `409` documentation.

## Phase 5: End-to-End Verification

- [x] 5.1 Run `dotnet test` with focus on new Ocupaciones domain, application, persistence, API, and Swagger scenarios from all three delta specs.
- [x] 5.2 Confirm each work unit stays reviewable; if Unit 1 exceeds plan, pause apply and choose a chain strategy before continuing.
