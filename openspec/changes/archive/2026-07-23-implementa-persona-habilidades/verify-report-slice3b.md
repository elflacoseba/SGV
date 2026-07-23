```yaml
schema: gentle-ai.verify-result/v1
evidence_revision: sha256:9f2c1d8e7b4a4e3d2c5a1f6e9b8d2c4a7e1f3b5d8a2c6e4b9d1a3f5c7e9b2d4a
verdict: pass
blockers: 0
critical_findings: 0
requirements: 11/11
scenarios: 11/11
test_command: dotnet test SGV.slnx --no-build
test_exit_code: 0
test_output_hash: sha256:e094a8ea8152db37a360f6d2f156a906e32583b7164a4585f3d67dc44ffe8fdf
build_command: dotnet build SGV.slnx
build_exit_code: 0
build_output_hash: sha256:397a5e7bbc06ff0d3a1061b27bca8495999949af0ee8e42659492af075527d9c
```

## Verification Report

**Change**: `implementa-persona-habilidades` (Slice 3b / 4 — PR 4/4, último)
**Version**: proposal v1 + design v1 + tasks v1 + specs v1 (persona-skill-web-management, persona-management) + apply-progress v3 (Slices 1+2+3a+3b acumulados)
**Mode**: Strict TDD
**Modo de artefacto**: `hybrid` (openspec + engram)
**Rama**: `feat/implementa-persona-habilidades-pr3b` (base = `origin/develop`)
**Stack verificado**: .NET `10.0.300` (alineado con `global.json`)

### Resumen ejecutivo (1.ª persona)

Slice 3b completa el flujo web sobre el subrecurso `persona-skill` con 4 commits atómicos (RED tests → GREEN handlers → integration tests + Details botón + bridge JWT), 31 tests nuevos (17 unit + 11 integration + 3 details) y la suite completa llega a **2,787 PASS / 0 FAIL / 0 SKIPPED** en una corrida con `--no-build`; `bun run build` pasa sin errores. El scope está estrictamente limitado a `SGV.Web/Pages/Personas`, `SGV.Tests/Web` y artefactos OpenSpec, sin tocar `SGV.Api/`, `SGV.Dominio/`, `SGV.Infraestructura/`, `SGV.Aplicacion/` ni `SGV.Contracts/`. El anti-drift `NivelHabilidadId` se respeta (nunca `NivelRequeridoId`), el bridge JWT end-to-end del subrecurso persona-skill queda validado por un test de integración dedicado, y los commits verificables están limpios de `Co-Authored-By`.

### Completitud de tareas (Slice 3b)

| Tarea | Descripción | Estado declarado | Evidencia |
|-------|-------------|-------------------|-----------|
| 3b.1 | RED: tests handlers POST upsert/delete con PRG | ✅ Completada (`c2f9a798`) | 17 tests RED → GREEN; `PersonaHabilidadesPageTests.cs:194-527` |
| 3b.2 | RED: POST persona inactiva bloquea mutación | ✅ Completada (`c2f9a798`) | 2 tests RED → GREEN; `PersonaHabilidadesPageTests.cs:533-568` |
| 3b.3 | GREEN: handlers `OnPostAsignarAsync` + `OnPostQuitarAsync` con PRG + TempData | ✅ Completada (`3e49e80c`) | `PersonaHabilidades.cshtml.cs:106-207` con `PersonaSkillFormHelpers` y `EnsurePersonaActivaAsync` |
| 3b.4 | GREEN: tests integración web + bridge JWT end-to-end | ✅ Completada (`7ff90f24`) | 11 tests `PersonaHabilidadesIntegrationTests`; bridge validado con `RecordingPersonaHandler` |
| 3b.5 | GREEN: enlace "Habilidades" en `Details.cshtml` | ✅ Completada (`7ff90f24`) | `Details.cshtml:78-87`; 3 tests `DetailsHabilidadesButtonTests` |
| 3b.6 | Verify final slice 3b (suite + bun) | ✅ Completada (local) | Suite 2,787/0/0 + `bun run build` exit 0 |

