## Verification Report

**Change**: reactivar-y-filtrar-unidades-organizativas-eliminadas
**Version**: N/A
**Mode**: Strict TDD

### Completeness
| Metric | Value |
|--------|-------|
| Tasks total | 20 |
| Tasks complete | 20 |
| Tasks incomplete | 0 |

### Build & Tests Execution
**Build**: ✅ Passed
```text
bun run build
- Result: passed
- Warnings: baseline-browser-mapping outdated, Browserslist/caniuse-lite outdated
```

**Tests**: ✅ 846 passed / ⚠️ 146 skipped / ❌ 0 failed
```text
dotnet test SGV.slnx
- Passed: 846
- Skipped: 146
- Failed: 0

dotnet test SGV.slnx --no-build --filter "FullyQualifiedName~UnidadOrganizativaWebTests"
- Passed: 51
- Skipped: 0
- Failed: 0

dotnet test SGV.slnx --no-build --filter "FullyQualifiedName~SwaggerConfigurationTests"
- Passed: 25
- Skipped: 0
- Failed: 0

ConnectionStrings__SgvDatabase="Server=localhost;Port=3306;Database=SGV;User=root;" dotnet test SGV.slnx --no-build --filter "FullyQualifiedName~UnidadOrganizativaRepositoryTests"
- Passed: 33
- Skipped: 0
- Failed: 0

ConnectionStrings__SgvDatabase="Server=localhost;Port=3306;Database=SGV;User=root;" dotnet test SGV.slnx --no-build --filter "FullyQualifiedName~UnidadOrganizativaRepositoryTests&FullyQualifiedName~QueryAsync_SinFiltros_RetornaTodasLasActivas"
- Passed: 1
- Skipped: 0
- Failed: 0

ConnectionStrings__SgvDatabase="Server=localhost;Port=3306;Database=SGV;User=root;" dotnet test SGV.slnx --no-build --collect:"XPlat Code Coverage" --filter "FullyQualifiedName~UnidadOrganizativaServicioConsultaTests|FullyQualifiedName~UnidadOrganizativaRepositoryTests|FullyQualifiedName~UnidadesOrganizativasControllerTests|FullyQualifiedName~SwaggerConfigurationTests|FullyQualifiedName~UnidadOrganizativaWebTests"
- Passed: 171
- Skipped: 0
- Failed: 0
```

**Coverage**: targeted slice collected successfully
```text
Coverage artifact:
tests/SGV.Tests/TestResults/dba19b63-872d-431d-b882-fa14a9f57030/coverage.cobertura.xml
```

### TDD Compliance
| Check | Result | Details |
|-------|--------|---------|
| TDD Evidence reported | ✅ | `apply-progress.md` incluye tabla `TDD Cycle Evidence` con 20 filas trazables |
| All tasks have tests | ✅ | 20/20 tareas con archivo/comando de prueba asociado |
| RED confirmed (tests exist) | ✅ | Los archivos reportados existen y contienen los casos declarados |
| GREEN confirmed (tests pass) | ✅ | Reejecución runtime confirmada para suite completa, web, Swagger y MySQL |
| Triangulation adequate | ✅ | Las tareas funcionales cubren default/eliminadas, no mezcla, toggle, paginación, orden, error, éxito y conflicto |
| Safety Net for modified files | ✅ | Las filas reportadas como safety net o baseline fueron revalidadas en esta corrida |

**TDD Compliance**: 6/6 checks passed

---

### Test Layer Distribution
| Layer | Tests | Files | Tools |
|-------|-------|-------|-------|
| Unit | 19 | 1 | xUnit |
| Integration | 141 | 4 | WebApplicationFactory + xUnit + MySqlFact |
| E2E | 0 | 0 | not installed |
| **Total** | **160** | **5** | |

Notas:
- La reejecución filtrada con cobertura pasó con 171 tests porque el filtro `FullyQualifiedName~UnidadesOrganizativasControllerTests` también alcanza la suite `TipoUnidadesOrganizativasControllerTests`; para la verificación del cambio solo se considera la evidencia de los 5 archivos vinculados al slice.
- No se detectaron tests E2E para este cambio y la configuración del repo no declara tooling E2E disponible.

---

