# Tasks: Implementa en el front crear/editar Cargos (Codigo editable)

> **Change:** `2026-06-30-cargos-crear-editar-codigo-editable`
> **Phase:** `sdd-tasks`
> **Strict TDD:** `true` (RED → GREEN → REFACTOR por escenario)
> **Alcance:** backend (`Codigo` editable en update + unicidad activa) + web (Create/Edit/PRG/submenú `Nueva`)

---

## 1. Convenciones

- Cada ítem es un checkbox `[ ]`; tiempo estimado **≤ 2 h** (regla `openspec/config.yaml:rules.tasks`).
- Cada tarea funcional lleva al menos un test que la cubre (dominio / aplicación / API / web).
- Numeración `PR-N.M` para trazabilidad con el PR. Tests rojos primero, luego implementación verde, luego refactor.
- Tareas puramente de soporte/docs no requieren tests propios (verificar `dotnet build` verde).

---

## 2. Review Workload Forecast

```markdown
## Review Workload Forecast

- **Total de tareas**: 26
- **Líneas estimadas modificadas**: ~1080 (Dominio ~30 + Aplicación ~180 + API ~30 + Infraestructura ~10 + Web ~680 + Tests ~150)
- **Distribución por capa**:
  - Dominio: ~30
  - Aplicación: ~180
  - API: ~30
  - Infraestructura: ~10
  - Web: ~680
  - Tests: ~150
- **Distribución por PR (Opción A — Chained)**:
  - PR 1 Backend (cargos): ~390 líneas en ~12 archivos
  - PR 2 Frontend (cargos-web): ~690 líneas en ~14 archivos
- **Chained PRs recommended**: Yes
- **400-line budget risk**: High (PR 2 excede 400)
- **Decision needed before apply**: Yes (delivery strategy = `ask-always`)
- **Justificación**: cambio cruza backend + frontend y revierte decisión archivada. PR 1 cabe bajo 400; PR 2 excede 400. Se recomienda chained con `size:exception` para PR 2 salvo que el usuario quiera dividir create/edit en PR 2A/2B (alternativa no modelada acá).
```

**Decisión que NO toma `sdd-tasks`:** el split final (chained con `size:exception` para PR 2, o single PR global). Queda para el orchestrator + usuario bajo `ask-always`.

### Work units sugeridos

| U | Meta | PR | Notas |
|---|------|----|-------|
| U1 | Backend: `Codigo` editable + unicidad activa + tests | PR 1 | ~390 líneas, base PR 2 |
| U2 | Frontend: cliente Create/Update/Niveles + Create + Edit + submenú + tests | PR 2 | ~690 líneas, depende de PR 1 |

---

## 3. PR 1 — Backend (cargos)

> **Rama destino:** trunk (decisión del usuario). **Estado post-PR1:** `dotnet build SGV.slnx` y `dotnet test --filter "FullyQualifiedName~Cargo"` en verde.

### Fase 1.1 — Dominio

- [ ] **PR-1.1 RED: tests de dominio para cambio de `Codigo`** — capa Dominio (tests); `tests/SGV.Tests/Dominio/Organizacion/CargoTests.cs`; tests `Actualizar_CambiaCodigoSiNoDuplica`, `Actualizar_ConCodigoVacio_ThrowsArgumentException`, `Actualizar_ConCodigoMayorA50_ThrowsArgumentException`. Eliminar o reemplazar `Actualizar_CodigoNoCambia` y `Codigo_EsInmutableTrasCreacion`. **0.5 h**.

- [ ] **PR-1.2 GREEN: `Cargo.Actualizar` acepta `codigo`** — capa Dominio; `src/SGV.Dominio/Organizacion/Cargo.cs`; nueva firma `Actualizar(string codigo, string nombre, Guid nivelId, string? descripcion = null)`; `Codigo` mantiene `private set` y se reasigna con `ValidacionesDominio.Requerido(codigo, ..., 50)`; actualizar XML doc. PR-1.1 verde. **0.5 h**. Dep: PR-1.1.

### Fase 1.2 — Aplicación

- [ ] **PR-1.3 RED: tests del validator para `Codigo` en update** — capa Aplicación (tests); `tests/SGV.Tests/Aplicacion/Organizacion/ActualizarCargoRequestValidatorTests.cs`; tests `Should_Have_Error_When_Codigo_Is_Empty/Null/Whitespace`, `Should_Have_Error_When_Codigo_Exceeds_Max_Length`, `Should_Not_Have_Error_For_Valid_Codigo`; ajustar `RequestValido()` para incluir `Codigo`. **0.5 h**. Dep: PR-1.2.

