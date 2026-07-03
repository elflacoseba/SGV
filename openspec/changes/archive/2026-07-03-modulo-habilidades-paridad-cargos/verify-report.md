## Verification Report

**Change**: `modulo-habilidades-paridad-cargos`  
**Modo**: Strict TDD + OpenSpec + verificación adversarial independiente  
**Fecha**: 2026-07-03

### 1. Resumen ejecutivo final

Revalidé `proposal.md`, `design.md`, `tasks.md`, `apply-progress.md`, los 5 delta specs y el código actual de `develop` con evidencia runtime nueva. La pasada residual cerró el único gap que quedaba: el escenario MUST de toggle `activas/eliminadas` ahora tiene prueba runtime explícita y passing.

Con la evidencia actual, el change queda **PASS**. Los 5 CRITICAL históricos están resueltos y no encontré desvíos nuevos que rompan spec, design o out-of-scope.

### 2. Evidencia de ejecución

| Check | Comando | Resultado | Evidencia resumida |
|---|---|---|---|
| Build solución | `dotnet build SGV.slnx` | ✅ PASS | `Build succeeded. 0 Warning(s), 0 Error(s)` |
| Suite runtime del repo excluyendo baseline conocido | `dotnet test SGV.slnx --filter "FullyQualifiedName!~OcupacionRepositoryTests"` | ✅ PASS | `Passed: 1223, Failed: 0, Total: 1223` (35 s) |
| Suite focalizada del change | `dotnet test SGV.slnx --filter "FullyQualifiedName~Habilidad|FullyQualifiedName~SkillsController|FullyQualifiedName~NivelesHabilidadController|FullyQualifiedName~SwaggerConfigurationTests|FullyQualifiedName~CargoWebTests"` | ✅ PASS | `Passed: 248, Failed: 0, Total: 248` |
| Test residual puntual | `dotnet test SGV.slnx --filter "FullyQualifiedName~Get_Index_ToggleSegmentoLink_PreservesSearchAndSortAndResetsPage"` | ✅ PASS | `Passed: 1, Failed: 0, Total: 1` |
| Bundle frontend | `bun run build` | ✅ PASS | `Finished 'build'`; warnings no bloqueantes de `baseline-browser-mapping` y `caniuse-lite` |

### 3. Seguimiento de los 5 CRITICAL

| ID | Estado | Evidencia específica |
|---|---|---|
| CRITICAL-01 — `/api/v1/skills/consulta` no normalizaba `page/pageSize` | ✅ RESUELTO | `src/SGV.Api/Controllers/SkillsController.cs:86-107` normaliza `page<1 => 1` y `pageSize<1 => 20`, `>100 => 100`; tests `GetConsulta_PageSizeMayorA100_NormalizaA100`, `GetConsulta_PageInvalido_NormalizaA1`, `GetConsulta_PageSizeNegativo_CaeADefault20` en `tests/SGV.Tests/Api/SkillsControllerTests.cs`; fix documentado en `apply-progress.md` bajo CRITICAL-01. |
| CRITICAL-02 — `tasks.md` sin cerrar | ✅ RESUELTO | `openspec/changes/modulo-habilidades-paridad-cargos/tasks.md` quedó con 25/25 tasks en `[x]`; búsqueda `^- \[ \]` sin matches. |
| CRITICAL-03 — faltaba `TDD Cycle Evidence` | ✅ RESUELTO | `openspec/changes/modulo-habilidades-paridad-cargos/apply-progress.md:295+` contiene la tabla obligatoria por task + correctivos. |
| CRITICAL-04 — `_Sidenav` no marcaba `Nueva` activa | ✅ RESUELTO | `src/SGV.Web/Pages/Shared/Partials/_Sidenav.cshtml:1-15,95-110` separa `habilidadesGroupActive`, `habilidadesListadoActive`, `habilidadesNuevaActive`; tests `Get_Sidenav_WhenAtHabilidadesIndex_MarksListadoActive` y `Get_Sidenav_WhenAtHabilidadesCrear_MarksNuevaActive` en `tests/SGV.Tests/Web/CargoWebTests.cs`. |
| CRITICAL-05 — cobertura runtime MUST incompleta | ✅ RESUELTO | Quedó cerrada en dos tandas: (a) `GetById_InactiveHabilidad_ReturnsNotFound`, `GetAll_WithEmptyCatalog_Returns200WithEmptyArray`, `Post_Edit_WhenBackendUnavailable_ShowsRecoverableError`; (b) residual final `Get_Index_ToggleSegmentoLink_PreservesSearchAndSortAndResetsPage` en `tests/SGV.Tests/Web/Habilidad/HabilidadIndexPageTests.cs:218+`, presente y passing en runtime (`1/1`). |

