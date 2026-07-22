# Archive Report: agrega-navegacion-personas-habilidades

> Issue #187 — "Agregar botón para ver las Habilidades de una Persona (y botón reverso para ver las Personas de una Habilidad)"
> Fecha de archive: 2026-07-22
> Artifact store: hybrid (openspec + engram)

## Resumen ejecutivo

El change `agrega-navegacion-personas-habilidades` cierra el gap de navegación cruzada `Persona↔Habilidad` que faltaba desde los listados principales. Se entregaron **3 PRs chained stacked-to-main** (#188, #189, #190) que implementan: botón admin-only "Habilidades" en `Personas/Index` (PR A), backend completo del subreverso `GET /api/v1/skills/{skillId}/personas` con DTOs, repositorio, servicio, endpoint y firma de cliente tipado (PR B), y frontend del subreverso con página `Habilidades/Personas` readonly + botón "Personas" en `Habilidades/Index` (PR C). La suite final alcanzó **2,824 PASS / 0 FAIL** y `bun run build` pasó sin errores.

Se promovió **1 spec nueva** (`skill-persona-query-contract`) como capacidad independiente en `openspec/specs/` y se fusionaron **3 deltas** en capacidades existentes: `habilidad-web-listado-detalle-baja` (+1 modified, +3 added requirements), `habilidad-management` (+4 added requirements), y `persona-management` (+4 added requirements). El change queda archivado en este reporte; el folder del change permanece en `openspec/changes/agrega-navegacion-personas-habilidades/` para consolidación posterior por el flujo OpenSpec.

## PRs mergeados

| PR | Slice | Rama | Estado |
|----|-------|------|--------|
| [#188](https://github.com/elflacoseba/SGV/pull/188) | A — UI Personas | `feat/agrega-navegacion-personas-habilidades-pr-a-ui-personas` | ✅ MERGED |
| [#189](https://github.com/elflacoseba/SGV/pull/189) | B — Backend subreverso | `feat/agrega-navegacion-personas-habilidades-pr-b-backend` | ✅ MERGED |
| [#190](https://github.com/elflacoseba/SGV/pull/190) | C — Frontend subreverso | `feat/agrega-navegacion-personas-habilidades-pr-c-frontend` | ✅ MERGED |

**Merge commit de cierre**: `cafc590c` (PR #190 → `develop`)

## Capacidades afectadas

| Capacidad | Acción | Path destino | Requisitos añadidos | Escenarios añadidos |
|-----------|--------|--------------|:-------------------:|:-------------------:|
| **skill-persona-query-contract** | 🆕 NEW (spec completa) | `openspec/specs/skill-persona-query-contract/spec.md` | 7 (REQ-SPQC-01..07) | 9 |
| **habilidad-web-listado-detalle-baja** | 🔀 DELTA merged | `openspec/specs/habilidad-web-listado-detalle-baja/spec.md` | +1 MODIFIED (Acciones contextuales), +3 ADDED (REQ-HLD-NEW, REQ-HLD-NEW-VISIBILITY, REQ-HLD-NEW-POSITION) | +5 |
| **habilidad-management** | 🔀 DELTA merged | `openspec/specs/habilidad-management/spec.md` | +4 ADDED (REQ-HM-NEW-PAGE, REQ-HM-NEW-AUTH, REQ-HM-NEW-READONLY, REQ-HM-NEW-LINK) | +5 |
| **persona-management** | 🔀 DELTA merged | `openspec/specs/persona-management/spec.md` | +4 ADDED (REQ-PM-NEW, REQ-PM-NEW-ADMIN, REQ-PM-NEW-POSITION, REQ-PM-NEW-CONTEXT) | +4 |

## Specs promovidas (Source of Truth)

| Capacidad | Path | Requisitos | Escenarios |
|-----------|------|:----------:|:----------:|
| skill-persona-query-contract | `openspec/specs/skill-persona-query-contract/spec.md` | 7 | 9 |
| habilidad-web-listado-detalle-baja | `openspec/specs/habilidad-web-listado-detalle-baja/spec.md` | 4 MODIFIED/ADDED | +5 |
| habilidad-management | `openspec/specs/habilidad-management/spec.md` | +4 ADDED | +5 |
| persona-management | `openspec/specs/persona-management/spec.md` | +4 ADDED | +4 |

## Decisiones cerradas

### De la propuesta (D1–D5)

| Decisión | Resolución |
|----------|------------|
| **D1 — Auth página `Personas.cshtml`** | `[Authorize]` sin rol (read-only autenticado). Cualquier autenticado puede consultar; anónimo redirige a sign-in. |
| **D2 — Alcance funcional** | Solo lectura + link a detalle de persona. Gestión del vínculo sigue en `Personas/PersonaHabilidades`. |
| **D3 — Botón en `Personas/Index`** | Admin-only con `@if (Model.EsAdministrador)`. Mismo criterio que `Details.cshtml`. |
| **D4 — Paginación/segmento endpoint** | `page`, `pageSize`, `search`, `sort`, `status` (default `activas`). `status` filtra por segmento de **persona** (`PersonaSegmentoListado`), no de habilidad. Query param HTTP se llama `status`. |
| **D5 — Shape `SkillPersonaDetailDto`** | `(PersonaDto Persona, NivelHabilidadDto Nivel)` + `PersonaId`, `NivelHabilidadId`, `HabilidadId` init-only. Sin `VerificadoAt`/`Fuente`. |

### Decisiones técnicas durante implementación

| Decisión | Resolución |
|----------|------------|
| Helper `BuildHabilidadesRouteValues` | Firma `BuildHabilidadesRouteValues(Guid id)` que lee `CurrentPage`, `Search`, `Sort`, `Segmento` del PageModel state (como `BuildEditRouteValues` existente), en lugar de recibir `PersonaListQuery` que no está expuesto. |
| Helper `BuildPersonasRouteValues` | Usa `RouteValueDictionary` (no anonymous object) para que `Segmento == null` no se serialice como `?status=` en vista activas. Espejo del precedent `BuildCargosRouteValues`. |
| PageModel más simple que `Cargos.cshtml.cs` | No expone `EsAdministrador` (consistente con REQ-HM-NEW-AUTH: sin restricción de rol). |

## Veredicto del verify

**`PASS WITH WARNINGS`** — 0 CRITICAL, 3 WARNING, 3 SUGGESTION.

| # | Tipo | Descripción | Acción |
|---|------|-------------|--------|
| W1 | Warning | 84 build warnings pre-existentes (CS8524, xUnit1031, CS8602, NU1510) — no introducidos por este change | Track en issues separadas; no bloquea archive |
| W2 | Warning | Tamaño real (1330 insertions) supera estimate (850–1080) — explicable por triangulación Strict TDD exhaustiva | Futura planificación debe asumir +50% overhead por tests/docs |
| W3 | Warning | `src/SGV.Web/wwwroot/js/pages/auth-password.js` modificada incidentalmente (working copy contamination, no relacionada al change) | Commit separado o revert; no bloquea archive |

## Métricas finales

| Métrica | Valor |
|---------|-------|
| PRs | 3 (#188, #189, #190) |
| Commits originales | 10 |
| Líneas insertadas totales | ~1,330 |
| Tests del change | 37 (3 PR A + 13 PR B + 21 PR C) |
| Suite final | 2,824 PASS / 0 FAIL / 0 SKIPPED |
| `dotnet build SGV.slnx` | ✅ 0 errors |
| `bun run build` | ✅ exit 0 |

## State of the Artifacts

| Artefacto | Path | Estado |
|-----------|------|--------|
| Proposal | `openspec/changes/agrega-navegacion-personas-habilidades/proposal.md` | ✅ Cerrado |
| Design | `openspec/changes/agrega-navegacion-personas-habilidades/design.md` | ✅ Cerrado |
| Tasks | `openspec/changes/agrega-navegacion-personas-habilidades/tasks.md` | ✅ 21/21 tareas completas (3 PRs) |
| Apply Progress | `openspec/changes/agrega-navegacion-personas-habilidades/apply-progress.md` | ✅ 3 PRs documentados |
| Verify Report | `openspec/changes/agrega-navegacion-personas-habilidades/verify-report.md` | ✅ PASS WITH WARNINGS |
| **Archive Report** | `openspec/changes/agrega-navegacion-personas-habilidades/archive-report.md` | ✅ **Este archivo** |

## Sync details

### Spec nueva creada

- **skill-persona-query-contract**: copiada íntegramente de `openspec/changes/agrega-navegacion-personas-habilidades/specs/skill-persona-query-contract/spec.md` → `openspec/specs/skill-persona-query-contract/spec.md`. No existía spec upstream previa.

### Deltas mergeados

| Spec existente | Acción | Requisito(s) | Detalle |
|----------------|--------|-------------|---------|
| `habilidad-web-listado-detalle-baja/spec.md` | MODIFIED | Acciones contextuales por segmento | Reemplazado el requirement completo: la vista activa ahora incluye `Personas` entre `Cargos` y `Editar`; la vista eliminada ahora excluye explícitamente `Personas`. |
| `habilidad-web-listado-detalle-baja/spec.md` | ADDED | REQ-HLD-NEW, REQ-HLD-NEW-VISIBILITY, REQ-HLD-NEW-POSITION | Tres nuevos requisitos que detallan el botón Personas, su gating de visibilidad y su posición en la columna Acciones. |
| `habilidad-management/spec.md` | ADDED | REQ-HM-NEW-PAGE, REQ-HM-NEW-AUTH, REQ-HM-NEW-READONLY, REQ-HM-NEW-LINK | Cuatro nuevos requisitos que describen la página `Habilidades/Personas`, su auth, su naturaleza read-only y el enlace al detalle de persona. |
| `persona-management/spec.md` | ADDED | REQ-PM-NEW, REQ-PM-NEW-ADMIN, REQ-PM-NEW-POSITION, REQ-PM-NEW-CONTEXT | Cuatro nuevos requisitos que describen el botón Habilidades en `Personas/Index`, su gating admin-only, posición y preservación de contexto. |

**Notas de merge:**
- Merge no destructivo: ningún requisito existente fue eliminado o renumerado.
- IDs nuevos (REQ-HLD-NEW*, REQ-HM-NEW*, REQ-PM-NEW*) no colisionan con IDs existentes en ninguna spec del catálogo.
- Se preservó el formato de cada spec existente (encabezados, bullet styles, terminología).
- La spec nueva (`skill-persona-query-contract`) usa el formato OpenSpec estándar del repo (con **GIVEN/WHEN/THEN** en bold), consistente con su contraparte `skill-cargo-query-contract`.

## Riesgos abiertos transferidos al catálogo

- **R-NEW-1 (auth-password.js contamination)**: `src/SGV.Web/wwwroot/js/pages/auth-password.js` tiene cambios no relacionados en working copy. Debe limpiarse antes del próximo commit.
- **R-NEW-2 (fixture de PageModel compartido)**: `HabilidadesPersonasPageTests` y `HabilidadesIndexPersonasButtonTests` usan `WebApplicationFactory` compartido. Si el fixture cambia (ej. Program.cs actualizado), estos tests requerirían actualización.
- **R-NEW-3 (cobertura de archivos cambiados)**: No se corrió `--collect:"XPlat Code Coverage"` en este change. Para auditoría futura, podría agregarse como gate opcional.

## Próximos pasos

1. **Limpiar working copy**: commit separado o revert de `src/SGV.Web/wwwroot/js/pages/auth-password.js` para eliminar contaminación incidental (W3).
2. **Actualizar `docs/decisiones-implementacion.md`**: documentar el nuevo endpoint `GET /api/v1/skills/{id}/personas` y la página `/organizacion/habilidades/{id}/personas` como subrecursos readonly autenticados.
3. **Cierre del ciclo SDD**: el orquestador decidirá si mover el folder del change a `openspec/changes/archive/`.

## Observaciones Engram (traceability)

| Topic | ID |
|-------|:--:|

**Estado**: `success` — change completo, specs promovidas, archive-report generado.
