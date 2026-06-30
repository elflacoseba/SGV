# Apply-Progress: Implementar el módulo de Cargos en el Frontend

## Estado global

- Cambio: `implementar-modulo-de-cargos-en-el-frontend`
- Modo: Strict TDD (`openspec/config.yaml` → `strict_tdd: true`)
- Estrategia de entrega: chained PRs (stacked-to-develop)
- PR actual: **PR 2 / 3** — Listado activo y baja lógica (Phase 2)
- Branch: `feat/cargos-web-listado-baja`
- PR abierta: <https://github.com/elflacoseba/SGV/pull/57>
- Estado PR: OPEN — pendiente de revisión humana (no merge desde el ejecutor).
- Review budget PR 2: ~960 líneas netas (+1003 ins / -43 del). Supera el budget de 400 líneas; aceptado por el orquestador junto con PR 1 cuando eligió `stacked-to-develop`. PR 2 está acotado a `SGV.Web` (`Pages/Organizacion/Cargos/Index.*` + `wwwroot/js/pages/cargos-index.js`) + `tests/SGV.Tests/Web/Cargo/**` (nuevo `CargoIndexPageTests.cs`, nuevo `FakeCargoApiClient.cs` extraído, ajuste de `CargoWebSeamTests.cs` para consumir el fake compartido).
- Estado de tests (PR 2 scope): `dotnet test --filter "FullyQualifiedName~CargoWebTests|FullyQualifiedName~CargoApiClientTests|FullyQualifiedName~CargoWebSeamTests|FullyQualifiedName~CargoIndexPageTests"` → 25/25 PASS.
- Estado de build: `dotnet build SGV.slnx` → success, 0 warnings, 0 errors.

## Resumen ejecutivo

PR 1 deja listos los seams (cliente tipado, contratos, registros DI, override de factory) y la entrada del menú para que los PR 2 (listado + baja lógica) y PR 3 (detalle readonly) sólo tengan que añadir comportamiento sin tocar la composición raíz. Las páginas `Index` y `Details` existen como placeholders mínimos con `[Authorize]` para que la redirección anónima esté probada en esta entrega; el cuerpo de cada página se reemplaza en su PR correspondiente.

## TDD Cycle Evidence (Strict TDD, Phase 1)

| Tarea | Test File | Layer | Safety Net | RED | GREEN | TRIANGULATE | REFACTOR |
|---|---|---|---|---|---|---|---|
| 1.1 | `tests/SGV.Tests/Web/CargoWebTests.cs` | Integration (`WebApplicationFactory`) | N/A (new file) | ✅ Written; 2 tests fail (404 vs. redirect esperado) | ✅ Passing tras páginas placeholder con `[Authorize]` | ➖ Single — escenarios cubren `/organizacion/cargos` y `/organizacion/cargos/detalles/{id}` | ✅ Comentarios XML en modelos |
| 1.2 | `tests/SGV.Tests/Web/CargoWebTests.cs` | Integration | N/A (new file) | ✅ Written; falla con "Cargos" no presente en menú | ✅ Passing tras agregar entrada `Cargos` al `_Sidenav` | ➖ Single — el escenario cubre presencia + prohibición de placeholders | ✅ Activos derivados del path |
| 1.3 | `tests/SGV.Tests/Web/Cargo/CargoWebSeamTests.cs` (`WithOverrides_CargoApiClient_SwapsToFakeImplementation`, `ProductionRegistration_ResolvesCargoApiClient`) | Unit + Integration (factory) | N/A (new file) | ✅ Written; `Assert.Same(fake, resolved)` falla con la registración por defecto | ✅ Passing: el contrato `ICargoApiClient` se resuelve desde la `ServiceCollection` y el `WithOverrides` re-registra el fake | ➖ Single — basta con probar que el contrato del seam es estable | ✅ Fake `FakeCargoApiClient` localizado en el archivo de tests |
| 1.4 | `tests/SGV.Tests/Web/Cargo/CargoApiClientTests.cs` | Unit (`HttpMessageHandler` stub) | N/A (new file) | ✅ Written; 7 tests apuntan a rutas específicas, métodos HTTP y traducción de `ProblemDetails` (incluye `Http500WithNonJsonBody` que **detectó** que `JsonException` no estaba contemplado en `try/catch` original) | ✅ Passing tras agregar `catch (System.Text.Json.JsonException)` (mismo patrón que `UnidadOrganizativaApiClient`) | ✅ 7 casos: 200 lista / 200 detalle / 404 detalle / 204 delete / 404 delete con `ProblemDetails` / 409 delete con `ProblemDetails` / 500 delete con body no JSON | ✅ Stub `HttpMessageHandler` privado al archivo; sin tocar `Program.cs` ni el factory |
| 1.5 | `tests/SGV.Tests/Web/Cargo/CargoWebSeamTests.cs` (`CargoListItemViewModel_Constructor_ExposesAllProperties`, `CargoListQuery_Constructor_ExposesAllProperties`, `CargoDeleteResult_Constructor_ExposesAllProperties`) | Unit (record shape) | N/A (new file) | ✅ Written; aserciones verifican el shape público que PR 2 consumirá (no son tautologías: cada una compara contra un valor específico distinto) | ✅ Passing; los records exponen las propiedades esperadas | ➖ Triangulation skipped: registros sin lógica, sin branching — se documenta el shape completo y nada más | ➖ Refactor no necesario: los records son declaraciones de tipo puras |
| 1.6 | `tests/SGV.Tests/Web/Cargo/CargoWebSeamTests.cs` (`ProductionRegistration_ResolvesCargoApiClient`) | Integration (`WebApplicationFactory.Services`) | N/A (new file) | ✅ Written; `GetRequiredService<ICargoApiClient>` con `Assert.IsType<CargoApiClient>(client)` falla si la línea de `AddHttpClient<ICargoApiClient, CargoApiClient>` en `Program.cs` se elimina | ✅ Passing; la registración tipada expone la implementación concreta correcta | ➖ Single — un `HttpClient` por tipo, una única registración, un único punto de verificación | ➖ Sin cambios en `Program.cs` (la registración previa ya era correcta) |
| 1.7 | `tests/SGV.Tests/Web/Cargo/CargoWebSeamTests.cs` (`WithOverrides_CargoApiClient_DefaultFakeDeleteAsync_ReturnsSuccess`, `WithOverrides_CargoApiClient_FakeDeleteAsync_ReturnsConfiguredConflictResult`) | Integration (`WebApplicationFactory`) | N/A (new file) | ✅ Written; sin override el `ICargoApiClient` resuelto NO es el fake — el test fallaría si el override no se aplicara o se aplicara a un descriptor equivocado | ✅ Passing; `FakeCargoApiClient.DeleteAsync` devuelve éxito por defecto y un `Conflict` cuando se configura `DeleteResultFactory` | ✅ 2 casos: default 204 NoContent y `Conflict` configurado — triangula el seam entre "override no usado" y "override + handler custom" | ✅ `FakeCargoApiClient` reusa el patrón de las fake clients existentes (`UnidadOrganizativaWebTests`) |
| 1.8 | `tests/SGV.Tests/Web/CargoWebTests.cs` (`Get_Sidenav_WhenAuthenticated_ExposesCargosModule`) | Integration | n/a | (cubierto en 1.2) | ✅ Entrada `Cargos` agregada al `_Sidenav` con `cargosActive` derivado del path | ➖ Single escenario | ✅ Sin clases CSS extra |
| 1.9 | n/a (refactor + verify) | n/a | n/a | n/a | n/a | n/a | ✅ XML docs presentes, build verde, suite cargo-web 17/17 PASS |

