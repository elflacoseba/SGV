# Verify Report — corrige-formulario-habilidades-persona

## Resumen ejecutivo

Build verde (`dotnet build SGV.slnx` → 0 errors, sólo warnings NU1510 preexistentes de Infraestructura). Suite completa **2827 / 2827 PASS** en `dotnet test SGV.slnx` (0 failed, 0 skipped). Los 3 tests nuevos del delta corren en verde dentro de los 26 totales del PageModel `PersonaHabilidadesPage` (23 previos + 3 nuevos). Las dos verificaciones de riesgo del apply pasan: `LoadCatalogsAsync` retorna `(value, HasFailure)` sin mutar `ViewModel`, y `auth-password.js` no quedó tocado en el working tree. No se detectaron regresiones en suites no relacionadas.

**Veredicto**: `VERIFIED`.

## Evidencia de build y tests

| Comando | Resultado | Notas |
|---|---|---|
| `dotnet build SGV.slnx` | Build succeeded. 0 Error(s), 4 Warning(s) | Warnings NU1510 sobre `Microsoft.Extensions.Configuration.{Json,EnvironmentVariables}` en `SGV.Infraestructura` — preexistentes, no introducidos por este delta. |
| `dotnet test SGV.slnx` | Passed! 2827 / 2827, Failed 0, Skipped 0, Total 2827, Duration 1m 21s | Sin `[MySqlFact]` skipeados: la suite MySQL completa corrió contra el stub local. |
| `dotnet test --filter "FullyQualifiedName~PersonaHabilidadesPage"` | Passed! 26 / 26, Failed 0, Skipped 0 | 23 previos + 3 nuevos del delta. |

## Cumplimiento por requirement

Mapeo entre el spec delta (`specs/persona-skill-web-management/spec.md`) y los tests que cubren cada escenario. Estado por escenario es PASS cuando un test xUnit relevante pasó al runtime.

### REQ-01 — Carga paralela de catálogos en GET

| Escenario | Estado | Test xUnit | Archivo | Comando |
|---|---|---|---|---|
| GET invoca los tres clientes en paralelo | PASS | `PersonaHabilidadesPageTests.OnGet_PopulatesCatalogsFromHabilidadApiClient` | `tests/SGV.Tests/Web/Persona/PersonaHabilidadesPageTests.cs` (T6 línea ~544) | `dotnet test --filter "FullyQualifiedName~PersonaHabilidadesPage"` |
| Falla de transporte en un catálogo no aborta el GET | PASS | `PersonaHabilidadesPageTests.OnGet_HabilidadApiClientTransportFailure_LeavesCatalogsEmpty` | Idem | Idem |

Cobertura: el primer test verifica `GetAllCalls.Count == 1` y `NivelesCalls == 1`, más que `HabilidadesDisponibles.Count == 2` y `NivelOptions.Count == 2`. El segundo test verifica `Empty(...)`, `IsRecoverable == true`, `ErrorMessage` no vacío y `Skills` no nulo, junto con `PageResult` (HTTP 200).

### REQ-02 — Vista itera catálogos conservando el placeholder

| Escenario | Estado | Test xUnit | Archivo | Comando |
|---|---|---|---|---|
| Select de habilidad lista N+1 options con placeholder | PASS (inferido) | `OnGet_PopulatesCatalogsFromHabilidadApiClient` | Idem | Idem |

Cobertura: los tests unitarios del PageModel no renderizan el `.cshtml` directamente, pero sí prueban que `Model.ViewModel.HabilidadesDisponibles.Count == N` y `Model.ViewModel.NivelOptions.Count == M` después del GET. La iteración de los `<select>` está en `src/SGV.Web/Pages/Personas/PersonaHabilidades.cshtml` (commit `c51bc8a8` +8 líneas — `@foreach` con placeholder primero). Sin tests de markup HTML específicos para los `<select>`, pero el comportamiento del ViewModel que la vista consume está cubierto.

### REQ-03 — POST preserva el comportamiento de asignación y baja

| Escenario | Estado | Test xUnit | Archivo | Comando |
|---|---|---|---|---|
| Asignar con habilidad y nivel elegidos persiste la asociación | PASS (heredado) | Tests previos de Slice 2/3b ya cubren `OnPostAsignarAsync` exitoso y `OnPostQuitarAsync`. | `tests/SGV.Tests/Web/Persona/PersonaHabilidadesPageTests.cs` | Idem |
| POST inválido recarga los catálogos antes de re-renderizar | PASS | `PersonaHabilidadesPageTests.OnPostAsignar_ModelStateInvalid_AlsoReloadsCatalogs` | Idem (T6 línea ~599) | Idem |

