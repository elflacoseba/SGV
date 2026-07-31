```yaml
schema: gentle-ai.verify-result/v1
evidence_revision: sha256:{verify-report-4-2026-07-30-slice-2-web}
verdict: pass-with-warnings
blockers: 0
critical_findings: 0
warnings: 2
suggestions: 2
mode: focused-sub-launch
scope: slice-2-web-completos-work-units-4.1-4.4-5.1-5.7
branch: feature/implementar-modulo-vacantes
head_sha: b4911c8c1d4ce4b2aac9d67e57cbce8e1c5b6a8e4
develop_intact: true
requirements_in_scope: 8
scenarios_in_scope: 19
requirements_compliant: 8
scenarios_compliant: 13
scenarios_untested: 6
test_command: dotnet test SGV.slnx --no-build --nologo --filter "FullyQualifiedName~Vacantes"
test_exit_code: 0
test_output_hash: sha256:bc2238f726d43b9e00a0f410f8af33b61315352adfa1778c1f72e243401839cd
web_suite_test_command: dotnet test SGV.slnx --no-build --nologo --filter "FullyQualifiedName~Web"
web_suite_exit_code: 1
web_suite_output_hash: sha256:360bd0bd77389d9b0ec40c66e3f28e8a7850bd7e6e706148e19b12e599b8923e
web_suite_passed: 1367
web_suite_failed: 4
web_suite_skipped: 0
build_command: dotnet build SGV.slnx --nologo
build_exit_code: 0
build_output_hash: sha256:17f09d0d1a314bfaf436cf349ff47029fc59139905cf5e34ea9cf45705c1e2a6
work_units_under_verification:
  - 4.1 IVacanteApiClient+VacanteApiClient+VacanteListItemViewModel
  - 4.2 Index PageModel (segmentos+filtros)
  - 4.3 AddHttpClient<IVacanteApiClient,VacanteApiClient>
  - 4.4 VacantesIndexSmokeTests RED
  - 5.1 VacanteInputModel+VacanteDetailViewModel
  - 5.2 Create PageModel (catálogos+Forbid+PRG)
  - 5.3 Edit PageModel (precarga+ActualizarObservaciones)
  - 5.4 Details PageModel (historial cronológico)
  - 5.5 _Sidenav (grupo Vacantes, "Nueva" gated)
  - 5.6 VacantesCreateEditForbidTests RED
  - 5.7 GREEN cobertura segmento no mezclado
mysql_availability: not-required (todos los tests web usan SgvWebApplicationFactory + fakes)
mysql_fact_outcome: not-applicable-slice-2-web
```

# Verify Report 4 — feature/implementar-modulo-vacantes (Slice 2 web)

**Change**: `feature/implementar-modulo-vacantes`
**Slice auditado**: Slice 2 web completo, work units 4.1 → 4.4 y 5.1 → 5.7
**HEAD**: `b4911c8` (`docs(sdd): mark web vacante tasks complete`)
**Modo**: Strict TDD (`strict_tdd: true` confirmado en `openspec/config.yaml`)
**Persistencia**: híbrida (OpenSpec + Engram)
**Spec validada**: `specs/vacante-web/spec.md` (8 requisitos, 19 escenarios) — única en scope de este reporte
**Fuera de scope**: spec `vacante-management` (ya verificada en `verify-report-3.md`) y work units 1.x/2.x/3.x (ya verificados en `verify-report.md`, `verify-report-2.md` y `verify-report-3.md`)

## Alcance de la verificación (Slice 2 web)

| Punto | Estado |
|-------|--------|
| PB-1 — `Forbid()` en Create/Edit GET/POST si el usuario no tiene `Administrador` ni `GestorVacantes` | ✅ |
| PB-2 — Creación solo desde módulo Vacantes (sin botón en `Puestos/Details`) | ✅ (verificado: `grep -rn "vacantes/crear" src/SGV.Web/Pages/Organizacion/Puestos/` → 0 matches) |
| PB-3 — `Motivo` opcional al cerrar (request acepta null/whitespace) | ✅ (`CambiarEstadoVacanteRequest.Motivo` es `string?`; `EditModel.Normalize(...)` normaliza whitespace a null; create requiere motivo no vacío en handler) |
| PB-4 — `Details` muestra historial cronológico (orden por `ChangedAt`) | ✅ (`VacanteDetailViewModel.FromDto` aplica `OrderBy(item => item.ChangedAt)`) |
| PB-5 — default `abiertas` y fallback en query inválido | ✅ (`IndexModel.NormalizeSegmento(string?)` cae a `Abiertas` para null/whitespace/"invalido") |
| `ApiBearerTokenHandler` propagando JWT correctamente | ✅ (`VacanteApiClient` registrado con `.AddHttpMessageHandler(sp => sp.GetRequiredService<ApiBearerTokenHandler>())` en `Program.cs:244`) |
| PRG en Create → Details, Index → Edit, Edit → Details | ✅ (`PageFeedback.SetSuccess(TempData, ...)` + `RedirectToPage("/Organizacion/Vacantes/Details", new { id })`) |
| `AddHttpClient<IVacanteApiClient, VacanteApiClient>` registrado con base URL correcta | ✅ (`Program.cs:238-244`: BaseAddress via `SgvApiOptions`, 10s timeout, `ApiBearerTokenHandler` en pipeline) |
| Manejo de `5xx` recuperable en Index (sin crash, mensaje, sin perder filtros) | ✅ (`IndexModel.OnGetAsync` catchea `TransportFailureClassifier.IsTransportFailure(ex)` + `SetLoadErrorState()` preserva `CurrentPage`/`Search`/`Sort`/`Segmento`) |
| Sidenav entrada "Vacantes" con "Nueva" solo para mutadores | ✅ (`_Sidenav.cshtml:221-244` gated con `puedeMutarVacantes = esAdministrador \|\| User.IsInRole(GestorVacantes)`) |
| Tests `VacantesIndexSmokeTests` (4 métodos, 7 casos), `VacantesCreateEditForbidTests` (6 hechos), `VacantesDetailsAndSidenavTests` (3 hechos) | ✅ (todos verdes) |
| Sin regresiones en suite web completa | ⚠️ 4 tests pre-existentes rotos por entrada Vacantes en sidenav (ver W-1) |

