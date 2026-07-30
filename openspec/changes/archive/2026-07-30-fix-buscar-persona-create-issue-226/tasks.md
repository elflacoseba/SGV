# Tasks: fix-buscar-persona-create-issue-226

> Issue: #226 | PR: #227 | Change: `fix-buscar-persona-create-issue-226`
> Artifact store: híbrido (OpenSpec + Engram)
> Delivery: single PR
> Review budget: 400 líneas de código
> Strict TDD MODE activo

## Resumen

3 tareas totales. 3 completadas. Scope total: 5 archivos, 606 insertions, 1 deletion.

## Work units

### ✅ WU-01 — Exploration & diagnosis [COMPLETADO]

**Descripción:** Investigar el bug, confirmar causa raíz vía análisis estático + tests empíricos. Documentar en `exploration.md`.

**Acceptance:**
- HTML server-rendered de `/seguridad/usuarios/crear` y `/organizacion/ocupaciones/crear` inspeccionado.
- Causa raíz identificada y documentada.
- Artefacto `openspec/changes/fix-buscar-persona-create-issue-226/exploration.md` escrito.

**Output:** `exploration.md` (118 líneas). Reporte del agente `sdd-explore` (con corrección posterior: refutación del "3 botones comparten el bug" — sólo la línea 245 está mal).

### ✅ WU-02 — Strict TDD red + green [COMPLETADO]

**Descripción:** Aplicar el fix usando TDD estricto.

**Acceptance:**
- Red: test ad-hoc `Issue226CreatePageTests.cs` corregido a regex estricta con `#`; ambos tests fallan.
- Green: `data-bs-target="@modalId"` → `data-bs-target="#@modalId"` en `_PersonaCard.cshtml` línea 245; ambos tests pasan.
- Refactor: innecesario (cambio de 1 carácter).

**Output:** 1 línea de producción + 1 línea de test corregida + 1 regression test nuevo (`Issue226RegressionTests.cs`).

### ✅ WU-03 — Suite completa + PR [COMPLETADO]

**Descripción:** Validar sin regresiones y abrir PR.

**Acceptance:**
- Suite Web completa: 1341/1341 PASS, 0 FAIL, 0 SKIP.
- Commit con mensaje conventional `fix(web):` y descripción del fix.
- Push a `origin/feat/fix-buscar-persona-create-issue-226`.
- PR #227 abierto a `develop`.

**Output:** commit `f14a872`, PR #227.

### ✅ WU-04 — Verify adversarial [COMPLETADO]

**Descripción:** Validación adversarial del fix por agente `sdd-verify`.

**Acceptance:**
- Build + suite Web pasan.
- Causa raíz confirmada.
- Sin regresiones.
- Verdict: PASS.

**Output:** `verify-report.md` escrito. Mem_save id `obs-6d7ce914dfc184da` (id 1543).

### ⏳ WU-05 — Archive [PENDIENTE]

**Descripción:** Sincronizar delta specs al spec baseline, generar archive-report.md, cerrar change.

**Acceptance:**
- `specs/issue-226/spec.md` con escenarios Given/When/Then.
- `archive-report.md` con resumen de cierre.
- Issue #226 cerrable.

**Output:** `specs/issue-226/spec.md` + `archive-report.md` (en curso).

## Pendiente para merge

- Aprobación del PR #227.
- Merge a `develop`.
- Cerrar la issue #226 con referencia al PR.