### 4. TDD Compliance

| Check | Resultado | Detalle |
|---|---|---|
| Strict TDD activo | ✅ | `openspec/config.yaml` + `apply-progress.md` |
| `TDD Cycle Evidence` reportado | ✅ | Sección presente en `apply-progress.md` |
| Tasks completas | ✅ | 25/25 marcadas |
| RED confirmado (tests existen) | ✅ | Los archivos y métodos citados existen en el árbol |
| GREEN confirmado (tests pasan hoy) | ✅ | 1223/1223 repo relevante, 248/248 focalizado, 1/1 residual |
| Assertion quality | ✅ | Revisión adversarial sin tautologías ni asserts vacíos evidentes en los tests del change |

### 5. Distribución de capas de test

| Layer | Evidencia principal | Estado |
|---|---|---|
| Unit | `HabilidadListQueryTests`, `HabilidadServicioConsultaTests`, `NivelHabilidadServicioConsultaTests`, `HabilidadApiClientTests`, `HabilidadWebSeamTests` | ✅ |
| Integración / HTTP / Web runtime | `SkillsControllerTests`, `NivelesHabilidadControllerTests`, `SwaggerConfigurationTests`, `HabilidadRepositoryTests`, `NivelHabilidadRepositoryTests`, `Habilidad*PageTests`, `CargoWebTests` | ✅ |
| E2E browser | No aplica en este change | ➖ |

### 6. Matriz spec × scenario × evidencia × estado