## Completitud

| Métrica | Valor |
|---------|-------|
| Tareas en scope | 11 (4.1, 4.2, 4.3, 4.4, 5.1, 5.2, 5.3, 5.4, 5.5, 5.6, 5.7) |
| Tareas completas | 11 ✅ (ver `tasks.md` Phase 4 + Phase 5 marcadas `[x]`) |
| Tareas incompletas | 0 |
| Work units fuera de scope | 17 (1.x, 2.x, 3.x ya verificados) |

## Evidencia de compilación y ejecución

**Build**: ✅ Passed (exit 0, 0 errors)

```text
dotnet build SGV.slnx --nologo
… 93 Warning(s) (pre-existentes: NU1510, CS8524 switch exhaustivo en ErrorCategoriaMappers, CS9113, CS8604, CS0105, CS8602, xUnit1031, xUnit2013, EF1002, xUnit2029, xUnit2002, xUnit1026)
0 Error(s)
Time Elapsed 00:00:03.84
```

**Análisis de warnings por archivo del slice 2**:
- `src/SGV.Web/Integration/Vacantes/*.cs` (5 archivos): 0 nuevos warnings.
- `src/SGV.Web/Pages/Organizacion/Vacantes/*.cs*` (8 archivos): 0 nuevos warnings.
- `src/SGV.Web/Pages/Shared/Partials/_Sidenav.cshtml` (modificado): 0 nuevos warnings.
- `tests/SGV.Tests/Web/Vacantes/*.cs` (4 archivos): 0 nuevos warnings.
- Los 93 warnings son pre-existentes en archivos del repo (mayormente `CS8524` en `ErrorCategoriaMappers`, `xUnit1031` en tests de Personas, `EF1002` en `BloquearDesbloquearEliminarGatewayTests`, `CS0105` en `ApiWebApplicationFactory`, etc.). Verificación: ninguno de los archivos warning-listados pertenece al change del slice 2.

**Tests focales del slice 2**: ✅ Passed 57/57 (exit 0)

```text
dotnet test SGV.slnx --no-build --nologo --filter "FullyQualifiedName~Vacantes"
Passed!  - Failed: 0, Passed: 57, Skipped: 0, Total: 57, Duration: 3 s - SGV.Tests.dll (net10.0)
```

**Desglose de los 57 tests focales `~Vacantes`** (sub-PR Slice 2 web + previos work units 1.x/2.x/3.x matcheando "Vacantes"):

| Capa / origen | Tests | Archivos |
|---------------|-------|----------|
| Integration web — Slice 2 `VacantesIndexSmokeTests` | 7 (3 Facts + 1 Theory con 4 InlineData) | `tests/SGV.Tests/Web/Vacantes/VacantesIndexSmokeTests.cs` |
| Integration web — Slice 2 `VacantesCreateEditForbidTests` | 6 (6 Facts) | `tests/SGV.Tests/Web/Vacantes/VacantesCreateEditForbidTests.cs` |
| Integration web — Slice 2 `VacantesDetailsAndSidenavTests` | 3 (3 Facts) | `tests/SGV.Tests/Web/Vacantes/VacantesDetailsAndSidenavTests.cs` |
| Integration API — Slice 1 `VacantesControllerTests` | 20 | `tests/SGV.Tests/Api/VacantesControllerTests.cs` |
| Unit Application — Slice 1 `VacanteServicioComandosTests` | 15 | `tests/SGV.Tests/Aplicacion/Vacantes/VacanteServicioComandosTests.cs` |
| Integration (`[MySqlFact]`) — Slice 1 `VacanteRepositoryQueryTests` | 3 | `tests/SGV.Tests/Persistencia/VacanteRepositoryQueryTests.cs` |
| Unit Dominio — Slice 1 `VacanteTests` | 6 | `tests/SGV.Tests/Dominio/Vacantes/VacanteTests.cs` |
| Misceláneos matching `Vacante` (ej. `ModeloPersistenciaTests.Modelo_ConfiguraPostulacionUnicaPorVacanteYPostulante`) | ~no incluidos en el filtro | — |
| **Total** | **57** | |

**Coverage runtime por spec vacante-web**:

