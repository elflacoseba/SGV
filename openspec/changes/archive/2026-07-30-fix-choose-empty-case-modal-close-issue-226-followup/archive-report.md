# Archive Report: fix-choose-empty-case-modal-close-issue-226-followup

> Change: `fix-choose-empty-case-modal-close-issue-226-followup`
> Issue: [#226](https://github.com/elflacoseba/SGV/issues/226) (follow-up de #227)
> PR: [#228](https://github.com/elflacoseba/SGV/pull/228)
> Verdict: **READY TO MERGE** ✅

## Resumen

Fix del bug "el modal no se cierra ni devuelve la persona al seleccionarla en el Caso 6" — USBJS-02 revisado de "abortar" a "elegir camino de presentación".

## Timeline

- **Causa raíz**: el fix #224 (USBJS-02) decidió abortar `choose()` con early return cuando los elementos del contrato no estaban presentes en el Caso 6. Eso dejó el modal sin cerrar, el `submit` deshabilitado y el evento `change` sin disparar.
- **TDD strict**: red (10 tests con código viejo) → 7/10 FAIL; green (10 tests con fix) → 10/10 PASS.
- **Validate**: suite Web completa 1348/1348 PASS, 0 FAIL, 0 SKIP.
- **PR**: #228 abierto a `develop`, `mergeStateStatus=CLEAN`, `mergeable=MERGEABLE`.
- **Commit**: `1ee9c80 fix(web): close modal and render card dynamically on choose in empty case (#226-followup)`.

## Artefactos generados

- `proposal.md` — contexto, causa raíz, approach, criterios de aceptación.
- `design.md` — 3 decisiones técnicas (sin early return, renderDynamicCard, handleQuitar).
- `tasks.md` — 3 work units completados.
- `specs/usuario-persona-buscador-js/spec.md` — delta spec MODIFIED sobre USBJS-02.
- `verify-report.md` — verdict PASS con matrix de cumplimiento 8/8 escenarios.

## Verificación final

- Build: 0 errors, 0 warnings.
- Suite Web: 1348/1348 PASS, 0 FAIL, 0 SKIP.
- Back-compat: casos 4/5 sin cambios; Caso 6 estrictamente más correcto.

## Spec USBJS-02 — Revisión documentada

El spec USBJS-02 del change #224 queda **MODIFIED** por este delta. El comportamiento del Caso 6 cambia de "abortar" a "elegir camino de presentación":

| Aspecto | USBJS-02 original (#224) | USBJS-02 revisado (#226-followup) |
|---|---|---|
| Caso 6 `choose()` | Aborta con `return` temprano | Invoca `renderDynamicCard(text)` |
| Modal | NO se cierra | SIEMPRE se cierra |
| `submit` | Queda deshabilitado | SIEMPRE se habilita |
| `change` event | NO se dispara | SIEMPRE se dispara |
| Console.warn | Sí (diagnóstico) | Sí (diagnóstico, se mantiene) |

El delta spec en el archive (`specs/usuario-persona-buscador-js/spec.md`) documenta el nuevo contrato con 5 escenarios Given/When/Then que cubren Caso 4/5/6 y las funciones `renderDynamicCard`/`handleQuitar`.

El spec base USBJS-02 del change #224 se conserva en `archive/2026-07-30-fix-persona-card-empty-state-issue-224/specs/usuario-persona-buscador-js/spec.md` — no se modifica físicamente para mantener auditoría histórica.

## Estado del PR #228

```
mergeStateStatus: CLEAN
mergeable:       MERGEABLE
reviewDecision:  (sin reviews aún)
```

✅ **PR #228 sin conflictos con develop. Listo para merge directo.**

> **Nota**: cuando se emitió el verify-report, el PR mostraba `mergeable_state=unstable` (conflicto temporal con develop durante la preparación de #227). El estado se normalizó a `CLEAN` antes del archive. No hay precondiciones pendientes.

## Lecciones aprendidas

1. **El early return en JS es un anti-patrón cuando hay operaciones críticas después.** El fix #224 fue demasiado conservador: abortó el flujo completo en lugar de separar las fases (mutación del display vs. operaciones críticas como cerrar modal y disparar change).

2. **Las decisiones de diseño deben ser user-centric.** El "NO ocultar el modal en Caso 6" de USBJS-02 era una decisión explícita, pero era incorrecta desde el punto de vista del usuario — el modal debe cerrarse siempre después de una selección.

3. **TDD strict con `git stash` valida que los tests son significativos.** Hacer `git stash` del fix y verificar que los tests fallen confirma que están cubriendo el bug real, no el código actual.

## Pendiente externo (fuera del scope de archive)

- Aprobar y mergear PR #228 a `develop`.
- Cerrar issue #226 cuando PR #228 esté mergeado.
- Considerar Playwright para tests runtime del JS (sugerido en verify-report como mejora futura).

## Status final

**READY TO MERGE** — PR #228 limpio, 0 conflictos, 0 precondiciones. El change está completamente archivado y listo para el merge.