### Test Summary

- **Total tests written**: 17 (3 originales `CargoWebTests` + 7 `CargoApiClientTests` + 7 `CargoWebSeamTests`) — RED → GREEN en PR 1.
- **Total tests passing**: 17/17 (3 `CargoWebTests` + 7 `CargoApiClientTests` + 7 `CargoWebSeamTests`).
- **Layers used**: Integration (10 — `WebApplicationFactory`), Unit (7 — handler stub + record shape).
- **Approval tests** (refactoring): 0 — no se refactorizó código preexistente en este PR.
- **Pure functions created**: 0 — sólo tipos/cliente/registros + `FakeCargoApiClient` para los tests.
- **Real bug encontrado por RED**: `CargoApiClient.DeleteAsync` no capturaba `System.Text.Json.JsonException`, lo que rompía el parseo tolerante de bodies no JSON (regresión latente también en `UnidadOrganizativaApiClient`, fuera de scope PR 1). El test `CargoApiClientTests.DeleteAsync_Http500WithNonJsonBody_ReturnsFailedResultWithoutCrashing` reproduce el crash y la línea agregada (`catch (System.Text.Json.JsonException)`) lo cierra en una edición mínima.

## Commits del PR 1

| SHA | Tipo | Mensaje |
|---|---|---|
| `f5de0275` | test | `test(cargos-web): agregar RED para shell y redirección anónima de Cargos` |
| `3171ef9f` | feat | `feat(cargos-web): agregar contrato y cliente HTTP de cargos` |
| `be95c82c` | feat | `feat(cargos-web): registrar cliente tipado y permitir override en factory de tests` |
| `f83cd892` | feat | `feat(cargos-web): exponer navegación de Cargos y placeholders autenticados` |
| _nuevo_ | test | `test(cargos-web): cubrir seams estructurales (cliente HTTP, contrato, DI, factory) con RED directo` |
| _nuevo_ | fix | `fix(cargos-web): tolerar `JsonException` en `DeleteAsync` para no propagar crash en body no JSON` |
| _nuevo_ | docs | `docs(cargos-web): documentar evidencia RED para tareas 1.3–1.7` |

Todos los mensajes respetan conventional commits; sin `Co-Authored-By` ni atribución a IA.

## Archivos tocados

