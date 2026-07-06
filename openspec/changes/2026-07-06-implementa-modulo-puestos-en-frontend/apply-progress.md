# Apply-Progress: Implementar el módulo de Puestos en el Frontend

## Estado global

- Cambio: `2026-07-06-implementa-modulo-puestos-en-frontend`
- Modo: Strict TDD (`openspec/config.yaml` → `strict_tdd: true`)
- Estrategia de entrega: chained PRs — `feature-branch-chain` (`stacked-to-develop`), 5 PRs.
- PR actual: **PR 2 / 5** — Listado + baja lógica + reactivación.
- Branch: `feat/puestos-pr2-listado-baja-reactivate` (base `develop@d78cfb95` que incluye PR 1 + commit `aa9affa7`).
- Estado PR: rama local con 4 commits ready (rama pusheada por orquestador; el orquestador gestiona `gh pr create`).
- Build: `dotnet build SGV.slnx` → success, **0 warnings, 0 errors**.
- Frontend: `bun install` + `bun run build` (en `src/SGV.Web`) → success.
- Tests slice PR 2: `--filter "FullyQualifiedName~PuestoIndexPageTests|FullyQualifiedName~PuestoWebSeamTests|FullyQualifiedName~PuestosApiClientTests|FullyQualifiedName~IPuestosApiClientContractTests"` → **75/75 PASS** (47 de PR 1 + 28 nuevos de PR 2: 16 en `PuestoIndexPageTests` + 2 nuevos `active` en `PuestoWebSeamTests`).
- Tests completos slice completo por orquestador (≥60/60 PASS esperado): 75/75 PASS.
- Suite web completa (`FullyQualifiedName~SGV.Tests.Web`): **372/372 PASS** (sin regresión).
- Suite completa `dotnet test SGV.slnx`: **1482 PASS / 12 FAIL** — las 12 fallas son exclusivamente `OcupacionRepositoryTests` (bug pre-existente #59, MySQL real, tipo `ActivePuestoIdUnique INT` vs `PuestoId CHAR(36)`; documentado en `AGENTS.md`). PR 2 es frontend-only y **no** toca Dominio/Aplicación/Infraestructura/Api; las 12 fallas son baseline, no regresión.

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
| 2.4 | n/a | n/a | **Harness JS unificado** en `ExecutePuestoConfirmationScriptAsync(PuestoConfirmationKind, bool)`: 81 líneas duplicadas eliminadas; helpers del PageModel consolidados (extract `BuildDetailsUrl` que hard-codifica `/organizacion/puestos/detalles/{id}` porque la página Details llega en PR 3C y `Url.Page` no resuelve todavía) — **28/28 PASS** del slice, token check OK, `bun run build` verde | `<docs>` (pendiente) |

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
| _pendiente_ | docs | `docs(sdd): registrar evidencia TDD de PR 2 de Puestos` |

## Branch state (acumulado PR 1 + PR 2)

- Branch actual: `feat/puestos-pr2-listado-baja-reactivate`
- Base: `develop@d78cfb95`
- Head SHA: `08c0908e` (cierre refs)

```
src/SGV.Web/Integration/Organizacion/IPuestosApiClient.cs        |  43 +++   (PR 1)
src/SGV.Web/Integration/Organizacion/PuestoListItemViewModel.cs  |  47 +++   (PR 1)
src/SGV.Web/Integration/Organizacion/PuestosApiClient.cs         | 156 +++++  (PR 1)
src/SGV.Web/Pages/Organizacion/Puestos/Index.cshtml              | 213 +++++  (PR 2)
src/SGV.Web/Pages/Organizacion/Puestos/Index.cshtml.cs           | 268 +++++  (PR 2)
src/SGV.Web/Pages/Shared/Partials/_Sidenav.cshtml                |  31 ++   (PR 1)
src/SGV.Web/Program.cs                                           |  12 +    (PR 1)
src/SGV.Web/wwwroot/js/pages/puestos-index.js                    |  85 +++   (PR 2)
tests/SGV.Tests/Web/Puesto/FakePuestosApiClient.cs               | 162 +++++  (PR 1)
tests/SGV.Tests/Web/Puesto/IPuestosApiClientContractTests.cs     | 133 +++++  (PR 1)
tests/SGV.Tests/Web/Puesto/PuestosApiClientTests.cs              | 404 ++++++ (PR 1)
tests/SGV.Tests/Web/Puesto/PuestoWebSeamTests.cs                 | 245 +++++  (PR 1 + 2 nuevos `active`)
tests/SGV.Tests/Web/Puesto/PuestoWebTestFixture.cs               | 118 +++++  (PR 1)
tests/SGV.Tests/Web/Puesto/PuestoIndexPageTests.cs               | 645 +++++  (PR 2)
tests/SGV.Tests/Web/SgvWebApplicationFactory.cs                  |  24 ++   (PR 1)
```

## Sugerencias para PR 3A

- **CRÍTICO — agregar `Pages/Organizacion/Puestos/Details.cshtml(.cs)` antes o junto con Create/Edit.** El PR 2 usa `BuildDetailsUrl` que hard-codéa `/organizacion/puestos/detalles/{id}?{queryString}` — cuando se cree la página Details en PR 3C se puede reemplazar por `Url.Page("/Organizacion/Puestos/Details", Model.BuildDetailsRouteValues(id))` y borrar el helper manual.
- **`PuestoInputModel`/`IPuestoForm`/`PuestoFormKeys`/`PuestoFormHelpers`** llegan en `tasks.md §5 (PR 3A.2)`. `PuestoFormHelpers.BuildReturnToListUrl` debe apuntar a `/Organizacion/Puestos/Index` con `returnStatus` propagado (mismo patrón que `CargoFormHelpers`).
- **Catalogos de Create vía `Task.WhenAll`** según design §4.4 / D8: las llamadas `IUnidadOrganizativaApiClient.GetAllAsync()`+`ICargoApiClient.GetAllAsync()`+`IPuestosApiClient.GetAllAsync()` en paralelo. `FakePuestosApiClient` ya soporta `GetAllException` para forzar caida del catálogo y verificar el manejo de error recuperable.
- **Tests del sidenav de Create/Edit** siguen el patrón `Get_Create_WhenAuthenticated_SidenavShowsNuevaEntryWithActiveState` (ya cubierto por la regex de `PuestoWebSeamTests::Get_Sidenav_WhenOnPuestosRoute_SubmenuIsActive` en PR 2).
- **El token check de la regla 2.4 sigue aplicando** en `Create.cshtml`, `Edit.cshtml` y sus JS companions (no incluir `>Crear<` en Edit, etc.).