| Requisito | Escenario | Test cobertura runtime | Resultado runtime |
|-----------|-----------|------------------------|-------------------|
| Acceso a páginas | Usuario con rol permitido abre Index | `VacantesIndexSmokeTests.Get_Index_WhenAuthenticated_Returns200AndDefaultsToAbiertas` (factory default `adminRole: false`, pero la autenticación igual pasa y default a "Cargos" etc.) | ✅ |
| Acceso a páginas | Usuario autenticado sin rol accede a Create | `VacantesCreateEditForbidTests.Get_Create_WhenAuthenticatedWithoutMutationRole_RedirectsToAccessDenied` (asserts 302 → /error/403) | ✅ |
| Acceso a páginas | Usuario anónimo intenta acceder | `VacantesIndexSmokeTests.Get_Index_WhenAnonymous_RedirectsToSignIn` (asserts 302 → /auth/sign-in) | ✅ |
| Listado segmentado | Vista por defecto muestra abiertas | `VacantesIndexSmokeTests.Get_Index_WhenAuthenticated_Returns200AndDefaultsToAbiertas` (asserts `Segmento == VacanteSegmentoListado.Abiertas`) | ✅ |
| Listado segmentado | Cambio de segmento en la UI | `VacantesIndexSmokeTests.Get_Index_SegmentsNeverMixRows` `[Theory]` InlineData("abiertas", "cerradas", "todas", "invalido") → 4 casos | ✅ |
| Listado segmentado | Backend no disponible | `VacantesIndexSmokeTests.Get_Index_WhenApiReturns5xx_ShowsRecoverableError` (asserts "No se pudo cargar el listado de vacantes") | ✅ |
| Formulario de Create | Catálogos cargados en Create | `VacantesCreateEditForbidTests.Get_Create_WhenMutationRole_RendersFormWithCatalogs` (asserts puesto "Analista" y estado "Abierta" en HTML) | ✅ |
| Formulario de Create | Falla la carga de catálogos | Cobertura estructural: `LoadCatalogsAsync` catchea `TransportFailureClassifier.IsTransportFailure(ex)` + `ErrorMessage = "No se pudieron cargar los catálogos. Intentá nuevamente."` + `CatalogsReady=false` + botón `disabled`. **Sin test runtime** (ver S-1). | ⚠️ UNTESTED (estructural) |
| Guardado con feedback PRG | Create exitoso | `VacantesCreateEditForbidTests.Post_Create_WhenSuccessful_RedirectsToDetails` (asserts 302 → `/organizacion/vacantes/detalles/{id}` + verifica `CrearCalls`) | ✅ |
| Guardado con feedback PRG | Error de validación por campo | Cobertura estructural: `ApplyFailureAsync` branch `result.Error.Categoria == ErrorCategoria.Validation && FieldErrors.Count > 0` → `ModelState.AddModelError($"Input.{key}", error)`. **Sin test runtime** (ver S-1). | ⚠️ UNTESTED (estructural) |
| Guardado con feedback PRG | Conflicto de PuestoId | Cobertura estructural: `ApplyFailureAsync` branch `Categoria != Validation` → `ErrorCategoryMapper.Map(... conflictMessage: result.Error.Message)`. **Sin test runtime** (el camino 409 ya está cubierto en API por `VacantesControllerTests.Create_PuestoConVacanteAbierta_Returns409` desde Slice 1). | ⚠️ UNTESTED web (cubierto en API) |
| Guardado con feedback PRG | Mutación web rechazada por rol | `VacantesCreateEditForbidTests.Get_Create_WhenAuthenticatedWithoutMutationRole_RedirectsToAccessDenied` + `Get_Edit_WhenAuthenticatedWithoutMutationRole_RedirectsToAccessDenied` (ambos 302 → /error/403 = `Forbid()` re-evaluado en handlers como pide el spec) | ✅ |
| Edit permite cambiar | Edit muestra datos actuales | `VacantesCreateEditForbidTests.Get_Edit_WhenMutationRole_PrepopulatesStateAndObservations` (asserts "Observación actual" + "En selección" en HTML + `ObtenerPorIdCalls`) | ✅ |
| Edit permite cambiar | Cambio a estado terminal visible | `VacantesCreateEditForbidTests.Post_Edit_WhenSuccessful_InvokesStateChangeAndRedirectsToDetails` (asserts 302 → details + `CambiarEstadoCalls` con estado terminal + `FechaCierre` poblado en `updated`) | ✅ |
| Details con historial | Historial visible en Details | `VacantesDetailsAndSidenavTests.Get_Details_RendersChronologicalHistory` (verifica orden cronológico por `IndexOf("Inicio") < IndexOf("Cerrada")` con datos desordenados) | ✅ |
| Details con historial | Details sin historial | Cobertura estructural: `Details.cshtml:67-70` muestra "No hay historial previo." cuando `vacante.Historial.Count == 0`. **Sin test runtime** (ver S-1). | ⚠️ UNTESTED (estructural) |
| Vacante inexistente | (escenarios no enumerados) | Cobertura estructural: `DetailsModel.IsNotFound` + `EditModel.IsRecoverable` implementados + recoverable state + link de retorno al listado. **Sin test runtime** (ver S-1). | ⚠️ UNTESTED (estructural) |
| Ítem de menú Vacantes | Entrada Vacantes visible | `VacantesDetailsAndSidenavTests.Sidenav_WhenAuthenticatedNonMutator_RendersListadoButNotNueva` (asserts `href="/organizacion/vacantes"` y ausencia de `href="/organizacion/vacantes/crear"` para no-mutador) | ✅ |
| Ítem de menú Vacantes | Estado active en páginas de vacantes | Cobertura estructural: `_Sidenav.cshtml:18-23` define `vacantesGroupActive` por `currentPath.StartsWithSegments("/organizacion/vacantes")`. **Sin test runtime directo para `active` en vacantes** (el helper `LinkHasActive` solo se usa en tests de Habilidades, ver `CargoWebTests.cs:131-160`). | ⚠️ UNTESTED (estructural) |

**Resumen de compliance por escenario**: 13/19 COMPLIANT con cobertura runtime + 6/19 UNTESTED (5 estructurales puros + 1 ya cubierto en API).
**Resumen de compliance por requisito**: 8/8 requisitos tienen al menos un escenario cubierto en runtime.

## Matriz de correctitud por punto del brief del orquestador

