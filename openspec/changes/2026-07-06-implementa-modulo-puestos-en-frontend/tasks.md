# Tasks: Implementar el módulo de Puestos en el Frontend

## Review Workload Forecast

| Campo | Valor |
|---|---|
| Líneas re-validadas | PR 1 ~770 · PR 2 ~1200 · PR 3 ~2500 · **Total ~4470** |
| 400-line risk | High (los 3 PRs lo exceden) |
| Chained PRs | Yes |
| Split recomendado | PR 1 → PR 2 → **PR 3A Create → PR 3B Edit → PR 3C Details** (5 PRs) o 3 PRs con `size:exception` |

Decision needed before apply: Yes
Chained PRs recommended: Yes
Chain strategy: pending
400-line budget risk: High

> **⚠ NIT 1 (WARNING absorbido)**: el forecast del design §10 (~890) está 5× debajo de la realidad. `git diff --stat` de Cargos archivado: PR 1=775, PR 2=1216, PR 3=383, PR 2A Create=1220, PR 2B Edit=901. La línea 643 del design suma 39+144+64+201+82+93+40=663 (no 180) y omite tests. Puestos PR 3 agrupa Create+Edit+Details (~2500, 6× sobre budget). **Recomendación obligatoria**: dividir PR 3 en 3A/3B/3C, o aceptar `size:exception`.

## 1. Resumen

22 tareas (PR 1: 7 · PR 2: 3 · PR 3A: 3 · PR 3B: 2 · PR 3C: 2 · soporte: 5). Conventional commits, sin `Co-Authored-By`. `delivery_strategy: ask-always`; orquestador confirma antes de `sdd-apply`.

## 2. Definiciones de tipos (NIT 2)

```csharp
// PuestoListItemViewModel.cs (design §3.2)
public sealed record PuestoListItemViewModel(
    Guid Id, string Codigo, string Nombre, string? Descripcion,
    string UnidadOrganizativaNombre, string CargoNombre, Guid? PuestoSuperiorId)
{ public string CodigoYNombre => $"{Codigo} — {Nombre}"; }

public sealed record PuestoDeleteResult(
    bool Succeeded, HttpStatusCode? StatusCode, string? Code, string? Message);

// PuestoListQuery.cs — USADO por IndexModel (filtros en memoria)
public sealed record PuestoListQuery(string? Search, string? Sort, string? Status, int Page)
{
    public const string SegmentoActivas = "activas";
    public const string SegmentoEliminadas = "eliminadas";
    public static PuestoListQuery Empty { get; } = new(null, null, SegmentoActivas, 1);
}
```

`PuestoInputModel`, `PuestoFormKeys`, `IPuestoForm`, `PuestoFormHelpers` verbatim en `design.md §3.3-3.4` (NIT 3: detalle absorbido del design).

## 3. PR 1 — Seams + shell + sidenav (~770)

- [x] **1.1** RED `PuestoWebSeamTests` (3 casos: sidenav `>Puestos<`+`ti ti-hierarchy`, `active` en `/organizacion/puestos(/...)`, sin placeholders).
- [x] **1.2** RED `PuestosApiClientTests` ≥10 casos con `HttpClient` mockeado: 200/404/204/400/409 + `JsonException` + Theory `TransportFails_PropagatesNativeException` + Fact `CancellationAlreadyRequested_ThrowsAndDoesNotSendRequest`.
- [x] **1.3** RED `IPuestosApiClientContractTests` (6 métodos via reflection).
- [x] **1.4** GREEN `IPuestosApiClient.cs`, `PuestosApiClient.cs` (+ `ToCommandResultAsync`), `PuestoListItemViewModel.cs`, `PuestoListQuery.cs`, `PuestoDeleteResult`.
- [x] **1.5** GREEN registrar `IPuestosApiClient` (`Timeout=10s`+`ApiBearerTokenHandler`) en `Program.cs`.
- [x] **1.6** GREEN `PuestoWebTestFixture.cs`, `FakePuestosApiClient.cs` (respuestas programadas + captura), override `WithPuestosApiClient` en `SgvWebApplicationFactory`.
- [x] **1.7** GREEN entry colapsable "Puestos" en `_Sidenav.cshtml` (`aria-controls="puestos"`, sub-items `Listado`/`Nuevo`).
- [x] **1.8** REFACTOR+VERIFY XML docs; build verde; test slice PR 1 17/17 PASS; `bun run build`.