- [ ] **PR-1.4 GREEN: extender `ActualizarCargoRequest` + validator** — capa Aplicación; `src/SGV.Aplicacion/Organizacion/Comandos/CargoRequests.cs`, `src/SGV.Aplicacion/Organizacion/Comandos/Validaciones/ActualizarCargoRequestValidator.cs`; agregar `string Codigo` como primer parámetro del record; `RuleFor(x => x.Codigo).NotEmpty().MaximumLength(50)` en el validator. PR-1.3 verde. **0.5 h**. Dep: PR-1.3.

- [ ] **PR-1.5 RED: tests de servicio para unicidad activa en update** — capa Aplicación (tests); `tests/SGV.Tests/Aplicacion/Organizacion/CargoServicioComandosTests.cs`; tests `ActualizarAsync_CodigoDuplicadoActivo_RetornaConflictoYSinGuardar`, `ActualizarAsync_MismoCodigoEnOtroCargoEliminado_PermiteOperacion`, `ActualizarAsync_CodigoInvalido_CortaAntesDeConsultarRepos`, `ActualizarAsync_CodigoDuplicado_RaceCondition_DevuelveConflicto` (vía `DbUpdateException` simulada), `ActualizarAsync_CodigoSinCambio_NoFallaValidacionUnicidad`. **1.0 h**. Dep: PR-1.4.

- [ ] **PR-1.6 GREEN: `CargoServicioComandos.ActualizarAsync` con unicidad activa + helper compartido** — capa Aplicación; `src/SGV.Aplicacion/Organizacion/Comandos/CargoServicioComandos.cs`; introducir helper privado `EnsureCodigoNoDuplicado(string codigo, Guid id, CancellationToken)` reutilizable entre `CrearAsync` y `ActualizarAsync`; invocar `repository.ExistsActiveCodeAsync(request.Codigo, excludingId: id, cancellationToken)` después del check de `NivelId`; mapear violación de índice único (`DbUpdateException`) a `Conflict "CodigoDuplicado"` con try/catch alrededor de `unitOfWork.SaveChangesAsync`; propagar `request.Codigo` a `cargo.Actualizar(...)`. PR-1.5 verde. **1.5 h**. Dep: PR-1.5.

### Fase 1.3 — API

- [ ] **PR-1.7 RED: tests de API para `PUT` con `codigo` (400/409)** — capa API (tests); `tests/SGV.Tests/Api/CargosControllerTests.cs`; tests `Put_ValidRequest_WithCodigo_Returns200OkWithUpdatedDto` (ajustar existente para enviar `codigo`), `Put_EmptyCodigo_Returns400WithFieldErrors`, `Put_DuplicateActiveCodigo_Returns409WithProblemDetails`. **0.5 h**. Dep: PR-1.6.

- [ ] **PR-1.8 GREEN: actualizar XML doc y contrato de `PUT`** — capa API; `src/SGV.Api/Controllers/CargosController.cs`; comentarios XML reflejan que `PUT` edita `codigo`; sin lógica nueva (el `CargoCommandResult.Failure` ya cubre `FieldErrors` para 400 y `Error` para 409). PR-1.7 verde. **0.5 h**. Dep: PR-1.7.

### Fase 1.4 — Infraestructura y verificación

- [ ] **PR-1.9 Verificar índice único cubre update** — capa Infraestructura (verificación); `src/SGV.Infraestructura/Persistencia/Migraciones/20260614183103_InicialSgvo.cs` (sólo lectura); confirmar `IX_Cargos_ActiveCodigoUnique` activo en MySQL y `ExistsActiveCodeAsync` cubre update; sin migración nueva; documentar en `apply-progress.md` que el índice es árbitro final y el pre-check es para UX. **0.25 h**. Dep: PR-1.6.

- [ ] **PR-1.10 VERIFY: build + suite Cargo** — capa Soporte; `dotnet build SGV.slnx` + `dotnet test SGV.slnx --filter "FullyQualifiedName~Cargo|FullyQualifiedName~Cargos"` en verde; `dotnet test --filter "FullyQualifiedName~UnidadOrganizativa"` sin regresión. **0.5 h**. Dep: PR-1.8, PR-1.9.

- [ ] **PR-1.11 Regenerar `docs/migracion-inicial-sgv.sql` (condicional)** — capa Soporte; `docs/migracion-inicial-sgv.sql`; como PR-1.9 confirma que no hay migración nueva, esta tarea se cierra sin acción pero queda registrada. Si `sdd-apply` requiriera migración, ejecutar `dotnet ef migrations script --idempotent --output docs/migracion-inicial-sgv.sql`. **0.25 h**. Dep: PR-1.9.

---

## 4. PR 2 — Frontend (cargos-web)

> **Rama destino:** trunk. **Depende de PR 1 mergeado.** **Estado post-PR2:** Create/Edit listos, submenú `Nueva` visible, suite `CargoWebTests` + `UnidadOrganizativaWebTests` verde; `bun run build` en `src/SGV.Web` verde.

