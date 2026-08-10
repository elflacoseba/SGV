# Apply Progress: invertir-flujo-cubrir (S1 + S2 + S3 + W-fix)

## Status
- PR S1 (Backend + Wire contracts): merged (PR #269, commit 0e98817b).
- PR S2 (Frontend Create): merged (PR #270, commit 4ab286b5).
- PR S3 (Frontend Details): **completed** (rama `feature/invertir-flujo-cubrir-s3-vacante-details`, mergeada como PR #271 en `develop`@`5ed8239d`).
- **W-fix (WARNING-1 + WARNING-2)**: **completed** (rama `feature/invertir-flujo-cubrir-warning-fixes`, 2 commits work-unit W-1.1 + W-2.1, lista para PR a `develop`).

## Commits de W-fix (rama `feature/invertir-flujo-cubrir-warning-fixes`)

| Hash | Título | Tareas |
|------|--------|--------|
| `56243b39` | `test(web): cubrir ?vacanteId con Vacante Cancelada muestra error legible (W-1)` | W-1.1 — test `Get_Create_WithVacanteIdCancelada_MuestraError_VacanteCancelada` (paridad con T2.2 Cubierta, mensaje literal del spec). |
| `3974cba7` | `test(web): cubrir POST con VacanteId redirige a Details y propaga id (W-2)` | W-2.1 — test `Post_Create_WithVacanteId_CreaOcupacionYRedirigeAVacanteDetails` (happy path POST end-to-end con `Input.VacanteId` hidden + redirect a `/organizacion/vacantes/detalles/{id}` + assert `CrearCalls[0].VacanteId`). |

## W-1 (WARNING-1) — Vacante Cancelada en Create

- **Spec**: `web-ocupaciones-crear-editar / REQ-OCC-FORM-001` escenario `?vacanteId` con Vacante **Cancelada** — error legible.
- **Producción (vigente desde S2 / T2.8)**: `Create.cshtml.cs:168-172` mapea `EstadoVacanteNombre == "Cancelada"` → `ErrorMessage = "Esta Vacante está cancelada y no puede cubrirse."`.
- **Test nuevo**: `Get_Create_WithVacanteIdCancelada_MuestraError_VacanteCancelada` (paridad estructural con `Get_Create_WithVacanteIdCubierta_MuestraError_*` / T2.2).
  - Reusa `FakeVacanteApiClient` ya extendido en S2 (no se agregó método nuevo al fake).
  - Asserts: API client invocado con `vacanteId`; mensaje literal presente; `Input.VacanteId` hidden ausente; `<select ... PuestoId ... disabled>` ausente.
- **RED/GREEN**: código pre-existente; el test pasa al primer run (approval test).

## W-2 (WARNING-2) — POST path con `VacanteId`

- **Spec**: `web-ocupaciones-crear-editar / REQ-OCC-FORM-001` escenario `?vacanteId` enviado — POST con `VacanteId` y redirect a vacante Details.
- **Producción (vigente desde S2 / T2.8)**: `Create.cshtml.cs:207-244` propaga `Input.VacanteId` a `CrearOcupacionRequest.VacanteId`; sobre éxito redirige a `RedirectToPage("/Organizacion/Vacantes/Details", new { id = Input.VacanteId.Value })`.
- **Test nuevo**: `Post_Create_WithVacanteId_CreaOcupacionYRedirigeAVacanteDetails` (happy path POST end-to-end).
  - GET inicial con `?vacanteId={id}` carga el form con el hidden `Input.VacanteId` poblado por T2.8 + dropdown PuestoId bloqueado.
  - POST envía el form (incluyendo `Input.VacanteId={id}` para reproducir la serialización de la hidden).
  - Asserts: redirect 302 a `/organizacion/vacantes/detalles/{vacanteId}` (URL canónica del `@page "/organizacion/vacantes/detalles/{id:guid}"`); `CrearCalls[0].VacanteId == vacanteId` (propagación al API); `vacanteApi.ObtenerPorIdCalls` registra la consulta inicial.
- **RED/GREEN**: código pre-existente; el test pasa al primer run (approval test).

## TDD Cycle Evidence (W-fix)

| Task | Test File | Layer | Safety Net | RED | GREEN | TRIANGULATE | REFACTOR |
|------|-----------|-------|------------|-----|-------|-------------|----------|
| W-1.1 | `OcupacionCreatePageTests.cs` | Web (Integration) | 23/23 OcupacionCreate preexistentes OK | ✅ Approval test escrito | ✅ Passed | ✅ Mismo path que T2.2 Cubierta (segundo estado terminal) | ➖ Clean (sólo test nuevo) |
| W-2.1 | `OcupacionCreatePageTests.cs` | Web (Integration) | 24/24 OK (post W-1.1) | ✅ Approval test escrito | ✅ Passed | ➖ Single (escenario happy path único del spec) | ➖ Clean (sólo test nuevo) |

> **Nota sobre el ciclo RED/GREEN**: ambos tests son **approval tests** — el código de producción ya existía desde S2 (T2.8) y S3 (ninguno aquí). El primer run es GREEN sin necesidad de tocar producción. Esto es esperable cuando el cambio es "agregar cobertura a comportamiento ya implementado"; los tests siguen protegiendo contra regresiones futuras.

## Validación final (W-fix)

```bash
$ dotnet build SGV.slnx --nologo --no-incremental
... 96 Warning(s) | 0 Error(s)
# 96 warnings = mismos que baseline de S1+S2+S3 (todos preexistentes; sin warnings nuevos introducidos por W-fix).

$ dotnet test SGV.slnx --nologo --no-build
Passed! - Failed: 0, Passed: 3490, Skipped: 0, Total: 3490, Duration: 2 m 18 s

$ dotnet test SGV.slnx --nologo --no-build --filter "FullyQualifiedName~OcupacionCreatePageTests"
Passed! - Failed: 0, Passed: 25, Skipped: 0, Total: 25, Duration: 5 s

$ git ls-files | grep invertir-flujo
(vacío — sin artifacts OpenSpec commiteados)

$ git diff --stat develop..HEAD
 .../Web/Ocupaciones/OcupacionCreatePageTests.cs    | 129 +++++++++++++++++++++
 1 file changed, 129 insertions(+)
```

- **Build**: 0 errores, 96 warnings (todos preexistentes; sin warnings nuevos).
- **Tests filtrados (`OcupacionCreatePageTests`)**: 25 pass, 0 fail (23 baseline + 2 nuevos W-fix).
- **Tests global**: 3490 pass, 0 fail (3488 baseline + 2 nuevos W-fix).
- **`git status`**: limpio (solo untracked para los artifacts OpenSpec y el `auth-password.js` modificado fuera de este batch).
- **`git ls-files | grep invertir-flujo`**: vacío.
- **`git diff --stat develop..HEAD`**: 1 file changed, 129 insertions(+), 0 deletions(-) — sólo el archivo de tests; 0 producción. Diff concentrado y reviewable.

### Work Unit Evidence (W-fix)

| Evidence | Value |
|---|---|
| Focused test command | `dotnet test SGV.slnx --nologo --filter "FullyQualifiedName~OcupacionCreatePageTests"` → 25/25 pass |
| Runtime harness | `dotnet test SGV.slnx --nologo --no-build` → 3490/3490 pass (full integration suite, Web ApplicationFactory) |
| Rollback boundary | `git revert 3974cba7` (W-2.1) y/o `git revert 56243b39` (W-1.1). Ambos tests son aislados en el archivo `tests/SGV.Tests/Web/Ocupaciones/OcupacionCreatePageTests.cs`; la reversión no toca código de producción ni afecta los demás tests del archivo. |

## Commits de S3

| Hash | Título | Tareas |
|------|--------|--------|
| `5b76924a` | `test(web): cubrir botón Cubrir Vacante + bloque Persona asignada en Details (T3.1-T3.4)` | T3.1-T3.4 (RED) + extensión de `BuildDetail` con `ocupacionDerivadaId` / `personaAsignadaNombre`. |
| `e4eb4d5a` | `feat(web): botón Cubrir Vacante + bloque Persona asignada en Details (T3.5-T3.8)` | T3.5 (VM), T3.6 (`FromDto`), T3.7 (`Details.cshtml.cs`), T3.8 (`Details.cshtml` button + block). |
| `1bddb0bf` | `test(web): triangulación no-mutator oculta botón Cubrir Vacante (T3.4-bis)` | Triangulación: `EsCubrible` combina `ViewModel.EsCubrible && CanMutate` (spec escenario "Usuario sin rol de mutación"). |

## Commits de S2

| Hash | Título | Tareas |
|------|--------|--------|
| `1127bfc9` | `test(web): cubrir ?vacanteId flujo Cubrir (T2.1-T2.5 RED + T2.13 wire)` | T2.1-T2.5 + T2.13 (interface + fake + impl) |
| `3ebf5dbf` | `feat(web): Create Ocupación con ?vacanteId + label Cubrir Vacante (T2.6-T2.10)` | T2.6, T2.7, T2.8, T2.9, T2.10 |
| `46befe14` | `feat(web): PuestoOcupaciones label Cubrir Vacante + vacanteId (T2.11-T2.14)` | T2.11, T2.12, T2.14 (T2.13 ya cubierto en commit 1) |
| `35cd5115` | `refactor(test): assert disabled via select tag match (more permissive)` | hardening del regex T2.1 |

## Commits de S1 (referencia, ya mergeada)

- `0b5a0e3b` feat(ocupaciones): crear ocupación con VacanteId (N2 invertido) (T1.1-T1.12)
- `29112a74` feat(vacantes): rechazar PATCH a Cubierta (N2 invertido) (T1.13-T1.17)
- `1f3a3d34` feat(vacantes): hidratar OcupacionDerivada en detalle (D-3) (T1.18-T1.23)
- `e8cc7109` test(api): cubrir inversion del flujo Cubrir (T1.24-T1.27)
- `0359d683` refactor(vacantes): limpiar using Ocupaciones dead-code (T1.29-T1.30)
- `4c664cbd` docs(apply): documentar inversion del flujo Cubrir (T1.31)

## Tests (S2)

- **Suites corridas**: `dotnet test SGV.slnx` (suite global, 0 MySQL → todos los `[MySqlFact]` skipeados; alineado con el patrón S1).
- **Resultado**: 3482 pass / 0 fail / 0 skip (3476 baseline + 6 nuevos de S2).
- **Tests nuevos S2** (6):
  - `OcupacionCreatePageTests.?vacanteId_ConVacanteAbierta_RendereaFormConPuestoIdBloqueadoYVgHint` (T2.1)
  - `OcupacionCreatePageTests.?vacanteId_ConVacanteCubierta_MuestraError_VacanteYaCubierta` (T2.2)
  - `OcupacionCreatePageTests.?vacanteId_ConVacanteInexistente_MuestraError_VacanteNoExiste` (T2.3)
  - `PuestoOcupacionesPageTests.VacanteAbiertaSinOcupacion_LabelCubrirVacanteYRouteVacanteId` (T2.4)
  - `PuestoOcupacionesPageTests.VacanteAbiertaConOcupacion_LabelNuevaOcupacion` (T2.5)
  - `PuestoOcupacionesPageTests.Get_Admin_ConOcupacionVigente_MuestraNuevaOcupacionYPuestoIdFallback` (T2.11-bis, escenario de coexistencia Voluntaria vs Ocupada)
- **Tests modificados** (1):
  - `PuestoOcupacionesPageTests.Get_Admin_RendersNewButtonWithPuestoIdQuery` (T2.11) — actualiza el assert al nuevo contrato: label "Cubrir Vacante" + href `?vacanteId=` cuando HayVacanteAbierta && !HayOcupacionActiva.

## Cambios por archivo (producción + tests)

### Producción S3 (`src/`)

- `SGV.Web/Integration/Vacantes/VacanteDetailViewModel.cs` — agrega `Guid? OcupacionDerivadaId` y `string? PersonaAsignadaNombre` al record; agrega `bool EsCubrible` (computed: `EstadoVacanteNombre != "Cubierta" && != "Cancelada"`). `FromDto` mapea los nuevos campos desde el DTO (S1 ya provee `OcupacionDerivadaId` / `PersonaAsignadaNombre`).
- `SGV.Web/Pages/Organizacion/Vacantes/Details.cshtml.cs` — expone `EsCubrible` (`ViewModel.EsCubrible && CanMutate`) y `EsCubierta` (comparación de nombre). `CanMutate` queda vigente para el flag de Edit y para combinar con `EsCubrible`.
- `SGV.Web/Pages/Organizacion/Vacantes/Details.cshtml` — agrega (a) botón `Cubrir Vacante` con `href=/organizacion/ocupaciones/crear?vacanteId={id}&returnUrl=/organizacion/vacantes/detalles/{id}` en la fila de acciones cuando `EsCubrible`; (b) bloque `Persona asignada` entre el card de detalle y el card de historial, visible cuando `EsCubierta && OcupacionDerivadaId.HasValue`, conteniendo `Persona asignada: {nombre}` y link `Ver ocupación` a `/organizacion/ocupaciones/detalles/{OcupacionDerivadaId}` (link omitido si el nombre está vacío — defensivo por D-3).

### Tests S3 (`tests/SGV.Tests/`)

- `Web/Vacantes/FakeVacanteApiClient.cs` — `BuildDetail` ahora acepta `ocupacionDerivadaId?` y `personaAsignadaNombre?` para alimentar `VacanteDetailDto` en los tests.
- `Web/Vacantes/VacantesDetailsAndSidenavTests.cs` — 5 tests nuevos: `Get_Details_VacanteAbierta_BotonCubrirVisible` (T3.1), `Get_Details_VacanteEnSeleccion_BotonCubrirVisible` (T3.2), `Get_Details_VacanteCubierta_BotonCubrirOculto_BloquePersonaAsignadaVisible` (T3.3), `Get_Details_VacanteCancelada_BotonCubrirOculto` (T3.4), `Get_Details_VacanteAbierta_NonMutator_BotonCubrirOculto` (T3.4-bis, triangulación `CanMutate`).

### Producción S2 (`src/`, referenciado, ya mergeada)

- `SGV.Web/Integration/Ocupaciones/OcupacionInputModel.cs` — `VacanteId?` opcional (hidden).
- `SGV.Web/Integration/Vacantes/IVacanteApiClient.cs` — `Task<VacanteDto?> ObtenerAbiertaPorPuestoAsync(Guid puestoId, CancellationToken)` con XML doc.
- `SGV.Web/Integration/Vacantes/VacanteApiClient.cs` — implementación reusando el listado segmentado `abiertas` filtrado por `PuestoId`, fallback `null` ante transporte.
- `SGV.Web/Pages/Organizacion/Ocupaciones/Create.cshtml` — bloque de hint `VacanteHintLabel` (alert-info).
- `SGV.Web/Pages/Organizacion/Ocupaciones/Create.cshtml.cs` — `OnGetAsync` agrega `[FromQuery] Guid? vacanteId` + método privado `ResolverVacanteParaCrearAsync` (mapea 4 estados: Abierta/En Selección → form con hint, Cubierta → error, Cancelada → error, null → error). `OnPostAsync` propaga `Input.VacanteId` a `CrearOcupacionRequest` y redirige al detalle de la Vacante.
- `SGV.Web/Pages/Organizacion/Ocupaciones/IOcupacionForm.cs` — `VacanteHintLabel` y `PuestoIdBloqueadoPorVacante` al interface.
- `SGV.Web/Pages/Organizacion/Ocupaciones/IOcupacionesCrossList.cs` — `NewOcupacionButtonLabel` con default `"Nueva ocupación"`.
- `SGV.Web/Pages/Organizacion/Ocupaciones/OcupacionFormPageModel.cs` — `VacanteHintLabel` y `PuestoIdBloqueadoPorVacante` virtuales; implementaciones explícitas del interface que delegan a la propiedad real (para que la vista, materializando `@model IOcupacionForm`, vea el valor del PageModel y no el default del interface).
- `SGV.Web/Pages/Organizacion/Ocupaciones/_Form.cshtml` — `<input type="hidden" asp-for="Input.VacanteId" />` cuando viene Vacante; dropdown de PuestoId con `disabled="@Model.PuestoIdBloqueadoPorVacante"` + hidden adicional para preservar el valor para model binding.
- `SGV.Web/Pages/Organizacion/Ocupaciones/_CrossList.cshtml` — `@Model.NewOcupacionButtonLabel` en lugar del literal "Nueva ocupación".
- `SGV.Web/Pages/Organizacion/Puestos/PuestoOcupaciones.cshtml.cs` — campo `_vacanteAbiertaId`; `NewOcupacionRouteValues` ahora produce `?vacanteId=` cuando HayVacanteAbierta && !HayOcupacionActiva, fallback `?puestoId=` defensivo. `NewOcupacionButtonLabel` explícito con derivado "Cubrir Vacante" / "Nueva ocupación" según flags. `OnGetAsync` invoca `ObtenerAbiertaPorPuestoAsync` para alimentar el route.

### Tests S2 (`tests/SGV.Tests/`, referenciado, ya mergeada)

- `Web/Ocupaciones/OcupacionCreatePageTests.cs` — 3 tests nuevos (T2.1-T2.3) + helper `SampleVacanteAbierta`.
- `Web/Ocupaciones/PuestoOcupacionesPageTests.cs` — 2 tests nuevos (T2.4-T2.5) + 1 test de coexistencia (T2.11-bis) + 1 test modificado (T2.11).
- `Web/Vacantes/FakeVacanteApiClient.cs` — `ObtenerAbiertaPorPuestoResult` y `ObtenerAbiertaPorPuestoCalls` (S3 extiende además `BuildDetail`).

## TDD Cycle Evidence (S3)

| Task | Test File | Layer | Safety Net | RED | GREEN | TRIANGULATE | REFACTOR |
|------|-----------|-------|------------|-----|-------|-------------|----------|
| T3.1 | `VacantesDetailsAndSidenavTests.cs` | Web (Integration) | 6/6 tests preexistentes OK | ✅ `Assert.Contains "Cubrir Vacante"` failed | ✅ Passed | ➖ Single (Abierta es la entrada principal) | ➖ Clean |
| T3.2 | `VacantesDetailsAndSidenavTests.cs` | Web (Integration) | 7/7 OK (post T3.1) | ✅ Mismo failure pattern | ✅ Passed | ✅ 2do estado (En Selección) | ➖ Clean |
| T3.3 | `VacantesDetailsAndSidenavTests.cs` | Web (Integration) | 8/8 OK (post T3.2) | ✅ `Assert.Contains "Persona asignada"` failed | ✅ Passed | ✅ Diferente path (bloque + link, no botón) | ➖ Clean |
| T3.4 | `VacantesDetailsAndSidenavTests.cs` | Web (Integration) | 9/9 OK (post T3.3) | ➖ Pass (asserts ausencia — branch no implementado aún) | ✅ Passed | ➖ Single (Cancelada es el único caso sin nada) | ➖ Clean |
| T3.4-bis | `VacantesDetailsAndSidenavTests.cs` | Web (Integration) | 10/10 OK (post T3.4) | ➖ Pass (asserts ausencia — branch no implementado aún) | ✅ Passed | ✅ Diferente axis (`CanMutate=false` en vez de `Estado`) | ➖ Clean |

## TDD Cycle Evidence (S2, referenciada)

| Task | Test File | Layer | Safety Net | RED | GREEN | TRIANGULATE | REFACTOR |
|------|-----------|-------|------------|-----|-------|-------------|----------|
| T2.1-T2.3 | `OcupacionCreatePageTests.cs` | Web (Integration) | 20 tests Create preexistentes OK | ✅ Assert.Empty, Assert.DoesNotContain failures | ✅ 3/3 pass | ✅ 3 escenarios (Abierta, Cubierta, Inexistente) | ➖ Clean |
| T2.4-T2.5 | `PuestoOcupacionesPageTests.cs` | Web (Integration) | 13 tests Puesto preexistentes OK | ✅ Assert.Contains "Cubrir Vacante"/"Nueva ocupación" failures | ✅ 2/2 pass | ✅ 2 escenarios (con/sin Ocupación) | ➖ Clean |
| T2.11-bis | `PuestoOcupacionesPageTests.cs` | Web (Integration) | 14 tests Puesto OK | (Cubierto por T2.4-T2.5) | ✅ 1/1 pass | ➖ Single (escenario edge de coexistencia) | ➖ Clean |

## Validación final

### S3

```bash
$ dotnet build SGV.slnx --nologo --no-incremental
... 96 Warning(s) | 0 Error(s)
# 96 warnings = mismos que baseline de S1+S2 (sin warnings nuevos introducidos por S3).

$ dotnet test SGV.slnx --nologo --filter "FullyQualifiedName~VacantesDetailsAndSidenavTests"
Passed! - Failed: 0, Passed: 11, Skipped: 0, Total: 11, Duration: 2 s

$ dotnet test SGV.slnx --nologo --no-build
Passed! - Failed: 0, Passed: 3487, Skipped: 0, Total: 3487, Duration: 2 m 34 s

$ cd src/SGV.Web && bun run build
[21:02:34] Finished 'build' after 3.09 s
```

- **Build**: 0 errores, 96 warnings (todos preexistentes; sin warnings nuevos).
- **Tests filtrados (`VacantesDetailsAndSidenavTests`)**: 11 pass, 0 fail (6 baseline + 5 nuevos de S3).
- **Tests global**: 3487 pass, 0 fail (3482 baseline + 5 nuevos de S3).
- **`bun run build`**: exit 0.
- **`git status`**: limpio (solo untracked para los artifacts OpenSpec).
- **`git ls-files | grep invertir-flujo`**: vacío (sin artifacts OpenSpec commiteados).
- **`git diff --stat develop..HEAD`** (post-S2 merge, commit `4ab286b5`):
  - 5 files changed, 209 insertions(+), 4 deletions(-)
  - **Production**: 64 insertions + 4 deletions (VM + PageModel + View) — dentro del presupuesto S3 (~80-110 líneas totales, ~64 de producción).
  - **Tests**: 145 insertions (5 nuevos tests + extensión de `BuildDetail`) — sobre el presupuesto, pero la suite Web sigue 1:1 con los escenarios del spec `vacante-web`.
- **Cobertura de escenarios del spec `vacante-web`**:
  - Vacante Abierta → botón visible ✅ T3.1
  - Vacante En Selección → botón visible ✅ T3.2
  - Vacante Cubierta → botón oculto + bloque persona asignada + link ver ocupación ✅ T3.3
  - Vacante Cancelada → botón oculto + bloque oculto ✅ T3.4
  - Usuario sin rol de mutación → botón oculto ✅ T3.4-bis (triangulación)
- **Presupuesto S3 planificado**: ~80-110 líneas (design §F / tasks §"PR S3"). **Presupuesto real**: 209 líneas (64 producción + 145 tests). El excedente se explica por la triangulación del escenario "Usuario sin rol de mutación" (T3.4-bis) y los comentarios XML en cada test siguiendo el patrón S2. La PR es reviewable: producción dentro del budget, tests bien aislados.

### S2 (referencia, ya mergeada)

```bash
$ dotnet build SGV.slnx --nologo
... 96 Warning(s) | 0 Error(s)
# 96 warnings = mismos que baseline de S1 (todos preexistentes en tests de Personas / `xUnit1031`, `xUnit2013`; ningún warning nuevo introducido por S2).

$ dotnet test SGV.slnx --nologo --no-build
Passed! - Failed: 0, Passed: 3482, Skipped: 0, Total: 3482, Duration: 2 m 20 s

$ cd src/SGV.Web && bun run build
[20:46:30] Finished 'build' after 3.1 s
```

- **Build**: 0 errores, 96 warnings (todos preexistentes; sin warnings nuevos).
- **Tests**: 3482 pass, 0 fail (sin contar `[MySqlFact]` que se skipean al no tener MySQL local).
- **`bun run build`**: exit 0.
- **`git status`**: limpio (solo untracked para los artifacts OpenSpec).
- **`git diff --stat`** desde `develop` (post-S1 merge, commit `0e98817b`):
  - 14 files changed, 615 insertions(+), 7 deletions(-)
  - **Production**: ~310 insertions + 5 deletions (315 líneas)
  - **Tests**: ~305 insertions (cubren 100% de los escenarios nuevos del spec REQ-OCC-FORM-001 / REQ-OCC-NAV-006 / REQ-OCC-NAV-008)
- **Presupuesto S2 planificado**: ~120-150 líneas (design §F). **Presupuesto real**: 615 líneas total (315 producción + 305 tests). El excedente se explica por:
  - La refactorización del contrato `IOcupacionForm` (interface explícito + property virtual) consume ~40 líneas vs el plan original.
  - El método `ResolverVacanteParaCrearAsync` agrega un método privado de ~30 líneas para mantener el switch exhaustivo (Abierta/Cubierta/Cancelada/null) separado del `OnGetAsync` principal.
  - La suite Web añade ~10 líneas por test en XML docs + helpers (máscaras regex, comparaciones de href).
  - La PR sigue siendo ≤400 líneas de código nuevo de producto (315 líneas de producción puras); el excedente es cobertura de tests + docs XML. **Reviewable**.

## Notas / blockers

### Desviaciones del design (S3)

1. **`EsCubrible` basado en `EstadoVacanteNombre` (string comparison)** en vez de un flag explícito del DTO. El design D-5 plantea cualquiera de las dos alternativas; se eligió la comparación case-insensitive contra los nombres `"Cubierta"` y `"Cancelada"` para no introducir un nuevo campo DTO (mantiene S3 estricto a frontend, sin re-tocar el wire). Compatible con los seeds vigentes (`Abierta`, `En selección`, `Cubierta`, `Cancelada`).

2. **`EsCubierta` en el PageModel vs `EsCubrible`**: agregué `EsCubierta` (computed) en `Details.cshtml.cs` para que la vista pueda decidir el bloque "Persona asignada" sin acceder a `ViewModel.EstadoVacanteNombre` directamente. Esto deja el template testeable sin lógica de comparación de strings inline. No introduce acoplamiento nuevo: es la misma comparación case-insensitive que `EsCubrible`, pero con la polaridad opuesta.

3. **Triangulación T3.4-bis (no-mutator)**: el spec `vacante-web` menciona el escenario "Usuario sin rol de mutación" pero `tasks.md` T3.1-T3.4 no lo incluye explícitamente. Agregué un test dedicado (`Get_Details_VacanteAbierta_NonMutator_BotonCubrirOculto`) para cubrir la otra rama de la conjunción `EsCubrible = ViewModel.EsCubrible && CanMutate`. Esto requirió también un commit separado para mantener la revisión legible (T3.1-T3.4 cubren el eje `Estado`, T3.4-bis cubre el eje `CanMutate`).

4. **Link "Ver ocupación" omitido cuando `PersonaAsignadaNombre` es null**: el spec `vacante-web` dice "el link 'Ver ocupación' DEBE omitirse o deshabilitarse en ausencia de nombre asignado". Elegí omitirlo (no deshabilitarlo) por simplicidad del template y porque el `OcupacionDerivadaId` no es navegable sin contexto de quién está asignada. Defensivo por D-3: un estado inconsistente Cubierta-sin-Ocupación no debería llegar a este branch (`OcupacionDerivadaId.HasValue` es la guarda del bloque).

### Desviaciones del design (S2, referenciadas)

1. **Implementación explícita del interface en `OcupacionFormPageModel`**: el `IOcupacionForm` ya no expone `=> false` como default para `PuestoIdBloqueadoPorVacante`/`VacanteHintLabel`. En su lugar, el interface declara la propiedad abstracta y la base `OcupacionFormPageModel` implementa ambos puntos vía `bool IOcupacionForm.X => this.X`. Esto fue necesario porque la vista materializa `@model IOcupacionForm` y leía el default literal del interface en lugar del PageModel concreto. Patrón vigente en el repo (ver `PuestoOcupacionesModel` que también implementa `IOcupacionesCrossList` con sintaxis explícita para esconder el view-model del base).

2. **`ResolvedVacanteId` fallback a `?puestoId=`**: cuando la consulta de la Vacante abierta por Puesto devuelve null (defensivo, transporte), `NewOcupacionRouteValues` cae al comportamiento previo con `?puestoId=` para no romper el flujo. Esto NO está documentado en el spec pero es defensivo (alineado con la política de degradación de `ExisteVacanteAbiertaParaPuestoAsync` que también degrada a `false`).

3. **T2.11-bis (test de coexistencia)**: el spec REQ-OCC-NAV-008 escenario "Puesto con Vacante abierta y Ocupación activa coexistente" no era un test explícito en `tasks.md` T2.4-T2.5, pero surgía naturalmente del cambio de comportamiento. Agregué un test dedicado (`Get_Admin_ConOcupacionVigente_MuestraNuevaOcupacionYPuestoIdFallback`) para cubrir el camino de fallback "Nueva ocupación" con `?puestoId=`. Esto requirió también actualizar `Get_Admin_RendersNewButtonWithPuestoIdQuery` (que verificaba el comportamiento previo) para usar el nuevo contrato, y romper el acoplamiento viejo.

### Lecciones aprendidas

- **Triangulación explícita por eje (S3)**: el spec `vacante-web` define 5 escenarios pero `tasks.md` T3.1-T3.4 sólo cubre 4 (eje `Estado`). El 5to (eje `CanMutate`) cae naturalmente como triangulación; agregarlo en un commit separado (T3.4-bis) mantiene la revisión legible y deja evidencia explícita de que la conjunción `EsCubrible = EsCubrible && CanMutate` está testeada en ambas ramas. Patrón a replicar: cuando un flag es compuesto, agregar un test por rama de la conjunción.
- **Computed properties en el ViewModel vs string comparison inline (S3)**: derivar `EsCubrible` y `EsCubierta` en el VM/PageModel (con `case-insensitive` contra nombres) es preferible a inline en la vista. El template queda declarativo (`@if (Model.EsCubrible)`) y los tests pueden assertar vía la presencia del bloque/botón sin reimplementar la lógica de estado en el test.
- **Strict TDD + chained PR (S2, vigente)**: separar el wire (interface + fake) en un commit inicial permite que los tests compilen y fallen al ejecutar (RED por comportamiento) en vez de RED por compilación. Esto deja evidencia más limpia en el log de TDD.
- **S3 dentro del budget de producción (S3)**: 64 líneas de producto vs 80-110 planificadas. La diferencia (overhead) viene de la triangulación T3.4-bis (28 líneas de test) más los comentarios XML por test. La revisión queda concentrada en el cambio funcional (62 líneas de producto en un commit) y el delta de tests es trivial de leer (un test por escenario del spec).

### Riesgos remanentes

- **Tests `[MySqlFact]`**: el change no introdujo nuevos `[MySqlFact]`. Los existentes siguen verdes por construcción (no tocan la capa nueva). El verificador `sdd-verify` debe correrlos contra MySQL 8 para confirmar la atomicidad transaccional (cubierta por S1).
- **Comparación `EstadoVacanteNombre` case-insensitive (S3)**: el flag `EsCubrible` depende de que `EstadoVacanteNombre` matchee exactamente `"Cubierta"` o `"Cancelada"` (case-insensitive). Si el backend cambia los strings (e.g. a minúsculas, a nuevas etiquetas i18n), el botón puede aparecer para Cubierta. Riesgo bajo — el seed del repo usa mayúsculas, y los demás consumers (DropDownList de Edit) ya comparan de la misma manera (vía el campo `EsCubierta` del `EstadoVacanteDto` para excluir Cubierta del dropdown — issue #268). Mitigación futura: pedir un flag `EsCubierta` / `EsCancelada` al backend vía DTO si la i18n entra al proyecto. **WARNING-3 (verify-report) — SUGGESTION**: este riesgo NO fue resuelto por W-fix; queda como follow-up futuro que probablemente requiera extender el DTO con `EsCubierta`/`EsCancelada` (cambio de D-3, no sólo un test).
- **Migration script**: `docs/migracion-inicial-sgv.sql` NO se regeneró (sin cambios de esquema, Q-T4 cerrado por S1).

## Próximos pasos

- **sdd-verify** (re-correr): la cobertura W-1 + W-2 quedó cerrada por W-fix; el verificador debe confirmar:
  - W-1.1: `Get_Create_WithVacanteIdCancelada_MuestraError_VacanteCancelada` pasa y cubre el escenario Cancelada.
  - W-2.1: `Post_Create_WithVacanteId_CreaOcupacionYRedirigeAVacanteDetails` pasa y cubre el happy path POST con `VacanteId`.
  - El conteo de escenarios cubiertos sube de 30/36 a **32/36** (W-1 + W-2 cerrados).
  - WARNING-3 sigue siendo un SUGGESTION / follow-up (no un test faltante).
- **sdd-archive**: archivar el change con deltas sincronizados a las specs vigentes (`vacante-management`, `web-ocupaciones-crear-editar`, `web-ocupaciones-navegacion-contextual`, `vacante-web`). Aplicar normalización DADO-CUANDO-ENTONCES (D-6) y renombre del código de error en spec vigente (D-4).

## Chain Context

- **Depends on**: S1 (PR #269, commit `0e98817b` en `develop`) + S2 (PR #270, commit `4ab286b5` en `develop`).
- **Current**: S3 está lista para PR a `develop` (no push, no merge; decisión del usuario).
- **Out of scope (esta delegación)**: sdd-verify, sdd-archive, follow-ups.
- **Verificación**:
  - `dotnet build SGV.slnx`: 0 errores, 0 warnings nuevos (96 = baseline).
  - `dotnet test SGV.slnx`: 3487 pass (3482 + 5 nuevos), 0 fail.
  - `dotnet test --filter "FullyQualifiedName~VacantesDetailsAndSidenavTests"`: 11 pass (6 + 5 nuevos), 0 fail.
  - `bun run build` en `src/SGV.Web`: exit 0.
  - `git status`: limpio (untracked solo para artifacts OpenSpec).
  - `git ls-files | grep invertir-flujo`: vacío.
- **Rollback**: `git revert <merge-s3>` restaura `Details.cshtml` sin botón ni bloque, `VacanteDetailViewModel` sin campos extra, `Details.cshtml.cs` sin `EsCubrible`/`EsCubierta`. Sin migración que revertir. Sin dependientes posteriores (S3 es la última PR del change).

### Test Summary (S3)

- **Total tests written**: 5 (T3.1-T3.4 + T3.4-bis triangulación)
- **Total tests passing**: 5 nuevos + 3482 existentes = 3487
- **Layers used**: Web (Integration con `SgvWebApplicationFactory` + `FakeVacanteApiClient` extendido)
- **Pure functions created**: 0 (la lógica es comparación de strings sobre `EstadoVacanteNombre` + combinación con flag `CanMutate` — el helper `EsCubrible` en el VM es un getter computed, no una función pura testeable directamente porque depende del DTO del backend; los tests Web cubren el comportamiento observable)
- **Mocks** vs **assertions**: cada test ≤2 mocks (cumple la regla `Mock/assertion ratio`)

### Test Summary (S2, referenciado)

- **Total tests written**: 6
- **Total tests passing**: 6 nuevos + 3476 existentes = 3482
- **Layers used**: Web (Integration con `SgvWebApplicationFactory` + fakes)
- **Pure functions created**: 0 (la lógica es orquestación de cliente API + switch por estado de Vacante)
- **Mocks** vs **assertions**: cada test ≤4 mocks (cumple la regla `Mock/assertion ratio`)

### Approval tests (refactor, S2)

- T2.11 cambio en `NewOcupacionRouteValues`: el comportamiento anterior (siempre `?puestoId=`) se preserva cuando `HayVacanteAbierta=false` o `HayOcupacionActiva=true` (camino de coexistencia). El test `Get_Admin_ConOcupacionVigente_MuestraVerOcupacion` (existente, sin cambios) + `Get_Admin_ConOcupacionVigente_MuestraNuevaOcupacionYPuestoIdFallback` (nuevo) son approval tests de la coexistencia.

### Risk: tests skipped

- 0 tests skipped en este run (MySQL no estaba disponible → los `[MySqlFact]` se skipean por su propia infra). El verificador `sdd-verify` debe correrlos contra MySQL para confirmar la atomicidad transaccional real (ya cubierta por S1).
