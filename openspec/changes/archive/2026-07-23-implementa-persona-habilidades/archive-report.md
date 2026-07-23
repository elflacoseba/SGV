# Archive Report: Implementa Persona-Habilidades

## Resumen ejecutivo

El change `implementa-persona-habilidades` completa la gestión web de habilidades por persona (`Persona ↔ Habilidad`), disponible hasta ahora solo en backend. Se entregaron **4 PRs stacked-to-main** (#183, #184, #185, #186) que implementan, en orden: migración atómica de wire-types `PersonaSkill*` a `SGV.Contracts` con taxonomía `ErrorCategoria` (Slice 1), cliente HTTP tipado con fakes (Slice 2), PageModel GET + autorización + vista Razor (Slice 3a), y handlers POST + PRG + bridge JWT + enlace desde Details (Slice 3b). La suite final alcanzó **2,787 PASS / 0 FAIL** y `bun run build` pasó sin errores.

Se promovió **1 spec nueva** (`persona-skill-web-management`) como capacidad independiente en `openspec/specs/` y se fusionaron **2 deltas** en capacidades existentes: `persona-management` (+1 requisito, +3 escenarios) y `commandresult-error-taxonomy` (+3 requisitos, +4 escenarios). El change queda archivado en este reporte; el folder del change permanece en `openspec/changes/implementa-persona-habilidades/` para consolidación posterior por el flujo OpenSpec.

## PRs mergeados

| PR | Slice | Rama | Estado |
|----|-------|------|--------|
| [#183](https://github.com/elflacoseba/SGV/pull/183) | 1 — Migrar wire-types + ErrorCategoria | `feat/implementa-persona-habilidades-pr1` | ✅ MERGED |
| [#184](https://github.com/elflacoseba/SGV/pull/184) | 2 — Cliente tipado + fakes | `feat/implementa-persona-habilidades-pr2` | ✅ MERGED |
| [#185](https://github.com/elflacoseba/SGV/pull/185) | 3a — PageModel GET + autorización + vista | `feat/implementa-persona-habilidades-pr3a` | ✅ MERGED |
| [#186](https://github.com/elflacoseba/SGV/pull/186) | 3b — Handlers POST + PRG + Details + bridge + bun | `feat/implementa-persona-habilidades-pr3b` | ✅ MERGED |

**Merge commit de cierre**: `01f36a9d` (PR #186 → `develop`)

## Capacidades afectadas

| Capacidad | Acción | Path destino |
|-----------|--------|--------------|
| **persona-skill-web-management** | 🆕 NEW (spec completa) | `openspec/specs/persona-skill-web-management/spec.md` — 7 requirements, 8 scenarios |
| **persona-management** | 🔀 DELTA merged | `openspec/specs/persona-management/spec.md` — +1 requisito (navegación a Habilidades), +3 escenarios |
| **commandresult-error-taxonomy** | 🔀 DELTA merged | `openspec/specs/commandresult-error-taxonomy/spec.md` — +3 requisitos (REQ-10, REQ-11, REQ-12), +4 escenarios |

## Decisiones cerradas

### De la propuesta (#1284 — «Persona-Habilidades decisiones de producto cerradas»)

| Decisión | Resolución |
|----------|------------|
| `VerificadoAt`/`Fuente` | **Diferidos**. No se exponen en UI ni en DTOs de este change. |
| Acceso a la página | **Solo rol `Administrador`** (lectura y escritura). Paridad con `CargoHabilidades`. |
| Persona inactiva | **Bloquear gestión**. GET redirige a `/error/404`; POST redirige con TempData warning sin invocar al cliente. |
| Taxonomía de errores | **Adoptar `ErrorCategoria`**. `PersonaSkillErrorType` interno; mapper común sin enum paralelo. |

### De Slice 2 (#1295 — «Persona-Habilidades decisiones Slice 2»)

| Decisión | Resolución |
|----------|------------|
| Overage de review budget | **Aceptado `size:exception`** (1,334 líneas netas vs budget 400). Fidelidad al patrón `CargoSkillApiClientTests`. |
| `FieldErrors` en `PersonaSkillCommandResult` | **Aprobado**. Source-compat, no breaking. |
| Bridge persona-skill end-to-end | **Diferido a Slice 3b**. Factory extendido en Slice 2; test materializado en Slice 3b. |

### De la estrategia de entrega (#1288 — «Persona-Habilidades estrategia PR 4 slices stacked-to-main»)

| Decisión | Resolución |
|----------|------------|
| Estrategia | **4 PRs stacked-to-main** (Slice 1 → Slice 2 → Slice 3a → Slice 3b). Cada slice < 400 líneas. |

### De merges intermedios (#1303 — «Slices 1, 2, 3a mergeados a develop»)

- PRs #183, #184, #185 mergeados secuencialmente a `develop` con `gh pr merge --merge --delete-branch`.
- PR #184 requirió rebase por cherry-picks duplicados.
- PR #185 requirió `git reflog` + `git reset` + cherry-pick selectivo (rebase skipeó todos los commits por detectar todo el contenido como duplicado).

### De cierre de cadena (#1306 — «Cadena completa PR #186 MERGED»)

- PR #186 mergeado a `develop` con merge commit `01f36a9d`.
- Cadena stacked-to-main completa: 4/4 PRs cerrados.

## State of the Artifacts

| Artefacto | Path | Estado |
|-----------|------|--------|
| Proposal | `openspec/changes/implementa-persona-habilidades/proposal.md` | ✅ Cerrado |
| Design | `openspec/changes/implementa-persona-habilidades/design.md` | ✅ Cerrado |
| Tasks | `openspec/changes/implementa-persona-habilidades/tasks.md` | ✅ 23/23 tareas completas (4 slices) |
| Apply Progress | `openspec/changes/implementa-persona-habilidades/apply-progress.md` | ✅ 4 slices documentados |
| Verify (Slice 1) | `openspec/changes/implementa-persona-habilidades/verify-report.md` | ✅ PASS |
| Verify (Slice 2) | `openspec/changes/implementa-persona-habilidades/verify-report-slice2.md` | ✅ FAIL → needs-fix (scope gate `SgvWebApplicationFactory`) — resuelto en PR #184 |
| Verify (Slice 3a) | — (contenido embebido en `apply-progress.md` § Slice 3a) | ✅ PASS |
| Verify (Slice 3b) | `openspec/changes/implementa-persona-habilidades/verify-report-slice3b.md` | ✅ PASS |
| **Archive Report** | `openspec/changes/implementa-persona-habilidades/archive-report.md` | ✅ **Este archivo** |

## Especificaciones promovidas (Source of Truth)

| Capacidad | Path | Requisitos | Escenarios |
|-----------|------|:----------:|:----------:|
| persona-skill-web-management | `openspec/specs/persona-skill-web-management/spec.md` | 7 | 8 |
| persona-management (delta) | `openspec/specs/persona-management/spec.md` | +1 (R-A1 navegación) | +3 |
| commandresult-error-taxonomy (delta) | `openspec/specs/commandresult-error-taxonomy/spec.md` | +3 (REQ-10..12) | +4 |

## Métricas del change

| Métrica | Valor |
|---------|-------|
| PRs | 4 (#183, #184, #185, #186) |
| Commits originales | ~24 |
| Líneas modificadas totales | ~4,574 |
| Tests nuevos | ~97 (21 + 45 + ~0 + 31 + infraestructura) |
| Suite final | 2,787 PASS / 0 FAIL / 0 SKIPPED |
| `bun run build` | ✅ exit 0 |
| `Co-Authored-By` en commits | 0 ocurrencias |

## Pendientes para futuras sesiones

1. **Resolver estado `escalated` del authority graph de `gentle-ai review`** — líneas `review-dc532bfa2cff5554` y `review-fdd099524d075b31` antes de empezar nuevos PRs con gate nativo.
2. **Refactor de `PersonaSkillFormHelpers`** — actualmente embebido en `PersonaHabilidades.cshtml.cs`. Si en el futuro se extrae a archivo separado (paralelo a `CargoHabilidadesPostHandlers.cs`), es refactor mecánico.
3. **`VerificadoAt`/`Fuente`** — diferidos; pueden volver como cambio separado si el negocio lo requiere.

## Observaciones Engram (traceability)

| Topic | ID |
|-------|:--:|
| `sdd/implementa-persona-habilidades/decisions` | #1284 |
| `sdd/implementa-persona-habilidades/pr-strategy` | #1288 |
| `sdd/implementa-persona-habilidades/slice2-decisions` | #1295 |
| `sdd/implementa-persona-habilidades/slices-1-2-3a-merged` | #1303 |
| `sdd/implementa-persona-habilidades/chain-complete` | #1306 |

**Estado**: `success` — change completo, specs promovidas, archive-report generado.