### Fase 2.1 — Cliente HTTP e infraestructura web

- [ ] **PR-2.1 RED: tests de `ICargoApiClient.CreateAsync/UpdateAsync/GetNivelesAsync`** — capa Web (tests); `tests/SGV.Tests/Web/Cargo/CargoApiClientTests.cs`; tests `CreateAsync_Http201WithPayload_ReturnsDtoAndHitsPostRoute`, `UpdateAsync_Http200WithPayload_ReturnsDtoAndHitsPutRoute`, `UpdateAsync_Http400WithValidationProblemDetails_ReturnsFailureWithFieldErrors`, `GetNivelesAsync_Http200WithArray_ReturnsDtosAndHitsCatalogRoute`. **0.75 h**. Dep: PR-1.10.

- [ ] **PR-2.2 GREEN: extender `ICargoApiClient` + `CargoApiClient`** — capa Web; `src/SGV.Web/Integration/Organizacion/ICargoApiClient.cs`, `src/SGV.Web/Integration/Organizacion/CargoApiClient.cs`; agregar `CreateAsync(CrearCargoRequest, CancellationToken) → Task<CargoCommandResult>`, `UpdateAsync(Guid, ActualizarCargoRequest, CancellationToken) → Task<CargoCommandResult>`, `GetNivelesAsync(CancellationToken) → Task<IReadOnlyList<NivelCargoDto>>`; implementar con `PostAsJsonAsync`/`PutAsJsonAsync` y parsing de `ValidationProblemDetails` + `ProblemDetails` (reusar patrón de `UnidadOrganizativaApiClient.ToCommandResultAsync`); ruta `/api/v1/niveles-cargo`. PR-2.1 verde. **1.0 h**. Dep: PR-2.1.

- [ ] **PR-2.3 Extender `FakeCargoApiClient`** — capa Web (tests); `tests/SGV.Tests/Web/Cargo/FakeCargoApiClient.cs`; agregar `CreateResult`/`UpdateResult`/`NivelesResult` configurables, propiedades `CreateCalls`/`UpdateCalls`/`NivelesCalls`, métodos default que devuelven `CargoCommandResult.Success(...)` o listas vacías (espejo de `FakeUnidadOrganizativaApiClient`). **0.75 h**. Dep: PR-2.2.

- [ ] **PR-2.4 Confirmar override en `SgvWebApplicationFactory`** — capa Web (tests); `tests/SGV.Tests/Web/SgvWebApplicationFactory.cs` (sin cambios), `tests/SGV.Tests/Web/Cargo/CargoWebSeamTests.cs` (regresión); extender `WithOverrides_CargoApiClient_SwapsToFakeImplementation` para que también cubra resolución de Create/Update/GetNiveles. **0.25 h**. Dep: PR-2.3.

### Fase 2.2 — Modelo e input compartido

- [ ] **PR-2.5 Crear `CargoInputModel`** — capa Web; `src/SGV.Web/Integration/Organizacion/CargoInputModel.cs`; record/class con `Codigo` (`[Required]`, `[StringLength(50)]`), `Nombre` (`[Required]`, `[StringLength(200)]`), `Descripcion?` (`[StringLength(1000)]`), `NivelId` (`[Required]`, Guid). DataAnnotations en español. **0.5 h**. Dep: PR-2.2.

- [ ] **PR-2.6 Crear `ICargoForm`** — capa Web; `src/SGV.Web/Integration/Organizacion/ICargoForm.cs`; interface con `CargoInputModel Input { get; }`, `IReadOnlyList<NivelCargoDto> NivelOptions { get; }`, `string? ErrorMessage { get; }` (espejo de `IUnidadOrganizativaForm`). **0.25 h**. Dep: PR-2.5.

- [ ] **PR-2.7 Crear `CargoFormHelpers`** — capa Web; `src/SGV.Web/Integration/Organizacion/CargoFormHelpers.cs`; helper estático `ApplyFieldErrorsToModelState(ModelStateDictionary, IReadOnlyDictionary<string, string[]>?)` espejo de `UnidadOrganizativaFormHelpers.ApplyFieldErrorsToModelState`. **0.5 h**. Dep: PR-2.5.

### Fase 2.3 — Partial compartido y navegación

- [ ] **PR-2.8 Crear `_Form.cshtml`** — capa Web; `src/SGV.Web/Pages/Organizacion/Cargos/_Form.cshtml`; `@model ICargoForm`; campos `Codigo`, `Nombre`, `Descripcion` y dropdown `Nivel` (`asp-items="new SelectList(Model.NivelOptions, \"Id\", \"Nombre\")"`); estilo Inspinia `form-floating`; `asp-validation-for` por campo y `asp-validation-summary="ModelOnly"`. **0.75 h**. Dep: PR-2.6.