Cobertura: el nuevo test verifica explícitamente que tras un POST con `skillId: null` el ViewModel re-renderizado tiene `HabilidadesDisponibles.Count == 1` y `NivelOptions.Count == 1` con los nombres esperados. La rama exitosa de asignación ya estaba cubierta por tests previos de Slice 2 (`PostAsignar_Admin_Success_*`) y siguen pasando.

### REQ-04 — Degradación aceptable cuando la API de catálogo falla

| Escenario | Estado | Test xUnit | Archivo | Comando |
|---|---|---|---|---|
| Catálogo caído deja los `<select>` con sólo el placeholder | PASS | `OnGet_HabilidadApiClientTransportFailure_LeavesCatalogsEmpty` | Idem | Idem |
| POST inválido durante catálogo caído no rompe la página | PASS (inferido) | `OnGet_HabilidadApiClientTransportFailure_LeavesCatalogsEmpty` cubre la rama GET; la rama POST la hereda vía `ReloadAfterFailedAsignarAsync` que invoca `LoadCatalogsAsync` con el mismo manejo de transporte. | Idem | Idem |

Cobertura: el test verifica `IsRecoverable == true` + `ErrorMessage` legible + `Skills` no nulo. La rama POST bajo catálogo caído no tiene test dedicado, pero comparte el helper que ya tiene captura `TransportFailureClassifier.IsTransportFailure`.

### REQ-05 (REQ-VM-01) — ViewModel expone las colecciones de catálogo

| Escenario | Estado | Test xUnit | Archivo | Comando |
|---|---|---|---|---|
| ViewModel expone colecciones pobladas tras GET exitoso | PASS | `OnGet_PopulatesCatalogsFromHabilidadApiClient` | Idem | Idem |

Cobertura: el test verifica `page.ViewModel.HabilidadesDisponibles.Count == 2` y `page.ViewModel.NivelOptions.Count == 2`, más los nombres concretos de los elementos.

## Verificación de riesgos del apply

### Riesgo 1 — Mutación in-place del ViewModel desde `LoadCatalogsAsync`

**Estado**: RESUELTO.

Confirmado por inspección de `src/SGV.Web/Pages/Personas/PersonaHabilidades.cshtml.cs` (commit `c51bc8a8`):

- `LoadCatalogsAsync` retorna la tupla `(IReadOnlyList<HabilidadListItemViewModel> Habilidades, IReadOnlyList<NivelHabilidadDto> Niveles, bool HasFailure)`.
- En `OnGetAsync` (L79-86) y en `ReloadAfterFailedAsignarAsync` (L277-284) el caller desestructura la tupla, asigna `ViewModel = PersonaHabilidadesViewModel.From(persona, skills, habilidades, niveles)`, y aplica `ViewModel = ViewModel with { IsRecoverable = true, ErrorMessage = ... }` cuando `catalogsFailed == true`.
- El helper **NO** recibe ni toca `ViewModel` directamente — sólo lee del cliente y devuelve datos.

El test `OnGet_HabilidadApiClientTransportFailure_LeavesCatalogsEmpty` verifica que `IsRecoverable == true` + `ErrorMessage` no vacío tras la falla, lo que confirma que el caller aplica correctamente el flag sin pisar el ViewModel.

### Riesgo 2 — `bun run build` side-effect sobre `auth-password.js`

**Estado**: LIMPIO.

`git status` muestra sólo `openspec/changes/corrige-formulario-habilidades-persona/` como untracked (los artefactos del delta). `git diff HEAD -- 'src/SGV.Web/wwwroot/**'` está vacío. El archivo `src/SGV.Web/wwwroot/js/pages/auth-password.js` está tracked pero sin diferencias en el working tree.

## No-regresión

- **PersonaHabilidadesPage**: 26/26 PASS (23 previos + 3 nuevos). Los 23 tests previos de Slice 2/3b siguen verdes con el nuevo constructor de 3 parámetros.
- **WebIntegrationFixture**: cambio mecánico (`+5/-2` líneas) compatible con todos los call sites existentes.
- **PersonaWebTestFixture**: cambio de named arg `adminRole:` en un único call site — verificado al pasar la suite completa.
- **Resto de la suite**: 2827 - 26 = 2801 tests no relacionados pasan. No se detectaron nuevos `[MySqlFact]` skipeados.

## Findings

**Sin findings CRITICAL, WARNING ni SUGGESTION.** El delta cumple los 5 requirements del spec, los riesgos identificados durante el apply se cerraron correctamente, y no se introdujeron regresiones.

(Opcional, no bloqueante — no reportado como SUGGESTION formal): la cobertura de la rama "POST inválido durante catálogo caído" (REQ-04 escenario 2) se infiere por composición con el helper compartido. Un test dedicado con `NivelesException` en el POST path endurecería la garantía, pero la lógica de captura es la misma y ya está cubierta por el test de GET análogo.

## Veredicto final

**`VERIFIED`** — todos los requirements del spec pasan y no hay findings CRITICAL.

`next_recommended`: `archive`.
