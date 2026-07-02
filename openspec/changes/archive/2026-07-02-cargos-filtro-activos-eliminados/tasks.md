# Tareas: cargos-filtro-activos-eliminados

## 1. Resumen ejecutivo

El cambio replica en Cargos el patrón ya archivado para Unidades Organizativas: extender la consulta backend con un segmento binario `activas` / `eliminadas`, exponer `GET /api/v1/cargos/consulta` y actualizar la página web `Index` para alternar entre vistas con reactivación por fila y preservación de contexto (`status`, `search`, `sort`, `p`). Mantiene `PATCH /api/v1/cargos/{id}/reactivar`, `CargoSegmentoListado` se usará en aplicación/controller y `status` solo vive en el borde HTTP/Web. La paginación se mueve a server-side para evitar reglas duplicadas en memoria. No hay migración de esquema ni cambios en soft-delete ni en `ActiveCodigoUnique`.

## 2. Notas de planificación

### 2.1 Resolución de ambigüedad menor del design

El design dejó dos alternativas para alojar `CargoSegmentoListado`:
- `src/SGV.Aplicacion/Organizacion/Consultas/CargoSegmentoListado.cs` (archivo dedicado al enum).
- `src/SGV.Aplicacion/Consultas/Dtos/CargoListQuery.cs` (junto al record de query).

**Decisión cerrada en tasks:** se adopta la opción B, alineada con el patrón vigente (`UnidadOrganizativaSegmentoListado` y `UnidadOrganizativaQuery` conviven en `src/SGV.Aplicacion/Organizacion/Consultas/Dtos/UnidadOrganizativaQuery.cs`). Archivo único:

- `src/SGV.Aplicacion/Organizacion/Consultas/Dtos/CargoListQuery.cs`
  - `public enum CargoSegmentoListado { Activas = 0, Eliminadas = 1 }`
  - `public sealed record CargoListQuery(int Page, int PageSize, string? Search, string? Sort, CargoSegmentoListado Segmento = CargoSegmentoListado.Activas);`

El nombre del record de aplicación es `CargoListQuery` (no `CargoQuery`) para evitar confusión con el `CargoListQuery` ya existente en la capa Web (`src/SGV.Web/Integration/Organizacion/CargoListItemViewModel.cs`), que se ampliará con `Status` para alinear con `UnidadOrganizativaListQuery`. No se renombra el de la capa Web para no propagar diffs por toda la suite.

### 2.2 Orden de ejecución

Se sigue el orden backend → API → web → JS → verificación, porque:
1. El frontend no puede inventar la vista de eliminados sin un contrato backend real (gap explícito en la exploración).
2. El cliente web necesita primero la firma definitiva de `QueryAsync` para mapear `status` en el query string.
3. El `Index.cshtml` debe consumir el `CargoSegmentoListado` ya normalizado en `IndexModel`.
4. El harness JS solo tiene sentido una vez que la Razor expone los `data-cargo-reactivate-*`.

### 2.3 Dependencias entre tareas

- T-002 depende de T-001 (necesita `CargoSegmentoListado` y `CargoListQuery`).
- T-003 depende de T-002 (servicio consulta llama al repositorio).
- T-004 depende de T-002 y T-003 (controller delega en el servicio).
- T-005 depende de T-004 (cliente web consume el endpoint nuevo).
- T-006 depende de T-005 (PageModel usa `QueryAsync`).
- T-007 depende de T-006 (Razor renderiza `Segmento`, `IsDeletedView`, `LastDeletedId`, helpers).
- T-008 depende de T-007 (JS se wirea a la nueva fila Reactivar).
- T-009 depende de T-008 (verificación final y Swagger).

### 2.4 Convenciones de commits

- Conventional Commits en español/inglés neutro (`feat: ...`, `fix: ...`, `chore: ...`, `test: ...`).
- Mensajes cortos y específicos al comportamiento que entrega el commit.
- Tests en el mismo commit que el código de producción que cubren (TDD estricto).
- Sin `Co-Authored-By` ni atribución a IA.
- Cada tarea propone un mensaje de commit sugerido.

## 3. Tareas

### T-001 — Enum `CargoSegmentoListado` y `CargoListQuery` de aplicación + tests unitarios