- [ ] **PR-2.9 Agregar entrada `Nueva` en `_Sidenav`** — capa Web; `src/SGV.Web/Pages/Shared/Partials/_Sidenav.cshtml`; agregar `<li class="side-nav-item"><a class="side-nav-link" href="/organizacion/cargos/crear"><span class="menu-text">Nueva</span></a></li>` dentro del submenú del grupo `cargos` (estado `active` ya heredado por `StartsWithSegments`). **0.25 h**. Dep: PR-2.8.

### Fase 2.4 — Página Create

- [ ] **PR-2.10 Crear `Create.cshtml` + `Create.cshtml.cs`** — capa Web; `src/SGV.Web/Pages/Organizacion/Cargos/Create.cshtml`, `src/SGV.Web/Pages/Organizacion/Cargos/Create.cshtml.cs`; `[Authorize]` `PageModel` que implementa `ICargoForm`; `OnGetAsync` carga `GetNivelesAsync()` y muestra error recuperable si falla; `OnPostAsync` valida ModelState, traduce `CargoCommandResult` a `Input.*` con `CargoFormHelpers.ApplyFieldErrorsToModelState`, PRG a `/organizacion/cargos/detalles/{id}` con `TempData["StatusMessage"]` en éxito. **1.5 h**. Dep: PR-2.8, PR-2.5.

- [ ] **PR-2.11 Tests web de Create** — capa Web (tests); `tests/SGV.Tests/Web/Cargo/CargoCreateEditPageTests.cs` (nuevo); tests `Get_Create_WhenAnonymous_RedirectsToSignIn`, `Get_Create_WhenAuthenticated_LoadsNivelesDropdown`, `Post_Create_WhenSuccessful_RedirectsToDetailsWithConfirmation`, `Post_Create_WhenConflictOnCodigo_ShowsFieldErrorAndKeepsForm`, `Post_Create_WhenBackendUnavailable_ShowsRecoverableError`. **1.5 h**. Dep: PR-2.10.

### Fase 2.5 — Página Edit y CTAs de navegación

- [ ] **PR-2.12 Crear `Edit.cshtml` + `Edit.cshtml.cs`** — capa Web; `src/SGV.Web/Pages/Organizacion/Cargos/Edit.cshtml`, `src/SGV.Web/Pages/Organizacion/Cargos/Edit.cshtml.cs`; `[Authorize]` `PageModel` que implementa `ICargoForm`; `OnGetAsync(id, p, search, sort)` carga `GetByIdAsync` + `GetNivelesAsync`, prellena `Input`, estado `IsRecoverable` si no existe; `OnPostAsync` valida ModelState, envía `UpdateAsync`, traduce `FieldErrors`/`Conflict`, PRG a sí mismo con `TempData["StatusMessage"]`; recarga catálogo tras error. **2.0 h**. Dep: PR-2.10.

- [ ] **PR-2.13 Tests web de Edit** — capa Web (tests); `tests/SGV.Tests/Web/Cargo/CargoCreateEditPageTests.cs`; tests `Get_Edit_WhenAnonymous_RedirectsToSignIn`, `Get_Edit_WhenAuthenticated_PrepopulatesFormAndNiveles`, `Get_Edit_WhenCargoNotFound_ShowsRecoverableState`, `Post_Edit_WhenSuccessful_RedirectsToEditWithConfirmation`, `Post_Edit_WhenCodigoConflict_ShowsFieldErrorAndKeepsForm`, `Post_Edit_WhenValidationFails_ShowsFieldErrors`. **1.75 h**. Dep: PR-2.12.

- [ ] **PR-2.14 Modificar `Details.cshtml` y `Index.cshtml` para CTAs** — capa Web; `src/SGV.Web/Pages/Organizacion/Cargos/Details.cshtml`, `src/SGV.Web/Pages/Organizacion/Cargos/Index.cshtml`; en `Details.cshtml` agregar botón "Editar" con `href="/organizacion/cargos/editar/{id}?p=...&search=...&sort=..."` (preservar contexto); en `Index.cshtml` agregar CTA "Crear cargo" en header de la card con `href="/organizacion/cargos/crear"`; ajustar `CargoDetailsPageTests` (quitar `Assert.DoesNotContain(">Editar<", ...)` y reemplazarlo por `Assert.Contains("Editar", content)`). **0.75 h**. Dep: PR-2.13.

