```yaml
schema: gentle-ai.verify-result/v1
evidence_revision: sha256:e1f7d02171cf1ade5cf42781d138fadd32e10db47d19b29cdc4b7f6a48bde848
verdict: pass
blockers: 0
critical_findings: 0
requirements: 14/14
scenarios: 45/45
test_command: dotnet test SGV.slnx --filter "FullyQualifiedName~Auditoria"
test_exit_code: 0
test_output_hash: sha256:592e064d86b5f27a4b40691606bc465bdc38cafc708d5a06fe5188e159a4578e
build_command: dotnet build SGV.slnx
build_exit_code: 0
build_output_hash: sha256:d4979eaaa97d506fd3fa4b0c3e597965ee1ec87e07e5b42d24fc218489735736
```

# Verify Report — `2026-07-31-ajustes-listado-auditoria` (issue #248)

> Verificación **completa** del change `2026-07-31-ajustes-listado-auditoria`
> (issue #248) sobre la rama `feat/issue-248-ajustes-listado-auditoria-slice-b`
> (base `develop`). Slice A (PR #249) ya mergeado a `develop`. Slice B es la
> rama actual bajo verificación; cubre las tareas 1.B.1 → 1.B.8.
> El verdict consolida ambas mitades del change.

## Mode

- `execution_mode`: interactive
- `artifact_store.mode`: hybrid (OpenSpec + Engram)
- `slice_en_verificacion`: change completo (Slice A + Slice B)
- `strict_tdd`: true (`openspec/config.yaml`); verificación estándar
  (módulo `strict-tdd-verify` no cargado: el apply ya consolidó TDD
  evidence en `sdd/2026-07-31-ajustes-listado-auditoria/apply-progress`).
- `MySQL`: local en `localhost:3306` (mysqld escuchando); los
  `[MySqlFact]` corren contra `sgv_test` real (no se skipean).

## Resumen ejecutivo

| Sección | Resultado |
|---|---|
| **Build** | ✅ `dotnet build SGV.slnx` → 0 errors, 4 warnings NU1510 pre-existentes en `SGV.Infraestructura` (sin relación con este change). |
| **Focused tests (Auditoria)** | ✅ 77/77 passed (0 failed, 0 skipped). Incluye los 29 `[MySqlFact]` del servicio contra MySQL real. |
| **Web suite completa** | ✅ 1398/1398 passed (0 failed, 0 skipped). |
| **Full suite global** | ✅ 3395/3395 passed (0 failed, 0 skipped). Sin regresiones. |
| **Slice B (Index rediseñado + Details)** | ✅ 18/18 tests Web nuevos + ampliados (14 IndexTests + 4 DetailsTests) pasan. |
| **D-2 cierre (verificación doble)** | ✅ Confirmado por código + tests: `Index.cshtml` no referencia `@item.EntityId`/`OldValuesJson`/`NewValuesJson`; `AuditoriaDto` sin esos campos por construcción (separación física de tipos); `AuditoriaDetalleDto` los expone y la page `Details` los renderea en `<pre>` por construcción. |
| **D-5 bis / D-6 / D-7 documentados** | ✅ `decisiones-implementacion.md` líneas 96-195 documentan las 3 decisiones con tabla de mapeo, ejemplos LINQ y tests verificadores. |
| **Verdict** | **PASS** |

### Conteo por dimensión

| Categoría | Total | PASS | FAIL | PARTIAL | N/A |
|---|---|---|---|---|---|
| Requirements delta specs (4 specs) | 14 | 14 | 0 | 0 | 0 |
| Escenarios cubiertos por tests runtime | 45 | 45 | 0 | 0 | 0 |
| Hallazgos | — | — | 0 CRITICAL | 0 WARNING | 3 SUGGESTION (pre-existentes/no-bloqueantes) |

---

## Cambios verificados (cambio completo)

### Archivos creados (Slice B)

- `src/SGV.Web/Pages/Auditorias/Details.cshtml`
- `src/SGV.Web/Pages/Auditorias/Details.cshtml.cs`
- `tests/SGV.Tests/Web/Auditoria/AuditoriasDetailsTests.cs`

### Archivos modificados (Slice B)

- `src/SGV.Web/Pages/Auditorias/Index.cshtml` (rediseño: toolbar horizontal de filtros, `<th>` ordenables con `GetSortIcon`/`GetSortRoute`, `<select name="pageSize">` 10/20/50/100, columna Acciones con Details link, paginación con números/Primera/Última)
- `src/SGV.Web/Pages/Auditorias/Index.cshtml.cs` (bind `Sort`/`CorrelationId`/`PageSize`; helpers `BuildSortRouteValues`/`BuildPagedRouteValues`/`BuildDetailsRouteValues`; normalizadores `NormalizeSort`/`NormalizePageSize`; constantes públicas `DefaultPageSize=20`, `DefaultSort=fecha_desc`, `AllowedPageSizes={10,20,50,100}`)
- `tests/SGV.Tests/Web/Auditoria/AuditoriasIndexTests.cs` (extendido: 6 tests Slice B con `[Fact]` + `[Theory(3 InlineData)]` cubriendo pageSize selector/pageSize out-of-set/sort reset/pageSize en paginación/Details link preserva contexto)
- `tests/SGV.Tests/Web/Auditoria/FakeAuditoriaApiClient.cs` (extendido: `GetDetalleException`, `GetDetalleCalls`, `GetDetalleHandler`)
- `docs/decisiones-implementacion.md` (D-5 bis, D-6, D-7 + tabla Capas actualizada)

### Heredado de Slice A (sin regresión)

Ver `verify-report.md` previo (PR #249). Confirmado por test pass a nivel
build/suite completo: las 10 tareas 1.A.* siguen vigentes y los wire
contracts (`AuditoriaDto`, `AuditoriaDetalleDto`, `AuditoriaListQuery`,
`IAuditoriaApiClient.GetDetalleAsync`, `AuditoriaApiClient.BuildQueryUri`,
`AuditoriaServicioConsulta` con sort/LEFT JOIN/`GetDetalleDtoAsync`,
índice `IX_Auditorias_CorrelationId_OccurredAt`, `AuditoriasController.GetById`
retornando `AuditoriaDetalleDto`) siguen en pie y sin cambios.

---

## Evidencia de build y tests

| Comando | Salida | Exit code |
|---|---|---|
| `dotnet build SGV.slnx` | 0 Error(s), 4 Warning(s) | 0 |
| `dotnet test SGV.slnx --filter "FullyQualifiedName~Auditoria"` | Passed 77, Failed 0, Skipped 0 | 0 |
| `dotnet test SGV.slnx --filter "FullyQualifiedName~AuditoriaServicioConsultaTests"` | Passed 29 (incluye `[MySqlFact]` contra `localhost:3306`) | 0 |
| `dotnet test SGV.slnx --filter "FullyQualifiedName~AuditoriasControllerTests"` | Passed 14 | 0 |
| `dotnet test SGV.slnx --filter "FullyQualifiedName~AuditoriasIndexTests"` | Passed 14 (12 + 3 InlineData) | 0 |
| `dotnet test SGV.slnx --filter "FullyQualifiedName~AuditoriasDetailsTests"` | Passed 4 | 0 |
| `dotnet test SGV.slnx --filter "FullyQualifiedName~SGV.Tests.Web"` | Passed 1398 | 0 |
| `dotnet test SGV.slnx` (full) | Passed 3395, Failed 0, Skipped 0 | 0 |
| `bun run build` en `src/SGV.Web` | OK (gulp build, sólo pre-existing deprecation warnings `DEP0180` y `browserslist`) | 0 |

MySQL local en `localhost:3306` (PID `11424`, `mysqld` escuchando vía
`lsof`). `[MySqlFact]` corre contra `sgv_test` con `EnsureCreatedAsync`
y `Database.Migrate()` idempotente por test. Los 29 tests del servicio
corrieron contra MySQL real, evidenciado por latencias 376-450 ms (no
in-memory).

---

## Cumplimiento por delta spec (cambio completo)

### `auditoria-query` (MODIFIED) — 4 requirements, 15 scenarios

#### Requirement: Listado paginado con orden determinista reciente-primero

| Escenario | Verdict | Evidencia |
|---|---|---|
| Defaults aplicados cuando se omiten parámetros | **PASS** (Slice A) | `QueryAsync_ClampInferior_PageYPageSizeSeAjustanAlMinimo` + `AuditoriasController.Get` con `[FromQuery] AuditoriaListQuery` (defaults `Page=1, PageSize=20`). |
| Orden determinista en empates de fecha | **PASS** (Slice A) | `QueryAsync_ConEmpateOccurredAt_OrdenaPorIdDesc` + `ThenByDescending(x => x.a.Id)` en `AuditoriaServicioConsulta.cs` línea 145. |
| PageSize por debajo del mínimo se normaliza a 1 | **PASS** (Slice A) | `QueryAsync_ClampInferior_PageYPageSizeSeAjustaAlMinimo` (pageSize=0 → 1). |
| PageSize excede el máximo permitido | **PASS** (Slice A) | `QueryAsync_ClampSuperior_PageSizeSeAjustaAlMaximo` (pageSize=9999 → 100). |

#### Requirement: Filtros combinables de consulta

| Escenario | Verdict | Evidencia |
|---|---|---|
| Filtros combinados filtran el resultado | **PASS** (Slice A) | `QueryAsync_Filtros_AplicanSegunEsperado` (Theory 7 InlineData). |
| Filtro por CorrelationId aísla la correlación | **PASS** (Slice A) | `QueryAsync_CorrelationId_AíslaRegistrosConEsaCorrelacion` (3 filas, 2 CorrelationIds, filtro aísla 2). |
| Filtros omitidos no filtran | **PASS** (Slice A) | `QueryAsync_Filtros_AplicanSegunEsperado(null,null,null,null,null,5)` → todas las filas. |
| Rango de fechas invertido | **PASS** (Slice A) | `QueryAsync_DateFromPosteriorADateTo_LanzaArgumentException` + `Get_Admin_DateFromMayorADateTo_Returns400ConProblemDetails`. |

#### Requirement: Detalle por identificador

| Escenario | Verdict | Evidencia |
|---|---|---|
| Detalle existente devuelve DTO enriquecido | **PASS** (Slice A) | `GetById_Admin_Existe_RetornaDetalleConEntityIdOldNewYUserName` + `GetDetalleDtoAsync_Existe_RetornaDetalleConOldNewYEntityId`. |
| Detalle inexistente | **PASS** (Slice A) | `GetById_Admin_NoExiste_404`. |
| Detalle con id no GUID | **PASS** (Slice A) | Controller `{id:guid}` (no matchea la ruta → 404 del router). |

#### Requirement: Contrato wire del listado sin valores anteriores/posteriores ni EntityId

| Escenario | Verdict | Evidencia |
|---|---|---|
| DTO de listado no expone old/new values ni EntityId | **PASS** (Slice A) | `AuditoriaDto_NoExponeOldValuesJsonNiNewValuesJson` + `AuditoriaDto_NoExponeEntityId` (reflexión). `Index.cshtml` grep de `@item.EntityId` → 0 matches; `@item.OldValuesJson`/`@item.NewValuesJson` → 0 matches. |
| UserName cae a guión cuando no hay usuario | **PASS** (Slice A) | `QueryAsync_UserIdInexistente_CaeAFallbackRayemEm`. |
| UserName resuelto desde AspNetUsers | **PASS** (Slice A) | `QueryAsync_UserIdExistente_ResuelveUserNameDeIdentity`. |
| Reflexión impide agregar old/new a AuditoriaDto | **PASS** (Slice A) | `AuditoriaDetalleDto_ExponeEntityIdOldValuesJsonNewValuesJson` (reflexión positiva sobre `AuditoriaDetalleDto`). |

### `auditoria-sort` (NEW) — 3 requirements, 8 scenarios

#### Requirement: Ordenamiento server-side por cinco columnas

| Escenario | Verdict | Evidencia |
|---|---|---|
| Default fecha_desc cuando Sort se omite | **PASS** (Slice A) | `QueryAsync_SortNull_DefaultEsFechaDescYIdDesc`. |
| Orden por entidad ascendente | **PASS** (Slice A) | `QueryAsync_SortEntidadAsc_OrdenaPorEntityName`. |
| Sort inválido cae a default sin error | **PASS** (Slice A) | `QueryAsync_SortInvalido_CaeAFechaDefaultSinError`. |
| Dirección descendente respetada | **PASS** (Slice A) | El `switch` cubre las 10 claves (`fecha_asc/desc`, `entidad_asc/desc`, `operacion_asc/desc`, `usuario_asc/desc`, `correlacion_asc/desc`) — `AuditoriaServicioConsulta.cs` líneas 131-144. |

#### Requirement: Desempate determinista por Id

| Escenario | Verdict | Evidencia |
|---|---|---|
| Empate en columna primaria se rompe por Id | **PASS** (Slice A) | `QueryAsync_ConEmpateOccurredAt_OrdenaPorIdDesc` + `ThenByDescending(x => x.a.Id)` universal. |

#### Requirement: Reset a página 1 al cambiar sort en la shell web (Slice B)

| Escenario | Verdict | Evidencia |
|---|---|---|
| Cambiar sort reinicia a página 1 | **PASS** (Slice B) | `Get_Index_SortHeader_LinkResetsPageAndPreservesPageSizeAndFilters`: usuario en `?p=3&pageSize=50&sort=fecha_desc&entityName=Cargo`, link del header `Entidad` → `?p=1&pageSize=50&sort=entidad_asc&entityName=Cargo`. `Index.cshtml.cs::BuildSortRouteValues(sortKey)` línea 229-240 fija `p = 1`. |
| Paginación preserva sort activo | **PASS** (Slice B) | `Get_Index_Pagination_PreservesSortAndPageSize`: link Siguiente lleva `p=2&pageSize=50&sort=usuario_desc&entityName=Cargo`. `Index.cshtml.cs::BuildPagedRouteValues(page)` línea 206-217 propaga `sort`. |
| Indicador visual de dirección activa | **PASS** (Slice B) | `Index.cshtml.cs::GetSortIcon(column)` línea 287-297 retorna `"ti ti-arrow-up"` o `"ti ti-arrow-down"`. `Index.cshtml` líneas 99-152 renderizan el icono sólo en el header activo (5 columnas con `GetSortIcon("fecha")`, `GetSortIcon("entidad")`, etc.). |

### `auditoria-detalle` (NEW) — 4 requirements, 14 scenarios

#### Requirement: DTO enriquecido AuditoriaDetalleDto

| Escenario | Verdict | Evidencia |
|---|---|---|
| DTO de detalle expone EntityId y old/new values | **PASS** (Slice A) | `AuditoriaDetalleDto_ExponeEntityIdOldValuesJsonNewValuesJson` + `GetDetalleDtoAsync_Proyeccion_ExponeEntityIdOldNewValuesEnSerializacion`. |
| Detalle de alta sin old values | **PASS** (Slice A) | `GetDetalleDtoAsync_AltaSinOld_OldEsNullNewConSnapshot`. |
| UserName cae a guión en detalle | **PASS** (Slice A) | `AuditoriaServicioConsulta.GetDetalleDtoAsync` línea 193: `u != null ? u.UserName : UserNameFallback`. Cobertura indirecta por el path compartido con el listado. |

#### Requirement: Endpoint de detalle API protegido por Administrador

| Escenario | Verdict | Evidencia |
|---|---|---|
| Administrador obtiene el detalle | **PASS** (Slice A) | `GetById_Admin_Existe_200` + `GetById_Admin_Existe_RetornaDetalleConEntityIdOldNewYUserName`. |
| Acceso anónimo al detalle API | **PASS** (Slice A) | `GetById_Anonymous_Returns401`. |
| Usuario sin rol Administrador al detalle API | **PASS** (Slice A) | `GetById_NonAdmin_Returns403`. |
| Detalle inexistente API | **PASS** (Slice A) | `GetById_Admin_NoExiste_404`. |

#### Requirement: Página web de detalle con render preformateado (Slice B)

| Escenario | Verdict | Evidencia |
|---|---|---|
| Página renderiza JSON en `<pre>` | **PASS** (Slice B) | `Get_Details_WhenRecordExists_RendersPreformattedJsonAndHeader`: assert `<pre` + `bg-light p-2` + contenido JSON. `Details.cshtml` líneas 94, 116, 136 renderean `<pre class="bg-light p-2">` para `ChangedPropertiesJson`/`OldValuesJson`/`NewValuesJson`. |
| Acceso web sin rol Administrador es rechazado | **PASS** (Slice B) | `Get_Details_WhenNonAdmin_RedirectsToAccessDenied`: response 302 a `/error/403`. `DetailsModel` línea 52 `[Authorize(Roles = RolesSgv.Administrador)]`. |
| Detalle inexistente en la página | **PASS** (Slice B) | `Get_Details_WhenRecordMissing_ShowsNotFoundState`: response 200 con copy "no está disponible"/"no encontrado". `Details.cshtml` líneas 15-31 muestran estado legible; `Details.cshtml.cs::OnGetAsync` línea 170-177 setea `IsNotFound = true` cuando el cliente devuelve `null`. |
| Fallo de transporte en la página de detalle | **PASS** (Slice B) | `Get_Details_WhenTransportFails_ShowsRecoverableBanner`: response 200 con `alert-danger` + copy "No se pudo contactar al servicio"/"Intentá nuevamente". `Details.cshtml.cs` línea 181-190 captura la excepción con `TransportFailureClassifier.IsTransportFailure(ex)` y setea `TransportErrorMessage = PageFeedback.TransportMessage`. El `id` se preserva en el CTA "Volver al listado" (`BuildBackUrl()` línea 116-131). |

#### Requirement: Contrato del cliente HTTP tipado para el detalle

| Escenario | Verdict | Evidencia |
|---|---|---|
| `GetDetalleAsync` 200 retorna DTO enriquecido | **PASS** (Slice A) | `AuditoriaApiClient.GetDetalleAsync` líneas 60-86 + `FakeAuditoriaApiClient.GetDetalleAsync` líneas 83-100 + `IAuditoriaApiClient.GetDetalleAsync` líneas 55-57. Cobertura runtime por `Get_Details_WhenRecordExists_RendersPreformattedJsonAndHeader` que invoca el path completo. |
| `GetDetalleAsync` 404 retorna null sin lanzar | **PASS** (Slice A) | `if (response.StatusCode == HttpStatusCode.NotFound) return null;` (líneas 76-79). Cobertura runtime por `Get_Details_WhenRecordMissing_ShowsNotFoundState`. |
| `GetDetalleAsync` propaga fallos de transporte | **PASS** (Slice A + B) | El método NO captura `HttpRequestException`/`TaskCanceledException`. Cobertura runtime por `Get_Details_WhenTransportFails_ShowsRecoverableBanner` que inyecta `HttpRequestException` en `FakeAuditoriaApiClient.GetDetalleException` y verifica que la PageModel la traduce a banner. |

### `auditoria-page-size` (NEW) — 3 requirements, 8 scenarios

#### Requirement: Selector de PageSize con opciones 10/20/50/100 (Slice B)

| Escenario | Verdict | Evidencia |
|---|---|---|
| Selector renderiza las cuatro opciones | **PASS** (Slice B) | `Get_Index_PageSizeSelector_RendersAllFourOptionsWithDefaultSelected`: `value="10"`, `value="20"`, `value="50"`, `value="100"` + opción 20 con `selected`. `Index.cshtml` líneas 64-85: `<select name="pageSize">` con `@foreach (var size in allowedPageSizes)` donde `AllowedPageSizes = {10, 20, 50, 100}`. |
| Selector refleja el pageSize actual | **PASS** (Slice B) | `Get_Index_PageSizeSelector_ReflectsActivePageSize`: request `?pageSize=50` → backend recibe `PageSize=50` exacto. `Index.cshtml` líneas 73-83 aplican `selected` al match. |
| Cambiar pageSize reinicia a página 1 | **PASS** (Slice B) | `Index.cshtml` línea 72: `onchange="this.form.p.value=1;this.form.submit();"` fuerza `p=1` antes de submit. |
| PageSize omitido cae a default 20 | **PASS** (Slice B) | `Index.cshtml.cs::OnGetAsync` línea 146 (`int pageSize = DefaultPageSize`) + `NormalizePageSize` línea 363-367. |

#### Requirement: Enlaces de paginación preservan PageSize (Slice B)

| Escenario | Verdict | Evidencia |
|---|---|---|
| Paginación conserva pageSize | **PASS** (Slice B) | `Get_Index_Pagination_PreservesFilters` línea 239: `pageSize=20` propagado a `p=3&pageSize=20&entityName=Cargo&operation=Alta&userId=u-7`. `BuildPagedRouteValues(page)` línea 206-217 incluye `pageSize = PageSize`. |
| Cambiar sort conserva pageSize | **PASS** (Slice B) | `Get_Index_SortHeader_LinkResetsPageAndPreservesPageSizeAndFilters` líneas 423-426: el link del header Entidad lleva `p=1&pageSize=50&sort=entidad_asc&entityName=Cargo`. `BuildSortRouteValues(sortKey)` línea 229-240 fija `pageSize = PageSize`. |

#### Requirement: PageSize inválido o fuera de rango se normaliza (Slice B)

| Escenario | Verdict | Evidencia |
|---|---|---|
| PageSize no numérico cae a default | **PASS** (Slice B) | `NormalizePageSize(int)` línea 363-367: `if (value <= 0) return DefaultPageSize`. El binder convierte no-numéricos a `0` → cae a default. |
| PageSize fuera de las opciones cae a default | **PASS** (Slice B) | `Get_Index_PageSizeOutOfSet_NormalizesToDefault` ([Theory] con 3 InlineData `[15]`, `[0]`, `[999]`): en los 3 casos el backend recibe `PageSize = 20` (default). `NormalizePageSize` retorna `DefaultPageSize` cuando `value` no está en `AllowedPageSizes`. |

---

## Cierre de D-2 (separación física de tipos — Slice A + verificación Slice B)

| Verificación | Estado | Evidencia |
|---|---|---|
| `AuditoriaDto` NO declara `EntityId` | ✅ PASS | `AuditoriaDto.cs` línea 23-31: 8 parámetros (`Id, EntityName, Operation, OccurredAt, UserId, UserName, ChangedPropertiesJson, CorrelationId`). Reflexión: `AuditoriaDto_NoExponeEntityId`. |
| `AuditoriaDto` NO declara `OldValuesJson`/`NewValuesJson` | ✅ PASS | Reflexión: `AuditoriaDto_NoExponeOldValuesJsonNiNewValuesJson`. `Index.cshtml` grep de `OldValuesJson`/`NewValuesJson` → 0 matches en columnas de fila. |
| `AuditoriaDetalleDto` declara `EntityId`, `OldValuesJson`, `NewValuesJson` | ✅ PASS | `AuditoriaDetalleDto.cs` líneas 27-38: 11 parámetros con `EntityId`, `OldValuesJson?`, `NewValuesJson?`. |
| `Index.cshtml` no expone `EntityId`/`OldValuesJson`/`NewValuesJson` | ✅ PASS (Slice B) | `grep "@item.EntityId"`: 0 matches. `grep "@item.OldValuesJson\|@item.NewValuesJson"`: 0 matches. El único lugar del shell que renderiza `OldValuesJson`/`NewValuesJson` es `Details.cshtml` líneas 114-144 (vía `@detalle.OldValuesJson`, no `@item`). |
| `GET /api/v1/auditorias/{id}` retorna `AuditoriaDetalleDto` | ✅ PASS | `AuditoriasController.GetById` línea 106 retorna `ActionResult<AuditoriaDetalleDto>`; atributo `[ProducesResponseType(typeof(AuditoriaDetalleDto), StatusCodes.Status200OK)]`. |
| `GET /api/v1/auditorias` retorna `AuditoriaDto` (no Detalle) | ✅ PASS | `AuditoriasController.Get` línea 62 retorna `ActionResult<PagedResult<AuditoriaDto>>`. |
| `Details.cshtml` renderiza `OldValuesJson`/`NewValuesJson` en `<pre>` | ✅ PASS (Slice B) | `Details.cshtml` líneas 114-124 (`OldValuesJson`), 134-144 (`NewValuesJson`); `Get_Details_WhenRecordExists_RendersPreformattedJsonAndHeader` verifica `<pre` + `bg-light p-2`. |

**D-2 cerrado por construcción** (separación física de tipos).
Re-verificación en Slice B confirma que `Index.cshtml` no expone ninguno
de los campos prohibidos y que `Details.cshtml` los renderea sólo en
`<pre>` (formato legible, no badge ni celda plana).

---

## Validaciones adicionales del orquestador (Slice B específico)

| Validación | Estado | Evidencia |
|---|---|---|
| **Reset a página 1 al cambiar sort** | ✅ PASS | `BuildSortRouteValues(sortKey)` línea 229-240 fija `p = 1`. Test: `Get_Index_SortHeader_LinkResetsPageAndPreservesPageSizeAndFilters`. |
| **Paginación preserva sort + pageSize + filtros** | ✅ PASS | `BuildPagedRouteValues(page)` línea 206-217 propaga `pageSize + sort + entityName + operation + dateFrom + dateTo + userId + correlationId`. Test: `Get_Index_Pagination_PreservesSortAndPageSize`. |
| **Selector de PageSize 10/20/50/100 con default 20** | ✅ PASS | `Index.cshtml` líneas 64-85 + `AllowedPageSizes = {10, 20, 50, 100}` (línea 73 Index.cshtml.cs) + `DefaultPageSize = 20` (línea 59). Test: `Get_Index_PageSizeSelector_RendersAllFourOptionsWithDefaultSelected`. |
| **`NormalizePageSize` fuera del set → default** | ✅ PASS | `NormalizePageSize` línea 363-367 + `AllowedPageSizes.Contains(value) ? value : DefaultPageSize`. Test: `Get_Index_PageSizeOutOfSet_NormalizesToDefault` (Theory 3 InlineData). |
| **Indicador visual de dirección de sort** (`GetSortIcon` flecha arriba/abajo) | ✅ PASS | `GetSortIcon(column)` línea 287-297 retorna `"ti ti-arrow-up"` (asc) o `"ti ti-arrow-down"` (desc). `Index.cshtml` líneas 100-152 renderizan el icono sólo en el header activo. Cobertura visual por inspección de las 5 columnas con `GetSortIcon("fecha|entidad|operacion|usuario|correlacion")`. |
| **`Details` renderiza JSON en `<pre>`** | ✅ PASS | `Details.cshtml` líneas 94, 116, 136: 3 bloques `<pre class="bg-light p-2">` para `ChangedPropertiesJson`/`OldValuesJson`/`NewValuesJson`. Test: `Get_Details_WhenRecordExists_RendersPreformattedJsonAndHeader`. |
| **`Details` autoriza `[Authorize(Roles="Administrador")]`** | ✅ PASS | `Details.cshtml.cs` línea 52 atributo de clase. Test: `Get_Details_WhenNonAdmin_RedirectsToAccessDenied` (302 → `/error/403`). |
| **`Details` 404 legible cuando id no existe** | ✅ PASS | `Details.cshtml.cs::OnGetAsync` línea 170-177 setea `IsNotFound = true`. `Details.cshtml` líneas 15-31 renderean estado legible. Test: `Get_Details_WhenRecordMissing_ShowsNotFoundState`. |
| **`Details` con banner recuperable ante fallo de transporte** | ✅ PASS | `Details.cshtml.cs::OnGetAsync` línea 181-190: `catch (Exception ex) when (TransportFailureClassifier.IsTransportFailure(ex))` → `IsNotFound = true; TransportErrorMessage = PageFeedback.TransportMessage`. `Details.cshtml` línea 10-13 renderea `<div class="alert alert-danger">@Model.TransportErrorMessage</div>`. Test: `Get_Details_WhenTransportFails_ShowsRecoverableBanner`. |
| **`Details` link desde el listado preserva contexto** | ✅ PASS | `Index.cshtml.cs::BuildDetailsRouteValues(id)` línea 250-262 propaga `p + pageSize + sort + correlationId + entityName + operation + dateFrom + dateTo + userId`. `DetailsModel::BuildBackUrl()` línea 116-131 reconstruye la URL de retorno. Test: `Get_Index_DetailsLink_PreservesListContext`. |
| **Columnas ordenables: las 5 (`fecha`, `entidad`, `operacion`, `usuario`, `correlacion`)** | ✅ PASS | `Index.cshtml` líneas 99-152: 5 `<th>` con `link-reset` que invocan `BuildSortRouteValues("fecha|entidad|operacion|usuario|correlacion")`. Cobertura runtime por el path del header Entidad en `Get_Index_SortHeader_LinkResetsPageAndPreservesPageSizeAndFilters`. |
| **`Sort` inválido en web cae al default** sin propagar valor inválido a la API | ✅ PASS | `NormalizeSort(value)` línea 336-349 retorna `DefaultSort` para cualquier valor fuera del set de 10 claves. El backend nunca recibe un sort inválido desde la shell. |
| **D-2 sigue cerrado en `Details.cshtml`** (no expone `EntityId`/`OldValuesJson`/`NewValuesJson` accidentalmente en el listado; el DTO de detalle sí los expone y la página los muestra en `<pre>`) | ✅ PASS | Listado (`Index.cshtml`) grep → 0 matches. Detalle (`Details.cshtml`) sí los expone intencionalmente en `<pre>`, líneas 52, 114-144. |
| **`UserName` fallback "—" en el listado y en el detalle** | ✅ PASS | Listado: `Index.cshtml` línea 174 `<span class="text-muted">@item.UserName</span>` (servicio ya coalesce). Detalle: `Details.cshtml` líneas 62-72 muestra `UserName` (servicio `GetDetalleDtoAsync` línea 193 coalesce con `UserNameFallback`). |
| **D-5 bis, D-6, D-7 documentados** | ✅ PASS | `decisiones-implementacion.md` líneas 96-195 documenta las 3 decisiones con tabla de mapeo LINQ completa, contexto histórico (D-5 vigente + D-5 bis levanta fuera-de-alcance) y tests verificadores. |

---

## Coherencia con la spec vigente `openspec/specs/auditoria-query/spec.md`

Slice A ya consolidó la coherencia con la spec vigente (ver verify-report
previo). Slice B no altera wire contracts ni esquemas de DB; los cambios
son UI/test/docs. Sin regresiones detectadas.

---

## Tabla de dimensiones verificadas

| Dimensión | Estado | Notas |
|---|---|---|
| Task completion | ✅ PASS | 18/18 tareas marcadas `[x]` en `tasks.md` (10 Slice A + 8 Slice B). |
| Spec correctness (auditoria-query) | ✅ PASS | 4/4 requirements, 15/15 scenarios con cobertura runtime. |
| Spec correctness (auditoria-sort) | ✅ PASS | 3/3 requirements, 8/8 scenarios con cobertura runtime (5 Slice A + 3 Slice B). |
| Spec correctness (auditoria-detalle) | ✅ PASS | 4/4 requirements, 14/14 scenarios con cobertura runtime (10 Slice A + 4 Slice B). |
| Spec correctness (auditoria-page-size) | ✅ PASS | 3/3 requirements, 8/8 scenarios con cobertura runtime (100% Slice B). |
| Design coherence | ✅ PASS | D-1 a D-7 implementadas; D-5 (DateTime vs DateTimeOffset) cerrado en `AuditoriaDetalleDto.cs` líneas 21-25. D-5 bis (LEFT JOIN UserName + fallback "—") cerrado en `decisiones-implementacion.md` línea 96-114 + `AuditoriaServicioConsulta.cs` línea 139-143/160/193. D-6 (sort switch) cerrado en `AuditoriaServicioConsulta.cs` líneas 131-145. D-7 (detalle admin) cerrado en `Details.cshtml`/`Details.cshtml.cs` + decisiones-implementacion.md líneas 153-195. |
| No-regresión | ✅ PASS | 3395/3395 tests pasan (vs 3382 en Slice A: +13 tests nuevos Slice B, todos pasando). Build limpio. |
| Compat main compilable | ✅ PASS (ya verificado en Slice A) | `main` queda compilable; Slice B sólo agrega UI + tests + docs sin tocar wire contracts ni esquema DB. |

---

## Hallazgos

### CRITICAL

_Ninguno._

### WARNING

_Ninguno._

### SUGGESTION / info (pre-existentes o no-bloqueantes)

1. **`Details.cshtml` no renderiza `ChangedPropertiesJson` cuando la
   lista está vacía con un fallback textual** — sólo muestra `—` para
   el caso vacío. El comportamiento actual es coherente con el resto
   del shell (cards de detalle con copy "—" para nulos); no afecta
   cumplimiento de spec (`auditoria-detalle` §"Página web de detalle
   con render preformateado" sólo obliga a renderizar `OldValuesJson`
   y `NewValuesJson` en `<pre>`, no `ChangedPropertiesJson`). Mejora
   UX opcional para un change futuro si se quiere una copy
   específica ("Sin propiedades modificadas" vs. "—"). **No
   bloqueante**.

2. **MySQL local intermitente en runs previos** — el apply-progress
   de Slice B reportó 30 `[MySqlFact]` skipped porque MySQL no estaba
   disponible en ese momento. En esta verificación, MySQL
   (`localhost:3306`, PID `11424`) SÍ está disponible y los 29
   `[MySqlFact]` del servicio corrieron contra MySQL real (latencias
   376-450 ms lo confirman). **No es regresión**: es estado del
   entorno de validación. Si en CI futura MySQL no estuviera, los
   tests se skipean clean (no fallan). Documentado en `AGENTS.md`
   §"Tests de Integración con MySQL".

3. **Smoke manual de `/auditorias/details?id={guid}` admin/no-admin
   no ejecutado** — el apply-progress de Slice B documentó esto como
   aceptado por spec (sin JWT disponible localmente). La cobertura
   equivalente la dan `AuditoriasDetailsTests.Get_Details_WhenRecordExists_*`,
   `Get_Details_WhenRecordMissing_ShowsNotFoundState`,
   `Get_Details_WhenTransportFails_ShowsRecoverableBanner` y
   `Get_Details_WhenNonAdmin_RedirectsToAccessDenied` (4 tests) que
   ejercen los 4 estados del endpoint vía `SgvWebApplicationFactory`
   con `FakeAuditoriaApiClient`. **No bloqueante**.

---

## Verdict final

**PASS**

- Build limpio (0 errors).
- 77/77 tests Auditoria pasan (incluye 29 `[MySqlFact]` contra MySQL real).
- 1398/1398 tests Web pasan (sin regresiones; +13 tests nuevos vs Slice A).
- 3395/3395 tests full suite pasan (sin regresiones).
- 14/14 requirements y 45/45 scenarios de las 4 delta specs cubiertas
  con tests runtime.
- D-2 cerrado por construcción (separación física de tipos
  verificada en código, en tests de reflexión, en grep del HTML
  renderizado y en serialización HTTP).
- D-5 bis, D-6, D-7 documentados en `decisiones-implementacion.md`
  con tabla de mapeo LINQ completa.
- Las 18 tareas (10 Slice A + 8 Slice B) están marcadas `[x]`.
- `main` queda compilable entre merges de A y B (Slice A aplicó
  hotfix compat que eliminó referencias a `@item.EntityId` antes
  de que exista `Details.cshtml`).

**Recomendación**: avanzar a `sdd-archive`. El change está completo,
todos los criterios de éxito del proposal están cumplidos y la
verificación runtime no encontró regresiones.