| Archivo | Acción | Resumen |
|---|---|---|
| `tests/SGV.Tests/Web/CargoWebTests.cs` | Creado | 3 tests RED→GREEN (anónimo en listado/detalle + sidenav). |
| `tests/SGV.Tests/Web/Cargo/CargoApiClientTests.cs` | Creado | 7 tests unitarios con `HttpMessageHandler` stub: 200/404/204/409/500 + `ProblemDetails`. |
| `tests/SGV.Tests/Web/Cargo/CargoWebSeamTests.cs` | Creado | 7 tests: shape de records, resolución DI de producción, override del factory, fake client. Incluye `FakeCargoApiClient`. |
| `src/SGV.Web/Integration/Organizacion/ICargoApiClient.cs` | Creado | Contrato tipado con XML docs. |
| `src/SGV.Web/Integration/Organizacion/CargoApiClient.cs` | Creado + 1 línea agregada | Cliente HTTP + traducción de `ProblemDetails`; `catch (System.Text.Json.JsonException)` para tolerar bodies no JSON. |
| `src/SGV.Web/Integration/Organizacion/CargoListItemViewModel.cs` | Creado | View model + `CargoListQuery` + `CargoDeleteResult`. |
| `src/SGV.Web/Program.cs` | Modificado | Registro de `ICargoApiClient` con `BaseAddress` desde `SgvApiOptions`. |
| `tests/SGV.Tests/Web/SgvWebApplicationFactory.cs` | Modificado | Override opcional de `ICargoApiClient`. |
| `src/SGV.Web/Pages/Shared/Partials/_Sidenav.cshtml` | Modificado | Entrada `Cargos` con `cargosActive` para `/organizacion/cargos(/...)`. |
| `src/SGV.Web/Pages/Organizacion/Cargos/Index.cshtml(.cs)` | Creado | Placeholder autenticado (`[Authorize]`). Implementación en PR 2. |
| `src/SGV.Web/Pages/Organizacion/Cargos/Details.cshtml(.cs)` | Creado | Placeholder autenticado (`[Authorize]`). Implementación en PR 3. |
| `openspec/changes/implementar-modulo-de-cargos-en-el-frontend/tasks.md` | Modificado | Phase 1 marcada como completada. |
| `openspec/changes/implementar-modulo-de-cargos-en-el-frontend/apply-progress.md` | Modificado | Tabla TDD Cycle Evidence actualizada con RED directo para 1.3–1.7 y nota del bug `JsonException`. |

## Hallazgos / desviaciones

- **Páginas placeholder mínimas (Index/Details)**: necesarias para que la redirección anónima (1.1) tenga una ruta a la cual `[Authorize]` pueda aplicarse. Se documenta en los XML doc que la implementación real llega en PR 2/3. Mantienen el scope: sólo `[Authorize]` + `OnGet` vacío + título "Cargos"/"Detalle de cargo" y subtítulo "Organización", sin create/edit, sin skills, sin reactivación, sin JS.
- **Bug real encontrado por RED en `CargoApiClient.DeleteAsync`**: la rama original capturaba `NotSupportedException` y `HttpRequestException` pero no `System.Text.Json.JsonException`, por lo que un `500 Internal Server Error` con body `text/plain` rompía el flujo de baja con un crash no controlado. La edición mínima agrega una cláusula `catch (System.Text.Json.JsonException) { }` alineada con el patrón existente. El test `DeleteAsync_Http500WithNonJsonBody_ReturnsFailedResultWithoutCrashing` lo reproduce y queda en verde. La misma regresión existe latente en `UnidadOrganizativaApiClient.DeleteAsync`, queda fuera del scope de PR 1 y se sugiere abrir un cambio específico.
- **Tests de shape de records (1.5)**: la spec original los marcaba como `n/a`. Para mantener TDD honesto, ahora se verifica el shape público (`Id`, `Codigo`, `Nombre`, `Descripcion`, `Nivel` para `CargoListItemViewModel`; los 4 campos de `CargoListQuery`; los 4 campos de `CargoDeleteResult`). Las aserciones comparan contra valores específicos, no son tautologías.
- **Tests de registración DI (1.6)**: la spec original los marcaba como `n/a`. Ahora `ProductionRegistration_ResolvesCargoApiClient` falla si la línea de `AddHttpClient<ICargoApiClient, CargoApiClient>` se elimina o cambia de tipo concreto.
- **Tests del override del factory (1.7)**: la spec original los marcaba como `n/a`. Ahora `WithOverrides_CargoApiClient_SwapsToFakeImplementation` falla si el `WithOverrides` re-registra el descriptor equivocado o no aplica el re-registro.
- **Tests preexistentes fallando (NO regresión)**: `UnidadOrganizativaWebTests.Get_Index_WhenTreeView{Fails,HasNoNodes,Requested_*` están rojos antes y después de este PR (verificado con `git stash` contra `develop`). No relacionados con Phase 1. El reporte al orquestador los cita para awareness.
- **Sin scripts JS**: PR 1 no incluye `cargos-index.js` (sujeto a PR 2 según tasks).
- **`bun install`/`bun run build`**: no requeridos porque PR 1 no toca assets (`wwwroot/js/pages/` ni CSS).

## Riesgos residuales

- **Bajo**: el detalle de listado/detalle readonly sigue sin ejercitar el `HttpClient` real porque PR 1 no entra en `OnGetAsync`/`OnPostDeleteAsync` (queda para PR 2/3). Las pruebas unitarias con handler stub cubren hoy la traducción de respuestas, no el path completo de Razor Pages.
- **Bajo**: la rama develop puede recibir otros PRs mientras esta está pendiente. Stacked PRs contra `develop` se mantendrán rebaseables porque los toques son aditivos y aislados a `SGV.Web` + `SGV.Tests/Web` + `openspec/changes`.
- **Bajo (regresión latente)**: la misma ausencia de `catch (System.Text.Json.JsonException)` existe en `UnidadOrganizativaApiClient.DeleteAsync`. Detectada durante el RED de PR 1; fuera de scope, se sugiere un cambio específico.

## PR Boundary