### Changed File Coverage
| File | Line % | Branch % | Uncovered Lines | Rating |
|------|--------|----------|-----------------|--------|
| `SGV.Aplicacion/Organizacion/Consultas/Dtos/UnidadOrganizativaQuery.cs` | 100.00% | 100.00% | — | ✅ Excellent |
| `SGV.Aplicacion/Organizacion/Consultas/UnidadOrganizativaServicioConsulta.cs` | 100.00% | 100.00% | — | ✅ Excellent |
| `SGV.Infraestructura/Persistencia/Repositorios/UnidadOrganizativaRepository.cs` | 80.00% | 50.00% | 67-68 | ⚠️ Acceptable |
| `SGV.Api/Controllers/UnidadesOrganizativasController.cs` | 100.00% | 100.00% | — | ✅ Excellent |
| `SGV.Web/Integration/Organizacion/UnidadOrganizativaListItemViewModel.cs` | 100.00% | 100.00% | — | ✅ Excellent |
| `SGV.Web/Integration/Organizacion/UnidadOrganizativaApiClient.cs` | 0.00% | 0.00% | 78-79, 81-84, 87-88 | ⚠️ Low |
| `SGV.Web/Integration/Organizacion/UnidadOrganizativaFormHelpers.cs` | 89.79% | 88.46% | 38-40, 62, 64 | ⚠️ Acceptable |
| `SGV.Web/Pages/Organizacion/UnidadesOrganizativas/Index.cshtml.cs` | 50.00% | 50.00% | 277-278, 286-289 | ⚠️ Low |
| `SGV.Web/Pages/Organizacion/UnidadesOrganizativas/Index.cshtml` | 90.90% | 95.58% | 98, 100, 116, 118, 142, 144 | ✅ Excellent |

**Average changed file coverage**: 78.08%

---

### Assertion Quality
**Assertion quality**: ✅ All assertions verify real behavior

---

### Quality Metrics
**Linter**: ➖ Not available
**Type Checker**: ➖ Not available

### Spec Compliance Matrix
| Requirement | Scenario | Test | Result |
|-------------|----------|------|--------|
| `unidad-organizativa-web-listado` | Carga inicial del listado | `UnidadOrganizativaWebTests > Get_Index_WhenAuthenticated_DefaultsToActivasAndShowsDeletedToggle` | ✅ COMPLIANT |
| `unidad-organizativa-web-listado` | Búsqueda sin resultados en la vista seleccionada | `UnidadOrganizativaWebTests > Get_Index_WhenSearchHasNoResults_ShowsEmptyState`; `Get_Index_WhenStatusDeletedAndNoResults_ShowsContextualEmptyState` | ✅ COMPLIANT |
| `unidad-organizativa-web-listado` | Cambio de página en la vista seleccionada | `UnidadOrganizativaWebTests > Get_Index_WhenChangingPage_ShowsRequestedPageAndCurrentIndicator`; `Get_Index_WhenStatusDeleted_KeptInPaginationLinks` | ✅ COMPLIANT |
| `unidad-organizativa-web-listado` | Ordenamiento de la página visible | `UnidadOrganizativaWebTests > Get_Index_WhenSortingVisiblePage_ReordersRowsAndKeepsCurrentPage`; `Get_Index_WhenStatusDeleted_KeptInSortLinksAndCurrentPage` | ✅ COMPLIANT |
| `unidad-organizativa-web-listado` | Error al consultar el listado | `UnidadOrganizativaWebTests > Get_Index_WhenQueryFails_ShowsVisibleErrorAndKeepsSearchAvailable`; `Get_Index_WhenDeletedQueryFails_KeepsDeletedSegmentForRetry` | ✅ COMPLIANT |
| `unidad-organizativa-web-listado` | Vista de eliminadas con acción por fila | `UnidadOrganizativaWebTests > Get_Index_WhenStatusDeleted_ShowsReactivateButtonPerRow` | ✅ COMPLIANT |
| `unidad-organizativa-web-listado` | Reactivación exitosa desde la vista de eliminadas | `UnidadOrganizativaWebTests > Post_ReactivateFromDeletedList_WhenSuccessful_RedirectsToActivasWithConfirmation` | ✅ COMPLIANT |
| `unidad-organizativa-web-listado` | Conflicto al reactivar desde la vista de eliminadas | `UnidadOrganizativaWebTests > Post_ReactivateFromDeletedList_WhenConflict_StaysInDeletedWithError` | ✅ COMPLIANT |
| `unidad-organizativa-crud` | Consulta por defecto devuelve activas | `UnidadOrganizativaServicioConsultaTests > QueryAsync_PorDefecto_RetornaSoloActivas`; `UnidadesOrganizativasControllerTests > Consulta_SinStatus_PorDefectoActivas`; `UnidadOrganizativaRepositoryTests > QueryAsync_SegmentoActivas_RetornaSoloActivas` | ✅ COMPLIANT |
| `unidad-organizativa-crud` | Consulta explícita de eliminadas | `UnidadOrganizativaServicioConsultaTests > QueryAsync_ConSegmentoEliminadas_RetornaSoloEliminadas`; `UnidadesOrganizativasControllerTests > Consulta_ConStatusEliminadas_RetornaSoloEliminadas`; `UnidadOrganizativaRepositoryTests > QueryAsync_SegmentoEliminadas_RetornaSoloEliminadas` | ✅ COMPLIANT |
| `unidad-organizativa-crud` | Segmentos de lectura no se mezclan | `UnidadOrganizativaServicioConsultaTests > QueryAsync_SegmentosNoSeMezclan`; `UnidadOrganizativaRepositoryTests > QueryAsync_SegmentosNoSeMezclan` | ✅ COMPLIANT |
| `sgv-readonly-api` | Descubrir el filtro de estado del listado | `SwaggerConfigurationTests > ConsultaEndpoint_DocumentaParametroStatus`; `ConsultaEndpoint_StatusParameter_DocumentaValoresActivasYEliminadas` | ✅ COMPLIANT |
| `sgv-readonly-api` | Documentar la respuesta de eliminadas | `SwaggerConfigurationTests > ConsultaEndpoint_ResponseSchema_ReusesUnidadOrganizativaDtoForDeletedView`; `ConsultaEndpoint_ResponseDescription_StatesDeletedViewKeepsSameContractWithoutMixedResults` | ✅ COMPLIANT |
| `sgv-readonly-api` | Mantener fuera de alcance el listado mixto y el árbol | `SwaggerConfigurationTests > ConsultaEndpoint_ResponseDescription_StatesDeletedViewKeepsSameContractWithoutMixedResults`; `ConsultaEndpoint_StatusParameter_NoApareceEnArbol` | ✅ COMPLIANT |

