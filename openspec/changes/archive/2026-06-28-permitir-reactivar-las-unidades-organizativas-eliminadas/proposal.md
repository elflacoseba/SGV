# Proposal: Permitir reactivar las unidades organizativas eliminadas

## Intent

Hoy la reactivación ya existe en dominio, aplicación, persistencia y API; el problema real es que `SGV.Web` no la expone y las especificaciones actuales no describen ese flujo. El objetivo es cerrar la brecha operativa para que un administrador pueda restaurar una unidad eliminada desde la shell web sin inventar trabajo backend innecesario.

## Scope

### In Scope
- Exponer la reactivación en `SGV.Web` (cliente API + acciones UI + feedback de conflicto/éxito).
- Mantener el flujo coherente en listado, detalle y edición para unidades eliminadas.
- Alinear specs y tests con el comportamiento ya existente en backend.

### Out of Scope
- Cambios de modelo de datos, migraciones o nueva lógica de reactivación en backend.
- Reglas nuevas de autorización para el endpoint existente.
- Rediseño del listado o del CRUD fuera del flujo de restauración.

## Capabilities

### New Capabilities
- None.

### Modified Capabilities
- `unidad-organizativa-crud`: documentar reactivación como operación soportada y sus conflictos.
- `unidad-organizativa-web-listado`: añadir acción visible para reactivar unidades eliminadas.
- `unidad-organizativa-web-detalle-edicion`: permitir restauración desde detalle/edición y mostrar estado recuperable.
- `sgv-readonly-api`: reflejar que las unidades organizativas pueden reactivarse mediante contrato documentado.

## Approach

Primero actualizar la capa web: agregar el método faltante en el cliente API y la acción de reactivar en las páginas que ya detectan unidades eliminadas. Luego ajustar las especificaciones OpenSpec para describir el flujo real y cubrirlo con pruebas web e integración sin tocar la implementación backend ya existente.

## Affected Areas

| Area | Impact | Description |
|------|--------|-------------|
| `src/SGV.Web/Integration/Organizacion/*` | Modified | Cliente HTTP para reactivación |
| `src/SGV.Web/Pages/Organizacion/UnidadesOrganizativas/*` | Modified | UI de listado, detalle y edición |
| `tests/SGV.Tests/Web/*` | Modified | Cobertura del flujo visible |
| `openspec/specs/*` | Modified | Alineación contractual |

## Risks

| Risk | Likelihood | Mitigation |
|------|------------|------------|
| La UI no explica por qué una reactivación falla | Medium | Mensajes accionables y reutilización del feedback backend |
| Conflictos por código activo duplicado | Medium | Mantener el contrato actual y probar el caso |
| Alcance se expande hacia backend innecesario | Low | Tratar el backend como base existente, no como frente de trabajo |

## Rollback Plan

Revertir los cambios de `SGV.Web`, restaurar las specs anteriores y dejar intacto el endpoint backend existente.

## Dependencies

- El endpoint `PATCH /api/v1/unidades-organizativas/{id}/reactivar` ya debe permanecer disponible.
- Las specs deben reflejar el flujo antes de pasar a diseño.

## Success Criteria

- [ ] Una unidad eliminada puede reactivarse desde `SGV.Web`.
- [ ] La UI muestra éxito o conflicto de forma clara.
- [ ] Las specs describen la reactivación sin duplicar trabajo backend.
