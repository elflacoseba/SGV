## Verification Report

**Change**: permitir-reactivar-las-unidades-organizativas-eliminadas
**Version**: N/A
**Mode**: Strict TDD

### Completeness
| Metric | Value |
|--------|-------|
| Tasks total | 12 |
| Tasks complete | 12 |
| Tasks incomplete | 0 |

### Build & Tests Execution
**Build**: ✅ Passed (compilación implícita dentro de `dotnet test`)
```text
dotnet test SGV.slnx --filter "FullyQualifiedName~UnidadOrganizativaRepositoryTests|FullyQualifiedName~UnidadesOrganizativasControllerTests|FullyQualifiedName~SwaggerConfigurationTests|FullyQualifiedName~UnidadOrganizativaWebTests"
  SGV.Dominio -> .../SGV.Dominio.dll
  SGV.Aplicacion -> .../SGV.Aplicacion.dll
  SGV.Infraestructura -> .../SGV.Infraestructura.dll
  SGV.Api -> .../SGV.Api.dll
  SGV.Web -> .../SGV.Web.dll
  SGV.Tests -> .../SGV.Tests.dll

dotnet test SGV.slnx
  SGV.Dominio -> .../SGV.Dominio.dll
  SGV.Aplicacion -> .../SGV.Aplicacion.dll
  SGV.Infraestructura -> .../SGV.Infraestructura.dll
  SGV.Api -> .../SGV.Api.dll
  SGV.Web -> .../SGV.Web.dll
  SGV.Tests -> .../SGV.Tests.dll
```

**Tests**: ❌ 819 passed / ❌ 1 failed / ⚠️ 143 skipped (suite completa)
```text
Filtered suite:
- Command: dotnet test SGV.slnx --filter "FullyQualifiedName~UnidadOrganizativaRepositoryTests|FullyQualifiedName~UnidadesOrganizativasControllerTests|FullyQualifiedName~SwaggerConfigurationTests|FullyQualifiedName~UnidadOrganizativaWebTests"
- Result: 91 passed, 1 failed, 30 skipped
- Failure: SGV.Tests.Api.SwaggerConfigurationTests.SwaggerDocument_DefinesBearerSchemeWithoutGlobalSecurityRequirement
  Assert.False() Failure at tests/SGV.Tests/Api/SwaggerConfigurationTests.cs:66

Full suite:
- Command: dotnet test SGV.slnx
- Result: 819 passed, 1 failed, 143 skipped
- Failure: SGV.Tests.Api.SwaggerConfigurationTests.SwaggerDocument_DefinesBearerSchemeWithoutGlobalSecurityRequirement
  Assert.False() Failure at tests/SGV.Tests/Api/SwaggerConfigurationTests.cs:66

Relevant coverage run:
- Command: dotnet test SGV.slnx --collect:"XPlat Code Coverage" --filter "FullyQualifiedName~UnidadOrganizativaRepositoryTests|FullyQualifiedName~UnidadesOrganizativasControllerTests|FullyQualifiedName~SwaggerConfigurationTests|FullyQualifiedName~UnidadOrganizativaWebTests"
- Result: 91 passed, 1 failed, 30 skipped
- Coverage artifact: tests/SGV.Tests/TestResults/72c1beb8-9397-435d-9a90-a4170cca617e/coverage.cobertura.xml
```

**Coverage**: changed-file average 62.45% → ⚠️ Below 80%

### TDD Compliance
| Check | Result | Details |
|-------|--------|---------|
| TDD Evidence reported | ✅ | `apply-progress.md` incluye tabla `TDD Cycle Evidence` |
| All tasks have tests | ⚠️ | Hay evidencia de tests para los slices funcionales, pero no todas las tareas tienen prueba dedicada y faltan escenarios verificados para Edit/Swagger |
| RED confirmed (tests exist) | ✅ | Existen los archivos de test reportados en apply (`UnidadOrganizativaRepositoryTests`, `UnidadesOrganizativasControllerTests`, `SwaggerConfigurationTests`, `UnidadOrganizativaWebTests`) |
| GREEN confirmed (tests pass) | ❌ | La suite relevante y la suite completa siguen fallando por el test Swagger preexistente |
| Triangulation adequate | ⚠️ | El flujo de `Details` está cubierto, pero `Edit` no tiene tests POST de reactivación; Swagger no verifica respuestas 200/404/409 del endpoint |
| Safety Net for modified files | ➖ | No se reportó una columna específica de safety net por archivo modificado |

**TDD Compliance**: 2/6 checks en estado plenamente satisfactorio

---

### Test Layer Distribution
| Layer | Tests | Files | Tools |
|-------|-------|-------|-------|
| Unit | 0 | 0 | xUnit |
| Integration | 13 | 4 | xUnit + WebApplicationFactory + MySqlFact |
| E2E | 0 | 0 | not installed |
| **Total** | **13** | **4** | |