**Compliance summary**: 14/14 escenarios compliant

### Correctness (Static Evidence)
| Requirement | Status | Notes |
|------------|--------|-------|
| Segmento binario `activas/eliminadas` | ✅ Implemented | `UnidadOrganizativaQuery`, controller, cliente web y repositorio propagan el segmento explícito |
| Vista inicial activa sin mezcla de grilla | ✅ Implemented | `IndexModel.NormalizeSegmento` cae en activas y la tabla renderiza un único segmento por request |
| Reactivación visible por fila en eliminadas | ✅ Implemented | `Index.cshtml` renderiza `Reactivar` solo cuando `Model.IsDeletedView` es verdadero |
| Éxito vuelve a activas y conflicto conserva eliminadas | ✅ Implemented | `OnPostReactivateAsync` elimina `status` en éxito y lo preserva en falla |
| Swagger mantiene mismo contrato DTO y excluye árbol/listado mixto | ✅ Implemented | XML docs de `Consulta` y tests Swagger confirman el mismo `UnidadOrganizativaDto` y ausencia de `status` en `/arbol` |

### Coherence (Design)
| Decision | Followed? | Notes |
|----------|-----------|-------|
| Parámetro binario explícito para el listado | ✅ Yes | `status` HTTP se traduce a `UnidadOrganizativaSegmentoListado` |
| Acción por fila solo en eliminadas | ✅ Yes | `Index.cshtml` alterna entre `Delete/Edit` y `Reactivate` según segmento |
| Volver a activas tras reactivación exitosa | ✅ Yes | Redirect de éxito vuelve a `Index` sin `status=eliminadas` |
| Mantener contexto con feedback en falla | ✅ Yes | Redirect de falla conserva `p`, `search`, `sort` y `status` |
| Validar persistencia MySQL del predicado | ✅ Yes | `UnidadOrganizativaRepositoryTests` pasó 33/33 con MySQL real |

### Issues Found
**CRITICAL**: None.

**WARNING**:
- `SGV.Web/Integration/Organizacion/UnidadOrganizativaApiClient.cs` quedó sin cobertura directa en el slice verificado (0% en la corrida de coverage), aunque el comportamiento web observable sí quedó cubierto mediante el fake client y las pruebas de Razor Pages.
- `SGV.Web/Pages/Organizacion/UnidadesOrganizativas/Index.cshtml.cs` mantiene cobertura baja en `ResolveRedirectPageAsync` (líneas 277-289), sin romper la spec de este cambio.
- `bun run build` pasa, pero sigue emitiendo warnings de datasets frontend desactualizados (`baseline-browser-mapping`, `caniuse-lite`).

**SUGGESTION**:
- Agregar una prueba directa para `UnidadOrganizativaApiClient.UpdateAsync` o elevar esa cobertura vía tests de integración HTTP si el equipo quiere cerrar el warning de 0%.
- Cubrir explícitamente el `catch` y el branch `currentPage <= 1` de `ResolveRedirectPageAsync` si ese helper vuelve a tocarse.

### Verdict
PASS WITH WARNINGS
El cambio cumple 20/20 tareas, 14/14 escenarios de spec y quedó revalidado con evidencia runtime real para web, Swagger y repositorio MySQL; solo quedan warnings no bloqueantes de cobertura y tooling frontend.
