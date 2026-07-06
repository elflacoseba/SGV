# Apply-Progress: Implementar el módulo de Puestos en el Frontend

## Estado global

- Cambio: `2026-07-06-implementa-modulo-puestos-en-frontend`
- Modo: Strict TDD (`openspec/config.yaml` → `strict_tdd: true`)
- Estrategia de entrega: chained PRs — `feature-branch-chain` (`stacked-to-develop`), 5 PRs.
- PR actual: **PR 3B / 5** — Edit (UI de edición de Puesto).
- Branch: `feat/puestos-pr3b-edit` (base `develop@95009b39` que incluye PR 1 + PR 2 + PR 3A mergeado + correcciones de review #92).
- Estado PR: rama local con 2 commits ready (`6903e564` + `8c33db13`) + el commit docs de cycle evidence. Rama pusheada por orquestador; el orquestador gestiona `gh pr create`.
- Build: `dotnet build SGV.slnx` → success, **0 warnings, 0 errors**.
- Frontend: `bun install` + `bun run build` (en `src/SGV.Web`) → success.
- Tests slice PR 3B: `--filter "FullyQualifiedName~PuestoEditPageTests|FullyQualifiedName~PuestoCreatePageTests|FullyQualifiedName~PuestoIndexPageTests|FullyQualifiedName~PuestoWebSeamTests|FullyQualifiedName~PuestosApiClientTests|FullyQualifiedName~IPuestosApiClientContractTests|FullyQualifiedName~PuestoPostResultMapperTests"` → **89/89 PASS** (81 baseline de PR 1+2+3A + 8 nuevos de PR 3B en `PuestoEditPageTests`).
- Suite web completa (`FullyQualifiedName~SGV.Tests.Web`): **395/395 PASS** (387 baseline + 8 nuevos PR 3B; sin regresión).
- Token check `git grep ">Crear<" src/SGV.Web/Pages/Organizacion/Puestos/Edit.cshtml{,.cs}` → **OK** (no aparece `>Crear<` en Edit ni en su code-behind).

## Resumen ejecutivo

PR 1 dejó los seams (`IPuestosApiClient`/`PuestosApiClient`/`PuestoListItemViewModel`/`PuestoDeleteResult`/`PuestoListQuery`), su registro DI, el override `WithPuestosApiClient`, el `FakePuestosApiClient`, la `PuestoWebTestFixture` y la entry colapsable "Puestos" en el sidenav. PR 2 cierra el listado web de puestos en `/organizacion/puestos` con tabla plana estilo Inspinia, baja lógica confirmada vía SweetAlert2, reactivación con feedback de conflicto y los tests de estado `active` del sidenav que quedaron diferidos en PR 1. La página no expone los flujos Crear/Editar/Habilidades (live en PR 3A/3B); el toggle "Eliminadas" se renderiza deshabilitado con tooltip "Próximamente" (decisión locked #2: backend sin endpoint segmentado, follow-up `puestos-filtro-activos-eliminados`).

## PR 1 — Seams + shell + sidenav

### TDD Cycle Evidence (Strict TDD)

| Tarea | RED test class::method | GREEN impl path | REFACTOR outcome | Commit SHA |
|---|---|---|---|---|
| 1.1 | `PuestoWebSeamTests::Get_Sidenav_WhenAuthenticated_ExposesPuestosModule` + `::PuestoListItemViewModel_Constructor_ExposesAllPropertiesAndCodigoYNombre` + `::PuestoListQuery_EmptyAndConstructor_ExposeExpectedDefaults` | `_Sidenav.cshtml` + `PuestoListItemViewModel.cs` | Highlighting por sub-item (grupo/Listado/Nuevo) derivado del path | `d0ab465b` |
| 1.2 | `PuestosApiClientTests::GetAllAsync_Http200WithArray_ReturnsDtosAndHitsGetRoute` (+ 24 casos: 200/404/204/400/409 + `JsonException` tolerante + Theory transporte ×6 + cancelación ×6) | `PuestosApiClient.cs` + `ToCommandResultAsync` | `catch (JsonException)` en `DeleteAsync` (misma tolerancia que `CargoApiClient`) | `5496989c` |
| 1.3 | `IPuestosApiClientContractTests::Interface_ExposesExactlySixPublicMethods` (+ 6 firmas por reflexión) | n/a (reflexión sobre `IPuestosApiClient`) | Guard de superficie: exactamente 6 métodos | `5496989c` |
| 1.4 | (cubierto por 1.2/1.3) | `Integration/Organizacion/IPuestosApiClient.cs`, `PuestosApiClient.cs`, `PuestoListItemViewModel.cs` (+ `PuestoDeleteResult` + `PuestoListQuery`) | XML docs en todos los tipos públicos | `5496989c` |
| 1.5 | `PuestoWebSeamTests::ProductionRegistration_ResolvesPuestosApiClient` | `Program.cs` (+`AddHttpClient<IPuestosApiClient, PuestosApiClient>`) | Timeout=10s + `ApiBearerTokenHandler` (paridad Cargo/Habilidad) | `d0ab465b` |
| 1.6 | `PuestoWebSeamTests::WithOverrides_PuestosApiClient_SwapsToFakeImplementation` + `::WithPuestosApiClient_ConfiguredConflictDeleteResult_IsReturned` | `SgvWebApplicationFactory.cs`, `FakePuestosApiClient.cs`, `PuestoWebTestFixture.cs` | Respuestas programadas + captura de invocaciones (D2) | `d0ab465b` |
| 1.7 | `PuestoWebSeamTests::Get_Sidenav_WhenAuthenticated_ExposesPuestosModule` + `::Get_Sidenav_WhenAuthenticated_DoesNotExposeUnimplementedModules` | `_Sidenav.cshtml` (entry `aria-controls="puestos"`, `ti ti-hierarchy`, `Listado`/`Nuevo`) | Sin SCSS propio; reusa `side-nav-item`/`side-nav-link` | `d0ab465b` |
| 1.8 | n/a (refactor + verify) | n/a | Build 0 warn/0 err · slice 47/47 PASS · `bun run build` verde | `096c40a8` |

### Test Summary (PR 1)

- **Total tests nuevos**: 47 (`PuestosApiClientTests` 25 · `IPuestosApiClientContractTests` 7 · `PuestoWebSeamTests` 15 incluidas Theory rows).
- **Passing**: 47/47 en el slice; 353/353 en toda la suite web.
- **Layers**: Unit (handler stub + record shape + reflexión) e Integration (`WebApplicationFactory` para el sidenav autenticado).
- **Approval tests** (refactor de código existente): 0 — PR 1 sólo agrega tipos/registro; los únicos archivos preexistentes editados (`Program.cs`, `_Sidenav.cshtml`, `SgvWebApplicationFactory.cs`) son extensiones aditivas cubiertas por `ProductionRegistration_*`, el sidenav render y toda la suite web verde.
- **Bug latente evitado**: `DeleteAsync` incluye `catch (System.Text.Json.JsonException)` desde el inicio (mismo hallazgo que el precedente de Cargos), cubierto por `DeleteAsync_Http500WithNonJsonBody_ReturnsFailedResultWithoutCrashing`.

### Hallazgos / desviaciones (PR 1)

- **Sin páginas placeholder (desviación deliberada del precedente Cargos):** el
  precedente `2026-06-30-...-cargos` creó `Index`/`Details` placeholder en PR 1
  para probar la redirección anónima y el estado `active` del sidenav. En este
  slice `tasks.md §3` NO lista páginas, y la regla dura del ejecutor limita las
  ediciones a los archivos de `tasks.md §3`. Por eso no se crean páginas en PR 1.
- **Tests de estado `active` del sidenav diferidos a PR 2:** los escenarios
  `Get_Sidenav_WhenOnPuestosRoute_SubmenuIsActive` y
  `Get_Sidenav_WhenOnPuestosSubroute_SubmenuIsExpanded` (design §13) requieren
  navegar a `/organizacion/puestos(/...)`, ruta que sólo existe cuando llega la
  página `Index` (PR 2). Sin esa ruta, el request devuelve 404 y no renderiza el
  layout/sidenav. La **lógica** de highlighting `active` (grupo + `Listado` +
  `Nuevo`, criterio idéntico a `Habilidades`) SÍ quedó implementada en
  `_Sidenav.cshtml`; sus tests de integración se materializan en PR 2 junto con
  la página que los habilita. PR 1 cubre presencia del módulo + submenú
  `Listado`/`Nuevo` + ausencia de módulos no especificados.
- **`DoesNotExposeUnimplementedModules` afirma sobre texto de menú (`>Modulo<`):**
  la app se llama "Sistema de Gestión de Vacantes", así que "Vacantes" aparece en
  el `<meta name="description">`. La aserción se acota al marcador de nav para
  evitar falsos positivos.
- **`PuestoInputModel`/`PuestoFormKeys`/`PuestoFormHelpers`/`IPuestoForm` NO se
  crean en PR 1:** `tasks.md §5 (PR 3A.2)` los ubica en PR 3A. Se respeta ese
  límite (design §10 los mencionaba en PR 1, pero `tasks.md` es la desagregación
  vigente tras el re-cálculo NIT).
- **Presupuesto de revisión:** PR 1 sumó **1302 add / 3 del** (11 archivos), por
  encima del forecast ~770 y del budget de 400. El grueso son tests (~1013
  líneas; producción neta ≈ 289) por la cobertura completa del contrato de
  transporte (6 métodos × propagación + cancelación). Aceptado dentro de la
  estrategia `feature-branch-chain` ya confirmada por el orquestador.

### Commits del PR 1

| SHA | Tipo | Mensaje |
|---|---|---|
| `5496989c` | feat | `feat(puestos-web): agregar cliente HTTP tipado y contratos de Puestos` |
| `d0ab465b` | feat | `feat(puestos-web): registrar seam de Puestos y entry del sidenav` |
| `096c40a8` | docs | `docs(sdd): registrar evidencia TDD de PR 1 de Puestos` |

## PR 2 — Listado + baja lógica + reactivación

### TDD Cycle Evidence (Strict TDD)

| Tarea | RED test class::method | GREEN impl path | REFACTOR outcome | Commit SHA |
|---|---|---|---|---|
| 2.1 | `PuestoIndexPageTests` ×16: render 6 cols (incl. Puesto superior link), empty state, búsqueda sin resultados, error visible, anónimo redirige, POST Delete éxito/409/404 con preservación, POST Reactivate éxito + 409 por código, status=eliminadas forward-compat, **+2 tests `active` diferidos** en `PuestoWebSeamTests::Get_Sidenav_WhenOnPuestosRoute_SubmenuIsActive` + `::Get_Sidenav_WhenOnPuestosSubroute_SubmenuIsExpanded` | n/a (RED puro) | n/a | `f1b3a935` |
| 2.2 | (RED 2.1) | `Index.cshtml` (6 columnas, toggle Eliminadas deshabilitado con tooltip "Próximamente", banners de feedback, data-attributes para JS) + `Index.cshtml.cs` (`OnGetAsync` con `deletedId`, `OnPostDeleteAsync` con PRG y `LastDeletedId`, `OnPostReactivateAsync` con switch sobre `PuestoErrorType`, `LoadAsync` con filter+sort en memoria sobre `GetAllAsync`, helpers `BuildToggleSegmentoRouteValues`/`BuildDetailsRouteValues`/`BuildDetailsUrl`/`MapToViewModel`) + `_Sidenav.cshtml` sin cambios (lógica `active` ya presente en PR 1) | Regex order-agnostic en el test del sub-item Listado (positive lookaheads); ajuste de `status=eliminadas` en el test del link al superior para forzar `returnStatus` | `8774a5f0` |
| 2.3 | `PuestoIndexPageTests::DeleteConfirmationScript_WhenConfirmed_SubmitsFormOnce` + `::ReactivateConfirmationScript_WhenConfirmed_SubmitsFormOnce` (+2 canceladas) | `wwwroot/js/pages/puestos-index.js` con `wirePuestoDeleteConfirmation`+`wirePuestoReactivateConfirmation` (SweetAlert2, `reverseButtons: true`, foco en Cancelar, copy en español) + `module.exports` para harness | n/a | `3f1b299c` |
| 2.4 | n/a | n/a | **Harness JS unificado** en `ExecutePuestoConfirmationScriptAsync(PuestoConfirmationKind, bool)`: 81 líneas duplicadas eliminadas; helpers del PageModel consolidados (extract `BuildDetailsUrl` que hard-codifica `/organizacion/puestos/detalles/{id}` porque la página Details llega en PR 3C y `Url.Page` no resuelve todavía) — **28/28 PASS** del slice, token check OK, `bun run build` verde | `05167b70` |

### Test Summary (PR 2)

- **Total tests nuevos en PR 2**: 18 (16 `PuestoIndexPageTests` + 2 nuevos `active` en `PuestoWebSeamTests`).
- **Passing**: 18/18 en el slice; **372/372** en toda la suite web; **1482/1494** en la suite completa (las 12 fallas de `OcupacionRepositoryTests` son baseline pre-existente #59, no regresión).
- **Layers**: Integration (`WebApplicationFactory`+`FakePuestosApiClient` para todos los escenarios server-side) + Node harness (4 escenarios para los eventos de confirmación).
- **Helpers compartidos extraídos** (REFACTOR 2.4):
  - `ExecutePuestoConfirmationScriptAsync(PuestoConfirmationKind, bool)` unifica los 4 tests de harness (-81 líneas netas).
  - `BuildDetailsUrl(Guid id)` del PageModel encapsula el escape de `p/search/sort/returnStatus` y la construcción manual del URL `/organizacion/puestos/detalles/{id}` (PR 3C no existe todavía; refactor a `Url.Page` cuando llegue la página).
- **Approval tests** (refactor de código existente): 0 — sólo archivos nuevos (`Pages/Organizacion/Puestos/Index.cshtml(.cs)`, `puestos-index.js`) y los de tests; ninguna modificación a código preexistente de Cargos/Habilidades/UnidadesOrganizativas.

### Desviaciones del diseño

- **`BuildDetailsUrl` hard-codifica el patrón `/organizacion/puestos/detalles/{id}`** en vez de usar `Url.Page("/Organizacion/Puestos/Details", ...)`. Razón: la página `Details` llega en PR 3C, por lo que `Url.Page` no la resuelve todavía (arrojaría una excepción por página no registrada). El helper queda listo para que PR 3C lo reemplace por `Url.Page` una vez que la página exista — cambio trivial y aislado.
- **El test del "Puesto superior" usa `status=eliminadas`** en vez del default. Razón: `returnStatus` sólo se incluye cuando el segmento vigente es no-default (espejo del patrón de `CargoIndexModel.BuildDetailsRouteValues`). El toggle "Eliminadas" está deshabilitado en este slice, pero la query sigue siendo válida (forward-compat con `puestos-filtro-activos-eliminados`).
- **Regex del test del sub-item Listado (`Get_Sidenav_WhenOnPuestosRoute_SubmenuIsActive`) reescrita en positive lookaheads** para ser order-agnostic entre `href` y `class`. Razón: el Razor renderiza los anchors de sub-items con `class` ANTES de `href`. El cambio es trivial (sigue cubriendo el mismo invariante: cualquier `<a href="/organizacion/puestos">` con `class="...active..."`).
- **Cuentas de tests**: la `tasks.md §4` dice "≥12/12 PASS" pero el spec cubierto exige 18 escenarios (16 de `PuestoIndexPageTests` + 2 diferidos a `PuestoWebSeamTests`). El slice los entrega todos y los pasa.

### Hallazgos

- **Cobertura del spec 1:1**: los 8 escenarios de `puesto-web-listado-detalle-baja` (Req 1-6) están cubiertos por tests específicos con el nombre verbatim del design §13.
- **Slugify de la URL Details**: `BuildDetailsUrl` usa `$"/organizacion/puestos/detalles/{id:D}"` (`D` = formato Guid canónico con guiones). Cualquier consumer que compare con `indexOf` debe usar el formateador `D` explícitamente; documentado en el helper.
- **Bug latente evitado**: el `try/catch` en `DeleteAsync` ya está cubierto por `PuestosApiClientTests` (PR 1); PR 2 hereda la tolerancia a `JsonException` y `HttpRequestException` sin tocarla.

### Commits del PR 2

| SHA | Tipo | Mensaje |
|---|---|---|
| `f1b3a935` | test | `test(puestos-web): agregar tests del listado/baja/reactivacion y sidenav active` |
| `8774a5f0` | feat | `feat(puestos-web): agregar listado web de puestos con baja y reactivacion` |
| `3f1b299c` | feat | `feat(puestos-web): agregar confirmaciones SweetAlert2 de baja y reactivacion` |
| `08c0908e` | refactor | `refactor(puestos-web): extraer harness JS compartido para Delete y Reactivate` |
| `8ab8fd01` | docs | `docs(sdd): registrar SHA real del commit docs de PR 2 de Puestos en cycle evidence` |

## Branch state (acumulado PR 1 + PR 2)

- Branch actual: `feat/puestos-pr3a-create`
- Base: `develop@b8fcd7d5` (incluye PR 1 + PR 2 mergeados + `8ab8fd01`)
- Head SHA: `4c883888` (cierre refs de PR 3A)

```
src/SGV.Web/Integration/Organizacion/IPuestosApiClient.cs        |  43 +++   (PR 1)
src/SGV.Web/Integration/Organizacion/PuestoListItemViewModel.cs  |  47 +++   (PR 1)
src/SGV.Web/Integration/Organizacion/PuestosApiClient.cs         | 156 +++++  (PR 1)
src/SGV.Web/Integration/Organizacion/PuestoInputModel.cs        |  39 +++   (PR 3A)
src/SGV.Web/Integration/Organizacion/IPuestoForm.cs             |  38 +++   (PR 3A)
src/SGV.Web/Integration/Organizacion/PuestoFormHelpers.cs        |  99 +++   (PR 3A, PuestoFormKeys+Helpers)
src/SGV.Web/Integration/Organizacion/PuestoPostResultMapper.cs  |  56 +++   (PR 3A.4, refactor extraído)
src/SGV.Web/Pages/Organizacion/Puestos/Index.cshtml              | 213 +++++  (PR 2)
src/SGV.Web/Pages/Organizacion/Puestos/Index.cshtml.cs           | 268 +++++  (PR 2)
src/SGV.Web/Pages/Organizacion/Puestos/Create.cshtml             |  44 +++   (PR 3A)
src/SGV.Web/Pages/Organizacion/Puestos/Create.cshtml.cs          | 277 ++++   (PR 3A)
src/SGV.Web/Pages/Organizacion/Puestos/_Form.cshtml              |  60 +++   (PR 3A, partial compartido)
src/SGV.Web/Pages/Shared/Partials/_Sidenav.cshtml                |  31 ++   (PR 1)
src/SGV.Web/Program.cs                                           |  12 +    (PR 1)
src/SGV.Web/wwwroot/js/pages/puestos-index.js                    |  85 +++   (PR 2)
tests/SGV.Tests/Web/Puesto/FakePuestosApiClient.cs               | 162 +++++  (PR 1)
tests/SGV.Tests/Web/Puesto/FakeUnidadOrganizativaApiClient.cs    |  73 +++   (PR 3A, stub para 3 catálogos)
tests/SGV.Tests/Web/Puesto/IPuestosApiClientContractTests.cs     | 133 +++++  (PR 1)
tests/SGV.Tests/Web/Puesto/PuestosApiClientTests.cs              | 404 ++++++ (PR 1)
tests/SGV.Tests/Web/Puesto/PuestoWebSeamTests.cs                 | 245 +++++  (PR 1 + 2 nuevos `active` PR 2)
tests/SGV.Tests/Web/Puesto/PuestoWebTestFixture.cs               | 153 ++++  (PR 1 + extensions PR 3A)
tests/SGV.Tests/Web/Puesto/PuestoIndexPageTests.cs               | 645 +++++  (PR 2)
tests/SGV.Tests/Web/Puesto/PuestoCreatePageTests.cs              | 410 +++++  (PR 3A)
tests/SGV.Tests/Web/Puesto/PuestoPostResultMapperTests.cs        | 132 +++   (PR 3A.4)
tests/SGV.Tests/Web/SgvWebApplicationFactory.cs                  |  24 ++   (PR 1)
```

## PR 3A — Create (UI de alta de Puesto, ~1100, base PR 2)

### TDD Cycle Evidence (Strict TDD)

| Tarea | RED test class::method | GREEN impl path | REFACTOR outcome | Commit SHA |
|---|---|---|---|---|
| 3A.1 | `PuestoCreatePageTests` ×9: `Get_Create_WhenAnonymous_RedirectsToSignIn`, `Get_Create_WhenAuthenticated_FormContainsAllSixFields`, `Get_Create_WhenPuestosCatalogHasResults_SelectContainsNPlusOneOptions`, `Get_Create_WhenPuestosCatalogFails_ShowsRecoverableError`, `Post_Create_WhenSuccessful_RedirectsToListadoWithConfirmation`, `Post_Create_WhenBackendReturnsFieldErrors_RendersFieldValidationOnCodigo`, `Post_Create_WhenCodigoDuplicado_ReturnsFieldErrorAndKeepsForm`, `Post_Create_WhenHttpRequestException_ReloadsCatalogAndShowsGeneralError`, `Get_Create_WhenAuthenticated_SidenavShowsNuevoEntryWithActiveState` | n/a (RED puro) | n/a | `4b016ed6` |
| 3A.2 | (cubierto por 3A.1) | `src/SGV.Web/Integration/Organizacion/PuestoInputModel.cs` (6 props con `[Required]`/`[StringLength]`) + `IPuestoForm.cs` (contrato compartido) + `PuestoFormHelpers.cs` (`PuestoFormKeys` + `PuestoFormHelpers.ApplyFieldErrorsToModelState` + `BuildReturnToListUrl` con `status` forward-compat) | XML doc en todos los tipos públicos | `53e18d60` |
| 3A.3 | (cubierto por 3A.1) | `src/SGV.Web/Pages/Organizacion/Puestos/_Form.cshtml` (`@model IPuestoForm`, `if (!Model.IsEdit)` oculta Codigo/UnidadOrganizativaId/CargoId) + `Create.cshtml` (form shell) + `Create.cshtml.cs` ([Authorize], `[BindProperty] PuestoInputModel Input`, `IPuestoForm` con `IsEdit=false`/`ErrorMessage`/`Return*`, `Task.WhenAll` 3 catálogos vía helper `LaunchSafeAsync<T>` que convierte sync-throws en faulted tasks, `OnPostAsync` con `try/catch` transporte + mapeo de `PuestoCommandResult` con 4 caminos: Ok/CodigoDuplicado/Validation/HttpFailure) | PRG a Index preservando `p/search/sort/status`; `BuildListRouteValues` extraído para claridad | `49d0b4e3` |
| 3A.4 | (ya cubierto por 3A.1 + nueva sección `PuestoPostResultMapperTests` ×6: `TryMapCommandResult_NullResult_*`, `EmptyFailureResult_*`, `SuccessResult_*`, `FieldErrorsWithMultipleKeysAndMessages_*`, `ErrorMessageWithoutFieldErrors_*`, `EmptyFieldErrorsDictionary_*`) | `src/SGV.Web/Integration/Organizacion/PuestoPostResultMapper.cs` con `TryMapCommandResult` (paridad verbatim con `CargoPostResultMapper.TryMap`); `Create.cshtml.cs` ahora delega al mapper tras chequear `PuestoErrorType.Conflict` | Inline mapping de `OnPostAsync` reemplazado por `PuestoPostResultMapper.TryMapCommandResult`; cobertura unitaria dedicada (6 casos) | `4c883888` |

### Test Summary (PR 3A)

- **Total tests nuevos en PR 3A**: 15 (`PuestoCreatePageTests` 9 · `PuestoPostResultMapperTests` 6).
- **Passing**: 15/15 en el slice; **81/81** en la suma con PR 1+2; **387/387** en toda la suite web; **1494/1494** en la suite completa sin `MySqlFact` (excluyendo los 12 fallos pre-existentes de `OcupacionRepositoryTests` baseline #59).
- **Layers**: Integration (`WebApplicationFactory` + los 3 fakes de catálogo) + Unit (`PuestoPostResultMapperTests` cubre los 4 outcomes de `TryMapCommandResult` sin levantar host).
- **Approval tests** (refactor de código existente): 0 — sólo archivos nuevos; **una** modificación aditiva a `PuestoWebTestFixture` (overloads `WithCargoApiClient`/`WithUnidadOrganizativaApiClient`/`WithCatalogFakes` y `CreateAuthenticatedClientAsync(unidad,cargo,puestos)`).
- **Helpers compartidos extraídos** (REFACTOR 3A.4):
  - `PuestoPostResultMapper.TryMapCommandResult(PuestoCommandResult?, ModelStateDictionary)` paridad verbatim con `CargoPostResultMapper.TryMap`; manejado por la regla 4 caminos de `OnPostAsync`.
  - `LaunchSafeAsync<T>(Func<Task<T>>)` local a `CreateModel.LoadCatalogsAsync`: convierte throws sincrónicos en faulted tasks para que `Task.WhenAll` + checks de `Task.Status` vean las fallas de forma uniforme (necesario porque `FakePuestosApiClient.GetAllAsync` lanza `HttpRequestException` de forma sincrónica sin envolverla en `Task.FromException`).

### Desviaciones del diseño

- **`IUnidadOrganizativaApiClient` no expone `GetAllAsync()`** (design §4.4 dice "tres `GetAllAsync`", el interface real sólo expone `QueryAsync(UnidadOrganizativaListQuery)` paginado). Workaround pragmático: `LoadCatalogsAsync` invoca `unidadOrganizativaApiClient.QueryAsync(new UnidadOrganizativaListQuery(1, 200, null, null, "activas"))` con `pageSize=200` para traer todas las unidades activas en una sola llamada. El shape del backend implica que una futura paginación real limitará el dropdown de Create a la primera página, lo que el spec debería documentar como follow-up. **Reportar como design drift.** No introduce cambio de comportamiento funcional (los dropdowns muestran las mismas unidades que la página de UnidadesOrganizativas).
- **`PuestoFormKeys` y `PuestoFormHelpers` en un único archivo `PuestoFormHelpers.cs`** (en vez de dos archivos separados como sugiere `tasks.md §5 PR 3A`). El precedent Cargos consolida ambas en un archivo; mantener la paridad evita fragmentación artificial. La substantive de cada tipo es exactamente la del tasks.md; sólo cambia el empaque.
- **`StatusMessage`/`StatusKind` en Create.cshtml.cs son props públicas adicionales** (no listadas en `tasks.md §5 PR 3A` ni en `IPuestoForm`). Razón: la página necesita mostrar el banner de feedback que llega vía `TempData` cuando el POST llega como follow-up de una redirección PRG. Como sólo el Create propio setea TempData hoy, las props no se ejercitan en ningún test del slice, pero la estructura queda lista para cuando Edit (PR 3B) herede la convención.
- **Helper `BuildReturnToListUrl` acepta `status`** como parámetro (el precedent Cargo no). Espejo del patrón de `UnidadOrganizativaFormHelpers.BuildReturnToListUrl`: serializa `status=eliminadas` sólo cuando es no-default (forward-compat con el toggle que llega en PR futuro `puestos-filtro-activos-eliminados`). El Index actual ya acepta `[FromQuery] status`.
- **PRG usa `BuildListRouteValues()` interno** (dict solo con keys presentes). El `RedirectToPage` con diccionario omite los nulls automáticamente; equivalente en semántica al precedent Cargos pero más explícito sobre qué se propaga.
- **NO se creó `Edit.cshtml*` ni `Details.cshtml*`** como placeholders. El partial `_Form.cshtml` ya soporta `IsEdit` via `IPuestoForm.IsEdit` para que PR 3B lo enchufe sin tocar el Razor.

### Hallazgos

- **`FakePuestosApiClient.GetAllAsync` lanza excepciones SINCRÓNICAS** (sin `Task.FromException`). Esto rompe el patrón `try { await Task.WhenAll(...); } catch {}` esperado por el design §4.4 y obliga a un helper `LaunchSafeAsync<T>` que envuelve la factory y convierte el throw en faulted task. Es una decisión pragmática del PR 1 que no se tocó en este slice; PR 3B/3C lo heredará salvo que se rediseñe el fake.
- **`Assert.Equal(seededPuestos.Length + 1, optionCount)` con overload de xUnit resuelve a `Assert.Equal<T>(IEnumerable<T>?, IEnumerable<T>?)` en lugar de `(T, T)`**. Solución trivial: `Assert.Equal<int>(expected, actual)` explícito. Aplicar en 3 sitios del test file. Ver `PuestoCreatePageTests.Get_Create_WhenPuestosCatalogHasResults_SelectContainsNPlusOneOptions` y los dos `Assert.Equal(2, apiClient.GetAllCalls)` para recarga tras fallo.
- **`Get_Create_WhenAnonymous_RedirectsToSignIn` requiere `[Authorize]`**. Verificable contra el precedent Cargos (`_PageTitle.cshtml` + `[Authorize]`). El test RED pasa a GREEN después de 3A.3 cuando la página existe.
- **Cobertura del spec 1:1** — los 9 escenarios del spec `puesto-web-crear-editar` se cubren con tests específicos con el nombre verbatim del design §13 (los 8 escenarios de Req 6 + el sidenav de Req 7). El test de PuestoSuperiorId con `N+1 opciones` (Req 3) es la triangulación clave: cuenta exactamente `<option>` matches dentro del `<select>` con regex.

### Commits del PR 3A

| SHA | Tipo | Mensaje |
|---|---|---|
| `4b016ed6` | test | `test(puestos-web): agregar tests RED de Create y fake de IUnidadOrganizativaApiClient para PR 3A` |
| `53e18d60` | feat | `feat(puestos-web): agregar tipos de input model y helpers del form de Puestos para PR 3A` |
| `49d0b4e3` | feat | `feat(puestos-web): agregar pagina Create de Puestos con 3 catalogos en paralelo via Task.WhenAll` |
| `4c883888` | refactor | `refactor(puestos-web): extraer PuestoPostResultMapper.TryMapCommandResult con tests unitarios` |
| _este commit_ | docs | `docs(sdd): registrar evidencia TDD de PR 3A de Puestos en cycle evidence` |

## Sugerencias para PR 3B

- **REUTILIZAR el partial `_Form.cshtml` introducido en PR 3A** — el flag `IPuestoForm.IsEdit` ya está soportado: cuando es `true`, el partial oculta Codigo/UnidadOrganizativaId/CargoId vía `if (!Model.IsEdit)`. El test RED obligatorio sigue siendo `Get_Edit_HtmlRenderizado_NoContieneCodigoUnidadOrganizativaNiCargo` (design §7, test verbatim en design §13).
- **REUTILIZAR `IPuestoForm`, `PuestoInputModel`, `PuestoFormKeys`, `PuestoFormHelpers`, `PuestoPostResultMapper`** sin cambios. El EditModel sólo necesita `IsEdit => true`, precargar `Input.Nombre/Descripcion/PuestoSuperiorId` desde `GetByIdAsync`, y armar `ActualizarPuestoRequest` en el POST. Los overrides en `CreateModel` de los 4 caminos del mapper se reducen a 3 en Edit (sin `Conflict → Codigo`, porque `ActualizarPuestoRequest` no incluye `Codigo`).
- **`PuestoApiClient.UpdateAsync`** ya está cubierto por transport tests de PR 1 (sólo falta el contrato 400/409 → `PuestoCommandResult.Failure` con `FieldErrors`).
- **`BuildReturnToListUrl` + `StatusMessage`/`StatusKind`** ya soportan PRG a Index preservando filtros (forward-compat); Edit puede usarlos verbatim para redirigir a Details tras éxito.
- **PR 2 dejó `BuildDetailsUrl(Guid id)` hard-codificando `/organizacion/puestos/detalles/{id}?{...}`**. PR 3C reemplazará este helper por `Url.Page(...)` cuando cree la página Details. Si PR 3B introduce Edit, NO necesita tocar este helper.
- **El token check de la regla 2.4 sigue aplicando** en `Create.cshtml`, `Edit.cshtml` y sus JS companions (no incluir `>Crear<` en Edit, etc.).

## PR 3B — Edit (UI de edición de Puesto, base PR 3A)

### TDD Cycle Evidence (Strict TDD)

| Tarea | RED test class::method | GREEN impl path | REFACTOR outcome | Commit SHA |
|---|---|---|---|---|
| 3B.1 | `PuestoEditPageTests` ×8: `Get_Edit_WhenAnonymous_RedirectsToSignIn`, `Get_Edit_WhenAuthenticated_PrepopulatesNombreDescripcionPuestoSuperior`, `Get_Edit_WhenPuestoNotFound_ShowsRecoverableState`, **`Get_Edit_HtmlRenderizado_NoContieneCodigoUnidadOrganizativaNiCargo`** (RED obligatorio — design §7, spec Req 4), `Post_Edit_WhenSuccessful_RedirectsToDetailsWithConfirmation`, `Post_Edit_WhenBackendReturnsFieldErrors_RendersFieldValidationOnNombre`, `Post_Edit_WhenCodigoDuplicadoConflict_ShowsSpecificMessageAndKeepsForm`, `Post_Edit_WhenTransportFails_ShowsRecoverableError` | n/a (RED puro) | n/a | `6903e564` |
| 3B.2 | (cubierto 3B.1) | `src/SGV.Web/Pages/Organizacion/Puestos/Edit.cshtml` (form shell reusando `_Form.cshtml` con `IsEdit=true`; render `IsRecoverable` muestra estado recuperable cuando el puesto no existe o falla el GET) + `src/SGV.Web/Pages/Organizacion/Puestos/Edit.cshtml.cs` (`[Authorize]`, `[BindProperty] PuestoInputModel Input`, `IPuestoForm` con `IsEdit=true`/`ErrorMessage`/`StatusMessage`/`StatusKind`/`IsRecoverable`/`Return*`, `Task.WhenAll` 3 catálogos vía helper `LaunchSafeAsync<T>` que convierte sync-throws en faulted tasks, `OnPostAsync` con **pre-populate desde `GetByIdAsync`** de los campos inmutables + `ModelState.Remove(...)` para los `[Required]` no bindeados, mapeo de `PuestoCommandResult` con los 4 caminos: Success → PRG hard-code a Details, Conflict → `PuestoPostResultMapper.TryMap`, FieldErrors → mapper, HttpFailure → `ErrorMessage` recuperable) | n/a | `8c33db13` |
| 3B.3 | (cubierto 3B.1 + nueva sección Cycle Evidence) | n/a | 89/89 PASS del slice; 395/395 PASS suite web (sin regresión); token check `>Crear<` ausente en `Edit.cshtml*` OK; `bun run build` verde | _este commit_ |

### Test Summary (PR 3B)

- **Total tests nuevos en PR 3B**: 8 (`PuestoEditPageTests`).
- **Passing**: 8/8 en el slice; **89/89** en la suma con PR 1+2+3A; **395/395** en toda la suite web; **1502/1502** en la suite completa sin `MySqlFact` (excluyendo los 12 fallos pre-existentes de `OcupacionRepositoryTests` baseline #59).
- **Layers**: Integration (`WebApplicationFactory` + fakes de catálogo).
- **Approval tests** (refactor de código existente): 0 — sólo archivos nuevos; **cero** modificaciones a `FakePuestosApiClient`/`PuestoWebTestFixture` (los overloads `UpdateResult`/`UpdateException`/`UpdateCalls` ya existían desde PR 1).
- **Helpers extraídos** (REFACTOR 3B.2):
  - `LoadCatalogsAsync(CancellationToken)` privado: `Task.WhenAll` 3 catálogos + `LaunchSafeAsync<T>` + `MapToSuperiorViewModel` (idénticos al precedent `CreateModel`, paridad verbatim).
  - `LaunchSafeAsync<T>(Func<Task<T>>)` privado: convierte throws sincrónicos del fake en faulted tasks (mismo workaround que Create).
  - **Pre-populate de campos inmutables en `OnPostAsync`**: dado que `PuestoInputModel` declara `Codigo`/`UnidadOrganizativaId`/`CargoId` con `[Required]` (paridad con Create) pero el form de Edit NO los renderiza, el POST no envía esos campos y `ModelState.IsValid` falla. La solución es recuperar el DTO vía `GetByIdAsync` antes de validar y limpiar los errores del ModelState con `ModelState.Remove(...)` para los 3 keys inmutables. **No introduce cambios de comportamiento**: el `ActualizarPuestoRequest` enviado al backend sigue conteniendo sólo `Nombre`/`Descripcion`/`PuestoSuperiorId` (verificado por `Post_Edit_WhenSuccessful_RedirectsToDetailsWithConfirmation` que afirma el shape exacto del payload vía `update.Request.Nombre == "Nombre actualizado"`).

### Desviaciones del diseño

- **Pre-populate en `OnPostAsync` (workaround para `[Required]` heredado)**: el design §4.5 describe el flujo "POST arma `ActualizarPuestoRequest(Nombre, DescripcionTrimmed, PuestoSuperiorId)` y llama `UpdateAsync`", pero no explicita cómo sortear la validación de los campos inmutables que el form de Edit no renderiza. Tres alternativas evaluadas:
  1. *Quitar `[Required]` de Codigo/UnidadOrganizativaId/CargoId* — descartado: rompe Create (no detectaría campos faltantes) y los tests de Create que asumen `[Required]` se mantienen.
  2. *Crear `PuestoEditInputModel` separado con sólo los 3 campos editables* — descartado: duplica el modelo y complica el binding con la firma de `IPuestosApiClient.UpdateAsync(Guid id, ActualizarPuestoRequest request, ...)`.
  3. *Pre-popular desde `GetByIdAsync` en `OnPostAsync` + `ModelState.Remove(...)`* — **elegido**: preserva el modelo compartido, agrega una llamada extra al API (trivially cacheable en el futuro si hace falta), y mantiene el test RED obligatorio verde (no introduce hidden inputs en el HTML).
- **PRG a Details hard-code**: `return Redirect($"/organizacion/puestos/detalles/{id:D}")` en vez de `RedirectToPage("/Organizacion/Puestos/Details", new { id })`. Razón: la página Details no existe hasta PR 3C, por lo que `Url.Page` no la resuelve. Constraint del orquestador explícita: "usá el mismo patrón de hard-code". PR 3C refactorizará esto.
- **`OnPostAsync` no distingue `Conflict → Codigo`**: el precedent Create intercepta `PuestoErrorType.Conflict` para mapear `CodigoDuplicado` al campo `Input.Codigo`. En Edit esto no aplica porque `ActualizarPuestoRequest` no incluye `Codigo` (es inmutable). Se delega al mapper genérico `PuestoPostResultMapper.TryMap` que aplica `FieldErrors` si los hay o el mensaje general bajo `string.Empty`. Cubierto por `Post_Edit_WhenCodigoDuplicadoConflict_ShowsSpecificMessageAndKeepsForm`.
- **Helpers `MapToSuperiorViewModel` y `LaunchSafeAsync<T>` extraídos a `PuestoFormHelpers`**: ambas PageModels consumían copias idénticas de estos helpers como `private static`. Refactorizado en commit `5385c0a6` (post-review #93) — ambos métodos ahora viven en `PuestoFormHelpers` como `public static`. El refactor se aplicó también a `CreateModel` (PR 3A) preservando el comportamiento 1:1; los 17 tests previos del slice Create+Edit+Helpers siguen verdes.
- **NO se creó `Details.cshtml*`** como placeholder ni como página real. El PRG a Details retorna 302 a una URL que aún no existe, pero el test `Post_Edit_WhenSuccessful_RedirectsToDetailsWithConfirmation` sólo verifica el status 302 y el Location header (no sigue redirects porque `AllowAutoRedirect=false`). El comportamiento end-to-end de la página de detalle se materializa en PR 3C.
- **`FakePuestosApiClient` no se extendió**: el fake ya tenía `UpdateResult`/`UpdateException`/`UpdateCalls` desde PR 1 (paridad con `FakeCargoApiClient`), por lo que los 4 outcomes (Success/400 FieldErrors/409 CodigoDuplicado/HttpFailure) ya eran configurables. **Cero modificaciones al fake en este slice.**
- **`PuestoWebTestFixture` no se extendió**: el helper `CreateAuthenticatedClientAsync(FakePuestosApiClient apiClient)` ya soportaba el override del fake para los tests de Edit sin necesidad de cambios.

### Hallazgos

- **`ModelState.Remove(key)` es esencial después del pre-populate**: si sólo se asigna `Input.Codigo = ...` sin limpiar el ModelState, el handler retorna `Page()` antes de llamar a `UpdateAsync` porque el binder ya marcó el error `[Required]` para `Codigo` durante el binding (cuando el form no envió ese campo). Verificado experimentalmente durante el ciclo GREEN: la primera versión del fix sólo pre-poblaba y los 3 tests POST fallaban con "Expected: Found, Actual: OK" porque `UpdateAsync` nunca se invocaba.
- **El test `Post_Edit_WhenCodigoDuplicadoConflict_ShowsSpecificMessageAndKeepsForm` afirma `Assert.Contains` del mensaje completo** en vez de buscar el span con `data-valmsg-for`. Razón: como Edit no tiene `Input.Codigo` en el form, no hay `data-valmsg-for="Input.Codigo"`. El conflict se aplica bajo `string.Empty` (vía mapper), lo que aparece en el `asp-validation-summary="ModelOnly"`. La aserción es menos estricta que las de Create (que sí verifican el span exacto) pero más fiel al comportamiento real del mapper. **Reportar como variante menor del spec `puesto-web-crear-editar` Req 6.** Crear tendría `Assert.True(Regex.IsMatch(...))` apuntando al span `Input.Codigo`; Edit tiene `Assert.Contains` del mensaje en cualquier parte del HTML.
- **`PuestoCommandResult` con `Error.Type == Conflict` ya NO requiere el override `ModelState.AddModelError(PuestoFormKeys.CodigoKey, ...)`** que Create sí necesita. En Edit el mapper genérico cubre los 3 outcomes restantes (Success, FieldErrors, Conflict-con-mensaje-general) sin lógica adicional.
- **El catálogo de Unidades/Cargos se carga en Edit aunque sean inmutables**: el design §4.5 dice "GET invoca `GetByIdAsync(id)` + dos catálogos" pero el dropdown de `PuestoSuperiorId` referencia los nombres `UnidadOrganizativaNombre` y `CargoNombre` vía `CodigoYNombre` (`"P-001 — Director"`), por lo que tener los catálogos cargados no es estrictamente necesario para renderizar el dropdown. La implementación carga los 3 catálogos en paralelo vía `Task.WhenAll` (paridad verbatim con Create) para mantener simetría y minimizar divergencia. Tradeoff: 2 llamadas extra al API en GET. Si en el futuro se mide que esto pesa, se puede acortar a sólo `GetAllAsync` para `PuestoSuperiorId`. Por ahora: paridad > micro-optimización.
- **No hay `Assert.True` con mensaje custom en xUnit v2**: el primer intento del test RED usaba `Assert.DoesNotMatch(content, regex, "mensaje")` que NO compila en xUnit v2 (sólo v3 soporta assertion messages). Corregido a `Assert.DoesNotMatch(regex, content)` sin mensaje. **Aplicar en futuras migraciones de tests.**
- **Pre-populate de `OnPostAsync` requiere `GetByIdAsync` extra (latencia de red duplicada por write)**: cada POST exitoso hace dos llamadas al API de Puestos (`GetByIdAsync` para pre-poblar + `UpdateAsync`). Con latencia de red normal, el usuario percibe el doble de demora al guardar vs. Create. Documentado como desviación aceptada en este PR (3 alternativas evaluadas en la sección anterior, todas con tradeoffs). **Follow-up explícito para PR 3D/3E**: medir latencia en entorno real y decidir entre (a) `<input type="hidden">` para los campos inmutables en `_Form.cshtml` con `IsEdit=true`, (b) `PuestoEditInputModel` separado sin `[Required]` en inmutables, o (c) `IEditPuestoStateCache` que mantenga el DTO en memoria durante el ciclo GET→POST. Requiere reabrir el debate de diseño y otro SDD slice — **no** es scope de PR 3B ni de PR 3C.

### Commits del PR 3B

| SHA | Tipo | Mensaje |
|---|---|---|
| `6903e564` | test | `test(puestos-web): agregar tests RED de Edit y test de ausencia obligatorio para PR 3B` |
| `8c33db13` | feat | `feat(puestos-web): agregar pagina Edit de Puestos con prepopulate y mapeo de 4 caminos para PR 3B` |
| `6b0a4c6a` | docs | `docs(sdd): registrar evidencia TDD de PR 3B de Puestos en cycle evidence` |
| `0666cfe8` | docs | `docs(sdd): registrar SHA real del commit docs de PR 3B en cycle evidence` |
| `5385c0a6` | refactor | `refactor(puestos-web): aplicar correcciones de PR review #93` (post-review: extraer helpers a `PuestoFormHelpers`, preservar `ErrorMessage` en catch de pre-populate, newline + comprimir XML docs) |
