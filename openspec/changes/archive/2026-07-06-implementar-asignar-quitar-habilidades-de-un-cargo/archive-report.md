# Archive Report — Implementar asignar/quitar Habilidades de un Cargo

**Fecha**: 2026-07-04

## Resumen

Se sincronizaron las delta specs del change hacia `openspec/specs/`, dejando a OpenSpec con el comportamiento final entregado en `develop` tras los merges de PR #82, #83, #84 y #85.

El cierre consolida cuatro capacidades: asignar/editar/quitar habilidades por cargo, defaults y validaciones de `Ponderacion`/`EsObligatoria`, la página Razor editable `Habilidades.cshtml` y el contrato enriquecido del subrecurso `GET /api/v1/cargos/{cargoId}/skills`.

Por instrucción del orquestador, este cierre preserva `proposal.md`, `design.md`, `tasks.md`, `apply-progress.md` y `verify-report.md` como evidencia histórica sin reescritura, y deja los artefactos listos en el working tree para que el orquestador haga el commit final.

## Specs sincronizadas

| Spec | Estado |
|---|---|
| `cargo-skill-asignar-editar` | added y aplicada a `openspec/specs/` |
| `cargo-skill-ponderacion-obligatoria` | added y aplicada |
| `cargo-skill-ui-tabla-editable` | added y aplicada |
| `cargo-skill-query-contract` | modified y aplicada |

## PRs mergeados

| PR # | Título | Sha merge |
|---|---|---|
| #82 | PR1 — Aplicación | `f252d3e1` |
| #83 | PR2 — Infraestructura + API | `7d511d55` |
| #84 | PR3a — Cliente web tipado | `914a93d3` |
| #85 | PR3b — Razor Page + suite web | `e2024212` |

## Verificaciones ejecutadas

- [x] Verify interim PR3a: 0 CRITICAL, 0 WARNING pendiente, 0 SUGGESTION.
- [x] Verify interim PR3b (pre-cierre): 1 CRITICAL + 2 WARNING.
- [x] Verify interim PR3b post-cierre: 0 CRITICAL, 1 WARNING fuera de scope (errores de `Actualizar` no anclados a la fila editada; decisión de UX para slice aparte).
- [x] Suite completa al cierre: 1364/1376 PASS (12 `OcupacionRepositoryTests` pre-existentes, issue #59, fuera de scope).
- [x] `bun run build` verde al cierre del slice web.

## Riesgos abiertos transferidos

- [ ] W1 PR3b — errores de validación de `Actualizar` no quedan anclados a la fila editada. Decisión de UX pendiente.
- [ ] Página `Habilidades.cshtml` no enlazada desde `Index`/`Edit`. Decisión de UX pendiente.
- [ ] Issue #59 — 12 fallos pre-existentes de `OcupacionRepositoryTests` por bug en migración inicial (`ActivePuestoIdUnique INT` vs `PuestoId CHAR(36)`).

## Memoria

- #569 — anti-drift cross-module blindado con `HabilidadesPage_NoContaminaHabilidadCatalogoConNivelRequerido` ✅.
- `Habilidad` entidad catálogo NO tiene `NivelId` propio. Toda asociación de nivel es vía `CargoHabilidad.NivelRequeridoId`.

## Notas de cierre

- `tasks.md` quedó verificado sin tareas de implementación pendientes en el artefacto persistido.
- `verify-report.md` se conserva como evidencia histórica del verify interim; el cierre post-CRITICAL queda trazado en `apply-progress.md` y en el estado mergeado de `develop`, sin reescribir el reporte histórico.
- No se movió la carpeta activa del change en esta ejecución porque el orquestador pidió preservar la ruta `openspec/changes/implementar-asignar-quitar-habilidades-de-un-cargo/` para el commit final de cierre.

## Cierre físico del archive (2026-07-06)

En esta sesión, bajo override explícito del maintainer para destrabar el ciclo del change, se completa el cierre físico moviendo la carpeta activa al archive:

- Ruta origen: `openspec/changes/implementar-asignar-quitar-habilidades-de-un-cargo/`
- Ruta destino: `openspec/changes/archive/2026-07-06-implementar-asignar-quitar-habilidades-de-un-cargo/`
- Movimiento ejecutado con `git mv` para preservar historial de renombre.
- Veredicto del verify post-cierre: 0 CRITICAL, 1 WARNING fuera de scope (`Actualizar` no anclado a la fila editada — decisión de UX para slice aparte, registrada en `apply-progress.md`).
- Type de cierre: `intentional-with-warnings` por la nota WARNING de UX fuera de scope. No se solicitó re-archivado del `verify-report.md` con el veredicto post-cierre porque la evidencia ya quedó consolidada en `apply-progress.md` y en este reporte.
