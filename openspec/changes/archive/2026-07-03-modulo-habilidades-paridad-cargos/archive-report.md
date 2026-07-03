# Archive Report: modulo-habilidades-paridad-cargos

## 1. Resumen ejecutivo

El change `modulo-habilidades-paridad-cargos` completó su ciclo SDD y quedó archivado después de sincronizar sus cinco delta specs hacia `openspec/specs/`.
La verificación final quedó en **PASS**, con `25/25` tasks marcadas, sin issues CRITICAL abiertas y sin necesidad de reconciliación mecánica en archive.
El merge de specs fue no destructivo: se reemplazaron únicamente los requisitos marcados como `MODIFIED`, se agregaron los `ADDED` y se crearon dos main specs nuevas para las capabilities web de Habilidades.

## 2. Specs sincronizados

| Dominio | Delta source | Main spec | Acción | Detalle |
|---|---|---|---|---|
| `habilidad-management` | `openspec/changes/archive/2026-07-03-modulo-habilidades-paridad-cargos/specs/habilidad-management/spec.md` | `openspec/specs/habilidad-management/spec.md` | Updated | Reemplazado `Requirement: Consultar Habilidades`; agregados `Publicar catálogo HTTP de niveles de habilidad` y `Autorización de endpoints de habilidades`. |
| `habilidad-web-listado-detalle-baja` | `openspec/changes/archive/2026-07-03-modulo-habilidades-paridad-cargos/specs/habilidad-web-listado-detalle-baja/spec.md` | `openspec/specs/habilidad-web-listado-detalle-baja/spec.md` | Created | Nuevo main spec copiado como source of truth inicial de la capability web. |
| `habilidad-web-crear-editar` | `openspec/changes/archive/2026-07-03-modulo-habilidades-paridad-cargos/specs/habilidad-web-crear-editar/spec.md` | `openspec/specs/habilidad-web-crear-editar/spec.md` | Created | Nuevo main spec copiado como source of truth inicial de la capability web. |
| `sgv-web-shell` | `openspec/changes/archive/2026-07-03-modulo-habilidades-paridad-cargos/specs/sgv-web-shell/spec.md` | `openspec/specs/sgv-web-shell/spec.md` | Updated | Reemplazado `Requirement: Minimal technical navigation` para incluir `Habilidades`, submenu `Listado`/`Nueva` e icono `ti ti-star`. |
| `sgv-readonly-api` | `openspec/changes/archive/2026-07-03-modulo-habilidades-paridad-cargos/specs/sgv-readonly-api/spec.md` | `openspec/specs/sgv-readonly-api/spec.md` | Updated | Reemplazado `Requirement: Public API Discoverability` para documentar `/api/v1/skills/consulta` y `/api/v1/niveles-habilidad`. |

## 3. Verificación del archive

- [x] Main specs actualizados correctamente.
- [x] Carpeta del change movida a `openspec/changes/archive/2026-07-03-modulo-habilidades-paridad-cargos/`.
- [x] Archive contiene `proposal.md`, `specs/`, `design.md`, `tasks.md`, `apply-progress.md` y `verify-report.md`.
- [x] `tasks.md` archivado no tiene tasks sin marcar (`grep -c "\- \[ \]"` = `0`).
- [x] `openspec/changes/` activo ya no contiene `modulo-habilidades-paridad-cargos`.

## 4. Source of truth actualizado

Los siguientes paths quedan como fuente vigente del comportamiento archivado:

- `openspec/specs/habilidad-management/spec.md`
- `openspec/specs/habilidad-web-listado-detalle-baja/spec.md`
- `openspec/specs/habilidad-web-crear-editar/spec.md`
- `openspec/specs/sgv-web-shell/spec.md`
- `openspec/specs/sgv-readonly-api/spec.md`

## 5. Notas de merge y reconciliación

- **Merge destructivo**: No.
- **Reconciliación mecánica de tasks**: No.
- **Regla `rules.archive` aplicada**: sí; se verificó que el merge no fuera destructivo antes de sincronizar los deltas.
- **Observación**: los main specs nuevos de `habilidad-web-listado-detalle-baja` y `habilidad-web-crear-editar` se crearon desde el delta completo porque no existía un spec principal previo en el repo.

## 6. Artefactos archivados y referencias

- `openspec/changes/archive/2026-07-03-modulo-habilidades-paridad-cargos/proposal.md`
- `openspec/changes/archive/2026-07-03-modulo-habilidades-paridad-cargos/design.md`
- `openspec/changes/archive/2026-07-03-modulo-habilidades-paridad-cargos/tasks.md`
- `openspec/changes/archive/2026-07-03-modulo-habilidades-paridad-cargos/apply-progress.md`
- `openspec/changes/archive/2026-07-03-modulo-habilidades-paridad-cargos/verify-report.md`
- `openspec/changes/archive/2026-07-03-modulo-habilidades-paridad-cargos/specs/habilidad-management/spec.md`
- `openspec/changes/archive/2026-07-03-modulo-habilidades-paridad-cargos/specs/habilidad-web-listado-detalle-baja/spec.md`
- `openspec/changes/archive/2026-07-03-modulo-habilidades-paridad-cargos/specs/habilidad-web-crear-editar/spec.md`
- `openspec/changes/archive/2026-07-03-modulo-habilidades-paridad-cargos/specs/sgv-web-shell/spec.md`
- `openspec/changes/archive/2026-07-03-modulo-habilidades-paridad-cargos/specs/sgv-readonly-api/spec.md`

## 7. Cierre

El change queda archivado con trazabilidad completa en OpenSpec. El próximo estado recomendado para este flujo es **archive-complete**; cualquier trabajo posterior debe abrirse como un change nuevo.
