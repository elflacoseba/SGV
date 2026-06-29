# Apply Progress: Reactivar y Filtrar Unidades Organizativas Eliminadas

## Mode
**Strict TDD** — cumulative artifact merged across prior implementation and this post-verify remediation batch.

## Batch Context
- **Batch**: Continuation (post-verify remediation / Phase 5)
- **Previous phases**: Phase 1, 2, 3 and 4 already implementadas en batches previos.
- **Delivery strategy**: single-pr with size:exception.

## TDD Cycle Evidence

| Task | Test File | Layer | Safety Net | RED | GREEN | TRIANGULATE | REFACTOR |
|------|-----------|-------|------------|-----|-------|-------------|----------|
| 1.1 | `tests/SGV.Tests/Aplicacion/Organizacion/UnidadOrganizativaServicioConsultaTests.cs` | Unit | ✅ Prior batch baseline | ✅ Written | ✅ Passed | ✅ 3 cases (`default`, `eliminadas`, no mezcla) | ✅ Helpers consolidados en 1.4 |
| 1.2 | `tests/SGV.Tests/Persistencia/UnidadOrganizativaRepositoryTests.cs` | Integration / MySQL | ⚠️ Prior batch sin runtime; remediado en Phase 5 | ✅ Written | ✅ Passed con MySQL real (33/33) | ✅ 3 cases de segmento + cobertura existente de paginación/filtros | ✅ Los tests de segmento siguen aislados con search tokens únicos y `SinFiltros` usa snapshot transaccional estable |
| 1.3 | `tests/SGV.Tests/Aplicacion/Organizacion/UnidadOrganizativaServicioConsultaTests.cs`, `tests/SGV.Tests/Persistencia/UnidadOrganizativaRepositoryTests.cs` | Unit + Integration | ✅ Prior batch baseline | ✅ Test drives it | ✅ Passed | ✅ Aplicación + repositorio ejercitan ambos segmentos | ✅ Clean |
| 1.4 | `tests/SGV.Tests/Aplicacion/Organizacion/UnidadOrganizativaServicioConsultaTests.cs`, `tests/SGV.Tests/Persistencia/UnidadOrganizativaRepositoryTests.cs` | Unit + Integration | ✅ Prior batch baseline | ✅ Approval/refactor | ✅ Passed | ➖ Single refactor slice | ✅ Helpers/fakes unificados |
| 2.1 | `tests/SGV.Tests/Api/UnidadesOrganizativasControllerTests.cs`, `tests/SGV.Tests/Api/SwaggerConfigurationTests.cs` | Integration | ✅ Prior batch baseline | ✅ Written | ✅ Passed | ✅ Controller + Swagger cubren param y docs | ✅ Clean |
| 2.2 | `tests/SGV.Tests/Api/UnidadesOrganizativasControllerTests.cs` | Integration | ✅ Prior batch baseline | ✅ Test drives it | ✅ Passed | ✅ `status` explícito `activas/eliminadas` + default | ✅ Clean |
| 2.3 | `tests/SGV.Tests/Web/UnidadOrganizativaWebTests.cs` | Integration | ✅ Prior batch baseline | ✅ Test drives it | ✅ Passed | ✅ Cliente/fake propagan segmento al listado | ✅ Clean |
| 2.4 | `tests/SGV.Tests/Api/ApiWebApplicationFactory.cs` + dobles web | Integration | ✅ Prior batch baseline | ✅ Approval/refactor | ✅ Passed | ➖ Single refactor slice | ✅ Fakes responden por segmento |
| 3.1 | `tests/SGV.Tests/Web/UnidadOrganizativaWebTests.cs` | Web / Integration | ✅ 41/41 | ✅ Written | ✅ Passed | ✅ 6 casos en batch previo + 4 casos explícitos en Phase 5 | ✅ Clean |
| 3.2 | `tests/SGV.Tests/Web/UnidadOrganizativaWebTests.cs` | Web / Integration | ✅ 41/41 | ✅ Test drives it | ✅ Passed | ✅ Redirects/contexto cubiertos por éxito y conflicto | ✅ Clean |
| 3.3 | `tests/SGV.Tests/Web/UnidadOrganizativaWebTests.cs` | Web / Integration | ✅ 41/41 | ✅ Test drives it | ✅ Passed | ✅ Toggle, filas y CTA por vista | ✅ Clean |
| 3.4 | `tests/SGV.Tests/Web/UnidadOrganizativaWebTests.cs` | Web / Integration | ✅ Existing tests | ✅ Approval via navigation tests | ✅ Passed | ➖ Single refactor slice | ✅ `status` preservado en retornos |
| 4.1 | Filtered slice command | Verification | ✅ Prior batch baseline | ➖ Verification task | ✅ Passed nuevamente en remediation: 109/109 (`Web` + `Swagger` + `Repository` con MySQL) | ➖ Multiple reruns by slice | ➖ None |
| 4.2 | Full suite + build commands | Verification | ✅ Prior batch baseline | ➖ Verification task | ✅ `dotnet test SGV.slnx --no-build` (846 passed, 146 skipped) + ✅ `bun run build` | ➖ Re-run under current batch | ➖ None |
| 5.1 | `tests/SGV.Tests/Web/UnidadOrganizativaWebTests.cs` | Web / Integration | ✅ 47/47 | ✅ Written first | ✅ Passed (51/51) | ✅ Added deleted-view coverage for initial load/toggle, pagination, sort and query failure | ✅ No production changes needed for web behavior |
| 5.2 | `tests/SGV.Tests/Api/SwaggerConfigurationTests.cs` | Integration | ✅ 23/23 | ✅ Written first | ✅ Failed first on response description; then passed (25/25) | ✅ Schema ref + response description assertions | ✅ Clean |
| 5.3 | `src/SGV.Api/Controllers/UnidadesOrganizativasController.cs` | API docs | ✅ 24/25 failing spec test | ✅ Test drives it | ✅ Passed | ➖ Single behavior/documentation gap | ✅ XML docs now state same `UnidadOrganizativaDto` contract and no mixed results |
| 5.4 | `tests/SGV.Tests/Persistencia/UnidadOrganizativaRepositoryTests.cs` | Integration / MySQL | ✅ Runtime env validated (`root` local, DB `SGV`) | ✅ Existing runtime tests exercised | ✅ Passed (33/33) | ✅ Segmento activas/eliminadas + no mezcla + existing query coverage | ✅ Runtime evidence rerun after isolating segment assertions with unique search tokens and stabilizing `SinFiltros` with a transaction snapshot |
| 5.5 | `openspec/.../apply-progress.md` | Artifact | N/A | ✅ Written | ✅ Updated | ➖ Single artifact merge | ✅ Cumulative evidence merged for phases 1, 2, 4 y 5 |
| 5.6 | `tests/SGV.Tests/Persistencia/UnidadOrganizativaRepositoryTests.cs` | Integration / MySQL | ✅ 33/33 repository baseline | ✅ Refactor/approval slice sobre el último caso no determinista | ✅ Passed after wrapping count + insert + paged query in one `REPEATABLE READ` transaction and rerunning MySQL runtime evidence (1/1 focused) | ✅ `SinFiltros` computes `activeCountBefore` and reads paged results from the same snapshot; los casos de segmento siguen aislados con search tokens únicos | ✅ Removed shared-DB drift without faking counts or inflating page size |

