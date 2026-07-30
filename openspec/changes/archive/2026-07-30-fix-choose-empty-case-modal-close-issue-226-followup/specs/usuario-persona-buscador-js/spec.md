# Spec Delta: usuario-persona-buscador-js (issue #226 follow-up)

> Change: `fix-choose-empty-case-modal-close-issue-226-followup`
> Issue: [#226](https://github.com/elflacoseba/SGV/issues/226)
> PR: [#228](https://github.com/elflacoseba/SGV/pull/228)
> Spec base: [`openspec/changes/archive/2026-07-30-fix-persona-card-empty-state-issue-224/specs/usuario-persona-buscador-js/spec.md`](../../../archive/2026-07-30-fix-persona-card-empty-state-issue-224/specs/usuario-persona-buscador-js/spec.md) (USBJS-02)

## Purpose

Relaja el contrato USBJS-02 del change #224: cuando los elementos del contrato
`displayInput/cardText/card/empty` no están presentes en el DOM (Caso 6 puro,
típico de Create), `choose()` ya no aborta con `return` temprano. En su lugar
selecciona el camino de presentación adecuado (mutar display o renderizar
card dinámica) y SIEMPRE cierra el modal, dispara el evento `change` y habilita
el `submit`.

## MODIFIED Requirements

### Requirement: USBJS-02 — `choose()` completa el flujo aunque falten elementos del contrato

`choose(persona)` MUST validar la presencia de `displayInput`, `cardText`,
`card` y `empty`. Si están todos presentes (casos 4/5), MUST mutar el display
normalmente. Si alguno es `null` (caso 6: empty state puro), MUST invocar
`renderDynamicCard(text)` que construye una card mínima con texto + Quitar +
Cambiar dentro del contenedor `display`. En AMBOS casos MUST cerrar el modal
vía `Modal.getOrCreateInstance(modal).hide()`, MUST disparar el evento `change`
sobre `hiddenInput` con bubbles, y MUST habilitar `submit.disabled = false`
(si `submit` existe). Si todos los elementos del contrato son `null`, MUST
emitir `console.warn` mencionando el `id` del modal y el `displayContainerId`
como señal de diagnóstico. NO MUST lanzar `TypeError`.

#### Scenario: caso_6_choose_closes_modal_and_renders_dynamic_card
- GIVEN caso 6 (`editable` + `PersonaDto=null` + sin `FallbackDisplay`): sin `data-usuario-persona-card`, `-display-input`, `-display-text`
- WHEN `choose(persona)` se invoca desde el modal
- THEN MUST escribir `console.warn` mencionando el `id` del modal y `displayContainerId`
- AND MUST invocar `renderDynamicCard(text)` para construir la card dinámica
- AND MUST emitir el evento `change` sobre `hiddenInput` con bubbles
- AND MUST invocar `Modal.getOrCreateInstance(modal).hide()` para cerrar el modal
- AND MUST habilitar `submit.disabled = false` si `submit` existe
- AND MUST NO lanzar `TypeError`.

#### Scenario: caso_6_choose_dynamic_card_contains_quitar_and_cambiar
- GIVEN caso 6 y `choose(persona)` invoca `renderDynamicCard(text)`
- WHEN la función completa su ejecución
- THEN el contenedor `display` MUST contener un elemento con `data-usuario-persona-card`
- AND un elemento con `data-usuario-persona-display-text` cuyo `textContent` sea `text`
- AND un `<button>` con `data-usuario-persona-quitar`
- AND un `<button>` con `data-usuario-persona-buscar`, `data-bs-toggle="modal"` y `data-bs-target="#<modalId>"`
- AND un `<input type="hidden" name="PersonaDisplay" data-usuario-persona-display-input">` con `value=text`
- AND el `displayInput` recién creado permite que la próxima invocación de `choose()` entre al camino de mutación normal (caso 5 effective).

#### Scenario: caso_6_choose_dynamic_quitar_resets_to_empty_state
- GIVEN caso 6 con card dinámica renderizada
- WHEN el usuario hace click en el botón "Quitar" de la card dinámica
- THEN MUST limpiar `hiddenInput.value = ''` y `modal.dataset.currentPersonaId = ''`
- AND MUST limpiar el contenedor `display` (replaceChildren)
- AND MUST mostrar el empty state (`empty.hidden = false`)
- AND MUST deshabilitar `submit.disabled = true` si `submit` existe
- AND MUST disparar el evento `change` sobre `hiddenInput`.

#### Scenario: caso_4_choose_runs_normally_no_warnings
- GIVEN caso 4 (todos los elementos del contrato presentes)
- WHEN `choose(persona)` se invoca
- THEN MUST escribir `displayInput.value`, `cardText.textContent`, `card.hidden=false`, `empty.hidden=true`
- AND MUST NO emitir `console.warn`
- AND MUST ocultar el modal.

#### Scenario: caso_5_choose_runs_normally_no_warnings
- GIVEN caso 5 (todos los elementos presentes)
- WHEN `choose(persona)` se invoca
- THEN MUST ejecutar las mismas mutaciones que en caso 4 sin warnings
- AND MUST ocultar el modal.

### Requirement: USBJS-03 — Handler Quitar reusable como función nombrada

El handler `click` de `[data-usuario-persona-quitar]` MUST estar definido como
función nombrada `handleQuitar()` para que pueda ser bindeada tanto en los
botones iniciales (vía `querySelectorAll(...).forEach(...)`) como en los
botones creados dinámicamente por `renderDynamicCard`. MUST limpiar
`hiddenInput.value = ''`, `modal.dataset.currentPersonaId = ''`, y disparar
el evento `change` sobre `hiddenInput`. Si los elementos del contrato están
presentes (caso 4/5), MUST mutar `displayInput`, `cardText`, `card` y
`empty` con los valores de reset. Si NO están (caso 6), MUST limpiar el
contenedor `display` y mostrar el empty state. NO MUST lanzar `TypeError`.

#### Scenario: caso_6_handle_quitar_resets_to_empty_state
- GIVEN caso 6 con card dinámica renderizada
- WHEN `handleQuitar()` se invoca desde un botón Quitar (inicial o dinámico)
- THEN MUST limpiar `hiddenInput.value = ''`
- AND MUST limpiar `modal.dataset.currentPersonaId = ''`
- AND MUST limpiar el contenedor `display` (replaceChildren)
- AND MUST mostrar el empty state (`empty.hidden = false`)
- AND MUST deshabilitar `submit.disabled = true`
- AND MUST disparar el evento `change` sobre `hiddenInput`.

#### Scenario: caso_4_handle_quitar_resets_card_and_shows_empty
- GIVEN caso 4 con persona seleccionada
- WHEN `handleQuitar()` se invoca
- THEN MUST escribir `displayInput.value = ''`, `cardText.textContent = ''`
- AND MUST ocultar la card (`card.hidden = true`)
- AND MUST mostrar el empty state (`empty.hidden = false`)
- AND MUST deshabilitar `submit.disabled = true`
- AND MUST disparar el evento `change` sobre `hiddenInput`.

## Spec compliance matrix

| ID | Aplica | Verificado por |
|---|---|---|
| USBJS-02 caso_6_closes_modal_and_renders_dynamic_card | Sí | `Issue226FollowupChooseTests` (source inspection: no early return, siempre .hide() después del dispatch, siempre submit.enable, console.warn presente, renderDynamicCard invocada) |
| USBJS-02 caso_6_dynamic_card_contains_quitar_and_cambiar | Sí | `Issue226FollowupChooseTests.RenderDynamicCard_CreatesCardTextQuitarAndCambiarElements` |
| USBJS-02 caso_6_dynamic_quitar_resets | Sí | `Issue226FollowupChooseTests.HandleQuitar_ClearsDynamicDisplayAndShowsEmpty` |
| USBJS-02 caso_4_no_warnings | Sí | `PersonaCardPartialTests.EditableWithPersona_EmitsQuitarAndBuscarButtonsAndModalBinding` (test pre-existente #219, sigue pasando) |
| USBJS-02 caso_5_no_warnings | Sí | `PersonaCardPartialTests.EditableWithPersonaNullAndFallbackDisplay_EmitsEditableFallbackCardWithQuitarCambiar` |
| USBJS-03 caso_6_handle_quitar_resets | Sí | `Issue226FollowupChooseTests.HandleQuitar_ClearsDynamicDisplayAndShowsEmpty` |
| USBJS-03 caso_4_handle_quitar_resets | Sí | `PersonaCardPartialTests.EditableWithPersona_EmitsQuitarAndBuscarButtonsAndModalBinding` |
| USBJS-03 función nombrada reusable | Sí | `Issue226FollowupChooseTests.Script_DefinesHandleQuitarFunction` + `InitialQuitarButtons_BindToHandleQuitar` |

Cumplimiento: **8/8 escenarios cubiertos**.

## Notas de migración

- El cambio es backwards-compatible para los casos 4/5 (sin cambios en el comportamiento observable).
- El cambio es estrictamente más correcto para el caso 6 (modal ahora cierra, persona ahora se renderiza).
- El spec USBJS-02 del change #224 queda **MODIFIED** por este delta; la próxima archive debe consolidar ambos.
