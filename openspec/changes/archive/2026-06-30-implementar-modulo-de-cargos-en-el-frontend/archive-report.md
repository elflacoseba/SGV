# Archive Report: Implementar el módulo de Cargos en el Frontend

**Change slug**: `implementar-modulo-de-cargos-en-el-frontend`
**Archived at**: 2026-06-30
**Archived to**: `openspec/changes/archive/2026-06-30-implementar-modulo-de-cargos-en-el-frontend/`
**Mode**: openspec

## Verdict

**PASS** — el ciclo SDD se cierra en verde. El cambio implementó el primer slice autenticado del módulo `Cargos` en `SGV.Web` (listado activo, detalle readonly y baja lógica confirmada) reutilizando el seam probado de `Unidades Organizativas` y respetando el no-goal de no expandir create, edit, skills, eliminados ni reactivación. Las tres PRs (`#55`, `#57`, `#58`) están mergeadas en `develop` con `31fba2e3` como HEAD y el scope Cargos pasa **27/27 tests**. No se detectaron issues CRITICAL en `verify-report.md`.

## Tarea Complete Gate

- [x] 23/23 tareas en `tasks.md` marcadas completas (1.1–1.9, 2.1–2.9, 3.1–3.5, 4.1–4.5).
- [x] Build: `dotnet build SGV.slnx` → 0 warnings, 0 errors.
- [x] Frontend pipeline: `bun run build` en `src/SGV.Web` → verde en 3.48 s.
- [x] Tests del scope Cargos-web: `CargoWebTests + CargoApiClientTests + CargoWebSeamTests + CargoIndexPageTests + CargoDetailsPageTests` → **27/27 PASS**.
- [x] Token check: 0 coincidencias de `Crear|Editar|Habilidades|Reactivar` en `src/SGV.Web/Pages/Organizacion/Cargos/**`, `cargo-index.js` ni contratos del módulo (`ICargoApiClient.cs`, `CargoApiClient.cs`, `CargoListItemViewModel.cs`).
- [x] Sin issues CRITICAL en `verify-report.md` (verdict `PASS`, review budget 383 líneas dentro del límite de 400).

## Sincronización de delta specs

| Capability | Acción | Detalle |
|------------|--------|---------|
| `cargo-web-listado-detalle-baja` | **CREADA** en canonical | Spec completo nuevo: 4 requirements y 8 escenarios (acceso autenticado, listado activo, detalle readonly con retorno, baja lógica confirmada con feedback de conflicto). No existía canonical previo; la delta del cambio era una spec completa, copiada 1:1 a `openspec/specs/cargo-web-listado-detalle-baja/spec.md` (85 líneas). |
| `sgv-web-shell` | **MODIFICADA** en canonical | El requirement `Minimal technical navigation` se reemplazó por la versión del delta: ahora expone `Home`, `Unidades Organizativas` **y `Cargos`** como módulos funcionales de negocio habilitados, reemplaza el escenario "primer módulo funcional" por "módulos funcionales habilitados", y conserva intacto el escenario "Otros módulos siguen fuera de alcance". Los otros 5 requirements (`Functional base shell`, `Demo content removal`, `Neutral branding and Inspinia visual system`, `No authentication dependency`, `Frontend validation expectations`) se preservan sin cambios. La nota `(Previously: …)` queda actualizada para reflejar la evolución del requirement. |

### Diff stats (delta → canonical)

| Archivo | Líneas antes | Líneas después | Δ |
|---------|--------------|----------------|---|
| `openspec/specs/cargo-web-listado-detalle-baja/spec.md` | inexistente | 85 | +85 |
| `openspec/specs/sgv-web-shell/spec.md` | 117 | 117 | 0 (replace in-place dentro del requirement `Minimal technical navigation`) |

## Contenido del archivo

| Artefacto | Estado |
|-----------|--------|
| `proposal.md` | ✅ Preservado |
| `exploration.md` | ✅ Preservado |
| `design.md` | ✅ Preservado |
| `specs/cargo-web-listado-detalle-baja/spec.md` | ✅ Preservado (delta sincronizado a canonical) |
| `specs/sgv-web-shell/spec.md` | ✅ Preservado (delta sincronizado a canonical) |
| `tasks.md` | ✅ Preservado (23/23 tareas completas) |
| `apply-progress.md` | ✅ Preservado (incluye evidencia RED→GREEN→REFACTOR de Phase 1–4 y nota de cierre) |
| `verify-report.md` | ✅ Preservado |
| `archive-report.md` | ✅ Este documento |

## Source of truth actualizado

Los siguientes specs canónicos reflejan el comportamiento implementado:

- `openspec/specs/cargo-web-listado-detalle-baja/spec.md` — **nueva spec** del módulo web de cargos con 4 requirements y 8 escenarios.
- `openspec/specs/sgv-web-shell/spec.md` — requirement `Minimal technical navigation` actualizado para incluir `Cargos` como segundo módulo funcional.

## Reconciliación de checkboxes

No se requirió reconciliación mecánica: `sdd-apply` dejó las 22 tareas originales (1.1–1.9, 2.1–2.9, 3.1–3.5, 4.1–4.4) marcadas como `[x]` correctamente. La tarea 4.5 (`Archivo SDD`) se agregó en este phase como cierre del ciclo y ya está marcada `[x]` también.

## Notas de archivo

- **Merge no destructivo**: ninguna capability canónica fue removida; el delta de `sgv-web-shell` solo modificó el texto y los escenarios del requirement `Minimal technical navigation`. Los otros 5 requirements (`Functional base shell`, `Demo content removal`, `Neutral branding and Inspinia visual system`, `No authentication dependency`, `Frontend validation expectations`) se preservan byte-a-byte.
- **Spec completa** para `cargo-web-listado-detalle-baja`: como es una capability nueva, no hubo merge: el delta es la spec completa y se copió directamente al tree canónico, sin tocar otras specs.
- **Convención de archivo respetada**: el cambio se movió a `openspec/changes/archive/2026-06-30-implementar-modulo-de-cargos-en-el-frontend/` siguiendo el patrón `YYYY-MM-DD-<slug>` observable en `archive-report.md` previos (ej.: `2026-06-26-implementa-el-modulo-de-unidadesorganizativas-en-el-frontend/`, `2026-06-29-reactivar-y-filtrar-unidades-organizativas-eliminadas/`).
- **Auditoría preservada**: el change folder movido conserva íntegros `proposal.md`, `exploration.md`, `design.md`, los dos `specs/**`, `tasks.md`, `apply-progress.md`, `verify-report.md` y este `archive-report.md`. Ningún artefacto fue modificado ni eliminado durante el archivo.

## SDD Cycle Complete

El cambio fue completamente planificado (`proposal` + `exploration`), especificado (`specs/cargo-web-listado-detalle-baja` + delta sobre `sgv-web-shell`), diseñado (`design`), desglosado en tareas (`tasks`), implementado en 3 PRs mergeados (`apply`), verificado con TDD estricto + `bun run build` + suite sin filtro (`verify`) y archivado (`archive`). El módulo `Cargos` queda disponible en el shell web como segundo módulo funcional de negocio autenticado, sin promises de paridad con `Unidades Organizativas`. Listo para el próximo cambio.
