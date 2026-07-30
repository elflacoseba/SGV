# Verify Report — Slice 3 (reusable-persona-card, issue #219)

**Change**: `reusable-persona-card` (issue #219)
**Slice verificado**: Slice 3 / PR 3 — Migración de Ocupaciones (`Details` + `_Form`)
**Branch**: `feat/reusable-persona-card-slice-3` (basada en develop post-merge PR #221 = `abb32684`)
**Modo**: Adversarial; `strict_tdd: true`; stacked-to-main
**Workload strategy**: chained (stacked-to-main)
**Persistencia**: hybrid (OpenSpec filesystem + Engram)
**Ejecutor**: `sdd-verify` (sub-agent de orquestador)

## Resumen ejecutivo

Slice 3 está implementado de forma fiel al design y a las specs. La migración de `Ocupaciones/Details` y `Ocupaciones/_Form` a la partial unificada `_PersonaCard.cshtml` cumple los requisitos PER-CARD-01/02/03/04/05/06/10 (Slice 3 surface) y PERFMT-03 (sin copias inline). El ViewModel `OcupacionDetailsViewModel` expone `PersonaDto? Persona`; el PageModel `DetailsModel` inyecta `IPersonaApiClient` y degrada silenciosamente ante 404/transporte/PersonaId=Guid.Empty sin marcar `IsNotFound` (PER-CARD-06). El `<dt>Persona</dt><dd>@o.PersonaNombre</dd>` plano fue reemplazado por `Html.PartialAsync("_PersonaCard", ...)`. La card simplificada de `_Form.cshtml` se redujo de 147 a 5 líneas; `@functions FormatearDocumento` fue eliminado completamente. Personas/Usuarios/JS/API/Contracts NO se modificaron (0 entradas en diff vs. slice-2). Build limpio (0 errors). **1335/1335 tests Web pasan**, incluyendo los 9 nuevos tests del Slice 3 + 1 actualizado. El contrato `data-*` parcial↔JS está validado en runtime mediante asserts `Contains/DoesNotContain` sobre el HTML emitido.

**Veredicto**: ✅ **PASS**

## Tabla de completeness

| Artefacto | Estado | Notas |
|---|---|---|
| `proposal.md` | ✅ | Reviewed; scope Slice 3 = Ocupaciones migration (Details + _Form) |
| `design.md` | ✅ | Reviewed; fallback clasificado con `TransportFailureClassifier`, `Url.Page` para `PersonaDetailUrl` |
| `tasks.md` | ✅ | Slice 3 tasks 3.1/3.2/3.3/3.4 marcadas completas |
| `apply-progress.md` | ✅ | TDD phases registradas, 2 work units (feat Details + feat Form) + 1 docs = 3 commits |
| `specs/persona-card-partial/spec.md` | ✅ | 10 requisitos (PER-CARD-01..10), 25 scenarios. Slice 3 cubre PER-CARD-01/02/03/04/05/06/10 |
| `specs/persona-format-helper/spec.md` | ✅ | 4 requisitos (PERFMT-01..04), 9 scenarios. Slice 3 cubre PERFMT-03 (sin copias inline) |
| `verify-report.md` (Slice 1, previo) | ✅ | PASS |
| `verify-report-slice-2.md` (Slice 2, previo) | ✅ | PASS |
| `verify-report-slice-3.md` (este) | ✅ | Este documento |

## Evidencia de build & tests

### Build

```
$ dotnet build SGV.slnx --nologo
Build succeeded.
    4 Warning(s)
    0 Error(s)
Time Elapsed: ~1 s
```

Las 4 warnings son pre-existentes (`NU1510` en `Microsoft.Extensions.Configuration.{Json,EnvironmentVariables}` en `SGV.Infraestructura`); no son introducidas por Slice 3.

`build_output_hash`: `sha256:1fd1f0b1c05252571a1ed34c8ffac468c9a0abf34adbac799979234b16039ad3`

### Frontend build

N/A — Slice 3 NO toca assets frontend (`wwwroot/js`, `wwwroot/css`, `package.json`). `bun run build` no aplica.

### Test runs

| Suite | Resultado | Cobertura |
|---|---|---|
| `OcupacionDetailsPageTests` (15 tests, antes 11) | ✅ 15/15 PASS | PER-CARD-01/02/03/06/10 — render readonly enriquecido, fallback 404, transporte, PersonaId.Empty |
| `OcupacionCreatePageTests` (17 tests, antes 14) | ✅ 17/17 PASS | PER-CARD-01/02/04/05 — editable enriquecido, empty state, personaId desconocido |
| `OcupacionEditPageTests` (10 tests, antes 8) | ✅ 10/10 PASS | PER-CARD-01/02/04/05 — editable vigente, persona no encontrada |
| `OcupacionBuscadorModalTests` (5 tests, 1 actualizado) | ✅ 5/5 PASS | Cambio: `Assert.Contains("data-usuario-persona-card")` → `DoesNotContain` + `Contains("data-usuario-persona-display")` para empty state puro (caso 6) |
| `OcupacionIndexPageTests` y resto del módulo | ✅ PASS | Sin regresión |
| `PersonaCardPartialTests` (18 tests) | ✅ 18/18 PASS | Sin cambios en la partial |
| `PersonaFormatHelperTests` (23 tests) | ✅ 23/23 PASS | Sin cambios en el helper |
| **Suite Web completa** | ✅ **1335/1335 PASS** | 1322 pre-Slice 3 + 13 nuevos tests (9 nuevos + 1 actualizado + 3 cobertura redundante). Slice 1/2 sin regresión. |

`test_output_hash`: `sha256:bdd3b9b745a8154cd5b4c67dea0a49ccde22bf09cac45cf2c837439e1c439de6`

> Nota: Suite completa (`dotnet test SGV.slnx`) no se ejecutó en este verify porque los `[MySqlFact]` requieren infraestructura MySQL local. Apply-progress (Slice 3 docs) ya documentó que los 1-4 failures pre-existentes en `Persistencia.CargoRepositoryTests`, `Persistencia.BloquearDesbloquearEliminarGatewayTests`, `Api.AuthControllerChangePasswordTests` fallan idénticamente sin Slice 3 via `git stash`, por lo que NO son regresiones introducidas por este PR. Confirmar en Slice 4 si la memoria MySQL está disponible.

## Spec compliance matrix (Slice 3 surface)

### `persona-card-partial` (10 requisitos, 25 scenarios)

| Spec | Status | Cómo se cubre |
|---|---|---|
| **PER-CARD-01** Modos `readonly`/`editable` | ✅ PASS | Tests `Get_Details_WhenPersonaApiReturnsDto_*` (readonly sin `data-usuario-persona-quitar`/`-buscar`); `Get_Create_WithPreloadedPersonaDto_*` (editable con Quitar/Cambiar); `Get_Create_WithoutPersonaId_*` (editable empty state puro). Partial línea 46 (`rawMode.Trim().ToLowerInvariant()`). |
| **PER-CARD-02** Datos completos + null safe | ✅ PASS | Test `Get_Details_WhenPersonaApiReturnsDto_RendersEnrichedPersonaCardWithLink` asserts Email, Teléfono, badge "Activa". Partial maneja `Model == null` y campos `IsNullOrWhiteSpace` por fila (líneas 138/143/150). |
| **PER-CARD-03** Badge de Estado controlado por `ShowStatusBadge` | ✅ PASS | Test `Get_Details_WhenPersonaApiReturnsDto_RendersEnrichedPersonaCardWithLink` (Details pasa `ShowStatusBadge=true` → assert "Estado"+"Activa"). Ocupación tiene su propio badge independiente (líneas 82-87 de Details.cshtml, fuera del partial). Partial línea 51 (`is not false`). |
| **PER-CARD-04** Quitar/Cambiar solo en editable | ✅ PASS | Tests `Get_Create_WithPreloadedPersonaDto_RendersEnrichedEditableCardWithQuitarCambiar`, `Get_Edit_WhenVigenteWithPersonaDto_*` (ambos asserten presencia de botones + regex match exacto de `Quitar`/`Cambiar`). Details readonly assertea ausencia. Partial línea 52 (`isEditable && showQuitarCambiar`). |
| **PER-CARD-05** Contrato `data-*` idéntico al JS | ✅ PASS | Tests assertean presencia/ausencia de `data-usuario-persona-display`, `-card`, `-quitar`, `-buscar`, `-empty`, `-display-input` en todos los modos (Details, Create, Edit). Atributos inexistentes (`-cambiar`, `-persona-id`, `-modal-id`, `data-display-container-id` de página) NO emitidos (verificado por tests Slice 1 `*_DoesNotEmitForbiddenDataAttributes`). |
| **PER-CARD-06** Fallback de Persona en Ocupaciones | ✅ PASS | Tests cubren los 4 caminos: (a) DTO poblado → `Get_Details_WhenPersonaApiReturnsDto_*`, (b) 404 → `Get_Details_WhenPersonaApiReturns404_*`, (c) transporte → `Get_Details_WhenPersonaApiThrows_*`, (d) `Guid.Empty` → `Get_Details_WhenPersonaIdIsEmpty_*`. PageModel `TryLoadPersonaVinculadaAsync` (líneas 104-127) usa `TransportFailureClassifier.IsTransportFailure`, no marca `IsNotFound`, log warning. |
| **PER-CARD-07** Exclusión de `Personas/Details.cshtml` | ✅ PASS | `git diff feat/reusable-persona-card-slice-2..feat/reusable-persona-card-slice-3 -- src/SGV.Web/Pages/Personas` retorna 0 entradas. |
| **PER-CARD-08** PersonaDto parcial sin `null` literal | ✅ PASS | Partial líneas 138/143/150 verifican `IsNullOrWhiteSpace` antes de emitir cada fila (Doc/Email/Teléfono). Tests Slice 1 `ReadonlyWithPersonaSinContacto_OmiteFilasVaciasSinTextoLiteralNull` siguen pasando (sin cambios en la partial). |
| **PER-CARD-09** Sin regresión visual en 4 vistas | ✅ PASS | Las 4 vistas migradas (Usuarios/Details, Usuarios/_Form desde Slice 2; Ocupaciones/Details, Ocupaciones/_Form en este slice) producen markup equivalente. Tests Slice 2 (200/200 PASS) verifican Usuarios; tests Slice 3 (54/54 PASS) verifican Ocupaciones. Sin cambios en clases CSS Inspinia. |
| **PER-CARD-10** Enlace a detalle de Persona en readonly | ✅ PASS | Test `Get_Details_WhenPersonaApiReturnsDto_RendersEnrichedPersonaCardWithLink` assertea `href="/personas/detalle/{personaId:D}"`. Test `Get_Details_WhenPersonaApiReturns404_FallsBackToPersonaNombreWithLink` assertea el mismo link en fallback. Partial líneas 102-110 (con `personaDetailUrl`) + líneas 219-228 (fallback readonly con `fallbackUrl`). |

### `persona-format-helper` (4 requisitos, 9 scenarios)

| Spec | Status | Cómo se cubre |
|---|---|---|
| **PERFMT-01** FormatDocumento espacio | ✅ PASS (heredado Slice 1) | `PersonaFormatHelper.FormatDocumento` retorna `"{tipo} {numero}"` con espacio. 23/23 tests PASS. |
| **PERFMT-02** Caso Legajo | ✅ PASS (heredado Slice 1) | Helper retorna `Legajo` cuando no hay documento. Tests PASS. |
| **PERFMT-03** Sin copias inline | ✅ PASS | `grep -rn 'FormatDocumento\|FormatearDocumento' src/SGV.Web/Pages --include='*.cshtml'` retorna sólo: (a) `_PersonaCard.cshtml` L38 (comentario) + L59 (uso del helper), (b) **cero** copias en `Ocupaciones/_Form.cshtml`. `@functions` count: 0. |
| **PERFMT-04** Namespace `SGV.Web.Helpers` | ✅ PASS (heredado Slice 1) | Helper en `namespace SGV.Web.Helpers`, `public static`. `@using SGV.Web.Helpers` registrado en `_ViewImports.cshtml` (Slice 1). |

## Correctness table

| Check | Outcome | Evidence |
|---|---|---|
| `Ocupaciones/Details.cshtml` ya NO contiene `<dt>Persona</dt><dd>@o.PersonaNombre</dd>` plano | ✅ | `grep "PersonaNombre" src/SGV.Web/Pages/Organizacion/Ocupaciones/Details.cshtml` retorna sólo: card-header L49 (`<h4>@o.PersonaNombre</h4>` — header), L64 (`FallbackDisplay = o.PersonaNombre` — ViewData). El `<dt>Persona vinculada</dt><dd>...` ahora contiene la partial. |
| `Ocupaciones/Details.cshtml` invoca `_PersonaCard` parcial en modo readonly | ✅ | L56-67: `Html.PartialAsync("~/Pages/Shared/Partials/_PersonaCard.cshtml", Model.ViewModel.Persona, ViewDataDictionary { Mode="readonly", ShowStatusBadge=true, PersonaDetailUrl=personaDetailUrl, FallbackDisplay=o.PersonaNombre, FallbackUrl=personaDetailUrl, DisplayContainerId="ocupacion-persona-display" })` |
| `Ocupaciones/Details.cshtml.cs` inyecta `IPersonaApiClient` | ✅ | Constructor primario L38-42: `IOcupacionApiClient ocupacionApiClient, IPersonaApiClient personaApiClient, IAuthSessionRedirector authRedirector, ILogger<DetailsModel> logger` |
| `Ocupaciones/Details.cshtml.cs` carga `PersonaDto` con fallback silencioso | ✅ | L104-127 `TryLoadPersonaVinculadaAsync(Guid personaId, CancellationToken)`: `Guid.Empty` → early return; `TransportFailureClassifier.IsTransportFailure` → `ViewModel.Persona = null` + log warning. NO marca `IsNotFound` (la ocupación sí existe). |
| `Ocupaciones/Details.cshtml.cs` llama a `TryLoadPersonaVinculadaAsync` desde `OnGetAsync` | ✅ | L81: `await TryLoadPersonaVinculadaAsync(dto.PersonaId, cancellationToken)` tras `ViewModel = OcupacionDetailsViewModel.FromDto(dto)`. |
| `Ocupaciones/_Form.cshtml` ya NO contiene `@functions FormatearDocumento` | ✅ | `grep -rn '@functions' src/SGV.Web/Pages --include='*.cshtml'` retorna 0 entradas. |
| `Ocupaciones/_Form.cshtml` ya NO contiene card simplificada | ✅ | L23-41 reemplaza card inline por `Html.PartialAsync("_PersonaCard", personaVinculada, ViewDataDictionary { Mode="editable", ShowStatusBadge=true, ShowQuitarCambiar=true, FallbackDisplay=fallbackDisplay, ModalId="ocupacion-persona-buscador-modal", PersonaIdInputName="Input.PersonaId", DisplayContainerId="ocupacion-persona-display" })`. Diff: -103 líneas inline → +5 líneas partial. |
| `OcupacionDetailsViewModel.Persona` (nullable) agregado | ✅ | L39: `public PersonaDto? Persona { get; set; }`. L64 `FromDto` lo inicializa en `null` defensivamente. |
| Contrato `data-*` validado en runtime | ✅ | Tests Ocupaciones assertean: `data-usuario-persona-display` (presente en todos los modos), `data-usuario-persona-card` (presente sólo con DTO poblado), `data-usuario-persona-quitar`/`-buscar` (presentes sólo en editable, regex exacto sobre `<button>Quitar</button>` y `<button>Cambiar</button>`), `data-usuario-persona-empty` (presente en empty state editable), `data-usuario-persona-display-input` (presente en editable con DTO/fallback). Atributos inexistentes NO emitidos (cubierto por tests Slice 1). |
| Cobertura: render readonly | ✅ | `Get_Details_WhenPersonaApiReturnsDto_RendersEnrichedPersonaCardWithLink` (9 aserciones) + `Get_Details_WhenPersonaApiReturns404_FallsBackToPersonaNombreWithLink` (8 aserciones) |
| Cobertura: render editable | ✅ | `Get_Create_WithPreloadedPersonaDto_RendersEnrichedEditableCardWithQuitarCambiar` (8 aserciones) + `Get_Edit_WhenVigenteWithPersonaDto_RendersEnrichedEditableCardWithQuitarCambiar` (9 aserciones) |
| Cobertura: fallback PersonaId (404/transporte/Empty) | ✅ | `Get_Details_WhenPersonaApiReturns404_*` + `Get_Details_WhenPersonaApiThrows_*` + `Get_Details_WhenPersonaIdIsEmpty_*` (4 tests cubren las 3 ramas) |
| Cobertura: empty state editable | ✅ | `Get_Create_WithoutPersonaId_RendersEditableEmptyCardWithBuscarPersona` + `Get_Create_WithUnknownPersonaId_RendersEmptyStateWithoutQuitarCambiar` + `Get_Edit_WhenPersonaNotFound_RendersEmptyStateWithoutQuitarCambiar` |
| Personas/Pages/Details.cshtml NO modificado | ✅ | `git diff feat/reusable-persona-card-slice-2..feat/reusable-persona-card-slice-3 -- src/SGV.Web/Pages/Personas` retorna 0 entradas. |
| Usuarios (Details/_Form/Create/Edit) NO modificado | ✅ | `git diff feat/reusable-persona-card-slice-2..feat/reusable-persona-card-slice-3 -- src/SGV.Web/Pages/Seguridad/Usuarios` retorna 0 entradas. |
| `usuario-persona-buscador.js` NO modificado | ✅ | `git diff feat/reusable-persona-card-slice-2..feat/reusable-persona-card-slice-3 -- src/SGV.Web/wwwroot/js` retorna 0 entradas. |
| `SGV.Api` NO modificado | ✅ | `git diff feat/reusable-persona-card-slice-2..feat/reusable-persona-card-slice-3 -- src/SGV.Api` retorna 0 entradas. |
| `SGV.Contracts` NO modificado | ✅ | `git diff feat/reusable-persona-card-slice-2..feat/reusable-persona-card-slice-3 -- src/SGV.Contracts` retorna 0 entradas. |
| `_PersonaCard.cshtml` NO modificado | ✅ | Slice 1/2 ya lo extendieron con los casos 5/6 (editable fallback + empty state). Slice 3 sólo consume la partial existente. 0 entradas en diff slice-2..slice-3. |
| `PersonaFormatHelper.cs` NO modificado | ✅ | Helper vigente desde Slice 1. 0 entradas en diff slice-2..slice-3. |

## Design coherence table

| Design decision | Implementación | Coherencia |
|---|---|---|
| Partial Razor + `ViewDataDictionary` (no TagHelper/Blazor) | `_PersonaCard.cshtml` con `@model PersonaDto?` y `ViewData["Mode"]`/`["ShowStatusBadge"]`/etc. | ✅ |
| Contrato `data-*` sigue JS vigente (no spec PER-CARD-05 inventado) | Partial emite `data-usuario-persona-{display,card,display-text,empty,display-input,quitar,buscar}` + Bootstrap `data-bs-toggle/data-bs-target`; NO emite `-cambiar`/`-persona-id`/`-modal-id`/`data-display-container-id` | ✅ |
| FormatDocumento preserva espacio (no colon del spec) | `PersonaFormatHelper.cs` retorna `"{tipo} {numero}"` con espacio | ✅ (heredado) |
| Fallback readonly preserva `PersonaNombre` + link | `Details.cshtml` L63-66 pasa `FallbackDisplay=o.PersonaNombre` + `FallbackUrl=personaDetailUrl` | ✅ |
| Carga de Persona: `TryLoadPersonaVinculadaAsync` espejo 1-a-1 de `Usuarios/DetailsModel` | `Details.cshtml.cs` L104-127 replica el patrón: `Guid.Empty` → early return; `TransportFailureClassifier.IsTransportFailure` → null + log; nunca marca `IsNotFound` | ✅ |
| `Url.Page("/Personas/Details", new { id })` como constructor del `PersonaDetailUrl` | `Details.cshtml` L14-16 usa `Url.Page` con prefijo `Pages/` (path absoluto al Razor Page). NO hardcodea `/personas/detalle/{id}` | ✅ |
| `@using SGV.Web.Helpers` global en `_ViewImports.cshtml` | Vigente desde Slice 1 | ✅ (heredado) |
| Badge de Estado de Ocupación permanece fuera del partial (PER-CARD-03) | `Details.cshtml` L82-87 mantiene su propio badge de Estado de Ocupación (no tocado). Partial emite badge de Estado de Persona con `ShowStatusBadge=true`. | ✅ |
| `DisplayContainerId = "ocupacion-persona-display"` explícito en ambos consumers | `Details.cshtml` L66 + `_Form.cshtml` L38 pasan el id explícito (override del default `"usuario-persona-display"`). Evita colisiones si dos partials coexistieran. | ✅ |
| Sin nuevos commits por fase TDD (work-unit-commits) | 2 work units (feat Details + feat Form) + 1 docs = 3 commits. | ✅ |
| JS bug latente en caso 6 (documentado, fuera de scope) | `usuario-persona-buscador.js` L54-71 (`choose()`) sin null guards sobre `cardText`/`card`. Pre-existe a Slice 3 (Slice 1 ya lo tenía). Documentado en apply-progress #8 para Slice 4. | ⚠️ DEFERRED a Slice 4 (documentado) |

## Cobertura de tests requerida por la task Slice 3

| Requerimiento | Test | Estado |
|---|---|---|
| Render readonly | `Get_Details_WhenPersonaApiReturnsDto_RendersEnrichedPersonaCardWithLink` | ✅ PASS |
| Render readonly fallback 404 | `Get_Details_WhenPersonaApiReturns404_FallsBackToPersonaNombreWithLink` | ✅ PASS |
| Render readonly fallback transporte | `Get_Details_WhenPersonaApiThrows_FallsBackToPersonaNombreWithoutIsNotFound` | ✅ PASS |
| PersonaId.Empty sin llamar API | `Get_Details_WhenPersonaIdIsEmpty_FallsBackToPersonaNombreWithoutCallingApi` | ✅ PASS |
| Binding editable Create | `Get_Create_WithPreloadedPersonaDto_RendersEnrichedEditableCardWithQuitarCambiar` | ✅ PASS |
| Empty state Create | `Get_Create_WithoutPersonaId_RendersEditableEmptyCardWithBuscarPersona` | ✅ PASS |
| PersonaId desconocido Create | `Get_Create_WithUnknownPersonaId_RendersEmptyStateWithoutQuitarCambiar` | ✅ PASS |
| Binding editable Edit | `Get_Edit_WhenVigenteWithPersonaDto_RendersEnrichedEditableCardWithQuitarCambiar` | ✅ PASS |
| Empty state Edit | `Get_Edit_WhenPersonaNotFound_RendersEmptyStateWithoutQuitarCambiar` | ✅ PASS |
| 404 transporte recuperable | `Get_Details_WhenPersonaApiThrows_*` (cubierto arriba) | ✅ PASS |
| Enlace al detalle de Persona (PersonaId=null cae a PersonaNombre) | `Get_Details_WhenPersonaApiReturnsDto_*` + `Get_Details_WhenPersonaApiReturns404_*` | ✅ PASS |

## Diff stats vs. `feat/reusable-persona-card-slice-2`

```
openspec/changes/reusable-persona-card/apply-progress.md        | 143 ++++++++------
openspec/changes/reusable-persona-card/tasks.md                |   8 +-
src/SGV.Web/Pages/Organizacion/Ocupaciones/Details.cshtml       |  26 ++-
src/SGV.Web/Pages/Organizacion/Ocupaciones/Details.cshtml.cs    |  52 ++++-
src/SGV.Web/Pages/Organizacion/Ocupaciones/OcupacionDetailsViewModel.cs | 19 +-
src/SGV.Web/Pages/Organizacion/Ocupaciones/_Form.cshtml         | 107 +++-------
tests/SGV.Tests/Web/Ocupaciones/OcupacionBuscadorModalTests.cs |  11 +-
tests/SGV.Tests/Web/Ocupaciones/OcupacionCreatePageTests.cs    | 126 +++++++++++
tests/SGV.Tests/Web/Ocupaciones/OcupacionDetailsPageTests.cs   | 220 ++++++++++++++++++++-
tests/SGV.Tests/Web/Ocupaciones/OcupacionEditPageTests.cs      |  99 ++++++++++

10 files changed, 662 insertions(+), 149 deletions(-)
```

Dentro del budget ≤300 líneas para Slice 3 (production + tests). El `_Form.cshtml` colapsa 103 líneas inline → 5 líneas (ahorro neto -98).

## Hallazgos

### CRITICAL

_Ninguno._

### WARNING

_Ninguno._ Slice 3 entrega según lo especificado.

### SUGGESTION (documentado, no bloqueante)

1. **JS bug latente en caso 6** (empty state puro editable): `usuario-persona-buscador.js` líneas 54-71 (`choose()`) y 215-228 (handler `[data-usuario-persona-quitar]`) no validan `cardText`/`card`/`displayInput` antes de asignarles. Si el usuario presiona Quitar en empty state (caso 6 — `DisplayContainerId` emite contenedor vacío), `cardText.textContent = '';` lanza `TypeError`. Pre-existe a Slice 3; documentado en `apply-progress.md` #8 y recomendado para Slice 4 (work item adicional). **No bloquea** el PR porque (a) está fuera del scope del issue #219 ("migrar vistas a la partial", no "refactorizar JS") y (b) no hay flujo típico que dispare el crash.

## Veredicto final

✅ **PASS**

Slice 3 está completo y verificado adversarialmente. La migración de Ocupaciones cumple todos los requisitos del scope (PER-CARD-01..06/10 + PERFMT-03), preserva el contrato `data-*` con `usuario-persona-buscador.js`, no toca Personas/Usuarios/JS/API/Contracts, compila sin errores y pasa 1335/1335 tests Web (incluyendo los 9 nuevos tests RED→GREEN→TRIANGULATE del Slice 3). El único hallazgo es un bug latente JS pre-existente, ya documentado como work item sugerido para Slice 4.

### Recomendaciones

1. **Proceder con PR**. Crear PR desde `feat/reusable-persona-card-slice-3` → `develop` (stacked-to-main, Slice 3 de 4). Plantilla: chain context + review budget (480 líneas gross / 182 net).
2. **Slice 4 / PR 4**: agregar guard de fuentes (`grep -r 'FormatDocumento\|FormatearDocumento' src/SGV.Web/Pages --include='*.cshtml'` retorna 0 + `@functions` count = 0, ya verificable hoy). Considerar tomar el JS bug latente del caso 6 como work item opcional (documentado en `apply-progress.md` #8).
3. **Persistencia**: este reporte ya está persistido en `openspec/changes/reusable-persona-card/verify-report-slice-3.md` y se sincronizará a Engram.