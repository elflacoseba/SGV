# Tasks: Implementa el módulo de UnidadesOrganizativas en el frontend

## Review Workload Forecast

| Field | Value |
|-------|-------|
| Estimated changed lines | 560-760 |
| 400-line budget risk | High |
| Chained PRs recommended | Yes |
| Suggested split | PR 1 shell+cliente+GET, PR 2 delete+SweetAlert2+feedback, PR 3 cierre+validación |
| Delivery strategy | ask-always |
| Chain strategy | pending |

Decision needed before apply: Yes
Chained PRs recommended: Yes
Chain strategy: pending
400-line budget risk: High

### Suggested Work Units

| Unit | Goal | Likely PR | Notes |
|------|------|-----------|-------|
| 1 | Base navegable y consulta SSR | PR 1 | Shell + cliente + tests RED/GREEN |
| 2 | Eliminación confirmada y manejo 409 | PR 2 | Depende de PR 1 |
| 3 | Pulido final y validaciones | PR 3 | Depende de PR 2 |

## Phase 1: Base de pruebas y contratos

- [x] 1.1 RED: crear `tests/SGV.Tests/Web/UnidadOrganizativaWebTests.cs` con escenarios de acceso autenticado, menú, carga inicial, vacío y error según specs.
- [x] 1.2 GREEN: extender `tests/SGV.Tests/Web/SgvWebApplicationFactory.cs` para inyectar dobles del nuevo cliente `Integration/Organizacion` sin romper auth.
- [x] 1.3 REFACTOR: crear `src/SGV.Web/Integration/Organizacion/IUnidadOrganizativaApiClient.cs` y `UnidadOrganizativaListItemViewModel.cs` con contrato de consulta/delete alineado al diseño.

## Phase 2: Listado SSR y navegación

- [x] 2.1 RED: agregar assertions en `UnidadOrganizativaWebTests.cs` para título, buscador, paginación y ordenamiento visible en `/organizacion/unidades-organizativas`.
- [x] 2.2 GREEN: implementar `src/SGV.Web/Integration/Organizacion/UnidadOrganizativaApiClient.cs` y registrar DI en `src/SGV.Web/Program.cs`.
- [x] 2.3 GREEN: crear `src/SGV.Web/Pages/Organizacion/UnidadesOrganizativas/Index.cshtml.cs` con `[Authorize]`, query binding, `OnGetAsync`, sort visible y estados vacío/error.
- [x] 2.4 GREEN: crear `src/SGV.Web/Pages/Organizacion/UnidadesOrganizativas/Index.cshtml` usando baseline Inspinia “Complete Custom Table”.
- [x] 2.5 GREEN: modificar `src/SGV.Web/Pages/Shared/Partials/_Sidenav.cshtml` para exponer `Home` + `Unidades Organizativas` y estado activo.

## Phase 3: Eliminación y feedback

- [x] 3.1 RED: ampliar `UnidadOrganizativaWebTests.cs` con cancelación, éxito delete y rechazo `409`, incluyendo permanencia de fila y mensaje visible.
- [x] 3.2 GREEN: incorporar SweetAlert2 en `src/SGV.Web/package.json` y `src/SGV.Web/plugins.config.js`, con hook JS en `Index.cshtml` para confirmación previa al POST.
- [x] 3.3 GREEN: completar `OnPostDeleteAsync` en `Index.cshtml.cs` y `UnidadOrganizativaApiClient.cs` para `204/404/409/5xx`, `TempData` y redirección conservando `page/search/sort`.

## Phase 4: Verificación y cierre

- [x] 4.1 REFACTOR: limpiar duplicaciones de tests/helpers web y dejar nombres/mensajes consistentes con el alcance solo listado.
- [x] 4.2 Validar `dotnet test SGV.slnx` y, por cambios en assets web, `bun run build` dentro de `src/SGV.Web`.
