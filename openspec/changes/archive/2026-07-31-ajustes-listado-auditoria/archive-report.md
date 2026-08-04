# Archive Report — `2026-07-31-ajustes-listado-auditoria` (issue #248)

> Change completo archivado. SDD cycle cerrado.

## Change metadata

| Campo | Valor |
|-------|-------|
| Change | `2026-07-31-ajustes-listado-auditoria` |
| Issue | `elflacoseba/SGV#248` |
| Fecha de archive | `2026-07-31` |
| Rama base | `develop` (mergeado via PR #249 + PR #250) |
| Verdict de verificación | **PASS** |
| Blockers | 0 |
| CRITICAL | 0 |

## Native Review Receipt Gate

Estado: `not_applicable` / `unmanaged`. El change ya estaba mergeado en `develop` antes de la fase de archive. El gate no bloqueó.

## Resumen ejecutivo

El change `2026-07-31-ajustes-listado-auditoria` implementó 4 capabilities nuevas o extendidas sobre el módulo de auditoría: sort server-side por 5 columnas (`auditoria-sort`), detalle con old/new values (`auditoria-detalle`), selector de pageSize 10/20/50/100 (`auditoria-page-size`), y extensión del query con CorrelationId + UserName (`auditoria-query` modificada). El change se entregó en 2 PRs stacked-to-main (Slice A: backend/contracts, Slice B: UI/tests/docs) y pasó verificación completa con 3395/3395 tests. D-2 quedó cerrado por separación física de tipos.

## Artefactos sincronizados en `openspec/specs/`

| Domain | Action | Requirements |
|--------|--------|-------------|
| `auditoria-query` | Updated | 4 requirements modificados (Listado paginado, Filtros combinables, Detalle por identificador, Contrato wire); 3 requirements preservados (Autorización, Protección de datos, Shell web); 1 requirement nuevo (Contrato wire sin EntityId) |
| `auditoria-sort` | Created | 3 requirements (Ordenamiento server-side 5 columnas, Desempate por Id, Reset page 1 en web) |
| `auditoria-detalle` | Created | 4 requirements (AuditoriaDetalleDto, Endpoint API protegido, Página web con render preformateado, Cliente HTTP tipado) |
| `auditoria-page-size` | Created | 3 requirements (Selector 10/20/50/100, Paginación preserva pageSize, Normalización de valores inválidos) |

**Total: 14 requirements, 45 scenarios** — todos cubiertos por tests runtime.

## Artefactos del change (archive)

```
openspec/changes/archive/2026-07-31-ajustes-listado-auditoria/
├── exploration.md       ✅
├── proposal.md          ✅
├── design.md            ✅
├── tasks.md             ✅ (18/18 tareas completadas [x])
├── verify-report.md     ✅ (PASS, 0 CRITICAL)
└── specs/
    ├── auditoria-query/    ✅
    ├── auditoria-sort/    ✅
    ├── auditoria-detalle/  ✅
    └── auditoria-page-size/ ✅
```

**Nota**: `apply-progress.md` no fue persistido al filesystem; su contenido existe como observación Engram (`sdd/2026-07-31-ajustes-listado-auditoria/apply-progress`, #1651).

## Specs actualizadas en `openspec/specs/`

| Spec | Estado post-archive |
|------|---------------------|
| `openspec/specs/auditoria-query/spec.md` | Sincronizada con delta MODIFIED |
| `openspec/specs/auditoria-sort/spec.md` | Creada (copia de delta NEW) |
| `openspec/specs/auditoria-detalle/spec.md` | Creada (copia de delta NEW) |
| `openspec/specs/auditoria-page-size/spec.md` | Creada (copia de delta NEW) |

## Decisiones de архитектура documentadas

- **D-5 bis** (LEFT JOIN `UserName`, fallback `"—"`): `decisiones-implementacion.md` líneas 96-114.
- **D-6** (sort server-side vía `switch(Sort)`, default `fecha_desc`): `decisiones-implementacion.md` líneas 115-152.
- **D-7** (detalle admin con `AuditoriaDetalleDto` old/new + `EntityId`): `decisiones-implementacion.md` líneas 153-195.

## Observaciones Engram vinculadas

| Artefacto | Observation ID |
|-----------|---------------|
| Explore | #1645 |
| Proposal | #1646 |
| Spec compuesta | #1647 |
| Design | #1648 |
| Tasks | #1649 |
| Slices (Slice A) | #1650 |
| Apply Slice B | #1651 |
| Verify report | #1652 |

## Estado final verificado (per verify-report, sin stale claims)

- Build: 0 errors, 4 warnings pre-existentes (NU1510 en SGV.Infraestructura, sin relación).
- Tests: 3395/3395 PASS (full suite), 77/77 Auditoria, 1398/1398 Web.
- D-2 cerrado por separación física de tipos (`AuditoriaDto` sin old/new/EntityId; `AuditoriaDetalleDto` los expone y `Details.cshtml` los renderiza en `<pre>`).
- `main` compilable entre merges de A y B (hotfix compat en Slice A).
- Las 18 tareas (10 Slice A + 8 Slice B) marcadas `[x]` en `tasks.md`.

## SDD Cycle

**Estado: CERRADO**

El change `2026-07-31-ajustes-listado-auditoria` completó todas las fases SDD:
- Proposal ✅
- Spec ✅
- Design ✅
- Tasks (18/18) ✅
- Apply (Slice A + Slice B) ✅
- Verify (PASS) ✅
- Archive ✅

El repo está listo para el próximo change.
