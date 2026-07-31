# Archive Report: fix-vacante-estado-inicial-no-terminal-issue-236

```yaml
schema: gentle-ai.sdd-archive/v1
change: fix-vacante-estado-inicial-no-terminal-issue-236
archived_by: sdd-archive executor
date: 2026-07-31
mode: hybrid
branch: develop (direct commit 2bfa58c)
tasks_completion: all_checked
verdict: pass
```

## Change Summary

| Campo | Valor |
|-------|-------|
| **Change** | `fix-vacante-estado-inicial-no-terminal-issue-236` |
| **Commit** | `2bfa58c` (`feat(vacantes): reject terminal estado inicial on create`) |
| **Archivos** | 3 archivos modificados, 87 insertions |
| **Tests** | 38/38 verde (`VacanteServicioComandosTests + VacantesControllerTests`) |
| **Verify-report** | APROBADO sin CRITICAL ni WARNING |
| **Modo** | Strict TDD (`strict_tdd: true` en `openspec/config.yaml`) |
| **Persistencia** | Híbrida (OpenSpec + Engram) |
| **Estrategia** | Single PR (~80-100 líneas) |

## Spec Delta Sincronizado

| Domain | Action | Detalle |
|--------|--------|---------|
| `vacante-management` | **Modified** | Requisito "Crear Vacante" actualizado con regla: "El `EstadoVacanteId` inicial NO DEBE referenciar un estado terminal (`EsTerminal = true`)" |

### Cambio específico en el requisito "Crear Vacante"

- **Texto antes**: no imponía restricción sobre la terminalidad del estado inicial.
- **Texto después**: se agregó la regla `El EstadoVacanteId inicial NO DEBE referenciar un estado terminal (EsTerminal = true); si lo hace, la operación DEBE ser rechazada con 400 Bad Request`.
- **Escenario "Creación exitosa"** actualizado: `EstadoVacanteId` ahora debe ser "existente **no terminal**".
- **Escenario nuevo "Estado inicial terminal rechazado"** agregado al requisito.

## Artefactos Archivados

- ✅ `proposal.md`
- ✅ `specs/vacante-management/spec.md` (delta histórico)
- ✅ `design.md`
- ✅ `tasks.md` (todas las tareas marcadas `[x]`)
- ✅ `apply-progress.md`
- ✅ `verify-report.md`
- ✅ `archive-report.md` (este archivo)

## Source of Truth Actualizado

- `openspec/specs/vacante-management/spec.md` — requisito "Crear Vacante" refleja la nueva regla de rechazo de estado terminal inicial.

## Decisiones de Implementación

| ID | Descripción |
|----|-------------|
| D-1 | La validación de estado terminal vive en `VacanteServicioComandos.CrearAsync`, no en el validador FluentValidation ni en el controller. |
| D-2 | Mensaje compartido entre `Error.Message` y `FieldErrors["estadoVacanteId"]`: `"El estado inicial de la vacante no puede ser un estado terminal (Cubierta, Cancelada)."` |
| D-3 | No se tocó el validador ni el controller; la regla es puramente de servicio de aplicación. |

## Riesgos y Mitigaciones

| Riesgo | Severidad | Mitigación |
|--------|-----------|------------|
| Clientes web que asumen `201` con cualquier `EstadoVacanteId` | Baja | El cambio es correctivo; clientes que ya usan estados no-terminales no se ven afectados. |
| `EstadoCubiertaId`/`EstadoCanceladaId` no exportados | Baja | Si no visibles, usar `Guid.Parse` inline (20000000-… bloque GUID). |

## Commit Realizado

```bash
git add openspec/specs/vacante-management/spec.md \
        openspec/changes/archive/2026-07-31-fix-vacante-estado-inicial-no-terminal-issue-236/
git commit -m "docs(openspec): archive fix-vacante-estado-inicial-no-terminal-issue-236"
```

## SDD Cycle Complete

El change #236 ha sido completamente:
- ✅ Propuesto
- ✅ Especificado (delta spec)
- ✅ Diseñado
- ✅ Dividido en tareas
- ✅ Implementado (single PR)
- ✅ Verificado (38/38 tests verde, verify-report APROBADO)
- ✅ Archivado

**Issue #236 lista para cerrarse.**