### Cycle Evidence (PR 1, NIT 4)

| T | RED test | GREEN impl path | REFACTOR outcome | Commit |
|---|---|---|---|---|
| 1.1 | `Get_Sidenav_WhenAuthenticated_ExposesPuestosModule` | `_Sidenav.cshtml` | XML doc + estado derivado | `feat(web)` |
| 1.2 | `GetAllAsync_Http200WithArray_ReturnsDtosAndHitsGetRoute` (×10) | `PuestosApiClient.cs`+`ToCommandResultAsync` | Catch `JsonException` (bug latente Cargos) | `feat(web)` |
| 1.3 | `IPuestosApiClient_HasAllExpectedMethods` | n/a (reflexión) | n/a | `test(web)` |
| 1.4 | (cubierto 1.2/1.3) | `Integration/Organizacion/Puesto*` | XML docs | `feat(web)` |
| 1.5 | `ProductionRegistration_ResolvesPuestosApiClient` | `Program.cs` +6 | n/a | `feat(web)` |
| 1.6 | `WithOverrides_PuestosApiClient_SwapsToFake` | `SgvWebApplicationFactory.cs` | n/a | `feat(web)` |
| 1.7 | `Get_Sidenav_WhenOnPuestosRoute_SubmenuIsActive` | `_Sidenav.cshtml` | Estado derivado | `feat(web)` |
| 1.8 | n/a | n/a | Build verde, 17/17 PASS | `docs(web)` |

## 4. PR 2 — Listado + baja + reactivación (~1200)

- [x] **2.1** RED `PuestoIndexPageTests` ≥12: render activo 6 columnas, toggle `Eliminadas` `disabled`+tooltip, búsqueda con/sin resultados, error visible, POST Delete éxito/409/404, POST Reactivate éxito/409 por código, preservación de contexto, harness JS.
- [x] **2.2** GREEN `Pages/Organizacion/Puestos/Index.cshtml(.cs)` con tabla Inspinia, `OnPostDeleteAsync`/`OnPostReactivateAsync`, `TempData`, `LastDeletedId`, `BuildToggleSegmentoRouteValues`, `MapToViewModel`, `[FromQuery] status` forward-compat.
- [x] **2.3** GREEN `wwwroot/js/pages/puestos-index.js` con `wirePuestoDeleteConfirmation`+`wirePuestoReactivateConfirmation` (SweetAlert2, `reverseButtons`, español).
- [x] **2.4** REFACTOR+VERIFY helpers extraídos; **18/18 PASS**; tokens `Crear/Editar/Habilidades` ausentes en `Index.cshtml*` y `puestos-index.js`; `bun run build`.

### Cycle Evidence (PR 2)

| T | RED test | GREEN impl path | REFACTOR outcome | Commit |
|---|---|---|---|---|
| 2.1 | `Get_Index_WhenAuthenticated_RendersActivePuestosTable` (+15 escenarios + 2 sidenav `active` diferidos en `PuestoWebSeamTests`) | n/a | n/a | `test(web)` (`f1b3a935`) |
| 2.2 | (RED 2.1) | `Index.cshtml(.cs)`+`PuestoListQuery`+`LastDeletedId`+`BuildToggleSegmentoRouteValues`+`MapToViewModel`+`BuildDetailsUrl` | (delegado a 2.4) | `feat(web)` (`8774a5f0`) |
| 2.3 | `DeleteConfirmationScript_WhenConfirmed_SubmitsFormOnce`+`ReactivateConfirmationScript_WhenConfirmed_SubmitsFormOnce` (+2 canceladas) | `puestos-index.js` | `module.exports` para harness | `feat(web)` (`3f1b299c`) |
| 2.4 | n/a | n/a | Harness JS unificado (`PuestoConfirmationKind` enum) → 28/28 PASS, token check OK | `docs(web)` (pendiente) |