- [ ] **PR-2.15 Tests de navegación y submenú** — capa Web (tests); `tests/SGV.Tests/Web/CargoWebTests.cs`, `tests/SGV.Tests/Web/Cargo/CargoIndexPageTests.cs`; tests `Get_Sidenav_WhenAuthenticated_ExposesCargosNuevaEntry`, `Get_Index_WhenAuthenticated_ShowsCreateCta`, `Get_Details_WhenAuthenticated_ShowsEditButton`; ajustar aserciones de `CargoDetailsPageTests` que asumían readonly estricto. **0.75 h**. Dep: PR-2.14.

### Fase 2.6 — Verificación final del frontend

- [ ] **PR-2.16 VERIFY: build + suite web completa** — capa Soporte; `dotnet build SGV.slnx`; `dotnet test SGV.slnx --filter "FullyQualifiedName~Web"` verde; `bun install && bun run build` en `src/SGV.Web` verde (smoke pipeline frontend); `dotnet test SGV.slnx` (suite completa sin filtro) sin regresiones. **0.75 h**. Dep: PR-2.15.

---

## 5. Tareas de soporte / docs

> Viven en la fase archive (PR final), no en `sdd-apply`. Sólo referencia para auditoría:

- **archive**: sincronizar `openspec/specs/cargo-management/spec.md` con el delta (`Codigo` editable, escenarios 200/400/404/409) y reconciliar `openspec/specs/cargo-web-listado-detalle-baja/spec.md` con la nueva cobertura de create/edit. Redactar `archive-report.md` en español.
- **archive**: regenerar `docs/migracion-inicial-sgv.sql` sólo si PR 1 hubiera generado migración nueva. Confirmado en PR-1.11 que **no aplica**.

---

## 6. Riesgos operativos para el apply

- **Conflictos de rebase esperados (chained PR):** PR 2 depende de PR 1 mergeado. Si el equipo rebasa PR 2 contra `main` antes de mergear PR 1, `CargoApiClientTests` no compila por la nueva firma de `ActualizarCargoRequest`. Convención: merge PR 1 → abrir PR 2 con base `main`.
- **Orden de merge sugerido:** PR 1 → PR 2. Sin dependencias cruzadas adicionales.
- **Tests críticos que NO pueden faltar:**
  - Dominio: `Actualizar_CambiaCodigoSiNoDuplica` + `Actualizar_ConCodigoVacio_ThrowsArgumentException`.
  - Aplicación: `ActualizarAsync_CodigoDuplicadoActivo_RetornaConflictoYSinGuardar` + `ActualizarAsync_CodigoInvalido_CortaAntesDeConsultarRepos`.
  - API: `Put_DuplicateActiveCodigo_Returns409WithProblemDetails`.
  - Web: `Post_Edit_WhenCodigoConflict_ShowsFieldErrorAndKeepsForm` + `Get_Sidenav_WhenAuthenticated_ExposesCargosNuevaEntry`.
- **Posibles roturas de `dotnet build` durante implementación:**
  - `ActualizarCargoRequest` cambia firma: ejecutar `grep -rn "new ActualizarCargoRequest" src tests` antes de mergear PR 1 para cazar todos los call sites.
  - `ICargoApiClient` se extiende: el `FakeCargoApiClient` debe implementar los nuevos métodos antes de que `SgvWebApplicationFactory` lo resuelva (PR-2.3 antes de PR-2.4).
  - `_Form.cshtml` con `asp-items`: verificar que el archivo agregue el `using` de `Microsoft.AspNetCore.Mvc.Rendering` si lo necesita.
- **Breaking change contractual:** `PUT /api/v1/cargos/{id}` ahora requiere `codigo`. Documentar en el body del PR 1 y avisar a consumidores externos si los hubiera (el design asume que no los hay dentro del repo).
- **Tamaño PR 2:** ~690 líneas excede el budget 400. Forecast sugiere chained con `size:exception` para PR 2 (decisión del usuario bajo `ask-always`). Si el usuario rechaza la excepción, alternativa: dividir PR 2 en `PR 2A — Create + cliente + submenú` (~350 líneas) y `PR 2B — Edit + details CTA` (~340 líneas). No se modela en este tasks.md porque la task description pidió 2 PRs.

---

## 7. PR 2A refactor cleanup (chained, behavior-preserving)

> **Rama destino:** trunk. **Depende de PR2A mergeado** (HEAD `7d36c65b` sobre `feat/cargos-crear-editar-codigo-editable-pr2a`). **Estado post-PR:** los 4 ítems diferidos por el orchestrator al cierre de PR2A quedan aplicados en commits atómicos. Cero cambio de comportamiento. Suite `~Cargo|Cargos` y `~Web` verdes.

### Cleanup.1 — Extraer `CargoPostResultMapper`

> Item diferido de PR2A: el mapeo inline del response no-2xx en `Create.cshtml.cs` no es testeable de forma aislada. Extraer a una clase dedicada con `TryMap(CargoCommandResult?, ModelStateDictionary) → bool`. **Strict TDD** (RED → GREEN por el test nuevo).

