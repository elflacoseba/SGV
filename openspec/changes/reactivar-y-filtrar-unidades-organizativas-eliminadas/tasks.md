# Tareas: Reactivar y filtrar unidades organizativas eliminadas

## Review Workload Forecast

| Field | Value |
|-------|-------|
| Estimated changed lines | 480-640 |
| 400-line budget risk | High |
| Chained PRs recommended | Yes |
| Suggested split | PR 1 consulta segmentada -> PR 2 API+cliente web -> PR 3 UI+flujo de reactivación |
| Delivery strategy | single-pr |
| Chain strategy | size-exception |

Decision needed before apply: Yes
Chained PRs recommended: Yes
Chain strategy: size-exception
400-line budget risk: High

### Suggested Work Units

| Unit | Goal | Likely PR | Notes |
|------|------|-----------|-------|
| 1 | Segmento `activas/eliminadas` en aplicación y repositorio | PR 1 | Base `main`; incluye tests de aplicación y persistencia |
| 2 | Exponer/documentar el filtro y propagarlo en `SGV.Web` | PR 2 | Depende de PR 1; concentra API, Swagger y cliente HTTP |
| 3 | Completar toggle, reactivación por fila y redirects | PR 3 | Depende de PR 2; cierra la UX y tests web |

## Phase 1: Consulta segmentada backend

- [x] 1.1 RED: ampliar `tests/SGV.Tests/Aplicacion/Organizacion/UnidadOrganizativaServicioConsultaTests.cs` con escenarios de segmento por defecto, `eliminadas` y no mezcla según `unidad-organizativa-crud`.
- [x] 1.2 RED: extender `tests/SGV.Tests/Persistencia/UnidadOrganizativaRepositoryTests.cs` para verificar en MySQL que `QueryAsync` devuelve solo activas o solo eliminadas.
- [x] 1.3 GREEN: modificar `src/SGV.Aplicacion/Organizacion/Consultas/Dtos/UnidadOrganizativaQuery.cs`, `IUnidadOrganizativaRepository.cs`, `UnidadOrganizativaServicioConsulta.cs` y `src/SGV.Infraestructura/Persistencia/Repositorios/UnidadOrganizativaRepository.cs` para propagar y aplicar el segmento.
- [x] 1.4 REFACTOR: consolidar helpers/fakes de consulta en esos tests sin alterar `ListAsync`, `GetByIdAsync` ni `GetTreeAsync`.

## Phase 2: Contrato HTTP y cliente web

- [x] 2.1 RED: agregar en `tests/SGV.Tests/Api/UnidadesOrganizativasControllerTests.cs` y `SwaggerConfigurationTests.cs` los escenarios del query param `status` y su documentación, sin tocar `arbol`.
- [x] 2.2 GREEN: actualizar `src/SGV.Api/Controllers/UnidadesOrganizativasController.cs` para aceptar/documentar `activas` y `eliminadas` manteniendo `UnidadOrganizativaDto`.
- [x] 2.3 GREEN: modificar `src/SGV.Web/Integration/Organizacion/UnidadOrganizativaListItemViewModel.cs`, `IUnidadOrganizativaApiClient.cs` y `UnidadOrganizativaApiClient.cs` para serializar el segmento en `/consulta`.
- [x] 2.4 REFACTOR: ajustar `tests/SGV.Tests/Api/ApiWebApplicationFactory.cs` y dobles web para responder por segmento y evitar duplicación.

## Phase 3: Listado web y reactivación

- [x] 3.1 RED: ampliar `tests/SGV.Tests/Web/UnidadOrganizativaWebTests.cs` con toggle activas/eliminadas, vacío contextual, reactivación exitosa volviendo a activas y conflicto permaneciendo en eliminadas.
- [x] 3.2 GREEN: modificar `src/SGV.Web/Pages/Organizacion/UnidadesOrganizativas/Index.cshtml.cs` para bindear el segmento, conservar `page/search/sort`, ejecutar `OnPostReactivateAsync` y redirigir según éxito o conflicto.
- [x] 3.3 GREEN: actualizar `src/SGV.Web/Pages/Organizacion/UnidadesOrganizativas/Index.cshtml` para mostrar selector binario, filas solo del segmento activo y botón `Reactivar` por fila solo en eliminadas.
- [x] 3.4 REFACTOR: modificar `src/SGV.Web/Integration/Organizacion/UnidadOrganizativaFormHelpers.cs` para preservar el segmento en retornos desde detalle/edición al listado.

## Phase 4: Verificación del slice

- [x] 4.1 Ejecutar `dotnet test SGV.slnx --filter "FullyQualifiedName~UnidadOrganizativaServicioConsultaTests|FullyQualifiedName~UnidadOrganizativaRepositoryTests|FullyQualifiedName~UnidadesOrganizativasControllerTests|FullyQualifiedName~SwaggerConfigurationTests|FullyQualifiedName~UnidadOrganizativaWebTests"` al cerrar cada ciclo RED/GREEN/REFACTOR.
- [x] 4.2 Ejecutar `dotnet test SGV.slnx` y, por cambios en `src/SGV.Web`, `bun run build` antes de pasar a `sdd-verify`.
