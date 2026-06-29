# Proposal: Reactivar y filtrar unidades organizativas eliminadas

## Intent

Cerrar una brecha operativa en `SGV.Web`: hoy la reactivación existe, pero el listado no permite ver solo eliminadas ni reactivar desde ese contexto. El objetivo es recuperación operativa, sin convertir el listado en una vista mixta.

## Scope

### In Scope
- Agregar en el listado un filtro para ver **solo** unidades organizativas eliminadas.
- Mostrar acción por fila para reactivar dentro de la vista de eliminadas.
- Al reactivar con éxito, volver por defecto al listado de unidades activas con confirmación visible.
- Mostrar mensajes claros y con guía cuando la reactivación falle por conflictos conocidos.

## Non-goals

- No mezclar activas y eliminadas en una misma grilla.
- No rediseñar árbol, detalle o edición en este corte.
- No agregar reglas nuevas de negocio para reactivación.
- No cambiar modelo de datos ni introducir migraciones.

## Capabilities

### New Capabilities
- None.

### Modified Capabilities
- `unidad-organizativa-web-listado`: incorporar vista filtrada de eliminadas y reactivación por fila desde ese contexto.
- `unidad-organizativa-crud`: extender el contrato de consulta para distinguir listado de activas vs. listado solo de eliminadas.
- `sgv-readonly-api`: reflejar el nuevo contrato de consulta del listado y mantener documentada la reactivación existente.

## Approach

Extender el flujo listado→API para aceptar un filtro de estado binario (`activas` o `eliminadas`) y reutilizar el endpoint actual de reactivación. La UX de eliminadas debe preservar búsqueda, paginación y orden dentro de esa vista, pero tras una reactivación exitosa debe regresar al listado activo.

## Affected Areas

| Area | Impact | Description |
|------|--------|-------------|
| `src/SGV.Web/Pages/Organizacion/UnidadesOrganizativas/Index.cshtml(.cs)` | Modified | Filtro, render de eliminadas y acción de reactivar |
| `src/SGV.Web/Integration/Organizacion/*` | Modified | Propagación del filtro y llamada de reactivación |
| `src/SGV.Api/Controllers/UnidadesOrganizativasController.cs` | Modified | Contrato HTTP del listado |
| `src/SGV.Aplicacion` + `src/SGV.Infraestructura` | Modified | Lectura activa vs. solo eliminadas |
| `tests/SGV.Tests/{Web,Api,Aplicacion,Persistencia}/*` | Modified | Cobertura del nuevo flujo |

## Risks

| Risk | Likelihood | Mitigation |
|------|------------|------------|
| Perder contexto entre vistas | Medium | Preservar el filtro en navegación y redirects relevantes |
| Conflictos de reactivación poco claros | Medium | Mensajes accionables con guía de siguiente paso |
| Expandir el alcance a listado mixto | Low | Mantener filtro exclusivo de eliminadas |

## Rollback Plan

Revertir los cambios de listado y contrato de consulta, dejando intacto el endpoint existente de reactivación y retornando al comportamiento actual de listar solo activas.

## Dependencies

- Disponibilidad del endpoint `PATCH /api/v1/unidades-organizativas/{id}/reactivar`.
- Alineación posterior de specs y pruebas con el contrato del filtro.

## Success Criteria

- [ ] El usuario puede cambiar entre listado activo y listado solo de eliminadas.
- [ ] Desde la vista de eliminadas puede reactivar una unidad por fila.
- [ ] Tras reactivar con éxito, la UX vuelve al listado activo con feedback visible.
- [ ] Si la reactivación falla, la UI muestra causa y guía accionable.