- [ ] **Cleanup.1.1 RED**: crear `tests/SGV.Tests/Web/Cargo/CargoPostResultMapperTests.cs` con al menos 4 casos: null result, result vacío, result con `FieldErrors` (múltiples keys + mensajes), result con `ErrorMessage` (sin `FieldErrors`). Verificar que el test falla al inicio (no compila: `CS0103`/`CS0246` porque el tipo no existe).
- [ ] **Cleanup.1.2 GREEN**: crear `src/SGV.Web/Integration/Organizacion/CargoPostResultMapper.cs` con `public static bool TryMap(CargoCommandResult? result, ModelStateDictionary modelState)`. La resolución: (1) si `result?.FieldErrors` tiene entries → `CargoFormHelpers.ApplyFieldErrorsToModelState` y retorna `true`; (2) else si `result?.Error?.Message` no es null/whitespace → `modelState.AddModelError(string.Empty, ...)` y retorna `false`; (3) else retorna `false`. Tests pasan.
- [ ] **Cleanup.1.3 GREEN**: `Create.cshtml.cs` delega a `CargoPostResultMapper.TryMap(result, ModelState)`. El branch `Conflict → Input.Codigo` se mantiene en línea; el fallback defensivo para `Error.Message` null se preserva. 250/250 cargo + 108/108 web verde.

### Cleanup.2 — Mover `ICargoForm` a `Integration/`

> Item diferido de PR2A: la asimetría con `IUnidadOrganizativaForm` (en `Integration/`) quedó documentada para resolver en una iteración posterior. Aplicar el move ahora. Behavior-preserving puro.

- [ ] **Cleanup.2.1**: `git mv` `src/SGV.Web/Pages/Organizacion/Cargos/ICargoForm.cs` → `src/SGV.Web/Integration/Organizacion/ICargoForm.cs`. Cambiar el namespace a `SGV.Web.Integration.Organizacion`. Quitar el `using` redundante del propio namespace destino.
- [ ] **Cleanup.2.2**: actualizar `@model` en `src/SGV.Web/Pages/Organizacion/Cargos/_Form.cshtml` a `SGV.Web.Integration.Organizacion.ICargoForm`. `Create.cshtml.cs` ya tiene el `using` correcto, no requiere cambios. 250/250 cargo + 108/108 web verde (los tests de `_Form.cshtml` ejercitan el render indirectamente).

### Cleanup.3 — Test fixture compartido para los tests web de Cargo

> Item diferido de PR2A: las 3 suites (`CargoCreatePageTests`, `CargoDetailsPageTests`, `CargoIndexPageTests`) duplican el setup de `RecordingHttpMessageHandler` + `ExtractAntiforgeryTokenAsync` + `CreateAuthenticatedClientAsync` + el helper `CreateCargo`. Extraer a `IClassFixture<CargoWebTestFixture>`. Behavior-preserving.

- [ ] **Cleanup.3.1**: crear `tests/SGV.Tests/Web/Cargo/CargoWebTestFixture.cs` (`IDisposable`) con: `BaseFactory` (expone el `SgvWebApplicationFactory` base), `WithCargoApiClient(FakeCargoApiClient)` (devuelve un factory con override), `CreateAuthenticatedClientAsync(FakeCargoApiClient)` (auth flow completo), `static ExtractAntiforgeryTokenAsync(HttpResponseMessage)`, `static JuniorNivelId` / `static SeniorNivelId` (constantes), `static BuildCargoDto(codigo, nombre, descripcion, nivelNombre)`, nested `RecordingHttpMessageHandler`.
- [ ] **Cleanup.3.2**: cada una de las 3 test classes declara `IClassFixture<CargoWebTestFixture>`, recibe el fixture en el constructor, y reemplaza las llamadas a helpers privados por `_fixture.X(...)` (o `CargoWebTestFixture.X(...)` para los estáticos). Las constantes locales (`JuniorNivelId`, `SeniorNivelId`) y el `private static CreateCargo` se eliminan. Nombres de tests y aserciones no cambian. 27/27 tests verdes en `~CargoCreatePageTests|~CargoIndexPageTests|~CargoDetailsPageTests`.

### Cleanup.4 — Unificación de XML docs en archivos cambiados

> Item diferido de PR2A: el idioma por defecto según `AGENTS.md` es inglés. Auditar y unificar dentro de cada archivo. Si un archivo ya es consistente (todo español o todo inglés), preservar el idioma existente y documentar la decisión.