Notas:
- Los tests agregados/modificados por este cambio viven todos en capas de integración.
- La cobertura del conflicto por padre inactivo proviene además de un test de aplicación preexistente ejecutado en `dotnet test SGV.slnx`: `UnidadOrganizativaServicioComandosTests.ReactivarAsync_PadreInactivo_RetornaConflictoYSinGuardar`.

---

### Changed File Coverage
| File | Line % | Branch % | Uncovered Lines | Rating |
|------|--------|----------|-----------------|--------|
| `SGV.Web/Integration/Organizacion/UnidadOrganizativaApiClient.cs` | 0.00% | 0.00% | L14, L21-L24, L26-L28, L32-L40, L44-L50, L54-L60, L64-L74, L78-L88, L92-L102, L106-L116, L120-L145, L148-L181, L184-L194 | ⚠️ Low |
| `SGV.Web/Pages/Organizacion/UnidadesOrganizativas/Index.cshtml.cs` | 88.16% | 64.10% | L114-L115, L149-L155, L160, L260-L261, L269-L272, L288-L289, L292-L293, L328-L330, L333-L335, L338 | ⚠️ Acceptable |
| `SGV.Web/Pages/Organizacion/UnidadesOrganizativas/Details.cshtml.cs` | 88.52% | 83.33% | L62-L66, L98-L99 | ⚠️ Acceptable |
| `SGV.Web/Pages/Organizacion/UnidadesOrganizativas/Edit.cshtml.cs` | 73.13% | 71.67% | L90-L94, L106-L108, L168-L195, L209-L213 | ⚠️ Low |

**Average changed file coverage**: 62.45%

---

### Assertion Quality
✅ No se detectaron aserciones triviales, tautológicas ni smoke tests vacíos en los tests modificados del cambio.

---

### Quality Metrics
**Linter**: ➖ Not available
**Type Checker**: ➖ Not available

### Spec Compliance Matrix
| Requirement | Scenario | Test | Result |
|-------------|----------|------|--------|
| `unidad-organizativa-crud` | Reactivación exitosa | `tests/SGV.Tests/Api/UnidadesOrganizativasControllerTests.cs > Reactivate_ExistentDeletedUnidad_Returns200OkWithDto`; `tests/SGV.Tests/Aplicacion/Organizacion/UnidadOrganizativaServicioComandosTests.cs > ReactivarAsync_PadreActivo_RetornaExitoYGuarda` | ⚠️ PARTIAL |
| `unidad-organizativa-crud` | Conflicto por código activo duplicado | `tests/SGV.Tests/Api/UnidadesOrganizativasControllerTests.cs > Reactivate_ConflictByActiveCode_Returns409WithProblemDetails` | ✅ COMPLIANT |
| `unidad-organizativa-crud` | Conflicto por padre inactivo o eliminado | `tests/SGV.Tests/Aplicacion/Organizacion/UnidadOrganizativaServicioComandosTests.cs > ReactivarAsync_PadreInactivo_RetornaConflictoYSinGuardar` | ✅ COMPLIANT |
| `unidad-organizativa-crud` | Unidad inexistente para reactivar | `tests/SGV.Tests/Api/UnidadesOrganizativasControllerTests.cs > Reactivate_NonExistentUnidad_Returns404WithProblemDetails` | ✅ COMPLIANT |
| `unidad-organizativa-web-listado` | Eliminación exitosa con siguiente paso visible | `tests/SGV.Tests/Web/UnidadOrganizativaWebTests.cs > Post_Delete_WhenSuccessful_ShowsReactivationBanner` | ✅ COMPLIANT |
| `unidad-organizativa-web-listado` | Reactivación exitosa desde el flujo de listado | `tests/SGV.Tests/Web/UnidadOrganizativaWebTests.cs > Post_ReactivateFromIndex_WhenSuccessful_RedirectsPreservingContext` | ✅ COMPLIANT |
| `unidad-organizativa-web-listado` | Conflicto al reactivar desde el listado | `tests/SGV.Tests/Web/UnidadOrganizativaWebTests.cs > Post_ReactivateFromIndex_WhenConflict_ShowsFeedbackAndKeepsContext` | ✅ COMPLIANT |
| `unidad-organizativa-web-detalle-edicion` | Detail muestra una acción de reactivar | `tests/SGV.Tests/Web/UnidadOrganizativaWebTests.cs > Get_Details_WhenUnidadDeleted_ShowsRecoverableStateWithReactivateAction` | ✅ COMPLIANT |
| `unidad-organizativa-web-detalle-edicion` | Edit bloquea edición y ofrece reactivación | `tests/SGV.Tests/Web/UnidadOrganizativaWebTests.cs > Get_Edit_WhenUnidadDeleted_ShowsRecoverableStateWithReactivateAction` | ✅ COMPLIANT |
| `unidad-organizativa-web-detalle-edicion` | Reactivación exitosa desde detail o edit | `tests/SGV.Tests/Web/UnidadOrganizativaWebTests.cs > Post_ReactivateFromDetails_WhenSuccessful_RedirectsToDetails` | ⚠️ PARTIAL |
| `unidad-organizativa-web-detalle-edicion` | Reactivación rechazada desde detail o edit | `tests/SGV.Tests/Web/UnidadOrganizativaWebTests.cs > Post_ReactivateFromDetails_WhenConflict_ShowsFeedback` | ⚠️ PARTIAL |
| `sgv-readonly-api` | Descubrir el endpoint de reactivación | `tests/SGV.Tests/Api/SwaggerConfigurationTests.cs > UnidadesOrganizativas_ExposesWriteOperations` | ✅ COMPLIANT |
| `sgv-readonly-api` | Documentar respuesta exitosa | (none found) | ❌ UNTESTED |
| `sgv-readonly-api` | Documentar errores previsibles | (none found) | ❌ UNTESTED |