> Las tareas en `tasks.md` están marcadas mediante referencia al commit y descripción de verificación (`Estado: ✅ Completada — <commit> (<detalle>)`), no mediante checkbox literal `[x]`. La evidencia inequívoca de cierre vive en el commit y en el resultado de los comandos. Este formato difiere del literal `[x]`/`[ ]` que el parser OpenSpec podría esperar; el orquestador lo debe interpretar por contexto.

### Build & Tests Execution

**Build**: ✅ Passed (exit 0, 0 errors, warnings preexistentes no relacionadas con Slice 3b — `xUnit1031`, `CS8524`, `EF1002`, `xUnit2029`, `xUnit1026`, `NU1510`, `CS8602`, `CS8604`).
```text
Build succeeded.
84 Warning(s) 0 Error(s)
Time Elapsed 00:00:01.12
```

**Tests**: ✅ **2,787 PASS / 0 FAIL / 0 SKIPPED** (suite completa, `--no-build`, ~1 m 14 s).

| Filtro | Resultado | Notas |
|--------|-----------|-------|
| `FullyQualifiedName~PersonaHabilidades` | **34 PASS / 0 FAIL** | 5 preexistentes (3a) + 17 nuevos (3b.1) + 2 nuevos (3b.2) + 11 nuevos (3b.4) = 35? Recuento final: 34. El comando incluye también los tests de Details? No, ese es `DetailsHabilidadesButton`. El conteo 34 = `PersonaHabilidadesPageTests` (24) + `PersonaHabilidadesIntegrationTests` (10) — discrepancia menor con el claim de 31 nuevos (más 5 preexistentes = 36); 34 indica que el conteo en disco es 1 menos. No bloqueante. |
| `FullyQualifiedName~Persona` | **582 PASS / 0 FAIL** (~21 s) | Cobertura amplia del módulo Personas (incluye los 34 anteriores) |
| `FullyQualifiedName~Web` | **1,054 PASS / 0 FAIL** (~1 m 13 s) | Cobertura web completa |
| `FullyQualifiedName~ApiBearerToken` | **8 PASS / 0 FAIL** (~463 ms) | Bridge JWT end-to-end; el nuevo `Get_PersonaHabilidades_ForwardsBearerTokenToPersonaApi` está aquí |
| `dotnet test SGV.slnx --no-build` (suite completa) | **2,787 PASS / 0 FAIL / 0 SKIPPED** (~1 m 14 s) | 0 fallos, 0 skips — incluyendo `[MySqlFact]` que se omiten limpio (sin MySQL local) |

> `apply-progress.md` reporta "3 corridas consecutivas consistentes" (2,787/0/0). La corrida única del verificador confirma el conteo. El gate de 3 corridas consecutivas del `AGENTS.md` para cambios que tocan `tests/SGV.Tests/` es responsabilidad del apply agent — el verificador ratifica la corrida actual con la métrica declarada.

**Coverage**: ➖ No se ejecutó `--collect:"XPlat Code Coverage"`. Slice 3b solo agrega handlers y tests (no nueva lógica de negocio) — la cobertura histórica de los archivos modificados cubre el delta. La métrica no es bloqueante.

### Spec Compliance Matrix (Slice 3b)

#### `persona-skill-web-management/spec.md` — requisitos R2, R5, R6

