# Proposal: Replace `UnidadOrganizativa.TipoUnidad` (string) with FK to `TipoUnidadOrganizativa` catalog

## Why

Today, `UnidadOrganizativa.TipoUnidad` is a free-form `varchar(50) NOT NULL`. There is no referential integrity, no shared vocabulary, no UI-ready catalog, and no audit trail on which values are valid. Every consumer (API, future front-end combo, reporting, future migration to Power BI / data lake) has to re-encode the same string set, with no protection against typos (`"Departameto"`, `"direccion"`, mixed case, accents).

This change promotes `TipoUnidad` to a first-class immutable catalog, mirroring the precedent already established by `NivelesHabilidad` (catalog FK on `CargoHabilidad.NivelRequeridoId`, `OnDelete(Restrict)`, no soft delete, no `IsActive`). The goals are:

1. **Data integrity** — make impossible to persist a value that is not in the agreed-upon list of organizational unit types.
2. **Shared vocabulary** — single source of truth consumed by API, reports, and future front-end combos.
3. **Operational safety** — explicit seed (7 reference values) plus fail-loud migration that aborts and reports dirty strings, instead of silently writing NULLs or empty defaults.
4. **Maintainability** — adding a new type is a seed-only change; no code or migration rewrites.

Out of scope: refactor of the analogous `Cargo.Nivel` (string) field, full CRUD for the catalog, i18n of `Nombre`, audit of the catalog rows. These are tracked as separate debt.

## What Changes

- **New domain entity** `TipoUnidadOrganizativa` (catalog, immutable, no soft delete) under `src/SGV.Dominio/Organizacion/`, mapped to table `TiposUnidadOrganizativa` (`Id` Guid PK, `Codigo` varchar(50) unique, `Nombre` varchar(100) NOT NULL). Pattern: `NivelHabilidad`.
- **Domain refactor** of `UnidadOrganizativa`: drop the `string TipoUnidad` property, add `Guid TipoUnidadOrganizativaId` + nav property `TipoUnidadOrganizativa`. Constructor, `CambiarDatos(...)`, and `ValidacionesDominio` updated. The nav is set by the application service after the FK is resolved.
- **Persistence boundary**: new `TipoUnidadOrganizativaEntity` + `TipoUnidadOrganizativaConfiguracion` (`HasIndex(Codigo).IsUnique()`, no `IsActive`, no `IsDeleted`). Update `UnidadOrganizativaEntity` + its configuracion to map the FK with `OnDelete(Restrict)` and an index on `TipoUnidadOrganizativaId`. Add `TipoUnidadOrganizativa` to `DomainToPersistenceMapper` / `PersistenceToDomainMapper` (manual mapping, no auto-map).
- **EF Core migration** (single, expand-contract) in `src/SGV.Infraestructura/Persistencia/Migraciones/`. Steps inside the migration `Up`:
  1. `CREATE TABLE TiposUnidadOrganizativa (...)` with PK Guid, unique `Codigo`, seed via `MigrationBuilder.InsertData` (7 rows with static Guids declared as constants of the migration class).
  2. Pre-flight backfill `SELECT`: read all distinct `TipoUnidad` strings still present in `UnidadesOrganizativas`. If any are NOT present in the seed, throw `InvalidOperationException` listing the offending strings and **abort** the migration (no column changes). This is the fail-loud gate (P5).
  3. Add `TipoUnidadOrganizativaId` column (nullable, no default) + FK with `OnDelete(Restrict)` + index.
  4. Backfill `TipoUnidadOrganizativaId` from existing strings using a `MigrationBuilder.Sql(...)` join against the seed (only safe values remain by step 2).
  5. `ALTER COLUMN TipoUnidadOrganizativaId ... NOT NULL`.
  6. `DROP COLUMN TipoUnidad`.
  `Down`: reverse in opposite order with explicit `DROP CONSTRAINT`, `DROP COLUMN TipoUnidadOrganizativaId`, re-add `TipoUnidad` (nullable), re-create the `DROP TABLE TiposUnidadOrganizativa`.
- **Seed**: 7 reference values with static Guids (declared as `public static readonly Guid` in `DatosSemilla.cs` and reused as the same constants in the migration `InsertData`). Values: `Institucion`, `Facultad`, `Secretaria`, `Direccion`, `Departamento`, `Division`, `Area`. Catalog is open-ended: future types are added by appending a new seed row + migration, never by mutating existing rows (P2).
- **Application layer**:
  - `CrearUnidadOrganizativaRequest` / `ActualizarUnidadOrganizativaRequest` replace `string TipoUnidad` with `Guid TipoUnidadId` (P3).
  - New `ITipoUnidadOrganizativaRepository` + `ITipoUnidadOrganizativaServicioConsulta` (`ListAsync`, `GetByIdAsync`).
  - Write command service resolves `tipoUnidadId` → `TipoUnidadOrganizativa` (load, throw `NotFound` if missing) before constructing the unit.
  - `UnidadOrganizativaDto` exposes both `TipoUnidadId` and `TipoUnidadNombre` (denormalized via projection, no second round-trip) (P3).