- **Capa(s)**: Aplicación / Tests.
- **Archivos a tocar/crear**:
  - `src/SGV.Aplicacion/Organizacion/Consultas/Dtos/CargoListQuery.cs` (nuevo).
  - `tests/SGV.Tests/Aplicacion/Organizacion/CargoListQueryTests.cs` (nuevo).
- **Requisitos cubiertos**: REQ-CM-02 (tipo de segmento y normalización a activas por defecto).
- **Estrategia TDD**:
  - RED: `CargoListQueryTests.Default_SegmentoEsActivas`, `CargoListQueryTests.PuedeConstruirQueryParaEliminadas`, `CargoListQueryTests.CargoSegmentoListado_TieneValoresEsperados`.
  - GREEN: declarar `enum CargoSegmentoListado { Activas = 0, Eliminadas = 1 }` y `record CargoListQuery(int Page, int PageSize, string? Search, string? Sort, CargoSegmentoListado Segmento = CargoSegmentoListado.Activas)`.
- **Comando de verificación**:
  ```
  dotnet test tests/SGV.Tests/Aplicacion/Organizacion/CargoListQueryTests.cs
  ```
- **Estimación**: 0.5 h — ~30 líneas de código + ~40 líneas de tests.
- **Dependencias**: ninguna.
- **Riesgos específicos**: bajo. Es un cambio de tipos puros sin dependencias externas.

### T-002 — `ICargoServicioConsulta.QueryAsync` + `CargoServicioConsulta.QueryAsync` + tests aplicación

- **Capa(s)**: Aplicación / Tests.
- **Archivos a tocar/crear**:
  - `src/SGV.Aplicacion/Organizacion/Consultas/ICargoServicioConsulta.cs` (modificar: agregar `QueryAsync`).
  - `src/SGV.Aplicacion/Organizacion/Consultas/CargoServicioConsulta.cs` (modificar: implementar `QueryAsync`).
  - `tests/SGV.Tests/Aplicacion/Organizacion/CargoServicioConsultaTests.cs` (modificar: agregar escenarios de segmento + `FakeCargoRepository.QueryAsync`).
- **Requisitos cubiertos**: REQ-CM-01, REQ-CM-02, REQ-CM-03.
- **Estrategia TDD**:
  - RED: `QueryAsync_ConSegmentoActivas_RetornaSoloActivos`, `QueryAsync_ConSegmentoEliminadas_RetornaSoloEliminados`, `QueryAsync_SegmentosNoSeMezclan`, `NormalizeStatus_ValorDesconocido_CaeA_Activas` (cubierto a nivel servicio cuando reciba el segmento ya normalizado, y mediante controller en T-004 para el valor crudo).
  - GREEN: agregar `Task<PagedResult<CargoDto>> QueryAsync(CargoListQuery query, CancellationToken ct)` al contrato; implementar delegando en `ICargoRepository.QueryAsync` y proyectando a `CargoDto` con `MapToDto`.
- **Comando de verificación**:
  ```
  dotnet test tests/SGV.Tests/Aplicacion/Organizacion/CargoServicioConsultaTests.cs
  ```
- **Estimación**: 1.5 h — ~30 líneas de código + ~80 líneas de tests (incluye ampliar `FakeCargoRepository` con `QueryAsync` parametrizable).
- **Dependencias**: T-001.
- **Riesgos específicos**: medio. `FakeCargoRepository` debe evolucionar para responder a `QueryAsync`; si se olvida, los tests de segmento quedan en verde falso. Mitigado por los asserts de no-mezcla explícitos en `QueryAsync_SegmentosNoSeMezclan`.

### T-003 — `ICargoRepository.QueryAsync` + `CargoRepository.QueryAsync` + tests persistencia MySQL

- **Capa(s)**: Infraestructura / Tests.
- **Archivos a tocar/crear**:
  - `src/SGV.Aplicacion/Organizacion/Consultas/ICargoRepository.cs` (modificar: declarar `QueryAsync`).
  - `src/SGV.Infraestructura/Persistencia/Repositorios/CargoRepository.cs` (modificar: implementar `QueryAsync` con predicado binario e `Include(NivelCargo)`).
  - `tests/SGV.Tests/Persistencia/CargoRepositoryTests.cs` (modificar: agregar `[MySqlFact]` para segmento y no-mezcla).
