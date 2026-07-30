# Proposal: fix-choose-empty-case-modal-close-issue-226-followup

> Issue: [#226](https://github.com/elflacoseba/SGV/issues/226) — "No abre el popup Buscar Persona al crear un Usuario o una Ocupación"
> PR: [#228](https://github.com/elflacoseba/SGV/pull/228)
> Predecesor: PR [#227](https://github.com/elflacoseba/SGV/pull/227) (fix del `#` en `data-bs-target`)
> Artifact store: híbrido (OpenSpec + Engram)
> Delivery: single PR (scope pequeño, ~80 líneas JS)

## Contexto

El PR #227 corrigió el bug "el modal no abre" (faltaba `#` en `data-bs-target`). Pero el fix #224 (USBJS-02) —introducido meses atrás— decidió que cuando los elementos del contrato `displayInput/cardText/card/empty` no están presentes en el DOM (Caso 6 puro, típico de Create), `choose()` aborta con `console.warn` y `return` temprano, dejando:

- El modal sin cerrar.
- El evento `change` sobre `hiddenInput` sin disparar.
- El display sin actualizar (el usuario no ve qué eligió).

El usuario reportó el nuevo síntoma inmediatamente después del merge de #227: "se abre el popup al presionar el botón Buscar pero no hace nada al seleccionar una Persona dentro del popup. No se cierra y no devuelve a quién lo llamó la persona elegida."

## Causa raíz

`src/SGV.Web/wwwroot/js/pages/usuario-persona-buscador.js` líneas 62-68 (versión pre-fix):

```js
if (!displayInput || !cardText || !card || !empty) {
    console.warn(...);
    return;  // ← ABORTA antes de cerrar el modal y disparar el change
}

displayInput.value = text;
cardText.textContent = text;
card.hidden = false;
empty.hidden = true;
if (submit) { submit.disabled = false; }
hiddenInput.dispatchEvent(new Event('change', { bubbles: true }));
window.bootstrap.Modal.getOrCreateInstance(modal).hide();
```

El `return` temprano deja 3 operaciones críticas sin ejecutar en el Caso 6:
- `submit.disabled = false` (submit queda deshabilitado).
- `hiddenInput.dispatchEvent('change')` (listeners no se enteran).
- `Modal.hide()` (modal queda abierto).

## Approach

`choose()` ya no aborta el flujo:
- **Casos 4/5** (elementos del contrato presentes): muta el display como antes.
- **Caso 6** (elementos ausentes): invoca `renderDynamicCard(text)` que construye una card mínima con texto + Quitar + Cambiar dentro del contenedor `display`. Replica visualmente el Caso 5 sin requerir recargar la página ni fetch del DTO.
- **SIEMPRE** cierra el modal, dispara `change` y habilita `submit`.

`handleQuitar` se refactoriza a función nombrada para que el botón Quitar del render dinámico pueda bindear el mismo handler (los botones creados dinámicamente no pueden usar `querySelectorAll`).

## Scope

### In scope
- Modificar `choose()` en `usuario-persona-buscador.js` para eliminar el `return` temprano.
- Agregar función `renderDynamicCard(text)` que construye card dinámica con Quitar + Cambiar.
- Refactorizar handler Quitar a función nombrada `handleQuitar`.
- Actualizar el forEach de Quitar para usar `handleQuitar`.
- Agregar 10 tests de source inspection (`Issue226FollowupChooseTests.cs`) que validan el contrato del JS.

### Out of scope
- Sin cambios en `data-bs-target` (ya resuelto en PR #227).
- Sin cambios en la partial `_PersonaCard.cshtml`.
- Sin cambios en el API.
- Sin recompilación de `vendors.min.js`.

## Acceptance criteria

1. Click en "Buscar Persona" en `/seguridad/usuarios/crear` abre el modal (PR #227).
2. **Click en "Seleccionar"** cierra el modal.
3. **Click en "Seleccionar"** muestra la persona elegida en el formulario (texto + botones Quitar/Cambiar).
4. **Click en "Seleccionar"** habilita el botón "Guardar".
5. **Click en "Quitar"** (del render dinámico) limpia el formulario y vuelve al empty state.
6. Suite Web completa (1348 tests) sigue pasando.
7. Los casos 4/5 (Edit/Details) siguen funcionando sin cambios.

## Risks

- **Bajo.** Cambio aislado al JS del modal. No toca API, persistencia, ni partial.
- **Render dinámico JS**: si el HTML esperado por el render dinámico cambia (clases CSS, atributos), el render puede no verse bien. Mitigado con tests de source inspection que validan la presencia de los elementos clave.

## Timeline

Fix puntual, single PR (#228). Merge a `develop` después del PR #227 (que también está abierto). Cuando ambos PRs estén mergeados, la issue #226 puede cerrarse.
