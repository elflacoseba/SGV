# Proposal: Agrega las opciones para crear, editar y ver el detalle de una Unidad Organizativa

## Intent

Cerrar el hueco de `SGV.Web`: hoy la shell solo lista/elimina, aunque el backend ya soporta create/get/update/change-parent. El primer corte útil es sumar create, detail y edit con manejo de conflictos.

## Scope

### In Scope
- Páginas Razor autenticadas para create, detail y edit.
- Mostrar la unidad padre actual y permitir cambiarla.
- Extender clientes web para `GET by id`, `POST`, `PUT`, `PATCH unidad-padre` y catálogo de tipos.
- UX de validación/conflicto con ayuda.

### Out of Scope
- Árbol, reactivación, acciones masivas, permisos granulares y rediseño backend.
- Nuevas reglas de negocio, migraciones o reemplazo del listado existente.

## Non-goals

No cubrir árbol/reactivación ni rehacer la navegación global.

## Capabilities

### New Capabilities
- `unidad-organizativa-web-detalle-edicion`: experiencia Razor autenticada para create/detail/edit con selector de tipo, unidad padre y feedback de validación/conflicto.

### Modified Capabilities
- `unidad-organizativa-web-listado`: incorpora acciones para crear, ver detalle y editar sin perder búsqueda, paginación y eliminación.
- `unidad-organizativa-crud`: el contrato de lectura debe entregar contexto legible de la unidad padre.

## Approach

Mantener el patrón actual de `SGV.Web`: PageModels delgados + clientes tipados. Detail/edit consumirán `GET /api/v1/unidades-organizativas/{id}`; create usará `POST`; edit separará `PUT` y `PATCH /unidad-padre` solo si cambia el padre. La UI mapeará `ValidationProblemDetails`/`ProblemDetails` a errores por campo o banner accionable.

## Affected Areas

| Area | Impact | Description |
|------|--------|-------------|
| `src/SGV.Web/Pages/Organizacion/UnidadesOrganizativas/**` | Modified/New | Páginas y enlaces |
| `src/SGV.Web/Integration/Organizacion/**` | Modified | Cliente CRUD y errores |
| `src/SGV.Web/Integration/**` | New/Modified | Catálogo de tipos/opciones |
| `src/SGV.Aplicacion/Organizacion/Consultas/Dtos/UnidadOrganizativaDto.cs` | Modified | Contexto de unidad padre |
| `tests/SGV.Tests/Web/UnidadOrganizativaWebTests.cs` | Modified | Cobertura web |

## Risks

| Risk | Likelihood | Mitigation |
|------|------------|------------|
| Cambio de padre requiere flujo multi-endpoint | Media | Diseñar specs explícitas para éxito parcial y mensajes de recuperación |
| DTO actual no expone nombre del padre | Alta | Definir delta de contrato mínimo, sin rediseño backend |
| Conflictos de negocio poco accionables | Media | Traducir `ProblemDetails` a mensajes con guía concreta |

## Rollback Plan

Revertir páginas, enlaces y clientes web, y deshacer el enriquecimiento mínimo del DTO. La shell vuelve al alcance actual sin tocar persistencia.

## Dependencies

- `GET/POST/PUT/PATCH /api/v1/unidades-organizativas/**`
- `GET /api/v1/tipos-unidad-organizativa`
- SweetAlert2/flash messaging en `SGV.Web`

## Success Criteria

- [ ] Un administrador puede abrir create, detail y edit desde el módulo actual.
- [ ] Detail/edit muestran la unidad padre actual y permiten cambiarla.
- [ ] Validaciones y conflictos de negocio se ven con mensajes claros y guía para corregir.
- [ ] El cambio no introduce rediseño backend ni amplía alcance a árbol/reactivación.
