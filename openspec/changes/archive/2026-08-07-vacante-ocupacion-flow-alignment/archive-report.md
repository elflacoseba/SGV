# Archive Report: vacante-ocupacion-flow-alignment

## Resumen ejecutivo

El change `vacante-ocupacion-flow-alignment` se archivó el **2026-08-07** tras completar exitosamente las fases de propuesta, diseño, especificación, implementación, verificación y corrección de 6 critical findings. El veredicto final de `sdd-verify` (post-fix) fue `READY TO MERGE` con `0 critical findings`. Los delta specs fueron sincronizados a `openspec/specs/` y el directorio del change fue movido al archive.

## Estado final verificado

| Artefacto | Status | Evidencia |
|---|---|---|
| `proposal.md` | ✅ | 84 líneas, propuesta completa con AC, risks, non-goals |
| `design.md` | ✅ | 172 líneas, decisiones D-1..D-4, data flow N2, work units |
| `tasks.md` | ✅ | 21 tasks + 7 T-FIX, todos `[x]` marcados; 28/28 tests de VacanteServicioComandos + 37/37 OcupacionServicioComandos + 32 web + 3 persistencia |
| `verify-report.md` | ✅ | 6 critical findings, verdict: FAIL (superseded) |
| `verify-report-2.md` | ✅ | 0 critical findings, verdict: **READY TO MERGE** |
| `specs/vacante-management/spec.md` | ✅ | Delta sincronizado a main spec |
| `specs/web-ocupaciones-navegacion-contextual/spec.md` | ✅ | Delta sincronizado a main spec |
| `specs/web-ocupaciones-crear-editar/spec.md` | ✅ | Delta sincronizado a main spec |

## Specs sincronizadas a main specs

### `openspec/specs/vacante-management/spec.md`

| Requisito | Acción | Detalle |
|---|---|---|
| Crear Vacante | MODIFIED | Agregado check N1 (`PuestoOcupado`) antes de `PuestoConVacanteAbierta`; escenario `Puesto con Ocupacion activa` agregado; escenario `Creación exitosa` actualizado con precondición de ausencia de Ocupacion activa |
| Cambiar estado de Vacante con historial | MODIFIED | Agregada regla N2 (transición a `Cubierta` requiere `PersonaId` y crea `Ocupacion` automáticamente); escenarios `Transición a Cubierta crea Ocupacion`, `Cubrir sin PersonaId es rechazado`, `Atomicidad extendida a Ocupacion` agregados |
| Unicidad de vacante abierta por puesto | MODIFIED | Agregada regla N4 (la posición se libera al finalizar la Ocupacion derivada, no al cubrir la Vacante); escenarios `Cubrir la Vacante no libera la posición`, `Finalizar la Ocupacion derivada libera la posición` agregados |
| Códigos de error de Ocupacion cruzada | ADDED | Nuevo requisito con escenario `Discriminación de códigos 409`; código `PuestoOcupado` en `VacanteErrorCodigo` distinto de `PuestoConVacanteAbierta` (BD) y de `OcupacionErrorCodigo.PuestoOcupado` |

### `openspec/specs/web-ocupaciones-navegacion-contextual/spec.md`

| Requisito | Acción | Detalle |
|---|---|---|
| REQ-OCC-NAV-006 — Alta contextual precargada | MODIFIED | Agregada bifurcación: Puesto con Vacante abierta → `Create` Ocupacion; Puesto sin Vacante → deriva a flujo Vacante; Puesto con Ocupacion activa → deriva a detalle Ocupacion vigente. Escenarios `Alta desde Puesto con Vacante abierta`, `Alta desde Puesto sin Vacante abierta (N3)`, `Alta desde Puesto con Ocupacion activa (N1)` agregados |
| REQ-OCC-NAV-007 — Navegación al flujo de Vacante desde Puesto | ADDED | Nuevo requisito con escenarios `Abrir Vacante desde Puesto sin vacante`, `Abrir Vacante oculto si ya existe`, `"Abrir Vacante" no-admin` |

### `openspec/specs/web-ocupaciones-crear-editar/spec.md`

| Requisito | Acción | Detalle |
|---|---|---|
| REQ-OCC-FORM-001 — Crear Ocupación | MODIFIED | Agregada regla N3: `PuestoId` debe tener Vacante abierta; escenario `Puesto sin Vacante abierta (N3)` agregado |
| REQ-OCC-FORM-005 — Conflictos de unicidad visibles | MODIFIED | Agregado tercer código `PuestoSinVacanteAbierta`; escenario `Puesto sin vacante abierta (N3)` y actualización de `Sin falso éxito` para cubrir 3 códigos |
| REQ-OCC-FORM-008 — Reactivación con colisión explícita | MODIFIED | Agregada regla Q2 (Vacante Cancelada bloquea reactivación); escenario `Vacante Cancelada bloquea reactivación (Q2)` agregado |
| REQ-OCC-FORM-009 — Flujo normal documentado | ADDED | Nuevo requisito con escenarios `Hints de flujo en Create`, `Create no sustituye al flujo automatizado` |

## Archivos movidos

