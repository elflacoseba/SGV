# Tasks: Agrega las opciones para crear, editar y ver el detalle de una Unidad Organizativa

## Review Workload Forecast

| Field | Value |
|-------|-------|
| Estimated changed lines | 750-1050 |
| 400-line budget risk | High |
| Chained PRs recommended | Yes |
| Suggested split | PR 1 contrato+cliente base → PR 2 listado+create+details → PR 3 edit+warning parcial+hardening |
| Delivery strategy | ask-on-risk |
| Chain strategy | stacked-to-develop |

Decision needed before apply: Yes
Chained PRs recommended: Yes
Chain strategy: stacked-to-develop
400-line budget risk: High

### Suggested Work Units

| Unit | Goal | Likely PR | Notes |
|------|------|-----------|-------|
| 1 | Enriquecer DTO y cliente tipado con pruebas API/aplicación | PR 1 | Base para las páginas; incluye RED/GREEN/REFACTOR backend+integration. |
| 2 | Agregar navegación del listado y páginas `Create`/`Details` con pruebas web | PR 2 | Base = PR 1 si se encadena; deja create/detail cerrados y revisables. |
| 3 | Implementar `Edit` con `PUT`/`PATCH`, warning parcial y pruebas finales | PR 3 | Base = PR 2 si se encadena; concentra el riesgo del flujo multi-endpoint. |

## Phase 1: Contrato y cliente base

- [x] 1.1 **RED**: ampliar `tests/SGV.Tests/Aplicacion/Organizacion/UnidadOrganizativaServicioConsultaTests.cs` y `tests/SGV.Tests/Api/UnidadesOrganizativasControllerTests.cs` para exigir `unidadPadreCodigo/nombre` en `GET /{id}` y `/consulta`, incluyendo unidad raíz.
- [x] 1.2 **GREEN**: actualizar `src/SGV.Aplicacion/Organizacion/Consultas/Dtos/UnidadOrganizativaDto.cs`, `src/SGV.Aplicacion/Organizacion/Consultas/UnidadOrganizativaServicioConsulta.cs` y `src/SGV.Infraestructura/Persistencia/Repositorios/UnidadOrganizativaRepository.cs` para mapear el resumen legible del padre.
- [x] 1.3 **REFACTOR**: extender `src/SGV.Web/Integration/Organizacion/IUnidadOrganizativaApiClient.cs`, `UnidadOrganizativaApiClient.cs` para `GetByIdAsync`, `GetTreeAsync`, `GetTiposAsync`, `CreateAsync`, `UpdateAsync`, `ChangeParentAsync` y extender `FakeUnidadOrganizativaApiClient` acorde.

## Phase 2: Listado y navegación

- [x] 2.1 **RED**: extender `tests/SGV.Tests/Web/UnidadOrganizativaWebTests.cs` para exigir botón crear, acciones detalle/editar por fila y preservación de `page/search/sort` al volver.
- [x] 2.2 **GREEN**: modificar `src/SGV.Web/Pages/Organizacion/UnidadesOrganizativas/Index.cshtml` y `Index.cshtml.cs` para renderizar enlaces create/detail/edit y helpers de retorno reutilizables.
- [x] 2.3 **REFACTOR**: consolidar el armado de rutas/mensajes de navegación en view models/helpers de `src/SGV.Web/Integration/Organizacion/` sin mezclar lógica de negocio en PageModels.

## Phase 3: Create y Details

- [x] 3.1 **RED**: agregar en `tests/SGV.Tests/Web/UnidadOrganizativaWebTests.cs` escenarios de auth, carga de catálogos, create exitoso, validación por campo y detail con padre legible o estado no disponible.
- [x] 3.2 **GREEN**: crear `src/SGV.Web/Pages/Organizacion/UnidadesOrganizativas/Create.cshtml(.cs)`, `Details.cshtml(.cs)` y `_Form.cshtml` con PRG, antiforgery, catálogos de tipo/padre y acción visible de volver.
- [x] 3.3 **REFACTOR**: reutilizar el parcial `_Form.cshtml` y normalizar mapeo de `ValidationProblemDetails` a `ModelState` para conservar datos ingresados.

## Phase 4: Edit y warning de éxito parcial

- [ ] 4.1 **RED**: ampliar `tests/SGV.Tests/Web/UnidadOrganizativaWebTests.cs` con edit exitoso, cambio de padre, conflicto 409, validación 400 y warning cuando `PATCH /unidad-padre` falla tras un `PUT` exitoso.
- [ ] 4.2 **GREEN**: crear `src/SGV.Web/Pages/Organizacion/UnidadesOrganizativas/Edit.cshtml(.cs)` con snapshot del padre original, exclusión de self/descendientes en opciones y flujo `PUT` seguido de `PATCH` solo si cambia el padre.
- [ ] 4.3 **REFACTOR**: centralizar banners/status de éxito parcial y recarga recuperable en `Edit.cshtml.cs` + cliente tipado para evitar duplicación entre create/edit/detail.

## Phase 5: Verificación final

- [ ] 5.1 Ejecutar `dotnet test SGV.slnx --filter "UnidadOrganizativa|UnidadesOrganizativasController|UnidadOrganizativaWebTests"` y corregir desvíos contra los escenarios de specs antes de marcar apply.
- [ ] 5.2 Ejecutar `dotnet build SGV.slnx` y registrar en `tasks.md`/`apply-progress.md` qué work unit quedó listo para PR 1, PR 2 o PR 3 según la estrategia elegida.