- **Starts from**: `origin/develop` @ `4c1c3032`.
- **Ends with**: PR 1 entrega seams + shell + placeholders autenticados + evidencia RED directa en 1.3–1.7. NO incluye tabla, búsqueda, orden, paginación, baja lógica ni detalle.
- **Review budget**: 350 líneas base + ~313 líneas de remediación (tests + fix + docs). El delta de remediación es aditivo y aislado a `tests/SGV.Tests/Web/Cargo/**` (nuevo), un `catch` adicional en `CargoApiClient.cs` y el `apply-progress.md` actualizado.

## Next steps recomendados

1. Revisión humana de PR 1.
2. Una vez aprobado y mergeado, abrir PR 2 (`feat/cargos-web-listado-baja`) que implementa `Index` completo (tabla Inspinia, búsqueda, orden, paginación local, `OnPostDeleteAsync`, `cargos-index.js` con confirmación SweetAlert2, feedback `409`).
3. Luego PR 3 (`feat/cargos-web-detalle-readonly`) con `Details` completo (render readonly + estado no encontrado).
4. Cambio aparte para cerrar la regresión latente de `JsonException` en `UnidadOrganizativaApiClient.DeleteAsync` (no es scope de este PR).

## Cómo reproducir la verificación

```bash
dotnet build SGV.slnx
dotnet test SGV.slnx --filter "FullyQualifiedName~CargoWebTests|FullyQualifiedName~CargoApiClientTests|FullyQualifiedName~CargoWebSeamTests|FullyQualifiedName~CargoIndexPageTests" --no-build
```

Ambos deben pasar en verde local (25/25). La CI levantará MySQL y ejecutará la suite completa, incluidos los escenarios preexistentes del módulo de unidades organizativas.

---

# PR 2 — Listado activo y baja lógica (Phase 2)

## Estado de Phase 2

- Branch: `feat/cargos-web-listado-baja`
- Base: `develop` (PR 1 mergeado en `01856599`).
- Commits nuevos: 3 (`test RED` → `feat GREEN` → `refactor`).
- Tareas 2.1–2.9 completas.
- Tests: 8 nuevos (RED directo) + 2 existentes (`CargoWebSeamTests`) migrados al fake compartido = 25/25 PASS en el scope PR 2.
- Build: `dotnet build SGV.slnx` → 0 warnings, 0 errors.
- `grep` de tokens prohibidos (`Crear|Editar|Habilidades|Reactivar`) sobre `src/SGV.Web/Pages/Organizacion/Cargos/*` y `src/SGV.Web/wwwroot/js/pages/cargos-index.js` → 0 matches (alcance de PR 2 honrado).

## TDD Cycle Evidence (Strict TDD, Phase 2)

