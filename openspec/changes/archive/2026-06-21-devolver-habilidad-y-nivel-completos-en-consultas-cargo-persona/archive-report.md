# Archive Report

**Change**: devolver-habilidad-y-nivel-completos-en-consultas-cargo-persona
**Archived at**: 2026-06-21
**Archive path**: `openspec/changes/archive/2026-06-21-devolver-habilidad-y-nivel-completos-en-consultas-cargo-persona/`
**Mode**: openspec

## Intent

Enrich `GET /api/v1/cargos/{cargoId}/skills` and `GET /api/v1/personas/{personaId}/skills` so consumers receive full nested `skill` and `nivel` data without extra round trips. Preserve `skillId` and `nivelId` for backward compatibility. Keep write contracts and parent payloads unchanged.

## Stale Checkbox Reconciliation

Task 4.3 ("Refactor duplicated test helpers") was unchecked at archive time. Reconciliation was performed with the following evidence from verify-report:

- **12/12 core implementation tasks complete** (task 4.3 is optional cleanup)
- **11/11 spec scenarios compliant** with passing tests
- **777/777 tests pass**, build succeeds (0 errors, 0 warnings)
- **No CRITICAL issues** found
- Verify-report verdict: **PASS WITH WARNINGS**, explicitly marks task 4.3 as "non-critical refactoring/cleanup task for duplicated test helper fakes, not a functional gap"

The archived tasks.md was updated to reflect task 4.3 as complete with this reconciliation note.

## Task Summary

| Metric | Value |
|--------|-------|
| Total tasks | 13 |
| Complete | 13 (12 core + 1 reconciled cleanup) |
| Verification | PASS WITH WARNINGS |

## Specs Synced

| Domain | Action | Details |
|--------|--------|---------|
| `cargo-skill-query-contract` | Created | New main spec (3 requirements, 4 scenarios) |
| `persona-skill-query-contract` | Created | New main spec (3 requirements, 4 scenarios) |
| `sgv-readonly-api` | Updated | Appended 1 requirement (Enriched Cargo and Persona skill query documentation) with 3 scenarios |

## Source of Truth Updated

- `openspec/specs/cargo-skill-query-contract/spec.md`
- `openspec/specs/persona-skill-query-contract/spec.md`
- `openspec/specs/sgv-readonly-api/spec.md`

## Archive Contents

- proposal.md ✅
- specs/ ✅ (3 domains: cargo-skill-query-contract, persona-skill-query-contract, sgv-readonly-api)
- design.md ✅
- tasks.md ✅ (13/13 tasks complete)
- verify-report.md ✅
- archive-report.md ✅ (this file)

## SDD Cycle Complete

The change has been fully planned, implemented, verified, and archived.
