# Archive Report — cargos-navegacion-habilidades

**Fecha**: 2026-07-06
**Tipo de cierre**: `intentional-with-warnings` (override explícito del maintainer)

## Resumen

Se cierra el change SDD `cargos-navegacion-habilidades` reubicando la carpeta activa a `openspec/changes/archive/2026-07-06-cargos-navegacion-habilidades/` y sincronizando la delta spec `cargo-skill-ui-tabla-editable` con el catálogo principal. El código correspondiente al change está mergeado en `develop` vía PR #87 (`e86779db Merge pull request #87 from elflacoseba/feat/cargos-navegacion-habilidades`).

## Overrides aplicados bajo autorización del maintainer

Este archive se ejecuta con dos excepciones explícitas al protocolo estándar `sdd-archive`, ambas autorizadas por el maintainer en sesión:

1. **Ausencia de `verify-report.md`**: la fase `sdd-verify` no dejó un `verify-report.md` persistido en el change folder. El maintainer aceptó el gap explícitamente para destrabar el ciclo del change. La evidencia de verificación del slice queda reconstituida desde `apply-progress.md` y `git log` (ver sección "Verificaciones ejecutadas").
2. **Stale-checkbox reconciliation en `tasks.md`**: el `tasks.md` persistido quedó con checkboxes `- [ ]` para T1.x, T2.x, T3.x a pesar de que `apply-progress.md` prueba el cierre completo (incluyendo remediación post-verify R1-R7) con TDD evidence, SHAs de commit y verificaciones verdes. Bajo override del maintainer, todos los checkboxes se flipean a `[x]` y se añade una nota de reconciliación al inicio del archivo citando `apply-progress.md` como fuente de verdad.

## Specs sincronizadas

| Spec | Acción | Detalle |
|---|---|---|
| `cargo-skill-ui-tabla-editable` | MODIFICADA (Req 3) + NUEVAS (Req 6, Req 7) | Aplicada a `openspec/specs/cargo-skill-ui-tabla-editable/spec.md`. Req 3 explicita ahora feedback de validación por fila para `Actualizar` con la convención `Actualizar[{skillId}].Campo`; Req 6 agrega descubribilidad desde `Index.cshtml`; Req 7 agrega descubribilidad desde `Details.cshtml`. Req 1, 2, 4 y 5 sin cambios respecto a la versión canónica. |

## PR mergeado

| PR # | Título | Sha merge | Notas |
|---|---|---|---|
| #87 | `feat/cargos-navegacion-habilidades` | `e86779db` | Merge a `develop`. Incluye los 6 commits de trabajo listados en `apply-progress.md`. |

## Verificaciones ejecutadas (reconstituidas desde `apply-progress.md`)

- [x] `dotnet build SGV.slnx`: PASS (0 warnings, 0 errors) — `2026-07-04 22:25` (aplicación inicial) y `2026-07-04 23:08` (post-remediación).
- [x] `dotnet test SGV.slnx`: **1381/1393 PASS** al cierre — `2026-07-04 23:09`. Los 12 fallos pre-existentes corresponden a `SGV.Tests.Persistencia.OcupacionRepositoryTests` por el bug conocido de migración issue #59 (`ActivePuestoIdUnique INT` vs `PuestoId CHAR(36)`), fuera del alcance de este change.
- [x] `dotnet test --filter "FullyQualifiedName~CargoHabilidades"`: 24/24 PASS.
- [x] `bun run build`: PASS (2.94 s) — `2026-07-04 23:10`.

## Riesgos abiertos transferidos