| Tarea | Test File | Layer | Safety Net | RED | GREEN | TRIANGULATE | REFACTOR |
|---|---|---|---|---|---|---|---|
| 2.1 | `tests/SGV.Tests/Web/Cargo/CargoIndexPageTests.cs::Get_Index_WhenAuthenticated_RendersActiveCargosTable` | Integration (`WebApplicationFactory` + fake `ICargoApiClient`) | ✅ 17/17 baseline antes de tocar páginas | ✅ Written; falla con `Assert.Contains` "C-001" ausente (la página era placeholder) | ✅ Passing tras `OnGetAsync` + tabla en `Index.cshtml` | ✅ 2 casos: 2 filas activas distintas ejercitan `.Select(MapToViewModel)` y los enlaces Detalle/Eliminar; ausencia explícita de Crear/Editar/Habilidades/Eliminadas | ➖ Sin duplicación a extraer (cada assertion es única y de comportamiento observable) |
| 2.2 | `tests/SGV.Tests/Web/Cargo/CargoIndexPageTests.cs::Get_Index_WhenSearchHasNoResults_ShowsEmptyState` | Integration | ✅ 17/17 baseline | ✅ Written; falla con "No se encontraron cargos" ausente | ✅ Passing tras el render de la fila vacía contextual con `<td colspan="4">` | ✅ Single caso (lista vacía) | ➖ Sin refactor (placeholder `<td colspan="4">` ya quedó) |
| 2.3 | `tests/SGV.Tests/Web/Cargo/CargoIndexPageTests.cs::Get_Index_WhenQueryFails_ShowsVisibleError` | Integration (fake que lanza `HttpRequestException`) | ✅ 17/17 baseline | ✅ Written; falla con "No se pudo cargar el listado" ausente y `Alert` sin clase `alert-danger` | ✅ Passing tras `LoadAsync` con `try/catch` + `LoadErrorMessage` + `<div class="alert alert-danger">` | ✅ 2 casos contra polaridad opuesta: 2.2 (datos vacíos controlados) y 2.3 (consulta rota con `HttpRequestException`) — cubren los dos caminos del `try/catch` | ➖ Sin refactor |
| 2.4 | `tests/SGV.Tests/Web/Cargo/CargoIndexPageTests.cs::DeleteConfirmationScript_WhenCancelled_DoesNotSubmitForm` + `_WhenConfirmed_SubmitsFormOnce` | Unit (Node harness `.cjs` con DOM fake + `Swal` mock) | ✅ 17/17 baseline | ✅ Written; los 2 tests fallan porque `cargos-index.js` no existe todavía (`require` lanza ENOENT). Confirmado ejecutando `dotnet test --filter` antes de GREEN. | ✅ Passing tras `cargos-index.js` con `wireCargoDeleteConfirmation` (espejo del patrón de `unidades-organizativas-index.js`): `showCancelButton`, `reverseButtons`, `confirmButtonText = "Sí, eliminar"`, `cancelButtonText = "Cancelar"`. | ✅ 2 casos: cancelación (no submit) y confirmación (submit único). Cubren ambas ramas de `result.isConfirmed`. | ✅ `module.exports = { wireCargoDeleteConfirmation }` agregado al final del script para mantener simetría con el script de unidades y permitir el harness Node sin ejecutar el DOM real. |
| 2.5 | `tests/SGV.Tests/Web/Cargo/CargoIndexPageTests.cs::Post_Delete_WhenSuccessful_RedirectsPreservingFiltersAndRefreshRemovesRow` + `_WhenConflict_ShowsFeedbackAndKeepsRowVisible` + `_WhenNotFound_ShowsFeedbackAndKeepsRowVisible` | Integration (POST + GET refresh) | ✅ 17/17 baseline | ✅ Written; 3 tests fallan: success porque `OnPostDeleteAsync` no existía, conflict porque la página no traducía `ProblemDetails.Detail`, not-found porque no había rama 404 | ✅ Passing tras `OnPostDeleteAsync` con `ResolveRedirectPageAsync`, `TempData` (success/danger) y 3 ramas de feedback (409/404/default). | ✅ 3 casos contra 3 ramas del contrato backend: 204, 409 (`CargoConPuestosActivos`), 404 (`CargoNoEncontrado`). El test de 409 valida la concatenación del prefijo "No se pudo eliminar el cargo." con el `ProblemDetails.Detail`. | ➖ Sin refactor posterior — la lógica de mensaje se mantuvo en un solo `switch` claro |
| 2.6 | n/a (GREEN impl) | n/a | ✅ 17/17 | ✅ Tests 2.1–2.3, 2.5 en RED | ✅ `src/SGV.Web/Pages/Organizacion/Cargos/Index.cshtml.cs`: `[Authorize]`, ctor primario (`ICargoApiClient`, `ILogger`), `OnGetAsync`, `OnPostDeleteAsync`, `ResolveRedirectPageAsync`, `ApplyVisibleFilter/Sort`, `GetSortRoute/GetSortIcon`, `MapToViewModel`, manejo de errores con `LoadErrorMessage`. | n/a | ✅ Helpers `ApplyVisiblePage` y `ComputeTotalCount` extraídos en commit aparte para eliminar duplicación entre `LoadAsync` y `ResolveRedirectPageAsync`. |
| 2.7 | n/a (GREEN impl) | n/a | ✅ 17/17 | ✅ Tests 2.1, 2.2 en RED | ✅ `src/SGV.Web/Pages/Organizacion/Cargos/Index.cshtml`: tabla Inspinia (`table table-custom`), buscador con `app-search`, sort links con `GetSortIcon`, paginación con `page-item active/disabled`, fila vacía contextual con `colspan="4"`, `<form method="post" data-cargo-delete-form formaction="?handler=Delete">`, `@Html.AntiForgeryToken()`. | n/a | ➖ Sin CSS nuevo; el shell Inspinia ya cubre el patrón |
| 2.8 | n/a (GREEN impl) | n/a | ✅ 17/17 | ✅ Tests 2.4 en RED | ✅ `src/SGV.Web/wwwroot/js/pages/cargos-index.js` con `wireCargoDeleteConfirmation` (SweetAlert2 + `reverseButtons`, mensajes en español, espejado de `unidades-organizativas-index.js`) | n/a | ➖ Sin refactor |
| 2.9 | n/a (refactor + verify) | n/a | n/a | n/a | n/a | n/a | ✅ Helper extraction (commit `refactor`). ✅ `dotnet test --filter "FullyQualifiedName~CargoWebTests|FullyQualifiedName~CargoApiClientTests|FullyQualifiedName~CargoWebSeamTests|FullyQualifiedName~CargoIndexPageTests"` → 25/25 PASS. ✅ Tokens prohibidos (Crear/Editar/Habilidades/Reactivar) no aparecen en los archivos del módulo. |

### Test Summary (Phase 2)

- **Total tests written**: 8 nuevos en `CargoIndexPageTests.cs` (RED → GREEN) + 2 migrados a la API actualizada del `FakeCargoApiClient` (de `DeleteResultFactory` a `DeleteResult`; siguen en GREEN).
- **Total tests passing**: 25/25 (3 `CargoWebTests` + 7 `CargoApiClientTests` + 7 `CargoWebSeamTests` + 8 `CargoIndexPageTests`).
- **Layers used**: Integration (10 — `WebApplicationFactory` con `ICargoApiClient` falso), Unit (1 — Node harness `.cjs` para `cargos-index.js`).
- **Approval tests** (refactoring): 0 — no se refactorizó código preexistente fuera del scope PR 2.
- **Pure functions created**: 3 (`ApplyVisibleFilter`, `Matches`, `ApplyVisibleSort`) en `Index.cshtml.cs`.
- **JS harness**: Node 1.3.14 ya disponible; `npx node` resuelve el script sin requerir `bun`/`gulp`.

### Commits del PR 2