| Spec | Scenario | Evidencia | Estado |
|---|---|---|---|
| habilidad-management | Listar habilidades activas legacy | `SkillsControllerTests.GetAll_ReturnsOkWithDtoArray` | ✅ COMPLIANT |
| habilidad-management | Obtener habilidad inexistente o inactiva | `GetById_NonExistentId_ReturnsNotFound`, `GetById_InactiveHabilidad_ReturnsNotFound` | ✅ COMPLIANT |
| habilidad-management | Consulta de eliminadas no mezcla segmentos | `GetConsulta_StatusEliminadas_RetornaSoloEliminadas`, `QueryAsync_ConSegmentoEliminadas_RetornaSoloEliminados` | ✅ COMPLIANT |
| habilidad-management | Paginación o status inválidos se normalizan | `GetConsulta_StatusInvalido_CaeA_Activas`, `GetConsulta_PageSizeMayorA100_NormalizaA100`, `GetConsulta_PageInvalido_NormalizaA1`, `GetConsulta_PageSizeNegativo_CaeADefault20` | ✅ COMPLIANT |
| habilidad-management | Búsqueda sin coincidencias devuelve página vacía | `HabilidadIndexPageTests.Get_Index_WhenSearchHasNoResults_ShowsEmptyState` | ✅ COMPLIANT |
| habilidad-management | Catálogo de niveles disponible para web | `NivelesHabilidadControllerTests.GetAll_ReturnsOkWithDtos`, `NivelHabilidadRepositoryTests.ListAllAsync_RetornaNivelesOrdenadosPorOrden` | ✅ COMPLIANT |
| habilidad-management | Catálogo vacío sigue siendo válido | `GetAll_WithEmptyCatalog_Returns200WithEmptyArray` | ✅ COMPLIANT |
| habilidad-management | Lecturas autenticadas exitosas | `GetAll`, `GetById`, `GetConsulta` autenticados | ✅ COMPLIANT |
| habilidad-management | Acceso anónimo rechazado | `GetAll_WithoutCredentials`, `GetById_WithoutCredentials`, `GetConsulta_WithoutCredentials` | ✅ COMPLIANT |
| habilidad-management | Mutación protegida por rol administrador | tests 401/403/2xx de `Create/Update/Delete/Reactivate` en `SkillsControllerTests` | ✅ COMPLIANT |
| habilidad-web-listado-detalle-baja | Usuario autenticado abre el módulo | `Get_Index_WhenAuthenticated_RendersActiveHabilidadesTable` | ✅ COMPLIANT |
| habilidad-web-listado-detalle-baja | Usuario anónimo intenta acceder | `Get_Index_WhenAnonymous_RedirectsToSignIn`, `Get_Details_WhenAnonymous_RedirectsToSignIn` | ✅ COMPLIANT |
| habilidad-web-listado-detalle-baja | Carga inicial en activas | `Get_Index_WhenAuthenticated_RendersActiveHabilidadesTable` | ✅ COMPLIANT |
| habilidad-web-listado-detalle-baja | Cambio a eliminadas preserva contexto | `Get_Index_ToggleSegmentoLink_PreservesSearchAndSortAndResetsPage` | ✅ COMPLIANT |
| habilidad-web-listado-detalle-baja | Búsqueda sin coincidencias | `Get_Index_WhenSearchHasNoResults_ShowsEmptyState` | ✅ COMPLIANT |
| habilidad-web-listado-detalle-baja | Vista activas muestra acciones | `Get_Index_WhenAuthenticated_RendersActiveHabilidadesTable` | ✅ COMPLIANT |
| habilidad-web-listado-detalle-baja | Vista eliminadas muestra solo reactivación | `Get_Index_WhenSegmentoEliminadas_RendersReactivarButtonOnly` | ✅ COMPLIANT |
| habilidad-web-listado-detalle-baja | Detalle existente | `Get_Details_WhenAuthenticated_ShowsHabilidadReadOnly` | ✅ COMPLIANT |
| habilidad-web-listado-detalle-baja | Baja lógica exitosa | `Post_Delete_WhenSuccessful_RedirectsPreservingFilters` | ✅ COMPLIANT |
| habilidad-web-listado-detalle-baja | Reactivación con conflicto por código activo | `Post_Reactivate_WhenCodigoDuplicado_ReturnsConflictAndStaysOnEliminadas` | ✅ COMPLIANT |
| habilidad-web-crear-editar | Usuario autenticado abre create | `Get_Create_WhenAuthenticated_RendersEmptyForm` | ✅ COMPLIANT |
| habilidad-web-crear-editar | Habilidad activa existente en edit | `Get_Edit_WhenAuthenticated_PrepopulatesForm` | ✅ COMPLIANT |
| habilidad-web-crear-editar | Habilidad inexistente o eliminada en edit | `Get_Edit_WhenHabilidadNotFound_ShowsRecoverableState` | ✅ COMPLIANT |
| habilidad-web-crear-editar | Create muestra campos editables | `Get_Create_WhenAuthenticated_RendersEmptyForm` | ✅ COMPLIANT |
| habilidad-web-crear-editar | Edit refleja la inmutabilidad de `Codigo` | `EditPage_MuestraCodigoComoReadonly_O_Disabled` | ✅ COMPLIANT |
| habilidad-web-crear-editar | Create exitoso | `Post_Create_WhenSuccessful_RedirectsToDetailsWithConfirmation` | ✅ COMPLIANT |
| habilidad-web-crear-editar | Edit exitoso | `Post_Edit_WhenSuccessful_RedirectsToDetailsWithConfirmation` | ✅ COMPLIANT |
| habilidad-web-crear-editar | Conflicto por `Codigo` activo duplicado | `Post_Create_WhenCodigoDuplicado_ReturnsFieldErrorAndKeepsForm` | ✅ COMPLIANT |
| habilidad-web-crear-editar | Backend no disponible durante el guardado | `Post_Create_WhenBackendUnavailable_ShowsRecoverableError`, `Post_Edit_WhenBackendUnavailable_ShowsRecoverableError` | ✅ COMPLIANT |
| sgv-web-shell | Navegación mínima con Habilidades habilitado | `CargoWebTests.Get_Sidenav_WhenAuthenticated_ExposesHabilidadesModule` | ✅ COMPLIANT |
| sgv-web-shell | Submenú de Habilidades visible y activo | `Get_Sidenav_WhenAtHabilidadesIndex_MarksListadoActive`, `Get_Sidenav_WhenAtHabilidadesCrear_MarksNuevaActive` | ✅ COMPLIANT |
| sgv-web-shell | Otros módulos siguen fuera de alcance | `CargoWebTests` valida ausencia de placeholders | ✅ COMPLIANT |
| sgv-readonly-api | Discover endpoints through API documentation | `SwaggerConfigurationTests` + endpoint `/api/v1/niveles-habilidad` | ✅ COMPLIANT |
| sgv-readonly-api | Discover skill management operations | `DiscoverSkillsConsultaEndpoint_Test` | ✅ COMPLIANT |
| sgv-readonly-api | Discover segmented skill query parameters | `Habilidades_ConsultaEndpoint_StatusParameter_DocumentaValoresActivasYEliminadas` | ✅ COMPLIANT |
| sgv-readonly-api | Discover skill-level catalog | `DiscoverNivelesHabilidadEndpoint_Test` | ✅ COMPLIANT |