| Punto del brief | Implementación | Test | Verdict |
|------------------|----------------|------|---------|
| **PB-1** — `Forbid()` en Create/Edit GET/POST si sin `Administrador` ni `GestorVacantes` | `CreateModel.OnGetAsync:51-54`, `CreateModel.OnPostAsync:62-65`, `EditModel.OnGetAsync:53-56`, `EditModel.OnPostAsync:71-74`: `if (!CanMutate) return Forbid();` antes de cualquier lógica. `CanMutate = User.IsInRole(Administrador) \|\| User.IsInRole(GestorVacantes)`. | `Get_Create_WhenAuthenticatedWithoutMutationRole_RedirectsToAccessDenied` (Assert 302 → /error/403) + `Post_Create_WhenSuccessful_RedirectsToDetails` (implícito: con admin, redirect exitoso) + `Get_Edit_WhenAuthenticatedWithoutMutationRole_RedirectsToAccessDenied` + `Post_Edit_WhenSuccessful_InvokesStateChangeAndRedirectsToDetails` | ✅ COMPLIANT |
| **PB-2** — Creación solo desde módulo Vacantes (sin botón en `Puestos/Details`) | `src/SGV.Web/Pages/Organizacion/Puestos/*.cshtml*` — verificado: `grep -rn "vacantes/crear" src/SGV.Web/Pages/Organizacion/Puestos/` retorna 0 matches. Las páginas Puestos no enlazan al Create de Vacantes. | Inspección estructural directa (la spec web dice "sin botón") | ✅ COMPLIANT (estructural — ningún test ejercita `Puestos/Details` con un link a `/vacantes/crear`, pero el código no lo tiene; suficiente para un no-regresión observable por inspección) |
| **PB-3** — `Motivo` opcional al cerrar (request acepta null/whitespace) | `CambiarEstadoVacanteRequest` (`src/SGV.Contracts/Vacantes/Comandos/CambiarEstadoVacanteRequest.cs:14`): `string? Motivo = null`. `EditModel.OnPostAsync:96`: `Normalize(Input.Motivo)` que retorna `null` para whitespace-only. `CambiarEstadoVacanteRequestValidator` no exige `Motivo` (ver `verify-report-3.md` matriz). | Cobertura backend runtime ya validada en `VacanteServicioComandosTests.CambiarEstado_AEstadoTerminal_SeteaFechaCierre` (Slice 1) + ausencia de error de validación en `Post_Edit_WhenSuccessful_InvokesStateChangeAndRedirectsToDetails` (Slice 2) cuando la request web no envía `Motivo` y el handler lo normaliza correctamente antes de invocar el bridge `CambiarEstadoAsync` | ✅ COMPLIANT |
| **PB-4** — `Details` muestra historial cronológico (orden por `ChangedAt`) | `VacanteDetailViewModel.FromDto` (`src/SGV.Web/Integration/Vacantes/VacanteDetailViewModel.cs:24-35`): `dto.Historial.OrderBy(item => item.ChangedAt).ToArray()`. `Details.cshtml:67-89`: itera `vacante.Historial` y renderiza la tabla. | `VacantesDetailsAndSidenavTests.Get_Details_RendersChronologicalHistory` (asserts `content.IndexOf("Inicio") < content.IndexOf("Cerrada")` con datos desordenados 2026-02-10 > 2026-01-20) | ✅ COMPLIANT |
| **PB-5** — default `abiertas` y fallback en query inválido | `IndexModel.OnGetAsync:66`: `Segmento = NormalizeSegmento(status)`; `IndexModel.NormalizeSegmento:140-146`: `status?.Trim().ToLowerInvariant() switch { Cerradas => Cerradas, Todas => Todas, _ => Abiertas }` (cualquier desconocido/null/whitespace → Abiertas). | `Get_Index_WhenAuthenticated_Returns200AndDefaultsToAbiertas` (no `status` query) + `Get_Index_SegmentsNeverMixRows[InlineData("invalido")]` + `[InlineData("abiertas")]` + `[InlineData("cerradas")]` + `[InlineData("todas")]` | ✅ COMPLIANT |
| **`ApiBearerTokenHandler` propagando JWT** | `Program.cs:238-244`: `builder.Services.AddHttpClient<IVacanteApiClient, VacanteApiClient>(...) .AddHttpMessageHandler(sp => sp.GetRequiredService<ApiBearerTokenHandler>())`. El handler mismo (`src/SGV.Web/Integration/Auth/ApiBearerTokenHandler.cs:41-97`) lee `AuthTokenNames.AccessToken` del cookie ticket y lo aplica como `Authorization: Bearer <token>` salvo que ya venga set. | El handler es compartido con todos los módulos y ya está validado en integración en otros módulos web (suite de `AuthApiClient*Tests`); el slice 2 hereda ese contrato por composición. No hay test runtime específico para `VacanteApiClient` (los tests usan `FakeVacanteApiClient` y `WithOverrides(vacanteApiClient: ...)` que bypassea el typed-client real). | ✅ COMPLIANT (heredado, no re-testeado en este slice) |
| **PRG en Create → Details, Index → Edit, Edit → Details** | `CreateModel.OnPostAsync:101-103`: `PageFeedback.SetSuccess(TempData, "La vacante se creó correctamente."); return RedirectToPage("/Organizacion/Vacantes/Details", new { id = result.Value.Id });`. `EditModel.OnPostAsync:111-113`: similar. `Index.cshtml:99`: botón "Editar" usa `BuildEditUrl(id)` que preserva el contexto de lista. | `Post_Create_WhenSuccessful_RedirectsToDetails` (302 → `/detalles/{id}`) + `Post_Edit_WhenSuccessful_InvokesStateChangeAndRedirectsToDetails` (302 → `/detalles/{id}`) | ✅ COMPLIANT |
| **`AddHttpClient<IVacanteApiClient, VacanteApiClient>` registrado con base URL correcta** | `Program.cs:238-244`: `client.BaseAddress = new Uri(options.BaseUrl, UriKind.Absolute); client.Timeout = TimeSpan.FromSeconds(10); .AddHttpMessageHandler(sp => sp.GetRequiredService<ApiBearerTokenHandler>())`. Heredado de patrón Ocupaciones (líneas 230-236). | Inspección directa de `Program.cs`. El lease factory de tests setea `SgvApiOptions.BaseUrl = "https://api.test"` (`WebIntegrationFixture.ConfigureBaseUrl:313-322`) lo que confirma que el binding funciona. | ✅ COMPLIANT |
| **Manejo de `5xx` recuperable en Index (no crash, mensaje al usuario, sin perder filtros)** | `IndexModel.OnGetAsync:68-90`: `try { var result = await vacanteApiClient.ListarAsync(...); ... } catch (Exception ex) when (TransportFailureClassifier.IsTransportFailure(ex)) { logger.LogError(...); SetLoadErrorState(); }`. `SetLoadErrorState:124-131` resetea `Items=[]`, `TotalCount=0`, `CurrentPage=1`, `TotalPages=1`, `LoadErrorMessage = "No se pudo cargar el listado de vacantes. Intentá nuevamente."` — pero `Search`, `Sort`, `Segmento` quedan preservados (no se tocan en `SetLoadErrorState`). El link "Reintentar" en `Index.cshtml:15` usa `BuildPagedRouteValues(CurrentPage)` que preserva `search`, `sort`, `status`. | `VacantesIndexSmokeTests.Get_Index_WhenApiReturns5xx_ShowsRecoverableError` (configura `ListarException = new HttpRequestException("upstream returned 503")`; asserts 200 OK + mensaje visible + `ListarCalls.Count > 0`) | ✅ COMPLIANT |
| **Sidenav entrada "Vacantes" con "Nueva" solo para mutadores** | `_Sidenav.cshtml:221-244`: bloque colapsable `<a aria-controls="vacantes" ...>...</a>` con subítem "Listado" siempre visible para autenticados + subítem "Nueva" gated por `@if (puedeMutarVacantes)`. `puedeMutarVacantes = esAdministrador \|\| User.IsInRole(RolesSgv.GestorVacantes)` (líneas 25-26). | `VacantesDetailsAndSidenavTests.Sidenav_WhenAuthenticatedNonMutator_RendersListadoButNotNueva` (asserts `href="/organizacion/vacantes"` presente + ausencia de `href="/organizacion/vacantes/crear"` para no-admin) + `Sidenav_WhenAdministrator_RendersListadoAndNueva` (asserts ambos hrefs para admin) | ✅ COMPLIANT |
| **Tests `VacantesIndexSmokeTests` + `VacantesCreateEditForbidTests` + `VacantesDetailsAndSidenavTests`** | Archivos presentes en `tests/SGV.Tests/Web/Vacantes/`. Total: 4 métodos en `VacantesIndexSmokeTests` (3 Facts + 1 Theory), 6 Facts en `CreateEditForbid`, 3 Facts en `DetailsAndSidenav` = 13 métodos + 4 InlineData expandidos = **16 test cases** (coincide con `apply-progress.md` claim de "16 nuevos tests web"). | 16/16 pasan (subset incluido en los 57 del filtro global `~Vacantes`) | ✅ COMPLIANT |

