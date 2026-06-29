# Archive Report: reactivar-y-filtrar-unidades-organizativas-eliminadas

**Date**: 2026-06-29
**Status**: PASS_WITH_WARNINGS
**Archived to**: `openspec/changes/archive/2026-06-29-reactivar-y-filtrar-unidades-organizativas-eliminadas/`

## Summary

Cierre del ciclo SDD para el cambio que agrega filtro de estado (`activas`/`eliminadas`) al listado web de unidades organizativas y reactivación por fila desde la vista de eliminadas.

## Specs Synced

| Domain | Action | Details |
|--------|--------|---------|
| `unidad-organizativa-crud` | Created | 1 requirement added: Consulta segmentada de unidades organizativas por estado (3 scenarios) |
| `sgv-readonly-api` | Created | 1 requirement added: Contrato documentado de filtro de listado de unidades organizativas (3 scenarios) |
| `unidad-organizativa-web-listado` | Updated | 2 requirements modified: Tabla consultable (5 scenarios updated) + Reactivación visible desde el flujo de listado (3 scenarios rewritten) |

## Archive Contents

- `proposal.md` ✅
- `specs/` ✅ (3 domains: unidad-organizativa-crud, sgv-readonly-api, unidad-organizativa-web-listado)
- `design.md` ✅
- `tasks.md` ✅ (20/20 tasks complete)
- `verify-report.md` ✅
- `apply-progress.md` ✅

## Source of Truth Updated

The following main specs now reflect the new behavior:
- `openspec/specs/unidad-organizativa-crud/spec.md` — added segmentado query requirement
- `openspec/specs/sgv-readonly-api/spec.md` — added documented filter contract requirement
- `openspec/specs/unidad-organizativa-web-listado/spec.md` — updated table and reactivation requirements

## Verification Notes

- Verdict: **PASS_WITH_WARNINGS**
- 20/20 tasks complete, 14/14 spec scenarios compliant
- 846 tests passed, 0 failed, 146 skipped
- No CRITICAL issues
- Warnings: low coverage on `UnidadOrganizativaApiClient.cs` (0%) and `Index.cshtml.cs` (50%), frontend dataset outdated warnings

## SDD Cycle Complete

The change has been fully planned, implemented, verified, and archived.
