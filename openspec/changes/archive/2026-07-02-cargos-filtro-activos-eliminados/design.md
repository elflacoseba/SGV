# Design: Cargos filtro activos/eliminados

## 1. Resumen ejecutivo

El diseño replica el patrón ya archivado de Unidades Organizativas, pero aterrizado en Cargos y respetando las diferencias reales del módulo. La decisión central es mover el listado web desde `GetAllAsync()` + paginación en memoria hacia una consulta server-side segmentada por `CargoSegmentoListado`. El borde HTTP/Web seguirá hablando en `status=eliminadas`, pero aplicación, repositorio y controller trabajarán con un enum explícito para no propagar strings ambiguos. El endpoint nuevo será `GET /api/v1/cargos/consulta`, mientras `GET /api/v1/cargos` queda intacto por compatibilidad. La reactivación reutiliza `PATCH /api/v1/cargos/{id}/reactivar`, preservando unicidad activa por `ActiveCodigoUnique`, trazabilidad y permisos actuales.

## 2. Contexto y objetivos

Hoy `src/SGV.Web/Pages/Organizacion/Cargos/Index.cshtml.cs` pagina y ordena en memoria sobre `ICargoApiClient.GetAllAsync()`, y la UI solo contempla activos. El objetivo es habilitar alternancia binaria Activas/Eliminadas, consulta paginada real desde backend, reactivación contextual por fila y preservación de `status/search/sort/p` en toda la navegación. Arquitectónicamente, el cambio se reparte en Clean Architecture: enum/query en Aplicación, filtrado segmentado en Infraestructura, normalización HTTP en API y composición UX en Razor Pages.

## 3. Mapeo de requisitos a diseño

| Requisito | Archivos | Tipo de cambio | Pruebas |
|---|---|---|---|
| REQ-CM-01 | `src/SGV.Aplicacion/Organizacion/Consultas/CargoServicioConsulta.cs`, `src/SGV.Aplicacion/Organizacion/Consultas/ICargoServicioConsulta.cs`, `src/SGV.Aplicacion/Organizacion/Consultas/ICargoRepository.cs`, `src/SGV.Infraestructura/Persistencia/Repositorios/CargoRepository.cs`, `src/SGV.Api/Controllers/CargosController.cs` | nuevo enum/query + método `QueryAsync` + endpoint `consulta` | `CargoServicioConsultaTests.QueryAsync_ConSegmentoEliminadas_RetornaSoloEliminadas`, `CargoRepositoryTests.QueryAsync_MySql_SegmentosNoSeMezclan`, `CargosControllerTests.GET_consulta_status_eliminadas_RetornaSoloEliminadas` |
| REQ-CM-02 | `src/SGV.Aplicacion/Organizacion/Consultas/CargoSegmentoListado.cs` o `Dtos/CargoListQuery.cs` **(a definir en tasks)**, `src/SGV.Api/Controllers/CargosController.cs`, `src/SGV.Web/Pages/Organizacion/Cargos/Index.cshtml.cs` | normalización única de `status` en controller/PageModel | `CargoServicioConsultaTests.NormalizeStatus_ValorDesconocido_CaeA_Activas`, `CargosControllerTests.GET_consulta_status_invalido_CaeA_Activas` |
| REQ-CM-03 | `src/SGV.Aplicacion/Organizacion/Consultas/Dtos/PagedResult.cs`, `CargoServicioConsulta.cs`, `CargoRepository.cs`, `Index.cshtml.cs` | reutilización `PagedResult<T>` + metadatos desde repositorio | `CargoRepositoryTests` de `TotalCount/TotalPages`, prueba API de contrato paginado |
| REQ-CM-04 | `src/SGV.Aplicacion/Organizacion/Comandos/CargoServicioComandos.cs`, `src/SGV.Api/Controllers/CargosController.cs`, `src/SGV.Web/Pages/Organizacion/Cargos/Index.cshtml.cs` | sin cambio de regla; se consume mejor el conflicto | `CargosControllerTests.PATCH_reactivar_RetornaConflictoPorCodigoActivo`, `CargoIndexPageTests.Index_PostReactivate_Falla_ConservaSegmentoEliminadas` |
| REQ-CW-01 | `src/SGV.Web/Pages/Organizacion/Cargos/Index.cshtml`, `Index.cshtml.cs`, `src/SGV.Web/Integration/Organizacion/CargoApiClient.cs`, `ICargoApiClient.cs`, `CargoListItemViewModel.cs` | toggle binario + query server-side | `CargoIndexPageTests.Index_Default_MuestraVistaActivas`, `Index_StatusEliminadas_MuestraToggleActivoEnEliminadas` |
| REQ-CW-02 | `Index.cshtml`, `Index.cshtml.cs`, `src/SGV.Web/wwwroot/js/pages/cargos-index.js` | render contextual + handler `Reactivate` | `CargoIndexPageTests` de render contextual y reactivación |
| REQ-CW-03 | `Index.cshtml.cs`, `ICargoApiClient.cs`, `CargoApiClient.cs` | redirect a activas en éxito / permanencia en eliminadas en falla | `Index_PostReactivate_Exito_RedirigeAActivas`, `Index_PostReactivate_Falla_ConservaSegmentoEliminadas` |
| REQ-CW-04 | `Index.cshtml`, `Index.cshtml.cs`, `src/SGV.Web/Integration/Organizacion/CargoFormHelpers.cs`, `Details.cshtml.cs`, `Edit.cshtml.cs` | preservación de `status` en links, forms y retorno | `Index_PostDelete_AlmacenaLastDeletedId_PermiteReactivarEnBanner` + prueba de links/forms |
| REQ-CW-05 | `src/SGV.Web/wwwroot/js/pages/cargos-index.js`, `tests/SGV.Tests/Web/Cargo/CargoIndexPageTests.cs` | segundo wire SweetAlert2 para reactivate | pruebas JS `requestSubmit` / `formaction` |
| REQ-CW-06 | `Index.cshtml`, `Index.cshtml.cs` | `TempData` con `LastDeletedId` y CTA solo en activas | `Index_PostDelete_AlmacenaLastDeletedId_PermiteReactivarEnBanner` |
| REQ-API-01 (`REQ-SRA-01` en spec) | `src/SGV.Api/Controllers/CargosController.cs`, `tests/SGV.Tests/Api/SwaggerConfigurationTests.cs`, `tests/SGV.Tests/Api/CargosControllerTests.cs` | documentación Swagger de `consulta` + reactivación | `GET_consulta_DocumentadoEnSwagger` |

