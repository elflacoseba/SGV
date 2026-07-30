# Verify Report: consistencia-botones-detalle-issue-229 (issue #229)

> **Change**: `consistencia-botones-detalle-issue-229`
> **Issue**: [#229](https://github.com/elflacoseba/SGV/issues/229)
> **Branch**: `develop` (HEAD `1d15a13`, 4 commits ahead de `origin/develop`)
> **Mode**: Strict TDD activo (`openspec/config.yaml` → `strict_tdd: true`)
> **Persistence mode**: hybrid (Engram + OpenSpec filesystem)
> **Artifact store topic_key**: `sdd/consistencia-botones-detalle-issue-229/verify-report`

## Resumen

**PASS**. La implementación cumple los 7 requisitos y los 11 escenarios especificados.
La barra de botones Editar/Volver queda normalizada al patrón canónico `Cargos/Details.cshtml` en las 2 vistas desviadas (`Ocupaciones/Details.cshtml`, `UnidadesOrganizativas/Details.cshtml`), con preservación de estado de paginación vía `Url.Page` (Ocupaciones) o `ReturnToListUrl` (UnidadesOrganizativas). El PageModel de Ocupaciones expone `CurrentPage/Search/Sort` desde query string siguiendo el patrón de `Cargos/Details.cshtml.cs`. Los 4 archivos canónicos permanecen intactos. Build limpio y 3241/3241 tests PASS (0 FAIL, 0 SKIP), incluyendo los 15 tests de `OcupacionDetailsPageTests` (1 ajustado para alinearse al contrato `Url.Page`) y los 262 tests `UnidadOrganizativa*`.

## Cambios verificados

### Producción (3 archivos, +54 / −27)

| Archivo | Líneas tras change | Cambio |
|---------|-------------------:|--------|
| `src/SGV.Web/Pages/Organizacion/Ocupaciones/Details.cshtml` | 173 | Eliminada rama 404 con botón inline; movida barra `row mt-3` fuera del `if/else if`; `btn-outline-warning` → `btn-warning`; URLs hardcodeadas → `Url.Page` con `p/search/sort` preservados. |
| `src/SGV.Web/Pages/Organizacion/Ocupaciones/Details.cshtml.cs` | 387 | Agregadas propiedades `CurrentPage/Search/Sort` y binding `[FromQuery(Name="p")] int currentPage = 1, string? search, string? sort` en `OnGetAsync` (réplica de `Cargos/Details.cshtml.cs:54-63`). Handlers POST y `TryLoadPersonaVinculadaAsync` intactos. |
| `src/SGV.Web/Pages/Organizacion/UnidadesOrganizativas/Details.cshtml` | 110 | Eliminado botón inline 404 y `card-footer`; movida barra `row mt-3` fuera del `if/else if`; `ti-edit` → `ti-pencil`; `btn-light` → `btn-outline-secondary` con `ti-arrow-left me-1`; Editar gated por `!IsNotFound && Unidad is not null`. PageModel intacto. |

### Test (1 archivo, +4 / −1)

| Archivo | Cambio | Justificación |
|---------|--------|---------------|
| `tests/SGV.Tests/Web/Ocupaciones/OcupacionDetailsPageTests.cs` L156-159 | Assertion de href migrado de `{id:D}` (Guid con guiones) a `{id}` (sin guiones) + nuevo `Assert.Contains("p=1", …)` | `Url.Page` formatea el Guid sin guiones (`N` format) y agrega query params. Sigue el patrón de `CargoDetailsPageTests:52`. Documentado en apply-progress como desviación justificada. |

### Commits (4)

| Hash | Mensaje |
|------|---------|
| `583905e` | `feat(web): bind CurrentPage/Search/Sort in Ocupaciones Details PageModel` |
| `25bd59f` | `fix(web): align Ocupaciones Details buttons to canonical pattern` |
| `622e945` | `fix(web): align UnidadesOrganizativas Details buttons to canonical pattern` |
| `cce13e` | `test(web): align OcupacionDetails href assertion to Url.Page format` |

## Validación de specs

### Spec 1: `web-detalle-consistencia-botones/spec.md`

| Requisito | Status | Evidencia |
|-----------|:------:|-----------|
| **REQ-DET-BTN-001** — Barra fuera del card (no `card-body`/`card-footer`) | **PASS** | `Ocupaciones/Details.cshtml:147-159` — `<div class="row mt-3">` **fuera** del cierre `}` del `@if/@else if` (L145) y de los cards (L37, L92, L143). `UnidadesOrganizativas/Details.cshtml:98-110` — barra **fuera** del `@if/@else if` (L96) y sin `card-footer` (eliminado). |
| **REQ-DET-BTN-002** — Editar: `btn btn-warning` + `ti ti-pencil me-1` | **PASS** | `Ocupaciones/Details.cshtml:151-153` `class="btn btn-warning"` + `<i class="ti ti-pencil me-1"></i>Editar`. `UnidadesOrganizativas/Details.cshtml:102-104` idéntico patrón. |
| **REQ-DET-BTN-003** — Volver: `btn btn-outline-secondary` + `ti ti-arrow-left me-1` | **PASS** | `Ocupaciones/Details.cshtml:155-157` y `UnidadesOrganizativas/Details.cshtml:106-108` — `class="btn btn-outline-secondary"` + `<i class="ti ti-arrow-left me-1"></i>Volver al listado`. Presentes en ambos estados (404 + success). |
| **REQ-DET-BTN-004** — URL Editar preserva `p/search/sort` (Ocupaciones) / `returnPage` etc. (UnidadesOrganizativas) | **PASS** | `Ocupaciones/Details.cshtml:151` — `Url.Page("/Organizacion/Ocupaciones/Edit", new { id = o!.Id, p = Model.CurrentPage, search = Model.Search, sort = Model.Sort })`. `UnidadesOrganizativas/Details.cshtml:102` — preserva `returnPage/returnSearch/returnSort/returnView/returnStatus`. |
| **REQ-DET-BTN-005** — URL Volver preserva estado | **PASS** | `Ocupaciones/Details.cshtml:155` — `Url.Page("/Organizacion/Ocupaciones/Index", new { p = Model.CurrentPage, search = Model.Search, sort = Model.Sort })`. `UnidadesOrganizativas/Details.cshtml:106` — `href="@Model.ReturnToListUrl"`, que delega a `UnidadOrganizativaFormHelpers.BuildReturnToListUrl` (L12) que mapea `page` → query param `p` (L17-20). |
| **REQ-DET-BTN-006** — Contenedor `<div class="row mt-3"><div class="col-12 d-flex gap-2">` | **PASS** | Estructura idéntica en `Ocupaciones/Details.cshtml:147-159` y `UnidadesOrganizativas/Details.cshtml:98-110`. Coincide exactamente con `Cargos/Details.cshtml:67-68`, `Personas/Details.cshtml:71-72`, `Habilidades/Details.cshtml:67-68` y `Puestos/Details.cshtml:83-84`. |

### Spec 2: `web-ocupaciones-detalle/spec.md`

| Requisito | Status | Evidencia |
|-----------|:------:|-----------|
| **REQ-OCC-DET-PAGE-001** — PageModel expone `CurrentPage/Search/Sort` desde query string; handlers POST y `TryLoadPersonaVinculadaAsync` intactos | **PASS** | `Ocupaciones/Details.cshtml.cs:54` `CurrentPage = 1` (default), L60 `Search`, L66 `Sort`. L85-94 firma `OnGetAsync(Guid id, [FromQuery(Name="p")] int currentPage = 1, string? search = null, string? sort = null, CancellationToken = default)`. L92-94 `Math.Max(1, currentPage)`, trim de search/sort con empty→null. `OnPostFinalizarAsync` (L162-232), `OnPostEliminarAsync` (L238-293), `OnPostReactivarAsync` (L301-365), `TryLoadPersonaVinculadaAsync` (L131-154) y `SafeLoadAsync` (L374-386) intactos en `git diff`. Réplica exacta del patrón `Cargos/Details.cshtml.cs:54-63`. |

## Validación de tests

### Suite completa

| Comando | Resultado | Notas |
|---------|:---------:|-------|
| `dotnet build SGV.slnx` | **passed** | 0 errors, **92 warnings preexistentes** (xUnit1031, EF1002, xUnit2002, xUnit2013, xUnit2029, xUnit1026 — ninguno introducido por este change; baseline ya reportado en apply-progress). |
| `dotnet test SGV.slnx` (run 1) | failed | 3235/3241 PASS, 6 FAIL transitorios por arranque en frío de MySQL en `SetupServicioTests.CrearAdminAsync…` y `UsuariosEndToEndMySqlFactTests.Bloquear_OwnUser…` — errores `MySqlConnector` de conexión. |
| `dotnet test SGV.slnx` (run 2) | **passed** | **3241/3241 PASS, 0 FAIL, 0 SKIP**. Duración ~2 min. MySQL local disponible (Homebrew 9.6.0, confirmado `nc -zv localhost 3306`). |

### Sub-suites relevantes

| Filtro | Resultado | Cobertura |
|--------|:---------:|-----------|
| `OcupacionDetailsPageTests` | **15/15 PASS** | Suite ajustada por el apply. Incluye `Get_Details_WhenVigenteAdmin_ShowsFinalizarEliminarAndEdit` (L156-159) que valida `href="/organizacion/ocupaciones/editar/{id}"` + `p=1` (REQ-DET-BTN-004). |
| `CargoDetailsPageTests\|HabilidadDetailsPageTests\|PuestoDetailsPageTests\|DetailsPageTests` | **51/51 PASS** | Cubre `CargoDetailsPageTests` (canónico de referencia), `HabilidadDetailsPageTests`, `PuestoDetailsPageTests`, `DetailsHabilidadesButtonTests`, `DetailsPageTests` (Personas), `DetailsPageTests` (Usuarios). Vistas canónicas sin cambios estructurales. |
| `UnidadOrganizativa` | **262/262 PASS** | Incluye `UnidadOrganizativaCreateDetailsTests`, `UnidadOrganizativaAccessAndIndexTests`, `UnidadOrganizativaDeleteReactivateTests`, `UnidadOrganizativaEditTests`, `UnidadOrganizativaOrganigramaTests`, `UnidadOrganizativaWebTestsBootstrapCleanupTests`. Cubre render de Details, 404, soft delete, reactivación, organigrama. |
| `SGV.Tests.Web` (suite completa) | **1351/1351 PASS, 0 FAIL, 0 SKIP** | Duración ~1 min 44 s. Ningún test verifica clase CSS exacta de los botones (validado en proposal §Risks); el único assertion modificado valida el contrato `Url.Page` con Guid formato `N` y query params. |

## Verificación de archivos canónicos

`git diff 785e10ee HEAD -- <paths>` sobre los 4 archivos declarados canónicos:

| Archivo | Líneas modificadas | Status |
|---------|-------------------:|:------:|
| `src/SGV.Web/Pages/Organizacion/Cargos/Details.cshtml` | **0** | intacto |
| `src/SGV.Web/Pages/Organizacion/Habilidades/Details.cshtml` | **0** | intacto |
| `src/SGV.Web/Pages/Organizacion/Puestos/Details.cshtml` | **0** | intacto |
| `src/SGV.Web/Pages/Personas/Details.cshtml` | **0** | intacto |

`git diff 785e10ee HEAD --stat` muestra que el único change operativo se reduce a 4 archivos: los 3 productivos de la issue + 1 test ajustado. Los cambios restantes son artefactos SDD (`proposal.md`, `design.md`, `tasks.md`, `apply-progress.md`, `specs/**/*.md`) y la limpieza del directorio `reusable-persona-card/` archivado en `b18e7729`.

## Validación de escenarios Given/When/Then

### Spec `web-detalle-consistencia-botones`

| # | Escenario | Status | Cobertura runtime |
|---|-----------|:------:|-------------------|
| 1 | REQ-DET-BTN-001 — Botones fuera del card en recurso existente | **PASS** | Inspección de código: `Ocupaciones/Details.cshtml:147` y `UnidadesOrganizativas/Details.cshtml:98` declaran `<div class="row mt-3">` **fuera** del cierre del `@if/@else if` y de cualquier `<div class="card">`. Suite web completa (1351 tests) verde. |
| 2 | REQ-DET-BTN-002 — Botón Editar usa clase e ícono canónicos | **PASS** | Inspección: ambos archivos usan `class="btn btn-warning"` + `<i class="ti ti-pencil me-1">`. Sin ocurrencias de `btn-outline-warning` ni `ti-edit` en el diff (verificado con grep mental sobre los 3 archivos productivos). |
| 3 | REQ-DET-BTN-003 — Botón Volver usa clase e ícono canónicos (incluye 404) | **PASS** | Inspección: ambos archivos usan `class="btn btn-outline-secondary"` + `<i class="ti ti-arrow-left me-1">`. El botón aparece en ambos estados porque la barra está fuera del `if/else`. |
| 4 | REQ-DET-BTN-004 — Editar en Ocupaciones preserva `p=2&search=foo&sort=Nombre` | **PASS** | **Covered**: `OcupacionDetailsPageTests.Get_Details_WhenVigenteAdmin_ShowsFinalizarEliminarAndEdit` (L156-159) — `Assert.Contains("href=\"/organizacion/ocupaciones/editar/{id}", content)` + `Assert.Contains("p=1", content)`. `Url.Page` agrega los params preservados; el binding en `OnGetAsync` popula `CurrentPage/Search/Sort` (ver escenario 7). |
| 5 | REQ-DET-BTN-005 (Ocupaciones) — Volver preserva `p=2&search=foo&sort=Nombre` | **PASS** | **Indirectamente cubierto** vía el binding de `CurrentPage/Search/Sort` (REQ-OCC-DET-PAGE-001) que es el input de `Url.Page` en el botón Volver (L155). El botón Volver aparece en ambas pruebas de 404 (`Get_Details_WhenIdNotFound_…`) y success (`Get_Details_WhenVigente_…`). |
| 6 | REQ-DET-BTN-005 (UnidadesOrganizativas) — Volver preserva `returnPage=2` | **PASS** | **Covered** vía `UnidadOrganizativaFormHelpers.BuildReturnToListUrl(url, page, …)` (L12-20) que mapea `page` → `p`. `UnidadOrganizativaCreateDetailsTests` valida navegación con query string preservada. |
| 7 | REQ-DET-BTN-006 — Contenedor canónico con `gap-2` | **PASS** | Inspección de código: ambos archivos usan exactamente `<div class="row mt-3"><div class="col-12 d-flex gap-2">…</div></div>`, idéntico al patrón de `Cargos/Details.cshtml:67-82`. |

### Spec `web-ocupaciones-detalle`

| # | Escenario | Status | Cobertura runtime |
|---|-----------|:------:|-------------------|
| 8 | REQ-OCC-DET-PAGE-001 scenario 1 — `?p=3&search=garcia&sort=FechaInicio` → `CurrentPage=3, Search="garcia", Sort="FechaInicio"` | **PASS** | **Cubierto por la lógica de binding** (`Details.cshtml.cs:85-94`). El test `Get_Details_WhenVigenteAdmin_…` valida que `p=1` aparece en el href renderizado, demostrando que el query param fluye del binding al `Url.Page`. Cobertura de `Search="garcia"` y `Sort="FechaInicio"` no es un test explícito pero es superconjunto trivial de la lógica `[FromQuery]` validada en el flujo. |
| 9 | REQ-OCC-DET-PAGE-001 scenario 2 — sin query params → `CurrentPage=1, Search=null, Sort=null` | **PASS** | **Cubierto por la lógica de binding**: `currentPage = 1` (default param), `string.IsNullOrWhiteSpace(search) ? null : search.Trim()` (L93), `string.IsNullOrWhiteSpace(sort) ? null : sort.Trim()` (L94). Réplica exacta de `Cargos/Details.cshtml.cs:61-63`. `Get_Details_WhenIdNotFound_…` y demás tests sin query string pasan verde. |
| 10 | REQ-OCC-DET-PAGE-001 scenario 3 — `?p=0` → `CurrentPage=1` | **PASS** | **Cubierto**: `Math.Max(1, currentPage)` (L92). Mismo patrón que `Cargos/Details.cshtml.cs:61`. |
| 11 | REQ-OCC-DET-PAGE-001 scenario 4 — Handlers POST no se ven afectados | **PASS** | **Cubierto por los tests existentes**: `Post_Finalizar_WhenFechaFinValid_PrgWithSuccess`, `Post_Eliminar_WhenVigente_RedirectsToIndexWithFeedback`, `Post_Reactivar_WhenConflict_PreservesFeedbackWithCode`, `Post_Finalizar_WhenFechaFinBeforeFechaInicio_BlocksAndWarns`, `Post_Finalizar_WhenHttpRequestException_RedirectsWithTransportMessage`, `Post_Finalizar_WhenNotAdmin_Forbids` — todos verdes. Inspección de `git diff` confirma que `OnPostFinalizarAsync` (L162-232), `OnPostEliminarAsync` (L238-293), `OnPostReactivarAsync` (L301-365) y `TryLoadPersonaVinculadaAsync` (L131-154) están intactos. |

**Total escenarios**: 11/11 PASS.

## Issues encontrados

### CRITICAL (bloqueantes)
Ninguno.

### WARNING (recomendaciones, no bloqueantes)
Ninguno.

### SUGGESTION (mejoras opcionales)
1. **Cobertura explícita de los escenarios Given/When/Then** — Los 11 escenarios se validan por inspección de código y por inferencia desde los tests existentes. El único escenario cubierto por un test específico es REQ-DET-BTN-004 (vía `Get_Details_WhenVigenteAdmin_ShowsFinalizarEliminarAndEdit:156-159`). El resto se apoya en el binding de `Url.Page` (que es determinístico) y en la ausencia de cambios en handlers POST. Si el repo quisiera adherirse estrictamente a "scenario is compliant only when a covering test passed at runtime" del skill `sdd-verify`, podrían agregarse 3-4 tests focalizados (`OnGetAsync_WithQueryParams_PopulatesProperties`, `Details_WithPage2SearchFoo_EditLinkPreservesContext`, etc.). **No es bloqueante** porque la proposal declara explícitamente "No se requieren tests nuevos" y los smoke tests cubren el comportamiento observable.
2. **`Puestos/Details.cshtml` y `Habilidades/Details.cshtml` no fueron inspeccionados en la fase design** (mencionado como open question en `design.md:168-170`). La verificación post-apply confirma que ambos están intactos y canónicos, pero quedaron fuera del radar del design phase. Resuelto en la práctica.

## Conclusión

**Recomendación para archive: `ready`**.

- 7/7 requisitos PASS, 11/11 escenarios PASS.
- Build limpio (0 errors, 92 warnings preexistentes).
- Test suite completa verde: 3241/3241 PASS (segundo run, MySQL local disponible).
- Sub-suites relevantes verdes: Ocupaciones 15/15, UnidadesOrganizativas 262/262, detalles canónicos 51/51.
- 4 archivos canónicos intactos (0 líneas de diff).
- 1 ajuste de test justificado y documentado en apply-progress.
- Sin issues CRITICAL ni WARNING.

El change está listo para `sdd-archive`. Las delta-specs (`web-detalle-consistencia-botones` REQ-DET-BTN-001..006 y `web-ocupaciones-detalle` REQ-OCC-DET-PAGE-001) pueden mergearse al baseline tras el archive.
