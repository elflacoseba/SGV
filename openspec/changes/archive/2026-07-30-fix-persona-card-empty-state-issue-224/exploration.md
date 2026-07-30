# Exploración: fix-persona-card-empty-state-issue-224

> Change: `fix-persona-card-empty-state-issue-224`
> Issue: [#224](https://github.com/elflacoseba/SGV/issues/224)
> Artifact store: **Engram + OpenSpec (híbrido)**
> Preflight cacheado: `automatic` + `hybrid` + `single-pr` + `400 líneas`
> Branch base: `develop` HEAD `05dc634b136164bd27ec55e643f3ade601c4f547` (limpio)
> Fecha: 2026-07-29

## 1. Contexto y resumen del bug

`TypeError: Cannot set properties of null (setting 'value')` en
`src/SGV.Web/wwwroot/js/pages/usuario-persona-buscador.js` cuando la partial
`_PersonaCard.cshtml` se renderiza en empty state puro (caso 6 del partial:
`Mode=editable` + `PersonaDto=null` + sin `FallbackDisplay`). Las mutaciones en
`choose()` (L54-71) y en el handler Quitar (L215-228) no validan nulls antes de
escribir `displayInput.value`, `cardText.textContent`, `card.hidden` o `empty.hidden`.

Bug pre-existente detectado durante el change `reusable-persona-card` (#219),
excluido del scope por:
- Decisión técnica #8 de `apply-progress.md` (no romper el ciclo SDD aprobado).
- No introducir fix de JS dentro de un change de Razor/C#.
- Mitigación temporal viable (recargar con `?personaId={guid}`).

Documentado en `openspec/changes/archive/2026-07-29-reusable-persona-card-issue-219/archive-report.md` líneas 113-121 como follow-up explícito.

Severidad: **Media**. Bloquea `Ocupaciones/Create` y `Ocupaciones/Edit` (rama
`isEditableFallback`) sin workaround limpio, pero tiene mitigación.

## 2. Confirmación del bug — shape DOM del caso 6

### 2.1. Lo que la partial emite en caso 6

`src/SGV.Web/Pages/Shared/Partials/_PersonaCard.cshtml`:

| Línea | Código | Atributo emitido |
|------:|--------|------------------|
| L210  | `<div id="@displayContainerId" data-usuario-persona-display></div>` | `data-usuario-persona-display` (contenedor vacío) |
| L242  | `<div data-usuario-persona-empty hidden="...">` | `data-usuario-persona-empty` (visible, con botón Buscar Persona) |

NO se emite ninguno de:
- `data-usuario-persona-card` (no hay card porque no hay persona)
- `data-usuario-persona-display-text` (no hay texto a mostrar)
- `data-usuario-persona-display-input` (L260: solo se emite cuando `hasPersona || isEditableFallback`)

### 2.2. Lo que el JS lookup retorna en caso 6

`usuario-persona-buscador.js` líneas 27-33:

```js
var hiddenInput = root.getElementById(modal.dataset.hiddenInputId);  // OK
var display = root.getElementById(modal.dataset.displayContainerId);  // OK (vacío)
var displayInput = display && display.parentElement.querySelector('[data-usuario-persona-display-input]');
                                                                            // ^^^ null
var card = display && display.querySelector('[data-usuario-persona-card]'); // ^^^ null
var cardText = display && display.querySelector('[data-usuario-persona-display-text]');
                                                                            // ^^^ null
var empty = display && display.querySelector('[data-usuario-persona-empty]'); // ^^^ null (!!!)
var submit = root.querySelector('[data-usuario-persona-submit]');     // OK
```

**Bug doble**:
1. `displayInput`, `card`, `cardText` → null por falta de markup en caso 6.
2. `empty` también devuelve null, aunque la partial SÍ emite `data-usuario-persona-empty` — pero lo emite FUERA del `<div id="displayContainerId">` (L242 vs L210). El JS lo busca en `display`, no en `display.parentElement`. **Esto es un bug latente adicional** que se manifiesta en caso 4/5 también (donde `empty` está hidden pero el JS igual lo busca mal).

### 2.3. Mutaciones que asume elementos no-null

`choose()` (L54-71):

```js
function choose(persona) {
    var text = personaDisplay(persona);
    hiddenInput.value = persona.id;          // OK
    displayInput.value = text;               // ❌ TypeError si null
    cardText.textContent = text;             // ❌ TypeError si null
    card.hidden = false;                     // ❌ TypeError si null
    empty.hidden = true;                     // ❌ TypeError si null (caso 6 + bug latente en otros casos)
    modal.dataset.currentPersonaId = persona.id;
    if (submit) {
        submit.disabled = false;
    }
    hiddenInput.dispatchEvent(new Event('change', { bubbles: true }));
    if (currentFetchController) {
        currentFetchController.abort();
        currentFetchController = null;
    }
    window.bootstrap.Modal.getOrCreateInstance(modal).hide();
}
```

Handler Quitar (L215-228):

```js
root.querySelectorAll('[data-usuario-persona-quitar]').forEach(function (button) {
    button.addEventListener('click', function () {
        hiddenInput.value = '';               // OK
        displayInput.value = '';              // ❌ TypeError si null
        cardText.textContent = '';            // ❌ TypeError si null
        card.hidden = true;                   // ❌ TypeError si null
        empty.hidden = false;                 // ❌ TypeError si null
        modal.dataset.currentPersonaId = '';
        if (submit) {
            submit.disabled = true;
        }
        hiddenInput.dispatchEvent(new Event('change', { bubbles: true }));
    });
});
```

### 2.4. Severidad por mutación

| Mutación | Severidad | Caso afectado |
|----------|-----------|---------------|
| `displayInput.value = ...` | **BLOCKER** | Caso 6 |
| `cardText.textContent = ...` | **BLOCKER** | Caso 6 |
| `card.hidden = ...` | **BLOCKER** | Caso 6 |
| `empty.hidden = true/false` | **WARNING** | Caso 6 + latente en 4/5 (el empty está en `parentElement` por la partial L242) |

El caso 6 es el único donde se dispara la combinación completa porque es el único donde `card` y `cardText` son null simultáneamente. En caso 4/5 esos existen, pero `empty.hidden` apuntaría a null y rompería silenciosamente la transición Quitar→Buscar.

## 3. Call sites del script

`grep -l usuario-persona-buscador.js` en `src/SGV.Web/Pages/**/*.cshtml`:

| Archivo | Línea | Modo | Riesgo caso 6 |
|---------|------:|------|---------------|
| `Seguridad/Usuarios/Create.cshtml` | 57 | editable | **ALTO** — empty state por defecto |
| `Seguridad/Usuarios/Edit.cshtml` | 75 | editable | **MEDIO** — depende del fetch del API |
| `Organizacion/Ocupaciones/Create.cshtml` | 47 | editable | **ALTO** — empty state al crear sin persona |
| `Organizacion/Ocupaciones/Edit.cshtml` | 65 | editable | **ALTO** — empty state cuando fetch falla (`isEditableFallback` no aplica) |

Las 4 vistas son consumers reales de la partial. Cualquiera puede disparar el bug si renderiza caso 6.

### 3.1. Consumers de la partial `_PersonaCard.cshtml`

| Archivo | Modo | Dispara caso 6 |
|---------|------|---------------|
| `Tests/PersonaCardHarness.cshtml` | parametrizable | sí (con `mode=editable` sin personaId) |
| `Seguridad/Usuarios/Details.cshtml` | readonly | no |
| `Seguridad/Usuarios/_Form.cshtml` | editable | sí (cuando no hay persona seleccionada) |
| `Organizacion/Ocupaciones/Details.cshtml` | readonly | no |
| `Organizacion/Ocupaciones/_Form.cshtml` | editable | sí |

## 4. Auditoría de tests existentes

### 4.1. Cobertura del contrato data-* por caso (en `PersonaCardPartialTests.cs`)

| Caso | Test | Cobertura |
|------|------|-----------|
| 1. readonly + DTO | `ReadonlyWithPersona_RendersNombreYDocumentoSinBotonesMutables` (L46-63) | ✅ |
| 2. readonly + DTO null + fallback | `ReadonlyWithFallbackDisplayAndUrl_RendersAnchorWithFallbackText` (L335-354) | ✅ |
| 3. readonly + DTO null + sin fallback | `ModeOmitted_FallsBackToReadonly` (L93-106), `PersonaNull_DoesNotThrowAndRendersEmptyDisplay` (L113-126) | ✅ |
| 4. editable + DTO | `EditableWithPersona_EmitsQuitarAndBuscarButtonsAndModalBinding` (L69-86) | ✅ |
| 5. editable + DTO null + fallback | `EditableWithPersonaNullAndFallbackDisplay_EmitsEditableFallbackCardWithQuitarCambiar` (L412-449) | ✅ |
| 6. editable + DTO null + sin fallback | `EditableWithPersonaNullAndNoFallback_EmitsEmptyStateWithBuscarPersona` (L457-475) | ✅ markup |

**Hallazgo crítico**: caso 6 tiene cobertura de markup PERO no detecta el bug JS porque los tests no ejecutan el script.

### 4.2. Tests de Ocupaciones que cubren el flujo

| Test | Archivo | Cobertura caso 6 |
|------|---------|------------------|
| `Get_Create_WithPreloadedPersonaDto_RendersEnrichedEditableCardWithQuitarCambiar` | `OcupacionCreatePageTests.cs` | caso 4 |
| `Get_Create_WithoutPersonaId_RendersEditableEmptyCardWithBuscarPersona` | `OcupacionCreatePageTests.cs` | **caso 6** (markup only) |
| `Get_Create_WithUnknownPersonaId_RendersEmptyStateWithoutQuitarCambiar` | `OcupacionCreatePageTests.cs` | **caso 6** (markup only) |
| `Get_Edit_WhenPersonaNotFound_RendersEmptyStateWithoutQuitarCambiar` | `OcupacionEditPageTests.cs` | **caso 6** (markup only) |
| `Get_Edit_WhenVigenteWithPersonaDto_RendersEnrichedEditableCardWithQuitarCambiar` | `OcupacionEditPageTests.cs` | caso 4 |

Todos verifican markup HTML; ninguno ejecuta el bundle JS.

### 4.3. Gap de testing JS

`openspec/config.yaml` declara:

```yaml
testing:
  e2e:
    available: false
    tool: —
```

`src/SGV.Web/package.json` NO incluye vitest/jest/mocha/playwright. Solo tiene `gulp` y dependencias de bundling.

**No hay infraestructura para tests JS automatizados** en el repo. Los `WebApplicationFactory` de xUnit renderizan HTML y Assertions sobre strings, pero NO ejecutan el script del navegador.

## 5. Patrones similares en otros JS del repo

`grep "querySelector" wwwroot/js/pages/*.js` (68 matches totales). Repaso por archivo:

| Archivo | Patrón | ¿Mismo riesgo? |
|---------|--------|----------------|
| `usuario-persona-buscador.js` | lookup defensivo en líneas 29-32, mutación sin guarda | **SÍ (target del fix)** |
| `cargos-index.js` | `form.querySelector` dentro de `forEach` | No (cada form es self-contained) |
| `usuarios-index.js` | idem cargos-index | No |
| `habilidades-index.js` | idem | No |
| `personas-index.js` | idem | No |
| `puestos-index.js` | idem | No |
| `unidades-organizativas-index.js` | idem | No |
| `skill-management.js` | row.querySelector dentro de forEach | No |
| `personas-typeahead.js` | lookup al `root` directo, no a parentElement | No (patrón distinto) |
| `form-choice.js` | `document.querySelectorAll("[data-choices]")` | No (Choices.js wrapper) |
| `auth-password.js` | `wrapper.querySelector(...)` dentro de forEach | No |

**Conclusión**: el patrón "lookup defensivo pero mutación no defensiva" es único a `usuario-persona-buscador.js`. El fix es localizado; no requiere refactor transversal.

## 6. Approach de testing — decisión técnica

Tres opciones evaluadas:

### Opción A (pragmática, scope mínimo) — RECOMENDADA

- Fix JS con null-guards + lookup corregido de `empty` (a `display.parentElement`).
- Tests .NET adicionales en `PersonaCardPartialTests.cs` que **documenten y refuercen** el contrato del caso 6 (qué elementos SÍ/NO se emiten).
- Smoke test manual documentado en `verify-report.md` con pasos reproducibles (cargar `/organizacion/ocupaciones/crear`, abrir modal, seleccionar persona, verificar consola sin errors).
- Build + suite .NET completa pasa.

**Pros**: scope mínimo, no agrega infra, consistente con el principio "foco en reglas de negocio" del repo. El fix es trivialmente verificable por inspección: cualquier null-guard correcto basta.

**Contras**: el bug no queda cubierto por un test automatizado rojo→verde. Pero la realidad es que el equipo ya aceptó esto como follow-up y el cambio #219 lo excluyó explícitamente.

### Opción B (Vitest + jsdom)

- Agregar `vitest`, `jsdom`, `@vitest/ui` al `package.json`.
- Crear `tests/SGV.Tests.Web.Js/` con test del lookup + mutaciones.
- Hook al `bun run build` o nuevo `bun run test:js`.

**Pros**: TDD real para JS. Deja precedente para futuros tests JS.

**Contras**: ~1-2 horas solo de setup + nueva dependencia en CI + nuevo harness a mantener. Scope creep para un fix de ~30 líneas. Introduce infra que el equipo nunca pidió.

### Opción C (jsdom standalone sin framework)

- Crear `tests/js/usuario-persona-buscador.test.js` ejecutado con `node --test` + `jsdom` cargado manualmente.

**Pros**: TDD real, cero deps nuevas en package.json.

**Contras**: integración con CI no estándar, script a mantener manualmente. Mismo esfuerzo que B sin los beneficios de un framework.

### Recomendación final: **Opción A**

Razones:
1. El bug es trivialmente detectable por null-guards. El valor del TDD aquí es bajo comparado con el costo de setup de infra.
2. Los tests .NET del contrato `data-*` (PER-CARD-01..10) ya cubren QUÉ elementos emite la partial por caso. La fix NO agrega lógica nueva, solo defensive guards.
3. El equipo excluyó este bug deliberadamente del change #219 (decisión documentada). Forzar infra nueva rompe ese equilibrio.
4. El principio del repo: "Cada test debe aportar valor real. Si no protege una regla de negocio, un comportamiento importante o previene una regresión, no generar el test." Un test JS para null-guards sobre un caso que la partial misma nunca emite los elementos necesarios es de bajo valor.
5. La verificación manual (smoke test) es suficiente para este nivel de riesgo. Si se rompe, los síntomas son TypeError en consola — detectable visualmente en cualquier sesión de navegador.

Si en el futuro el equipo decide invertir en infra JS testing, será un change dedicado, no un side-effect de este fix.

## 7. Cambios complementarios recomendados

1. **Mejorar el lookup de `empty`** en JS (L32) para usar `display.parentElement` (consistente con el comentario de la partial L236-237: "El JS lo lee vía `display.parentElement.querySelector(...)`"). Esto arregla el bug latente en caso 4/5.
2. **Mejorar el lookup de `displayInput`** en JS (L29): ya usa `display.parentElement`, pero debería validar que existe antes de escribir (defensa en profundidad).
3. **Refactor de `choose()` y handler Quitar** con guard clauses explícitas: si los elementos no existen, log warning a consola y abortar limpiamente (no tirar TypeError).
4. **Actualizar `docs/decisiones-implementacion.md`** § "Frontend / JS compartido" con la nota del fix y el patrón defensivo establecido.
5. **Smoke test manual** documentado en `verify-report.md`.

## 8. Riesgos y mitigaciones

| Riesgo | Severidad | Mitigación |
|--------|-----------|------------|
| Regresión en Usuarios Create/Edit | Media | Los tests .NET existentes (`PersonaCardPartialTests`, `OcupacionBuscadorModalTests`) siguen pasando porque verifican markup. Smoke test manual cubre comportamiento JS. |
| Otros call sites del script quedan con el mismo bug | Baja | Auditoría completa (sección 5) confirma que NO hay otros JS con el patrón defectuoso. |
| Refactor del lookup de `empty` rompe binding en otros consumers | Baja | El comportamiento esperado: empty visible cuando no hay persona, hidden cuando hay. El JS solo cambia QUÉ nodo lee, no QUÉ hace con él. |
| Fix no se ejecuta por algún bundling issue | Baja | `bun run build` valida que el bundle se genera sin errores. |
| Strict TDD sin test rojo→verde | Baja | Documentado como decisión explícita en sección 6. El equipo lo acepta (follow-up documentado en archive #219). |

## 9. Próximos pasos para `sdd-propose`

La propuesta debería cubrir:

1. **Intent**: cerrar el follow-up del change #219 (issue #224). Fix JS con null-guards en `choose()` y handler Quitar. Refactor menor del lookup para coherencia con la partial.
2. **Scope**: un archivo (`usuario-persona-buscador.js`), un archivo de tests (`PersonaCardPartialTests.cs`), un archivo de docs (`docs/decisiones-implementacion.md`), un archivo de spec (delta).
3. **Out of scope**: agregar Vitest, refactor de otros JS, cambios en la partial, cambios en API/Contracts.
4. **Approach**: Opción A documentada arriba (pragmática, sin infra JS nueva).
5. **Criterios de aceptación**: derivados de la issue #224 (5 items) + 1 item nuevo sobre el bug latente de `empty` lookup.

## 10. Artefactos a producir

| Artefacto | Path | Backend |
|-----------|------|---------|
| Exploration | `openspec/changes/fix-persona-card-empty-state-issue-224/exploration.md` | OpenSpec |
| Exploration (resumen) | topic_key `sdd/fix-persona-card-empty-state-issue-224/explore` | Engram |
| Proposal | `openspec/changes/fix-persona-card-empty-state-issue-224/proposal.md` | OpenSpec |
| Proposal | topic_key `sdd/fix-persona-card-empty-state-issue-224/proposal` | Engram |
| Spec (delta) | `openspec/changes/fix-persona-card-empty-state-issue-224/specs/usuario-persona-buscador-js/spec.md` | OpenSpec |
| Design | `openspec/changes/fix-persona-card-empty-state-issue-224/design.md` | OpenSpec |
| Tasks | `openspec/changes/fix-persona-card-empty-state-issue-224/tasks.md` | OpenSpec |
| Apply progress | `openspec/changes/fix-persona-card-empty-state-issue-224/apply-progress.md` | OpenSpec |
| Verify report | `openspec/changes/fix-persona-card-empty-state-issue-224/verify-report.md` | OpenSpec |
| Archive report | `openspec/changes/fix-persona-card-empty-state-issue-224/archive-report.md` | OpenSpec |

Specs candidatas (todas NEW porque no hay canónicas previas que cubran este contrato JS):

- `usuario-persona-buscador-js` — contrato entre el JS y el DOM emitido por `_PersonaCard.cshtml` (5 requirements, ~10 escenarios).
