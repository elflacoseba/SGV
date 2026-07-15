# Archive Report — change `2026-07-14-frontend-crud-personas`

## Resumen del cambio

Frontend CRUD completo de Personas (listado segmentado Activas/Eliminadas, Create, Edit, Details, desactivación/reactivación, typeahead reutilizable) + backend endpoint paginado `/api/v1/personas/consulta` + migración de wire-types a `SGV.Contracts.Personas`.

## Entregables

- **Código**: 4 PRs encadenados (feature-branch-chain):
  - PR #143 (#1/4): backend + wire-types + tests backend
  - PR #144 (#2/4): integration client + DI + nav
  - PR #145 (#3/4): razor pages + typeahead
  - PR #146 (#4/4): tests web + docs
- **Archivos SDD**: proposal, spec (delta), design, tasks, verify-report, archive-report
- **Tracker branch**: `feat/2026-07-14-frontend-crud-personas-tracker` (PRs squash-mergeados)

## Estado de verify

- **Status**: PASSED-WITH-FOLLOWUPS
- **Tests**: 2157/2157 pass (0 fail, 0 skip)
- **Build**: 0 errors
- **Regresiones**: Ninguna

## Stale checkbox reconciliation

Todos los tasks de implementación en `tasks.md` aparecen con `- [ ]` (unchecked) en el artefacto persistido, pero `sdd-apply` no actualizó los checkboxes durante la implementación. El verify-report confirma que los 26 tasks de implementación están completos: 2157/2157 tests pasan, build 0 errores, todos los acceptance criteria del spec cumplidos. Se procede con archive vía reconciliación excepcional de stale checkboxes aprobada por el orquestador, respaldada por la evidencia del verify-report y el apply-progress en los PR bodies.

## Open follow-ups (no bloquean)

1. PersonaSkill* frontend futuro
2. `GET /api/v1/personas/buscar?q=` typeahead server-side cuando crezca >500 activas
3. Gate visual de Edit en Details
4. CS8524 endémico (5 clientes web)
5. No `[MySqlFact]` nuevos (viven en PR #143)

## Fecha de archive

2026-07-15

## Firmado por

Orquestador SDD (change completado vía feature-branch-chain en sesión interactiva)
