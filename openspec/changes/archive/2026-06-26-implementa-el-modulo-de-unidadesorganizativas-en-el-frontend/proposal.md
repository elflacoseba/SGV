# Proposal: Implementa el módulo de UnidadesOrganizativas en el frontend

## Intent

Habilitar el primer módulo funcional de `SGV.Web` para Unidades Organizativas, acotado al listado autenticado. Hoy la shell solo cubre login/home, mientras `SGV.Api` ya expone `GET /api/v1/unidades-organizativas/consulta` y `DELETE /api/v1/unidades-organizativas/{id}`.

## Scope

### In Scope
- Página Razor protegida para listar unidades organizativas dentro de la shell actual.
- Alta de navegación en sidebar para exponer el módulo.
- Cliente tipado web para consultar listado paginado y ejecutar eliminación.
- Tabla basada en `InspinaTemplate/Inspinia/Pages/Tables/Custom.cshtml`, patrón **Complete Custom Table**, con búsqueda, ordenamiento y paginación visibles.
- Acción por fila para eliminar, con confirmación previa mediante SweetAlert2.
- Estados UX mínimos: vacío, error y refresco del listado tras eliminar.

### Out of Scope
- Alta, edición, detalle, árbol, reactivación y cambio de unidad padre.
- Filtros avanzados, exportación, acciones masivas y permisos granulares.
- Cambios en contratos backend o en persistencia.

## Non-goals

No convertir este primer corte en CRUD completo ni rediseñar la shell web fuera de la navegación necesaria.

## Capabilities

### New Capabilities
- `unidad-organizativa-web-listado`: experiencia web autenticada para consultar y eliminar unidades organizativas desde Razor Pages.

### Modified Capabilities
- `sgv-web-shell`: la navegación deja de ser solo técnica y expone el primer módulo de negocio real.

## Approach

Seguir el patrón actual de `SGV.Web`: PageModels delgados + cliente HTTP tipado registrado en `Program.cs`. La tabla tomará como baseline el ejemplo Inspinia indicado. Búsqueda y paginación se apoyarán en `/consulta`; el ordenamiento del primer corte se define sobre el conjunto visible porque el contrato actual no expone sort server-side.

## Affected Areas

| Area | Impact | Description |
|------|--------|-------------|
| `src/SGV.Web/Pages/Organizacion/UnidadesOrganizativas/**` | New | Página y PageModel del listado |
| `src/SGV.Web/Integration/**` | Modified | Cliente tipado para consulta/eliminación |
| `src/SGV.Web/Pages/Shared/Partials/_Sidenav.cshtml` | Modified | Entrada de menú del módulo |
| `src/SGV.Web/Program.cs` | Modified | Registro DI del cliente |
| `tests/SGV.Tests/Web/**` | Modified | Pruebas web del listado y navegación |

## Risks

| Risk | Likelihood | Mitigation |
|------|------------|------------|
| El API no soporta ordenamiento server-side | Media | Explicitar alcance client-side en specs/diseño |
| `DELETE` puede devolver `409` por dependencias | Media | Mostrar feedback claro y no retirar la fila localmente |
| El módulo fija patrón para futuros clientes web | Media | Reutilizar el enfoque ya usado en autenticación |

## Rollback Plan

Revertir página, menú y cliente del módulo; la shell vuelve a `Home` + autenticación sin tocar API ni base de datos.

## Dependencies

- `GET /api/v1/unidades-organizativas/consulta`
- `DELETE /api/v1/unidades-organizativas/{id}`
- Assets SweetAlert2 ya presentes en la base Inspinia/`SGV.Web`

## Success Criteria

- [ ] Un usuario autenticado puede abrir el listado desde la navegación.
- [ ] La tabla replica la base visual de Inspinia con búsqueda, ordenamiento visible y paginación.
- [ ] El usuario debe confirmar la eliminación con SweetAlert2 antes de enviar la acción.
- [ ] Tras eliminar con éxito, la fila deja de verse al refrescar el listado.
