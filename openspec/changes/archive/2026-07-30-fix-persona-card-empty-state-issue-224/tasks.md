# Tasks: fix-persona-card-empty-state-issue-224

> Change: `fix-persona-card-empty-state-issue-224`
> Issue: [#224](https://github.com/elflacoseba/SGV/issues/224)
> Artifact store: **Engram + OpenSpec (híbrido)**
> Delivery strategy: **Single PR** (auto-aceptada, scope pequeño ~59 líneas)
> Review budget: **400 líneas de código**
> Branch: `feat/fix-persona-card-empty-state-issue-224` (base `develop`)

## Review Workload Forecast

| Field | Value |
|-------|-------|
| Estimated changed lines | ~59 líneas de código (3 cambios JS + 1 test + 1 nota docs) |
| 400-line budget risk | **Low** |
| Chained PRs recommended | **No** |
| Suggested split | Single PR (`feat/fix-persona-card-empty-state-issue-224` → `develop`) |
| Delivery strategy | `single-pr` |
| Chain strategy | `size-exception` (no aplica: dentro del budget) |

```text
Decision needed before apply: No
Chained PRs recommended: No
Chain strategy: size-exception
400-line budget risk: Low
```

### Suggested Work Units

| Unit | Goal | Likely PR | Focused test command | Runtime harness | Rollback boundary |
|------|------|-----------|----------------------|-----------------|-------------------|
| 1 | Regression guard del contrato markup caso 6 | PR 1 (parte de single PR) | `dotnet test SGV.slnx --filter "FullyQualifiedName~EditableWithPersonaNullAndNoFallback_DoesNotEmitMutableCardContractAttributes"` | N/A (markup, no JS runtime) | `tests/SGV.Tests/Web/Tests/PersonaCardPartialTests.cs` removable sin tocar producción |
| 2 | Fix USBJS-01 lookup `empty` | PR 1 (parte de single PR) | `bun run build` en `src/SGV.Web` | Smoke test 1 (`Ocupaciones/Create` empty state) | 1 línea en `usuario-persona-buscador.js` L32 — revertible atómicamente |
| 3 | Fix USBJS-02 null-guards en `choose()` | PR 1 (parte de single PR) | `bun run build` + smoke test 1 | Smoke tests 1, 2 (modal sin TypeError) | Bloque L54-83 del archivo JS — revertible sin afectar otros call sites |
| 4 | Fix USBJS-03 null-guards en handler Quitar | PR 1 (parte de single PR) | `bun run build` + smoke test 4 | Smoke test 4 (`Usuarios/_Form` Quitar) | Bloque L215-239 del archivo JS — revertible sin afectar `choose()` |

> Nota: el Single PR admite squash final. Cada task = 1 commit atómico (work-unit-commits).

---

## Resumen de tasks

| # | Task | Esfuerzo | Archivos | Depende de |
|---|------|----------|----------|------------|
| 1 | RED — Regression guard del caso 6 (markup) | 15 min | `tests/SGV.Tests/Web/Tests/PersonaCardPartialTests.cs` | ninguna |
| 2 | GREEN — Fix lookup `empty` (USBJS-01) | 5 min | `src/SGV.Web/wwwroot/js/pages/usuario-persona-buscador.js` | Task 1 |
| 3 | GREEN — Null-guards en `choose()` (USBJS-02) | 15 min | `src/SGV.Web/wwwroot/js/pages/usuario-persona-buscador.js` | Task 2 |
| 4 | GREEN — Null-guards en handler Quitar (USBJS-03) | 10 min | `src/SGV.Web/wwwroot/js/pages/usuario-persona-buscador.js` | Task 3 |
| 5 | DOCS — Nota en `decisiones-implementacion.md` | 10 min | `docs/decisiones-implementacion.md` | Task 4 |
| 6 | VERIFY — Build + suite + smoke tests manuales | 20 min | `verify-report.md` (nuevo) | Task 5 |
| 7 | ARCHIVE — Mover change a `archive/` + cerrar issue | 10 min | `openspec/changes/archive/2026-07-30-.../` | Task 6 |
| **Total** | | **~85 min** | | |

---

## Convenciones

Cada task sigue este formato:

```markdown
### Task N: [Nombre corto]

**Status**: pending | in_progress | completed
**Estimated effort**: ≤ 2h
**Files**: [lista de paths]
**Depends on**: [task IDs o "ninguna"]

**Description**: [2-3 oraciones de qué hace y por qué]

**Acceptance criteria**:
- [ ] Criterio 1
- [ ] Criterio 2
- ...

**Test command** (si aplica):
```bash
dotnet test SGV.slnx --filter "FullyQualifiedName~X"
# o
cd src/SGV.Web && bun run build
```

**Work unit commit**:
```bash
git checkout develop && git pull
git checkout -b feat/fix-persona-card-empty-state-issue-224
# ... cambios ...
git add <files>
git commit -m "<conventional commit message>"
```
```

---

## Tasks

### Task 1: Regression guard del contrato markup del caso 6

**Status**: pending
**Estimated effort**: 15 min
**Files**: `tests/SGV.Tests/Web/Tests/PersonaCardPartialTests.cs`
**Depends on**: ninguna

**Description**: Agregar test que valida el **contrato negativo** del caso 6: la partial NO emite los 4 atributos mutables (`data-usuario-persona-card`, `data-usuario-persona-display-input`, `data-usuario-persona-display-text`, `data-usuario-persona-quitar`). Este test es un **regression guard**, no un test RED→GREEN tradicional: el comportamiento ya existe en la partial (`reusable-persona-card` change #219), el test lo blinda contra regresiones futuras.

**Acceptance criteria**:
- [ ] Test `EditableWithPersonaNullAndNoFallback_DoesNotEmitMutableCardContractAttributes` agregado en `PersonaCardPartialTests.cs` después del test existente L457-475, antes de `private static string BuildQuery(...)` (L477).
- [ ] Test referencia `mode=editable` sin persona ni fallback (caso 6).
- [ ] 4 `Assert.DoesNotContain` sobre los 4 atributos mutables.
- [ ] Test **PASA desde el primer commit** (GREEN inicial; no es RED).
- [ ] Suite .NET previa sigue verde.

**Test command**:
```bash
dotnet test SGV.slnx --filter "FullyQualifiedName~EditableWithPersonaNullAndNoFallback_DoesNotEmitMutableCardContractAttributes"
# Esperado: 1 passed
```

**Work unit commit**:
```bash
git checkout develop && git pull
git checkout -b feat/fix-persona-card-empty-state-issue-224
git add tests/SGV.Tests/Web/Tests/PersonaCardPartialTests.cs
git commit -m "test(web): add regression guard for case 6 card contract (#224)"
```

---

### Task 2: Fix del lookup de `empty` (USBJS-01)

**Status**: pending
**Estimated effort**: 5 min
**Files**: `src/SGV.Web/wwwroot/js/pages/usuario-persona-buscador.js`
**Depends on**: Task 1

**Description**: Corregir el bug latente en el lookup de `empty` (L32). La partial emite el atributo `data-usuario-persona-empty` como **sibling** del contenedor `display` (L242), pero el JS lo busca dentro de `display`. Refactor a `display.parentElement.querySelector(...)` consistente con el comentario de la partial L236-237 y con el patrón ya usado para `displayInput` (L29).

**Acceptance criteria**:
- [ ] L32 cambiada de `display && display.querySelector('[data-usuario-persona-empty]')` a `display && display.parentElement.querySelector('[data-usuario-persona-empty]')`.
- [ ] `bun run build` pasa sin errores en `src/SGV.Web`.
- [ ] Suite .NET completa sigue verde.
- [ ] Test del Task 1 sigue pasando.

**Test command**:
```bash
cd src/SGV.Web && bun run build
dotnet test SGV.slnx
```

**Work unit commit**:
```bash
git add src/SGV.Web/wwwroot/js/pages/usuario-persona-buscador.js
git commit -m "fix(js): read empty state from display.parentElement (#224, USBJS-01)"
```

---

### Task 3: Null-guards en `choose()` (USBJS-02)

**Status**: pending
**Estimated effort**: 15 min
**Files**: `src/SGV.Web/wwwroot/js/pages/usuario-persona-buscador.js`
**Depends on**: Task 2

**Description**: Refactor de `choose()` (L54-71) con guard clauses que abortan limpiamente cuando faltan `displayInput`, `cardText`, `card` o `empty`. Aplica el snippet del `design.md` §3.1.2 (L54-83). Reordena `hiddenInput.value` y `modal.dataset.currentPersonaId` **antes** del bloque abortable para preservar la selección del usuario (D4). El `change` event NO se dispara en aborto (USBJS-02 L55-60).

**Acceptance criteria**:
- [ ] Función `choose()` refactorizada con el snippet verbatim del design §3.1.2.
- [ ] Comentario `// USBJS-02:` presente explicando la decisión.
- [ ] `bun run build` pasa.
- [ ] Suite .NET completa sigue verde.
- [ ] Smoke test manual en `Ocupaciones/Create` con empty state → seleccionar persona → console **sin TypeError** (documentar en `verify-report.md` durante Task 6).

**Test command**:
```bash
cd src/SGV.Web && bun run build
dotnet test SGV.slnx
# Smoke test manual (sin command):
# 1. Levantar SGV.Api + SGV.Web
# 2. Ir a /organizacion/ocupaciones/crear
# 3. Click "Buscar Persona" → seleccionar persona
# 4. DevTools Console: 0 TypeError, 0 warnings inesperados
```

**Work unit commit**:
```bash
git add src/SGV.Web/wwwroot/js/pages/usuario-persona-buscador.js
git commit -m "fix(js): abort choose() when card contract missing (#224, USBJS-02)"
```

---

### Task 4: Null-guards en handler Quitar (USBJS-03)

**Status**: pending
**Estimated effort**: 10 min
**Files**: `src/SGV.Web/wwwroot/js/pages/usuario-persona-buscador.js`
**Depends on**: Task 3

**Description**: Aplicar el mismo patrón defensivo del Task 3 al handler `click` de `[data-usuario-persona-quitar]` (L215-228). Aplica el snippet del `design.md` §3.1.3 (L215-240). En caso 6 el botón Quitar no se emite, así que `querySelectorAll` itera cero nodos (USBJS-03 L83-88); el guard cubre el escenario defensivo de DOM manipulado.

**Acceptance criteria**:
- [ ] Handler Quitar refactorizado con el snippet verbatim del design §3.1.3.
- [ ] Comentario `// USBJS-03:` presente.
- [ ] `bun run build` pasa.
- [ ] Suite .NET completa sigue verde.
- [ ] Smoke test manual en `Usuarios/_Form` con persona precargada → pulsar Quitar → console **sin TypeError** (documentar en Task 6).

**Test command**:
```bash
cd src/SGV.Web && bun run build
dotnet test SGV.slnx
# Smoke test manual:
# 1. Ir a /seguridad/usuarios/crear
# 2. Precargar persona (caso 4)
# 3. Pulsar "Quitar" → empty state aparece
# 4. DevTools Console: 0 TypeError, 0 warnings inesperados
```

**Work unit commit**:
```bash
git add src/SGV.Web/wwwroot/js/pages/usuario-persona-buscador.js
git commit -m "fix(js): abort Quitar handler when card contract missing (#224, USBJS-03)"
```

---

### Task 5: Nota en `decisiones-implementacion.md`

**Status**: pending
**Estimated effort**: 10 min
**Files**: `docs/decisiones-implementacion.md`
**Depends on**: Task 4

**Description**: Agregar la nueva sección `## Frontend / JS compartido` antes de `## Frontend CRUD de Personas` (L483) con el snippet del `design.md` §3.3.1. Documenta el patrón defensivo "lookup defensivo + mutación abortable", el lookup de `empty` desde `parentElement`, y la decisión explícita de no agregar Vitest/Jest al proyecto.

**Acceptance criteria**:
- [ ] Nueva sección `## Frontend / JS compartido` insertada antes de L483.
- [ ] Sub-sección `### Patrón defensivo en \`usuario-persona-buscador.js\` (issue #224)` con el contenido del design.
- [ ] Mención explícita de USBJS-01..03 y referencia a `openspec/changes/fix-persona-card-empty-state-issue-224/`.
- [ ] Decisión de "no agregar Vitest/Jest" documentada.
- [ ] Línea original `## Frontend CRUD de Personas` preservada inmediatamente después.
- [ ] Cambio solo de docs — no se toca código en este commit.

**Test command**:
```bash
# Verificación visual: abrir docs/decisiones-implementacion.md y confirmar:
grep -n "## Frontend / JS compartido" docs/decisiones-implementacion.md
grep -n "Patrón defensivo en" docs/decisiones-implementacion.md
grep -n "USBJS" docs/decisiones-implementacion.md
```

**Work unit commit**:
```bash
git add docs/decisiones-implementacion.md
git commit -m "docs(frontend): note defensivo del bug #224 en decisiones-implementacion.md"
```

---

### Task 6: Verificación completa (build + suite + smoke tests)

**Status**: pending
**Estimated effort**: 20 min
**Files**: `openspec/changes/fix-persona-card-empty-state-issue-224/verify-report.md` (nuevo)
**Depends on**: Task 5

**Description**: Ejecutar la validación completa del change: build .NET, suite completa, bundle frontend, 4 smoke tests manuales. Documentar resultados en `verify-report.md`.

**Acceptance criteria**:
- [ ] `dotnet build SGV.slnx` sin errores, 0 warnings nuevos.
- [ ] `dotnet test SGV.slnx` verde (incluido el test del Task 1).
- [ ] `cd src/SGV.Web && bun run build` verde.
- [ ] 4 smoke tests manuales ejecutados:
  - [ ] Smoke 1: `Ocupaciones/Create` empty state → modal → seleccionar persona → console limpia.
  - [ ] Smoke 2: `Ocupaciones/Edit` con `PersonaId=Guid.Empty` y fetch fallido → modal → seleccionar persona → console limpia.
  - [ ] Smoke 3: `Usuarios/Create` con persona precargada (caso 4) → seleccionar otra persona → modal cierra, card actualizada.
  - [ ] Smoke 4: `Usuarios/_Form` con persona precargada → pulsar Quitar → empty aparece.
- [ ] `verify-report.md` creado con secciones: comandos ejecutados, resultados, observaciones, diff stats.
- [ ] `git diff --stat develop` muestra ≤ 400 líneas de código (meta: ~59 líneas).

**Test command**:
```bash
dotnet build SGV.slnx
dotnet test SGV.slnx
cd src/SGV.Web && bun run build
git diff --stat develop
# Smoke tests manuales ejecutados en navegador (no automatizados)
```

**Work unit commit**:
```bash
git add openspec/changes/fix-persona-card-empty-state-issue-224/verify-report.md
git commit -m "docs(sdd): verify-report for #224"
```

---

### Task 7: Archivo del change + merge + cierre de issue

**Status**: pending
**Estimated effort**: 10 min
**Files**: `openspec/changes/archive/2026-07-30-fix-persona-card-empty-state-issue-224/` (movido)
**Depends on**: Task 6

**Description**: Mover el directorio del change a `archive/2026-07-30-fix-persona-card-empty-state-issue-224/`, crear `archive-report.md` con resumen + criterios cumplidos + líneas modificadas, abrir PR contra `develop`, mergear, cerrar la issue #224 con referencia al PR.

**Acceptance criteria**:
- [ ] Directorio `openspec/changes/fix-persona-card-empty-state-issue-224/` movido a `openspec/changes/archive/2026-07-30-fix-persona-card-empty-state-issue-224/`.
- [ ] `archive-report.md` creado con: resumen del change, criterios de aceptación cumplidos (checklist), líneas modificadas (~59 código), referencia al PR mergeado, referencia a la issue #224.
- [ ] PR abierto contra `develop` (base `develop`, head `feat/fix-persona-card-empty-state-issue-224`).
- [ ] PR mergeado (squash permitido).
- [ ] Branch `feat/fix-persona-card-empty-state-issue-224` eliminada post-merge.
- [ ] Issue #224 cerrada con `state_reason=completed` y comentario referenciando el PR.

**Test command**:
```bash
# Verificación post-archive:
ls openspec/changes/archive/2026-07-30-fix-persona-card-empty-state-issue-224/
gh pr view --json state,mergedAt,title
gh issue view 224 --json state,stateReason
```

**Work unit commit**:
```bash
git add openspec/changes/archive/2026-07-30-fix-persona-card-empty-state-issue-224/
git commit -m "docs(sdd): archive report for #224"
```

---

## Estimación total

| Task | Esfuerzo |
|------|----------|
| 1 | 15 min |
| 2 | 5 min |
| 3 | 15 min |
| 4 | 10 min |
| 5 | 10 min |
| 6 | 20 min |
| 7 | 10 min |
| **Total** | **~85 min** (1h 25min) |

---

## Work unit commits — convención

- Cada task = **1 commit atómico**.
- Mensajes en [Conventional Commits](https://www.conventionalcommits.org/) (sin `Co-Authored-By` ni atribución a IA).
- Branch de feature: `feat/fix-persona-card-empty-state-issue-224` (basada en `develop`).
- **Squash final opcional** al merge, según criterio del maintainer (single PR).

Ejemplo de flujo de trabajo por task:
```bash
git checkout develop && git pull
git checkout feat/fix-persona-card-empty-state-issue-224
# ... aplicar cambios ...
git add <files>
git commit -m "<mensaje>"
```

---

## Plan de ejecución (sdd-apply)

Orden de ejecución recomendado:

1. **Branch setup** (fuera de las tasks):
   ```bash
   git checkout develop && git pull
   git checkout -b feat/fix-persona-card-empty-state-issue-224
   ```
2. Ejecutar **Task 1** (test nuevo) — 1 commit.
3. Ejecutar **Task 2** (lookup `empty`) — 1 commit.
4. Ejecutar **Task 3** (null-guards `choose()`) — 1 commit.
5. Ejecutar **Task 4** (null-guards Quitar) — 1 commit.
6. Ejecutar **Task 5** (docs) — 1 commit.
7. Ejecutar **Task 6** (verify + `verify-report.md`) — 1 commit.
8. **Abrir PR** contra `develop` (single PR; squash de los 7 commits según preferencia del maintainer).
9. Ejecutar **Task 7** (archive + merge + cerrar issue) — 1 commit de docs + acciones GitHub.

Alternativa: squash en grupos lógicos (3 commits JS en 1, docs en 1, archive en 1) si el maintainer prefiere historial limpio.

---

## Riesgos operativos

| Riesgo | Severidad | Mitigación |
|--------|-----------|------------|
| Test del Task 1 falla por cambios imprevistos en `PersonaCardPartialTests.cs` o `PersonaCardHarness.cshtml` | Alta | Parar y revisar antes de continuar; el test debe pasar desde el primer commit (GREEN inicial). |
| `bun run build` falla por issues de bundling del JS modificado | Media | Verificar que el archivo sigue siendo IIFE válido (estructura `(function() { ... })();` intacta). Revertir el cambio puntual si el bundle falla. |
| Smoke tests manuales revelan regresiones en casos 1-5 | Alta | Detener el change; investigar el bug antes de merge. El test del Task 1 blinda el contrato markup, pero no detecta regresiones JS en casos ya soportados. |
| Bundle pipeline ignora el cambio en `usuario-persona-buscador.js` | Baja | Confirmar que el archivo está referenciado en `gulpfile` / `package.json`; revisar el manifest de bundle generado. |
| Reorden de `hiddenInput.value` antes del guard en `choose()` rompe lógica externa que asume el orden | Baja | El `change` event NO se dispara en aborto (USBJS-02 L55-60); cualquier handler externo solo escucha cambios válidos. Documentado en el comentario del código (L56-58). |
| PR se acerca al budget de 400 líneas por agregar docs accidentales | Baja | El Task 5 es el único cambio de docs en código. `verify-report.md` y `archive-report.md` son SDD artifacts (no cuentan para budget). |