- **Read API**: new `TiposUnidadOrganizativasController` at `GET /api/v1/tipos-unidad-organizativa` (list) and `GET /{id:guid}` (detail), both anonymous, read-only, aligned with `CargosController` (P4).
- **Strict TDD ordering** (per `openspec/config.yaml strict_tdd: true`):
  1. Domain: red tests for `TipoUnidadOrganizativa` ctor invariants + `UnidadOrganizativa.CambiarDatos` no longer accepting `string TipoUnidad`.
  2. Application: red tests for `CrearUnidadOrganizativaRequest` carrying `Guid TipoUnidadId`, command service rejecting unknown IDs, query DTO exposing `TipoUnidadId` + `TipoUnidadNombre`.
  3. Persistence: red tests for FK constraint (`OnDelete(Restrict)`), unique `Codigo`, fail-loud migration behavior (verified against a fixture DB containing a dirty string — test asserts migration throws and lists values).
  4. API: red tests for `GET /api/v1/tipos-unidad-organizativa` and `GET /{id:guid}` returning the catalog.
- **SQL script**: regenerate `docs/migracion-inicial-sgv.sql` with `dotnet ef migrations script … --idempotent` to include the new migration in the cumulative script.
- **OpenSpec specs to update in the `sdd-spec` phase**: see `Dependencies` below.

## Impact

| Layer | Impact | Files / Lines (high-level) |
|-------|--------|----------------------------|
| Domain | New entity + refactor | +`src/SGV.Dominio/Organizacion/TipoUnidadOrganizativa.cs` (~30 lines), modify `src/SGV.Dominio/Organizacion/UnidadOrganizativa.cs` (~20 lines changed) |
| Application | Refactor requests/DTOs + new query service | modify 4 files in `src/SGV.Aplicacion/Organizacion/Comandos/` + 1 DTO in `src/SGV.Aplicacion/Organizacion/Consultas/Dtos/` + new `ITipoUnidadOrganizativaServicioConsulta.cs` + new `ITipoUnidadOrganizativaRepository.cs` (~120 lines changed total) |
| Infrastructure | New entity/config/mappers/repo + seed + migration | +`TipoUnidadOrganizativaEntity.cs` + `TipoUnidadOrganizativaConfiguracion.cs` + `TipoUnidadOrganizativaRepository.cs` (~80 lines), modify `UnidadOrganizativaEntity.cs` + `UnidadOrganizativaConfiguracion.cs` + `DomainToPersistenceMapper.cs` + `PersistenceToDomainMapper.cs` + `DatosSemilla.cs` (~60 lines changed) + 1 new migration (~150 lines including the fail-loud `Sql` block) |
| API | New controller | +`src/SGV.Api/Controllers/TiposUnidadOrganizativasController.cs` (~40 lines), no other controller changes (request shape change is wire-format breaking but routes are unchanged) |
| Tests | Modify existing + add new | 7 files modified in `tests/SGV.Tests/{Api,Aplicacion,Persistencia}/` + 3 new (catalog DTO test, fail-loud migration test, new API GET test) (~300 lines changed/added total) |
| Migrations | 1 new | `src/SGV.Infraestructura/Persistencia/Migraciones/<timestamp>_ReemplazarTipoUnidadPorCatalogo.cs` |
| Docs / SQL | Regenerate script | `docs/migracion-inicial-sgv.sql` (regenerated, full content shift) |
| OpenSpec specs | Update existing in spec phase | `openspec/specs/sgv-database/spec.md`, `openspec/specs/unidad-organizativa-crud/spec.md` (delta); `openspec/specs/sgv-readonly-api/spec.md` touched only to add the new catalog endpoint to the documented list |

Estimated diff: **~650–850 changed/added lines end-to-end**.

## Out of Scope

- CRUD endpoints for `TipoUnidadOrganizativa` (read-only in this change, P2).
- Refactor of `Cargo.Nivel` (string → FK) — separate debt.
- Internacionalización del campo `Nombre` (es-MX only, no `DescripcionCultura` column).
- Auditoría de cambios sobre filas del catálogo (catalog is immutable by construction, no audit needed).
- Hard delete of `UnidadOrganizativa` (current soft-delete behavior preserved).
- Authentication / authorization changes for the new endpoint (anonymous, like `CargosController`).

