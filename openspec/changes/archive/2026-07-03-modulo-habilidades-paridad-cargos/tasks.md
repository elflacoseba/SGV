# Tasks: módulo de Habilidades en SGV.Web con paridad completa con Cargos

**Change**: `modulo-habilidades-paridad-cargos`
**Proposal**: `openspec/changes/modulo-habilidades-paridad-cargos/proposal.md`
**Design**: `openspec/changes/modulo-habilidades-paridad-cargos/design.md`
**Exploración previa**: `openspec/changes/implementar-modulo-habilidades-frontend/exploration.md`
**Specs**: `specs/habilidad-management/`, `specs/habilidad-web-listado-detalle-baja/`, `specs/habilidad-web-crear-editar/`, `specs/sgv-web-shell/`, `specs/sgv-readonly-api/`

## Resumen ejecutivo

Este change replica en `SGV.Web` el patrón probado de `Cargos` sobre el catálogo maestro de `Habilidades` sin introducir asignaciones `habilidad↔cargo` ni `habilidad↔persona`. Cubre backend nuevo mínimo (`/skills/consulta`, `/niveles-habilidad`, auth/roles), cliente HTTP tipado, sidebar, páginas Razor y pruebas web/backend alineadas con `strict_tdd: true`. `Habilidad` NO modela `NivelId` propio; el catálogo `/niveles-habilidad` se publica pero NO es consumido por el frontend maestro, y esa ausencia queda blindada por tests anti-drift. Plan de entrega en 3 slices con chained PRs por exceso del budget de 400 líneas.

---

## Review Workload Forecast

| Campo | Valor |
|-------|-------|
| Líneas estimadas (rango) | 1300–1900 |
| Slice 1 (backend + tests) | 350–550 |
| Slice 2 (cliente + shell + seams) | 180–280 |
| Slice 3 (Razor + JS + tests web) | 700–1000 (dividido en 3A y 3B) |
| ¿Algún slice excede 400 líneas? | Sí (Slice 3) |
| Chained PRs recommended | Sí |
| 400-line budget risk | High |
| Decision needed before apply | Sí |
| Delivery strategy | ask-on-risk (default) |
| Chain strategy | pending (decision humana antes de apply) |

```text
Decision needed before apply: Yes
Chained PRs recommended: Yes
Chain strategy: stacked-to-main|feature-branch-chain|size-exception|pending
400-line budget risk: High
```

### PR boundaries propuestos (orientativos, no impuestos)

- **PR 1 — Slice 1 / A**: `HabilidadListQuery`, `QueryAsync` en repo/servicio, `INivelHabilidadServicioConsulta` + impl, `[Authorize]` + roles en `SkillsController`, `GetConsulta`, `GetNivelesHabilidad`, fix `NivelHabilidadRepository` sort `Codigo → Orden` y todos los tests backend nuevos (incluye tests anti-drift JSON: `HabilidadDto` NO expone `nivelId`).
- **PR 2 — Slice 2**: `IHabilidadApiClient` + `HabilidadApiClient`, VMs web (`HabilidadListItemViewModel`, `HabilidadListQuery`, `HabilidadDeleteResult`, `HabilidadInputModel`), registro DI en `Program.cs`, entrada `Habilidades` en `_Sidenav.cshtml` (submenú `Listado` + `Nueva`, icono `ti ti-star`), tests unit + seam.
- **PR 3 — Slice 3A**: `Index.cshtml(.cs)` segmentado (activas/eliminadas), baja + reactivación con SweetAlert2 (`habilidades-index.js`), test web de listado + sidenav.
- **PR 4 — Slice 3B**: `Create.cshtml(.cs)`, `Edit.cshtml(.cs)` (Codigo readonly), `Details.cshtml(.cs)`, parcial `_Form.cshtml` SIN dropdown de nivel, tests web por página incluyendo blindaje explícito anti-drift de `Nivel` en HTML.

Estrategia coherente: **stacked-to-main** si el equipo quiere PRs chicos mergeables uno a uno (recomendado para mantener review agudo por PR); **feature-branch-chain** si se quiere un tracker integrador y rollback más controlado. La elección es humana; no se impone acá.

---