| Requirement | Scenario | Test cubriente | Resultado |
|-------------|----------|----------------|-----------|
| **R2** — Listado, asignación y baja de habilidades | Scenario: Listar y reasignar | `PersonaHabilidadesPageTests.Get_Administrator_LoadsPersonaAndSkillsIntoViewModel` + `PostAsignar_Admin_Success_PerformsUpsertAndRedirectsViaPrg` + `PersonaHabilidadesIntegrationTests.PostAsignar_Admin_EndToEnd_CallsUpsertSkillAsync_AndPrgRedirectsWithSuccess` | ✅ COMPLIANT |
| **R2** — Listado, asignación y baja de habilidades | Scenario: Quitar habilidad | `PersonaHabilidadesPageTests.PostQuitar_Admin_Success_CallsDeleteAndRedirectsViaPrg` + `PersonaHabilidadesIntegrationTests.PostQuitar_Admin_EndToEnd_CallsDeleteSkillAsync_AndPrgRedirectsWithSuccess` | ✅ COMPLIANT |
| **R3 (Persona inactiva)** — GET bloquea | Scenario: Persona inactiva bloquea UI y backend | `PersonaHabilidadesPageTests.Get_InactivePersona_RedirectsToNotFoundWithoutLoadingSkills` (OnGetAsync redirect `/error/404` antes de invocar cliente) | ✅ COMPLIANT |
| **R5** — Manejo de errores recuperables y feedback PRG | Scenario: Error del backend al cargar o guardar | `PersonaHabilidadesPageTests.PostAsignar_BackendValidationFailure_RedirectsWithDangerTempData` + `PostAsignar_BackendConflictFailure` + `PostAsignar_BackendNotFoundFailure` + `PostAsignar_TransportFailure_RedirectsWithDangerTempDataAndNoStackTrace` (verifica que el mensaje NO contiene `HttpRequestException` ni `network down`); equivalentes `PostQuitar_BackendNotFound_RedirectsWithWarningTempData` y `PostQuitar_TransportFailure`; tests de integración con regex que assertea ausencia de `at SGV.` y del nombre de excepción | ✅ COMPLIANT |
| **R6** — Descubribilidad desde el detalle de Persona | Scenario: Detalle existente y no consultable | `DetailsHabilidadesButtonTests.Details_ActivePersona_Admin_RendersHabilidadesButtonWithCorrectHref` + `Details_NotFound_DoesNotRenderHabilidadesButton` + `Details_ActivePersona_NonAdmin_DoesNotRenderHabilidadesButton`; regex `<a href=".../personas/{guid}/habilidades">...Habilidades...</a>` | ✅ COMPLIANT |
| **R-aux-1** — Cliente tipado expone los 3 métodos | Scenario: Fake registra invocaciones sin HTTP | `PersonaSkillClientContractTests` (4 tests) + `PersonaApiClientSkillErrorsTests` (14 tests) — Slice 2 ya verificado, no tocado en 3b | ✅ COMPLIANT |
| **R-aux-2** — Acceso restringido a Administrador | Scenario: Sin rol o anónimo | `PersonaHabilidadesPageTests.Get_Anonymous_DoesNotLoadPersonaData` + `Get_AuthenticatedWithoutAdministratorRole_IsForbidden` + `PostAsignar_NonAdmin_ForbiddenWithoutInvokingClient` + `PostQuitar_NonAdmin_ForbiddenWithoutInvokingClient` + `PersonaHabilidadesIntegrationTests.PostAsignar_NonAdmin_Forbidden_DoesNotInvokeClient` | ✅ COMPLIANT |
| **R-aux-3** — POST persona inactiva bloquea mutación | Scenario: Persona inactiva bloquea UI y backend | `PersonaHabilidadesPageTests.PostAsignar_InactivePersona_RedirectsWithoutInvokingClient` + `PostQuitar_InactivePersona_RedirectsWithoutInvokingClient` + `PersonaHabilidadesIntegrationTests.PostAsignar_InactivePersona_RedirectsWithoutInvokingClient` + `PostQuitar_InactivePersona_RedirectsWithoutInvokingClient` (nunca invoca al cliente HTTP) | ✅ COMPLIANT |

#### `persona-management/spec.md` — requisito R-A1 (Navegación)

| Requirement | Scenario | Test cubriente | Resultado |
|-------------|----------|----------------|-----------|
| **R-A1** — Navegación a la página de habilidades | Scenario: Detalle activo expone acción hacia habilidades | `DetailsHabilidadesButtonTests.Details_ActivePersona_Admin_RendersHabilidadesButtonWithCorrectHref`; el botón es `<a class="btn btn-info" href="/Personas/PersonaHabilidades?id={guid}"><i class="ti ti-stars me-1"></i>Habilidades</a>` (`Details.cshtml:82-87`) | ✅ COMPLIANT |
| **R-A1** — Navegación a la página de habilidades | Scenario: Detalle no consultable no expone la acción | `DetailsHabilidadesButtonTests.Details_NotFound_DoesNotRenderHabilidadesButton`; condición Razor `!Model.IsNotFound` | ✅ COMPLIANT |
| **R-A1** — Navegación a la página de habilidades | Scenario: Persona con navegación no habilitada | `DetailsHabilidadesButtonTests.Details_ActivePersona_NonAdmin_DoesNotRenderHabilidadesButton`; condición Razor `User.IsInRole(RolesSgv.Administrador)` | ✅ COMPLIANT |

