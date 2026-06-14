# Tasks: Update to .NET 10 and Replace SQL Server with MySQL/Pomelo

## Review Workload Forecast

| Field | Value |
|-------|-------|
| Estimated changed lines | 900-1600 |
| 400-line budget risk | High |
| Chained PRs recommended | Yes |
| Suggested split | PR 1 runtime/package retarget → PR 2 provider/tests/modeling → PR 3 migrations/docs |
| Delivery strategy | ask-on-risk |
| Chain strategy | pending |

Decision needed before apply: Yes
Chained PRs recommended: Yes
Chain strategy: pending
400-line budget risk: High

### Suggested Work Units

| Unit | Goal | Likely PR | Notes |
|------|------|-----------|-------|
| 1 | Retarget SDK/TFMs and pin EF 9.x | PR 1 | Single reviewable foundation with restore/build/test smoke |
| 2 | Prove and implement Pomelo/MySQL provider behavior | PR 2 | Depends on PR 1; keep RED/GREEN/REFACTOR tests with model changes |
| 3 | Replace migrations and update docs/config guidance | PR 3 | Depends on PR 2; generated migration diff likely dominates size |

## Phase 1: Foundation

- [ ] 1.1 Update `global.json`, `src/SGV.Dominio/SGV.Dominio.csproj`, `src/SGV.Aplicacion/SGV.Aplicacion.csproj`, `src/SGV.Infraestructura/SGV.Infraestructura.csproj`, and `tests/SGV.Tests/SGV.Tests.csproj` to `net10.0`.
- [ ] 1.2 Replace `Microsoft.EntityFrameworkCore.SqlServer` with `Pomelo.EntityFrameworkCore.MySql` in `src/SGV.Infraestructura/SGV.Infraestructura.csproj`, keeping EF/Identity/Design packages on 9.x.
- [ ] 1.3 Verify restore/build package resolution on `SGV.slnx` so EF relational dependencies stay 9.x under .NET 10.

## Phase 2: RED

- [ ] 2.1 Extend `tests/SGV.Tests/Persistencia/ModeloPersistenciaTests.cs` with failing assertions for Pomelo provider selection and MySQL-safe uniqueness/filter behavior from the spec scenarios.
- [ ] 2.2 Add a failing migration/script verification test in `tests/SGV.Tests/Persistencia/ModeloPersistenciaTests.cs` or a sibling persistence test file for MySQL-compatible artifacts.
- [ ] 2.3 Mark or gate the real-provider test path in `tests/SGV.Tests/Persistencia/` so MySQL 8 verification is explicit when the server is unavailable.

## Phase 3: GREEN

- [ ] 3.1 Update `src/SGV.Infraestructura/Persistencia/SgvDbContextFactory.cs` to use `UseMySql`, `ConnectionStrings:Default`, and `Database:ServerVersion` with a fixed MySQL 8 server version.
- [ ] 3.2 Refactor SQL Server-specific EF modeling in `src/SGV.Infraestructura/Persistencia/Configuraciones/CargoConfiguracion.cs`, `PuestoConfiguracion.cs`, `PostulanteConfiguracion.cs`, `UnidadOrganizativaConfiguracion.cs`, and related files that use `HasFilter`/check SQL fragments.
- [ ] 3.3 Replace `src/SGV.Infraestructura/Persistencia/Migraciones/20260613022804_InicialSgvo*.cs`, `20260613022933_AgregarDatosSemillaBase*.cs`, and `SgvDbContextModelSnapshot.cs` with a fresh Pomelo baseline.

## Phase 4: REFACTOR + Verify

- [ ] 4.1 Clean persistence tests and supporting infrastructure in `tests/SGV.Tests/Persistencia/ModeloPersistenciaTests.cs` and `src/SGV.Infraestructura/Persistencia/SgvDbContext.cs` after GREEN passes, without changing behavior.
- [ ] 4.2 Update `docs/migracion-inicial-sgv.sql`, `docs/decisiones-implementacion.md`, `AGENTS.md`, and `openspec/config.yaml` to state .NET 10 + MySQL/Pomelo + EF Core 9.x.
- [ ] 4.3 Run a repository-wide SQL Server reference sweep and verify `dotnet test` covers the modified `sgv-database` scenarios before handing off to `sdd-apply`.
