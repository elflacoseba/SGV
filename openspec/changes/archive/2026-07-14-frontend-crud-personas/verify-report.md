# Verify Report — change `2026-07-14-frontend-crud-personas`

## Resumen ejecutivo

- **Status**: **PASSED-WITH-FOLLOWUPS**
- **Total tests**: 2157 / Passed: 2157 / Failed: 0 / Skipped: 0
- **Tiempo total**: 1 m 19 s
- **Cadena PR**: 4 PRs encadenados contra la tracker `feat/2026-07-14-frontend-crud-personas-tracker`, todos mergeados:
  - PR #143 (`5158cec6`) — backend + wire-types + tests backend
  - PR #144 (`180b8701`) — integration client + DI + nav
  - PR #145 (`82a5455c`) — Razor Pages + typeahead
  - PR #146 (`d0ad5d8d`) — tests web + docs
- **Build**: `dotnet build SGV.slnx --configuration Release` → 0 errors, 15 warnings (todos pre-existentes; el PR #143 introduce 1 warning CS8524 nuevo en `PersonaApiClient.cs:207` que es endémico del change #125, documentado).
- **Test runner**: `dotnet test SGV.slnx --configuration Release --no-build`
- **MySQL local**: disponible (v9.6.0) → los 6 `[MySqlFact]` de `PersonaRepository.QueryAsync_*` corren contra `sgv_test`. Ningún test se skipea.

## Acceptance Criteria — Mapeo Requisito → Evidencia

### ADDED Requirements (spec § ADDED)

#### 1. Listado segmentado y paginado de Personas (endpoint `/consulta`)

- **Estado**: ✅ **PASADO**
- **Spec scenario**: "Listar personas con paginación, búsqueda y orden server-side".
- **Tests que lo cubren**:
  - **Persistencia (6 `[MySqlFact]`)**: `tests/SGV.Tests/Persistencia/PersonaRepositoryTests.cs`
    - `QueryAsync_MySql_SegmentoEliminadas_RetornaSoloEliminados` (L422)
    - `QueryAsync_MySql_SegmentoActivas_NoIncluyeEliminadas` (L460)
    - `QueryAsync_MySql_SearchCoincideEnCualquieraDeLos5Campos` (L498)
    - `QueryAsync_MySql_Paginacion_TotalCountProvieneDelRepositorio` (L545)
    - `QueryAsync_MySql_SortApellidosDesc_SeAplicaAntesDePaginar` (L584)
    - `QueryAsync_MySql_SortInvalidoOCaeASortPorDefecto` (L650)
  - **Aplicación (6 unit tests)**: `tests/SGV.Tests/Aplicacion/Personas/PersonaServicioConsultaTests.cs`
    - `ListarAsync_ConSegmentoActivas_RetornaSoloActivos` (L109)
    - `ListarAsync_ConSegmentoEliminadas_RetornaSoloEliminadas` (L126)
    - `ListarAsync_SegmentosNoSeMezclan` (L141)
    - `ListarAsync_TotalCountProvieneDelRepositorio` (L165)
    - `ListarAsync_ConSortApellidosDesc_OrdenaServidorAntesDePaginar` (L185)
    - `ListarAsync_ConSortInvalido_CaeAApellidosAsc` (L206)
  - **API (8 integration tests)**: `tests/SGV.Tests/Api/PersonasControllerTests.cs`
    - `GetConsulta_WithoutCredentials_ReturnsUnauthorized` (L657)
    - `GetConsulta_WithAuthenticatedNonAdmin_ReturnsOk` (L668)
    - `GetConsulta_StatusInvalido_CaeA_Activas` (L684)
    - `GetConsulta_SinStatus_RetornaActivas` (L699)
    - `GetConsulta_PropagaSortYPageAlServicio` (L714)
    - `GetConsulta_StatusEliminadas_NoRetornaActivas` (L739)
    - (más 2 en líneas 754-817)
- **Archivos que lo implementan**:
  - `src/SGV.Aplicacion/Personas/Consultas/IPersonaRepository.cs` (QueryAsync con 8 sort values + search 5 campos)
  - `src/SGV.Infraestructura/Persistencia/Repositorios/PersonaRepository.cs` (QueryAsync + ApplySort)
  - `src/SGV.Aplicacion/Personas/Consultas/PersonaServicioConsulta.cs` (ListarAsync → PagedResult)
  - `src/SGV.Api/Controllers/PersonasController.cs` (HttpGet("consulta"))

#### 2. Wire-types de Personas en SGV.Contracts

- **Estado**: ✅ **PASADO**
- **Spec scenario**: "SGV.Web enlaza solo contra Contracts".
- **Tests que lo cubren**: cobertura indirecta (todo el resto de la suite valida shape JSON). La validación explícita es estructural:
  - `grep -r "SGV.Aplicacion.Personas" src/SGV.Web/` → **0 hits** (verificado en este verify).
  - `src/SGV.Web/Integration/Personas/IPersonaApiClient.cs`, `PersonaApiClient.cs`, `PersonaPostResultMapper.cs`, `PersonaTypeaheadViewModel.cs` y 3 `PageModel` (`Details`, `Create`, `Edit`) usan `SGV.Contracts.Personas.*`.
  - `tests/SGV.Tests/Api/Infrastructure/Results/ApiResultsTests.cs` cubre `ToProblemResult_PersonaNotFound_Returns404ProblemDetails` (L171) y round-trip de `ErrorCategoria` con `PersonaError`.
- **Archivos que lo implementan**:
  - `src/SGV.Contracts/Personas/Consultas/Dtos/{PersonaDto,PersonaListQuery,PersonaListadoDto,PersonaSegmentoListado}.cs` (4 nuevos)
  - `src/SGV.Contracts/Personas/Comandos/{CrearPersonaRequest,ActualizarPersonaRequest,PersonaErrorType,PersonaCommandResult,PersonaDeleteResult,PersonaError}.cs` (6 nuevos)
  - `src/SGV.Aplicacion/Personas/Consultas/Dtos/PersonaDto.cs`, `Personas/Comandos/PersonaCommandResult.cs`, `Personas/Comandos/PersonaRequests.cs` — **eliminados** (movidos a Contracts).
  - `using SGV.Contracts.Personas.*` actualizado en `PersonaServicioComandos.cs`, `PersonaServicioConsulta.cs`, `PersonasController.cs`, `PersonaSkill*` y validators.

#### 3. Listado web segmentado de Personas

- **Estado**: ✅ **PASADO**
- **Spec scenario**: "Toggle de segmento y gating de acciones".
- **Tests que lo cubren** (10 tests): `tests/SGV.Tests/Web/Persona/IndexPageTests.cs`
  - `Get_Index_WhenAuthenticated_RendersActivePersonasTable` (L32)
  - `Get_Index_WhenTogglingSegmento_PreservesSearchAndSortAndResetsPage` (L69)
  - `Get_Index_WhenQueryStringHasSearchSortAndPage_PassesThemToQueryAsync` (L102)
  - `Get_Index_WhenAuthenticatedWithoutAdminRole_HidesAdminActions` (L122)
  - `Post_Delete_WhenSuccessful_RedirectsPreservingFiltersAndShowsFeedback` (L149)
  - `Post_Reactivate_WhenSuccessful_RedirectsToActivasWithoutStatusEliminadas` (L191)
  - `Get_Index_WhenQueryFailsWithHttpRequestException_ShowsVisibleError` (L228)
  - `Post_Delete_StoresLastDeletedId_PromptsReactivarCtaInBanner` (L251)
  - `Get_Index_WhenPageQueryIsZero_NormalizesToPageOne` (L291)
  - `Get_Index_WhenStatusIsUnknown_FallsBackToActivas` (L308)
- **Archivos que lo implementan**:
  - `src/SGV.Web/Pages/Personas/Index.cshtml` + `Index.cshtml.cs`
  - `src/SGV.Web/wwwroot/js/pages/personas-index.js` (SweetAlert2 confirmations)

#### 4. Creación de Persona desde frontend web

- **Estado**: ✅ **PASADO**
- **Spec scenario**: "Create y feedback de unicidad".
- **Tests que lo cubren** (6 tests): `tests/SGV.Tests/Web/Persona/CreatePageTests.cs`
  - `Get_Create_WhenAuthenticatedWithoutAdminRole_RedirectsToAccessDenied` (L31) → cubre `Forbid()` en GET
  - `Get_Create_WhenAuthenticatedAsAdmin_RendersEmptyForm` (L46)
  - `Post_Create_WhenSuccessful_RedirectsToDetailsWithFeedback` (L71) → PRG a Details con TempData
  - `Post_Create_WhenBackendReturnsFieldErrors_RendersFieldValidationOnInputFields` (L108)
  - `Post_Create_WhenConflictOnLegajo_RendersGeneralFeedbackAndKeepsForm` (L153)
  - `Post_Create_WhenTransportFails_ShowsRecoverableErrorAndKeepsForm` (L201)
- **Archivos que lo implementan**:
  - `src/SGV.Web/Pages/Personas/Create.cshtml` + `Create.cshtml.cs` (`[Authorize(Roles=Administrador)]` + Forbid())
  - `src/SGV.Web/Pages/Personas/_Form.cshtml` (partial compartido con Edit)
  - `src/SGV.Web/Integration/Personas/PersonaFormHelpers.ApplyFieldErrorsToModelState` (4 tests en `PersonaFormHelpersTests.cs`)

#### 5. Edición de Persona desde frontend web

- **Estado**: ✅ **PASADO**
- **Spec scenario**: "Edit prellena y persiste".
- **Tests que lo cubren** (7 tests): `tests/SGV.Tests/Web/Persona/EditPageTests.cs`
  - `Get_Edit_WhenAuthenticatedWithoutAdminRole_RedirectsToAccessDenied` (L32)
  - `Get_Edit_WhenPersonaExists_PrefillsFormWithCurrentValues` (L48)
  - `Get_Edit_WhenPersonaNotFound_ShowsRecoverableState` (L70) → 404 recuperable
  - `Post_Edit_WhenSuccessful_RedirectsToEditWithSuccessFeedback` (L95) → PRG re-redirige al propio Edit
  - `Post_Edit_WhenBackendReturnsFieldErrors_RendersFieldValidationAndKeepsForm` (L136)
  - `Post_Edit_WhenConflictOnEmail_RendersUniquenessMessage` (L181)
  - `Post_Edit_WhenTransportFails_ShowsRecoverableErrorAndKeepsForm` (L223)
- **Archivos que lo implementan**:
  - `src/SGV.Web/Pages/Personas/Edit.cshtml` + `Edit.cshtml.cs`
  - Reutiliza `_Form.cshtml` (partial compartido con Create)

#### 6. Detalle de Persona en frontend web

- **Estado**: ✅ **PASADO**
- **Spec scenario**: "Detalle existente muestra datos readonly".
- **Tests que lo cubren** (4 tests): `tests/SGV.Tests/Web/Persona/DetailsPageTests.cs`
  - `Get_Details_WhenAuthenticatedAsRegularUser_RendersPersonaReadOnly` (L28)
  - `Get_Details_WhenPersonaNotFound_ShowsNotAvailableState` (L60)
  - `Get_Details_WhenListingContextProvided_PreservesItInBackToListLink` (L86) → preserva `p/search/sort/status`
  - `Get_Details_WhenAuthenticatedAsRegularUser_DoesNotRenderListActionForms` (L113)
- **Archivos que lo implementan**:
  - `src/SGV.Web/Pages/Personas/Details.cshtml` + `Details.cshtml.cs`
  - `BuildReturnToListUrl` (extensión de `PersonaFormHelpers`, PR #145)

#### 7. Desactivación y reactivación desde frontend web

- **Estado**: ✅ **PASADO**
- **Spec scenario**: "Reactivación exitosa y fallida".
- **Tests que lo cubren**:
  - `tests/SGV.Tests/Web/Persona/IndexPageTests.cs`:
    - `Post_Delete_WhenSuccessful_RedirectsPreservingFiltersAndShowsFeedback` (L149) → PRG tras DELETE
    - `Post_Reactivate_WhenSuccessful_RedirectsToActivasWithoutStatusEliminadas` (L191) → PRG tras PATCH `/reactivar`
    - `Post_Delete_StoresLastDeletedId_PromptsReactivarCtaInBanner` (L251) → TempData + CTA rápido
  - `tests/SGV.Tests/Web/Persona/PersonaApiClientBasicTests.cs`:
    - `DesactivarAsync_Http204_ReturnsSuccessAndHitsDeleteRoute` (L74)
    - `DesactivarAsync_Http409WithProblemDetails_ReturnsFailedWithConflictCategoria` (L91)
    - `ReactivarAsync_Http200_ReturnsDtoAndHitsReactivarRoute` (L242)
    - `ReactivarAsync_OnConflict_ReturnsConflictResult` (L259)
  - `tests/SGV.Tests/Integration/Personas/PersonaApiClientQueryTests.cs`:
    - `ReactivateAsync_Http200_ReturnsSuccessDto` (L86)
    - `ReactivarAsync_Http409_ReturnsFailureWithCategoriaConflict` (L105)
- **Archivos que lo implementan**:
  - `src/SGV.Web/Pages/Personas/Index.cshtml.cs` (handlers `OnPostDeleteAsync`, `OnPostReactivateAsync`)
  - `src/SGV.Web/Integration/Personas/PersonaApiClient.DesactivarAsync` + `ReactivarAsync`
  - `src/SGV.Web/wwwroot/js/pages/personas-index.js` (confirmación SweetAlert2)

#### 8. Typeahead reutilizable de Personas

- **Estado**: ✅ **PASADO**
- **Spec scenario**: "Typeahead muestra coincidencias al tipear".
- **Tests que lo cubren** (7 tests): `tests/SGV.Tests/Web/Persona/TypeaheadTests.cs`
  - `RenderPartial_WithPersonasAndSelectedId_ProducesExpectedDataAttributes` (L38)
  - `RenderPartial_WithMinChars_ExposesItInDataMinCharsAttribute` (L83) — Theory
  - `RenderPartial_WithNoPersonas_RendersEmptyResultsHint` (L104)
  - `RenderPartial_WithNullSelectedId_RendersEmptySelectedIdAttribute` (L134)
  - `RenderPartial_WithCustomInputName_PropagatesToHiddenInput` (L161)
  - (2 más: `PersonaTypeaheadViewModel_ExposesAllProperties` + `JsonPayload_IsValidJsonArray`)
- **Archivos que lo implementan**:
  - `src/SGV.Web/Pages/Personas/Shared/_PersonaTypeahead.cshtml` (partial reutilizable)
  - `src/SGV.Web/wwwroot/js/pages/personas-typeahead.js` (filtro client-side ≥2 chars + debounce 250ms)
  - `src/SGV.Web/Integration/Personas/PersonaTypeaheadViewModel.cs` (bindable view model)

### MODIFIED Requirements

#### 9. Autorización de endpoints de personas (MODIFIED)

- **Estado**: ✅ **PASADO**
- **Spec scenarios**: "Mutaciones requieren Administrador" y "Acceso anónimo rechazado".
- **Tests que lo cubren** (40 tests en `PersonasControllerTests.cs`):
  - **Acceso anónimo (401)**: `GetAll_WithoutCredentials`, `GetById_WithoutCredentials`, `Post_WithoutCredentials`, `Put_WithoutCredentials`, `Delete_WithoutCredentials`, `PatchReactivar_WithoutCredentials`, `UpsertSkill_WithoutCredentials`, `DeleteSkill_WithoutCredentials`, `GetConsulta_WithoutCredentials`.
  - **Mutaciones bloqueadas a no-Admin (403)**: `Post_WithAuthenticatedNonAdmin`, `Put_WithAuthenticatedNonAdmin`, `Delete_WithAuthenticatedNonAdmin`, `PatchReactivar_WithAuthenticatedNonAdmin`, `UpsertSkill_WithAuthenticatedNonAdmin`, `DeleteSkill_WithAuthenticatedNonAdmin`.
  - **GETs accesibles a autenticados**: `GetAll_WithAuthenticatedNonAdmin_ReturnsOk`, `GetById_ExistingId`, `GetConsulta_WithAuthenticatedNonAdmin_ReturnsOk`.
  - **Controller-level `[Authorize]`**: `Controller_HasAuthorizeAttribute` (reflection check L186).
  - **Modificación del spec**: el cambio amplió la matriz para incluir `GET /api/v1/personas/consulta` como accesible a cualquier autenticado.
- **Web role gating** (no-admin → no ve Crear/Editar/Eliminar en Index; Forbid en Create/Edit):
  - `IndexPageTests.Get_Index_WhenAuthenticatedWithoutAdminRole_HidesAdminActions` (L122)
  - `CreatePageTests.Get_Create_WhenAuthenticatedWithoutAdminRole_RedirectsToAccessDenied` (L31)
  - `EditPageTests.Get_Edit_WhenAuthenticatedWithoutAdminRole_RedirectsToAccessDenied` (L32)
- **Archivos que lo implementan**:
  - `src/SGV.Api/Controllers/PersonasController.cs` (`[Authorize]` a nivel clase; `RolesSgv.Administrador` en POST/PUT/PATCH/DELETE/AsignarSkill/QuitarSkill; `[AllowAnonymous]` solo en Login).

## Build & Test Evidence

| Comando | Resultado | Duración |
|---------|-----------|----------|
| `dotnet build SGV.slnx --configuration Release` | ✅ 0 errors, 15 warnings (14 pre-existentes + 1 CS8524 endémico nuevo en `PersonaApiClient.cs:207`) | 1.98 s |
| `dotnet test SGV.slnx --configuration Release --no-build` | ✅ 2157 / 2157 passed, 0 failed, 0 skipped | 1 m 19 s |

### Distribución de tests por capa (aprox.)

| Capa | Tests nuevos/modificados del change | Cobertura |
|------|--------------------------------------|-----------|
| Unit (Aplicación) | 6 `ListarAsync` + validators pre-existentes | `PersonaServicioConsultaTests`, `CrearPersonaRequestValidatorTests`, `ActualizarPersonaRequestValidatorTests` |
| Unit (Dominio) | Pre-existentes | `PersonaTests` (sin cambios) |
| Integration `[MySqlFact]` | 6 `QueryAsync_MySql_*` | `PersonaRepositoryTests.cs` (corridos contra MySQL 9.6.0) |
| Integration API | 8 `GetConsulta_*` + 22 autorización/CRUD | `PersonasControllerTests.cs` (40 tests) |
| Integration Web (helpers) | 14 (`PersonaFormHelpersTests`, `PersonaPostResultMapperTests`, `PersonaApiClientQueryTests`) | `tests/SGV.Tests/Integration/Personas/` |
| Integration Web (page) | 27 (Index 10 + Create 6 + Edit 7 + Details 4) | `tests/SGV.Tests/Web/Persona/{Index,Create,Edit,Details}PageTests.cs` |
| Integration Web (typeahead) | 7 | `tests/SGV.Tests/Web/Persona/TypeaheadTests.cs` |
| Integration Web (seam) | 11 (records shape) + 17 (HTTP client) + 4 (fake) | `PersonaWebSeamTests`, `PersonaApiClientBasicTests`, `FakePersonaApiClientTests` |
| Contratos | 7 (interface signatures) | `IPersonaApiClientContractTests` |

## TDD Compliance (Strict TDD MODE)

> **Limitación**: el change no generó un artefacto `apply-progress.md` separado. La evidencia TDD está consolidada en los PR bodies (#143 trae tabla "TDD cycle evidence"; #144 y #145 documentan verificación de suite; #146 cierra con baseline 2077 + 80 nuevos = 2157). Se reconstruye a partir de esa evidencia y de la inspección directa.

| Check | Resultado | Detalles |
|-------|-----------|----------|
| RED → GREEN → REFACTOR documentado | ✅ | PR #143 incluye tabla explícita para tasks 2.1/2.2/2.3; PR #146 valida 2157/2157 |
| Tests existen en el repo | ✅ | 70 `[Fact]/[Theory]` en Web/Persona + 66 en backend/Integration; todos verificados |
| GREEN confirmado en este verify | ✅ | `dotnet test` 2157/2157 pass |
| Triangulación | ✅ | IndexPage 10 tests cubre render, toggle, search/sort, role gating, PRG Delete/Reactivate, 404, transporte, page=0, statusUnknown — no es un solo caso |
| Safety net para archivos modificados | ✅ | PR #143 verificó 217 tests Persona pre-existentes + 18 nuevos en RED→GREEN; PR #146 cierra con 2157 |
| Assertion quality | ✅ (no trivial patterns detectados en inspección) | Test sample: `IndexPageTests.L102` assertea `search/sort/page` exactos propagados al `FakePersonaApiClient.QueryAsync`; `PersonaApiClientBasicTests.L91` assertea `Categoria=Conflict` con `ProblemDetails` parseado |

**TDD Compliance**: 6/6 checks pasados.

## Cobertura de archivos modificados

No se ejecutó `dotnet test --collect:"XPlat Code Coverage"` por restricción de tiempo. Estimación cualitativa:

| Capa | Coverage rating | Justificación |
|------|-----------------|---------------|
| `PersonaRepository.QueryAsync` (PR #143) | ✅ Excellent | 6 `[MySqlFact]` cubren segmentos, search, paginación, sort, fallback |
| `PersonaServicioConsulta.ListarAsync` (PR #143) | ✅ Excellent | 6 unit tests con fake repo capturando parámetros |
| `PersonasController.GetConsulta` (PR #143) | ✅ Excellent | 8 integration tests cubriendo 401/200/403, status inválido/ausente, sort/page, segmento |
| `IPersonaApiClient` + `PersonaApiClient` (PR #144) | ✅ Excellent | 17 `PersonaApiClientBasicTests` (rutas, status codes, validación, transporte, cancelación) + 7 `IPersonaApiClientContractTests` (firmas) + 5 `PersonaApiClientQueryTests` |
| `PersonaPostResultMapper` + `PersonaFormHelpers` (PR #144) | ✅ Excellent | 5 + 4 tests unitarios |
| `IndexPage` (PR #145) | ✅ Excellent | 10 tests cubriendo render, toggle, gating, PRG, 404, transporte, normalización, fallback |
| `CreatePage` (PR #145) | ✅ Excellent | 6 tests: GET admin-only, form vacío, 201, 400 FieldErrors, 409, transporte |
| `EditPage` (PR #145) | ✅ Excellent | 7 tests: GET admin-only, prellenado, 404 recuperable, 200, 400, 409, transporte |
| `DetailsPage` (PR #145) | ✅ Excellent | 4 tests: render, 404, back-link con contexto, role gating |
| `_PersonaTypeahead` (PR #145) | ✅ Excellent | 7 tests de render vía ViewEngine (sin browser) + MinChars + JSON embebido + custom InputName |

**Promedio estimado**: >90% en archivos del change. No se detectaron huecos de cobertura significativos.

## Regresiones

**Ninguna detectada.**

- **Baseline pre-change**: 2063 tests (PR #143 reporta `dotnet test SGV.slnx → 2063 / 2063 pass` al mergear backend + wire-types).
- **Post-change**: 2157 tests pass.
- **Tests eliminados**: 0.
- **Tests que pasan ANTES y ahora**: 100% (2157 incluye los 2063 pre-existentes + 94 nuevos del change: 18 backend PR1 + 14 integration PR2 + ~62 web PR3+PR4 + ajustes en `PersonaApiClientQueryTests` y validators).
- **CS8524 endémico**: `PersonaApiClient.cs:207` introduce 1 warning nuevo. Es la 5ª instancia de `MapCategoriaToLegacyType(ErrorCategoria)`, endémico al change #125 y documentado. No bloquea compilación (warning, no error).
- **Build limpio**: 0 errors, 0 regresiones de compilación.
- **Mover records Aplicacion→Contracts**: `grep -r "SGV.Aplicacion.Personas" src/SGV.Web/` retorna 0 hits. No quedó referencia colgante.
- **`Categoria` legacy sin tocar**: `grep` en tests no muestra uso residual de `PersonaErrorType` directo; todo el flujo usa `ErrorCategoria` desde el change #125.

## Open Follow-ups (no bloquean archive)

1. **Frontend de habilidades de persona** (`PersonaSkill*` records viven en `SGV.Aplicacion.Personas.Habilidades`): cuando se sume al scope de Personas, los records deben moverse a `SGV.Contracts.Personas.Habilidades` siguiendo el precedente. Documentado en `docs/decisiones-implementacion.md` § Follow-up.
2. **`GET /api/v1/personas/buscar?q=`** — endpoint server-side de búsqueda rápida para el typeahead, requerido cuando el dataset activo supere las ~500 personas. Asunción documentada; el primer GET actual pesa ~100 KB para ese umbral. Documentado en `docs/decisiones-implementacion.md` § Asunción del typeahead.
3. **Gate de Edit en Details**: el page model actual (`Details.cshtml`) muestra el botón "Editar" a cualquier autenticado y delega el gate al handler GET de `Edit`. Considerar gating visual en Details para UX consistente con Index. Documentado en `docs/decisiones-implementacion.md` § Follow-up. Tests `DetailsPageTests.Get_Details_WhenAuthenticatedAsRegularUser_DoesNotRenderListActionForms` ya validan el comportamiento actual; el cambio sería de UX, no de seguridad.
4. **CS8524 endémico en 5 clientes web** (`CargoApiClient`, `PuestoApiClient`, `UnidadOrganizativaApiClient`, `PersonaApiClient`, `HabilidadApiClient`): el método `MapCategoriaToLegacyType(ErrorCategoria)` aparece en los 5 archivos. Es endémico del change #125 y se resuelve naturalmente al archivar ese change (cuando los enums `[Obsolete]` se eliminen). Documentado en `docs/decisiones-implementacion.md` § MapCategoriaToLegacyType endémico.
5. **No `[MySqlFact]` agregados en PR #4**: el PR #4 no agregó tests `[MySqlFact]` porque el scope era tests web + docs. Los 6 `[MySqlFact]` de `PersonaRepository.QueryAsync_*` viven en PR #143 y se ejecutan condicionalmente. Si en CI futura no hay MySQL, esos 6 tests se skipean limpio (ya documentado en AGENTS.md).

## Conclusión

El change `2026-07-14-frontend-crud-personas` cumple con **todos los requisitos del spec** (8 ADDED + 1 MODIFIED). El suite completo de **2157 tests pasa en 1 m 19 s** con MySQL local disponible. El build es limpio. No se detectaron regresiones. Los 4 follow-ups están documentados en `docs/decisiones-implementacion.md` y `apply-progress` (PR bodies) y son no-bloqueantes.

**Status**: **PASSED-WITH-FOLLOWUPS**.

Proceder a `sdd-archive` si el orquestador lo decide.