**Compliance summary**: 9/14 escenarios plenamente compliant

### Correctness (Static Evidence)
| Requirement | Status | Notes |
|------------|--------|-------|
| Reactivación backend existente reutilizada | ✅ Implemented | `SGV.Web` consume `PATCH /api/v1/unidades-organizativas/{id}/reactivar` mediante `ReactivateAsync`. |
| Listado con CTA de reactivación | ✅ Implemented | `Index.cshtml` muestra banner con acción visible y `Index.cshtml.cs` maneja `OnPostReactivateAsync`. |
| Detail/Edit recuperables | ✅ Implemented | `Details` y `Edit` renderizan estado recuperable cuando `GetByIdAsync` devuelve `null`. |
| Documentación Swagger del endpoint | ⚠️ Partially verified | El controlador declara `200/404/409`, pero los tests modificados solo verifican presencia del path y verbo `PATCH`. |

### Coherence (Design)
| Decision | Followed? | Notes |
|----------|-----------|-------|
| Reutilizar el endpoint backend existente sin tocar lógica de negocio | ✅ Yes | El cambio se concentra en `SGV.Web`, tests y documentación del endpoint existente. |
| Recordar la última eliminación y ofrecer reactivación desde Index | ✅ Yes | Se implementó con `deletedId` en query + `TempData`, consistente con la desviación registrada en apply. |
| Estado recuperable en Details/Edit usando lecturas active-only | ✅ Yes | `GetByIdAsync == null` deriva a UI recuperable sin agregar consulta incluyendo eliminados. |
| Preservar el contexto de retorno | ⚠️ Partial | `Details/Edit` preservan `returnPage/search/sort/view`; `Index` preserva `page/search/sort` pero no `view` en delete/reactivate. |

### Issues Found
**CRITICAL**:
- `dotnet test SGV.slnx` sigue en rojo por `SGV.Tests.Api.SwaggerConfigurationTests.SwaggerDocument_DefinesBearerSchemeWithoutGlobalSecurityRequirement`. Coincide exactamente con la falla preexistente reportada en apply (misma prueba, misma aserción en línea 66). No se detectaron fallas nuevas introducidas por este cambio, pero la suite completa no queda archivablemente verde.
- La spec `unidad-organizativa-web-detalle-edicion` exige reactivación exitosa y rechazada desde “detail o edit”, pero no existen tests POST equivalentes para `Edit?handler=Reactivate`; solo hay cobertura para `Details`. Eso deja ambos escenarios en `PARTIAL` y rompe la verificación estricta de TDD.
- La spec `sgv-readonly-api` exige verificar que Swagger documente `200 OK` con `UnidadOrganizativaDto` y `404/409` para el endpoint de reactivación. Los tests actuales solo prueban que el path exista y exponga `PATCH`, por lo que esos escenarios quedan `UNTESTED`.

**WARNING**:
- La cobertura de archivos modificados quedó por debajo del umbral práctico de 80% en `UnidadOrganizativaApiClient.cs` (0.00%) y `Edit.cshtml.cs` (73.13%). El cliente HTTP nuevo no tiene prueba runtime directa porque la suite web usa `FakeUnidadOrganizativaApiClient`.
- `Index.cshtml` e `Index.cshtml.cs` no preservan `view` en los forms/redirects de delete/reactivate (`Index.cshtml:21-27`, `Index.cshtml.cs:80`, `Index.cshtml.cs:108-121`). Esto contradice el wording de las tareas 2.1/2.3 y la afirmación de apply, aunque no rompe un escenario explícito de spec.
- Los tests MySQL agregados para `UnidadOrganizativaRepositoryTests` siguen ejecutándose como `SKIP` en este entorno; la evidencia runtime del restablecimiento de flags de persistencia no se observó localmente durante verify.

**SUGGESTION**:
- Agregar pruebas directas para `UnidadOrganizativaApiClient.ReactivateAsync` o reemplazar el fake en un slice puntual para levantar la cobertura del cliente HTTP nuevo.
- Extender Swagger tests para inspeccionar `responses[200|404|409]` y el schema `UnidadOrganizativaDto`, evitando depender solo de atributos leídos estáticamente.

### Verdict
FAIL
La implementación está cerca y no introdujo regresiones nuevas, pero el cambio NO está listo para archive porque la suite completa sigue roja por una falla preexistente y todavía faltan escenarios obligatorios de spec/TDD por probar.
