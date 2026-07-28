# Archive Report: Completar módulo de Puestos — endpoint segmentado, paginación server-side y protección de baja por ocupaciones vigentes

> Change: `2026-07-27-completar-puestos-issue-209` · Issue: #209
> Idioma: español · Fecha de archive: 2026-07-27
> Modo: hybrid (Engram + filesystem OpenSpec)

## Resumen

Se archivó el cambio que cierra tres brechas del módulo `Puestos` respecto de `Cargos`: consulta segmentada paginada server-side, endpoint HTTP `GET /api/v1/puestos/consulta` con coexistencia del endpoint legado, y baja lógica protegida contra ocupaciones vigentes con código de error estable `PuestoConOcupacionesActivas` y mapeo 409. La implementación se entregó en 2 PRs stacked-to-main (PR #210 backend, PR #211 web) con 15 tareas, build 0 errores/0 warnings nuevas, suite focal 1710/1710 PASS sobre `develop` — ambas ramas mergeadas. Las 7 decisiones locked (DEC-1..DEC-7) están reflejadas en código con cobertura de test suficiente. No se requirió migración de BD.

## Specs Synced

| Domain | Action | Details |
|--------|--------|---------|
| `puesto-management` | Updated (ADDED) | 3 nuevos requisitos: REQ-PTO-001 (consulta segmentada paginada), REQ-PTO-002 (endpoint HTTP de consulta), REQ-PTO-010 (baja protegida con ocupaciones vigentes) |
| `puesto-web-listado-detalle-baja` | Updated (ADDED) | 1 nuevo requisito: REQ-PTO-020 (listado web paginado, toggle Eliminadas funcional y feedback 409) |

## Delta doble — reconciliación

Este change aplicó dos deltas MODIFIED simultáneas sobre capacidades distintas en la misma iteración de desarrollo:

1. **`puesto-management`** recibe REQ-PTO-001 (contrato de consulta segmentada en `IPuestoRepository`/`IPuestoServicioConsulta`), REQ-PTO-002 (nuevo endpoint HTTP `GET /consulta`) y REQ-PTO-010 (guarda `DesactivarAsync` contra `IOcupacionRepository.ExistsActiveByPuestoAsync`). Entregado en **PR #210** (4 commits backend sobre `develop`).

2. **`puesto-web-listado-detalle-baja`** recibe REQ-PTO-020 (`PuestoIndexModel.LoadAsync` migra a `IPuestosApiClient.QueryAsync`, toggle Eliminadas activo con `<a>`, paginación server-side y feedback 409 vía `TempData["ErrorCode"]`). Entregado en **PR #211** (3 commits web sobre `develop` con PR #210 mergeado).

Ambos PRs fueron verificados individualmente con APPROVE y mergeados a `main`. La reconciliación es coherente con el diseño (`design.md` §Riesgo R3) y el verify global confirma que no hay conflictos entre los requisitos de ambas capacidades: REQ-PTO-010 (backend) y REQ-PTO-020 (web) comparten el mismo código de error `PuestoConOcupacionesActivas` y el mismo mapeo 409 → feedback sin falsear éxito.

## Archive Contents

- `proposal.md` ✅ — Propuesta completa del cambio (110 líneas, 6 decisiones locked).
- `design.md` ✅ — Diseño técnico detallado (97 líneas, 7 decisiones locked, threat matrix).
- `tasks.md` ✅ — 15 tareas implementadas, todas `[x]` completas.
- `specs/` ✅ — 3 specs delta:
  - `specs/puestos-consulta-segmentada/spec.md` — REQ-PTO-001 (3 escenarios) + REQ-PTO-002 (3 escenarios).
  - `specs/puestos-proteccion-baja/spec.md` — REQ-PTO-010 (4 escenarios, MODIFIED).
  - `specs/web-puestos-paginacion/spec.md` — REQ-PTO-020 (5 escenarios, MODIFIED).
- `apply-progress.md` ✅ — Progreso detallado por commit, TDD evidence, decisiones locked aplicadas en ambos PRs.
- `verify-report.md` ✅ — Verificación final **APPROVE** para PR1 backend + PR2 web, sin CRITICAL.

## Source of Truth Updated

Los siguientes specs ahora reflejan el nuevo comportamiento:

- `openspec/specs/puesto-management/spec.md` — extendido con REQ-PTO-001, REQ-PTO-002 y REQ-PTO-010 (Source y Verification por requisito apuntando a archivos de test y delta specs archivados).
- `openspec/specs/puesto-web-listado-detalle-baja/spec.md` — extendido con REQ-PTO-020 (Source y Verification apuntando a tests de PageModel y ApiClient).

## Task Completion Summary

| Tarea | Estado | PR |
|-------|--------|----|
| T-01: `PuestoListQuery` + `PuestoSegmentoListado` en Contracts | ✅ | PR #210 |
| T-02: Type alias `PuestoListQuery` en Web (DEC-1) | ✅ | PR #210 |
| T-03: Guarda `DesactivarAsync` contra ocupaciones (DEC-2, DEC-3) | ✅ | PR #210 |
| T-04: Tests unit de la guarda de baja | ✅ | PR #210 |
| T-05: `IPuestoRepository.QueryAsync` + impl server-side (DEC-4, DEC-5) | ✅ | PR #210 |
| T-06: Tests MySQL de consulta segmentada y paginada | ✅ | PR #210 |
| T-07: `IPuestoServicioConsulta.QueryAsync` | ✅ | PR #210 |
| T-08: Tests unit del servicio de consulta | ✅ | PR #210 |
| T-09: Endpoint HTTP `/consulta` y mapeo 409 (DEC-6) | ✅ | PR #210 |
| T-10: Tests API del endpoint y baja protegida | ✅ | PR #210 |
| T-11: `IPuestosApiClient.QueryAsync` y serialización (DEC-7) | ✅ | PR #211 |
| T-12: Tests del cliente HTTP de puestos | ✅ | PR #211 |
| T-13: Refactor `PuestoIndexModel.LoadAsync` a consulta paginada | ✅ | PR #211 |
| T-14: Tests del PageModel y feedback 409 | ✅ | PR #211 |
| T-15: Toggle Eliminadas y controles de paginación en la vista | ✅ | PR #211 |

## Decisiones locked aplicadas

| # | Decisión | Resumen |
|---|----------|---------|
| DEC-1 | Type alias `PuestoListQuery` en `PuestoListItemViewModel.cs` preserva nombre importado sin romper consumidores legacy | ✅ |
| DEC-2 | Ctor primario 7-parámetros + ctor legacy 4 con `NullOcupacionRepository` preserva fixtures existentes | ✅ |
| DEC-3 | `PuestoError.Categoria = ErrorCategoria.Conflict` explícito (default `Unexpected` → 500) | ✅ |
| DEC-4 | `QueryAsync` propio con `AsNoTracking()` + Includes; no reusa `Query` base que filtra `IsActive` | ✅ |
| DEC-5 | Repo devuelve `(Items, TotalCount)`; servicio construye `PagedResult<PuestoDto>` (paridad Cargos) | ✅ |
| DEC-6 | Controller no normaliza `page<1`/`pageSize<1` (paridad Cargos); PageModel clampea client-side | ✅ |
| DEC-7 | `BuildQueryUri` con `StringBuilder` + `Uri.EscapeDataString` (espejo `CargoApiClient`) | ✅ |

## Pending Items (SUGGESTIONS from verify-report)

- **W1:** El record legacy `PuestoListQuery` (`PuestoListItemViewModel.cs:63-73`) sigue en el namespace `SGV.Web.Integration.Organizacion` por backward-compat con `PuestoWebSeamTests`. Borrarlo en un follow-up de limpieza cuando el test se migre totalmente al record de Contracts.
- **S1:** Documentar en `docs/decisiones-implementacion.md` el patrón "type alias para records migrados a Contracts" cuando se use por primera vez en otro módulo (Habilidades tiene su `HabilidadListQuery` legacy).
- **S2:** Extraer códigos de error de `PuestoServicioComandos` (`PuestoConOcupacionesActivas`, etc.) a una clase estática `PuestoErrorCodes` en un PR de limpieza dedicado.

## Cycle Closure

- PR #210: Backend — mergeado a `develop` (4 commits `8a9e08c0..27fb36b9`).
- PR #211: Web — mergeado a `develop` (3 commits `87f7687..2d8878a`).
- `develop` sincronizado con ambos merges.
- Change archivado en `openspec/changes/archive/2026-07-27-completar-puestos-issue-209/`.
- Specs actualizados en `openspec/specs/puesto-management/spec.md` y `openspec/specs/puesto-web-listado-detalle-baja/spec.md`.

El cambio fue completamente planificado, implementado, verificado y archivado. Todos los artefactos están preservados en el audit trail.