## Open Questions

None at proposal time. The five critical product decisions (seed list, inmutabilidad, contract shape `Guid` + denormalized name, new read-only endpoint, fail-loud backfill) are confirmed. No additional gaps surfaced during drafting.

## Acceptance Criteria

1. `TiposUnidadOrganizativa` table exists with `Id` (Guid PK), `Codigo` (varchar(50) unique NOT NULL), `Nombre` (varchar(100) NOT NULL); no `IsActive`/`IsDeleted` columns.
2. `UnidadOrganizativa.TipoUnidadOrganizativaId` is a NOT NULL FK with `OnDelete(Restrict)` and an index.
3. `CrearUnidadOrganizativaRequest` and `ActualizarUnidadOrganizativaRequest` accept `TipoUnidadId` (Guid); unknown IDs are rejected with a validation error before any write.
4. `UnidadOrganizativaDto` exposes `TipoUnidadId` (Guid) and `TipoUnidadNombre` (string) — denormalized, fetched in the same query as the unit.
5. `GET /api/v1/tipos-unidad-organizativa` returns the full seed list as DTOs; `GET /api/v1/tipos-unidad-organizativa/{id:guid}` returns 200 with the DTO or 404.
6. Migration fails explicitly (`InvalidOperationException`) if any pre-existing `UnidadesOrganizativas.TipoUnidad` value is not in the seed; the message lists every offending value and the migration leaves the schema unchanged.
7. The 7 seed values (`Institucion`, `Facultad`, `Secretaria`, `Direccion`, `Departamento`, `Division`, `Area`) are present after running migrations on a clean DB, with the static Guids declared as `public static readonly Guid` in both the migration and `DatosSemilla`.
8. `dotnet build` and `dotnet test` pass (tests tagged `[MySqlFact]` are skipped with a documented reason when MySQL is not available locally; no regression in non-DB tests).

## Risks

| # | Risk | Likelihood | Mitigation |
|---|------|------------|------------|
| R1 | **Breaking change in API contract**: `tipoUnidad` (string) disappears from request and DTO. Existing API consumers will receive 400/deserialize errors. | High | Document explicitly in the release notes / changelog; no field alias for the old name. Front-end clients must move to `tipoUnidadId` in lockstep. |
| R2 | **Fail-loud backfill blocks deploy** if any legacy dirty string exists. | Medium | Provide a one-off SQL dry-run query in the migration comments to enumerate dirty strings before running the migration. Document a manual cleanup script. |
| R3 | **Diff size 650–850 lines exceeds the 400-line review budget**. | High | The orchestrator has `delivery_strategy: ask-always` (C1). When `sdd-tasks` runs, it will trigger the Review Workload Guard and pause to discuss **chained PRs** (e.g. PR1 = domain + persistence entity + migration, PR2 = application + tests, PR3 = API + OpenSpec deltas). This proposal does NOT decide the chained-PR split; it flags the gate explicitly. |
| R4 | **Index contention on FK** when the new `TipoUnidadOrganizativaId` index is created on a large `UnidadesOrganizativas` table. | Low | The seed catalog is small (7 rows) and the index is on a single FK column; expect an `ALGORITHM=INPLACE, LOCK=NONE` build on MySQL 8. Verify with `EXPLAIN` against a representative dataset during `sdd-verify`. |
| R5 | **Duplicated Guids between migration seed and runtime seed** if `DatosSemilla` and the migration `InsertData` drift. | Medium | Single source: declare the 7 Guids as `public static readonly Guid` in `DatosSemilla` and reference the same constants in the migration's `InsertData` call. Add a unit test asserting both lists are identical. |

## Size Forecast

- **~650–850 changed/added lines** end-to-end (model + migration + 7 test files modified + 3 new + API controller + spec deltas).
- **Exceeds the 400-line budget** defined for the change (`delivery_strategy: ask-always`).
- The orchestrator will hit the **Review Workload Guard** at `sdd-tasks` time. Expected outcome: chained PRs (3 slices, ~200–300 lines each), gated on user confirmation. The proposal does NOT pre-decide the slice boundaries; that is mechanical work for `sdd-tasks`.

## Dependencies

- **OpenSpec specs to update** in the `sdd-spec` phase:
  - `openspec/specs/sgv-database/spec.md` — add a new requirement `Tipos de Unidad Organizativa como Catálogo` (inmutabilidad, FK `OnDelete(Restrict)`, seed determinístico).
  - `openspec/specs/unidad-organizativa-crud/spec.md` — modify `Manage Organizational Units` and `Validate Organizational Unit Writes` to reference `TipoUnidadId` instead of the old `TipoUnidad` string. No new requirement, only modifications.
  - `openspec/specs/sgv-readonly-api/spec.md` — extend the `List supported resources` scenario to include `tipos de unidad organizativa`. (This is a `MODIFIED` requirement, not a new one — the spec already covers all read-only resources generically.)