- **Requisitos cubiertos**: REQ-CM-01, REQ-CM-03, REQ-CM-04 (metadatos y predicado).
- **Estrategia TDD**:
  - RED: `QueryAsync_MySql_SegmentoEliminadas_RetornaSoloEliminados`, `QueryAsync_MySql_SegmentoActivas_NoIncluyeEliminadas`, `QueryAsync_MySql_SegmentosNoSeMezclan`, `QueryAsync_MySql_ActivaYEliminada_MismoCodigo_RetornaAmbasEnDistintosSegmentos`, `QueryAsync_MySql_Paginacion_TotalCountProvieneDelRepositorio`.
  - GREEN: implementar `QueryAsync` replicando el predicado de `UnidadOrganizativaRepository.QueryAsync`: `segmento == Activas ? (IsActive && !IsDeleted) : (!IsActive && IsDeleted)`, `Include(NivelCargo)`, `Count()` antes de `Skip/Take`, `OrderBy(Codigo)`.
- **Comando de verificación**:
  ```
  dotnet test tests/SGV.Tests/Persistencia/CargoRepositoryTests.cs
  dotnet test tests/SGV.Tests/Persistencia/MySqlFactAttribute.cs
  ```
  (en entorno con MySQL disponible; sin MySQL los `[MySqlFact]` se skipean limpio, verificado en AGENTS.md).
- **Estimación**: 2 h — ~50 líneas de código + ~120 líneas de tests con `[MySqlFact]`.
- **Dependencias**: T-002.
- **Riesgos específicos**: medio. Los tests MySQL dependen de una base `sgv_test` accesible; el bootstrap es automático pero requiere MySQL 8 local. Si los tests caen al stub, hay que documentarlo en apply-progress.

### T-004 — `CargosController.GetConsulta` (`GET /api/v1/cargos/consulta`) + tests API + Swagger

- **Capa(s)**: API / Tests.
- **Archivos a tocar/crear**:
  - `src/SGV.Api/Controllers/CargosController.cs` (modificar: agregar `HttpGet("consulta")` con parámetros `page`, `pageSize`, `search`, `sort`, `status` y `[ProducesResponseType]`).
  - `tests/SGV.Tests/Api/CargosControllerTests.cs` (modificar: agregar escenarios `GET_consulta_status_eliminadas_RetornaSoloEliminadas`, `GET_consulta_status_invalido_CaeA_Activas`, `GET_consulta_sinStatus_RetornaActivas`, `GET_consulta_DocumentadoEnSwagger`).
  - `tests/SGV.Tests/Api/SwaggerConfigurationTests.cs` (modificar: asegurar que `/api/v1/cargos/consulta` queda documentado y que `PATCH /reactivar` sigue visible).
  - `tests/SGV.Tests/Api/ApiWebApplicationFactory.cs` (modificar: ampliar `FakeCargoServicio.QueryAsync`).
- **Requisitos cubiertos**: REQ-CM-01, REQ-CM-02, REQ-SRA-01.
- **Estrategia TDD**:
  - RED: cuatro `[Fact]` en `CargosControllerTests` y un escenario adicional en `SwaggerConfigurationTests`.
  - GREEN: implementar el endpoint normalizando `status`: `status == "eliminadas"` → `Eliminadas`, caso contrario → `Activas`. Documentar el query param y los response types 200/401; mantener intactos `GET /api/v1/cargos`, `POST`, `PUT`, `DELETE`, `PATCH /reactivar` y el subrecurso skills.
- **Comando de verificación**:
  ```
  dotnet test tests/SGV.Tests/Api/CargosControllerTests.cs
  dotnet test tests/SGV.Tests/Api/SwaggerConfigurationTests.cs
  ```
- **Estimación**: 2 h — ~40 líneas de código (controller) + ~80 líneas de tests.
- **Dependencias**: T-002, T-003.
- **Riesgos específicos**: medio. Si el fake `FakeCargoServicio` no aprende `QueryAsync`, los tests pueden pasar al endpoint nuevo y devolver 500 por `NotImplementedException`. Mitigado escribiendo los RED primero.