- [ ] 12 fallos pre-existentes en `OcupacionRepositoryTests` por issue #59, fuera del scope de este change. Pendiente de un SDD change independiente.
- [ ] `Habilidades.cshtml` no enlaza la entrada desde `Index`/`Edit` (decisión de UX para slice aparte, registrada en memoria #569 del change `implementar-asignar-quitar-habilidades-de-un-cargo`).
- [ ] `apply-progress.md` reporta 224/224 PASS para un subset histórico, pero el conteo real hoy es 225/225. Diferencia menor, no bloqueante del cierre.

## TDD evidence (resumen)

Disciplina `strict_tdd: true` respetada en los 6 commits productivos del change:

| Sha | Mensaje | Rol TDD |
|---|---|---|
| `4ca00d27` | `test(web): cargo index exposes Habilidades CTA on active rows` | RED |
| `1deb4398` | `feat(web): cargo index CTA Habilidades in active Acciones column` | GREEN |
| `40e7de01` | `test(web): cargo details exposes Habilidades button on footer` | RED |
| `93114206` | `feat(web): cargo details Habilidades button on footer` | GREEN |
| `41adc2f2` | `test(web): Habilidades ApplyActualizar maps FieldErrors per row` | RED |
| `c8668b42` | `feat(web): split ApplySkillFailureToModelState per handler in Habilidades page model` | GREEN |
| `c2fb846d` | `test(web): Habilidades Actualizar tests use Actualizar[xxx].Campo form keys` (remediación) | RED |
| `1d64e805` | `feat(web): Habilidades Actualizar reads values from Actualizar[xxx] form prefix` (remediación) | GREEN |

## Notas de cierre

- `proposal.md`, `design.md`, `tasks.md`, `apply-progress.md` y `specs/cargo-skill-ui-tabla-editable/spec.md` se preservan como evidencia histórica sin reescritura adicional (salvo la reconciliación de checkboxes en `tasks.md` documentada arriba).
- No se generó `verify-report.md`: el gap fue aceptado por el maintainer y queda trazado en este reporte.
- La carpeta activa `openspec/changes/cargos-navegacion-habilidades/` se mueve a `openspec/changes/archive/2026-07-06-cargos-navegacion-habilidades/`.

## Result Contract

- **status**: success
- **executive_summary**: Change `cargos-navegacion-habilidades` archivado como `intentional-with-warnings` bajo override explícito del maintainer. Spec `cargo-skill-ui-tabla-editable` sincronizada al catálogo principal con Req 3 modificada y Req 6 y 7 nuevas. PR #87 ya mergeado a `develop`. Carpeta activa movida al archive.
- **artifacts**:
  - `openspec/specs/cargo-skill-ui-tabla-editable/spec.md` (modificada: Req 3, Req 6, Req 7)
  - `openspec/changes/archive/2026-07-06-cargos-navegacion-habilidades/proposal.md`
  - `openspec/changes/archive/2026-07-06-cargos-navegacion-habilidades/design.md`
  - `openspec/changes/archive/2026-07-06-cargos-navegacion-habilidades/tasks.md` (reconciliado)
  - `openspec/changes/archive/2026-07-06-cargos-navegacion-habilidades/apply-progress.md`
  - `openspec/changes/archive/2026-07-06-cargos-navegacion-habilidades/exploration.md`
  - `openspec/changes/archive/2026-07-06-cargos-navegacion-habilidades/specs/cargo-skill-ui-tabla-editable/spec.md`
  - `openspec/changes/archive/2026-07-06-cargos-navegacion-habilidades/archive-report.md` (este archivo)
- **next_recommended**: cycle-complete
- **risks**:
  - Override del gap de `verify-report.md`: el change no pasó por la fase formal `sdd-verify`, por lo que la aceptabilidad final descansa en `apply-progress.md` + `git log` + override explícito.
  - Stale-checkbox reconciliation: cualquier auditoría futura debe mirar la nota al inicio de `tasks.md` para entender por qué los checkboxes no se flipean in-place durante el apply.
  - 12 fallos pre-existentes `OcupacionRepositoryTests` siguen sin resolver (issue #59).
- **skill_resolution**: paths-injected — `sdd-archive` (skill cargada manualmente, sub-agent no disponible como `subagent_type` válido en esta instalación de OpenCode)