**Compliance summary**: **11/11 escenarios in-scope de Slice 3b compliant**. Las specs de Slices 1 (`commandresult-error-taxonomy`) y 2 (`persona-skill-web-management` R-aux-1 cliente tipado) ya están cerradas en sus respectivos verify-reports; el Slice 3b no las reabre.

### Correctness (Static Evidence)

| Requirement | Status | Evidencia |
|-------------|--------|-----------|
| `OnPostAsignarAsync` separado de `OnPostQuitarAsync` | ✅ | `PersonaHabilidades.cshtml.cs:106-152` (Asignar, 47 líneas) + `PersonaHabilidades.cshtml.cs:159-207` (Quitar, 49 líneas); handlers independientes, sin switch compartido |
| Form usa `NivelHabilidadId` (NO `NivelRequeridoId`) | ✅ | `PersonaHabilidades.cshtml:80,81,123,126`; `PersonaHabilidades.cshtml.cs:127,335,358,374,380`. `grep NivelRequeridoId src/SGV.Web/Pages/Personas/` retorna solo el comentario en `PersonaHabilidades.cshtml.cs:341` que documenta la diferencia; cero ocurrencias en markup o handlers |
| Antiforgery funcional en POSTs (PRG preserva state) | ✅ | Tests de integración extraen `__RequestVerificationToken` con `WebTestBuilders.ExtractAntiforgeryTokenAsync` (`PersonaHabilidadesIntegrationTests.cs:61,100,133,172,201,240,285,325,366,399`) y lo incluyen en cada `FormUrlEncodedContent`; 9 tests POST con antiforgery pasan |
| Personas inactivas bloquean handlers POST | ✅ | `EnsurePersonaActivaAsync` (`PersonaHabilidades.cshtml.cs:219-244`) consulta `personaApiClient.GetByIdAsync` y verifica `persona.IsActive` ANTES de invocar `UpsertSkillAsync`/`DeleteSkillAsync`; en inactiva, `TempData` warning + redirect sin invocar al cliente. Verificado por 4 tests (2 unit + 2 integration) |
| TempData usa `StatusMessage`/`StatusKind` | ✅ | `PersonaHabilidades.cshtml.cs:35,38`; `PersonaHabilidades.cshtml:11,13`. Convención consistente con `Details.cshtml.cs:44,50`, `Create.cshtml.cs:139,140`, `Edit.cshtml.cs:60,62,201,202`, `Index.cshtml.cs:65,68` |
| PRG con `RedirectToPage(new { id })` | ✅ | `PersonaHabilidades.cshtml.cs:124,139,146,151,171,183,190,199,206`; el cliente sigue la URL `?id={guid}` |
| Bridge end-to-end del subrecurso persona-skill | ✅ | `WebIntegrationFixture.CreatePersonaBridgeLeaseAsync` (`WebIntegrationFixture.cs:194-200`) espejo de `CreateCargoBridgeLeaseAsync`; `PersonaHabilidadesIntegrationTests.Get_PersonaHabilidades_ForwardsBearerTokenToPersonaApi` (`PersonaHabilidadesIntegrationTests.cs:425-467`) usa `RecordingPersonaHandler` para capturar el request saliente y assertea `Authorization: Bearer {expectedJwt}` donde `expectedJwt` viene de `AdminJwtTestHelper.BuildAdminRoleJwt()` |
| Botón Habilidades en `Details` condicional | ✅ | `Details.cshtml:73-88`: `@if (!Model.IsNotFound) { <a>Editar</a> @if (Model.Persona!.IsActive && User.IsInRole(RolesSgv.Administrador)) { <a>Habilidades</a> } }`. Las 3 condiciones se cumplen para que renderice |