### T-005 — `ICargoApiClient.QueryAsync` + `ICargoApiClient.ReactivateAsync` + tests cliente web

- **Capa(s)**: Web (Integration) / Tests.
- **Archivos a tocar/crear**:
  - `src/SGV.Web/Integration/Organizacion/ICargoApiClient.cs` (modificar: agregar `QueryAsync(CargoListQuery)` y `ReactivateAsync`).
  - `src/SGV.Web/Integration/Organizacion/CargoApiClient.cs` (modificar: implementar `QueryAsync` con `BuildQueryUri` que serialice `status`, e implementar `ReactivateAsync` mapeando `CargoCommandResult`).
  - `src/SGV.Web/Integration/Organizacion/CargoListItemViewModel.cs` (modificar: ampliar `CargoListQuery` con `Status`).
  - `tests/SGV.Tests/Web/Cargo/CargoApiClientTests.cs` (modificar: agregar `QueryAsync_BuildsStatusQuery` y `ReactivateAsync_MapsConflict`).
  - `tests/SGV.Tests/Web/Cargo/FakeCargoApiClient.cs` (modificar: agregar `QueryAsync` configurable por query y `ReactivateAsync` configurable).
- **Requisitos cubiertos**: REQ-CM-01, REQ-CW-01, REQ-CW-03.
- **Estrategia TDD**:
  - RED: `QueryAsync_WithStatusEliminadas_SerializesStatusInUri`, `ReactivateAsync_OnConflict_ReturnsConflictResult`.
  - GREEN: implementar `QueryAsync` con builder de URI idéntico al de `UnidadOrganizativaApiClient.BuildQueryUri`, y `ReactivateAsync` que llama `PatchAsync` y mapea respuestas con `ToCommandResultAsync` (reutilizar lógica de `CargoApiClient`).
- **Comando de verificación**:
  ```
  dotnet test tests/SGV.Tests/Web/Cargo/CargoApiClientTests.cs
  ```
- **Estimación**: 1.5 h — ~60 líneas de código + ~50 líneas de tests.
- **Dependencias**: T-004.
- **Riesgos específicos**: bajo. Es replicación del patrón UO ya validado.

### T-006 — `IndexModel` (segmento, normalización, OnPostReactivateAsync, TempData) + tests web

- **Capa(s)**: Web (Pages) / Tests.
- **Archivos a tocar/crear**:
  - `src/SGV.Web/Pages/Organizacion/Cargos/Index.cshtml.cs` (modificar: agregar `Segmento`, `IsDeletedView`, `DeletedView = "eliminadas"`, `LastDeletedId`, `HasLastDeleted`, `NormalizeSegmento`, `OnPostReactivateAsync`, helpers `BuildDetailsRouteValues`, `BuildEditRouteValues` con `status`, reescribir `LoadAsync` para usar `QueryAsync` y remover el `GetAllAsync` + paginación en memoria).
  - `tests/SGV.Tests/Web/Cargo/CargoIndexPageTests.cs` (modificar: añadir tests de segmento, reactivate, TempData).
- **Requisitos cubiertos**: REQ-CW-01, REQ-CW-02, REQ-CW-03, REQ-CW-04, REQ-CW-06, REQ-CM-04.
- **Estrategia TDD**:
  - RED: `Index_Default_MuestraVistaActivas`, `Index_StatusEliminadas_MuestraToggleActivoEnEliminadas`, `Index_PostReactivate_Exito_RedirigeAActivas`, `Index_PostReactivate_Falla_ConservaSegmentoEliminadas`, `Index_PostDelete_AlmacenaLastDeletedId_PermiteReactivarEnBanner`, `Index_GetQuery_SinStatus_CaeA_Activas`.
  - GREEN: implementar `Segmento`, `IsDeletedView`, `OnPostReactivateAsync` que devuelve redirect a `Index(p=1)` en éxito (sin `status=eliminadas`) y a `Index(status=eliminadas)` en conflicto (404/409). `OnPostDeleteAsync` debe seguir guardando `LastDeletedId` cuando la baja es exitosa. Reescribir `LoadAsync` para invocar `QueryAsync(new CargoListQuery(CurrentPage, DefaultPageSize, Search, Sort, Status: Segmento))` y mapear `PagedResult.Items` a `CargoListItemViewModel`.
