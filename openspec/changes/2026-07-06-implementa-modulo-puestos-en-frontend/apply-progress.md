# Apply-Progress: Implementar el módulo de Puestos en el Frontend

## Estado global

- Cambio: `2026-07-06-implementa-modulo-puestos-en-frontend`
- Modo: Strict TDD (`openspec/config.yaml` → `strict_tdd: true`)
- Estrategia de entrega: chained PRs — `feature-branch-chain` (`stacked-to-develop`), 5 PRs.
- PR actual: **PR 1 / 5** — Seams + shell + sidenav.
- Branch: `feat/puestos-pr1-seams-shell` (base `develop`).
- Estado PR: no abierta todavía (rama local; el orquestador gestiona `gh pr create`).
- Build: `dotnet build SGV.slnx` → success, **0 warnings, 0 errors**.
- Frontend: `bun install` + `bun run build` (en `src/SGV.Web`) → success.
- Tests slice PR 1: `--filter "FullyQualifiedName~PuestoWebSeamTests|FullyQualifiedName~PuestosApiClientTests|FullyQualifiedName~IPuestosApiClientContractTests"` → **47/47 PASS**.
- Suite web completa (`FullyQualifiedName~SGV.Tests.Web`): **353/353 PASS** (sin regresión por editar `_Sidenav.cshtml` y `SgvWebApplicationFactory.cs`).
- Suite completa `dotnet test SGV.slnx`: 1463 PASS / **12 FAIL** — las 12 fallas son exclusivamente `OcupacionRepositoryTests` (bug pre-existente #59, MySQL real, tipo `ActivePuestoIdUnique INT` vs `PuestoId CHAR(36)`; documentado en `AGENTS.md`). Este slice es frontend-only y **no toca** Dominio/Aplicación/Infraestructura/Api; las 12 fallas son baseline, no regresión.

## Resumen ejecutivo

PR 1 deja listos los seams (cliente tipado `IPuestosApiClient`/`PuestosApiClient`,
view models, `PuestoDeleteResult`, `PuestoListQuery`), su registro DI en
`Program.cs` (Timeout=10s + `ApiBearerTokenHandler`), el override
`WithPuestosApiClient` en `SgvWebApplicationFactory`, el `FakePuestosApiClient`
y la `PuestoWebTestFixture`, más la entry colapsable "Puestos" en el sidenav.
Las páginas (`Index`/`Details`/`Create`/`Edit`) llegan en PR 2/PR 3A/3B/3C. No
se crearon páginas placeholder en PR 1 (a diferencia del precedente de Cargos):
el slice queda acotado exactamente a los archivos de `tasks.md §3`.

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
| 1.7 | `PuestoWebSeamTests::Get_Sidenav_WhenAuthenticated_ExposesPuestosModule` + `::Get_Sidenav_WhenAuthenticated_DoesNotExposeUnimplementedModules` | `_Sidenav.cshtml` (entry `aria-controls="puestos"`, `ti ti-briefcase`, `Listado`/`Nuevo`) | Sin SCSS propio; reusa `side-nav-item`/`side-nav-link` | `d0ab465b` |
| 1.8 | n/a (refactor + verify) | n/a | Build 0 warn/0 err · slice 47/47 PASS · `bun run build` verde | `<docs>` |

### Test Summary

- **Total tests nuevos**: 47 (`PuestosApiClientTests` 25 · `IPuestosApiClientContractTests` 7 · `PuestoWebSeamTests` 15 incluidas Theory rows).
- **Passing**: 47/47 en el slice; 353/353 en toda la suite web.
- **Layers**: Unit (handler stub + record shape + reflexión) e Integration (`WebApplicationFactory` para el sidenav autenticado).
- **Approval tests** (refactor de código existente): 0 — PR 1 sólo agrega tipos/registro; los únicos archivos preexistentes editados (`Program.cs`, `_Sidenav.cshtml`, `SgvWebApplicationFactory.cs`) son extensiones aditivas cubiertas por `ProductionRegistration_*`, el sidenav render y toda la suite web verde.
- **Bug latente evitado**: `DeleteAsync` incluye `catch (System.Text.Json.JsonException)` desde el inicio (mismo hallazgo que el precedente de Cargos), cubierto por `DeleteAsync_Http500WithNonJsonBody_ReturnsFailedResultWithoutCrashing`.

### Commits del PR 1

| SHA | Tipo | Mensaje |
|---|---|---|
| `5496989c` | feat | `feat(puestos-web): agregar cliente HTTP tipado y contratos de Puestos` |
| `d0ab465b` | feat | `feat(puestos-web): registrar seam de Puestos y entry del sidenav` |
| _pendiente_ | docs | `docs(sdd): registrar evidencia TDD de PR 1 de Puestos` |

### Hallazgos / desviaciones

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
- **Presupuesto de revisión:** PR 1 suma **1302 add / 3 del** (11 archivos), por
  encima del forecast ~770 y del budget de 400. El grueso son tests (~1013
  líneas; producción neta ≈ 289) por la cobertura completa del contrato de
  transporte (6 métodos × propagación + cancelación). Aceptado dentro de la
  estrategia `feature-branch-chain` ya confirmada por el orquestador.

## Branch state

- Branch: `feat/puestos-pr1-seams-shell`
- Base: `develop`
- Head SHA: `d0ab465b` (antes del commit docs; se actualiza al cerrar)

```
 src/SGV.Web/Integration/Organizacion/IPuestosApiClient.cs        |  43 +++
 src/SGV.Web/Integration/Organizacion/PuestoListItemViewModel.cs  |  47 +++
 src/SGV.Web/Integration/Organizacion/PuestosApiClient.cs         | 156 ++++++++
 src/SGV.Web/Pages/Shared/Partials/_Sidenav.cshtml                |  31 ++
 src/SGV.Web/Program.cs                                           |  12 +
 tests/SGV.Tests/Web/Puesto/FakePuestosApiClient.cs              | 162 +++++++++
 tests/SGV.Tests/Web/Puesto/IPuestosApiClientContractTests.cs    | 133 +++++++
 tests/SGV.Tests/Web/Puesto/PuestoWebSeamTests.cs                | 175 +++++++++
 tests/SGV.Tests/Web/Puesto/PuestoWebTestFixture.cs              | 118 ++++++
 tests/SGV.Tests/Web/Puesto/PuestosApiClientTests.cs             | 404 +++++++++++
 tests/SGV.Tests/Web/SgvWebApplicationFactory.cs                 |  24 +-
 11 files changed, 1302 insertions(+), 3 deletions(-)
```

## Sugerencias para PR 2

- Al crear `Index.cshtml(.cs)`, agregar en `PuestoWebSeamTests` (o
  `PuestoIndexPageTests`) los tests de estado `active` diferidos:
  `Get_Sidenav_WhenOnPuestosRoute_SubmenuIsActive` y
  `Get_Sidenav_WhenOnPuestosSubroute_SubmenuIsExpanded`.
- `FakePuestosApiClient` ya modela baja lógica (`_deletedIds`) y captura de
  invocaciones — PR 2 puede usar `DeleteCalls`/`ReactivateCalls` y `GetAllResult`
  sin extenderlo. `PuestoListQuery` (filtros en memoria) ya está disponible.
- El toggle "Eliminadas" deshabilitado (decisión locked #2) usa `GetAllAsync`
  como fuente única; no hay endpoint `/consulta` segmentado.
