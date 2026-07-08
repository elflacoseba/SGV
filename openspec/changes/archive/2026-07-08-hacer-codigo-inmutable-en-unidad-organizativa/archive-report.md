# Archive Report — `hacer-codigo-inmutable-en-unidad-organizativa`

**Change**: `hacer-codigo-inmutable-en-unidad-organizativa`
**Archive date**: 2026-07-08
**Mode**: hybrid (openspec + Engram)
**Source of truth**: openspec filesystem + Engram memory store

## Resultado del SDD cycle

El change `hacer-codigo-inmutable-en-unidad-organizativa` completó el ciclo completo de SDD: planificación, implementación por chained PRs (stacked-to-main), verificación strict-TDD y archivado.

- **Verdict de verify**: PASS — sin issues CRITICAL.
- **Tasks**: 20/20 marcadas `[x]` en `tasks.md`.
- **Build / Tests / bun build**: verdes (1529/1541 tests OK; 12 fallos pre-existentes del issue #59, fuera del scope y documentados).

## Specs sincronizadas (delta → main)

| Dominio main spec | Acción | Detalle |
|-------------------|--------|---------|
| `openspec/specs/unidad-organizativa-crud/spec.md` | Updated | 3 requirements MODIFIED: `Manage Organizational Units`, `Validate Organizational Unit Writes`, `Exponer errores de validación por campo`. Los demás requirements del main spec se preservaron sin cambios (`Mantener frontera de validación`, `Resumen legible de unidad padre en lecturas`, `Reactivación de unidades organizativas`, `Consulta segmentada de unidades organizativas por estado`). |
| `openspec/specs/unidad-organizativa-web-detalle-edicion/spec.md` | Updated | 1 requirement MODIFIED: `Datos visibles y editables de la unidad organizativa`. Los demás requirements del main spec se preservaron sin cambios (`Acceso autenticado a create, detail y edit`, `Guardado con feedback accionable`, `Estado recuperable para unidades eliminadas`). |

Delta specs aplicados (todos los cambios fueron MODIFIED, no ADDED/REMOVED/RENAMED):

### `unidad-organizativa-crud/spec.md`

- `Manage Organizational Units` reemplazado por la versión que enuncia `POST MUST aceptar codigo` y `PUT MUST NOT exponer codigo`; update preserva el código persistido aunque el cliente envíe un `codigo` extra fuera de contrato.
- `Validate Organizational Unit Writes` reemplazado por la versión que limita la validación de `codigo` (requerido/máx. 50) únicamente al create; update valida solo campos editables y la reactivación conserva el chequeo de conflicto por código activo.
- `Exponer errores de validación por campo` reemplazado por la versión que garantiza que `codigo` no aparece como campo validable en update porque no pertenece al request.

### `unidad-organizativa-web-detalle-edicion/spec.md`

- `Datos visibles y editables de la unidad organizativa` reemplazado por la versión que obliga a `codigo` solo lectura (o equivalente no editable) en edit; el submit envía solo campos editables y preserva el código original; create mantiene `codigo` editable.

## Archive folder

```
openspec/changes/archive/2026-07-08-hacer-codigo-inmutable-en-unidad-organizativa/
├── proposal.md
├── design.md
├── exploration.md
├── tasks.md                    (20/20 tareas [x], 0 sin marcar)
├── verify-report.md
└── specs/
    ├── unidad-organizativa-crud/spec.md
    └── unidad-organizativa-web-detalle-edicion/spec.md
```

La carpeta activa `openspec/changes/hacer-codigo-inmutable-en-unidad-organizativa/` ya no existe; el único contenido de `openspec/changes/` es el subdirectorio `archive/`.

## Artifacts trazables (Engram)

IDs de observación y sync_id para trazabilidad del pipeline SDD en Engram:

| Artifact | Observation ID | Sync ID |
|----------|----------------|---------|
| `sdd/hacer codigo inmutable en Unidad Organizativa/explore` | 740 | `obs-dc40828d5b44db27` |
| `sdd/hacer codigo inmutable en Unidad Organizativa/proposal` | 741 | `obs-cc85f184b3928910` |
| `sdd/hacer codigo inmutable en Unidad Organizativa/spec` | 742 | `obs-a05c0436d2413666` |
| `sdd/hacer codigo inmutable en Unidad Organizativa/design` | 743 | `obs-efc55107708fa357` |
| `sdd/hacer codigo inmutable en Unidad Organizativa/apply-progress` (PR3) | 746 | `obs-78e37cc41df72678` |
| `sdd/hacer codigo inmutable en Unidad Organizativa/verify-report` | 747 | `obs-9558f424c5c8446d` |

Observaciones auxiliares (no son artifacts SDD directos pero dan contexto de la sesión):

- #744 (`obs-df246f271a4bfa0a`) — decisión de chain strategy `stacked-to-main`.
- #745 (`obs-706eb260820652c3`) — convención de branching del repo (develop como default, main reservado para release).

Nota: la observación `sdd/hacer codigo inmutable en Unidad Organizativa/tasks` no se persistió a Engram para este change; el artifact `tasks.md` vive únicamente en filesystem y fue inspeccionado directamente desde `openspec/changes/archive/2026-07-08-hacer-codigo-inmutable-en-unidad-organizativa/tasks.md`.

## Validaciones ejecutadas por `sdd-archive`

- [x] Task Completion Gate: las 20 tareas de `tasks.md` están `[x]`, 0 sin marcar.
- [x] No CRITICAL issues en `verify-report.md` (PASS).
- [x] Main specs actualizados con los MODIFIED requirements correctos.
- [x] Requirements no presentes en el delta quedaron intactos en cada main spec.
- [x] Cambio movido a `openspec/changes/archive/2026-07-08-hacer-codigo-inmutable-en-unidad-organizativa/`.
- [x] Directorio activo `openspec/changes/` ya no contiene el change.
- [x] Archive contiene todos los artifacts: `proposal.md`, `design.md`, `exploration.md`, `tasks.md`, `verify-report.md`, `specs/{domain}/spec.md`.

## Source of truth actualizado

Los siguientes specs reflejan el nuevo comportamiento:

- `openspec/specs/unidad-organizativa-crud/spec.md`
- `openspec/specs/unidad-organizativa-web-detalle-edicion/spec.md`

## SDD cycle completo

El change fue planificado, implementado (3 PRs stacked-to-main), verificado y archivado. Está listo para el siguiente change.