- **Comando de verificación**:
  ```
  dotnet test tests/SGV.Tests/Web/Cargo/CargoIndexPageTests.cs
  ```
- **Estimación**: 2 h — ~120 líneas de código (refactor importante de `LoadAsync`) + ~150 líneas de tests.
- **Dependencias**: T-005.
- **Riesgos específicos**: alto. Es el refactor más sensible: rompe la paginación en memoria y el contrato `GetAllAsync`. Mitigado por los tests RED que cubren el comportamiento anterior (carga inicial) y los nuevos.

### T-007 — `Index.cshtml` (toggle, hidden status, render condicional, CTA, contexto) + tests web

- **Capa(s)**: Web (Pages) / Tests.
- **Archivos a tocar/crear**:
  - `src/SGV.Web/Pages/Organizacion/Cargos/Index.cshtml` (modificar: agregar toggle Activas/Eliminadas, hidden `status` en GET y POST, render condicional de acciones por fila, CTA `LastDeletedId` solo en activas, preservar `status` en orden/paginación, ajustar título y empty state según segmento).
  - `tests/SGV.Tests/Web/Cargo/CargoIndexPageTests.cs` (modificar: añadir asserts de presencia/ausencia de elementos por segmento).
- **Requisitos cubiertos**: REQ-CW-01, REQ-CW-02, REQ-CW-04, REQ-CW-05, REQ-CW-06.
- **Estrategia TDD**:
  - RED: `Index_StatusEliminadas_OcultaDetalleEditarEliminar`, `Index_StatusEliminadas_MuestraBotonReactivar`, `Index_StatusEliminadas_OcultaCtaLastDeleted`, `Index_Activas_MuestraCtaLastDeleted_CuandoTempDataLoTiene`.
  - GREEN: copiar el patrón de `UnidadesOrganizativas/Index.cshtml`: toggle con `Model.IsDeletedView`, formularios POST con `name="status" type="hidden"`, columna de acciones con ramas por segmento, banner con CTA condicional.
- **Comando de verificación**:
  ```
  dotnet test tests/SGV.Tests/Web/Cargo/CargoIndexPageTests.cs
  ```
- **Estimación**: 1.5 h — ~80 líneas modificadas/agregadas + ~50 líneas de tests.
- **Dependencias**: T-006.
- **Riesgos específicos**: medio. Render condicional容易 olvidar un `status` en un link/oculto. Mitigado con assert específicos por segmento y revisar manualmente la paginación.

### T-008 — `cargos-index.js` (wire `data-cargo-reactivate-*` con SweetAlert2) + tests JS

- **Capa(s)**: Web (JS) / Tests.
- **Archivos a tocar/crear**:
  - `src/SGV.Web/wwwroot/js/pages/cargos-index.js` (modificar: agregar `wireCargoReactivateConfirmation(root, swal)` y exportarlo).
  - `tests/SGV.Tests/Web/Cargo/CargoIndexPageTests.cs` (modificar: añadir harness JS paralelo al de delete, `ReactivateConfirmationScript_WhenCancelled_DoesNotSubmitForm`, `ReactivateConfirmationScript_WhenConfirmed_SubmitsFormOnce`).
- **Requisitos cubiertos**: REQ-CW-05.
- **Estrategia TDD**:
  - RED: dos `[Fact]` que ejecutan un harness Node.js análogo al de delete, asserting `submitCount`, `preventDefaultCalled`, textos del modal.
  - GREEN: replicar `wireUnidadOrganizativaReactivateConfirmation` cambiando prefijos de `data-uo-*` a `data-cargo-*` y textos al español de Cargos; exponer `wireCargoReactivateConfirmation` en `module.exports`.
- **Comando de verificación**:
  ```
  dotnet test tests/SGV.Tests/Web/Cargo/CargoIndexPageTests.cs
  bun run build
  ```
- **Estimación**: 1 h — ~40 líneas de código JS + ~60 líneas de tests con harness.
- **Dependencias**: T-007.
- **Riesgos específicos**: bajo. Mismo patrón UO ya validado. Si `node` no está disponible en la máquina del revisor, el harness falla con error claro.

### T-009 — Cerrar spec Swagger y verificación final

