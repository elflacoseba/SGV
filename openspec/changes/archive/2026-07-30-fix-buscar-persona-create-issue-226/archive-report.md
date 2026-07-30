# Archive Report: fix-buscar-persona-create-issue-226

> Change: `fix-buscar-persona-create-issue-226`
> Issue: [#226](https://github.com/elflacoseba/SGV/issues/226)
> PR: [#227](https://github.com/elflacoseba/SGV/pull/227)
> Verdict: READY TO MERGE
> Archived: 2026-07-30

## Resumen

Fix de 1 carácter (`#`) en `data-bs-target` del botón "Buscar Persona" del Caso 6 de `_PersonaCard.cshtml`. Bootstrap 5 trata `data-bs-target` como selector CSS vía `document.querySelector(...)`; sin `#`, el modal no se abría.

## Timeline

- **Explore**: agente `sdd-explore` confirmó la causa raíz (selector CSS sin `#`). Nota: el reporte inicial de "3 botones con el bug" fue refutado por inspección línea por línea — solo la línea 245 estaba afectada.
- **TDD strict**: red (test falla con regex estricta `#`) → green (fix 1 línea) → refactor innecesario.
- **Apply**: commit `f14a872` + `4b613e5` (2 commits), push a `origin/feat/fix-buscar-persona-create-issue-226`.
- **Verify**: suite Web 1341/1341 PASS, 0 FAIL, 0 SKIP. Verdict PASS.
- **PR**: #227 abierto a `develop`, en estado `unstable` (sin review aún).

## Artefactos generados

| Artefacto | Líneas | Estado |
|---|---|---|
| `exploration.md` | 118 | ✅ |
| `apply-progress.md` | 99 | ✅ |
| `proposal.md` | 55 | ✅ |
| `design.md` | 63 | ✅ |
| `tasks.md` | 76 | ✅ |
| `specs/issue-226/spec.md` | 81 | ✅ |
| `verify-report.md` | 218 | ✅ |
| `archive-report.md` | — | ✅ (este archivo) |

## Spec Delta — Decisión de sincronización

El delta spec (`specs/issue-226/spec.md`) define 2 requisitos (BSMODAL-01 y BSMODAL-02) con 6 escenarios sobre la partial `_PersonaCard.cshtml`.

**Decisión: no se sincroniza al spec baseline.**

Razones:
1. No existe `openspec/specs/issue-226/spec.md` — el delta no tiene un spec base propio.
2. El delta es un sub-requirement del spec `reusable-persona-card` (#219), que ya tiene los requisitos de la partial `_PersonaCard.cshtml`.
3. El fix es 1 carácter; el contrato formal del `#` en `data-bs-target` queda documentado en el delta spec y en el archive-report como auditoría, pero no modifica el spec principal de `#219`.
4. Los 6 escenarios del delta están cubiertos por los 3 tests nuevos, que viven en la codebase como regression tests permanentes.

## Verificación final

| Check | Resultado |
|---|---|
| Build `dotnet build src/SGV.Web` | ✅ 0 errors, 0 warnings |
| Suite Web `dotnet test --filter ~SGV.Tests.Web` | ✅ 1341/1341 PASS, 0 FAIL, 0 SKIP |
| Tests del fix `~Issue226` | ✅ 3/3 PASS |
| PR #227 estado | ✅ Abierto, sin conflictos con `develop` |
| Verdict `verify-report.md` | ✅ PASS (0 CRITICAL) |
| Tareas de implementación | ✅ Todas completadas (WU-01 a WU-04) |

## Lecciones aprendidas

1. **Bootstrap 5 `data-bs-target` requiere `#`.** El bundle interno usa `getElementFromSelector(...)` que ejecuta `document.querySelector(target)`. Sin `#`, busca por tag name (no por id).

2. **Los regex permisivos (`#?`) enmascaran bugs.** El test ad-hoc original usaba `#?` por "ser permisivo", lo que dejó pasar el bug. Lección: los tests deben ser estrictos en sus aserciones — la permisividad en los tests es deuda técnica que se cobra después.

3. **Verificar empíricamente los reportes de sub-agentes.** El `sdd-explore` reportó "3 botones comparten el bug", pero la inspección línea por línea refutó esa hipótesis: solo la línea 245 estaba mal. No aceptar diagnósticos de segunda mano sin verificar contra el código.

4. **El análisis línea por línea es irremplazable.** Para bugs aparentemente "globales" en partials compartidas, contar las apariciones de un patrón con `grep` + verificar cada match es el método más confiable.

## Pendiente externo (fuera del scope de archive)

- Aprobación del PR #227 por el maintainer.
- Merge a `develop`.
- Cerrar la issue #226 con referencia al PR.
- (Opcional) Considerar Roslyn analyzer para validar `data-bs-target="^#"` en `.cshtml` y prevenir regresiones futuras (sugerencia documentada en `verify-report.md`).
- (Opcional) Integrar `coverlet.collector` para reportes de cobertura automáticos en CI (sugerencia informativa, no bloqueante).

## Status final

**READY TO MERGE** — change cerrado, todos los artefactos presentes, verify PASS, PR abierto.