## Completed Tasks
- [x] 1.1 RED: tests de aplicación para segmento default/eliminadas/no mezcla.
- [x] 1.2 RED: tests MySQL de repositorio para segmentos.
- [x] 1.3 GREEN: propagación del segmento en aplicación y repositorio.
- [x] 1.4 REFACTOR: consolidación de helpers/fakes.
- [x] 2.1 RED: tests de controller y Swagger para `status`.
- [x] 2.2 GREEN: contrato HTTP documentado para `activas/eliminadas`.
- [x] 2.3 GREEN: cliente web serializa el segmento.
- [x] 2.4 REFACTOR: fakes de API/web responden por segmento.
- [x] 3.1 RED: tests web de toggle, vacío contextual y reactivación.
- [x] 3.2 GREEN: PageModel preserva contexto y reactivación.
- [x] 3.3 GREEN: vista Razor muestra toggle y CTA contextual.
- [x] 3.4 REFACTOR: helpers de retorno preservan `status`.
- [x] 4.1 Ejecutar slice filtrado durante los ciclos.
- [x] 4.2 Ejecutar suite completa y build frontend.
- [x] 5.1 RED: evidencia web explícita para `status=eliminadas` en carga inicial, paginación, orden y error.
- [x] 5.2 RED: assertions Swagger del mismo `UnidadOrganizativaDto` y ausencia de respuesta mixta.
- [x] 5.3 GREEN: ajuste mínimo de documentación API para cerrar el gap de Swagger.
- [x] 5.4 GREEN: evidencia runtime MySQL real para `UnidadOrganizativaRepositoryTests`.
- [x] 5.5 REFACTOR: merge del progreso acumulado en este artifact.
- [x] 5.6 REFACTOR: tests MySQL de `QueryAsync` aislados de la primera página compartida.

## Files Changed