## Slice 1 — Backend + tests xUnit

> Numeración: #1, #2, ... ; subtasks con #1.1, #1.2 cuando aplique.

- [x] **#1.1** Crear `src/SGV.Aplicacion/Habilidades/Consultas/Dtos/HabilidadListQuery.cs` con `HabilidadSegmentoListado { Activas=0, Eliminadas=1 }` y record `HabilidadListQuery(Page, PageSize, Search?, Sort?, Segmento = Activas)`.
  - Test: `tests/SGV.Tests/Aplicacion/Habilidades/HabilidadListQueryTests.cs` (nuevo): `Default_SegmentoEsActivas`, `PuedeConstruirQueryParaEliminadas`.
  - Verif: `dotnet build SGV.slnx` + `dotnet test SGV.slnx --filter "FullyQualifiedName~HabilidadListQueryTests"`.
  - Rollback: borrar archivo + revertir task.
- [x] **#1.2** Agregar firma `QueryAsync(search, page, pageSize, sort, segmento, ct)` a `IHabilidadRepository` (`src/SGV.Aplicacion/Habilidades/Consultas/IHabilidadRepository.cs`) — solo declaración.
  - Test: `HabilidadListQueryTests` cubre el record; compilación de `HabilidadRepository` cubre la firma.
  - Verif: `dotnet build src/SGV.Infraestructura/SGV.Infraestructura.csproj`.
  - Rollback: revertir cambios del archivo.
- [x] **#1.3** Implementar `QueryAsync` en `src/SGV.Infraestructura/Persistencia/Repositorios/HabilidadRepository.cs`: filtro por `IsActive && !IsDeleted` o `!IsActive && IsDeleted` según segmento, búsqueda en `Codigo/Nombre/Categoria/Descripcion`, `ApplySort` server-side (`codigo_asc|desc`, `nombre_asc|desc`, `categoria_asc|desc`; desconocido → `codigo_asc`), `Skip/Take` después del orden, `Include` solo si hace falta.
  - Test: `tests/SGV.Tests/Persistencia/HabilidadRepositoryTests.cs` agregar `[MySqlFact] QueryAsync_SegmentoEliminadas_ExcluyeActivas`, `QueryAsync_SortNombreDesc_AplicaAntesDePaginar`, `QueryAsync_SortDesconocido_CaeACodigoAsc`, `QueryAsync_PageSizeYNormalizacion` (estos últimos requieren extender normalización en query → mover al servicio o documentar que se valida en controller).
  - Verif: `dotnet test SGV.slnx --filter "FullyQualifiedName~HabilidadRepositoryTests"`.
  - Rollback: revertir implementación (deja la firma).
