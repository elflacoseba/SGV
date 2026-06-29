## Verification Report

**Change**: reactivar-y-filtrar-unidades-organizativas-eliminadas
**Version**: N/A
**Mode**: Strict TDD

### Completeness
| Metric | Value |
|--------|-------|
| Tasks total | 14 |
| Tasks complete | 14 |
| Tasks incomplete | 0 |

### Build & Tests Execution
**Build**: ✅ Passed
```text
bun run build
- Result: passed
- Warnings: baseline-browser-mapping outdated, Browserslist/caniuse-lite outdated
```

**Tests**: ✅ 840 passed / ⚠️ 146 skipped / ❌ 0 failed
```text
dotnet test SGV.slnx
- Passed: 840
- Skipped: 146
- Failed: 0

dotnet test SGV.slnx --filter "FullyQualifiedName~UnidadOrganizativaServicioConsultaTests|FullyQualifiedName~UnidadOrganizativaRepositoryTests|FullyQualifiedName~UnidadesOrganizativasControllerTests|FullyQualifiedName~SwaggerConfigurationTests|FullyQualifiedName~UnidadOrganizativaWebTests"
- Passed: 132
- Skipped: 33
- Failed: 0
- Note: all MySQL-backed UnidadOrganizativaRepositoryTests were skipped in this environment
```

**Coverage**: ⚠️ Available via `dotnet test SGV.slnx --collect:"XPlat Code Coverage"`
```text
Coverage artifact:
tests/SGV.Tests/TestResults/912b8a50-0c9e-4357-95b6-bb4e3e1d0a99/coverage.cobertura.xml
```

### TDD Compliance
| Check | Result | Details |
|-------|--------|---------|
| TDD Evidence reported | ✅ | `apply-progress.md` incluye tabla `TDD Cycle Evidence` |
| All tasks have tests | ⚠️ | 4/14 tareas tienen evidencia TDD detallada en el artifact actual; fases 1, 2 y 4 no quedaron desglosadas por tarea |
| RED confirmed (tests exist) | ✅ | `tests/SGV.Tests/Web/UnidadOrganizativaWebTests.cs` existe y cubre la fila 3.1 |
| GREEN confirmed (tests pass) | ⚠️ | La evidencia explícita de la tabla actual pasa; las tareas MySQL de repositorio no ejecutaron runtime por tests skip |
| Triangulation adequate | ⚠️ | La tabla actual reporta triangulación fuerte solo para 3.1; el resto depende de cobertura indirecta o evidencia no detallada |
| Safety Net for modified files | ⚠️ | Solo la fase 3 documenta safety net explícito en el artifact actual |

**TDD Compliance**: 2/6 checks plenamente verificados sin reservas

---

### Test Layer Distribution
| Layer | Tests | Files | Tools |
|-------|-------|-------|-------|
| Unit | 19 | 1 | xUnit |
| Integration | 135 | 4 | WebApplicationFactory + xUnit + MySqlFact |
| E2E | 0 | 0 | not installed |
| **Total** | **154** | **5** | |

Notas:
- Los tests de persistencia existen pero quedaron `SKIP` por falta de ejecución MySQL en este entorno.
- No se detectaron capas E2E para este cambio.

---