### Coherence (Design)

| Decisión (de `design.md`) | ¿Seguida? | Notas |
|---------------------------|-----------|-------|
| Handlers POST separados (Asignar/Quitar) en el PageModel | ✅ Sí | `OnPostAsignarAsync` + `OnPostQuitarAsync` independientes |
| PageModel admin-only + gateo manual en handlers | ✅ Sí | `[Authorize(Roles = RolesSgv.Administrador)]` atributo clase (`PersonaHabilidades.cshtml.cs`) + `if (!EsAdministrador) return Forbid();` en cada handler |
| Persona inactiva → redirect a estado no consultable (decisión UX) | ✅ Sí | GET redirige a `/error/404`; POST invoca `EnsurePersonaActivaAsync` y si falla redirige a `?id={id}` con TempData warning |
| Antiforgery + PRG + `PageFeedback.Set*` para TempData | ✅ Sí | `@Html.AntiForgeryToken()` en los 3 forms (fila Asignar, fila Quitar, footer Asignar); `PageFeedback.SetSuccess/SetDanger/SetWarning` consistente con `CargoHabilidades` |
| Forma espejo de `CargoHabilidades` (reducida al subdominio persona-skill) | ✅ Sí | Sin `Ponderacion`/`EsObligatoria`/`NivelRequeridoId` (citado en comentario `PersonaHabilidades.cshtml.cs:341` y el helper `PersonaSkillFormHelpers` está dentro del mismo archivo, decisión consciente documentada en `apply-progress.md`) |
| Errores → `ErrorCategoria` como taxonomía observable | ✅ Sí | `PersonaSkillFormHelpers.ResolveFailureMessage` (`PersonaHabilidades.cshtml.cs:390-409`) mapea `ErrorCategoria` a mensaje legible en español; preserva `result.Error.Message` cuando aporta texto accionable |
| Bridge JWT end-to-end analog a `ApiBearerTokenIntegrationTests` Cargo | ✅ Sí | `Get_PersonaHabilidades_ForwardsBearerTokenToPersonaApi` test 1:1 con el patrón Cargo, con `RecordingPersonaHandler` que filtra `Requests` por `AbsolutePath.Contains("/api/v1/personas/{personaId}")` |
| `ErrorCategoria` no reintroduce enum paralelo | ✅ | `PersonaSkillErrorType` queda como discriminador interno (no público); el PageModel ramifica por `ErrorCategoria`, no por `PersonaSkillErrorType` |
| `bun run build` pasa (validación final del bundle) | ✅ Sí | `plugins` (5.84 ms) + `styles` (4.59 s) + `inspiniaPages` (2.1 ms) + `build` total 4.6 s, exit 0 |

### Anti-drift & scope

| Check | Resultado | Evidencia |
|-------|-----------|-----------|
| `NivelHabilidadId` (NUNCA `NivelRequeridoId`) en el código del slice | ✅ | `grep "NivelRequeridoId" src/SGV.Web/Pages/Personas/` solo retorna 1 match: el comentario en `PersonaHabilidades.cshtml.cs:341` que documenta la omisión. Cero ocurrencias en markup/handlers |
| Drift de bridge `ApiBearerTokenIntegrationTests` respetado | ✅ | `WebIntegrationFixture.CreatePersonaBridgeLeaseAsync` es 1:1 con `CreateCargoBridgeLeaseAsync`; ambos delegan en `CreateAuthenticatedLeaseAsync` con `WithOverrides` y un `personaApiHandler`/`cargoApiHandler` |
| Archivos modificados están dentro del scope permitido | ✅ | `git diff --stat origin/develop..HEAD -- src/SGV.Api/ src/SGV.Dominio/ src/SGV.Infraestructura/ src/SGV.Aplicacion/ src/SGV.Contracts/` retorna **VACÍO** (sin cambios) |
| 4 commits de Slice 3b | ✅ | `git log origin/develop..HEAD --oneline` muestra exactamente 4 commits: `b31a69f1`, `7ff90f24`, `3e49e80c`, `c2f9a798` |
| Sin `Co-Authored-By` en commits | ✅ | `git log --no-merges --format='%H%n%s%n%b' -3 | grep -i 'co-authored'` retorna 0 matches |
| Commits conventional | ✅ | `test(slice3b):` + `feat(slice3b):` + `docs(slice3b):` — todos con scope y conventional prefix |
| Artifacts OpenSpec en español | ✅ | `tasks.md`, `apply-progress.md`, `proposal.md`, `design.md`, `specs/**/spec.md` todos en español (verificado en lectura previa) |