| File | Action | What Was Done |
|------|--------|---------------|
| `tests/SGV.Tests/Web/UnidadOrganizativaWebTests.cs` | Modified | Added 4 explicit deleted-view tests for default toggle visibility, pagination links, sort links, and query failure retry state. |
| `tests/SGV.Tests/Api/SwaggerConfigurationTests.cs` | Modified | Added assertions for paged schema reuse of `UnidadOrganizativaDto` and response wording that rules out mixed results. |
| `src/SGV.Api/Controllers/UnidadesOrganizativasController.cs` | Modified | Updated XML docs for `GET /consulta` to document active/deleted views, same DTO contract, and no mixed response. |
| `tests/SGV.Tests/Persistencia/UnidadOrganizativaRepositoryTests.cs` | Modified | Made `QueryAsync_SinFiltros_RetornaTodasLasActivas` deterministic by wrapping the baseline count, inserts and paged reads in one MySQL `REPEATABLE READ` transaction snapshot, while keeping the prior segment-token isolation. |
| `openspec/changes/reactivar-y-filtrar-unidades-organizativas-eliminadas/tasks.md` | Modified | Marked Phase 5 remediation tasks as completed. |
| `openspec/changes/reactivar-y-filtrar-unidades-organizativas-eliminadas/apply-progress.md` | Modified | Corrected the cumulative TDD evidence to state exactly which MySQL `QueryAsync` cases are deterministic now. |

## Test Results
- **Safety net — web**: `dotnet test SGV.slnx --no-build --filter "FullyQualifiedName~UnidadOrganizativaWebTests"` → 47 passed.
- **Safety net — Swagger**: `dotnet test SGV.slnx --filter "FullyQualifiedName~SwaggerConfigurationTests"` → 23 passed.
- **Phase 5 RED/GREEN — web**: `dotnet test SGV.slnx --filter "FullyQualifiedName~UnidadOrganizativaWebTests"` → 51 passed after adding explicit evidence tests.
- **Phase 5 RED/GREEN — Swagger**: first run failed on `ConsultaEndpoint_ResponseDescription_StatesDeletedViewKeepsSameContractWithoutMixedResults`; second run passed with 25/25 after updating controller XML docs.
- **MySQL runtime evidence**: `ConnectionStrings__SgvDatabase="Server=localhost;Port=3306;Database=SGV;User=root;" dotnet test SGV.slnx --filter "FullyQualifiedName~UnidadOrganizativaRepositoryTests"` → 33 passed, 0 skipped, 0 failed.
- **Phase 5.6 runtime evidence — remaining `SinFiltros` case**: `ConnectionStrings__SgvDatabase="Server=localhost;Port=3306;Database=SGV;User=root;" dotnet test SGV.slnx --filter "FullyQualifiedName~UnidadOrganizativaRepositoryTests&FullyQualifiedName~QueryAsync_SinFiltros_RetornaTodasLasActivas"` → 1 passed, 0 skipped, 0 failed after the test used a single `REPEATABLE READ` transaction snapshot.
- **Filtered remediation slice**: `ConnectionStrings__SgvDatabase="Server=localhost;Port=3306;Database=SGV;User=root;" dotnet test SGV.slnx --no-build --filter "FullyQualifiedName~UnidadOrganizativaRepositoryTests|FullyQualifiedName~SwaggerConfigurationTests|FullyQualifiedName~UnidadOrganizativaWebTests"` → 109 passed.
- **Build**: `dotnet build SGV.slnx` → passed.
- **Standard full suite**: `dotnet test SGV.slnx --no-build` → 846 passed, 146 skipped, 0 failed.
- **Frontend build**: `bun run build` → passed (warnings only: `baseline-browser-mapping`, `caniuse-lite`).
- **Additional discovery**: running full suite with MySQL env override surfaces unrelated pre-existing failures in `OcupacionRepositoryTests` cleanup; not remediated in this change.

## Deviations from Design
None — implementation and remediation stay inside the approved verify gaps.

## Issues Found
- Full suite with MySQL enabled (`ConnectionStrings__SgvDatabase=... dotnet test SGV.slnx --no-build`) exposes unrelated failures in `tests/SGV.Tests/Persistencia/OcupacionRepositoryTests.cs` caused by cleanup severing required `PersonaEntity` ↔ `OcupacionEntity` relations. This is outside the scope of `reactivar-y-filtrar-unidades-organizativas-eliminadas`.

## Remaining Tasks
All change tasks are complete.

## Workload / PR Boundary
- Mode: single-pr with size:exception
- Current work unit: Phase 5 post-verify remediation
- Boundary: close verify evidence gaps only (web runtime evidence, Swagger evidence, MySQL runtime evidence, cumulative artifacts)
- Estimated review budget impact: small incremental remediation inside approved size exception

## Status
20/20 tasks complete. Ready for `sdd-verify` re-run, with the explicit caveat that a full-suite run under MySQL override reveals unrelated `OcupacionRepositoryTests` failures outside this change.