### Changed File Coverage
| File | Line % | Branch % | Rating | Notes |
|------|--------|----------|--------|-------|
| `src/SGV.Aplicacion/Organizacion/Consultas/Dtos/UnidadOrganizativaQuery.cs` | 100% | 100% | ✅ Excellent | Cubierto por tests de aplicación/API |
| `src/SGV.Aplicacion/Organizacion/Consultas/UnidadOrganizativaServicioConsulta.cs` | 100% | 75% | ⚠️ Acceptable | Falta branch completo en combinaciones internas |
| `src/SGV.Api/Controllers/UnidadesOrganizativasController.cs` | 97.43% | 87.50% | ✅ Excellent | Buen coverage del contrato HTTP |
| `src/SGV.Web/Pages/Organizacion/UnidadesOrganizativas/Index.cshtml` | 90.90% | 95.58% | ✅ Excellent | Cobertura fuerte del markup listado |
| `src/SGV.Web/Pages/Organizacion/UnidadesOrganizativas/Index.cshtml.cs` | 83.73% | 65.15% | ⚠️ Acceptable | Branches faltantes en redirects/contexto |
| `src/SGV.Web/Pages/Organizacion/UnidadesOrganizativas/Details.cshtml.cs` | 100% | 50% | ⚠️ Acceptable | Branches de reactivación/errores no totalmente cubiertos |
| `src/SGV.Web/Pages/Organizacion/UnidadesOrganizativas/Edit.cshtml.cs` | 100% | 75% | ⚠️ Acceptable | Cobertura parcial de ramas de conflicto |
| `src/SGV.Web/Integration/Organizacion/UnidadOrganizativaFormHelpers.cs` | 89.79% | 88.46% | ⚠️ Acceptable | Cobertura razonable |
| `src/SGV.Web/Integration/Organizacion/UnidadOrganizativaApiClient.cs` | 0% | 0% | ⚠️ Low | No hubo ejecución directa del cliente HTTP tipado en esta suite |
| `src/SGV.Infraestructura/Persistencia/Repositorios/UnidadOrganizativaRepository.cs` | 0% | 0% | ⚠️ Low | Los tests MySQL quedaron `SKIP`; sin evidencia runtime real del repositorio |

**Average changed file coverage**: mixed / no confiable para los archivos MySQL y el cliente tipado por falta de ejecución directa

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
| `unidad-organizativa-web-listado` | Carga inicial del listado | `UnidadOrganizativaWebTests > Get_Index_WhenAuthenticated_RendersShellMenuAndInitialTable` | ⚠️ PARTIAL |
| `unidad-organizativa-web-listado` | Búsqueda sin resultados en la vista seleccionada | `UnidadOrganizativaWebTests > Get_Index_WhenSearchHasNoResults_ShowsEmptyState`; `Get_Index_WhenStatusDeletedAndNoResults_ShowsContextualEmptyState` | ✅ COMPLIANT |
| `unidad-organizativa-web-listado` | Cambio de página en la vista seleccionada | `UnidadOrganizativaWebTests > Get_Index_WhenChangingPage_ShowsRequestedPageAndCurrentIndicator` | ⚠️ PARTIAL |
| `unidad-organizativa-web-listado` | Ordenamiento de la página visible | `UnidadOrganizativaWebTests > Get_Index_WhenSortingVisiblePage_ReordersRowsAndKeepsCurrentPage` | ⚠️ PARTIAL |
| `unidad-organizativa-web-listado` | Error al consultar el listado | `UnidadOrganizativaWebTests > Get_Index_WhenQueryFails_ShowsVisibleErrorAndKeepsSearchAvailable` | ⚠️ PARTIAL |
| `unidad-organizativa-web-listado` | Vista de eliminadas con acción por fila | `UnidadOrganizativaWebTests > Get_Index_WhenStatusDeleted_ShowsReactivateButtonPerRow` | ✅ COMPLIANT |
| `unidad-organizativa-web-listado` | Reactivación exitosa desde la vista de eliminadas | `UnidadOrganizativaWebTests > Post_ReactivateFromDeletedList_WhenSuccessful_RedirectsToActivasWithConfirmation` | ✅ COMPLIANT |
| `unidad-organizativa-web-listado` | Conflicto al reactivar desde la vista de eliminadas | `UnidadOrganizativaWebTests > Post_ReactivateFromDeletedList_WhenConflict_StaysInDeletedWithError` | ✅ COMPLIANT |
| `unidad-organizativa-crud` | Consulta por defecto devuelve activas | `UnidadOrganizativaServicioConsultaTests > QueryAsync_PorDefecto_RetornaSoloActivas`; `UnidadesOrganizativasControllerTests > Consulta_SinStatus_PorDefectoActivas` | ✅ COMPLIANT |
| `unidad-organizativa-crud` | Consulta explícita de eliminadas | `UnidadOrganizativaServicioConsultaTests > QueryAsync_ConSegmentoEliminadas_RetornaSoloEliminadas`; `UnidadesOrganizativasControllerTests > Consulta_ConStatusEliminadas_RetornaSoloEliminadas` | ✅ COMPLIANT |
| `unidad-organizativa-crud` | Segmentos de lectura no se mezclan | `UnidadOrganizativaServicioConsultaTests > QueryAsync_SegmentosNoSeMezclan` | ✅ COMPLIANT |
| `sgv-readonly-api` | Descubrir el filtro de estado del listado | `SwaggerConfigurationTests > ConsultaEndpoint_DocumentaParametroStatus`; `ConsultaEndpoint_StatusParameter_DocumentaValoresActivasYEliminadas` | ✅ COMPLIANT |
| `sgv-readonly-api` | Documentar la respuesta de eliminadas | `SwaggerConfigurationTests > ConsultaEndpoint_StatusParameter_DocumentaValoresActivasYEliminadas` | ⚠️ PARTIAL |
| `sgv-readonly-api` | Mantener fuera de alcance el listado mixto y el árbol | `SwaggerConfigurationTests > ConsultaEndpoint_StatusParameter_NoApareceEnArbol` | ⚠️ PARTIAL |

