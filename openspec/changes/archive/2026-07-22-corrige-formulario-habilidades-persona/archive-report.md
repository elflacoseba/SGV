# Archive Report — corrige-formulario-habilidades-persona

## Resumen

Delta sobre `implementa-persona-habilidades`: el formulario "Asignar" de `/personas/{id:guid}/habilidades` no cargaba los catálogos de habilidades ni niveles. Se inyectó `IHabilidadApiClient`, se agregó helper `LoadCatalogsAsync` con carga paralela y degradación aceptable, se pobló el ViewModel con dos nuevas colecciones, y se iteraron en los `<select>` de la vista. Build + suite completa pasan.

## Archivos de implementación

| Commit | Mensaje | Archivos | ± Líneas |
|--------|---------|----------|----------|
| `c51bc8a8` | fix(personas): poblar selects de habilidad/nivel en form Asignar | 3 | +89 / -7 |
| `11b04b41` | test(personas): cubrir carga de catálogos en el page de Habilidades | 2 | +116 / -2 |

**Total**: 5 archivos, 205 inserciones, 9 eliminaciones.

## Stats

| Métrica | Valor |
|---------|-------|
| Commits de implementación | 2 |
| Archivos modificados | 5 |
| Líneas agregadas | 205 |
| Líneas eliminadas | 9 |
| Tests nuevos de delta | 3 |
| Suite completa | 2827 / 2827 PASS |
| Requirements en spec delta | 5 (REQ-01 a REQ-05) |
| Veredicto verify | VERIFIED |

## Traceability — Engram Observation IDs

| Artifact | ID |
|----------|----|
| Session Preflight | #1324 |
| Bug encontrado | #1325 |
| Proposal | #1326 |
| Spec | #1327 |
| Design | #1328 |
| Tasks | #1329 |
| Apply | #1330 |
| Verify | #1332 |
| Archive (este) | (actual) |

## Specs canónicos actualizados

- `openspec/specs/persona-skill-web-management/spec.md` — 6 requirements originales preservados + 5 nuevos apendados (Carga paralela de catálogos, Vista itera catálogos, POST preserva comportamiento, Degradación aceptable, ViewModel expone colecciones). Nota sobre contratos `skillId`/`nivelId` preservada al final.

## Stale Checkbox Reconciliation

Los tasks en `tasks.md` — persistidos tanto en filesystem como en Engram #1329 — quedaron como `- [ ]` sin marcar, aunque `verify-report.md` demuestra que los 7 grupos de tareas (T1–T7) se completaron correctamente: build 0 errors, suite 2827/2827 PASS, 3 tests nuevos en verde, 5 requirements validados. El orchestrator instruyó explícitamente el archive, y el verify-report provee evidencia suficiente de completitud. No se requiere re-aplicación de `sdd-apply`.

## Archivo movido

`openspec/changes/corrige-formulario-habilidades-persona/` → `openspec/changes/archive/2026-07-22-corrige-formulario-habilidades-persona/`

## Contenido del archive

- proposal.md ✅
- specs/persona-skill-web-management/spec.md ✅ (delta spec)
- design.md ✅
- tasks.md ✅ (7/7 tasks — stale checkboxes, completitud probada por verify)
- verify-report.md ✅ (VERIFIED)
- archive-report.md ✅ (este documento)

## SDD Cycle Complete

El cambio fue planificado, especificado, diseñado, implementado, verificado y archivado.
