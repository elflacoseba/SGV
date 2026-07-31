```yaml
schema: gentle-ai.verify-result/v1
evidence_revision: sha256:90fec0aa245c21b6886cf17fde86b0e72cb8e35b0a6660c4f1f0a234630c90c5
verdict: fail
blockers: 1
critical_findings: 1
requirements: 8/8
scenarios: 19/19
scenarios_untested: 0
test_command: dotnet test SGV.slnx --no-build --no-restore --nologo --filter "FullyQualifiedName~Vacantes"
test_exit_code: 0
test_output_hash: sha256:7467b5b0391790c45ab5d6492c755b5f47b015b2a2a1300b838d5d2dcaef5bc3
build_command: dotnet build SGV.slnx --no-restore --nologo
build_exit_code: 0
build_output_hash: sha256:fa5948d34c49fd45d724075da4a2e5cf54702667061002c9d02645bc4fedb26f
focused_web_test_command: dotnet test tests/SGV.Tests/SGV.Tests.csproj --no-build --no-restore --nologo --filter "FullyQualifiedName~SGV.Tests.Web.Vacantes"
focused_web_test_exit_code: 0
focused_web_test_output_hash: sha256:46729768c6bbbcda53a3b756761c32fd95d4f1572c8ade020f949fb8552df9a7
web_suite_test_command: dotnet test SGV.slnx --no-build --no-restore --nologo --filter "FullyQualifiedName~Web"
web_suite_test_exit_code: 0
web_suite_test_output_hash: sha256:b59671f246211cb2c4827203a82f48e390ffde10885d0fdba36a10ea8a39e058
full_suite_test_command: dotnet test SGV.slnx --no-build --no-restore --nologo
full_suite_test_exit_code: 1
full_suite_test_output_hash: sha256:29da0bb101bbdaa3d87390ea4797173377e79836dff9a7169aec55eff4d76ebc
mysql_available: true
mysql_tests_skipped: 0
branch: develop
head_sha: a374fc1cd6c2aeec793b45f6c4f2869d6646e6cc
```

# Verify Report 5 — cierre de cobertura runtime de `vacante-web`

**Change archivado**: `2026-07-30-feature-implementar-modulo-vacantes`  
**Issue de seguimiento**: `#234`  
**Modo**: Strict TDD, remediación retrospectiva de cobertura sobre comportamiento ya implementado  
**Spec validada**: `specs/vacante-web/spec.md` — 8 requisitos, 19 escenarios

## Resultado ejecutivo

Se agregaron exactamente seis pruebas de integración Web para S8, S10, S11, S16, S17 y S19. Los 19 escenarios de `vacante-web` tienen ahora cobertura runtime: **19/19 COMPLIANT, 0 UNTESTED**.

La compilación, el filtro Vacantes y la suite Web completa pasan. El veredicto global permanece en **FAIL** porque la suite completa de la solución encontró tres fallos fuera del diff; uno de Swagger se reproduce aislado y los dos de MySQL pasan aislados.

## Cambios de cobertura

| Escenario | Prueba agregada | Comportamiento verificado |
|-----------|-----------------|--------------------------|
| S8 — falla de carga de catálogos | `Get_Create_WhenCatalogLoadFails_ShowsRecoverableErrorAndDisablesSave` | Error recuperable visible y acción Guardar deshabilitada. |
| S10 — validación por campo | `Post_Create_WhenApiReturnsFieldValidationError_ShowsFieldErrorAndPreservesInput` | Error asociado a `Input.Motivo`, valor ingresado conservado y backend invocado una vez. |
| S11 — conflicto Web | `Post_Create_WhenApiReturnsConflict_ShowsMessageAndPreservesInput` | Mensaje de conflicto visible, formulario conservado y sin redirección exitosa. |
| S16 — Details sin historial | `Get_Details_WhenNoHistory_ShowsEmptyState` | Mensaje explícito “No hay historial previo.”. |
| S17 — vacante inexistente | `Get_Details_WhenVacanteDoesNotExist_ShowsRecoverableStateWithReturnLink` | Estado no disponible y camino visible de retorno al listado. El escenario queda cubierto mediante Details, según el comportamiento alternativo permitido por el spec. |
| S19 — sidenav activo | `Get_Index_MarksVacantesSidenavGroupActive` | El grupo `aria-controls="vacantes"` contiene la clase semántica `active` en la ruta del módulo. |

## Evidencia de ejecución

| Validación | Resultado |
|------------|-----------|
| Baseline previo del namespace Web Vacantes | ✅ 16 passed, 0 failed, 0 skipped |
| Build de solución | ✅ 0 errores, 2 warnings `NU1510` preexistentes |
| Namespace Web Vacantes | ✅ 22 passed, 0 failed, 0 skipped — aumento exacto de 6 |
| Filtro histórico `~Vacantes` | ✅ 63 passed, 0 failed, 0 skipped — antes 57 |
| Suite Web completa | ✅ 1377 passed, 0 failed, 0 skipped — antes 1371 |
| Suite completa de solución | ❌ 3313 passed, 3 failed, 0 skipped, total 3316 |

## Matriz completa de compliance runtime

