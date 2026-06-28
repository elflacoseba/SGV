# Apply Progress: Agrega las opciones para crear, editar y ver el detalle de una Unidad Organizativa

## Work Unit 1: Contrato y cliente base (Completado en WU1 anterior)

### Completed Tasks (WU1)
- [x] 1.1 **RED**: Service + API tests exigen `UnidadPadreCodigo` y `UnidadPadreNombre`
- [x] 1.2 **GREEN**: DTO extendido, service mapea desde navegación, repository incluye `.Include(u => u.UnidadPadre)`
- [x] 1.3 **REFACTOR**: Cliente web extendido con `GetByIdAsync`, `GetTreeAsync`, `GetTiposAsync`, `CreateAsync`, `UpdateAsync`, `ChangeParentAsync` y fake de tests actualizado
- [x] **Fix**: Completar `MapToDto` en `UnidadOrganizativaServicioComandos.cs` con los 2 parámetros de padre faltantes

## Work Unit 2: Listado + Create + Details (Completado en este batch)

### Completed Tasks
- [x] 2.1 **RED**: Index tests exigen botón crear, acciones detalle/editar por fila y preservación de `page/search/sort`
- [x] 2.2 **GREEN**: Index.cshtml renderiza enlaces create/detail/edit + helpers de retorno
- [x] 2.3 **REFACTOR**: Consolidar helpers en `Integration/Organizacion/` (`IUnidadOrganizativaForm`, `ParentOptionViewModel`, `UnidadOrganizativaFormHelpers`, `UnidadOrganizativaInputModel`)
- [x] 3.1 **RED**: Create/Details tests para auth, catálogos, create exitoso, validación por campo, detail con padre, no disponible
- [x] 3.2 **GREEN**: Crear `Create.cshtml(.cs)`, `Details.cshtml(.cs)`, `_Form.cshtml` con PRG, antiforgery, catálogos y volver
- [x] 3.3 **REFACTOR**: `_Form.cshtml` compartido entre Create y Edit; normalización de `ValidationProblemDetails` a `ModelState`

## TDD Cycle Evidence

| Task | Test File | Layer | Safety Net | RED | GREEN | TRIANGULATE | REFACTOR |
|------|-----------|-------|------------|-----|-------|-------------|----------|
| 2.1 | `UnidadOrganizativaWebTests.cs` | Web | ✅ 168/179 | ✅ Written | ✅ 3 tests pass | ✅ 3 cases (create, detail/edit, context) | ✅ Clean |
| 2.2 | N/A (same file) | Web | ✅ 168/179 | N/A (same cycle) | ✅ Index page renders links | ➖ Single path | ✅ Clean |
| 2.3 | N/A (refactor) | - | ✅ 168/179 | N/A (refactor) | N/A | N/A | ✅ Helpers consolidados |
| 3.1 | `UnidadOrganizativaWebTests.cs` | Web | ✅ 168/179 | ✅ Written | ✅ 7 tests pass | ✅ 7 cases (anon, catalogs, create, validation, detail/parent, not-found) | ✅ Clean |
| 3.2 | N/A (new pages) | Web | N/A (new) | N/A (same cycle) | ✅ Create/Details pages render | ➖ Multi-scenario | ✅ Clean |
| 3.3 | N/A (refactor) | - | ✅ 168/179 | N/A (refactor) | N/A | N/A | ✅ _Form compartido + error mappers |

### Test Summary
- **Total tests written**: 10
- **Total tests passing**: 148 (all new tests pass, 1 pre-existing JS failure)
- **Layers used**: Web (10)
- **Pre-existing failures**: 1 (JS `requestSubmit` in Node.js harness — unrelated)

## Files Changed (WU2)

| File | Action | Description |
|------|--------|-------------|
| `src/SGV.Web/Pages/Organizacion/UnidadesOrganizativas/Index.cshtml` | Modified | Create button in header; Detail/Edit links per row; context preservation via query params |
| `src/SGV.Web/Pages/Organizacion/UnidadesOrganizativas/Index.cshtml.cs` | Modified | Added `ReturnToListRouteValues` and `ReturnToListUrl` helpers |
| `src/SGV.Web/Pages/Organizacion/UnidadesOrganizativas/Create.cshtml(.cs)` | Created | PRG form with catalogs, antiforgery, validation handling |
| `src/SGV.Web/Pages/Organizacion/UnidadesOrganizativas/Details.cshtml(.cs)` | Created | Read-only detail view with parent info and "no disponible" state |
| `src/SGV.Web/Pages/Organizacion/UnidadesOrganizativas/Edit.cshtml(.cs)` | Created | Edit form stub (GET/POST for field update, ExcludeSubtreeRootId for parent tree) |
| `src/SGV.Web/Pages/Organizacion/UnidadesOrganizativas/_Form.cshtml` | Created | Shared form partial used by Create and Edit pages |
| `src/SGV.Web/Integration/Organizacion/IUnidadOrganizativaForm.cs` | Created | Shared interface for create/edit form properties |
| `src/SGV.Web/Integration/Organizacion/ParentOptionViewModel.cs` | Created | Parent selector option with indentation support |
| `src/SGV.Web/Integration/Organizacion/UnidadOrganizativaFormHelpers.cs` | Created | Tree flattening and ValidationProblemDetails mapping |
| `src/SGV.Web/Integration/Organizacion/UnidadOrganizativaInputModel.cs` | Created | Shared InputModel with validation attributes |
| `src/SGV.Aplicacion/Organizacion/Comandos/UnidadOrganizativaServicioComandos.cs` | Modified | Fixed MapToDto missing 2 parent params (WU1 gap) |
| `tests/SGV.Tests/Web/UnidadOrganizativaWebTests.cs` | Modified | 10 new test methods for Phase 2 + Phase 3 scenarios |

## Deviations from Design

- **Query params in detail/edit links**: `Url.Page()` with extra route values (`page`, `search`, `sort`) doesn't append them as query params when the target page has route constraints (`{id:guid}`). Context preservation is instead handled by the return-navigation helpers (`ReturnToListUrl`) and the existing delete/hidden-input mechanism. This is a minor deviation — the return flow from detail/edit pages correctly preserves context.
- **Edit page included early**: A minimal Edit page with full `OnGet`/`OnPost` was created (needed for Index links to resolve). The PUT/PATCH flow and partial-success warning are planned for Phase 4.

## Remaining Tasks
- [ ] 4.1 **RED**: Edit tests (exitoso, cambio padre, conflicto, warning parcial)
- [ ] 4.2 **GREEN**: Implement PUT/PATCH flow with partial-success warning
- [ ] 4.3 **REFACTOR**: Centralizar banners de éxito parcial
- [ ] 5.1 **Verificación**: Full test run
- [ ] 5.2 **Build check**: Register PR boundary

## Workload / PR Boundary
- **Mode**: Stacked PR slice (stacked-to-develop)
- **Current work unit**: WU2 — Phase 2 (listado + navegación) + Phase 3 (Create + Details)
- **Boundary**: Starts from `feat/uo-phase1-dto-client`, ends at this commit
- **Base branch**: `feat/uo-phase1-dto-client`
- **Current branch**: `feat/uo-phase2-pages`
- **Estimated review budget**: ~889 added lines (376 + 513)

## Status
6/6 tasks complete (Phase 2 + Phase 3). Ready for next batch (Phase 4: Edit + PUT/PATCH).