### 7. Coherencia con proposal y design

| Tema | Evidencia | Estado |
|---|---|---|
| Paridad con Cargos sin copia literal de `Nivel` | `_Form.cshtml`, `HabilidadAntiDriftTests`, JSON anti-drift de `SkillsControllerTests` | ✅ |
| Normalización HTTP vive en controller | `SkillsController.cs:94-105` | ✅ |
| `GET /api/v1/niveles-habilidad` en controller paralelo | `src/SGV.Api/Controllers/NivelesHabilidadController.cs` | ✅ |
| Orden de catálogo por `Orden` | `NivelHabilidadRepositoryTests` + controller tests | ✅ |
| `Codigo` readonly en edición | `Edit.cshtml/_Form.cshtml` + `EditPage_MuestraCodigoComoReadonly_O_Disabled` | ✅ |
| Sidebar debajo de `Cargos` con `Listado` y `Nueva` | `_Sidenav.cshtml` + `CargoWebTests` | ✅ |

### 8. Cumplimiento de out-of-scope

| Restricción | Verificación | Estado |
|---|---|---|
| Sin migraciones nuevas | no hay nuevos archivos en `src/SGV.Infraestructura/Persistencia/Migraciones` | ✅ |
| Sin asignaciones `habilidad↔cargo` / `habilidad↔persona` | no hay endpoints/subrecursos nuevos de asignación | ✅ |
| Sin `nivelId` en catálogo maestro web/API de habilidades | tests anti-drift web + JSON | ✅ |
| Sin expansión de `PagedResult<T>` | el contrato sigue en `Items/TotalCount/Page/PageSize` | ✅ |

### 9. Commits y estrategia de entrega

| Check | Evidencia | Estado |
|---|---|---|
| Estrategia stacked-to-develop / PRs chained | PRs `#73`, `#74`, `#75`, `#76` ya integrados a `develop` | ✅ |
| Correctivos previos visibles en historial | `056fbfbc test(skills): add runtime coverage for MUST scenarios of sdd-verify`, `77238754 fix(web): mark Habilidades Nueva sidebar entry active on crear route`, merges de `#73-#76` | ✅ |
| Residual final aún no commiteado | `git status --short` muestra cambios pendientes en `HabilidadIndexPageTests.cs`, `apply-progress.md` y este `verify-report.md`; verify NO hizo commit | ✅ |

### 10. Observaciones no bloqueantes

- `bun run build` sigue emitiendo warnings de metadata vieja de Browserslist (`baseline-browser-mapping`, `caniuse-lite`). No afecta el change.
- El comando focalizado ejecutado con el filtro pedido devolvió `248/248` en esta corrida, no `249/249`. No detecté fallas funcionales; simplemente documento la evidencia real observada en runtime.

### 11. Veredicto final

**PASS**

El change cumple proposal, design, tasks y specs con evidencia runtime suficiente. El residual del toggle `activas/eliminadas` quedó cerrado por el test `Get_Index_ToggleSegmentoLink_PreservesSearchAndSortAndResetsPage`, que está presente en el árbol y pasa en ejecución real.