## 5. PR 3 — Create + Edit + Details (~2500)

### 5.1 Mini-tabla per-file (NIT 1: re-cálculo honesto)

| Archivo | Líneas | Fuente |
|---|---|---|
| `_Form.cshtml` | ~70 | design §4.6 |
| `Create.cshtml(.cs)` | ~195 | design §4.4 (espejo Cargos PR2A 183) |
| `Edit.cshtml(.cs)` | ~215 | design §4.5 (espejo Cargos PR2B 263) |
| `Details.cshtml(.cs)` | ~110 | design §4.3 (espejo Cargos PR3 138) |
| `PuestoInputModel.cs`+`IPuestoForm.cs`+`PuestoFormKeys.cs`+`PuestoFormHelpers.cs` | ~120 | design §3.3-3.4 |
| `PuestoCreatePageTests.cs` | ~480 | ~10 tests estilo Cargos 484 |
| `PuestoEditPageTests.cs` | ~300 | ~10 tests estilo Cargos 302 |
| `PuestoDetailsPageTests.cs` | ~140 | ~5 tests estilo Cargos 140 |
| **Subtotal** | **~1630** | |
| + extensiones `FakePuestosApiClient`, fixtures, seam | ~870 | espejado Cargos PR2A+2B |
| **Total PR 3** | **~2500** | vs design 180 (14× off) |

### PR 3A — Create (~1100, base PR 2)

- [ ] **3A.1** RED `PuestoCreatePageTests` ≥8: anónimo redirige, render 6 campos, `PuestoSuperiorId` N+1 opciones, catálogo falla recuperable, POST éxito → PRG Index, POST 400 FieldErrors, POST 409 `CodigoDuplicado`, POST `HttpRequestException`/`TaskCanceledException` recuperable.
- [ ] **3A.2** GREEN `PuestoInputModel.cs`, `IPuestoForm.cs`, `PuestoFormKeys.cs`, `PuestoFormHelpers.cs`.
- [ ] **3A.3** GREEN `_Form.cshtml` (`@model IPuestoForm`, `if (!Model.IsEdit)`) + `Create.cshtml(.cs)` con `Task.WhenAll` 3 catálogos + `OnPostAsync` mapeando `PuestoCommandResult`.
- [ ] **3A.4** REFACTOR+VERIFY `TryMapCommandResult` extraído (paridad `CargoPostResultMapper`).

### PR 3B — Edit (~900, base PR 3A)

- [ ] **3B.1** RED `PuestoEditPageTests` ≥8: anónimo redirige, render 3 campos prellenados, no encontrado recuperable, **`Get_Edit_HtmlRenderizado_NoContieneCodigoUnidadOrganizativaNiCargo`** (RED obligatorio), POST éxito → PRG Details, POST 400 FieldErrors, POST 409.
- [ ] **3B.2** GREEN `Edit.cshtml(.cs)` `IsEdit=true`, `OnPostAsync` → `UpdateAsync`.
- [ ] **3B.3** REFACTOR+VERIFY tokens `>Crear<` ausentes en `Edit.cshtml`.

### PR 3C — Details (~500, base PR 3B)

- [ ] **3C.1** RED `PuestoDetailsPageTests` ≥5: anónimo redirige, render readonly con `dl.row`, no encontrado recuperable, retorno al listado preservando contexto, link a superior preservando contexto.
- [ ] **3C.2** GREEN `Details.cshtml(.cs)` readonly, link `Editar` → `Edit`, `Volver al listado` → `Index`.
- [ ] **3C.3** REFACTOR+VERIFY tokens `Crear/Reactivar` ausentes en `Details.cshtml`.

### Cycle Evidence (PR 3)