- [ ] **Cleanup.4.1**: auditar `///` doc comments en: `Create.cshtml.cs`, `ICargoForm.cs`, `CargoFormHelpers.cs`, `CargoPostResultMapper.cs`, `_Form.cshtml` (solo XML-style, no comentarios Razor), `CargoCreatePageTests.cs`, `CargoPostResultMapperTests.cs`, `CargoWebTestFixture.cs`. Para cada archivo, elegir un idioma y unificar internamente.
- [ ] **Cleanup.4.2**: documentar en `apply-progress.md` el idioma elegido por archivo y la razón (regla por defecto de `AGENTS.md` o contexto existente). Default = inglés; excepción = preservar el idioma consistente del archivo si el contexto existente es claro.

### Resumen de la cadena

| Commit | Tarea | Tipo | TDD | Líneas (estimadas) |
|---|---|---|---|---|
| 1 | Cleanup.1.1+1.2+1.3 | refactor | RED→GREEN | ~175 (mapper 76 + tests 95 + Create.cshtml.cs delta 4) |
| 2 | Cleanup.2.1+2.2 | refactor | N/A | ~3 (move + 1 line) |
| 3 | Cleanup.3.1+3.2 | refactor | N/A (helpers move) | net ~-40 (fixture 138, test files -178) |
| 4 | Cleanup.4.1+4.2 | docs | N/A | ~0 (solo swap de idioma) |
| 5 | OpenSpec artifacts | docs | N/A | tasks.md + apply-progress.md + SDD untracked files |

**Total estimado**: ~150 líneas net (incluyendo 95 líneas del test nuevo del mapper). Bien dentro del budget de 400.

---

## 8. PR 2B — Frontend Edit + Details CTA (chained)

