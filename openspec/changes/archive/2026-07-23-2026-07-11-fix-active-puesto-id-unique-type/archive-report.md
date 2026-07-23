# Archive Report — `2026-07-11-fix-active-puesto-id-unique-type`

**Change**: `2026-07-11-fix-active-puesto-id-unique-type`
**Archived**: 2026-07-23
**Archive path**: `openspec/changes/archive/2026-07-23-2026-07-11-fix-active-puesto-id-unique-type/`
**Mode**: hybrid (filesystem + Engram)
**Verify verdict**: PASS WITH WARNINGS (no CRITICAL issues)

## Engram Observation IDs

| Artifact | Observation ID |
|----------|---------------|
| Explore | #956 |
| Proposal | #957 |
| Spec (delta) | #958 |
| Design | #959 |
| Tasks | #960 |
| Apply progress | #963 |
| Verify report | #964 |
| Archive report | *(this observation)* |

## Specs Synced

| Domain | Action | Details |
|--------|--------|---------|
| `sgv-database` | Updated | 1 requirement added, 0 modified, 0 removed |

### Requirement added: Coincidencia de tipo entre columna generada y columna fuente
- 4 scenarios merged into `openspec/specs/sgv-database/spec.md`
- Covers the type-coincidence invariant for computed columns referencing Guid columns mapped as `char(36)`

## Task Completion

- **Tasks**: 10/10 complete (`- [x]`), 0 unchecked (`- [ ]`)
- **Task Completion Gate**: ✅ Passed — no stale checkboxes

## Archive Contents

| Artifact | Status |
|----------|--------|
| `proposal.md` | ✅ |
| `specs/sgv-database/spec.md` (delta) | ✅ |
| `design.md` | ✅ |
| `tasks.md` | ✅ (10/10 complete) |
| `exploration.md` | ✅ |
| `verify-report.md` | ✅ |

## Deviations Documented

The verify report (observation #964) documents 4 deviations that do not block archive:
1. `HasColumnType("char(36)")` removido del config (EF/Pomelo NRE bug)
2. Storage final = `varchar(36)` en lugar de `char(36)` (MySqlConnector auto-detect Guid)
3. UPDATE defensivo pre-ALTER eliminado (MySQL re-evalúa durante ALTER COLUMN)
4. Assert T-006 relajado a `(36)` (consistente con storage real `varchar(36)`)

## Smoke Test

`dotnet test SGV.slnx --nologo`: **Passed** — 2891/2891 passed, 0 failed, 0 skipped.

## SDD Cycle Complete

The change `2026-07-11-fix-active-puesto-id-unique-type` (fix #59 — `ActivePuestoIdUnique` tipo incompatible con `PuestoId CHAR(36)`) has been fully planned, explored, specified, designed, implemented, verified, and archived.
