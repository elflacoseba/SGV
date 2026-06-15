# Tasks: Refactor EF persistence entities

## Review Workload Forecast

| Field | Value |
|-------|-------|
| Estimated changed lines | 900-1400 |
| 400-line budget risk | High |
| Chained PRs recommended | Yes |
| Suggested split | PR 1 foundation/entities -> PR 2 repositories/audit -> PR 3 snapshot/regression verification |
| Delivery strategy | ask-always |
| Chain strategy | pending |

Decision needed before apply: Yes
Chained PRs recommended: Yes
Chain strategy: pending
400-line budget risk: High

### Suggested Work Units

| Unit | Goal | Likely PR | Notes |
|------|------|-----------|-------|
| 1 | Move EF model to `*Entity` types and preserve mappings/seeds | PR 1 | Base slice; includes model RED/GREEN tests |
| 2 | Map repositories and audit flow back to Domain semantics | PR 2 | Depends on PR 1; includes repository/audit regressions |
| 3 | Prove no schema drift and finalize snapshot-only metadata updates | PR 3 | Depends on PR 2; verify empty DDL diff before review |

## Phase 1: Model safety net

- [x] 1.1 RED: Update `tests/SGV.Tests/Persistencia/ModeloPersistenciaTests.cs` to assert SGV tables map to `src/SGV.Infraestructura/Persistencia/Entidades/*Entity.cs` CLR types while `AspNet*` mappings stay unchanged.
- [x] 1.2 RED: Add schema-invariant assertions in `tests/SGV.Tests/Persistencia/ModeloPersistenciaTests.cs` for current table names, generated columns, unique indexes, FK/check-constraint semantics, and EF Core 9/Pomelo metadata.
- [x] 1.3 GREEN: Create `src/SGV.Infraestructura/Persistencia/Entidades/` with `EntityBase.cs`, `AuditableEntityBase.cs`, and `*Entity.cs` files for every SGV mapped table named in `design.md`.

## Phase 2: EF mapping refactor

- [x] 2.1 GREEN: Switch `src/SGV.Infraestructura/Persistencia/SgvDbContext.cs` and `Configuraciones/ConfiguracionComun.cs` to `DbSet<*Entity>` and Infrastructure base entities only.
- [x] 2.2 GREEN: Retarget `src/SGV.Infraestructura/Persistencia/Configuraciones/*.cs` to persistence entities, preserving table names, column names, keys, indexes, relationships, generated-column workarounds, and Identity exclusions.
- [x] 2.3 GREEN: Update `src/SGV.Infraestructura/Persistencia/DatosSemilla.cs` to seed identical values through `NivelHabilidadEntity`, `EstadoVacanteEntity`, `EstadoPostulacionEntity`, `CargoEntity`, and `HabilidadEntity`.
- [x] 2.4 REFACTOR: Remove direct Domain-type EF usage from Infrastructure persistence files without changing Application contracts (DbContext, Configs, Seeds completed; Repository retargeting scoped to PR 2).

## Phase 3: Repository and audit boundary

- [ ] 3.1 RED: Extend `tests/SGV.Tests/Persistencia/{CargoRepositoryTests.cs,HabilidadRepositoryTests.cs,UnidadOrganizativaRepositoryTests.cs,PuestoRepositoryTests.cs}` to prove ordering, filters, and `Puesto` relationship graphs remain unchanged.
- [ ] 3.2 RED: Add `tests/SGV.Tests/Persistencia/AuditoriaSaveChangesInterceptorTests.cs` for create/update/soft-delete semantics, sensitive-field exclusion, and logical audit names without the `Entity` suffix.
- [ ] 3.3 GREEN: Add `src/SGV.Infraestructura/Persistencia/Mapeos/*.cs` plus updates in `Repositorios/ReadOnlyRepository.cs`, `CargoRepository.cs`, `HabilidadRepository.cs`, `UnidadOrganizativaRepository.cs`, and `PuestoRepository.cs` to query `*Entity` sets and return Domain models.
- [ ] 3.4 GREEN: Update `src/SGV.Infraestructura/Persistencia/AuditoriaSaveChangesInterceptor.cs` to track `EntityBase`/`AuditableEntityBase`, write `AuditoriaEntity`, and normalize logical entity names.

## Phase 4: Schema-drift proof

- [ ] 4.1 RED/GREEN: Update `tests/SGV.Tests/Persistencia/RepositoryTestData.cs` so MySQL persistence tests can create persistence graphs without forcing Domain EF tracking.
- [ ] 4.2 GREEN: Adjust `src/SGV.Infraestructura/Persistencia/Migraciones/SgvDbContextModelSnapshot.cs` only after model tests pass and no schema change is required.
- [ ] 4.3 VERIFY: Generate a throwaway EF migration or idempotent script from `src/SGV.Infraestructura/SGV.Infraestructura.csproj`; discard it unless the diff is snapshot/CLR-name-only.
- [ ] 4.4 VERIFY: Run `dotnet test` and mark this change ready only when model, repository, seed, and audit regressions all pass.
