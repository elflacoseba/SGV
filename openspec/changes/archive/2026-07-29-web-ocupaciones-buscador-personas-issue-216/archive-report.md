# Archive Report: Buscador de Personas en Ocupación (#216)

> Change: `2026-07-29-web-ocupaciones-buscador-personas-issue-216`
> Issue: [#216](https://github.com/elflacoseba/SGV/issues/216)
> Modo artefactos: **both** (OpenSpec + Engram)
> Verificación: APPROVE WITH COMMENTS
> Archivada: 2026-07-29

## Resumen

El change #216 reemplazó el `<select>` plano de `PersonaId` en los formularios Crear/Editar Ocupación por la card enriquecida + modal reutilizable ya vigente en Usuarios, sin aplicar el filtro `soloSinUsuario=true` (una persona puede tener múltiples ocupaciones). Se introdujo el atributo `data-solo-sin-usuario` en el modal raíz para conditionalizar el flag en el JS compartido, con default `true` que preserva backwards-compat estricta con Usuarios. `IOcupacionForm` expone `PersonaDisplay` y `PersonaVinculada`; `PersonaOptions` fue eliminado de la interfaz y el PageModel. El CRITICAL-01 detectado en la primera verificación (duplicación del `<select>` de `PuestoId`) fue corregido en el commit `376786d`. Suite completa verde (1282/1282 Web, 3160/3168 total; 1 fail preexistente no relacionado).

## Specs sincronizadas

| Spec | Tipo | Source (delta) | Target (canónica) | Acción |
|------|------|----------------|-------------------|--------|
| `ocupacion-web-selector-persona-buscador` | **NEW** | `openspec/changes/.../specs/ocupacion-web-selector-persona-buscador/spec.md` | `openspec/specs/ocupacion-web-selector-persona-buscador/spec.md` | Creada canónica — 7 requirements, 15 scenarios |
| `usuario-web-selector-persona-buscador` | **MODIFIED** | `openspec/changes/.../specs/usuario-web-selector-persona-buscador/spec.md` | `openspec/specs/usuario-web-selector-persona-buscador/spec.md` | Merged — 1 ADDED REQ-USB-12 + 2 MODIFIED REQ-USB-03/10 |

## Cambios mergeados a canónicas

### `ocupacion-web-selector-persona-buscador` (NEW — creada)

- **OCC-PER-BUSC-01** (7 scenarios): Reemplazo del `<select>` por card + modal en Create/Edit de Ocupaciones.
- **OCC-PER-BUSC-02** (3 scenarios): `IOcupacionForm` expone `PersonaDisplay` y `PersonaVinculada`; `GetByIdAsync` en Edit con caída suave.
- **OCC-PER-BUSC-03** (2 scenarios): Búsqueda sin `soloSinUsuario`; modal declara `data-solo-sin-usuario="false"`.
- **OCC-PER-BUSC-04** (3 scenarios): Preselección en Edit, `Cambiar` excluye persona actual, `Quitar` limpia sin API.
- **OCC-PER-BUSC-05** (2 scenarios): Pre-carga via `?personaId` en Create; id inexistente cae a estado vacío.
- **OCC-PER-BUSC-06** (2 scenarios): Estados del modal reutilizados (Inicial/Empty/Loading/Error).
- **OCC-PER-BUSC-07** (1 scenario): Tests xUnit actualizados para cubrir modal en lugar de `<select>`.

### `usuario-web-selector-persona-buscador` (MODIFIED — mergeado)

- **REQ-USB-03** (MODIFIED — 3 scenarios): Búsqueda lazy ahora conditionaliza `soloSinUsuario` via `data-solo-sin-usuario`; default `true` preserva Usuarios. Se agregó scenario `Búsqueda desde modal con data-solo-sin-usuario="false"`.
- **REQ-USB-10** (MODIFIED — 2 scenarios): Listado exclusivo ahora documenta que `data-solo-sin-usuario="false"` queda fuera del scope de este requisito (delegado a `ocupacion-web-selector-persona-buscador`). Se agregó scenario `Modal reutilizado con soloSinUsuario=false`.
- **REQ-USB-12** (ADDED — 4 scenarios): Configuración del modal via `data-solo-sin-usuario`; parseo case-insensitive; default `true`; backwards-compat con Usuarios.

## Veredicto del verify

**APPROVE WITH COMMENTS** — verificado post-fix de CRITICAL-01 (commit `376786d`).

- Suite Web completa: **1282/1282 PASS**
- Suite .NET completa: **3160/3168 PASS** (1 fail preexistente `CargoRepositoryTests.ListAllAsync_RetornaCargosOrdenadosPorCodigo`, no relacionado con el change)
- Build: 0 errores, 0 warnings nuevos
- `bun run build`: OK

Warnings residuales (no blockers):
- WARNING-01: Tests RED→GREEN no spliteados en commits separados (strict TDD).
- WARNING-02: 1 `[MySqlFact]` falla por MySQL no disponible (no relacionado).
- WARNING-03: Size exception (682 netas vs budget 400; aprobado por maintainer).
- WARNING-04: Validación runtime del query param con `soloSinUsuario=false` no automatizada (mismo enfoque que precedent 2026-07-17).

Suggestions residuales (no blockers):
- SUGGESTION-01: Tests parametrizados para OCC-PER-BUSC-04.
- SUGGESTION-02: Test de regresión específico para `<select>` único de PuestoId.
- SUGGESTION-03: Snapshot test del HTML renderizado.

## Pendientes

- Smoke manual del flujo Create/Edit Ocupaciones (documentado en apply-progress §Pendientes).
- Confirmar `size:exception` antes de PR.
- Validación runtime del query param `soloSinUsuario=false` con DevTools.

## PR / Issue

- **PR**: No creado aún (archive precedes push/PR).
- **Issue**: [#216](https://github.com/elflacoseba/SGV/issues/216) — abierta, pendiente de cierre post-PR.

## Artefactos del change (audit trail)

- `openspec/changes/archive/2026-07-29-web-ocupaciones-buscador-personas-issue-216/proposal.md`
- `openspec/changes/archive/2026-07-29-web-ocupaciones-buscador-personas-issue-216/design.md`
- `openspec/changes/archive/2026-07-29-web-ocupaciones-buscador-personas-issue-216/tasks.md`
- `openspec/changes/archive/2026-07-29-web-ocupaciones-buscador-personas-issue-216/apply-progress.md`
- `openspec/changes/archive/2026-07-29-web-ocupaciones-buscador-personas-issue-216/verify-report.md`
- `openspec/changes/archive/2026-07-29-web-ocupaciones-buscador-personas-issue-216/archive-report.md` ← este archivo
- `openspec/changes/archive/2026-07-29-web-ocupaciones-buscador-personas-issue-216/specs/ocupacion-web-selector-persona-buscador/spec.md`
- `openspec/changes/archive/2026-07-29-web-ocupaciones-buscador-personas-issue-216/specs/usuario-web-selector-persona-buscador/spec.md`

## Commits del change en `develop`

```
9c91747 chore(sdd): record post-verify correction for #216
376786d fix(web): remove duplicated puesto select after #216 refactor
a699a288 feat(web): wire persona finder modal into ocupacion forms
4c39f658 feat(web): enrich ocupacion form with linked persona card
89715653 feat(web): make soloSinUsuario filter configurable via data attribute
```

5 commits, conventional commits, sin `Co-Authored-By`.