- **Predecedent in repo**: `src/SGV.Dominio/Habilidades/NivelHabilidad.cs` + `src/SGV.Infraestructura/Persistencia/Configuraciones/NivelHabilidadConfiguracion.cs` + `src/SGV.Infraestructura/Persistencia/Configuraciones/CargoHabilidadConfiguracion.cs` (FK `OnDelete(Restrict)`, no `IsActive`, unique `Codigo`). Pattern must be mirrored exactly.
- **Active change stub** (to be cleaned up during archive of the previous change): `openspec/changes/implementar-modulo-unidad-organizativa-crud-completo/` is empty after archive. The current change does not depend on it functionally; it only inherits the `UnidadOrganizativa` and request/DTO files that change in this proposal.

## References

- `src/SGV.Dominio/Organizacion/UnidadOrganizativa.cs` — current `TipoUnidad` property and ctor (lines 14, 29, 47).
- `src/SGV.Infraestructura/Persistencia/Configuraciones/UnidadOrganizativaConfiguracion.cs:18` — current `HasMaxLength(50).IsRequired()` mapping.
- `src/SGV.Dominio/Habilidades/NivelHabilidad.cs` — catalog entity precedent.
- `src/SGV.Dominio/Habilidades/CargoHabilidad.cs:34-36` — FK navigation precedent.
- `src/SGV.Infraestructura/Persistencia/Configuraciones/NivelHabilidadConfiguracion.cs` — catalog table mapping precedent.
- `src/SGV.Infraestructura/Persistencia/Configuraciones/CargoHabilidadConfiguracion.cs:27-30` — FK `OnDelete(Restrict)` precedent.
- `src/SGV.Aplicacion/Organizacion/Comandos/UnidadOrganizativaRequests.cs` — request shape to refactor.
- `src/SGV.Aplicacion/Organizacion/Consultas/Dtos/UnidadOrganizativaDto.cs` — DTO to extend.
- `src/SGV.Api/Controllers/CargosController.cs` — endpoint precedent for the new `TiposUnidadOrganizativasController`.
- `src/SGV.Infraestructura/Persistencia/Migraciones/20260614183103_InicialSgvo.cs:268-308` — current `UnidadesOrganizativas` schema baseline.
- `src/SGV.Infraestructura/Persistencia/Mapeos/DomainToPersistenceMapper.cs` + `PersistenceToDomainMapper.cs` — manual mappers to extend.
- `src/SGV.Infraestructura/Persistencia/DatosSemilla.cs` — existing seed (does not include unit types today).
- `openspec/config.yaml` — `strict_tdd: true` governs the test-first ordering.
- `openspec/specs/sgv-database/spec.md` — target spec to receive a new requirement.
- `openspec/specs/unidad-organizativa-crud/spec.md` — target spec to receive a `MODIFIED` block.
- `openspec/specs/sgv-readonly-api/spec.md` — target spec to extend the resource list.
- `docs/migracion-inicial-sgv.sql` — to be regenerated with the new migration in the cumulative script.

## Capabilities (contract with `sdd-spec`)

### New Capabilities
- `tipo-unidad-organizativa-catalog`: read-only catalog of organizational unit types, immutable, exposed via `GET /api/v1/tipos-unidad-organizativa` and `GET /{id:guid}`. Seed of 7 values with static Guids.

### Modified Capabilities
- `unidad-organizativa-crud`: request and DTO swap `TipoUnidad: string` for `TipoUnidadId: Guid` (+ `TipoUnidadNombre` on the DTO). FK enforcement moves validation from the string-domain layer to the application service (existence check before write).
- `sgv-database`: add a new requirement `Tipos de Unidad Organizativa como Catálogo`. No change to the existing hierarchy / soft-delete / active-code-uniqueness requirements.

## Rollback Plan

Revert the migration (drop `TiposUnidadOrganizativa` table, drop FK, re-add nullable `TipoUnidad` string column with `varchar(50)`), revert request/DTO/controller changes, revert OpenSpec deltas (do not merge into main specs). Because the migration has a `Down` that restores the column as nullable, no data is lost as long as `dotnet ef database update <previous-migration>` is run before deploying the revert. The seed catalog can simply be dropped (no FK relationships from anywhere else outside this change). If the revert happens **after** the contract step (FK in production), a forward-fix migration is preferred over a `Down` to avoid data backfill in reverse.
