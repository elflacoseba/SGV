# Archive Report: Autorización diferenciada en CargosController

## Archive Metadata
| Field | Value |
|-------|-------|
| Change | 2026-07-01-cargos-crear-autorizacion-admin |
| Archived on | 2026-07-01 |
| Archived to | openspec/changes/archive/2026-07-01-2026-07-01-cargos-crear-autorizacion-admin/ |
| Verdict from verify | FAIL (1 CRITICAL preexistente) |
| Override | Usuario aprobó archive consciente (CRITICAL es preexistente, bug #59, fuera de scope del change) |

## Specs Synced
| Domain | Action | Details |
|--------|--------|---------|
| `cargo-management` | Updated | 1 requirement agregado: `Autorización de endpoints de cargos` |
| `cargo-skill-query-contract` | Updated | 1 requirement agregado: `Autorización del subrecurso de skills de cargos` |
| `sgv-readonly-api` | Updated | 1 requirement modificado: `No Authentication Requirement` |

## Archive Contents
- proposal.md ✅
- exploration.md ✅
- design.md ✅
- specs/ ✅ (3 files)
- tasks.md ✅ (15/15 complete)
- apply-progress.md ✅
- verify-report.md ✅

## Override Justification
El verify reportó 1 CRITICAL por `dotnet test SGV.slnx` exit code 1 (12 fallos en `OcupacionRepositoryTests`, bug preexistente #59). Esos fallos son anteriores al change #62, están documentados en `AGENTS.md`, y NO son regresión del change archivado.

El change sí cumple su alcance al 100%:
- 15/15 tasks completas
- 46/46 tests específicos del change PASS
- 10/10 scenarios del spec compliance matrix PASS
- Code vs design 100% coherente
- `Program.cs` no fue modificado
- Cero string literal de rol; la autorización usa `RolesSgv.Administrador`

Decisión consciente del usuario: archivar con override del CRITICAL preexistente. El bug #59 queda como work item aparte y fuera del scope de este change.

## Source of Truth Updated
- `openspec/specs/cargo-management/spec.md`
- `openspec/specs/cargo-skill-query-contract/spec.md`
- `openspec/specs/sgv-readonly-api/spec.md`

## Audit Trail Inventory
- `proposal.md` — intención, alcance, riesgos y criterios de éxito del change.
- `exploration.md` — análisis del estado actual, alternativas y recomendación técnica.
- `design.md` — decisiones de arquitectura, data flow, archivos a tocar y estrategia de testing.
- `specs/cargo-management/spec.md` — delta spec del dominio cargos.
- `specs/cargo-skill-query-contract/spec.md` — delta spec del subrecurso de skills de cargos.
- `specs/sgv-readonly-api/spec.md` — delta spec de acceso público/anónimo versus endpoints de cargos.
- `tasks.md` — plan ejecutable con 15/15 tasks marcadas `[x]`.
- `apply-progress.md` — evidencia TDD, archivos modificados, desviaciones y estado de implementación.
- `verify-report.md` — evidencia de build/tests, matriz de cumplimiento y clasificación del CRITICAL preexistente.

## SDD Cycle Complete
El change fue planificado, implementado, verificado y archivado. Próximo change puede comenzar.