## 4. Backend (Aplicación + Infraestructura + API)

- **Enum `CargoSegmentoListado`**: ubicarlo en `src/SGV.Aplicacion/Organizacion/Consultas/CargoSegmentoListado.cs` o junto al query DTO en `Dtos/` (decisión menor a cerrar en tasks). Valores: `Activas = 0`, `Eliminadas = 1`. `status` se traduce solo en `CargosController`.
- **`ICargoServicioConsulta.QueryAsync(CargoListQuery query, CancellationToken)`**: nuevo contrato read-only; normaliza page/pageSize ya validados por el borde y retorna `PagedResult<CargoDto>`.
- **`CargoServicioConsulta`**: replicará `UnidadOrganizativaServicioConsulta.QueryAsync`, mapeando `(Items, TotalCount)` del repositorio a `PagedResult<CargoDto>`. La validación de unicidad para reactivar NO se mueve: sigue en `CargoServicioComandos` con `ExistsActiveCodeAsync`.
- **`ICargoRepository.QueryAsync`**: firma análoga a `IUnidadOrganizativaRepository.QueryAsync`, con `search`, `page`, `pageSize`, `CargoSegmentoListado`, `CancellationToken`.
- **`CargoRepository.QueryAsync`**: usará `Context.Set<CargoEntity>().AsNoTracking().Include(c => c.NivelCargo)` y predicado binario `IsActive && !IsDeleted` vs `!IsActive && IsDeleted`; contará antes de `Skip/Take`; ordenará por código y luego dejará a web el sort visible ya existente, salvo que tasks decidan bajar sort al backend.
- **`CargosController`**: agrega `GET /api/v1/cargos/consulta`; mantiene `GET /api/v1/cargos` legacy. Debe documentar `[ProducesResponseType(typeof(PagedResult<CargoDto>), 200)]`, `401` y respuestas de `PATCH /reactivar` ya vigentes.
- **DTOs / query objects**: `PagedResult<T>` y `CargoDto` se reutilizan. `CargoListQuery` web actual deberá ampliarse con `Status`; el query de aplicación será nuevo y tipado.
- **Normalización del `status`**: único punto en controller y único espejo semántico en `IndexModel.NormalizeSegmento`; cualquier valor distinto de `eliminadas` cae a activas.

## 5. Web (SGV.Web)

- **`IndexModel.cs`**: agregar `Segmento`, `IsDeletedView`, `CurrentView` (si se replica exacto el patrón UO), `LastDeletedId`, constante `DeletedView = "eliminadas"`, `NormalizeSegmento`, `OnPostReactivateAsync`, `BuildDetailsRouteValues`, `BuildEditRouteValues`, `ReturnToListRouteValues`. `OnGetAsync` dejará de llamar `GetAllAsync()` y pasará a `QueryAsync(new CargoListQuery(..., Status: Segmento))`.
- **`Index.cshtml`**: toggle Activas/Eliminadas, `hidden status` en GET y POST, CTA con `LastDeletedId`, acciones condicionales por segmento, preservación de `status` en orden/paginación.
- **`ICargoApiClient` / `CargoApiClient`**: sumar `QueryAsync(CargoListQuery)`, `ReactivateAsync(Guid, CancellationToken)` y serialización del query string hacia `/api/v1/cargos/consulta`.
- **`CargoListItemViewModel`**: el item sirve; el `record CargoListQuery` requiere `Status` y probablemente siga en el mismo archivo salvo refactor mínimo.
- **JS (`cargos-index.js`)**: replicar `unidades-organizativas-index.js` con `data-cargo-reactivate-form`, `data-cargo-reactivate-button`, `data-cargo-item-name`, `data-cargo-item-code`, `form.requestSubmit(button)` y fallback `form.submit()`.
- **Suite web**: extender `CargoIndexPageTests` y `FakeCargoApiClient` para soportar `QueryAsync`, segmentos y `ReactivateAsync`.

