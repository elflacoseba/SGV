# Proposal: Implementar el módulo de Cargos en el Frontend

## Intent

Habilitar el primer slice web autenticado de `Cargos` en `SGV.Web`, reutilizando el patrón de `Unidades Organizativas` pero sin prometer paridad funcional. El objetivo es cubrir consulta de cargos activos, detalle readonly y baja lógica usando exclusivamente contratos backend ya existentes.

## Scope

### In Scope
- Agregar navegación autenticada del shell hacia `Cargos`.
- Incorporar un cliente tipado para `GET /api/v1/cargos`, `GET /api/v1/cargos/{id}` y `DELETE /api/v1/cargos/{id}`.
- Implementar listado web de cargos activos con tabla estilo Inspinia, acciones de detalle y baja lógica confirmada.
- Implementar pantalla de detalle readonly con retorno al listado y manejo visible de errores.
- Mostrar feedback claro cuando la baja sea rechazada por conflicto (por ejemplo, puestos activos asociados).

### Out of Scope
- Alta, edición y formularios de cargo.
- Gestión de habilidades requeridas del cargo.
- Listado de eliminados, reactivación y flujos sobre cargos eliminados.
- Cambios de backend, nuevos endpoints, paginación server-side o filtros de eliminados.

## Non-goals

- No replicar `Unidades Organizativas` en todo su alcance actual.
- No introducir UX que dependa de contratos no disponibles hoy.

## Capabilities

### New Capabilities
- `cargo-web-listado-detalle-baja`: módulo Razor Pages autenticado para listar cargos activos, ver detalle y ejecutar baja lógica con confirmación.

### Modified Capabilities
- `sgv-web-shell`: ampliar la navegación autenticada para exponer `Cargos` como nuevo módulo funcional sin agregar placeholders ajenos a este slice.

## Approach

Replicar la estructura probada de `Unidades Organizativas`: registro DI en `Program.cs`, cliente HTTP tipado, páginas Razor bajo `Pages/Organizacion/Cargos`, `TempData` para feedback, confirmación JS de eliminación y pruebas web con fake client. Como `CargoRepository` y `CargoServicioConsulta` solo exponen cargos activos, el listado será activo-only y el flujo exitoso de baja volverá al listado, sin vista de eliminados ni reactivación.

## Affected Areas

| Area | Impact | Description |
|------|--------|-------------|
| `src/SGV.Web/Program.cs` | Modified | Registrar cliente HTTP de Cargos |
| `src/SGV.Web/Pages/Shared/Partials/_Sidenav.cshtml` | Modified | Exponer navegación del módulo |
| `src/SGV.Web/Integration/Organizacion/**` | Modified | Agregar contrato/cliente/view models de Cargos |
| `src/SGV.Web/Pages/Organizacion/Cargos/**` | New | Listado, detalle y soporte de baja lógica |
| `src/SGV.Web/wwwroot/js/pages/**` | New | Confirmación de eliminación |
| `tests/SGV.Tests/Web/**` | Modified | Cobertura del módulo web de Cargos |

## Risks

| Risk | Likelihood | Mitigation |
|------|------------|------------|
| Deriva a skills o reactivación | High | Dejarlo explícitamente fuera de alcance en specs/diseño |
| `DELETE` rechazado por dependencias | Medium | Traducir `409 Conflict` a feedback accionable |
| Crecimiento excesivo del diff | Medium | Copiar el seam existente y limitar el slice a list/detail/delete |

## Rollback Plan

Revertir las páginas Razor, el cliente HTTP de Cargos, la entrada del menú y las pruebas web. No requiere rollback de base de datos ni de API porque este cambio no modifica contratos backend.

## Dependencies

- `src/SGV.Api/Controllers/CargosController.cs`
- Patrón existente de `src/SGV.Web/Pages/Organizacion/UnidadesOrganizativas/**`

## Success Criteria

- [ ] Un usuario autenticado puede abrir `Cargos` desde el shell y ver cargos activos.
- [ ] Desde el listado puede abrir detalle y volver al contexto del listado.
- [ ] La baja lógica pide confirmación, elimina en éxito y muestra error claro en conflicto.
- [ ] El cambio no introduce UI de skills, eliminados ni reactivación.
