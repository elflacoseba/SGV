# Spec Delta: usuario-persona-buscador-js

## Purpose

Spec NEW que documenta el contrato entre el script
`wwwroot/js/pages/usuario-persona-buscador.js` y el DOM emitido por
`_PersonaCard.cshtml` en sus 6 ramas de render. Reglamenta el comportamiento
defensivo del script cuando la partial omite atributos `data-*` (caso 6),
causa raíz del bug #224 (`TypeError` por asumir nodos siempre presentes).

## ADDED Requirements

### Requirement: USBJS-01 — Lookup del empty state desde `display.parentElement`

El script MUST localizar el empty state con
`display.parentElement.querySelector('[data-usuario-persona-empty]')`. La
partial emite ese atributo como sibling de `display` (L242), no como hijo.
Aplica en `editable`, con el empty visible (caso 6) u oculto vía `hidden` (4/5).

#### Scenario: caso_6_empty_visible_lookup_returned
- GIVEN caso 6 (`editable` + `PersonaDto=null` + sin `FallbackDisplay`)
- WHEN el script ejecuta `display.parentElement.querySelector('[data-usuario-persona-empty]')`
- THEN MUST retornar el nodo
- AND el nodo MUST estar visible (sin `hidden`).

#### Scenario: caso_4_empty_hidden_lookup_returned
- GIVEN caso 4 (`editable` + `PersonaDto` poblado)
- WHEN el script ejecuta el lookup en `display.parentElement`
- THEN MUST retornar el nodo
- AND el nodo MUST tener `hidden` aplicado por la partial.

#### Scenario: caso_5_empty_hidden_lookup_returned
- GIVEN caso 5 (`editable` + `PersonaDto=null` + con `FallbackDisplay`)
- WHEN el script ejecuta el lookup en `display.parentElement`
- THEN MUST retornar el nodo
- AND el nodo MUST tener `hidden` aplicado por la partial.

### Requirement: USBJS-02 — `choose()` aborta limpiamente si faltan elementos del contrato

`choose(persona)` MUST validar que `displayInput`, `cardText`, `card` y `empty`
existan antes de escribir. Si alguno es `null`, MUST emitir `console.warn`
mencionando el `id` del modal y el `displayContainerId`, y retornar early sin
tocar el DOM restante ni ocultar el modal. NO MUST lanzar `TypeError`. Las
mutaciones sobre `hiddenInput.value` y `modal.dataset.currentPersonaId` MUST
ejecutarse aun en aborto; el `change` event sobre `hiddenInput` MUST NO
dispararse en aborto.

#### Scenario: caso_6_choose_warns_and_aborts_without_typeerror
- GIVEN caso 6 (sin `data-usuario-persona-card`, `-display-input`, `-display-text`)
- WHEN `choose(persona)` se invoca desde el modal
- THEN MUST escribir `console.warn` mencionando el `id` del modal y `displayContainerId`
- AND MUST NO lanzar `TypeError`
- AND MUST NO ocultar el modal.

#### Scenario: caso_6_choose_still_updates_hidden_input_and_current_persona_id
- GIVEN caso 6 y el script aborta la mutación del display
- WHEN `choose(persona)` retorna
- THEN `hiddenInput.value` MUST quedar igual a `persona.id`
- AND `modal.dataset.currentPersonaId` MUST quedar igual a `persona.id`
- AND el `change` event sobre `hiddenInput` MUST NO dispararse.

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

### Requirement: USBJS-03 — Handler Quitar aborta limpiamente bajo las mismas condiciones

El handler `click` de `[data-usuario-persona-quitar]` MUST aplicar la misma
validación que `choose()`: si `displayInput`, `cardText`, `card` o `empty` son
`null`, MUST emitir `console.warn` y retornar early sin tocar el DOM. NO MUST
lanzar `TypeError`. Cuando la partial no emite el botón Quitar (caso 6), el
handler cumple trivialmente al no registrarse.

#### Scenario: caso_6_quitar_button_not_bound
- GIVEN caso 6 (sin botón `[data-usuario-persona-quitar]`)
- WHEN el document carga y `root.querySelectorAll('[data-usuario-persona-quitar]')` itera
- THEN MUST hallar cero nodos
- AND ningún handler queda bound.

#### Scenario: caso_4_quitar_handler_runs_normally
- GIVEN caso 4 con botón Quitar presente y todos los elementos del contrato disponibles
- WHEN el Administrador pulsa Quitar
- THEN MUST limpiar `displayInput.value`, `cardText.textContent`, poner `card.hidden=true`, `empty.hidden=false`
- AND MUST vaciar `hiddenInput.value` y `modal.dataset.currentPersonaId`
- AND MUST NO emitir `console.warn`.

#### Scenario: defensive_quitar_warns_when_contract_elements_missing
- GIVEN un botón `[data-usuario-persona-quitar]` existe pero `empty` o `card` resultan `null` (DOM manipulado)
- WHEN el handler `click` se invoca
- THEN MUST escribir `console.warn` mencionando el `id` del modal y `displayContainerId`
- AND MUST NO lanzar `TypeError`
- AND MUST retornar early sin despachar el `change` event.

## MODIFIED Requirements

Ninguno.

## REMOVED Requirements

Ninguno.