## 6. Tests

- **Aplicación**: `QueryAsync_ConSegmentoEliminadas_RetornaSoloEliminadas`, `QueryAsync_SegmentosNoSeMezclan`, `NormalizeStatus_ValorDesconocido_CaeA_Activas`.
- **Persistencia MySQL**: `QueryAsync_MySql_SegmentosNoSeMezclan`, `QueryAsync_MySql_ActivaYEliminada_MismoCodigo_RetornaAmbasEnDistintosSegmentos`, metadatos `TotalCount/TotalPages`.
- **API**: `GET_consulta_status_eliminadas_RetornaSoloEliminadas`, `GET_consulta_status_invalido_CaeA_Activas`, `PATCH_reactivar_RetornaConflictoPorCodigoActivo`, `GET_consulta_DocumentadoEnSwagger`.
- **Web**: `Index_Default_MuestraVistaActivas`, `Index_StatusEliminadas_MuestraToggleActivoEnEliminadas`, `Index_PostReactivate_Exito_RedirigeAActivas`, `Index_PostReactivate_Falla_ConservaSegmentoEliminadas`, `Index_PostDelete_AlmacenaLastDeletedId_PermiteReactivarEnBanner`.

## 7. Decisiones técnicas

- **D1**: endpoint `consulta`; alinea naming con `UnidadesOrganizativasController` y reduce costo cognitivo.
- **D2**: paginación server-side; evita duplicar reglas de filtro/segmento en memoria y hace coherente `TotalCount`.
- **D3**: `CargoSegmentoListado`; agrega un tipo, pero elimina strings sueltos fuera del borde.
- **D4**: `LastDeletedId` se mantiene en `TempData` para el siguiente PRG a activas y se limpia al reactivar; si el usuario navega a eliminadas, el CTA no se muestra.
- **D5**: `GET /api/v1/cargos` se conserva como lectura legacy de activos; no se depreca en este cambio.

## 8. Impacto en auditoría, soft-delete y permisos

No cambia el interceptor `src/SGV.Infraestructura/Persistencia/AuditoriaSaveChangesInterceptor.cs`. No cambia el modelo de soft-delete ni `ActiveCodigoUnique` definido en `src/SGV.Infraestructura/Persistencia/Configuraciones/CargoConfiguracion.cs`. Los permisos de `CargosController` se mantienen: lectura autenticada, mutaciones para administrador.

## 9. Diagrama / flujo

| Flujo | Resultado |
|---|---|
| `GET Index` activas | `QueryAsync(status=null)` → muestra activas |
| toggle a eliminadas | `p=1`, preserva `search/sort`, envía `status=eliminadas` |
| `POST Delete` en activas | guarda `LastDeletedId` + banner success |
| CTA/banner Reactivar | usa `LastDeletedId` solo en activas |
| `POST Reactivate` éxito | limpia `LastDeletedId` y redirige a activas |
| `POST Reactivate` falla | mantiene `status=eliminadas` + banner danger |

## 10. Riesgos de implementación

- **Backend / medium**: dejar sort/paginación híbridos puede crear contratos inconsistentes.
- **Web / high**: olvidar `status` en un link/form rompe el segmento post-redirect.
- **Tests / medium**: `FakeCargoApiClient` y `FakeCargoServicio` deben evolucionar con el nuevo contrato o habrá falsos verdes.

## 11. Plan de entrega

Recomendación: **chained PR**. El cambio toca backend, API, cliente web, Razor, JS y cuatro capas de tests; razonablemente supera el budget de 400 líneas. Slice 1: backend + API + tests de aplicación/persistencia/API. Slice 2: web + JS + tests web.

## 12. Checklist de verificación

- `dotnet build SGV.slnx` OK.
- `dotnet test SGV.slnx` OK.
- MySQL tests de `CargoRepositoryTests` pasan.
- `CargoIndexPageTests` cubre toggle/reactivación/TempData/JS.
- Swagger expone `/api/v1/cargos/consulta` y `PATCH /reactivar`.
- Sin regresión en `GET /api/v1/cargos`, detalle, edición y baja lógica actual.

## 13. Referencias

- `openspec/changes/cargos-filtro-activos-eliminados/proposal.md`
- `openspec/changes/cargos-filtro-activos-eliminados/exploration.md`
- `openspec/changes/cargos-filtro-activos-eliminados/specs/**/spec.md`
- `openspec/changes/archive/2026-06-29-reactivar-y-filtrar-unidades-organizativas-eliminadas/design.md`
- `src/SGV.Api/Controllers/CargosController.cs`
- `src/SGV.Infraestructura/Persistencia/Repositorios/CargoRepository.cs`
- `src/SGV.Web/Pages/Organizacion/Cargos/Index.cshtml`
- `src/SGV.Web/Pages/Organizacion/Cargos/Index.cshtml.cs`
