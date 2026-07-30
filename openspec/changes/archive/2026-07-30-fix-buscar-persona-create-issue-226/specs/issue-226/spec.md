# Spec: persona buscador modal — prefijo `#` en data-bs-target (issue #226)

> Change: `fix-buscar-persona-create-issue-226` (delta spec sobre `reusable-persona-card` #219)
> Issue: [#226](https://github.com/elflacoseba/SGV/issues/226)
> PR: [#227](https://github.com/elflacoseba/SGV/pull/227)

## Contexto

Este delta agrega un único requisito al spec del partial `_PersonaCard.cshtml` (introducido por el change `reusable-persona-card` #219): los atributos `data-bs-target` emitidos por la partial deben llevar el prefijo `#` para que Bootstrap 5 pueda resolver el modal.

El bug #226 descubrió que el atributo del botón "Buscar Persona" del Caso 6 (empty state puro, `editable + PersonaDto null + sin FallbackDisplay`) se emitía sin `#`, lo que rompía la apertura del modal en `/seguridad/usuarios/crear` y `/organizacion/ocupaciones/crear`.

## Requirement: BSMODAL-01 — data-bs-target con prefijo `#`

Los atributos `data-bs-target` emitidos por la partial `_PersonaCard.cshtml` para abrir el modal selector de Personas deben llevar el prefijo `#` literal, de modo que Bootstrap 5 (que trata el atributo como selector CSS vía `document.querySelector(...)`) pueda resolver el id del modal.

### Scenario: BSMODAL-01-S01 — Botón Buscar Persona en Create Usuario abre modal

**Given** un usuario administrador autenticado carga `GET /seguridad/usuarios/crear`
**And** el render incluye `<div class="modal fade" id="usuario-persona-buscador-modal" data-usuario-persona-modal>` en el DOM
**And** la partial `_PersonaCard.cshtml` se invoca con `Mode="editable"` + `PersonaVinculada=null` + `FallbackDisplay=null` (Caso 6 puro)
**And** el empty state se emite como `<div data-usuario-persona-empty>` sin atributo `hidden`
**And** el botón "Buscar Persona" lleva `data-bs-toggle="modal"` y `data-bs-target="#usuario-persona-buscador-modal"` (con `#`)
**When** el usuario hace click en el botón "Buscar Persona"
**Then** el modal `#usuario-persona-buscador-modal` se vuelve visible (`class="modal fade show"`)
**And** el campo de búsqueda del modal recibe focus

### Scenario: BSMODAL-01-S02 — Botón Buscar Persona en Create Ocupación abre modal

**Given** un usuario administrador autenticado carga `GET /organizacion/ocupaciones/crear`
**And** el render incluye `<div class="modal fade" id="ocupacion-persona-buscador-modal" data-usuario-persona-modal>` en el DOM
**And** la partial `_PersonaCard.cshtml` se invoca con `Mode="editable"` + `PersonaVinculada=null` (Caso 6 puro)
**And** el botón "Buscar Persona" lleva `data-bs-target="#ocupacion-persona-buscador-modal"` (con `#`)
**When** el usuario hace click en el botón "Buscar Persona"
**Then** el modal `#ocupacion-persona-buscador-modal` se vuelve visible
**And** el campo de búsqueda del modal recibe focus

### Scenario: BSMODAL-01-S03 — Botones Cambiar en Edit siguen abriendo el modal

**Given** un usuario con persona vinculada renderiza la card editable (Caso 4 con `PersonaDto` poblado)
**And** el botón "Cambiar" lleva `data-bs-target="#usuario-persona-buscador-modal"` (con `#`, sin cambios por este change)
**When** el usuario hace click en "Cambiar"
**Then** el modal `#usuario-persona-buscador-modal` se vuelve visible

### Scenario: BSMODAL-01-S04 — Botón Quitar sigue funcionando

**Given** una card editable con persona seleccionada (Caso 4 o 5)
**When** el usuario hace click en "Quitar"
**Then** la persona se desvincula (`hiddenInput.value=""`, `currentPersonaId=""`)
**And** la card se oculta y el empty state se vuelve visible
**And** no se dispara apertura de modal (Quitar no usa `data-bs-target`)

## Requirement: BSMODAL-02 — tests de render con regex estricta

Los tests que verifican la presencia del atributo `data-bs-target` deben exigir el prefijo `#` explícitamente. No se acepta la regex `#?` (cero o un `#`) porque enmascara regresiones futuras del mismo tipo.

### Scenario: BSMODAL-02-S01 — Test de Create exige `#`

**Given** el test `Issue226CreatePageTests.Get_UsuarioCrear_RenderizaModalYEmptyStateSinHidden` corre
**When** el HTML del botón "Buscar Persona" emite `data-bs-target="usuario-persona-buscador-modal"` (sin `#`)
**Then** el test falla con mensaje: `Expected data-bs-target="#usuario-persona-buscador-modal" (Bootstrap 5 requiere prefijo '#' porque trata el atributo como selector CSS, no como id). Sin '#' el modal no abre.`

### Scenario: BSMODAL-02-S02 — Test del Caso 6 blinda ausencia de hidden

**Given** la partial se renderiza con `Mode=editable` y sin personaId ni fallback (Caso 6)
**When** el harness emite el HTML
**Then** el `<div data-usuario-persona-empty>` NO tiene atributo `hidden` (Razor omite el atributo cuando la expresión resuelve a `null`)
**And** el botón "Buscar Persona" lleva `data-bs-target="#<modalId>"` con `#`

## Spec compliance matrix

| ID | Aplica | Verificado por |
|---|---|---|
| BSMODAL-01-S01 | Sí | `Issue226CreatePageTests.Get_UsuarioCrear_RenderizaModalYEmptyStateSinHidden` |
| BSMODAL-01-S02 | Sí | `Issue226CreatePageTests.Get_OcupacionCrear_RenderizaModalYEmptyStateSinHidden` |
| BSMODAL-01-S03 | Sí | `PersonaCardPartialTests.EditableWithPersona_EmitsQuitarAndBuscarButtonsAndModalBinding` (test pre-existente #219) |
| BSMODAL-01-S04 | Sí | Cobertura JS pre-existente vía `usbjs-quitar-contract.spec` (no testeable directamente en suite .NET, validado por inspection) |
| BSMODAL-02-S01 | Sí | Mismo test que BSMODAL-01-S01 (regex estricta `#`) |
| BSMODAL-02-S02 | Sí | `Issue226RegressionTests.EditableWithPersonaNullAndNoFallback_NoHiddenAttributeOnEmptyDiv` |

Cumplimiento: **6/6 escenarios cubiertos**.
