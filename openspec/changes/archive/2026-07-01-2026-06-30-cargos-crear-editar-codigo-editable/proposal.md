# Proposal: Implementa en el front crear/editar Cargos

## Intent

Completar `Cargos` en `SGV.Web` con create/edit y permitir en backend que `Codigo` sea editable en `PUT /api/v1/cargos/{id}` sin duplicados activos. El cambio cierra la brecha del slice web archivado y revierte deliberadamente la inmutabilidad definida el 2026-06-18.

## Scope

### In Scope
- `SGV.Web`: `Create.cshtml`, `Edit.cshtml`, `_Form.cshtml`, `ICargoForm`, PRG, validación server-side, helpers de `ModelState`, integración API y smoke tests web.
- `SGV.Web`: entrada `Nueva` en `_Sidenav.cshtml`.
- `SGV.Web`: selector `Nivel` desde `GET /api/v1/niveles-cargo`.
- Backend: `ActualizarCargoRequest` agrega `Codigo`.
- Backend: validator y update verifican unicidad de `Codigo` activo.
- Backend: dominio/handler permiten cambiar `Codigo`.
- Tests: se reemplaza `CargoTests.Actualizar_CodigoNoCambia` y se cubren update válido/conflicto duplicado.
- Spec: actualizar `openspec/specs/cargo-management/spec.md` y `openspec/specs/cargo-web-listado-detalle-baja/spec.md`.

### Out of Scope / Non-goals
- Reactivación de cargos eliminados desde UI.
- Skills asociadas a cargos.
- Soft delete UI fuera del flujo actual.
- Cambios en otros módulos.
- i18n/a11y fuera del patrón actual.

## Capabilities

### New Capabilities
- None.

### Modified Capabilities
- `cargo-management`: `Codigo` editable solo si no duplica otro activo.
- `cargo-web-listado-detalle-baja`: el módulo web deja de excluir create/edit.

## Approach

Replicar `UnidadesOrganizativas`: Razor Pages separadas, partial compartido, sin modales, PRG y roundtrip server-side. La unicidad activa seguirá apoyándose en `IX_Cargos_ActiveCodigoUnique`.

## Key Design Decisions

- Razor Pages + `_Form.cshtml` compartido.
- `Codigo` editable en edit.
- PRG post-guardado.
- Validación server-side como flujo principal.

## Risks

- **Crítico**: alto riesgo de superar 400 líneas; backend + frontend probablemente requieran chained PRs.
- `PUT /api/v1/cargos/{id}` cambia contrato; no encontré otros consumers runtime internos fuera de API/app/tests, pero puede afectar consumidores externos.
- Revierte una decisión archivada; la trazabilidad debe quedar explícita.
- Hay que confirmar el comportamiento del índice único en update, no solo en create.

## Assumptions / Proposal question round

- Se mantiene exactamente el patrón UX de `UnidadesOrganizativas`.
- `GET /api/v1/niveles-cargo` es el catálogo único de nivel.
- Si existen consumers externos del `PUT`, requerirán coordinación.
- La regla de unicidad en edit es la misma que en create.

## Rollback Plan

Revertir páginas web, cliente tipado y cambios de contrato/regla de update. No requiere migración nueva.

## Success Criteria

- [ ] Usuarios autenticados pueden crear y editar cargos desde `SGV.Web`.
- [ ] `Codigo` solo cambia cuando no produce duplicado activo.
- [ ] El shell muestra `Nueva` y el formulario carga `NivelCargo` desde catálogo.

## Notas de revisión

`exploration.md` asumía `Codigo` readonly; esa asunción queda reemplazada por decisión explícita del usuario.
