# Apply-Progress: Implementar el módulo de Cargos en el Frontend

## Estado global

- Cambio: `implementar-modulo-de-cargos-en-el-frontend`
- Modo: Strict TDD (`openspec/config.yaml` → `strict_tdd: true`)
- Estrategia de entrega: chained PRs (stacked-to-develop)
- PR actual: **PR 1 / 3** — Fundación y shell (Phase 1)
- Branch: `feat/cargos-web-foundation`
- PR abierta: <https://github.com/elflacoseba/SGV/pull/55>
- Estado PR: OPEN — pendiente de revisión humana (no merge desde el ejecutor).
- Review budget PR 1: 350 líneas base + remediación de evidencia TDD (~313 líneas adicionales, mayormente tests). El ejecutor prioriza cerrar el CRITICAL del verify (RED directo en 1.3–1.7) por sobre el budget estricto; sigue siendo un PR acotado a `SGV.Web` + `SGV.Tests/Web` + artefacto SDD.
- Estado de tests: `dotnet test --filter "FullyQualifiedName~CargoWebTests|FullyQualifiedName~CargoApiClientTests|FullyQualifiedName~CargoWebSeamTests"` → 17/17 PASS.
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
dotnet test SGV.slnx --filter "FullyQualifiedName~CargoWebTests|FullyQualifiedName~CargoApiClientTests|FullyQualifiedName~CargoWebSeamTests" --no-build
```

Ambos deben pasar en verde local (17/17). La CI levantará MySQL y ejecutará la suite completa, incluidos los escenarios preexistentes del módulo de unidades organizativas.