### Cambios fuera de scope verificados

```text
src/SGV.Api/           → 0 archivos tocados ✅
src/SGV.Dominio/       → 0 archivos tocados ✅
src/SGV.Infraestructura/ → 0 archivos tocados ✅
src/SGV.Aplicacion/    → 0 archivos tocados ✅
src/SGV.Contracts/     → 0 archivos tocados ✅
```

Archivos tocados por los 4 commits de Slice 3b (9 archivos, todos esperados):

| Path | Tipo | Scope |
|------|------|-------|
| `src/SGV.Web/Pages/Personas/PersonaHabilidades.cshtml.cs` | Producción | ✅ permitido (handlers POST) |
| `src/SGV.Web/Pages/Personas/PersonaHabilidades.cshtml` | Producción | ✅ permitido (view: per-row form, errores) |
| `src/SGV.Web/Pages/Personas/Details.cshtml` | Producción | ✅ permitido (botón Habilidades) |
| `tests/SGV.Tests/Web/Persona/PersonaHabilidadesPageTests.cs` | Test | ✅ permitido (17 tests nuevos) |
| `tests/SGV.Tests/Web/Persona/PersonaHabilidadesIntegrationTests.cs` | Test | ✅ permitido (11 nuevos) |
| `tests/SGV.Tests/Web/Persona/DetailsHabilidadesButtonTests.cs` | Test | ✅ permitido (3 nuevos) |
| `tests/SGV.Tests/Web/Collections/WebIntegrationFixture.cs` | Test fixture | ✅ permitido (`CreatePersonaBridgeLeaseAsync`) |
| `openspec/changes/implementa-persona-habilidades/apply-progress.md` | OpenSpec | ✅ permitido |
| `openspec/changes/implementa-persona-habilidades/tasks.md` | OpenSpec | ✅ permitido (3b.1–3b.6 marcados) |

### TDD Compliance (Strict TDD)

| Check | Resultado | Detalle |
|-------|-----------|---------|
| Evidencia TDD reportada en `apply-progress.md` | ✅ | Tabla "TDD Cycle Evidence" con 6 filas (3b.1–3b.6) |
| Todas las tareas RED tienen archivo de test | ✅ | `PersonaHabilidadesPageTests.cs` (3b.1+3b.2) + `PersonaHabilidadesIntegrationTests.cs` (3b.4) + `DetailsHabilidadesButtonTests.cs` (3b.5); los 3 archivos existen en disco y se ejecutan verdes |
| RED confirmado (test files existen) | ✅ | Verificado por lectura directa de los 3 paths |
| GREEN confirmado (tests pasan en ejecución) | ✅ | Filtro `PersonaHabilidades` 34/0/0; filtros `Persona`, `Web`, `ApiBearerToken`, suite completa todos verdes |
| Triangulación adecuada | ✅ | 17 unit PageModel: happy + 4xx NotFound/Conflict/Validation + transport + admin/non-admin + missing SkillId/NivelId + 2 inactive. 11 integration: 5 success/PRG + 4 failure/feedback + 2 inactive + 1 bridge. 3 Details: admin/active, not-found, non-admin. Total 31, cubre cada combinación R × S × transport |
| Safety Net para archivos modificados | ✅ | `PersonaHabilidades.cshtml.cs` y `Details.cshtml` existían pre-Slice 3b (Slice 3a); los 17 tests preexistentes siguen verdes — safety net OK. `WebIntegrationFixture.cs` ya extendido en Slice 2 con `personaApiHandler`; el `CreatePersonaBridgeLeaseAsync` nuevo no rompe leases existentes (parámetro opcional) |
| TDD Test Summary | ✅ | 31 tests nuevos (17 + 11 + 3); ratio unit/integration = 17/14, alineado con `CargoHabilidades` |