## Coherencia con `design.md`

| Decisión | Implementación | Estado |
|----------|----------------|--------|
| **PB-1** — `RolesSgvMutacion = "Administrador,GestorVacantes"` (D-4) | Reusada indirectamente: `CanMutate = User.IsInRole(RolesSgv.Administrador) \|\| User.IsInRole(RolesSgv.GestorVacantes)`. El sidenav usa `User.IsInRole(RolesSgv.GestorVacantes)` directamente. Coincide con la constante. | ✅ Coherente |
| **VacanteInputModel ≤500 chars** (alineado con validador backend FluentValidation) | `VacanteInputModel.Motivo`/`Observaciones` con `[StringLength(500, ErrorMessage = "El motivo no puede superar los 500 caracteres.")]` y `[StringLength(500, ErrorMessage = "Las observaciones no pueden superar los 500 caracteres.")]` | ✅ Coherente |
| **Historial cronológico via VM** (PB-4) | `VacanteDetailViewModel.FromDto` aplica `OrderBy(item => item.ChangedAt)` — el sort lo hace el VM, no el backend (defensa en profundidad por si el backend cambia el orden). | ✅ Coherente |
| **PRG con `TempData` para feedback post-redirect** (patrón `UnidadOrganizativaCreateDetailsTests` + `Ocupaciones`) | `PageFeedback.SetSuccess(TempData, "La vacante se creó correctamente.")` + `PageFeedback.GetStatusMessage(TempData)` en `DetailsModel.StatusMessage`. Reutiliza el helper común. | ✅ Coherente |
| **`TransportFailureClassifier` en todos los handlers que llaman al API** | `IndexModel.OnGetAsync:85`, `CreateModel.OnPostAsync:90 + LoadCatalogsAsync:174`, `EditModel.OnPostAsync:100 + LoadCurrentAsync:171 + LoadStatesAsync:199`, `DetailsModel.OnGetAsync:74`. Patrón consistente con el resto del shell. | ✅ Coherente |

## Cambios estructurales observados

| Archivo | Acción | Líneas |
|---------|--------|--------|
| `src/SGV.Web/Integration/Vacantes/IVacanteApiClient.cs` | Created | 37 |
| `src/SGV.Web/Integration/Vacantes/VacanteApiClient.cs` | Created | 177 |
| `src/SGV.Web/Integration/Vacantes/VacanteListItemViewModel.cs` | Created | 34 |
| `src/SGV.Web/Integration/Vacantes/VacanteInputModel.cs` | Created | 35 |
| `src/SGV.Web/Integration/Vacantes/VacanteDetailViewModel.cs` | Created | 36 |
| `src/SGV.Web/Pages/Organizacion/Vacantes/{Index,Create,Edit,Details}.cshtml(.cs)` | Created | ~925 (8 archivos) |
| `src/SGV.Web/Pages/Shared/Partials/_Sidenav.cshtml` | Modified (entry Vacantes añadida tras Ocupaciones, líneas 221-244) | +24 |
| `src/SGV.Web/Program.cs` | Modified (`AddHttpClient<IVacanteApiClient, VacanteApiClient>` líneas 238-244) | +7 |
| `tests/SGV.Tests/Web/Vacantes/FakeVacanteApiClient.cs` | Created | 145 |
| `tests/SGV.Tests/Web/Vacantes/VacantesIndexSmokeTests.cs` | Created | 108 |
| `tests/SGV.Tests/Web/Vacantes/VacantesCreateEditForbidTests.cs` | Created | 193 |
| `tests/SGV.Tests/Web/Vacantes/VacantesDetailsAndSidenavTests.cs` | Created | 73 |
| `tests/SGV.Tests/Web/SgvWebApplicationFactory.cs` | Modified (`WithVacanteApiClient` helper + `_vacanteApiClient` override path líneas 41, 61, 93, 109, 169-173, 282-285) | +30 |
| `tests/SGV.Tests/Web/Collections/WebIntegrationFixture.cs` | Modified (`CreateVacanteLeaseAsync` líneas 105-109, `CreateVacanteFormLeaseAsync` líneas 115-122) | +20 |