| T | RED test | GREEN impl path | REFACTOR outcome | Commit |
|---|---|---|---|---|
| 3A.1 | `Get_Create_WhenAuthenticated_RendersAllSixFields` (×8) | n/a | n/a | `test(web)` |
| 3A.2 | (cubierto 3A.1) | `PuestoInputModel.cs`+helpers | n/a | `feat(web)` |
| 3A.3 | `Get_Create_WhenAuthenticated_FormContainsCodigoInput` | `_Form.cshtml`+`Create.cshtml(.cs)` | `TryMap` extraído | `feat(web)` |
| 3A.4 | n/a | n/a | ~10/10 PASS | `docs(web)` |
| 3B.1 | **`Get_Edit_HtmlRenderizado_NoContieneCodigoUnidadOrganizativaNiCargo`** (×8) | n/a | n/a | `test(web)` |
| 3B.2 | (RED 3B.1) | `Edit.cshtml(.cs)` | n/a | `feat(web)` |
| 3B.3 | n/a | n/a | ~8/8 PASS, sin `>Crear<` | `docs(web)` |
| 3C.1 | `Get_Details_WhenAuthenticated_ShowsPuestoReadOnly` (×5) | n/a | n/a | `test(web)` |
| 3C.2 | (RED 3C.1) | `Details.cshtml(.cs)` | n/a | `feat(web)` |
| 3C.3 | n/a | n/a | ~5/5 PASS, sin `Crear/Reactivar` | `docs(web)` |

## 6. Branching strategy

`delivery_strategy: ask-always`. Dos opciones:

- **stacked-to-main**: 3 PRs apilados con `--no-ff`. PR 3 ~2500 líneas (6× budget).
- **feature-branch-chain** (recomendado): 5 PRs encadenados. Cada PR ≤1200, rollback granular. Más overhead de rebase.

Recomendación: **feature-branch-chain con 5 PRs**. Fallback: 3 PRs con `size:exception` formal en PR 3.

## 7. Comandos de validación por PR

```bash
# PR 1
dotnet restore && dotnet build SGV.slnx
dotnet test SGV.slnx --no-build --filter "FullyQualifiedName~PuestoWebSeamTests|FullyQualifiedName~PuestosApiClientTests|FullyQualifiedName~IPuestosApiClientContractTests"
(cd src/SGV.Web && bun install && bun run build)

# PR 2
dotnet test SGV.slnx --no-build --filter "FullyQualifiedName~PuestoIndexPageTests"
(cd src/SGV.Web && bun run build)

# PR 3A · 3B · 3C
dotnet test SGV.slnx --no-build --filter "FullyQualifiedName~PuestoCreatePageTests"   # 3A
dotnet test SGV.slnx --no-build --filter "FullyQualifiedName~PuestoEditPageTests"     # 3B
dotnet test SGV.slnx --no-build --filter "FullyQualifiedName~PuestoDetailsPageTests"  # 3C
(cd src/SGV.Web && bun run build)  # 3C cierra el slice visual
```

## 8. Tareas que NO existen

Tests Dominio/Aplicación/Persistencia/API (cubiertos por `archive/2026-06-19-implementa-modulo-puestos/`). E2E con browser real, performance, migraciones EF, cambios en `Program.cs` del API, `[Authorize(Roles=Administrador)]` en `PuestosController` (→ follow-up `puestos-crear-autorizacion-admin`), endpoint `/api/v1/puestos/consulta?status=...` (→ follow-up `puestos-filtro-activos-eliminados`), vista de organigrama, i18n, export PDF/Excel, búsqueda server-side full-text.

## 9. Definition of Done

- [ ] `dotnet build SGV.slnx` 0 warnings/errors.
- [ ] `dotnet test SGV.slnx --filter "FullyQualifiedName~Puesto"` 100% PASS (~50 tests del slice).
- [ ] 3 PRs (con `size:exception`) o **5 PRs (recomendado)** mergeados vía `feature-branch-chain`.
- [ ] `apply-progress.md` con Cycle Evidence Tables completas (RED→GREEN→REFACTOR).
- [ ] `verify-report.md` PASS sin CRITICAL.
- [ ] Sync delta specs a `openspec/specs/{puesto-web-listado-detalle-baja,puesto-web-crear-editar,sgv-web-shell,web-apiclient-transport-contract}/spec.md` + `archive-report.md`.