| SHA | Tipo | Mensaje |
|---|---|---|
| `7bb80ab6` | test | `test(cargos-web): agregar RED para listado, baja lógica y harness JS de confirmación` |
| `f2bc3b7c` | feat | `feat(cargos-web): implementar listado activo y baja lógica confirmada con SweetAlert2` |
| `f09bfdd4` | refactor | `refactor(cargos-web): extraer helpers ApplyVisiblePage y ComputeTotalCount` |

Todos los mensajes respetan conventional commits; sin `Co-Authored-By` ni atribución a IA.

### Archivos tocados (PR 2)

| Archivo | Acción | Resumen |
|---|---|---|
| `tests/SGV.Tests/Web/Cargo/CargoIndexPageTests.cs` | Creado | 8 tests: 2.1, 2.2, 2.3, 2.4 (×2), 2.5 (×3) + helpers (`CreateCargo`, `CreateAuthenticatedClientAsync`, `ExtractAntiforgeryTokenAsync`, `ExecuteDeleteConfirmationScriptAsync`, `RecordingHttpMessageHandler`, `DeleteScriptExecutionResult`). |
| `tests/SGV.Tests/Web/Cargo/FakeCargoApiClient.cs` | Creado | Fake en memoria compartido por `CargoWebSeamTests` y `CargoIndexPageTests`. Configura resultado de `GetAllAsync`, fuerza excepción, registra llamadas y simula el filtrado post-borrado. |
| `tests/SGV.Tests/Web/Cargo/CargoWebSeamTests.cs` | Modificado | Migrado para consumir el `FakeCargoApiClient` compartido (elimina la copia local duplicada). 2 tests ajustados al nuevo shape (`DeleteResult` + `DeleteCalls`). |
| `src/SGV.Web/Pages/Organizacion/Cargos/Index.cshtml` | Reemplazado | Implementación completa: tabla Inspinia, buscador, sort links, paginación, fila vacía, feedback, formulario de baja con `?handler=Delete`, scripts/secciones SweetAlert2. |
| `src/SGV.Web/Pages/Organizacion/Cargos/Index.cshtml.cs` | Reemplazado | `[Authorize]`, ctor primario (`ICargoApiClient`, `ILogger`), `OnGetAsync`, `OnPostDeleteAsync`, `ResolveRedirectPageAsync`, helpers de filtro/sort/paginación. |
| `src/SGV.Web/wwwroot/js/pages/cargos-index.js` | Creado | `wireCargoDeleteConfirmation` (SweetAlert2, `reverseButtons`, español). |

## Hallazgos / desviaciones (Phase 2)
- **Filtro/sort/paginación en memoria** (decisión explícita del diseño): el backend solo expone `GET /api/v1/cargos` con la lista completa de activos, sin endpoint paginado. Por eso `Index.cshtml.cs` aplica `ApplyVisibleFilter`, `ApplyVisibleSort` y `Skip/Take` sobre la lista en memoria. Cobertura: 8 tests en memoria + 7 tests unitarios del `CargoApiClient` que validan el contrato HTTP.
- **`FakeCargoApiClient` ahora filtra ids eliminados** en cada `GetAllAsync` posterior al `DeleteAsync` exitoso. Esto refleja el comportamiento real del backend (la baja lógica hace que el cargo deje de aparecer en el endpoint de activos) y permite que el test `Post_Delete_WhenSuccessful_RedirectsPreservingFiltersAndRefreshRemovesRow` valide la aserción "el cargo eliminado deja de verse" tras el refresh del redirect. El cambio es aislado al fake, no toca producción.
- **Test de éxito ajustado durante RED→GREEN**: el primer setup usaba `toDelete.Nombre = "Analista a Eliminar"` y `remaining.Nombre = "Otro Cargo"` con `search = "ana"`. Tras el delete, el filtro "ana" dejaba a "Otro Cargo" fuera de la vista, contradiciendo la aserción "el otro cargo debe seguir visible". Reemplazado por nombres que ambos contienen "Ana" para que el filtro siga aplicando a la fila superviviente. El cambio es de datos de prueba, no de cobertura ni de comportamiento esperado.
- **Helpers extraídos en REFACTOR** (commit aparte): `ApplyVisiblePage` y `ComputeTotalCount` eliminan duplicación entre `LoadAsync` y `ResolveRedirectPageAsync`. Tests siguen verdes (25/25) tras el refactor.
- **JS harness con Node embebido**: se replica exactamente el patrón ya usado por `UnidadOrganizativaWebTests.ExecuteDeleteConfirmationScriptAsync` (Node 1.3.14, archivo `.cjs` temporal en `%TEMP%`, llamada a `Process.Start`). Cero dependencias nuevas en el proyecto.

## Riesgos residuales (Phase 2)

- **Resuelto (drift de base)**: el PR 2 se inició sobre `feat/cargos-web-foundation`. Tras merge de PR 1, se rebaseó sobre `develop` y PR #57 ahora apunta correctamente a `develop`. No queda drift pendiente.
- **Bajo (JS harness en CI)**: el harness usa `node`; si la imagen de CI no incluye Node, los 2 tests 2.4 fallarían. Mitigación: el proyecto ya usa el mismo harness en `UnidadOrganizativaWebTests`, lo que confirma que Node está disponible en CI.
- **Bajo (límite de PR)**: el tamaño del diff de PR 2 (~960 líneas netas) sigue siendo alto, pero el orquestador ya aceptó el excedente al elegir `stacked-to-develop`. La concentración de líneas se da en tests (`CargoIndexPageTests.cs` 432 + `FakeCargoApiClient.cs` 99 = ~530 líneas de tests contra ~430 líneas de código de producción).