- **Capa(s)**: API / Tests / Docs.
- **Archivos a tocar/crear**:
  - `tests/SGV.Tests/Api/SwaggerConfigurationTests.cs` (verificar que `consulta` y `PATCH /reactivar` quedan en la documentación efectiva).
  - `openspec/changes/cargos-filtro-activos-eliminados/apply-progress.md` (crear al inicio de apply; completar al final con la trazabilidad TDD).
  - `openspec/changes/cargos-filtro-activos-eliminados/verify-report.md` (crear al cierre de cada slice).
- **Requisitos cubiertos**: REQ-SRA-01 (cierre), verificación integral de REQ-CM-01..04, REQ-CW-01..06.
- **Estrategia TDD**:
  - No es código de producción nuevo; es la barrera de aceptación del slice.
  - RED: ejecutar `dotnet test SGV.slnx` y `bun run build` debe pasar limpio.
  - GREEN: agregar asserts específicos en `SwaggerConfigurationTests` para `/api/v1/cargos/consulta` y `PATCH /reactivar` si no existen.
  - REFACTOR: actualizar `apply-progress.md` y `verify-report.md`.
- **Comando de verificación**:
  ```
  dotnet build SGV.slnx
  dotnet test SGV.slnx
  bun run build
  ```
- **Estimación**: 1 h — ~20 líneas de asserts + documentación.
- **Dependencias**: T-001..T-008.
- **Riesgos específicos**: bajo. Es cierre de verificación.

## 4. Plan de entrega

### 4.1 Estimación total

| Concepto | Estimación |
|---|---|
| Total tareas | 9 |
| Horas totales | ~13 h |
| Líneas estimadas (código + tests + assets) | ~880 |
| Riesgo de presupuesto 400 líneas | Alto |

### 4.2 Estrategia recomendada: chained-pr (stacked-to-main)

El cambio cruza 4 capas (Aplicación, Infraestructura, API, Web/JS) con tests en 4 suites distintas (`Aplicacion/Organizacion`, `Persistencia`, `Api`, `Web/Cargo`) y un activo JS. Una sola PR superaría claramente el presupuesto de 400 líneas.

**Recomendación: chained-pr con dos slices**, cada uno mergeable a `main` y verificable de forma autónoma, siguiendo el patrón ya validado para Unidades Organizativas (`openspec/changes/archive/2026-06-29-reactivar-y-filtrar-unidades-organizativas-eliminadas/tasks.md`):

- **Slice 1 — Backend segmentado + API documentada** (T-001 a T-004):
  - PR base: `main`.
  - Entrega: enum + query, repositorio segmentado MySQL, controller `consulta` con Swagger.
  - Verificación: `dotnet test SGV.Tests/Aplicacion/Organizacion` + `dotnet test SGV.Tests/Persistencia` + `dotnet test SGV.Tests/Api`.
  - Rollback: revertir el PR borra el endpoint nuevo y deja el listado como estaba; no afecta datos ni `GET /api/v1/cargos` legacy.
  - Líneas estimadas: ~470 (código + tests MySQL + tests API).
  - Commits sugeridos:
    1. `feat(cargos): introduce cargo query segment enum and dto`
    2. `feat(cargos): add paginated query service for active/deleted segments`
    3. `feat(cargos): add segmented cargo repository query`
    4. `feat(cargos-api): expose paginated consulta endpoint and swagger docs`
- **Slice 2 — Cliente web + Razor + JS + tests web** (T-005 a T-008):
  - PR base: `main` (toma el código de Slice 1 ya mergeado).
  - Entrega: cliente HTTP con `QueryAsync`/`ReactivateAsync`, `IndexModel` con segmento y reactivate, `Index.cshtml` con toggle y acciones contextuales, `cargos-index.js` con confirmación de reactivación.
  - Verificación: `dotnet test SGV.Tests/Web/Cargo` + `bun run build`.
  - Rollback: revertir el PR vuelve al listado activo hardcodeado sin perder el endpoint backend (que queda subutilizado hasta un PR siguiente, pero no rompe nada).
  - Líneas estimadas: ~390.
  - Commits sugeridos:
    1. `feat(cargos-web): extend api client with query and reactivate`
    2. `feat(cargos-web): rewrite index page model with segment and reactivate handler`
    3. `feat(cargos-web): render actives/deleted toggle and contextual actions`
    4. `feat(cargos-web): wire reactivate confirmation in cargos-index.js`