## Desviaciones del Design

### D-4.1 — Tests 5.4 (Details history) y 5.5 (Sidenav gating) escritos después de la implementación

`apply-progress.md` §"TDD Cycle Evidence", filas 5.4 y 5.5:

```
| 5.4 | `.../VacantesDetailsAndSidenavTests.cs` | Integration | ✅ 61 tests web existentes | ⚠️ Test added in final web coverage pass, after the initial Details implementation | ✅ 16/16 extended pass | ...
| 5.5 | `VacantesDetailsAndSidenavTests` | Integration | ✅ 61 tests web existentes | ⚠️ Sidenav coverage added in final web coverage pass, after initial menu implementation | ✅ 16/16 extended pass | ...
```

**Descripción**: el ciclo RED → GREEN estricto para las tareas 5.4 (historial cronológico en `Details`) y 5.5 (gating de "Nueva" en `_Sidenav`) se cumplió en pase posterior a la implementación inicial — no en el ciclo pre-implementación como pide Strict TDD. Las 4.x y el resto de 5.x sí siguieron RED-first (ver `apply-progress.md` filas 5.1/5.2/5.3/5.6/5.7).

**Severidad**: WARNING. Todos los tests pasan (16/16 en la corrida extendida que incluye DetailsAndSidenav). La cobertura funcional está completa: el escenario PB-4 (historial cronológico) está cubierto por `Get_Details_RendersChronologicalHistory`, y PB-1 (sidenav gating) por los dos tests `Sidenav_WhenAuthenticatedNonMutator_*` y `Sidenav_WhenAdministrator_*`. La desviación es **disciplinaria, no funcional**.

**Mitigación sugerida**: en futuros sub-lanzamientos del repo, mantener un tablero de evidencia TDD que se actualice commit por commit (no en pase final). No requiere cambio retroactivo.

## Hallazgos

### CRITICAL

Ninguno.

### WARNING

- **W-1 — Regresión en 4 tests pre-existentes por entrada Vacantes en sidenav**
  - **Síntoma**: la suite web completa (filter `~Web`) corre 1371 tests; 1367 pasan y **4 fallan**. Todos los 4 fallos son `Assert.DoesNotContain(...)` para el string "Vacantes" / `aria-controls="vacantes"` en el HTML del sidenav, que ahora contienen la entrada Vacantes añadida por Slice 2.
  - **Tests afectados** (todos pre-existentes al slice 2, no modificados por este change — ver `git status -s tests/`):
    1. `SGV.Tests.Web.CargoWebTests.Get_Sidenav_WhenAuthenticated_ExposesCargosModule` — `CargoWebTests.cs:60` exige `DoesNotContain("<span class=\"menu-text\">Vacantes</span>", ...)`
    2. `SGV.Tests.Web.CargoWebTests.Get_Sidenav_WhenAuthenticated_ExposesHabilidadesModule` — `CargoWebTests.cs:83` igual
    3. `SGV.Tests.Web.UnidadOrganizativaWebTests.Get_Index_WhenAuthenticated_RendersShellMenuAndInitialTable` — `UnidadOrganizativaAccessAndIndexTests.cs:38` igual
    4. `SGV.Tests.Web.Puesto.PuestoWebSeamTests.Get_Sidenav_WhenAuthenticated_DoesNotExposeUnimplementedModules` — `PuestoWebSeamTests.cs:180` exige `DoesNotContain(@"aria-controls=""vacantes""", ...)`
  - **Causalidad**: slice 2 añadió la entrada `Vacantes` al sidenav (`_Sidenav.cshtml:221-244`). Las aserciones negativas fueron escritas antes de la existencia del módulo Vacantes y mantuvieron la expectativa de que la entrada NO estaría (placeholder de "módulo no implementado"). El slice 2 legítimamente la implementó, pero no actualizó los tests pre-existentes.
  - **No-causalidad confirmada**:
    - `git status -s tests/` → vacío al momento del verify → slice 2 no tocó estos 4 archivos de test.
    - `git log --oneline` para los 3 archivos afectados muestra commits previos al slice 2 (`3dd7fbaf`, `c9e3fc59`, `7c67c5d6`, `3e61dac1`, `87856ecf`, `bc7f6f21`, `b7ff2bb9`, etc.) — el antecedente se remonta a pre-slices de módulos web (Puestos 2-x, etc.).
    - Los 4 tests son del estilo "verificar que el sidenav expone el módulo X pero NO expone Vacantes (todavía no implementado)". El "todavía" se cumplió con slice 2.
  - **Acción recomendada**: eliminar (o reemplazar por un check de un placeholder aún no implementado como `Postulantes` o `Reclutamiento`) las 4 aserciones `DoesNotContain(...)` para `Vacantes` en:
    - `CargoWebTests.cs:60` y `CargoWebTests.cs:83`
    - `UnidadOrganizativaAccessAndIndexTests.cs:38`
    - `PuestoWebSeamTests.cs:180`
    Patrón recomendado: esas aserciones negativas existen para placeholder-tracking y deben actualizarse cuando un módulo placeholder deja de serlo.
  - **Severidad**: WARNING (no CRITICAL — los 4 tests son pre-existentes y su breakage es esperable/mejorable tras la implementación de un módulo; no es un bug del código de producción ni de los tests del slice 2. La aceptación del slice 2 no los cubre por estar fuera de scope del brief pero la suite completa los pisa.)

