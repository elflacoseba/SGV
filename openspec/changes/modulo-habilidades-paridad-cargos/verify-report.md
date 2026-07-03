## Verification Report

**Change**: `modulo-habilidades-paridad-cargos`  
**Modo**: OpenSpec + verificación adversarial independiente  
**Fecha**: 2026-07-03

### 1. Resumen ejecutivo

Verifiqué proposal, design, los 5 delta specs, `tasks.md`, `apply-progress.md`, código fuente, commits y ejecución real de build/tests/frontend. El change tiene una base funcional sólida en backend/web, pero NO cumple todavía con el estándar de archive readiness.

El veredicto es **BLOCKED** por tres razones objetivas: (1) el contrato HTTP de `/api/v1/skills/consulta` no normaliza `page/pageSize` como exigen spec + design + tasks; (2) `tasks.md` no fue actualizado por apply y sigue con todas las tasks sin marcar; (3) en el shell, el submenú `Habilidades` no refleja la opción activa correspondiente (`Nueva`) y además faltan pruebas de runtime para varios escenarios obligatorios.

### 2. Evidencia de ejecución

| Check | Comando | Resultado | Evidencia |
|---|---|---|---|
| Build solución | `dotnet build SGV.slnx` | ✅ PASS | `Build succeeded. 0 Warning(s), 0 Error(s)` |
| Test suite completa | `dotnet test SGV.slnx` | ⚠️ Baseline known-failing | `Failed: 12, Passed: 1218, Total: 1230` — los 12 fallos son `OcupacionRepositoryTests` con `ActivePuestoIdUnique` (issue #59 preexistente, fuera de scope) |
| Test suite excluyendo baseline conocido | `dotnet test SGV.slnx --filter "FullyQualifiedName!~OcupacionRepositoryTests"` | ✅ PASS | `Passed: 1215, Failed: 0, Total: 1215` |
| Test suite focalizada del change | `dotnet test SGV.slnx --filter "FullyQualifiedName~Habilidad|FullyQualifiedName~SkillsController|FullyQualifiedName~SwaggerConfigurationTests|FullyQualifiedName~CargoWebTests"` | ✅ PASS | `Passed: 240, Failed: 0, Total: 240` |
| Frontend bundle | `bun run build` | ✅ PASS | `Finished 'build'` |

### 3. Findings CRITICAL

#### CRITICAL-01 — `/api/v1/skills/consulta` incumple la normalización de `page/pageSize`

- **Spec/design afectados**:
  - `specs/habilidad-management/spec.md:17-18, 41-46`
  - `design.md:94`
  - `tasks.md:84`
- **Evidencia en código**:
  - `src/SGV.Api/Controllers/SkillsController.cs:86-100` crea `new HabilidadListQuery(page, pageSize, ...)` sin normalizar.
  - `src/SGV.Aplicacion/Habilidades/Consultas/Dtos/HabilidadListQuery.cs:28-33` es un record plano, sin lógica de normalización.
  - `src/SGV.Aplicacion/Habilidades/Consultas/HabilidadServicioConsulta.cs:26-38` reexpone `query.Page` y `query.PageSize` tal cual en `PagedResult`.
- **Evidencia en tests**:
  - `tests/SGV.Tests/Api/SkillsControllerTests.cs:559-616` fija explícitamente el comportamiento incorrecto: `pageSize=500` llega como `500` y `page=0` llega como `0`.
- **Impacto**: rompe el contrato documentado del endpoint y contradice proposal/design/tasks. No es deuda menor: es drift contractual ya blindado por tests equivocados.

#### CRITICAL-02 — `tasks.md` no fue sincronizado por apply y sigue con todas las tasks pendientes

- **Spec/process afectados**:
  - `openspec-convention.md:35-37`
  - `sdd-verify/SKILL.md:69, 85`
- **Evidencia**:
  - `openspec/changes/modulo-habilidades-paridad-cargos/tasks.md:52-163` mantiene todas las tasks como `- [ ]`.
  - Comando usado: `grep "\- \[ \]" openspec/changes/modulo-habilidades-paridad-cargos/tasks.md` → **25 matches**.
  - `openspec/changes/modulo-habilidades-paridad-cargos/apply-progress.md:184` afirma `28/28` completadas, pero el artifact canónico de tasks NO refleja ese estado.
- **Impacto**: para verify, task incompleta/no marcada sigue siendo blocking. Además hay inconsistencia de conteo: el `tasks.md` contiene 25 checklist items, no 28.

#### CRITICAL-03 — Incumplimiento de Strict TDD: falta la tabla `TDD Cycle Evidence` en `apply-progress.md`

- **Spec/process afectados**:
  - `openspec/config.yaml:11-17`
  - `strict-tdd-verify.md:10-48, 261-264`
- **Evidencia**:
  - Comando usado: `grep "TDD Cycle Evidence|RED|GREEN|TRIANGULATE|SAFETY NET" openspec/changes/modulo-habilidades-paridad-cargos/apply-progress.md` → **sin resultados**.
  - El `apply-progress.md` narra PRs y validaciones, pero no aporta la tabla obligatoria por task para verificar RED/GREEN/TRIANGULATE/SAFETY NET.
- **Impacto**: con `strict_tdd: true`, verify no puede demostrar que el ciclo TDD reportado haya ocurrido según protocolo.

#### CRITICAL-04 — El shell no marca la opción activa correspondiente en `Habilidades`

- **Spec afectada**:
  - `specs/sgv-web-shell/spec.md:27-32`
- **Evidencia en código**:
  - `src/SGV.Web/Pages/Shared/Partials/_Sidenav.cshtml:6` define un único `habilidadesActive` para todo `/organizacion/habilidades`.
  - `src/SGV.Web/Pages/Shared/Partials/_Sidenav.cshtml:95-101` aplica `@habilidadesActive` solo al link `Listado`; el link `Nueva` nunca recibe clase activa.
- **Evidencia en tests**:
  - `tests/SGV.Tests/Web/CargoWebTests.cs:73-93` solo verifica presencia, links e icono; no cubre estado activo de la opción correspondiente.
- **Impacto**: en `/organizacion/habilidades/crear` el grupo puede verse activo, pero la opción correcta (`Nueva`) no lo está. Eso incumple una scenario MUST del shell.

#### CRITICAL-05 — La cobertura de escenarios obligatorios es incompleta

- **Regla aplicable**: `sdd-verify/SKILL.md:40-42, 61, 72-78` exige prueba de runtime por escenario requerido.
- **Escenarios sin prueba de runtime suficiente o con evidencia incompleta**:
  - `habilidad-management` → **Obtener habilidad inexistente o inactiva**: hay test de inexistente (`SkillsControllerTests.cs:109-118`), pero no de habilidad inactiva.
  - `habilidad-management` → **Catálogo vacío sigue siendo válido**: hay test de servicio (`NivelHabilidadServicioConsultaTests.cs:39-48`), pero no prueba HTTP runtime de `/api/v1/niveles-habilidad` devolviendo colección vacía.
  - `habilidad-web-listado-detalle-baja` → **Cambio a eliminadas preserva contexto**: el código parece contemplarlo (`Index.cshtml.cs:236-242`), pero no hay prueba runtime que verifique preservar búsqueda/orden y resetear página.
  - `habilidad-web-crear-editar` → **Backend no disponible durante el guardado**: create sí está cubierto (`HabilidadCreatePageTests.cs:106-121`), edit no tiene prueba equivalente.
  - `sgv-web-shell` → **Submenú visible y activo**: presencia sí, estado activo correspondiente no.
- **Impacto**: no puedo marcar cumplimiento completo de specs cuando faltan escenarios MUST con runtime evidence.

### 4. Findings WARNING

#### WARNING-01 — El baseline del repo sigue sin poder ejecutar `dotnet test SGV.slnx` completamente en verde

- **Evidencia**: `dotnet test SGV.slnx` → `Failed: 12, Passed: 1218, Total: 1230`.
- **Detalle**: los 12 fallos corresponden al bug conocido de `OcupacionRepositoryTests` (`ActivePuestoIdUnique`) y son previos a este change.
- **Impacto**: no bloquea específicamente este change porque el usuario explicitó la exclusión, pero sí afecta la condición global de archive del repo.

#### WARNING-02 — El `apply-progress.md` reporta contadores que no cierran con los artifacts actuales

- **Evidencia**:
  - `apply-progress.md:184-189` afirma `28/28` tasks y `1208/1208` tests excluidos.
  - `tasks.md` contiene 25 checklist items pendientes.
  - Mi corrida excluyendo `OcupacionRepositoryTests` dio `1215/1215`, no `1208/1208`.
- **Impacto**: el reporte de apply no es una fuente suficientemente confiable para archive sin corrección documental.

### 5. Findings SUGGESTION

#### SUGGESTION-01 — Actualizar dependencias de Browserslist del pipeline frontend

- **Evidencia**: `bun run build` emitió avisos de `baseline-browser-mapping` y `caniuse-lite` desactualizados.
- **Impacto**: no afecta el cambio, pero conviene limpiar ruido de build.

### 6. TDD Compliance

| Check | Resultado | Detalles |
|---|---|---|
| Strict TDD activo | ✅ | `openspec/config.yaml:11` |
| `TDD Cycle Evidence` reportado | ❌ | `apply-progress.md` no contiene la tabla obligatoria |
| Tasks marcadas en `tasks.md` | ❌ | 25 checklist items siguen en `- [ ]` |
| GREEN confirmado por ejecución actual | ⚠️ Parcial | La suite focalizada del change pasa (`240/240`), pero falta la tabla por task y el artefacto de tasks no fue actualizado |
| Assertion quality | ✅ | No encontré tautologías obvias ni tests vacíos en los archivos revisados del change |

### 7. Test Layer Distribution

| Layer | Evidencia principal | Resultado |
|---|---|---|
| Unit | `HabilidadListQueryTests`, `HabilidadServicioConsultaTests`, `NivelHabilidadServicioConsultaTests`, `HabilidadApiClientTests`, `HabilidadWebSeamTests` | ✅ |
| Integration | `SkillsControllerTests`, `NivelesHabilidadControllerTests`, `SwaggerConfigurationTests`, `Habilidad*PageTests`, `HabilidadRepositoryTests`, `NivelHabilidadRepositoryTests` | ✅ |
| E2E | No existe tooling E2E en el repo (`openspec/config.yaml:25-27`) | ➖ |

### 8. Tabla de cobertura spec × scenario × evidencia

| Spec | Scenario | Evidencia | Estado |
|---|---|---|---|
| habilidad-management | Listar habilidades activas legacy | `SkillsControllerTests.GetAll_ReturnsOkWithDtoArray` (`tests/SGV.Tests/Api/SkillsControllerTests.cs:58-72`) + `dotnet test ...` focalizado `240/240` | ✅ PASS |
| habilidad-management | Obtener habilidad inexistente o inactiva | Inexistente: `GetById_NonExistentId_ReturnsNotFound` (`SkillsControllerTests.cs:109-118`). Inactiva: sin prueba runtime específica. | ❌ PARTIAL |
| habilidad-management | Consulta de eliminadas no mezcla segmentos | `GetConsulta_StatusEliminadas_RetornaSoloEliminadas` (`SkillsControllerTests.cs:443-462`) + `QueryAsync_SegmentosNoSeMezclan` (`HabilidadServicioConsultaTests.cs:101-127`) | ✅ PASS |
| habilidad-management | Paginación o status inválidos se normalizan | Los tests actuales fijan lo contrario: `GetConsulta_PageSizeMayorA100_NormalizaA100` y `GetConsulta_PageInvalido_LlegaCeroYServicioLoManeja` (`SkillsControllerTests.cs:559-616`) | ❌ FAIL |
| habilidad-management | Búsqueda sin coincidencias devuelve página vacía | Evidencia visible en web: `HabilidadIndexPageTests.Get_Index_WhenSearchHasNoResults_ShowsEmptyState` (`tests/SGV.Tests/Web/Habilidad/HabilidadIndexPageTests.cs:78-95`); falta prueba HTTP runtime específica del endpoint | ⚠️ PARTIAL |
| habilidad-management | Catálogo de niveles disponible para web | `NivelesHabilidadControllerTests.GetAll_ReturnsOkWithDtos` (`tests/SGV.Tests/Api/NivelesHabilidadControllerTests.cs:20-37`) + orden por `Orden` en `NivelHabilidadRepositoryTests.cs:25-41` | ✅ PASS |
| habilidad-management | Catálogo vacío sigue siendo válido | `NivelHabilidadServicioConsultaTests.ListAsync_CuandoNoExistenRegistros_RetornaListaVacia` (`tests/SGV.Tests/Aplicacion/Habilidades/NivelHabilidadServicioConsultaTests.cs:39-48`); falta prueba HTTP runtime | ❌ UNTESTED |
| habilidad-management | Lecturas autenticadas exitosas | `GetAll_ReturnsOkWithDtoArray`, `GetById_ExistingId_ReturnsOkWithDto`, `GetConsulta_StatusEliminadas_RetornaSoloEliminadas` | ✅ PASS |
| habilidad-management | Acceso anónimo rechazado | `GetAll_WithoutCredentials_ReturnsUnauthorized`, `GetById_WithoutCredentials_ReturnsUnauthorized`, `GetConsulta_WithoutCredentials_ReturnsUnauthorized` (`SkillsControllerTests.cs:136-157, 432-440`) | ✅ PASS |
| habilidad-management | Mutación protegida por rol administrador | 401/403: `Create_WithoutCredentials_ReturnsUnauthorized`, `Create/Update/Delete/Reactivate_WithAuthenticatedNonAdmin_ReturnsForbidden` (`SkillsControllerTests.cs:621-679`). 2xx admin: create/update/delete/reactivate en el mismo archivo (`185-198`, `258-271`, `329-338`, `402-427`). | ✅ PASS |
| habilidad-web-listado-detalle-baja | Usuario autenticado abre el módulo | `HabilidadIndexPageTests.Get_Index_WhenAuthenticated_RendersActiveHabilidadesTable` (`44-76`) | ✅ PASS |
| habilidad-web-listado-detalle-baja | Usuario anónimo intenta acceder | `Get_Index_WhenAnonymous_RedirectsToSignIn` (`28-41`) + `HabilidadDetailsPageTests.Get_Details_WhenAnonymous_RedirectsToSignIn` (`23-33`) | ✅ PASS |
| habilidad-web-listado-detalle-baja | Carga inicial en activas | `Get_Index_WhenAuthenticated_RendersActiveHabilidadesTable` (`44-76`) | ✅ PASS |
| habilidad-web-listado-detalle-baja | Cambio a eliminadas preserva contexto | Código: `Index.cshtml.cs:236-242`; no hay prueba runtime que verifique preservar búsqueda/orden y resetear página | ❌ UNTESTED |
| habilidad-web-listado-detalle-baja | Búsqueda sin coincidencias | `Get_Index_WhenSearchHasNoResults_ShowsEmptyState` (`78-95`) | ✅ PASS |
| habilidad-web-listado-detalle-baja | Vista activas muestra acciones | `Get_Index_WhenAuthenticated_RendersActiveHabilidadesTable` (`63-70`) | ✅ PASS |
| habilidad-web-listado-detalle-baja | Vista eliminadas muestra solo reactivación | `Get_Index_WhenSegmentoEliminadas_RendersReactivarButtonOnly` (`115-135`) | ✅ PASS |
| habilidad-web-listado-detalle-baja | Detalle existente | `HabilidadDetailsPageTests.Get_Details_WhenAuthenticated_ShowsHabilidadReadOnly` (`35-56`) | ✅ PASS |
| habilidad-web-listado-detalle-baja | Baja lógica exitosa | `Post_Delete_WhenSuccessful_RedirectsPreservingFilters` (`137-154`) | ✅ PASS |
| habilidad-web-listado-detalle-baja | Reactivación con conflicto por código activo | `Post_Reactivate_WhenCodigoDuplicado_ReturnsConflictAndStaysOnEliminadas` (`193-215`) | ✅ PASS |
| habilidad-web-crear-editar | Usuario autenticado abre create | `HabilidadCreatePageTests.Get_Create_WhenAuthenticated_RendersEmptyForm` (`39-61`) | ✅ PASS |
| habilidad-web-crear-editar | Habilidad activa existente en edit | `HabilidadEditPageTests.Get_Edit_WhenAuthenticated_PrepopulatesForm` (`36-59`) | ✅ PASS |
| habilidad-web-crear-editar | Habilidad inexistente o eliminada en edit | `HabilidadEditPageTests.Get_Edit_WhenHabilidadNotFound_ShowsRecoverableState` (`61-75`) | ✅ PASS |
| habilidad-web-crear-editar | Create muestra campos editables y sin nivel | `HabilidadCreatePageTests.Get_Create_WhenAuthenticated_RendersEmptyForm` (`49-60`) + `HabilidadAntiDriftTests.cs:27-72` | ✅ PASS |
| habilidad-web-crear-editar | Edit refleja inmutabilidad de `Codigo` | `HabilidadEditPageTests.EditPage_MuestraCodigoComoReadonly_O_Disabled` (`119-137`) + `_Form.cshtml:19-20` | ✅ PASS |
| habilidad-web-crear-editar | Create exitoso | `Post_Create_WhenSuccessful_RedirectsToDetailsWithConfirmation` (`63-80`) | ✅ PASS |
| habilidad-web-crear-editar | Edit exitoso | `Post_Edit_WhenSuccessful_RedirectsToDetailsWithConfirmation` (`77-94`) | ✅ PASS |
| habilidad-web-crear-editar | Conflicto por `Codigo` activo duplicado | `Post_Create_WhenCodigoDuplicado_ReturnsFieldErrorAndKeepsForm` (`82-104`) | ✅ PASS |
| habilidad-web-crear-editar | Backend no disponible durante el guardado | Create sí: `Post_Create_WhenBackendUnavailable_ShowsRecoverableError` (`106-121`); Edit no tiene prueba equivalente | ❌ PARTIAL |
| sgv-web-shell | Navegación mínima con Habilidades habilitado | `CargoWebTests.Get_Sidenav_WhenAuthenticated_ExposesHabilidadesModule` (`73-93`) | ✅ PASS |
| sgv-web-shell | Submenú de Habilidades visible y activo | Visible: `CargoWebTests.cs:73-93`; activo correspondiente falla por implementación en `_Sidenav.cshtml:95-101` | ❌ FAIL |
| sgv-web-shell | Otros módulos siguen fuera de alcance | `CargoWebTests.cs:88-92` | ✅ PASS |
| sgv-readonly-api | Discover endpoints through API documentation | `SwaggerConfigurationTests.cs:85-120`, `767-813` | ✅ PASS |
| sgv-readonly-api | Discover organizational unit write operations | `SwaggerConfigurationTests.cs:123-176, 255+` | ✅ PASS |
| sgv-readonly-api | Discover cargo management operations | `SwaggerConfigurationTests.Cargos_ExposesWriteOperations` (`216-253`) | ✅ PASS |
| sgv-readonly-api | Discover puesto management operations | Evidencia existente en `SwaggerConfigurationTests` general de paths (`101-120`) | ✅ PASS |
| sgv-readonly-api | Discover skill management operations | `DiscoverSkillsConsultaEndpoint_Test` (`767-793`) + paths `/api/v1/skills/{id}/reactivar` (`109-111`) | ✅ PASS |
| sgv-readonly-api | Discover segmented skill query parameters | `Habilidades_ConsultaEndpoint_StatusParameter_DocumentaValoresActivasYEliminadas` (`816-836`) | ✅ PASS |
| sgv-readonly-api | Discover skill-level catalog | `DiscoverNivelesHabilidadEndpoint_Test` (`796-813`) | ✅ PASS |
| sgv-readonly-api | Discover persona management operations | Evidencia existente en `SwaggerConfigurationTests` general de paths (`113-115`) | ✅ PASS |
| sgv-readonly-api | Exclude unsupported operations from documentation | `SwaggerConfigurationTests.NonOrgResources_OnlyExposeGetOperations` (`178-214`) | ✅ PASS |

### 9. Coherencia con design y proposal

| Tema | Verificación | Resultado |
|---|---|---|
| Paridad con Cargos adaptada, sin `NivelId` en Habilidad | `_Form.cshtml:1-45`, `HabilidadDto.cs:6-12`, `SkillsControllerTests.GetAll_JsonResponse_NoExponeNivelIdEnHabilidadDto` (`160-179`), `HabilidadAntiDriftTests.cs:27-98` | ✅ |
| `GET /api/v1/niveles-habilidad` separado en controller paralelo | `src/SGV.Api/Controllers/NivelesHabilidadController.cs:12-69` | ✅ |
| `NivelHabilidadRepository` ordena por `Orden` | `NivelHabilidadRepository.cs:35-42` + `NivelHabilidadRepositoryTests.cs:25-41` | ✅ |
| `[Authorize]` global + rol admin en mutaciones de skills | `SkillsController.cs:17, 112-113, 145-146, 175-176, 199-200` | ✅ |
| Sin migraciones nuevas | `git diff --name-only a90e0e50^..80af28cf -- src/SGV.Infraestructura/Persistencia/Migraciones` → sin output | ✅ |
| Normalización `page/pageSize` según design | `SkillsController.cs:86-100` + `SkillsControllerTests.cs:559-616` | ❌ |

### 10. Out-of-scope compliance

| Restricción | Verificación | Resultado |
|---|---|---|
| No tocar `CargoSkillRepository` | No aparece en `git diff --name-only a90e0e50^..80af28cf` | ✅ |
| No tocar `PersonaSkillRepository` | No aparece en `git diff --name-only a90e0e50^..80af28cf` | ✅ |
| No introducir gestión habilidad↔cargo/persona en este change | No hay archivos nuevos/modificados para esos subrecursos en el diff del change | ✅ |
| No agregar `nivelId` a DTO/POST/PUT de skills | `HabilidadDto.cs:6-12`, anti-drift JSON en `SkillsControllerTests.cs:160-179`, forms sin `Input.NivelId` | ✅ |
| No crear migraciones nuevas | Diff del change sobre `Persistencia/Migraciones` sin cambios | ✅ |

### 11. Commits y estrategia de entrega

| Check | Evidencia | Resultado |
|---|---|---|
| 9 commits del change | `git log --oneline -12` muestra 9 commits relevantes desde `a90e0e50` hasta `80af28cf` | ✅ |
| 5 `feat` + 4 `docs(apply)` | `git log --oneline -12` | ✅ |
| Sin `Co-Authored-By` ni atribución IA | `git log -12 --format="%H%n%B%n---"` sin coincidencias | ✅ |
| Estrategia stacked-to-main documentada | `apply-progress.md:4-5, 13, 15-23` | ✅ |

### 12. Veredicto final

**BLOCKED**

El change NO está listo para `sdd-archive` todavía.

Bloqueos reales:

1. El endpoint `/api/v1/skills/consulta` incumple la normalización obligatoria de `page/pageSize` y los tests actuales blindan el comportamiento incorrecto.
2. `tasks.md` no fue actualizado por apply y permanece con todas las tasks en estado pendiente.
3. Falta la evidencia estructurada obligatoria de Strict TDD (`TDD Cycle Evidence`).
4. El shell no cumple completamente la scenario de opción activa correspondiente en `Habilidades`.
5. La cobertura runtime de specs es incompleta para varios escenarios MUST.

**Recomendación**: volver a `sdd-apply`, corregir los desvíos anteriores, actualizar `tasks.md` y regenerar `apply-progress.md` con evidencia TDD verificable. Recién después repetir `sdd-verify`.