## PR Boundary (Phase 2)

- **Starts from**: `develop` (PR 1 mergeado en `01856599`, rebase aplicado).
- **Ends with**: listado activo (Inspinia, búsqueda, sort, paginación local en memoria) + `OnPostDeleteAsync` con TempData + `cargos-index.js` con `wireCargoDeleteConfirmation` + feedback para 204/409/404. **NO incluye**: detalle readonly (PR 3), create/edit, skills, eliminados, reactivación.
- **Review budget**: 960 líneas netas (~1003 ins / -43 del). Aceptado por excedencia según `stacked-to-develop`.

## Next steps recomendados (Phase 2)

1. Revisión humana de PR 2 (PR #57). Ya apunta a `develop` con PR 1 mergeado.
2. Una vez aprobado y mergeado, abrir PR 3 (`feat/cargos-web-detalle-readonly`) con `Details.cshtml` + `Details.cshtml.cs`.
3. Phase 4: `bun run build` en `src/SGV.Web` + `dotnet test SGV.slnx` sin filtro verde.

## Cómo reproducir la verificación (Phase 2)

```bash
dotnet build SGV.slnx
dotnet test SGV.slnx --filter "FullyQualifiedName~CargoWebTests|FullyQualifiedName~CargoApiClientTests|FullyQualifiedName~CargoWebSeamTests|FullyQualifiedName~CargoIndexPageTests" --no-build
```

Ambos deben pasar en verde local (25/25). La CI levantará MySQL y ejecutará la suite completa.

---

# PR 3 — Detalle readonly (Phase 3)

## Estado de Phase 3

- Branch: `feat/cargos-web-detalle-readonly`
- Base: `develop` (PR 2 mergeado en `7de20163`).
- Commits: 3 (`test RED` → `feat GREEN` → `docs REFACTOR`).
- Tareas 3.1–3.5 completas.
- Tests: 2 nuevos (RED directo) = 27/27 PASS en el scope PR 3.
- Build: `dotnet build SGV.slnx` → 0 warnings, 0 errors.
- `grep` de tokens prohibidos (`Crear|Editar|Habilidades|Reactivar`) sobre `src/SGV.Web/Pages/Organizacion/Cargos/*` → 0 matches (alcance de PR 3 honrado).

## TDD Cycle Evidence (Strict TDD, Phase 3)

| Tarea | Test File | Layer | Safety Net | RED | GREEN | TRIANGULATE | REFACTOR |
|---|---|---|---|---|---|---|---|
| 3.1 | `tests/SGV.Tests/Web/Cargo/CargoDetailsPageTests.cs::Get_Details_WhenAuthenticated_ShowsCargoReadOnly` | Integration (`WebApplicationFactory` + fake `ICargoApiClient`) | ✅ 25/25 baseline (Phase 1+2) | ✅ Written; falla con "C-001" ausente (el placeholder no renderizaba datos) | ✅ Passing tras `OnGetAsync` + `dl` readonly en `Details.cshtml` | ➖ Single — el escenario cubre "detalle existente" sin ramas de variación (los campos son fijos) | ➖ Sin duplicación a extraer (página nueva, sin lógica compartida con Index) |
| 3.2 | `tests/SGV.Tests/Web/Cargo/CargoDetailsPageTests.cs::Get_Details_WhenCargoNotFound_ShowsNotAvailableState` | Integration | ✅ 25/25 baseline | ✅ Written; falla con "no está disponible" ausente | ✅ Passing tras rama `IsNotFound` + `<div class="card">` con mensaje informativo | ➖ Single — un único camino "no encontrado", sin branching interno | ➖ Sin refactor (código mínimo) |
| 3.3 | n/a (GREEN impl) | n/a | ✅ 25/25 | ✅ Tests 3.1, 3.2 en RED | ✅ `src/SGV.Web/Pages/Organizacion/Cargos/Details.cshtml.cs`: `[Authorize]`, ctor primario (`ICargoApiClient`, `ILogger`), `OnGetAsync(id, p, search, sort)`, log warning en 404, log error en excepción, `IsNotFound` + retorno preservando filtros. | n/a | ✅ XML docs en todos los miembros públicos |
| 3.4 | n/a (GREEN impl) | n/a | ✅ 25/25 | ✅ Tests 3.1, 3.2 en RED | ✅ `src/SGV.Web/Pages/Organizacion/Cargos/Details.cshtml`: `@page "/organizacion/cargos/detalles/{id:guid}"`, `dl.row` con `dt.col-sm-3`/`dd.col-sm-9` para cada campo, rama `IsNotFound` con `<div class="card text-center">`, enlace "Volver al listado" preservando `p/search/sort` vía `Url.Page`. Sin Crear/Editar/Habilidades/Reactivar. | n/a | ➖ Sin refactor |
| 3.5 | n/a (refactor + verify) | n/a | n/a | n/a | n/a | n/a | ✅ Código mínimo sin duplicación. ✅ `dotnet test --filter "FullyQualifiedName~CargoWebTests|FullyQualifiedName~CargoApiClientTests|FullyQualifiedName~CargoWebSeamTests|FullyQualifiedName~CargoIndexPageTests|FullyQualifiedName~CargoDetailsPageTests"` → 27/27 PASS. ✅ Build 0 warnings. ✅ Tokens prohibidos ausentes. |

### Test Summary (Phase 3)

- **Total tests written**: 2 nuevos en `CargoDetailsPageTests.cs` (RED → GREEN).
- **Total tests passing**: 27/27 (3 `CargoWebTests` + 7 `CargoApiClientTests` + 7 `CargoWebSeamTests` + 8 `CargoIndexPageTests` + 2 `CargoDetailsPageTests`).
- **Layers used**: Integration (2 — `WebApplicationFactory` con `ICargoApiClient` falso).
- **Approval tests** (refactoring): 0 — no se refactorizó código preexistente.
- **Pure functions created**: 0 — el detalle es UI pura (lectura de API + render condicional).

### Commits del PR 3

| SHA | Tipo | Mensaje |
|---|---|---|
| _pendiente_ | test | `test(cargos-web): agregar RED para detalle readonly de cargos` |
| _pendiente_ | feat | `feat(cargos-web): implementar detalle readonly con dl y estado no disponible` |
| _pendiente_ | docs | `docs(cargos-web): documentar evidencia TDD de Phase 3 y marcar tareas 3.1-3.5 como completas` |

Todos los mensajes respetan conventional commits; sin `Co-Authored-By` ni atribución a IA.

### Archivos tocados (PR 3)

| Archivo | Acción | Resumen |
|---|---|---|
| `tests/SGV.Tests/Web/Cargo/CargoDetailsPageTests.cs` | Creado | 2 tests: 3.1 (detalle existente con Codigo/Nombre/Descripcion/Nivel + Volver al listado), 3.2 (cargo no encontrado con estado recuperable). Helpers: `CreateCargo`, `CreateAuthenticatedClientAsync`, `ExtractAntiforgeryTokenAsync`, `RecordingHttpMessageHandler`. |
| `tests/SGV.Tests/Web/Cargo/FakeCargoApiClient.cs` | Modificado | `GetByIdAsync` ahora busca en `_getAllResult` por Id (backward compatible: null si no está, null si eliminado, devuelve el cargo si existe). |
| `src/SGV.Web/Pages/Organizacion/Cargos/Details.cshtml` | Reemplazado | Implementación completa: `dl.row` readonly para Codigo/Nombre/Descripcion/Nivel + rama `IsNotFound` con mensaje informativo + enlace "Volver al listado" con `Url.Page` preservando `p/search/sort`. |
| `src/SGV.Web/Pages/Organizacion/Cargos/Details.cshtml.cs` | Reemplazado | `[Authorize]`, ctor primario (`ICargoApiClient`, `ILogger`), `OnGetAsync(id, p, search, sort)`, carga desde API, `IsNotFound` en null/exception, log warning/error según el caso. |
| `openspec/changes/implementar-modulo-de-cargos-en-el-frontend/tasks.md` | Modificado | Phase 3 marcada como completada (3.1–3.5 `[x]`). |

## Hallazgos / desviaciones (Phase 3)

- **`FakeCargoApiClient.GetByIdAsync` actualizado para devolver datos reales**: antes devolvía `null` siempre. Ahora busca en `_getAllResult` por Id y filúa eliminados. Es 100% backward compatible: los tests existentes (Phase 1/2) nunca ejercitaban `GetByIdAsync`.
- **Test 3.2 corregido durante RED**: la aserción original buscaba `"no disponible"` pero el texto visible es `"no está disponible"`. Se corrigió durante la fase RED antes de pasar a GREEN. Esto validó que el contenido HTML real se renderiza correctamente.

## Riesgos residuales (Phase 3)

- **Bajo (PR scope)**: el detalle readonly no ejerce el `HttpClient` real (usa fake). Las pruebas unitarias del `CargoApiClient` (7 tests) cubren la traducción de respuestas HTTP incluyendo el caso 200 y 404.
- **Bajo (límite de PR)**: PR 3 es pequeño (~180 líneas estimadas, dentro del budget de 400). No requiere excepción.

## PR Boundary (Phase 3)

- **Starts from**: `develop` (PR 2 mergeado en `7de20163`).
- **Ends with**: detalle readonly con `dl` + rama `IsNotFound` + "Volver al listado" preservando contexto. **NO incluye**: create/edit, skills, eliminados, reactivación, cambios backend, migraciones, `bun run build`, suite completa sin filtro (Phase 4).

## Next steps recomendados (Phase 3)

1. Revisión humana de PR 3.
2. Una vez aprobado y mergeado, ejecutar Phase 4: `bun run build` en `src/SGV.Web` + `dotnet test SGV.slnx` sin filtro.
3. Considerar cambio aparte para cerrar la regresión latente de `JsonException` en `UnidadOrganizativaApiClient.DeleteAsync` (detectada en PR 1).

## Cómo reproducir la verificación (Phase 3)

```bash
dotnet build SGV.slnx
dotnet test SGV.slnx --filter "FullyQualifiedName~CargoWebTests|FullyQualifiedName~CargoApiClientTests|FullyQualifiedName~CargoWebSeamTests|FullyQualifiedName~CargoIndexPageTests|FullyQualifiedName~CargoDetailsPageTests" --no-build
```

Ambos deben pasar en verde local (27/27).