- **Slice 3 — Cierre de verificación y Swagger** (T-009): puede vivir como commit dentro de Slice 2 o como PR de cierre según preferencia.

### 4.3 Estrategia de cadena

`stacked-to-main` por defecto: cada slice mergea a `main` en orden. Es el patrón más simple para este equipo y permite rollback independiente. Si en `sdd-apply` el equipo decide mantener todo en una sola feature branch con un PR de integración, el orquestador puede conmutar a `feature-branch-chain`.

## 5. Review Workload Forecast

| Field | Value |
|---|-------|
| Estimated changed lines | ~880 |
| Estimated files touched | ~18 |
| Estimated tasks | 9 |
| 400-line budget risk | high |
| Chained PRs recommended | true |
| Decision needed before apply | true |

Decision needed before apply: Yes
Chained PRs recommended: Yes
Chain strategy: stacked-to-main
400-line budget risk: High

`estimated_changed_lines` cubre: código de producción (T-001..T-008 ≈ 430 LOC), tests (T-002..T-008 ≈ 660 LOC), JS (T-008 ≈ 40 LOC), activos Razor (T-007 ≈ 80 LOC) y documentación de Swagger / `xml` docs en DTOs. Margen de error ±15 %.

`estimated_files_touched` ≈ 18 porque incluye: `CargoListQuery.cs` (nuevo), `ICargoServicioConsulta.cs`, `CargoServicioConsulta.cs`, `ICargoRepository.cs`, `CargoRepository.cs`, `CargosController.cs`, `ICargoApiClient.cs`, `CargoApiClient.cs`, `CargoListItemViewModel.cs`, `Index.cshtml.cs`, `Index.cshtml`, `cargos-index.js`, `CargoListQueryTests.cs` (nuevo), `CargoServicioConsultaTests.cs`, `CargoRepositoryTests.cs`, `CargosControllerTests.cs`, `SwaggerConfigurationTests.cs`, `ApiWebApplicationFactory.cs`, `FakeCargoApiClient.cs`, `CargoApiClientTests.cs`, `CargoIndexPageTests.cs`, `apply-progress.md`, `verify-report.md`.

`decision_needed_before_apply` queda en `true` porque la estrategia `ask-always` del cambio exige confirmación explícita de la estrategia de cadena antes de empezar a aplicar (slice único vs chained, stacked-to-main vs feature-branch-chain).

## 6. Checklist pre-apply

- [ ] `dotnet restore` ejecutado limpio.
- [ ] `dotnet build SGV.slnx` sin errores ni warnings nuevos.
- [ ] `dotnet test SGV.Tests/Aplicacion/Organizacion/CargoServicioConsultaTests.cs` en verde.
- [ ] `dotnet test SGV.Tests/Persistencia/CargoRepositoryTests.cs` en verde (con MySQL disponible; en su defecto, los `[MySqlFact]` se skipean sin romper la suite).
- [ ] `dotnet test SGV.Tests/Api/CargosControllerTests.cs` y `SwaggerConfigurationTests.cs` en verde.
- [ ] `dotnet test SGV.Tests/Web/Cargo/CargoIndexPageTests.cs` en verde.
- [ ] `dotnet test SGV.slnx` completo en verde (sin regresiones en Unidades Organizativas, Puestos, Habilidades, etc.).
- [ ] `bun run build` en `src/SGV.Web` en verde.
- [ ] Swagger expone `GET /api/v1/cargos/consulta` y mantiene `PATCH /api/v1/cargos/{id}/reactivar`.
- [ ] `GET /api/v1/cargos` legacy sigue respondiendo solo activos (no regresión).
- [ ] No hay conflictos locales ni archivos sin stagear.
- [ ] No se commiteó ningún secreto ni connection string productiva.
- [ ] Mensajes de commit en conventional commits sin atribución a IA.
- [ ] Estrategia de cadena confirmada por el equipo antes del primer slice (stacked-to-main vs feature-branch-chain).
- [ ] `openspec/changes/cargos-filtro-activos-eliminados/apply-progress.md` inicializado con la trazabilidad TDD.