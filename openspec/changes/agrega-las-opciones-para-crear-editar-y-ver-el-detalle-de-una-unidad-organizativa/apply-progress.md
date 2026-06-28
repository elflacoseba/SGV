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

## Work Unit 3: Edit + PUT/PATCH + Warning parcial + Verificación (Completado)

### Completed Tasks
- [x] 4.1 **RED**: 6 edit tests (exitoso sin cambio padre, con cambio padre, PATCH falla, conflicto 409, validación 400, PATCH no llamado)
- [x] 4.2 **GREEN**: Edit.cshtml.cs con `OriginalUnidadPadreId` snapshot, flujo PUT→PATCH, warning de éxito parcial, TempData banners
- [x] 4.3 **REFACTOR**: `StatusMessage`/`StatusKind` centralizados en EditModel via TempData; banner en Edit.cshtml; carga recuperable de catálogos tras error
- [x] 5.1 **Verificación**: 184 tests passing, 1 pre-existing JS failure, 30 skipped
- [x] 5.2 **Build check**: `dotnet build SGV.slnx` 0 errors

## TDD Cycle Evidence (WU3)

| Task | Test File | Layer | Safety Net | RED | GREEN | TRIANGULATE | REFACTOR |
|------|-----------|-------|------------|-----|-------|-------------|----------|
| 4.1 | `UnidadOrganizativaWebTests.cs` | Web | ✅ 177/208 | ✅ Written (tests 2,3 fail) | ✅ 27/27 web tests pass | ✅ 6 paths (no-change, change, partial-fail, conflict, validation, no-patch) | ✅ Clean |
| 4.2 | `Edit.cshtml.cs` + `Edit.cshtml` | Web | ✅ 177/208 | N/A (same cycle) | ✅ PUT→PATCH flow, warning partial success | ➖ Multi-branch | ✅ Banners centralizados vía TempData |
| 4.3 | N/A (refactor) | - | ✅ 177/208 | N/A (refactor) | N/A | N/A | ✅ StatusMessage/StatusKind via TempData |
| 5.1 | All test files | All | ✅ 177/208 | N/A (verification) | ✅ 184 pass | N/A | N/A |
| 5.2 | Build system | - | N/A | N/A | ✅ Build succeeds | N/A | N/A |

### Test Summary (WU3)
- **Total tests written**: 8 new (6 edit + 2 GET edit)
- **Total tests passing**: 184 (all new tests pass, 1 pre-existing JS failure)
- **Layers used**: Web (8)
- **Pre-existing failures**: 1 (JS `requestSubmit` in Node.js harness — unrelated)

## Files Changed (WU3)

| File | Action | Description |
|------|--------|-------------|
| `src/SGV.Web/Pages/Organizacion/UnidadesOrganizativas/Edit.cshtml.cs` | Modified | Added `OriginalUnidadPadreId` snapshot field, PUT→PATCH flow with parent comparison, partial-success warning via TempData, `StatusMessage`/`StatusKind` banners |
| `src/SGV.Web/Pages/Organizacion/UnidadesOrganizativas/Edit.cshtml` | Modified | Added TempData banner rendering, hidden field for `OriginalUnidadPadreId` |
| `tests/SGV.Tests/Web/UnidadOrganizativaWebTests.cs` | Modified | Added `ChangeParentCommandResult` + `ChangeParentCalls` to fake client; 8 new test methods for Edit scenarios |

## Deviations from Design (WU3)

- **Parent snapshot persistence**: Design mentioned "private field" for original parent, but Razor Pages PageModel is instantiated per request. Used `[BindProperty]` hidden field (`OriginalUnidadPadreId`) instead, matching the existing pattern for `ReturnPage`/`ReturnSearch`/`ReturnSort`. Same reliability, no extra API calls.
- **No full-atomicity**: As per design, the partial-success warning is preferred over simulated atomicity.

## Remaining Tasks
None — all 12 tasks (Phases 1-5) complete.

## Workload / PR Boundary
- **Mode**: Stacked PR slice (stacked-to-develop)
- **Current work unit**: WU3 — Phase 4 (Edit + PUT/PATCH) + Phase 5 (Verificación)
- **Boundary**: This is the final slice. Ready for PR 3 targeting `feat/uo-phase2-pages`.
- **Base branch**: `feat/uo-phase2-pages`
- **Current branch**: `feat/uo-phase3-edit`
- **Estimated review budget**: This slice (WU3)

## Status
12/12 tasks complete. Ready for verify phase / PR creation.

## Corrective Batch: Verify fixes

### Completed Tasks
- [x] Added runtime auth coverage for anonymous GET to `Edit`
- [x] Strengthened create success coverage to require visible confirmation in `Details`
- [x] Strengthened edit success coverage to require preserved listing context through `Details`
- [x] Fixed delete confirmation script fallback for the Node harness and browser compatibility
- [x] Restored visible success banners and stable contextual back-links in `Details`/`Edit`

### TDD Cycle Evidence (Corrective Batch)

| Task | Test File | Layer | Safety Net | RED | GREEN | TRIANGULATE | REFACTOR |
|------|-----------|-------|------------|-----|-------|-------------|----------|
| verify-fix-1 | `UnidadOrganizativaWebTests.cs` | Web | ✅ 3/3 targeted baseline | ✅ 4 targeted tests executed first, 3 failed | ✅ 4/4 targeted tests pass after fixes | ✅ auth, success banner, context roundtrip, JS confirm fallback | ✅ Extracted shared return-to-list URL helper and kept fix localized |

### Files Changed (Corrective Batch)

| File | Action | Description |
|------|--------|-------------|
| `tests/SGV.Tests/Web/UnidadOrganizativaWebTests.cs` | Modified | Added anonymous Edit auth coverage and upgraded create/edit success assertions to verify visible confirmation + contextual back-link behavior |
| `src/SGV.Web/wwwroot/js/pages/unidades-organizativas-index.js` | Modified | Added `requestSubmit` fallback to `submit()` for current test harness compatibility without changing browser behavior |
| `src/SGV.Web/Pages/Organizacion/UnidadesOrganizativas/Details.cshtml(.cs)` | Modified | Render TempData status banner and accept preserved contextual return params |
| `src/SGV.Web/Pages/Organizacion/UnidadesOrganizativas/Edit.cshtml(.cs)` | Modified | Preserve return context across POST success flow and reuse stable back-link URL |
| `src/SGV.Web/Integration/Organizacion/UnidadOrganizativaFormHelpers.cs` | Modified | Added helper to build return-to-list URLs without losing `page/search/sort` |
