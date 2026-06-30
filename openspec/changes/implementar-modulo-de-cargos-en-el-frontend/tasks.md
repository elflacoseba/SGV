# Tasks: Implementar el módulo de Cargos en el Frontend

## Review Workload Forecast

| Campo | Valor |
|-------|-------|
| Cambios estimados | ~890 líneas (PR 1 ~230, PR 2 ~480, PR 3 ~180) |
| Riesgo 400 líneas | High |
| Chained PRs | Yes |
| División | PR 1 → PR 2 → PR 3 (stacked o feature-branch-chain) |
| TDD | strict_tdd: true (RED → GREEN → REFACTOR por escenario) |
| Scope | Listado/detalle/baja; sin create/edit, skills, eliminados ni reactivación |

Decision needed before apply: Yes
Chained PRs recommended: Yes
Chain strategy: pending
400-line budget risk: High

### Work Units sugeridos

| U | Meta | PR |
|---|------|----|
| U1 | Seams + shell: cliente, DI, override factory, `_Sidenav` | PR 1 |
| U2 | Listado activo + baja lógica confirmada | PR 2 |
| U3 | Detalle readonly | PR 3 |

## Phase 1 — Fundación y shell (PR 1)

- [x] 1.1 RED en `tests/SGV.Tests/Web/CargoWebTests.cs`: anónimo en `/organizacion/cargos` y en `/organizacion/cargos/detalles/{id}`.
- [x] 1.2 RED: `Get_Sidenav_WhenAuthenticated_ExposesCargosModule` exige `Cargos` y prohíbe placeholders (`Vacantes`, `Reclutamiento`, `Catálogos`).
- [x] 1.3 GREEN: `ICargoApiClient.cs` (`GetAllAsync/GetByIdAsync/DeleteAsync`) y records `CargoListQuery`, `CargoDeleteResult`.
- [x] 1.4 GREEN: `CargoApiClient.cs` consume `GET /api/v1/cargos[/{id}]` y `DELETE /api/v1/cargos/{id}`; traduce `ProblemDetails` a `CargoDeleteResult`.
- [x] 1.5 GREEN: `src/SGV.Web/Integration/Organizacion/CargoListItemViewModel.cs` con `CargoListItemViewModel(Id, Codigo, Nombre, Descripcion, Nivel)`.
- [x] 1.6 GREEN: registrar `ICargoApiClient` tipado en `src/SGV.Web/Program.cs` (espejo de `IUnidadOrganizativaApiClient`).
- [x] 1.7 GREEN: extender `tests/SGV.Tests/Web/SgvWebApplicationFactory.cs` con override de `ICargoApiClient`.
- [x] 1.8 GREEN: entrada `Cargos` en `src/SGV.Web/Pages/Shared/Partials/_Sidenav.cshtml` con estado activo en `/organizacion/cargos(/...)`.
- [x] 1.9 REFACTOR+VERIFY: XML doc en tipos nuevos; `dotnet build SGV.slnx` y `dotnet test --filter "~CargoWebTests"` en GREEN.

## Phase 2 — Listado activo y baja lógica (PR 2)

- [x] 2.1 RED: `Get_Index_WhenAuthenticated_RendersActiveCargosTable` (filas Detalle/Eliminar; nada de create/edit).
- [x] 2.2 RED: `Get_Index_WhenSearchHasNoResults_ShowsEmptyState`.
- [x] 2.3 RED: `Get_Index_WhenQueryFails_ShowsVisibleError`.
- [x] 2.4 RED: harness `cargos-index.js` (`DeleteConfirmation_WhenCancelled_DoesNotSubmitForm`, `_WhenConfirmed_SubmitsFormOnce`).
- [x] 2.5 RED: `Post_Delete_WhenSuccessful_RedirectsPreservingFiltersAndRefreshRemovesRow` y `_WhenConflict_ShowsFeedbackAndKeepsRowVisible` (409 `CargoConPuestosActivos`).
- [x] 2.6 GREEN: `src/SGV.Web/Pages/Organizacion/Cargos/Index.cshtml.cs` (`[Authorize]`, `OnGetAsync`, `OnPostDeleteAsync`, filtro/sort/paginación en memoria, `TempData`, `ResolveRedirectPageAsync`).
- [x] 2.7 GREEN: `src/SGV.Web/Pages/Organizacion/Cargos/Index.cshtml` (tabla Inspinia, buscador, sort links, paginación, fila vacía contextual, `?handler=Delete`).
- [x] 2.8 GREEN: `cargos-index.js` con `wireCargoDeleteConfirmation` (SweetAlert2, `reverseButtons`, español).
- [x] 2.9 REFACTOR+VERIFY: `dotnet test --filter "~CargoWebTests"` (solo listado/baja en GREEN); sin create/edit/eliminadas.

## Phase 3 — Detalle readonly (PR 3)

- [x] 3.1 RED: `Get_Details_WhenAuthenticated_ShowsCargoReadOnly` (`Codigo/Nombre/Descripcion/Nivel` + `Volver al listado`).
- [x] 3.2 RED: `Get_Details_WhenCargoNotFound_ShowsNotAvailableState`.
- [x] 3.3 GREEN: `src/SGV.Web/Pages/Organizacion/Cargos/Details.cshtml.cs` (`[Authorize]`, `OnGetAsync(id, p, search, sort)`, log + estado no disponible).
- [x] 3.4 GREEN: `src/SGV.Web/Pages/Organizacion/Cargos/Details.cshtml` con `dl` readonly y rama `IsNotFound` (sin reactivación).
- [x] 3.5 REFACTOR+VERIFY: `dotnet build SGV.slnx` y suite `CargoWebTests` completa en GREEN.

## Phase 4 — Verificación final del slice

- [ ] 4.1 `bun install && bun run build` en `src/SGV.Web`.
- [ ] 4.2 `dotnet test SGV.slnx` (sin filtro) verde: shell, unidades organizativas y cargos.
- [ ] 4.3 Confirmar que ningún archivo nuevo mencione `Crear/Editar/Habilidades/Reactivar`.
- [ ] 4.4 Preparar commits por unidad (`work-unit-commits`): PR 1 → PR 2 → PR 3 con conventional commits y tests/docs en cada commit.