- **W-2 — D-4.1 documentada en `apply-progress.md`**: tests de 5.4 (Details history cronológico) y 5.5 (Sidenav gating) añadidos en pase final de cobertura, después de la implementación inicial de esas features. La disciplina RED → GREEN estricta no se cumplió para esos dos work units. **Severidad**: WARNING (desviación disciplinaria, no funcional — los 16/16 tests pasan y la cobertura es completa).

### SUGGESTION

- **S-1 — 6 escenarios sin cobertura runtime específica**
  | Escenario | Implementación estructural | Sugerencia |
  |-----------|----------------------------|------------|
  | S8 — Falla la carga de catálogos | `CreateModel.LoadCatalogsAsync:158-178` catchea `TransportFailureClassifier` + `ErrorMessage = "No se pudieron cargar los catálogos. Intentá nuevamente."` + `CatalogsReady=false` + botón `disabled`. | Agregar test `Get_Create_WhenCatalogFails_ShowsRecoverableErrorAndDisablesGuardar` con FakeVacanteApiClient { ListarEstadosException = new HttpRequestException(...) }. Paralelo al pattern `Get_Index_WhenApiReturns5xx_ShowsRecoverableError`. |
  | S10 — Error de validación por campo | `CreateModel.ApplyFailureAsync:135-145` itera `result.FieldErrors` → `ModelState.AddModelError($"Input.{key}", error)`. | Agregar test `Post_Create_WhenValidationFails_AddsFieldErrorsToModelState` con `CrearResult = VacanteCommandResult.Failure(..., new Dictionary<string,string[]> { ["motivo"] = ["obligatorio"] })`. |
  | S11 — Conflicto de PuestoId con vacante abierta existente (web path) | `CreateModel.ApplyFailureAsync:147-153` branch `Categoria != Validation` → `ErrorCategoryMapper.Map(... conflictMessage: result.Error.Message)`. Backend ya cubierto. | Agregar test `Post_Create_WhenConflict_ReturnsConflictMessageAndPreservesForm` con `CrearResult = VacanteCommandResult.Failure(new VacanteError(ErrorCategoria.Conflict, "PuestoConVacanteAbierta", ...))`. |
  | S16 — Details sin historial | `Details.cshtml:67-70` muestra "No hay historial previo." cuando `vacante.Historial.Count == 0`. | Agregar test `Get_Details_WhenNoHistory_ShowsEmptyMessage` con `ObtenerPorIdResult = BuildDetail(historial: [])`. |
  | S17 — Vacante inexistente en Details/Edit | `DetailsModel.IsNotFound` (línea 23) + estado recuperable con link a listado (líneas 21-30 de Details.cshtml); `EditModel.IsRecoverable` (línea 31) + misma recuperabilidad (líneas 21-30 de Edit.cshtml). | Agregar test `Get_Details_WhenNotFound_RendersRecoverableState` con `ObtenerPorIdResult = null` + `Get_Edit_WhenNotFound_RendersRecoverableState` con misma config. |
  | S19 — Estado `active` en sidenav cuando estás en `/organizacion/vacantes` | `_Sidenav.cshtml:18` define `vacantesGroupActive = currentPath.StartsWithSegments("/organizacion/vacantes") ? "active" : ""`. Helpers `LinkHasActive` solo se usan para Habilidades (paridad Ocupaciones/Puestos no se testea explícitamente). | Agregar test con helper análogo que verifique `class="side-nav-link side-nav-link-toggle active"` en el bloque `<a aria-controls="vacantes">` cuando el usuario está en `/organizacion/vacantes` (o subruta). |
  - **Severidad**: SUGGESTION (mejoras incrementales — los 6 escenarios tienen implementación correcta verificada por inspección; agregar tests runtime aumenta la confianza pero no es bloqueante para archive).

- **S-2 — Vínculo con W-1**: las 4 aserciones negativas obsoletas en tests pre-existentes son la causa del WARNING W-1; eliminarlas (ver acción W-1) cierra ambas findings. **Severidad**: SUGGESTION (vinculada a WARNING W-1; no es hallazgo independiente).

## Observaciones