- [x] **#1.4** Crear `src/SGV.Aplicacion/Habilidades/Consultas/INivelHabilidadServicioConsulta.cs` con `ListAsync(ct)` + `GetByIdAsync(id, ct)`.
  - Test: contrato via `NivelHabilidadServicioConsultaTests` (#1.6).
  - Verif: `dotnet build SGV.slnx`.
  - Rollback: borrar archivo.
- [x] **#1.5** Crear `src/SGV.Aplicacion/Habilidades/Consultas/NivelHabilidadServicioConsulta.cs` mapeando `NivelHabilidad -> NivelHabilidadDto` (Id/Codigo/Nombre/ValorNumerico/Orden).
  - Test: `tests/SGV.Tests/Aplicacion/Habilidades/NivelHabilidadServicioConsultaTests.cs` (nuevo): `ListAsync_CuandoExistenRegistros_RetornaListaCompleta`, `ListAsync_CuandoNoExistenRegistros_RetornaListaVacia`, `GetByIdAsync_RetornaDto_CuandoExiste`, `GetByIdAsync_RetornaNull_CuandoNoExiste`.
  - Verif: `dotnet test SGV.slnx --filter "FullyQualifiedName~NivelHabilidadServicioConsultaTests"`.
  - Rollback: borrar archivos.
- [x] **#1.6** Modificar `IHabilidadServicioConsulta` para agregar `QueryAsync(HabilidadListQuery, ct)`.
  - Test: cubierto por #1.7.
  - Verif: `dotnet build SGV.slnx`.
  - Rollback: revertir interface.
- [x] **#1.7** Implementar `QueryAsync` en `HabilidadServicioConsulta` llamando al repo, devolviendo `PagedResult<HabilidadDto>` con `Page/PageSize` del input (no del repo, que NO devuelve esos campos).
  - Test: extender `tests/SGV.Tests/Aplicacion/Habilidades/HabilidadServicioConsultaTests.cs` con `QueryAsync_ConSegmentoActivas_RetornaSoloActivos`, `QueryAsync_ConSegmentoEliminadas_RetornaSoloEliminados`, `QueryAsync_SegmentosNoSeMezclan`, `QueryAsync_TotalCountProvieneDelRepositorio`, `QueryAsync_ConSortNombreDesc_OrdenaServidorAntesDePaginar`, `QueryAsync_ConSortDesconocido_CaeACodigoAsc`, `QueryAsync_PageSize_NormalizaAMaximo100` (o documentar que la normalización vive en el controller).
  - Verif: `dotnet test SGV.slnx --filter "FullyQualifiedName~HabilidadServicioConsultaTests"`.
  - Rollback: revertir el método (la firma queda en interface, falla compilación → restaurar interface también).
- [x] **#1.8** Modificar `NivelHabilidadRepository.ListAllAsync` para ordenar por `Orden` (ascendente) en lugar de `Codigo`.
  - Test: actualizar `tests/SGV.Tests/Persistencia/NivelHabilidadRepositoryTests.cs` — renombrar test existente `ListAllAsync_RetornaNivelesOrdenadosPorCodigo` → `ListAllAsync_RetornaNivelesOrdenadosPorOrden` y cambiar asserción a comparar `Orden` ascendente entre elementos consecutivos.
  - Verif: `dotnet test SGV.slnx --filter "FullyQualifiedName~NivelHabilidadRepositoryTests"`.
  - Rollback: restaurar `OrderBy(e => e.Codigo)`.
- [x] **#1.9** Agregar `[Authorize]` a nivel de controller en `src/SGV.Api/Controllers/SkillsController.cs`; agregar `[Authorize(Roles = RolesSgv.Administrador)]` a `Create`, `Update`, `Delete`, `Reactivate`; agregar `GetConsulta` (`[HttpGet("consulta")]`, query: `page`, `pageSize`, `search`, `sort`, `status` — normaliza `page<1=>1`, `pageSize<1=>20`, `pageSize>100=>100`, `status!=eliminadas=>activas`, propaga `sort` sin filtrar al servicio); agregar `GetNivelesHabilidad` (`[HttpGet]` separado, ruta registrada en `NivelesHabilidadController` o como sub-controller — preferir sub-controller paralelo a `NivelesCargoController` para evitar coupling).
  - Test: `tests/SGV.Tests/Api/SkillsControllerTests.cs` extender con: `GetConsulta_WithoutCredentials_ReturnsUnauthorized`, `GetConsulta_StatusEliminadas_RetornaSoloEliminadas`, `GetConsulta_StatusInvalido_CaeA_Activas`, `GetConsulta_SinStatus_RetornaActivas`, `GetConsulta_PropagaSortAlServicio`, `GetConsulta_SortInvalido_NoLanzaYLlegaAlServicio`, `GetConsulta_PageSizeMayorA100_NormalizaA100`, `GetConsulta_PageInvalido_NormalizaA1`, `Create_WithoutCredentials_ReturnsUnauthorized`, `Create_WithAuthenticatedNonAdmin_ReturnsForbidden`, `Update_WithAuthenticatedNonAdmin_ReturnsForbidden`, `Delete_WithAuthenticatedNonAdmin_ReturnsForbidden`, `Reactivate_WithAuthenticatedNonAdmin_ReturnsForbidden`, `Controller_HasAuthorizeAttribute`, `GetAll_JsonResponse_NoExponeNivelIdEnHabilidadDto` (asserción explícita anti-drift: la respuesta JSON de `GET /api/v1/skills` NO contiene `nivelId`, `nivelNombre` ni `NivelId`).
  - Verif: `dotnet test SGV.slnx --filter "FullyQualifiedName~SkillsControllerTests"`.
  - Rollback: revertir diff del controller; preservar tests nuevos como recordatorios para próximos passes.
- [x] **#1.10** Crear `src/SGV.Api/Controllers/NivelesHabilidadController.cs` (paralelo a `NivelesCargoController`) con `GET` → `ListAsync` y `GET {id:guid}` → `GetByIdAsync`.
  - Test: crear `tests/SGV.Tests/Api/NivelesHabilidadControllerTests.cs` con `GetAll_ReturnsOkWithDtos`, `GetById_ExistingId_ReturnsOk`, `GetById_NonExistentId_ReturnsNotFound`, `GetAll_WithoutCredentials_ReturnsUnauthorized` (heredado por `[Authorize]` global en `SkillsController`? NO: `NivelesHabilidadController` no hereda de `SkillsController`; requiere `[Authorize]` propio o moverse a controller de Skills — preferir controller separado + `[Authorize]` propio).
  - Verif: `dotnet test SGV.slnx --filter "FullyQualifiedName~NivelesHabilidadControllerTests"`.
  - Rollback: borrar archivos.
- [x] **#1.11** Verificar discoverability Swagger: `tests/SGV.Tests/Api/SwaggerConfigurationTests.cs` ya cubre `/api/v1/skills`; agregar tests para `/api/v1/skills/consulta` y `/api/v1/niveles-habilidad`, replicando el patrón de `ConsultaEndpoint_StatusParameter_DocumentaValoresActivasYEliminadas` (con `activas|eliminadas`).
  - Test: nuevo `DiscoverSkillsConsultaEndpoint_Test` y `DiscoverNivelesHabilidadEndpoint_Test` en `SwaggerConfigurationTests.cs`.
  - Verif: `dotnet test SGV.slnx --filter "FullyQualifiedName~SwaggerConfigurationTests"`.
  - Rollback: revertir cambios del test.

> **Out of scope en Slice 1**: ninguna migración nueva (diseño declara que no se requiere); si `EXPLAIN` revela índice faltante, dividir en slice reversible separado.

---

## Slice 2 — Cliente HTTP tipado + shell + navegación

- [x] **#2.1** Crear `src/SGV.Web/Integration/Habilidades/HabilidadListItemViewModel.cs` con `HabilidadListItemViewModel(Id, Codigo, Nombre, Descripcion, Categoria)`, `HabilidadListQuery(Page, PageSize, Search, Sort, Status)`, `HabilidadDeleteResult(Succeeded, StatusCode, Code, Message)`.
  - Test: `tests/SGV.Tests/Web/Habilidad/HabilidadWebSeamTests.cs` (nuevo): constructores exponen todas las propiedades.
  - Verif: `dotnet test SGV.slnx --filter "FullyQualifiedName~HabilidadWebSeamTests"`.
  - Rollback: borrar archivo.
- [x] **#2.2** Crear `src/SGV.Web/Integration/Habilidades/IHabilidadApiClient.cs` con `GetAllAsync`, `GetByIdAsync`, `DeleteAsync`, `CreateAsync`, `UpdateAsync`, `GetNivelesHabilidadAsync`, `QueryAsync`, `ReactivarAsync`.
  - Test: contrato via #2.3.
  - Verif: `dotnet build SGV.slnx`.
  - Rollback: borrar archivo.
- [x] **#2.3** Crear `src/SGV.Web/Integration/Habilidades/HabilidadApiClient.cs` replicando el patrón de `CargoApiClient`: rutas `/api/v1/skills`, `/api/v1/niveles-habilidad`; builder `BuildQueryUri` con `page/pageSize/search/sort/status`; traducción HTTP a `HabilidadDeleteResult`/`HabilidadCommandResult` (400/401/404/409 → resultados tipados, 204 → éxito).
  - Test: `tests/SGV.Tests/Web/Habilidad/HabilidadApiClientTests.cs` (nuevo): replica los tests de `CargoApiClientTests` adaptados a habilidades — `GetAllAsync_Http200WithPayload_ReturnsParsedDtosAndHitsListRoute`, `GetByIdAsync_Http404_ReturnsNull`, `DeleteAsync_Http204_ReturnsSuccess`, `DeleteAsync_Http409WithProblemDetails_ReturnsFailedResult`, `QueryAsync_PasaQueryString_AlServicio`, `CreateAsync_Http400WithValidationProblemDetails_ReturnsFailureWithFieldErrors`, `ReactivarAsync_Http200_ReturnsSuccess`.
  - Verif: `dotnet test SGV.slnx --filter "FullyQualifiedName~HabilidadApiClientTests"`.
  - Rollback: borrar archivos.
- [x] **#2.4** Registrar `AddHttpClient<IHabilidadApiClient, HabilidadApiClient>` en `src/SGV.Web/Program.cs` con `BaseAddress = options.BaseUrl` + `Timeout = 10s` + `ApiBearerTokenHandler` (paridad exacta con `CargoApiClient`).
  - Test: `HabilidadWebSeamTests.ProductionRegistration_ResolvesHabilidadApiClient` (nuevo).
  - Verif: `dotnet test SGV.slnx --filter "FullyQualifiedName~HabilidadWebSeamTests"`.
  - Rollback: borrar el bloque `AddHttpClient`.
- [x] **#2.5** Extender `src/SGV.Web/Pages/Shared/Partials/_Sidenav.cshtml` con entrada colapsable `Habilidades` debajo de `Cargos`: icono `ti ti-star`, submenú `Listado` (`/organizacion/habilidades`) + `Nueva` (`/organizacion/habilidades/crear`), variable `habilidadesActive` derivada de `currentPath.StartsWithSegments("/organizacion/habilidades")`.
  - Test: `tests/SGV.Tests/Web/HabilidadWebTests.cs` (nuevo): `Get_Sidenav_WhenAuthenticated_ExposesHabilidadesModule` verifica presencia de `Habilidades`, hrefs correctos, y ausencia de placeholders no especificados (reclutamiento, vacantes, catálogos).
  - Verif: `dotnet test SGV.slnx --filter "FullyQualifiedName~HabilidadWebTests"`.
  - Rollback: borrar bloque `Habilidades` en `_Sidenav.cshtml`; revertir test.

---

## Slice 3A — Razor: Index + JS + tests de listado

- [x] **#3.1** Crear `src/SGV.Web/Pages/Organizacion/Habilidades/Index.cshtml(.cs)` con PageModel `[Authorize]` que consume `IHabilidadApiClient.QueryAsync`; toggle `activas|eliminadas` con reset de página; banner `TempData` con CTA de reactivación rápida (`LastDeletedId`); SweetAlert2 para confirmación de baja y reactivación.
  - Test: `tests/SGV.Tests/Web/Habilidad/HabilidadIndexPageTests.cs` (nuevo, replica `CargoIndexPageTests`): `Get_Index_WhenAnonymous_RedirectsToSignIn`, `Get_Index_WhenAuthenticated_RendersActiveHabilidadesTable`, `Get_Index_WhenSearchHasNoResults_ShowsEmptyState`, `Get_Index_WhenQueryFails_ShowsVisibleError`, `Post_Delete_WhenSuccessful_RedirectsPreservingFilters`, `Post_Delete_WhenConflict_RedirectsWithErrorMessage`, `Post_Reactivate_WhenSuccessful_RedirectsToActivas`, `Post_Reactivate_WhenCodigoDuplicado_ReturnsConflictAndStaysOnEliminadas`, `Get_Index_WhenSegmentoEliminadas_RendersReactivarButtonOnly`.
  - Verif: `dotnet test SGV.slnx --filter "FullyQualifiedName~HabilidadIndexPageTests"`.
  - Rollback: borrar archivos.
- [x] **#3.2** Crear `src/SGV.Web/wwwroot/js/pages/habilidades-index.js` con handlers `data-habilidad-delete-form` y `data-habilidad-reactivate-form` (paridad con `cargos-index.js`, mensajes en español, `icon: 'question'` para reactivación).
  - Test: test JS en `HabilidadIndexPageTests` que verifica (vía markup) presencia de `data-habilidad-delete-button` y `data-habilidad-reactivate-button` con mensajes esperados.
  - Verif: `dotnet test SGV.slnx --filter "FullyQualifiedName~HabilidadIndexPageTests"`.
  - Rollback: borrar archivo.
- [x] **#3.3** (Anti-drift Slice 3A) Verificar que `Index.cshtml` NO muestra `data-cargo-*` ni ningún filtro/columna relacionada con nivel.
  - Test: assert dentro de `HabilidadIndexPageTests`: `Assert.DoesNotContain("Nivel", content)` y `Assert.DoesNotContain("data-cargo-", content)`.
  - Verif: cubierto por #3.1.
  - Rollback: N/A (asserción de test).

## Slice 3B — Razor: Create / Edit / Details + _Form + tests

- [x] **#3.4** Crear `src/SGV.Web/Integration/Habilidades/HabilidadInputModel.cs` con `Codigo`, `Nombre`, `Categoria?`, `Descripcion?` (anotaciones `[Required]`/`[StringLength]` replicando longitudes del dominio).
  - Test: `HabilidadWebSeamTests` agrega `HabilidadInputModel_Defaults_CodigoEsVacioYCategoriaEsNull` + assert de longitudes.
  - Verif: `dotnet test SGV.slnx --filter "FullyQualifiedName~HabilidadWebSeamTests"`.
  - Rollback: borrar archivo.
- [x] **#3.5** Crear `src/SGV.Web/Pages/Organizacion/Habilidades/_Form.cshtml` parcial compartido para create/edit: 4 campos (`Codigo`, `Nombre`, `Categoria`, `Descripcion`). **NO incluye ningún `<select>` cuyo `name` o label contenga `Nivel`**. En edit el input `Codigo` se renderiza con `readonly` o `disabled` según `Model.IsEdit`.
  - Test: assert anti-drift centralizado (ver #3.9).
  - Verif: cubierto por #3.9.
  - Rollback: borrar archivo.
- [x] **#3.6** Crear `src/SGV.Web/Pages/Organizacion/Habilidades/Create.cshtml(.cs)` con PageModel `[Authorize]`; GET carga formulario vacío; POST con PRG a Details, mapea 409 a `ModelState["Input.Codigo"]`; manejo de `HttpRequestException`/`TaskCanceledException`/`JsonException` como error recuperable.
  - Test: `tests/SGV.Tests/Web/Habilidad/HabilidadCreatePageTests.cs` (nuevo, replica `CargoCreatePageTests`): `Get_Create_WhenAnonymous_RedirectsToSignIn`, `Get_Create_WhenAuthenticated_RendersEmptyForm` (asserta presencia de los 4 campos y ausencia de cualquier `<select>` relacionado con nivel), `Post_Create_WhenSuccessful_RedirectsToDetailsWithConfirmation`, `Post_Create_WhenCodigoDuplicado_ReturnsFieldErrorAndKeepsForm`, `Post_Create_WhenBackendUnavailable_ShowsRecoverableError`.
  - Verif: `dotnet test SGV.slnx --filter "FullyQualifiedName~HabilidadCreatePageTests"`.
  - Rollback: borrar archivos.
- [x] **#3.7** Crear `src/SGV.Web/Pages/Organizacion/Habilidades/Edit.cshtml(.cs)` con PageModel `[Authorize]`; GET precarga el form, marca `IsRecoverable` si el backend devuelve null/404/error de transporte; POST con PRG a sí mismo con TempData; `Codigo` se renderiza como `readonly` o `disabled` en `_Form.cshtml` cuando `Model.IsEdit == true`.
  - Test: `tests/SGV.Tests/Web/Habilidad/HabilidadEditPageTests.cs` (nuevo, replica `CargoEditPageTests`): `Get_Edit_WhenAnonymous_RedirectsToSignIn`, `Get_Edit_WhenAuthenticated_PrepopulatesForm`, `Get_Edit_WhenHabilidadNotFound_ShowsRecoverableState`, `Post_Edit_WhenSuccessful_RedirectsToDetailsWithConfirmation`, `Post_Edit_WhenConflictOnCodigo_ReturnsFieldError`, `EditPage_MuestraCodigoComoReadonly_O_Disabled` (asserción que busca `readonly="readonly"` o `disabled="disabled"` en el input `Input.Codigo`).
  - Verif: `dotnet test SGV.slnx --filter "FullyQualifiedName~HabilidadEditPageTests"`.
  - Rollback: borrar archivos.
- [x] **#3.8** Crear `src/SGV.Web/Pages/Organizacion/Habilidades/Details.cshtml(.cs)` con PageModel `[Authorize]`; detalle readonly de los 4 campos (`Codigo`, `Nombre`, `Categoria`, `Descripcion`); `IsNotFound` cuando `GetByIdAsync` devuelve null o falla; acción "Volver al listado" preservando `p/search/sort`.
  - Test: `tests/SGV.Tests/Web/Habilidad/HabilidadDetailsPageTests.cs` (nuevo, replica `CargoDetailsPageTests`): `Get_Details_WhenAnonymous_RedirectsToSignIn`, `Get_Details_WhenAuthenticated_ShowsHabilidadReadOnly`, `Get_Details_WhenHabilidadNotFound_ShowsNotAvailableState`.
  - Verif: `dotnet test SGV.slnx --filter "FullyQualifiedName~HabilidadDetailsPageTests"`.
  - Rollback: borrar archivos.
- [x] **#3.9** (Anti-drift blindante centralizado) Test `tests/SGV.Tests/Web/Habilidad/HabilidadAntiDriftTests.cs` que verifica para `Create`, `Edit` y `_Form` (cuando se renderiza como parte de Create/Edit): NO existe ningún `<select>` cuyo `name` contenga `Nivel`, NO existe texto visible `Nivel` (case-insensitive) en el form, NO existe input `name="Input.NivelId"`. Este test es el guardián explícito contra reintroducción del dropdown por copia del patrón Cargos.
  - Test: el archivo mismo es la asserción.
  - Verif: `dotnet test SGV.slnx --filter "FullyQualifiedName~HabilidadAntiDriftTests"`.
  - Rollback: borrar archivo de test (no afecta al producto).

---

## Verificación global del change

- **Build**: `dotnet build SGV.slnx --configuration Release`.
- **Tests**: `dotnet test SGV.slnx --configuration Release --no-build`.
- **Frontend (si se tocó `SGV.Web/wwwroot`)**: `bun install` (en `src/SGV.Web`) + `bun run build`.
- **Preflight MySQL** (issue #59): si algún `[MySqlFact]` se ve afectado por el orden `Orden`, ejecutar contra MySQL 8 local con `ConnectionStrings__SgvDatabase` configurado.
- **No se crean migraciones** en este change (diseño declara que no se requiere); si surge índice faltante, dividir en slice reversible separado.

## Out of scope (refuerzo del contrato)

- Asignaciones `habilidad↔cargo` y `habilidad↔persona` — no tocar `CargoSkillRepository`, `PersonaSkillRepository`, ni sub-recursos.
- No introducir `nivelId` en `HabilidadDto` ni en `POST/PUT /api/v1/skills`.
- No expandir `PagedResult<T>` más allá de `Items/TotalCount/Page/PageSize`.
- No migraciones nuevas (salvo índice MySQL con evidencia `EXPLAIN` — slice reversible aparte).
- No tocar `SGV.Web` para ninguna otra cosa fuera de `Pages/Organizacion/Habilidades/**`, `Integration/Habilidades/**`, `Pages/Shared/Partials/_Sidenav.cshtml` y `wwwroot/js/pages/habilidades-index.js`.

## Riesgos y mitigaciones (replicados desde el design)

| Riesgo | Mitigación |
|--------|-----------|
| Reintroducir dropdown de nivel por copia de `Cargos` | `#3.5` `_Form.cshtml` sin select de nivel + `#3.9` `HabilidadAntiDriftTests` |
| `/consulta` lenta en MySQL 8 | Sin índice extra en este slice; si profiling lo exige, slice reversible con `DROP INDEX` documentado |
| Drift entre skills y cargos | Nombres/rutas idénticos a `Cargos`; diferencias obligatorias documentadas en este tasks.md y en `design.md` |
| `403` poco claro en web | `HabilidadApiClient` traduce 403 a `HabilidadCommandResult.Failure(Conflict|Validation)` con `Detail` accionable |
| Cambio de contrato paginado compartido | Mantener `PagedResult<T>` actual; no expandir scope a Cargos en este change |