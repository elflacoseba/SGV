# Tasks: fix-choose-empty-case-modal-close-issue-226-followup

> Issue: #226 (follow-up) | PR: #228 | Change: `fix-choose-empty-case-modal-close-issue-226-followup`
> Artifact store: híbrido (OpenSpec + Engram)
> Delivery: single PR
> Review budget: 400 líneas
> Strict TDD MODE activo

## Resumen

3 tareas totales. 3 completadas. Scope: 2 archivos, 505 insertions, 28 deletions.

## Work units

### ✅ WU-01 — TDD red: tests de source inspection del nuevo contrato

**Descripción:** Escribir 10 tests que validan el nuevo contrato USBJS-02 (relajado del "abortar" al "elegir camino de presentación"). Los tests son source inspection (regex sobre el código del JS).

**Acceptance:**
- 10 tests cubren: `choose()` no tiene early return, `choose()` siempre dispara change, siempre cierra modal, siempre habilita submit; existe `renderDynamicCard`, `handleQuitar`; `renderDynamicCard` crea card + text + quitar + cambiar; `handleQuitar` limpia display dinámico.
- Con código VIEJO: 7/10 tests FAIL.
- Con código NUEVO: 10/10 tests PASS.

**Output:** `tests/SGV.Tests/Web/Tests/Issue226FollowupChooseTests.cs` (438 líneas, 10 tests).

### ✅ WU-02 — TDD green: refactor de `choose()` + `renderDynamicCard` + `handleQuitar`

**Descripción:** Aplicar el fix al JS:
1. Eliminar `return` temprano en `choose()`.
2. Agregar `renderDynamicCard(text)` para Caso 6.
3. Refactorizar handler Quitar a `handleQuitar()` nombrada.
4. Bindear `handleQuitar` en forEach inicial y en botones dinámicos.

**Acceptance:**
- Tests WU-01 pasan (10/10).
- Suite Web completa sigue pasando (1348/1348).

**Output:** `src/SGV.Web/wwwroot/js/pages/usuario-persona-buscador.js` (262 → 351 líneas, +95 / -28).

### ✅ WU-03 — Suite completa + PR

**Descripción:** Validar sin regresiones y abrir PR.

**Acceptance:**
- Suite Web: 1348/1348 PASS.
- Commit con mensaje conventional `fix(web):` y descripción del fix.
- Push a `origin/feat/fix-choose-empty-case-modal-close-issue-226-followup`.
- PR #228 abierto a `develop`.

**Output:** commit `1ee9c80`, PR #228.