| Id | Escenario | Evidencia runtime | Resultado |
|----|-----------|-------------------|-----------|
| S1 | Usuario autenticado abre Index | `Get_Index_WhenAuthenticated_Returns200AndDefaultsToAbiertas` | ✅ COMPLIANT |
| S2 | Usuario sin rol accede a Create | `Get_Create_WhenAuthenticatedWithoutMutationRole_RedirectsToAccessDenied` | ✅ COMPLIANT |
| S3 | Usuario anónimo intenta acceder | `Get_Index_WhenAnonymous_RedirectsToSignIn` | ✅ COMPLIANT |
| S4 | Vista por defecto muestra abiertas | `Get_Index_WhenAuthenticated_Returns200AndDefaultsToAbiertas` | ✅ COMPLIANT |
| S5 | Cambio de segmento en la UI | `Get_Index_SegmentsNeverMixRows` | ✅ COMPLIANT |
| S6 | Backend no disponible | `Get_Index_WhenApiReturns5xx_ShowsRecoverableError` | ✅ COMPLIANT |
| S7 | Catálogos cargados en Create | `Get_Create_WhenMutationRole_RendersFormWithCatalogs` | ✅ COMPLIANT |
| S8 | Falla la carga de catálogos | `Get_Create_WhenCatalogLoadFails_ShowsRecoverableErrorAndDisablesSave` | ✅ COMPLIANT |
| S9 | Create exitoso | `Post_Create_WhenSuccessful_RedirectsToDetails` | ✅ COMPLIANT |
| S10 | Error de validación por campo | `Post_Create_WhenApiReturnsFieldValidationError_ShowsFieldErrorAndPreservesInput` | ✅ COMPLIANT |
| S11 | Conflicto de `PuestoId` | `Post_Create_WhenApiReturnsConflict_ShowsMessageAndPreservesInput` | ✅ COMPLIANT |
| S12 | Mutación Web rechazada por rol | `Get_Create_WhenAuthenticatedWithoutMutationRole_RedirectsToAccessDenied` y `Get_Edit_WhenAuthenticatedWithoutMutationRole_RedirectsToAccessDenied` | ✅ COMPLIANT |
| S13 | Edit muestra datos actuales | `Get_Edit_WhenMutationRole_PrepopulatesStateAndObservations` | ✅ COMPLIANT |
| S14 | Cambio a estado terminal visible | `Post_Edit_WhenSuccessful_InvokesStateChangeAndRedirectsToDetails` | ✅ COMPLIANT |
| S15 | Historial visible en Details | `Get_Details_RendersChronologicalHistory` | ✅ COMPLIANT |
| S16 | Details sin historial | `Get_Details_WhenNoHistory_ShowsEmptyState` | ✅ COMPLIANT |
| S17 | Vacante inexistente | `Get_Details_WhenVacanteDoesNotExist_ShowsRecoverableStateWithReturnLink` | ✅ COMPLIANT |
| S18 | Entrada Vacantes visible | `Sidenav_WhenAuthenticatedNonMutator_RendersListadoButNotNueva` | ✅ COMPLIANT |
| S19 | Estado active en páginas de Vacantes | `Get_Index_MarksVacantesSidenavGroupActive` | ✅ COMPLIANT |

**Resumen**: 19/19 escenarios COMPLIANT; **0 escenarios UNTESTED**.

## TDD y calidad de aserciones

| Check | Resultado | Detalle |
|-------|-----------|---------|
| Safety net previo | ✅ | 16/16 pruebas Web Vacantes verdes antes de editar. |
| Seis pruebas agregadas | ✅ | 3 en Create y 3 en Details/Sidenav. |
| GREEN runtime | ✅ | 22/22 pruebas Web Vacantes verdes. |
| RED-first | ➖ | No aplica a producción nueva: la issue remedia deuda de cobertura sobre ramas ya implementadas y archivadas. |
| Aserciones conductuales | ✅ | Todas ejecutan `WebApplicationFactory`; validan respuesta HTTP y feedback, preservación, navegación o estado DOM observable. |
| Aserciones triviales | ✅ | No se agregaron tautologías, checks de tipo aislados ni loops sin garantía de ejecución. |
| Capa | Integration Web | 6 pruebas en 2 archivos; E2E no disponible. |
| Cobertura de líneas | ➖ | No aplica a archivos de producción: el diff sólo agrega pruebas y este reporte. |

## Fallos de la suite global

### Reproducible aislado

- `SGV.Tests.Api.SwaggerConfigurationTests.NonOrgResources_OnlyExposeGetOperations`
  - `tests/SGV.Tests/Api/SwaggerConfigurationTests.cs:235`
  - Esperado: `get`; real: `post`.
  - Reejecución aislada: **0 passed, 1 failed**.

### Pasan al reejecutarse aislados

- `SGV.Tests.Seguridad.JwtCorteInmediatoMySqlFactTests.BloquearUsuario_InvalidaJwtInmediatamente`
  - En suite global: esperado `OK`, real `Unauthorized`.
  - Reejecución aislada: **1 passed, 0 failed**.
- `SGV.Tests.Persistencia.PersonaRepositoryTests.ActualizarPersona_LimpiarLegajo_PersisteNullYRegistraUpdateLegajoEnAuditorias`
  - En suite global: auditoría esperada no encontrada.
  - Reejecución aislada: **1 passed, 0 failed**.

MySQL estuvo disponible: **0 pruebas skipeadas**. Los dos resultados MySQL son compatibles con interferencia de estado u orden, pero no se establece causa raíz en este cambio porque los archivos involucrados están fuera del diff de la issue #234.

## Archivos de implementación de la issue

- `tests/SGV.Tests/Web/Vacantes/VacantesCreateEditForbidTests.cs`
- `tests/SGV.Tests/Web/Vacantes/VacantesDetailsAndSidenavTests.cs`
- `openspec/changes/archive/2026-07-30-feature-implementar-modulo-vacantes/verify-report-5.md`

No se modificó código de producción, `FakeVacanteApiClient`, reportes previos ni otros artefactos históricos.

## Veredicto

**FAIL — gate global de la solución**

La remediación solicitada queda demostrada en su alcance: seis pruebas adicionales, `vacante-web` en 19/19 runtime, filtro Vacantes y suite Web completos en verde. No se declara éxito global porque `dotnet test SGV.slnx` terminó con exit code 1 por los tres fallos documentados.
