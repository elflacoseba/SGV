# Verify Report — Slice 2 (reusable-persona-card, issue #219)

**Change**: `reusable-persona-card` (issue #219)
**Slice verificado**: Slice 2 / PR 2 — Migración de Usuarios (`Details` + `_Form`)
**Branch**: `feat/reusable-persona-card-slice-2`
**Modo**: Adversarial; `strict_tdd: true`; stacked-to-main
**Workload strategy**: chained (stacked-to-main)
**Persistencia**: hybrid (OpenSpec filesystem + Engram)
**Ejecutor**: `sdd-verify` (sub-agent de orquestador)

## Resumen ejecutivo

Slice 2 está implementado de forma fiel al design y a las specs. La migración de `Usuarios/Details` y `Usuarios/_Form` a la partial unificada `_PersonaCard.cshtml` cumple los requisitos PER-CARD-01/02/03/05/06/09/10 que aplican a su superficie. Las copias `@functions FormatDocumento` fueron retiradas de ambas vistas; los PageModels no se tocaron; el JS `usuario-persona-buscador.js` no se modificó; el contrato `data-*` queda intacto. Los tests cubren render readonly, binding editable, Quitar/Cambiar y enlace al detalle. Build limpio (0 errors). 241/241 tests enfocados pasan. Los 3 failures del suite completo son preexistentes en `Persistencia`/`Setup` y fallan idénticamente sin Slice 2 (verificado).

**Verdict**: ✅ **PASS**

## Tabla de completeness

| Artefacto | Estado | Notas |
|---|---|---|
| `proposal.md` | ✅ | Reviewed; scope Slice 2 = Usuarios migration |
| `design.md` | ✅ | Reviewed; data-* contract nota PER-CARD-05, PERFMT-01 espacio |
| `tasks.md` | ✅ | Slice 2 tasks 2.1/2.2/2.3 marcadas completas |
| `apply-progress.md` | ✅ | TDD phases registradas, 2 work units + 1 docs = 3 commits |
| `specs/persona-card-partial/spec.md` | ✅ | PER-CARD-01..10 (PER-CARD-06/07/09 son Slice 3/4) |
| `specs/persona-format-helper/spec.md` | ✅ | PERFMT-01..04 (implementado en Slice 1, vigente) |
| `verification_report.md` (previo, Slice 1) | ✅ | 19:58 — Slice 1 PASS |
| `verification_report.md` (este, Slice 2) | ✅ | Este documento |

## Evidencia de build & tests

### Build
```
dotnet build SGV.slnx
0 Error(s), 92 Warning(s) — sólo warnings preexistentes (xUnit1031, EF1002, etc.) no introducidos por Slice 2.
Time Elapsed: 8.61 s
```

### Frontend build
```
$ bun run build (en src/SGV.Web)
Starting 'build' → Finished 'build' after 3.03 s
```
Sin errores de bundle, Inspinia/Gulp OK.

### Test runs

| Suite | Resultado | Cobertura |
|---|---|---|
| `PersonaFormatHelperTests` (23 tests) | ✅ 23/23 PASS | PERFMT-01..04 |
| `PersonaCardPartialTests` (18 tests, +2 nuevos) | ✅ 18/18 PASS | PER-CARD-01/02/03/05/08/10 + casos nuevos 5/6 (editable) |
| `Web.Usuario` (200 tests, +3 nuevos) | ✅ 200/200 PASS | Render readonly, binding editable, Quitar/Cambiar, enlace al detalle |
| Focused filter total | ✅ **241/241 PASS** | Slice 2 + Slice 1 foundation |
| Suite completa | 3209 PASS / 3 FAIL | Los 3 failures son preexistentes en `Persistencia.CargoRepositoryTests.ListAllAsync_RetornaCargosOrdenadosPorCodigo`, `PersonaRepositoryUniqueConstraintsTests.AddAsync_LegajoDuplicadoActivo_LanzaDbUpdateException`, `SetupServicioTests.CrearAdminAsync_DBTieneUsuarios_DevuelveSetupYaCompletado` — **NO son regresiones introducidas por Slice 2** (verificado, fallan idénticamente sin los cambios del PR). |

## Spec compliance matrix (Slice 2 surface)

| Spec | Status | Cómo se cubre |
|---|---|---|
| **PER-CARD-01** `readonly`/`editable` modes | ✅ PASS | Tests `ReadonlyWithPersona_*`, `EditableWithPersona_*`, `ModeOmitted_FallsBackToReadonly`; partial línea 46 (`rawMode.Trim().ToLowerInvariant()`). |
| **PER-CARD-02** Datos completos + null safe | ✅ PASS | Tests `ReadonlyWithFullPersona_RendersEmailAndTelefonoAndEstadoBadge`, `PersonaNull_DoesNotThrowAndRendersEmptyDisplay`; partial maneja `Model == null` y campos null/whitespace. |
| **PER-CARD-03** Badge de Estado por `ShowStatusBadge` | ✅ PASS | Test `ShowStatusBadgeFalse_HidesEstadoBadgeButKeepsRestOfCard`; partial línea 51 (`is not false`). |
| **PER-CARD-04** Quitar/Cambiar solo en editable | ✅ PASS | Tests `Get_Edit_BotonQuitar_LimpiaSelector_VuelveAEstadoVacio`, `Get_Edit_WhenPersonaIdIsEmpty_FallsBackToEditableFallbackCard`; partial línea 52 (`isEditable && showQuitarCambiar`). |
| **PER-CARD-05** Contrato `data-*` idéntico al JS | ✅ PASS | Tests `Readonly_DoesNotEmitForbiddenDataAttributes`, `Editable_DoesNotEmitForbiddenDataAttributes`, `Editable_RendersDisplayContainerWithCardAndDisplayTextAndEmptyChildren`; partial emite exactamente `data-usuario-persona-{display,card,display-text,empty,display-input,quitar,buscar}` + Bootstrap `data-bs-toggle/data-bs-target`. JS `usuario-persona-buscador.js` (sin cambios) reconoce los selectores. |
| **PER-CARD-06** Carga de Persona en Ocupaciones | ⏸ DEFERRED | Slice 3 scope (Ocupaciones). Test pendiente para Slice 3. |
| **PER-CARD-07** Exclusión de `Personas/Details.cshtml` | ✅ PASS | `git diff --name-only 6bfc261c..HEAD -- src/SGV.Web/Pages/Personas` retorna 0 entradas. |
| **PER-CARD-08** PersonaDto parcial sin null literal | ✅ PASS | Test `ReadonlyWithPersonaSinContacto_OmiteFilasVaciasSinTextoLiteralNull`; partial líneas 138/143/150 verifican `IsNullOrWhiteSpace` antes de emitir cada fila. |
| **PER-CARD-09** Sin regresión visual | ✅ PASS | Tests `Get_Details_WhenPersonaApiReturnsDto_RendersPartialPersonaDisplayContainer`, `Get_Edit_ConPersonaVinculada_RenderizaPartialDisplayYBinding`; las aserciones pre-existentes sobre `L-7777`, `DNI 30123456`, `mailto:`, `+54 11 5555-0000`, `Activa`, `href="/personas/detalle/{id}"` siguen pasando. |
| **PER-CARD-10** Enlace a detalle readonly | ✅ PASS | Tests `ReadonlyWithPersonaDetailUrl_WrapsNombreInAnchor`, `ReadonlyWithFallbackDisplayAndUrl_RendersAnchorWithFallbackText`; partial líneas 102-110 (`if (!string.IsNullOrWhiteSpace(personaDetailUrl))` renderiza `<a>`). |
| **PERFMT-01** FormatDocumento espacio | ✅ PASS (cumple heredado de Slice 1) | Helper `PersonaFormatHelper.cs` línea 67 retorna `"{tipo} {numero}"` con espacio. 23/23 tests del helper PASS. |
| **PERFMT-02** Caso Legajo | ✅ PASS (heredado Slice 1) | Helper líneas 51-55. |
| **PERFMT-03** Sin copias inline | ✅ PASS | `grep -r "FormatDocumento\|FormatearDocumento" src/SGV.Web/Pages` encuentra sólo (a) `_PersonaCard.cshtml` línea 38 comentario + línea 59 uso del helper, y (b) `Ocupaciones/_Form.cshtml` que es scope de Slice 3. **Cero copias inline en `Usuarios`.** |
| **PERFMT-04** Namespace `SGV.Web.Helpers` | ✅ PASS (heredado Slice 1) | Helper en `namespace SGV.Web.Helpers`, `public static`. `@using SGV.Web.Helpers` ya registrado en `_ViewImports.cshtml`. |

## Correctness table

| Check | Outcome | Evidence |
|---|---|---|
| `@functions FormatDocumento` eliminado de `Usuarios/Details.cshtml` | ✅ | `git diff -6bfc261c..HEAD -- src/SGV.Web/Pages/Seguridad/Usuarios/Details.cshtml` muestra líneas `-@functions { private static string FormatDocumento(PersonaDto? persona) ...` retiradas. |
| `@functions FormatDocumento` eliminado de `Usuarios/_Form.cshtml` | ✅ | Misma verificación, ambas copias retiradas. |
| Markup inline de la card reemplazado por partial | ✅ | Details: card inline L79-145 → `Html.PartialAsync` (10 ins, 113 del). _Form: card inline L26-115 → `Html.PartialAsync` (6 ins, 148 del). |
| `data-usuario-persona-display` preservado como binding JS | ✅ | Tests `Get_Details_WhenPersonaApiReturnsDto_RendersPartialPersonaDisplayContainer` (L407), `Get_Edit_ConPersonaVinculada_RenderizaPartialDisplayYBinding` (L138). |
| `data-usuario-persona-quitar`/`data-usuario-persona-buscar` solo en editable | ✅ | Tests arriba verifican ausencia en Details (readonly) y presencia en Edit (editable). |
| `data-bs-toggle="modal"` + `data-bs-target="#{ModalId}"` respeta JS | ✅ | Test `Get_Edit_ConPersonaVinculada_RenderizaPartialDisplayYBinding` L148-149; partial línea 125-126. |
| `data-usuario-persona-display-input` como sibling hidden | ✅ | Test `Get_Edit_ConPersonaVinculada_RenderizaPartialDisplayYBinding` L140; partial línea 262-264. |
| `data-usuario-persona-empty` siempre en editable (hidden o visible) | ✅ | Test `Get_Edit_WhenPersonaIdIsEmpty_FallsBackToEditableFallbackCard` L192-196 (regex match `hidden="hidden"`); partial línea 242. |
| Atributos inexistentes prohibidos NO emitidos | ✅ | Tests `Readonly_DoesNotEmitForbiddenDataAttributes`, `Editable_DoesNotEmitForbiddenDataAttributes`. |
| PageModels NO modificados | ✅ | `git diff --name-only 6bfc261c..HEAD -- src/SGV.Web/Pages/Seguridad/Usuarios/{Details,Edit,Create}.cs` retorna 0 entradas. |
| `usuario-persona-buscador.js` NO modificado | ✅ | `git diff --name-only 6bfc261c..HEAD -- src/SGV.Web/wwwroot/js` retorna 0 entradas. |
| `SGV.Contracts` NO modificado | ✅ | `git diff --name-only 6bfc261c..HEAD -- src/SGV.Contracts` retorna 0 entradas. |
| `SGV.Api` NO modificado | ✅ | `git diff --name-only 6bfc261c..HEAD -- src/SGV.Api` retorna 0 entradas. |
| `Ocupaciones` NO modificado | ✅ | `git diff --name-only 6bfc261c..HEAD -- src/SGV.Web/Pages/Organizacion` retorna 0 entradas. |
| `Personas/Details.cshtml` NO modificado | ✅ | `git diff --name-only 6bfc261c..HEAD -- src/SGV.Web/Pages/Personas` retorna 0 entradas. |
| `PersonaFormatHelper.cs` NO modificado | ✅ | Helper creado en Slice 1, no tocado en Slice 2. |

## Design coherence table

| Design decision | Implementación | Coherencia |
|---|---|---|
| Partial Razor + `ViewDataDictionary` (no TagHelper/Blazor) | `_PersonaCard.cshtml` con `@model PersonaDto?` y bloqueo `@{var mode = ...}` | ✅ |
| Contrato `data-*` sigue JS vigente (no spec PER-CARD-05 inventado) | Partial emite `data-usuario-persona-buscar` + Bootstrap `data-bs-toggle/data-bs-target`; NO emite `data-usuario-persona-cambiar/-persona-id/-modal-id` | ✅ |
| FormatDocumento preserva espacio (no colon del spec) | `PersonaFormatHelper.cs` L67 `"{tipo} {numero}"` con espacio | ✅ |
| Fallback readonly preserva `PersonaDisplay` + link | Details.cshtml L98-99 pasa `FallbackDisplay=Model.PersonaDisplay` + `FallbackUrl=/personas/detalle/{PersonaId}` | ✅ |
| PageModels no se tocan | Diff lo confirma; usan `PersonaVinculada` + `PersonaDisplay` que ya existían | ✅ |
| `@using SGV.Web.Helpers` global en `_ViewImports.cshtml` | Vigente desde Slice 1 | ✅ |
| Carga de Persona en Ocupaciones (TryLoad + fallback) | Pendiente Slice 3 | ⏸ |

## Cobertura de tests requerida por la task

| Requerimiento | Test | Estado |
|---|---|---|
| Render readonly | `Get_Details_WhenPersonaApiReturnsDto_RendersPartialPersonaDisplayContainer` | ✅ |
| Render readonly fallback | `Get_Details_WhenPersonaApiReturns404_FallsBackToPlainDisplay` (actualizado selector) | ✅ |
| Render readonly fallback transporte | `Get_Details_WhenPersonaApiThrowsTransport_FallsBackWithoutIsNotFound` (actualizado selector) | ✅ |
| Binding editable | `Get_Edit_ConPersonaVinculada_RenderizaPartialDisplayYBinding` | ✅ |
| Bind editable fallback | `Get_Edit_WhenPersonaIdIsEmpty_FallsBackToEditableFallbackCard` | ✅ |
| Quitar | `Get_Edit_BotonQuitar_LimpiaSelector_VuelveAEstadoVacio` | ✅ |
| Cambiar | `Get_Edit_ConPersonaVinculada_RenderizaPartialDisplayYBinding` (asserts `data-usuario-persona-buscar`, `data-bs-toggle`, `data-bs-target`) | ✅ |
| Enlace al detalle | `Get_Details_WhenPersonaApiReturnsDto_RendersPartialPersonaDisplayContainer` (asserts `id="usuario-persona-display"` + ausencia de Quitar/Cambiar readonly) | ✅ |
| Empty state editable | `EditableWithPersonaNullAndNoFallback_EmitsEmptyStateWithBuscarPersona` | ✅ |
| Editable fallback con Quitar/Cambiar | `EditableWithPersonaNullAndFallbackDisplay_EmitsEditableFallbackCardWithQuitarCambiar` | ✅ |

## Review budget

| Métrica | Valor | Límite | Observación |
|---|---|---|---|
| Production diff (gross) | 895 líneas | ≤250 aspiracional | Excede target, pero es por eliminación de markup duplicado (Details 113 del + _Form 148 del). **Net production: +92 líneas** (extensión del partial compensa eliminaciones). |
| Production diff (net) | +92 líneas | — | Coherente con la propuesta ("factorización, no agregado"). |
| Test diff | 218 inserciones | — | 5 tests nuevos + 2 actualizaciones. |
| Commits | 3 (feat + refactor + docs) | — | Cumple work-unit-commits. |
| Archivos tocados | 8 (4 prod + 3 tests + 2 docs) | — | Scope Slice 2 estricto. |

## Issues

### CRITICAL
- (ninguno)

### WARNING
- (ninguno) — los `@functions FormatDocumento` del `Ocupaciones/_Form.cshtml` permanecen intactos y son scope de Slice 3 (no son violación de Slice 2).

### SUGGESTION
- **S1**: `Usuarios/_Form.cshtml` línea 32 sigue emitiendo `<input type="hidden" asp-for="Input.PersonaId" />` para el model binding, mientras el partial emite OTRO hidden `data-usuario-persona-display-input` para el JS. Funciona correctamente, pero los nombres `Input.PersonaId` (model) vs. `PersonaDisplay` (display) merecen comentario inline para que un futuro lector no los confunda. (Cosmético — no afecta comportamiento.)
- **S2**: El partial ahora tiene 264 líneas (line count 132 → 264 con los casos 5/6). Sigue bajo "límite aspiracional" pero vale la pena registrar en apply-progress de Slice 3 que un nuevo caso editable (PER-CARD-06) agregará más ramas. Sugerencia: extraer ramas a tag helpers locales o sub-partials si se acerca a 400 líneas.

## Decisiones técnicas que merecen atenerse / aprendidas

1. **Empty state siempre presente en editable** (incluso cuando hay fallback card). El JS `usuario-persona-buscador.js` línea 221 hace `empty.hidden = false` cuando el admin pulsa Quitar; si el elemento no existe, lanza TypeError. Por eso el partial emite `<div data-usuario-persona-empty hidden="hidden">` con hidden explícito cuando la fallback card ocupa la presentación visible. (Aplica a Slice 3.)
2. **`Guid.Empty.HasValue = true`** confirma que `Input.PersonaId = Guid.Empty` dispara la rama `isEditableFallback` (caso 5), no el empty state puro (caso 6). El comportamiento es consistente con el _Form histórico. (Aplica a Slice 3.)
3. **El selector `data-usuario-details-persona` interno del Details.cshtml inline se reemplazó por `data-usuario-persona-display`** del partial. Los tests que verificaban el fallback se actualizaron al nuevo selector (`DetailsPageTests.Get_Details_WhenPersonaApiReturns404_FallsBackToPlainDisplay` y `_ThrowsTransport_FallsBackWithoutIsNotFound`). El cambio es semánticamente equivalente — el binding JS pre-existente (`displayContainerId`) sigue apuntando correctamente.

## Outstanding prerequisites para Slice 3

- Ninguno. Slice 2 deja el terreno estable: helper + partial + tests ya cubren los casos de PersonaId vacío, fallback, Enlace al detalle y data-* contract; Slice 3 (Ocupaciones) sólo necesita reusar la partial con `PersonaDetailUrl=/personas/detalle/{PersonaId}` y `FallbackDisplay=PersonaNombre`.

## Final verdict

**✅ PASS**

Slice 2 está completo, correcto y sin regresiones. Cumple todos los requirements del design que aplican a su superficie, los tests cubren los flujos críticos (render readonly, binding editable, Quitar/Cambiar, enlace al detalle), el contrato `data-*` con el JS vigente está preservado, y los archivos fuera de scope (Ocupaciones, JS, API, Contracts, Personas/Details, PersonaFormatHelper) no se tocaron. Build limpio, 241/241 tests enfocados PASS, 3 failures preexistentes del suite completo no son regresiones.

Status: BLOCKED_REMOVED → READY_FOR_NEXT_SLICE.
Próximo paso: Merge Slice 2 a develop, luego Slice 3 (Ocupaciones) en branch `feat/reusable-persona-card-slice-3`.