**Compliance summary**: 8/14 escenarios totalmente conformes; 6/14 con evidencia parcial

### Correctness (Static Evidence)
| Requirement | Status | Notes |
|------------|--------|-------|
| Segmento binario `activas/eliminadas` | ✅ Implemented | `UnidadOrganizativaQuery`, controller, repo y cliente web propagan el segmento |
| Reactivación por fila solo en eliminadas | ✅ Implemented | `Index.cshtml` muestra botón `Reactivar` únicamente en vista eliminadas |
| Redirección a activas tras éxito | ✅ Implemented | `Index.cshtml.cs` elimina `status=eliminadas` en success redirect |
| Preservación de contexto | ✅ Implemented | `status`, `p`, `search`, `sort` se propagan en forms/links/helpers |

### Coherence (Design)
| Decision | Followed? | Notes |
|----------|-----------|-------|
| Parámetro binario explícito para el listado | ✅ Yes | `status` HTTP -> `UnidadOrganizativaSegmentoListado` |
| Acción por fila solo en eliminadas | ✅ Yes | Render condicional en `Index.cshtml` |
| Volver a activas tras reactivación exitosa | ✅ Yes | Redirect sin `status` en `OnPostReactivateAsync` |
| Mantener contexto con feedback en falla | ✅ Yes | Redirect conserva segmento al fallar |
| Validar persistencia MySQL del predicado | ⚠️ Partial | El código existe, pero los tests MySQL no corrieron en este entorno |

### Issues Found
**CRITICAL**:
- Los escenarios `Carga inicial del listado`, `Cambio de página en la vista seleccionada`, `Ordenamiento de la página visible` y `Error al consultar el listado` no tienen evidencia runtime completa para la vista `eliminadas`; hoy solo hay cobertura parcial centrada en `activas` o en la mera presencia del toggle.
- La spec `sgv-readonly-api` no quedó totalmente probada: falta evidencia runtime que confirme en Swagger que la respuesta documentada de `consulta` sigue referenciando explícitamente `UnidadOrganizativaDto` también para la vista de eliminadas y que no se documenta una respuesta mixta.
- `tests/SGV.Tests/Persistencia/UnidadOrganizativaRepositoryTests.cs` quedó completamente `SKIP`; por lo tanto no hay evidencia runtime MySQL para el predicado real de `UnidadOrganizativaRepository.QueryAsync`, pese a que el cambio toca persistencia.

**WARNING**:
- El `apply-progress.md` actual no deja trazabilidad TDD detallada para las tareas de fases 1, 2 y 4; solo la continuación de fase 3 está tabulada.
- El reporte de cobertura deja `UnidadOrganizativaApiClient.cs` y `UnidadOrganizativaRepository.cs` con 0% efectivo en esta corrida.
- `bun run build` pasa, pero emite warnings de datasets frontend desactualizados (`baseline-browser-mapping`, `caniuse-lite`).

**SUGGESTION**:
- Agregar tests web explícitos para paginación, orden y error de carga en `status=eliminadas`.
- Agregar assertions Swagger sobre el schema/respuesta de `GET /api/v1/unidades-organizativas/consulta` para la vista eliminadas.
- Repetir verify en un entorno con MySQL disponible para convertir la evidencia parcial del repositorio en evidencia runtime real.

### Verdict
FAIL
Hay implementación funcional visible y la suite general está en verde, pero faltan escenarios spec con evidencia runtime completa y la verificación MySQL del repositorio no se ejecutó.