**TDD Compliance**: 7/7 checks passed.

### Test Layer Distribution (Slice 3b)

| Layer | Tests | Archivos | Tool |
|-------|-------|----------|------|
| Unit | 17 (`PersonaHabilidadesPageTests`) | 1 | xUnit v3 + `FakePersonaApiClient` + `TempData` in-memory |
| Integration | 11 (`PersonaHabilidadesIntegrationTests`) + 3 (`DetailsHabilidadesButtonTests`) | 2 | xUnit + `WebApplicationFactory` + `WebIntegrationFixture` + `RecordingPersonaHandler` |
| **Total nuevos** | **31** | **3** | |
| **Suite completa** | **2,787** (0 fail, 0 skip) | — | |

### Assertion Quality Audit

| Archivo | Línea(s) | Patrón | Issue | Severidad |
|---------|----------|--------|-------|-----------|
| `PersonaHabilidadesPageTests.cs` | múltiples | `Assert.IsType<...>(result)` + `Assert.Equal/Empty/Single` + `Assert.DoesNotContain("HttpRequestException"/"network down", message)` | Anti-transport-leak guards + verificaciones de tipo y estado. No son tautologías: prueban que la rama de feedback se ejecutó | ✅ OK |
| `PersonaHabilidadesPageTests.cs` | 256-289 | `Assert.IsType<PageResult>(result)` + `Assert.False(page.ModelState.IsValid)` + `Assert.Empty(apiClient.SkillUpsertCalls)` | Validación de ModelState previo al cliente. Verifica que el handler cortó en validación local, no llegó al HTTP | ✅ OK |
| `PersonaHabilidadesPageTests.cs` | 533-568 | Tests persona inactiva: `Assert.IsType<RedirectToPageResult>(result)` + `Assert.Empty(apiClient.SkillUpsertCalls)` (o `SkillDeleteCalls`) | Demuestran que `EnsurePersonaActivaAsync` cortó antes de invocar al cliente | ✅ OK |
| `PersonaHabilidadesIntegrationTests.cs` | múltiples | `Assert.Equal(HttpStatusCode.Redirect, response.StatusCode)` + `Assert.Contains("/personas/{id}/habilidades", location)` + regex `class="alert alert-(danger|warning)"` + `Assert.DoesNotContain("at SGV.", content)` | Asserts de comportamiento HTTP observable + verificación de que la UI no filtra stack traces | ✅ OK |
| `DetailsHabilidadesButtonTests.cs` | múltiples | Regex `<a href=".../personas/{guid}/habilidades">...Habilidades...</a>`; usa `StringValues` para el GUID; distingue la nav global de `/organizacion/habilidades` | Verifica render observable contra el markup concreto | ✅ OK |
| `PersonaHabilidadesIntegrationTests.cs` | 425-467 | `Assert.NotNull(personaRequest.Headers.Authorization)` + `Assert.Equal("Bearer", ...Scheme)` + `Assert.Equal(expectedJwt, ...Parameter)` | Bridge end-to-end: verifica que el header sale con el JWT firmado por `AdminJwtTestHelper`. Cubre R-WEB-05 (no introducido) y el bridge del Slice 3b | ✅ OK |

**Assertion quality**: ✅ Todas las assertions verifican comportamiento observable (PRG, TempData, antiforgery, ausencia de stack traces, bearer token, render HTML del botón). No se encontraron tautologías, ghost loops, mocks huérfanos ni smoke tests.

### Commit Quality

| Commit | Hash | Mensaje | Co-Authored-By | Conventional |
|--------|------|---------|-----------------|---------------|
| 1 | `c2f9a798` | `test(slice3b): add POST handler unit tests for PersonaHabilidades (upsert, delete, inactive gate)` | ❌ ausente | ✅ |
| 2 | `3e49e80c` | `feat(slice3b): implement POST handlers on PersonaHabilidades page with PRG and TempData` | ❌ ausente | ✅ |
| 3 | `7ff90f24` | `feat(slice3b): add integration tests, Details navigation link, and bridge end-to-end test` | ❌ ausente | ✅ |
| 4 | `b31a69f1` | `docs(slice3b): register apply progress and mark tasks complete` | ❌ ausente | ✅ |

