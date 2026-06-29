# Tareas: Permitir reactivar las unidades organizativas eliminadas

## Review Workload Forecast

| Field | Value |
|-------|-------|
| Estimated changed lines | 520-680 |
| 400-line budget risk | High |
| Chained PRs recommended | Yes |
| Suggested split | PR 1 contratos y cliente web -> PR 2 flujo listado -> PR 3 detail/edit |
| Delivery strategy | ask-on-risk |
| Chain strategy | pending |

Decision needed before apply: Yes
Chained PRs recommended: Yes
Chain strategy: pending
400-line budget risk: High

### Suggested Work Units

| Unit | Goal | Likely PR | Notes |
|------|------|-----------|-------|
| 1 | Cubrir contrato existente de reactivación y exponer `ReactivateAsync` en `SGV.Web` | PR 1 | Base `main`; incluye tests API/persistencia y cliente HTTP |
| 2 | Agregar banner/reactivación en `Index` preservando `p/search/sort/view` | PR 2 | Depende de PR 1; diff enfocado en listado y tests web |
| 3 | Hacer recuperables `Details` y `Edit` con PRG y feedback | PR 3 | Depende de PR 2; mantiene el contexto de retorno |

## Phase 1: Contrato y regresión backend

- [x] 1.1 RED: ampliar `tests/SGV.Tests/Persistencia/UnidadOrganizativaRepositoryTests.cs` con casos de `ReactivateAsync` para restaurar `IsActive/IsDeleted/DeletedAt` y cubrir el escenario de unidad eliminada recuperable.
- [x] 1.2 RED: agregar en `tests/SGV.Tests/Api/UnidadesOrganizativasControllerTests.cs` los escenarios `PATCH /api/v1/unidades-organizativas/{id}/reactivar` para `200`, `404` y `409` según la spec `unidad-organizativa-crud`.
- [x] 1.3 GREEN/REFACTOR: extender `tests/SGV.Tests/Api/SwaggerConfigurationTests.cs` para verificar que `/api/v1/unidades-organizativas/{id}/reactivar` figure en Swagger con `PATCH` y respuestas documentadas.

## Phase 2: Cliente web y flujo de listado

- [x] 2.1 RED: en `tests/SGV.Tests/Web/UnidadOrganizativaWebTests.cs`, escribir pruebas fallidas para banner post-delete, reactivación exitosa y conflicto desde `Index`, preservando `p/search/sort/view`.
- [x] 2.2 GREEN: modificar `src/SGV.Web/Integration/Organizacion/IUnidadOrganizativaApiClient.cs` y `src/SGV.Web/Integration/Organizacion/UnidadOrganizativaApiClient.cs` para soportar `ReactivateAsync(Guid)` con mapeo `200/404/409` a `UnidadOrganizativaCommandResult`.
- [x] 2.3 GREEN: actualizar `src/SGV.Web/Pages/Organizacion/UnidadesOrganizativas/Index.cshtml.cs` para guardar la última eliminación en `TempData`, agregar `OnPostReactivateAsync` y mantener PRG con el mismo contexto de listado.
- [x] 2.4 REFACTOR: ajustar `src/SGV.Web/Pages/Organizacion/UnidadesOrganizativas/Index.cshtml` para renderizar CTA de reactivación, éxito/conflicto y no romper el toggle `list/tree`.

## Phase 3: Estados recuperables en detail y edit

- [x] 3.1 RED: sumar en `tests/SGV.Tests/Web/UnidadOrganizativaWebTests.cs` escenarios fallidos para `Details` y `Edit` cuando `GetByIdAsync` devuelve `null`, incluyendo éxito y conflicto al reactivar.
- [x] 3.2 GREEN: modificar `src/SGV.Web/Pages/Organizacion/UnidadesOrganizativas/Details.cshtml.cs` y `Details.cshtml` para mostrar estado recuperable, `OnPostReactivateAsync` y redirección coherente a `Details` con `returnPage/search/sort/view`.
- [x] 3.3 GREEN/REFACTOR: modificar `src/SGV.Web/Pages/Organizacion/UnidadesOrganizativas/Edit.cshtml.cs` y `Edit.cshtml` para bloquear el formulario si la unidad está eliminada y ofrecer reactivación con salida segura al listado.

## Phase 4: Verificación de slices

- [x] 4.1 Ejecutar `dotnet test SGV.slnx --filter "FullyQualifiedName~UnidadOrganizativaRepositoryTests|FullyQualifiedName~UnidadesOrganizativasControllerTests|FullyQualifiedName~SwaggerConfigurationTests|FullyQualifiedName~UnidadOrganizativaWebTests"` tras cada slice para cerrar RED->GREEN->REFACTOR sin arrastrar fallas.
- [x] 4.2 Ejecutar `dotnet test SGV.slnx` antes de cerrar apply y dejar el cambio listo para `sdd-verify`.