```
openspec/changes/vacante-ocupacion-flow-alignment/
  → openspec/changes/archive/2026-08-07-vacante-ocupacion-flow-alignment/
```

Contenido archivado:
- `proposal.md` ✅
- `design.md` ✅
- `tasks.md` ✅ (783 líneas, 21 tasks + 7 T-FIX todos completados)
- `specs/vacante-management/spec.md` ✅
- `specs/web-ocupaciones-navegacion-contextual/spec.md` ✅
- `specs/web-ocupaciones-crear-editar/spec.md` ✅
- `verify-report.md` ✅ (superseded)
- `verify-report-2.md` ✅ (final, verdict: READY TO MERGE)

## Métricas finales de implementación

| Métrica | Valor |
|---|---|
| Tasks originales | 21 |
| Tasks T-FIX | 7 |
| Tasks completadas | 28/28 (100%) |
| Capa de dominio | `Ocupacion.VacanteId` nullable con navegación; ctors y `Reconstitute` actualizados |
| Capa de aplicación | `VacanteServicioComandos` N1+N2; `OcupacionServicioComandos` N3+Q2 |
| Capa de persistencia | Migración `AddVacanteIdToOcupaciones`; FK `ON DELETE RESTRICT`; índice no único `IX_Ocupaciones_VacanteId` |
| Tests focalizados verdes | 3442/3452 suite completa (10 fallos preexistentes, no regresión del change) |
| Códigos de error nuevos | `VacanteErrorCodigo.PuestoOcupado`, `PersonaIdRequeridoParaCubrir`; `OcupacionErrorCodigo.PuestoSinVacanteAbierta`, `VacanteCanceladaParaReactivar` |
| Escenarios spec compliant | 29/29 (100%) |

## Problemas conocidos (no bloquean el merge)

| # | Problema | Afecta | Resolución recomendada |
|---|---|---|---|
| 1 | Suite completa con 10 fallos preexistentes (datos residuales en `sgv_test` + test ortogonal de Auditoría) | Ningún archivo del change | Issues separados: TRUNCATE/CLEAN entre runs de la suite; corregir render del módulo Auditoría |
| 2 | `docs/migracion-inicial-sgv.sql` tiene `UPDATE` sin `;` en línea preexistente (`:2572-2574`) | Script SQL standalone | Issue separado fuera del scope de este change |
| 3 | Helper `WithEstadoVacante` usa reflection para tests Q1/Q2 | Tests | Reemplazar por builder tipado en mejora futura |

## Arquitectura y decisiones registradas

- **D-1**: `VacanteServicioComandos` inyecta `IOcupacionRepository` para N1; reutiliza `ExistsActiveByPuestoAsync` existente.
- **D-2**: `CambiarEstadoVacanteRequest.PersonaId` (nullable, obligatorio si destino `Cubierta`); atomicidad vía transacción EF única.
- **D-3**: `OcupacionServicioComandos` inyecta `IVacanteRepository` para N3; orden de checks: N3 antes de unicidad.
- **D-4**: Q2 en `ReactivarAsync`; FK rota histórica permite reactivación (Ocupaciones pre-N2).
- **T-5.0**: Distinción `Cubierta` vs `Cancelada` por comparación de nombre literal (`estadoVacante.Nombre == "Cancelada"`).

## Siguiente paso recomendado

El change está listo para merge. El branch `feature/vacante-ocupacion-flow-alignment` contiene el diff completo con WU-1 a WU-7 (incluyendo FIXES). La recomendación es abrir PR contra `develop` con `size:exception` documentada y los 3 issues conocidos referenciados.

## Riesgos residuales

- **N1+N3 TOCTOU**: aceptados y documentados; el índice `ActivePuestoIdUnique` funciona como safety net a nivel BD.
- **Ocupaciones históricas con `VacanteId = NULL`**: intencional; queries que cruzan deben handlear `NULL`.
- **MySQL unique index con `NULL` múltiple**: MySQL permite múltiples `NULL` en unique index; no rompe el constraint existente.

## Trazabilidad de artefactos Engram

| Artefacto | Topic key | Observation ID |
|---|---|---|
| Proposal | `sdd/vacante-ocupacion-flow-alignment/proposal` | (persisted via apply) |
| Spec | `sdd/vacante-ocupacion-flow-alignment/spec` | (persisted via spec) |
| Design | `sdd/vacante-ocupacion-flow-alignment/design` | (persisted via design) |
| Tasks | `sdd/vacante-ocupacion-flow-alignment/tasks` | (persisted via tasks) |
| Apply Progress | `sdd/vacante-ocupacion-flow-alignment/apply-progress` | #1713 |
| Verify Report (initial) | `sdd/vacante-ocupacion-flow-alignment/verify-report` | #1714 |
| Verify Report (post-fix) | `sdd/vacante-ocupacion-flow-alignment/verify-report-2` | #1715 |
| Archive Report | `sdd/vacante-ocupacion-flow-alignment/archive-report` | (este documento) |

---

**Change archivado**: `vacante-ocupacion-flow-alignment`
**Fecha**: 2026-08-07
**Veredicto final**: READY TO MERGE
**Orquestador**: invoked by orchestrator for SDD archive phase