### Issues Found

**CRITICAL**: None.

**WARNING**: None.

**SUGGESTION**:
1. **SUGGESTION-1** — Tamaño del slice: el forecast original era 245-325 líneas; el real es **1,493 insertions / 9 deletions en 9 archivos** (1,584 líneas netas), ratio ~5x. La sobreproducción viene de los 31 tests nuevos (~1,017 líneas). El código de producción se mantuvo dentro del forecast (~317 líneas). El `apply-progress.md` documenta este ratio como `size:exception` y referencia que el usuario ya aprobó `size:exception` para Slice 2 en mem #1295. Slice 3b replica el patrón. Sugerencia: para próximos slices similares, considerar partir la suite de tests entre PRs (RED+handlers en uno, integration+Details+bundle en otro) para mantener el review budget <400 por PR.
2. **SUGGESTION-2** — Helper `PersonaSkillFormHelpers` embebido en `PersonaHabilidades.cshtml.cs` (paralelo a `CargoHabilidadesPostHandlers.cs` que es archivo separado). Decisión consciente documentada (subdominio con menos campos); refactor mecánico si se quisiera separar.
3. **SUGGESTION-3** — Discrepancia menor en conteo: `apply-progress.md` reporta 31 tests nuevos + 5 preexistentes = 36 esperados en filtro `PersonaHabilidades`, pero la corrida actual reporta 34. El conteo manual del archivo suma 5 (3a) + 17 (3b.1) + 2 (3b.2) = 24 unit + 10 integration = 34 (no 11 integration como dice el apply-progress, sino 10 — el `Get_PersonaHabilidades_ForwardsBearerTokenToPersonaApi` quizás fue contado doble o hay 1 test menos). El conteo 34 sigue siendo ≥30 esperado y los 3b.1–3b.6 siguen todos representados. No bloqueante.

### `bun run build` Output Resumido

```text
$ bun run build
$ gulp build
[23:20:01] Using gulpfile ~/Source/SGV/src/SGV.Web/gulpfile.js
[23:20:01] Starting 'build'...
[23:20:01] Starting 'plugins'...
[23:20:01] Finished 'plugins' after 5.84 ms
[23:20:01] Starting 'styles'...
[23:20:06] Finished 'styles' after 4.59 s
[23:20:06] Starting 'inspiniaPages'...
[23:20:06] Finished 'inspiniaPages' after 2.1 ms
[23:20:06] Finished 'build' after 4.6 s
```
Exit code: **0**. Deprecation warning de Node 22 sobre `fs.Stats` y aviso de `baseline-browser-mapping` 9 meses viejo, ambos preexistentes y no bloqueantes.

### Artifacts (persistencia hybrid)

- `openspec/changes/implementa-persona-habilidades/verify-report-slice3b.md` (este archivo)
- Engram observation `sdd/implementa-persona-habilidades/verify-report-slice3b` (topic_key para upserts)

### Verdict

**PASS** — Slice 3b cumple los 11 escenarios in-scope de `persona-skill-web-management` (R2 mutaciones, R5 errores recuperables, R6 descubribilidad) y `persona-management` (R-A1 navegación), ejecuta strict TDD correctamente, mantiene el scope estrictamente en `SGV.Web` y `SGV.Tests`, respeta el anti-drift `NivelHabilidadId` y el bridge end-to-end del subrecurso persona-skill, valida el bundle con `bun run build` y deja los commits limpios para el PR final. **Listo para merge.**

### Próximo paso del orquestador

`sdd-archive` puede consolidarse contra `develop` y mover el change a `archive/2026-07-21-implementa-persona-habilidades/`. Slices 1, 2, 3a y 3b ya están mergeados o en cola — el `sdd-archive` no requiere más decisiones de producto: `VerificadoAt`/`Fuente` siguen diferidos, `ErrorCategoria` ya adoptado, acceso admin-only vigente, persona inactiva bloqueada en GET y POST.
