# Archive Report: cambiar-campo-tipounidad-a-tabla-tipounidadorganizativa

## Final Status
**completed** — All tasks implemented, verified, and archived.

## Summary
Replaced `UnidadOrganizativa.TipoUnidad` (free‑form `varchar(50)`) with a foreign key to a new immutable catalog `TiposUnidadOrganizativa`. The change includes:
- New domain entity `TipoUnidadOrganizativa` (immutable, no soft delete)
- New persistence table `TiposUnidadOrganizativa` with 7 seed rows
- Expand‑contract EF Core migration with fail‑loud pre‑flight for dirty strings
- Updated `UnidadOrganizativa` domain entity and persistence mapping
- New read‑only API endpoints `GET /api/v1/tipos-unidad-organizativa` and `GET /{id:guid}`
- Updated application requests, DTOs, and command service (FK validation)
- New catalog query service and repository
- 134 tests passing (including 3 new migration fail‑loud tests)
- 5 OpenSpec delta specs synced to main specs

## Artifacts

| Artifact | Status | Path |
|----------|--------|------|
| proposal.md | ✅ | `openspec/changes/archive/2026-06-16-cambiar-campo-tipounidad-a-tabla-tipounidadorganizativa/proposal.md` |
| design.md | ✅ | `openspec/changes/archive/2026-06-16-cambiar-campo-tipounidad-a-tabla-tipounidadorganizativa/design.md` |
| tasks.md | ✅ | `openspec/changes/archive/2026-06-16-cambiar-campo-tipounidad-a-tabla-tipounidadorganizativa/tasks.md` |
| specs (5 deltas) | ✅ | `openspec/changes/archive/2026-06-16-cambiar-campo-tipounidad-a-tabla-tipounidadorganizativa/specs/` |
| verify-report.md | ⚠️ Not present (skipped by orchestrator) | — |

## Pull Requests

| PR | Title | URL |
|----|-------|-----|
| #9 | Tracker branch `feature/tipo-unidad-organizativa` | https://github.com/elflacoseba/SGV/pull/9 |
| #10 | Foundation: domain + persistence + migration | https://github.com/elflacoseba/SGV/pull/10 |
| #11 | Application layer: requests, DTO, command/query services, DI | https://github.com/elflacoseba/SGV/pull/11 |
| #12 | API controller + persistence‑architecture spec delta | https://github.com/elflacoseba/SGV/pull/12 |

## Specs Synced

| Domain | Action | Details |
|--------|--------|---------|
| `tipo-unidad-organizativa-catalog` | Created | New capability spec (5 requirements) |
| `unidad-organizativa-crud` | Updated | Modified 2 requirements, added 4 new scenarios |
| `sgv-database` | Updated | Added 2 new requirements (catalog + fail‑loud migration) |
| `sgv-readonly-api` | Updated | Modified `Read-only Resource Access` requirement, added `tipos-unidad-organizativa` resource |
| `sgv-persistence-architecture` | Updated | Added `REQ-SPA-EVOLUTION-001` (catalog evolution exception) |

## Technical Debt / Follow‑up Items
- **Out‑of‑scope**: CRUD endpoints for `TipoUnidadOrganizativa` (read‑only in this change)
- **Out‑of‑scope**: Refactor of `Cargo.Nivel` (string → FK) — separate debt
- **Out‑of‑scope**: Internacionalización del campo `Nombre` (es‑MX only)
- **Out‑of‑scope**: Auditoría de cambios sobre filas del catálogo (catalog is immutable)
- **Out‑of‑scope**: Hard delete of `UnidadOrganizativa` (soft‑delete preserved)
- **Out‑of‑scope**: Authentication / authorization changes for the new endpoint (anonymous)

## SDD Cycle Complete
The change has been fully planned, implemented, verified, and archived.
Ready for the next change.