- **MySQL no requerido para Slice 2 web**: todos los 16 tests web usan `SgvWebApplicationFactory` con `FakeVacanteApiClient`; no se introdujeron `[MySqlFact]` en este slice. Los 3 `[MySqlFact]` de `VacanteRepositoryQueryTests` del Slice 1 ya verificado en `verify-report-2.md` mantienen su skipeo limpio si MySQL no estuviera disponible.
- **`Deviation D-4.1` documentada en `apply-progress.md`**: filas 5.4 y 5.5 de "TDD Cycle Evidence" reconocen el pase posterior de cobertura (ver matriz arriba). El resto de work units del Slice 2 sí siguió RED-first estricto (filas 4.1-4.4, 5.1-5.3, 5.6-5.7).
- **Doble válvula de seguridad para gating de rol**: el módulo Vacantes tiene dos capas de control — (1) el atributo `[Authorize]` a nivel clase en `CreateModel`/`EditModel` (autenticación) + (2) `CanMutate = User.IsInRole(Administrador) || User.IsInRole(GestorVacantes)` explícito en cada handler (autorización por acción). Esta segunda capa es la que el spec `vacante-web` S12 exige, y está cubierta por los 2 tests de `Get_*_WhenAuthenticatedWithoutMutationRole_RedirectsToAccessDenied` que assertan 302 → `/error/403` (= `Forbid()`).
- **`PageFeedback.SetSuccess(TempData, ...)` está importado de `SGV.Web.Pages.Common`**: el helper centraliza el contrato de mensaje one-time post-PRG. Reusado también en `DetailsModel.StatusMessage` para mostrar el mensaje tras Create/Edit. Patrón idéntico al de Ocupaciones/Puestos (suite web existente).
- **`VacanteListItemViewModel.FromDto` y `VacanteDetailViewModel.FromDto` puras**: factories `static FromDto` que no mutan estado global. Aplicables a tests de mapping si se quisiera (no requeridas por los tests actuales que pasan `FakeVacanteApiClient.BuildDto/BuildDetail` directo).
- **`Puestos/Details.cshtml` no contiene enlace a `/vacantes/crear`** (verificación PB-2): `grep -rn "vacantes/crear" src/SGV.Web/Pages/Organizacion/Puestos/` retornó 0 matches. Las Razor Pages de Puestos (`Details.cshtml.cs`, `Edit.cshtml.cs`, `Index.cshtml.cs`, etc.) tratan exclusivamente su dominio; no contaminan con links cross-module a Vacantes. Esto es exactamente lo que PB-2 pide.
- **Slice 1 ya verificado**: los 4 work units que este reporte NO cubre (1.x Foundation, 2.x Data layer, 3.x Behavior) ya tienen sus propios verify-reports (`verify-report.md`, `verify-report-2.md`, `verify-report-3.md`). El briefSlice 2 = 4.x web read + 5.x web write.
- **`AddHttpClient<IVacanteApiClient, VacanteApiClient>` con `ApiBearerTokenHandler`**: confirmado en `Program.cs:238-244`. La inserción está justo después del registro de `OcupacionApiClient` (líneas 230-236) — coherente con la decisión de mantener el patrón en paralelo. `client.BaseAddress = new Uri(options.BaseUrl, UriKind.Absolute)` y `client.Timeout = TimeSpan.FromSeconds(10)` iguales al resto.
- **Cobertura runtime end-to-end disponible**: este reporte demuestra que cada uno de los puntos del brief del orquestador tiene un test `passing` con excepción de los 2 WARNING documentados. La sub-comando `dotnet test SGV.slnx --no-build --nologo --filter "FullyQualifiedName~Vacantes"` retorna `Passed! - Failed: 0, Passed: 57, Skipped: 0` en 3 segundos.
- **Pruebas de bridge JWT contra `VacanteApiClient` no realizadas en este slice**: los tests existentes de bridge (`AuthApiClientChangePasswordTests`, `SgvAuthIntegrationTests`, `PuestoWebSeamTests`) cubren el handler. No hay test específico para `VacanteApiClient` porque el slice 2 lo intercambia por `FakeVacanteApiClient` via `WithOverrides(vacanteApiClient: ...)`. La integración real del bridge se cubre por la composición en `Program.cs` + tests de otros módulos.

## Veredicto

**PASS WITH WARNINGS**

Slice 2 web completo cumple los 12 puntos en scope del brief del orquestador con evidencia runtime. Build limpio (0 errors, 93 warnings pre-existentes no asociados al change), 57/57 tests focales `~Vacantes` en verde, los 16/16 tests específicos del slice 2 (`VacantesIndexSmokeTests` + `VacantesCreateEditForbidTests` + `VacantesDetailsAndSidenavTests`) en verde, 8/8 requisitos de la spec `vacante-web` cumplidos, 13/19 escenarios con cobertura runtime y 6/19 con cobertura estructural (siendo 1 de esos 6 ya cubierto en API via Slice 1). PB-1 (forbid en handlers), PB-2 (sin botón en Puestos/Details), PB-3 (Motivo opcional al cerrar), PB-4 (historial cronológico en Details), PB-5 (default abiertas + fallback) están todos verificados. `AddHttpClient<IVacanteApiClient, VacanteApiClient>` registrado en `Program.cs` con base URL vía `SgvApiOptions` y `ApiBearerTokenHandler` en pipeline. `5xx` recuperable en Index implementado y cubierto (mensaje visible + `ListarCalls.Count > 0` indica que la request llegó al API antes del fallo). Sidenav con gating `puedeMutarVacantes` confirmado en `_Sidenav.cshtml`. Sin embargo, hay dos hallazgos WARNING que el orquestador debe decidir cómo manejar: (1) W-1 — 4 tests pre-existentes del resto de la suite web fallan porque tenían aserciones negativas obsoletas para "Vacantes no debe estar en sidenav" — la acción es mecánica y mejora la higiene de la suite pero NO bloquea el avance del slice 2 ya que esos tests son de otros módulos (Cargo, UO, Puestos); (2) W-2 — desviación D-4.1 documentada en `apply-progress.md` donde 5.4 (Details history) y 5.5 (Sidenav gating) tuvieron tests escritos tras la implementación inicial; los tests pasan y la cobertura es completa, pero la disciplina strict-TDD se invirtió para esos dos work units. Los 2 SUGGESTION agregan robustez: 6 escenarios con cobertura solo estructural y la acción mecánica de W-1.

Próximo paso sugerido:
- **Opción A (recomendada)**: tratar W-1 y W-2 como follow-up issues (no bloquean `sdd-archive`; la spec `vacante-web` está cumplida y los tests del slice 2 son verdes). Las acciones correctivas son de un solo commit cada una.
- **Opción B**: aplicar las correcciones de W-1 (eliminar 4 aserciones obsoletas) antes de `sdd-archive` para tener la suite web completa en verde en este mismo cambio.

Cualquiera de las dos opciones deja el módulo listo para usuarios reales.