> **Rama destino:** develop. **Depende de PR2A mergeado** (develop en `6c1553e0` después de PR #63). **Estado post-PR:** página Edit funcional con PRG + TempData, botón "Editar" en Details preservando contexto (p/search/sort). Suite `~Cargo|Cargos` y `~Web` verde.

### Review Workload Forecast PR 2B

- **Líneas estimadas**: ~500 (Cliente HTTP ~80 + Web ~210 + Tests ~150 + Artefactos ~60).
- **Distribución por capa**:
  - Web (`ICargoApiClient.UpdateAsync` + impl + Edit + Details CTA): ~360
  - Tests (CargoApiClient Update + CargoEditPage + ajuste Details): ~150
  - Artefactos (`tasks.md` + `apply-progress.md`): ~60
- **Chained PRs recommended**: N/A (PR 2B es el último eslabón del split; mergea solo contra develop).
- **400-line budget risk**: High — `size:exception` probable (PR1 y PR2A ya usaron `size:exception`; PR 2B hereda el patrón).
- **Justificación**: incluye nuevo método en cliente HTTP + página Razor completa + 6 tests Edit + inversión de la aserción `DoesNotContain "Editar"` → `Contains "Editar"` en `CargoDetailsPageTests`.

### Fase 8.1 — Cliente HTTP: agregar `UpdateAsync`

- [ ] **PR-2B.1 RED+GREEN: cliente `UpdateAsync` (interface + impl + fake + tests)** — capa Web; `src/SGV.Web/Integration/Organizacion/ICargoApiClient.cs`, `src/SGV.Web/Integration/Organizacion/CargoApiClient.cs`, `tests/SGV.Tests/Web/Cargo/FakeCargoApiClient.cs`, `tests/SGV.Tests/Web/Cargo/CargoApiClientTests.cs`; tests `UpdateAsync_Http200WithPayload_ReturnsDtoAndHitsPutRoute`, `UpdateAsync_Http400WithValidationProblemDetails_ReturnsFailureWithFieldErrors`, `UpdateAsync_Http409WithProblemDetails_ReturnsFailureWithConflict`; agregar `Task<CargoCommandResult> UpdateAsync(Guid id, ActualizarCargoRequest request, CancellationToken cancellationToken = default)`; implementar con `PutAsJsonAsync` reusando `ToCommandResultAsync`; URL `/api/v1/cargos/{id}`; extender `FakeCargoApiClient` con `UpdateResult`, `UpdateCalls` (captura `(Guid Id, ActualizarCargoRequest Request, CancellationToken)`), `UpdateException`. **1.5 h**. Dep: PR2A mergeado (develop @ 6c1553e0).

### Fase 8.2 — Página Edit

- [ ] **PR-2B.2 RED: tests web de Edit (GET/POST)** — capa Web (tests); `tests/SGV.Tests/Web/Cargo/CargoEditPageTests.cs` (nuevo, separado de `CargoCreatePageTests` para evitar acoplamiento); tests `Get_Edit_WhenAnonymous_RedirectsToSignIn`, `Get_Edit_WhenAuthenticated_PrepopulatesFormAndNiveles`, `Get_Edit_WhenCargoNotFound_ShowsRecoverableState`, `Post_Edit_WhenSuccessful_RedirectsToEditWithConfirmation`, `Post_Edit_WhenCodigoConflict_ShowsFieldErrorAndKeepsForm`, `Post_Edit_WhenValidationFails_ShowsFieldErrors`. Reusar `CargoWebTestFixture`. **1.5 h**. Dep: PR-2B.1.

- [ ] **PR-2B.3 GREEN: crear `Edit.cshtml` + `Edit.cshtml.cs`** — capa Web; `src/SGV.Web/Pages/Organizacion/Cargos/Edit.cshtml`, `src/SGV.Web/Pages/Organizacion/Cargos/Edit.cshtml.cs`; `[Authorize]` `PageModel` que implementa `ICargoForm` con `IsEdit = true`; `OnGetAsync(Guid id, [FromQuery] int? p, [FromQuery] string? search, [FromQuery] string? sort)` carga `GetByIdAsync` + `GetNivelesAsync`, prellena `Input`; `IsRecoverable` cuando el cargo no existe; `OnPostAsync(Guid id, [FromQuery] int? p, [FromQuery] string? search, [FromQuery] string? sort)` valida ModelState, llama `UpdateAsync`, traduce `FieldErrors` (vía `CargoFormHelpers.ApplyFieldErrorsToModelState`) y `Conflict → Input.Codigo` (vía `ModelState.AddModelError` directo, mismo patrón que Create), PRG a sí mismo con `TempData["StatusMessage"]`; recarga catálogo tras error. Reusar `_Form.cshtml`. PR-2B.2 verde. **2.0 h**. Dep: PR-2B.2.

### Fase 8.3 — CTA de Details

- [ ] **PR-2B.4 Modificar `Details.cshtml` y ajustar `CargoDetailsPageTests`** — capa Web + Web (tests); `src/SGV.Web/Pages/Organizacion/Cargos/Details.cshtml`, `tests/SGV.Tests/Web/Cargo/CargoDetailsPageTests.cs`; en `Details.cshtml` agregar `<a class="btn btn-primary" href="@Url.Page("/Organizacion/Cargos/Edit", new { id = Model.Cargo!.Id, p = Model.CurrentPage, search = Model.Search, sort = Model.Sort })"><i class="ti ti-pencil me-1"></i>Editar</a>` solo si el cargo está disponible (no en `IsNotFound`); preservar query string para volver al listado filtrado tras guardar. En `CargoDetailsPageTests.Get_Details_WhenAuthenticated_ShowsCargoReadOnly`: sustituir `Assert.DoesNotContain(">Editar<", ...)` por `Assert.Contains("Editar", content)` + aserción de que el link apunta a `/organizacion/cargos/editar/{id}` con preservación de query string. En `Get_Details_WhenCargoNotFound_ShowsNotAvailableState`: mantener `Assert.DoesNotContain(">Editar<", ...)` (correcto: no debe haber Edit en estado no disponible). **0.75 h**. Dep: PR-2B.3.

### Fase 8.4 — Verificación final

- [ ] **PR-2B.5 VERIFY: build + suite cargo + web** — capa Soporte; `dotnet build SGV.slnx` + `dotnet test SGV.slnx --filter "FullyQualifiedName~Cargo|FullyQualifiedName~Cargos"` + `dotnet test SGV.slnx --filter "FullyQualifiedName~Web"` en verde; `bun install && bun run build` en `src/SGV.Web` verde. **0.5 h**. Dep: PR-2B.4.

### Notas de revisión para el orchestrator

- **PR 2B no depende de backend nuevo**: el endpoint `PUT /api/v1/cargos/{id}` con `Codigo` ya está mergeado desde PR1.
- **PR 2B hereda infraestructura web del PR2A cleanup**: reusa `CargoPostResultMapper`, `CargoWebTestFixture`, e `ICargoForm` (en `Integration/Organizacion/`).
- **Edición vs. creación**: la página Edit usa el mismo `_Form.cshtml` que Create. La única diferencia visible al usuario es la ruta (`/organizacion/cargos/editar/{id}` vs `/organizacion/cargos/crear`) y el comportamiento PRG (Edit redirige a sí mismo, Create a Details).
- **Preservar contexto de paginación**: el botón "Editar" en Details debe propagar `p`, `search`, `sort` como query string para que la página Edit pueda redirigir de vuelta al listado filtrado tras guardar (vía `CargoFormHelpers.BuildReturnToListUrl`).
- **Tests de Details a ajustar**: el caso "cargo encontrado" invierte su aserción; el caso "no disponible" mantiene la negación.
- **Issue #62 (backend `[Authorize]` POST/PUT/DELETE)** sigue fuera de scope. La página Edit tiene `[Authorize]` pero el endpoint `PUT /api/v1/cargos/{id}` queda abierto. Resolver en change SDD aparte si el usuario decide cerrar #62.
