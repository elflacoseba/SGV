# Apply Progress: Reactivar y Filtrar Unidades Organizativas Eliminadas

## Mode
**Strict TDD** — all cycles completed with RED/GREEN/REFACTOR.

## Batch Context
- **Batch**: Continuation (Phase 3 + Phase 4)
- **Previous phases**: Phase 1 (consulta segmentada backend) and Phase 2 (contrato HTTP y cliente web) completed in prior batch.
- **Delivery strategy**: single-pr with size:exception.

## TDD Cycle Evidence

| Task | Test File | Layer | Safety Net | RED | GREEN | TRIANGULATE | REFACTOR |
|------|-----------|-------|------------|-----|-------|-------------|----------|
| 3.1 | `tests/SGV.Tests/Web/UnidadOrganizativaWebTests.cs` | Web/Integration | ✅ 41/41 | ✅ Written | ✅ Passed | ✅ 6 tests | ✅ Clean |
| 3.2 | — | PageModel | N/A (existing) | ✅ Test drives it | ✅ Passed | ➖ Via 3.1 tests | ✅ Clean |
| 3.3 | — | Razor View | N/A (existing) | ✅ Test drives it | ✅ Passed | ➖ Via 3.1 tests | ✅ Clean |
| 3.4 | `UnidadOrganizativaFormHelpers.cs` | Static | ✅ Existing tests | N/A (refactor) | ✅ Approval via existing tests | ➖ Single | ✅ Added status param |

## Completed Tasks
- [x] Phase 1: Consulta segmentada backend (completed in prior batch)
- [x] Phase 2: Contrato HTTP y cliente web (completed in prior batch)
- [x] Phase 3: Listado web y reactivación
- [x] Phase 4: Verificación del slice

## Files Changed

| File | Action | What Was Done |
|------|--------|---------------|
| `tests/SGV.Tests/Web/UnidadOrganizativaWebTests.cs` | Modified | Added 6 new tests: toggle active/deleted, contextual empty state, Reactivate button per row, success redirect to activas, conflict stays in deleted |
| `src/SGV.Web/Pages/Organizacion/UnidadesOrganizativas/Index.cshtml.cs` | Modified | Added Segmento property, NormalizeSegmento(), IsDeletedView; updated OnGetAsync, OnPostReactivateAsync, OnPostDeleteAsync with status param; updated all route builders (ReturnToList, Create, Details, Edit, ViewToggle) with status |
| `src/SGV.Web/Pages/Organizacion/UnidadesOrganizativas/Index.cshtml` | Modified | Added Activas/Eliminadas toggle button group, contextual empty state message, Reactivate button per row in deleted view, hidden status inputs in search/pagination/forms |
| `src/SGV.Web/Integration/Organizacion/UnidadOrganizativaFormHelpers.cs` | Modified | Added `status` parameter to `BuildReturnToListUrl` |
| `src/SGV.Web/Pages/Organizacion/UnidadesOrganizativas/Details.cshtml.cs` | Modified | Added ReturnStatus property, accepted returnStatus in OnGetAsync, propagated in redirects |
| `src/SGV.Web/Pages/Organizacion/UnidadesOrganizativas/Details.cshtml` | Modified | Added returnStatus hidden input in reactivate form, added to Edit link |
| `src/SGV.Web/Pages/Organizacion/UnidadesOrganizativas/Edit.cshtml.cs` | Modified | Added ReturnStatus property, accepted returnStatus in OnGetAsync, propagated in redirects and reactivation |
| `src/SGV.Web/Pages/Organizacion/UnidadesOrganizativas/Edit.cshtml` | Modified | Added returnStatus hidden inputs in both reactivate form and main edit form |
| `src/SGV.Web/Pages/Organizacion/UnidadesOrganizativas/Create.cshtml.cs` | Modified | Added ReturnStatus property, accepted returnStatus in OnGetAsync, propagated in redirect |
| `openspec/changes/reactivar-y-filtrar-unidades-organizativas-eliminadas/tasks.md` | Modified | All tasks marked [x] |

## Test Results
- **Full suite**: 840 passed, 146 skipped, 0 failed
- **Web tests**: 47 passed (41 existing + 6 new), 0 failed
- **Frontend build**: `bun run build` passed

## Deviations from Design
None — implementation matches design.md.

## Issues Found
None.

## Remaining Tasks
All tasks are complete. Ready for `sdd-verify`.

## Workload / PR Boundary
- Mode: single-pr with size:exception
- Current work unit: Phase 3 (listado web y reactivación) + Phase 4 (verificación)
- Boundary: continuation batch completing all Phase 3 and Phase 4 tasks
- Estimated review budget impact: size:exception previously approved
