# Design: fix-persona-card-empty-state-issue-224

## 1. Contexto

La issue #224 reporta un `TypeError: Cannot set properties of null (setting 'value')`
en `src/SGV.Web/wwwroot/js/pages/usuario-persona-buscador.js` cuando la partial
`_PersonaCard.cshtml` se renderiza en el caso 6 (`Mode=editable` + `PersonaDto=null`
+ sin `FallbackDisplay`). En esa rama la partial emite solo el contenedor
`data-usuario-persona-display` (vacío, L210) y el empty state
`data-usuario-persona-empty` (L242) como **hermano** del display; **no** emite
`data-usuario-persona-card`, `data-usuario-persona-display-input` ni
`data-usuario-persona-display-text`. El JS asume que esos nodos existen siempre y
escribe directamente sobre ellos en `choose()` (L54-71) y en el handler Quitar
(L215-228), produciendo el TypeError y rompiendo `Ocupaciones/Create` y
`Ocupaciones/Edit` (`isEditableFallback`).

Adicionalmente se detectó un **bug latente** en el lookup de `empty` (L32): el JS
lo busca con `display.querySelector(...)` pero la partial lo emite en
`display.parentElement` (L242), tal como documenta el comentario de la partial
L236-237. Este bug latente afecta también la transición Quitar→Buscar de los
casos 4/5. El change `reusable-persona-card` (#219) excluyó deliberadamente este
fix (decisión técnica #8 de su `apply-progress.md`); este change retoma el
follow-up.

**Scope** (ver `proposal.md`): un archivo JS, un archivo de tests .NET, una nota
en `docs/decisiones-implementacion.md`. **Approach**: Opción A de la exploración
(pragmática) — null-guards en el JS + test .NET del contrato markup del caso 6.
No se modifica la partial ni se agrega Vitest. La spec NEW
`usuario-persona-buscador-js` (USBJS-01..03, 10 escenarios) formaliza el contrato
JS↔DOM que este diseño implementa.

## 2. Decisiones técnicas

### D1 — El fix vive en el JS, no en la partial `_PersonaCard.cshtml`

| Opción | Tradeoff | Decisión |
|--------|----------|----------|
| Null-guards en el JS | Corrige la causa sin tocar markup estable | **Elegida** |
| Emitir siempre `data-usuario-persona-card` en la partial | Rompe las 5 ramas validadas; exige nuevos tests de los casos 1-5; scope creep | Rechazada |

La issue #224 lo establece y el archive del change #219 (decisión técnica #8) lo
confirma. La partial ya fue diseñada para emitir el caso 6 como nodo separado.

### D2 — Refactor del lookup de `empty` a `display.parentElement`

| Opción | Tradeoff | Decisión |
|--------|----------|----------|
| `display.parentElement.querySelector('[data-usuario-persona-empty]')` | Alinea el código con el contrato documentado (partial L236-237) y arregla el bug latente de los casos 4/5 | **Elegida** |
| Emitir el empty como hijo del display | Refactor de la partial + del bloque editable L240-249; muy invasivo | Rechazada |

El comentario de la partial L236-237 dice explícitamente "El JS lo lee vía
`display.parentElement.querySelector(...)`" pero el JS real (L32) usa
`display.querySelector`. Es una corrección que alinea el código con su contrato.

### D3 — Null-guards con `console.warn` + `return early`

| Opción | Tradeoff | Decisión |
|--------|----------|----------|
| `console.warn(...)` + early return | Telemetría visible en DevTools sin romper el flujo; el usuario sigue pudiendo usar el modal | **Elegida** |
| `throw new Error(...)` | Convierte un error silencioso en uno ruidoso que rompe el flujo JS | Rechazada |
| `alert(...)` | Molesto para el usuario final | Rechazada |

El bug es de flujo UI, no de programación defensiva crítica.

### D4 — `hiddenInput.value` y `modal.dataset.currentPersonaId` se actualizan aun en aborto

| Opción | Tradeoff | Decisión |
|--------|----------|----------|
| Actualizar siempre hidden + currentPersonaId | Preserva la selección del usuario; el form recibe `PersonaId` válido | **Elegida** |
| Abortar también el update del hidden | El form quedaría con `PersonaId` vacío aunque el usuario eligió persona | Rechazada |

La spec USBJS-02 L55-60 lo establece. El `change` event NO se dispara en aborto
porque no hay display coherente con qué sincronizar.

### D5 — No agregar Vitest ni infraestructura JS

| Opción | Tradeoff | Decisión |
|--------|----------|----------|
| Solo tests .NET del contrato markup + smoke manual | Sin infra nueva; consistente con el principio "foco en reglas de negocio" del repo | **Elegida** |
| Vitest + jsdom | ~1-2h de setup + nueva dep + nuevo harness; costo >> beneficio para ~30 líneas | Rechazada |

Decisión documentada en `exploration.md` §6.

### D6 — Test .NET único del contrato markup del caso 6

| Opción | Tradeoff | Decisión |
|--------|----------|----------|
| Test compuesto `DoesNotEmitMutableCardContractAttributes` | Cubre la causa raíz; si alguien agrega los atributos al caso 6, el test falla y recuerda que el JS no los espera | **Elegida** |
| Tests parametrizados por atributo individual | Sobretesting; el test compuesto es suficiente | Rechazada |

El test existente `EditableWithPersonaNullAndNoFallback_EmitsEmptyStateWithBuscarPersona`
(L457-475) cubre `card`, `quitar` y `display-input`. El test nuevo agrega
verificación de `data-usuario-persona-display-text` (no cubierto hoy) y documenta
el contrato negativo completo del caso 6.

## 3. Cambios archivo por archivo

### Archivo 1 — `src/SGV.Web/wwwroot/js/pages/usuario-persona-buscador.js`

#### Cambio 3.1.1 — Lookup de `empty` desde `display.parentElement` (USBJS-01)

**ANTES** (L32):
```js
32:     var empty = display && display.querySelector('[data-usuario-persona-empty]');
```

**DESPUÉS** (L32):
```js
32:     var empty = display && display.parentElement.querySelector('[data-usuario-persona-empty]');
```

**Justificación**: bug latente. El empty siempre está en `parentElement` por
diseño de la partial (L242). Sin este fix, los casos 4/5 tendrían `empty === null`
y la transición Quitar→Buscar rompería silenciosamente.

#### Cambio 3.1.2 — Null-guards en `choose()` (USBJS-02)

**ANTES** (L54-71):
```js
54:     function choose(persona) {
55:         var text = personaDisplay(persona);
56:         hiddenInput.value = persona.id;
57:         displayInput.value = text;
58:         cardText.textContent = text;
59:         card.hidden = false;
60:         empty.hidden = true;
61:         modal.dataset.currentPersonaId = persona.id;
62:         if (submit) {
63:             submit.disabled = false;
64:         }
65:         hiddenInput.dispatchEvent(new Event('change', { bubbles: true }));
66:         if (currentFetchController) {
67:             currentFetchController.abort();
68:             currentFetchController = null;
69:         }
70:         window.bootstrap.Modal.getOrCreateInstance(modal).hide();
71:     }
```

**DESPUÉS** (L54-77):
```js
54:     function choose(persona) {
55:         var text = personaDisplay(persona);
56:         // USBJS-02: actualizar hiddenInput y currentPersonaId siempre (la
57:         // selección del usuario es válida aunque el display no se pueda
58:         // sincronizar). Solo el bloque de mutación del display es abortable.
59:         hiddenInput.value = persona.id;
60:         modal.dataset.currentPersonaId = persona.id;
61: 
62:         if (!displayInput || !cardText || !card || !empty) {
63:             console.warn(
64:                 '[usuario-persona-buscador] choose() aborted: missing card contract elements. '
65:                 + 'modalId=' + modal.id + ', displayContainerId=' + modal.dataset.displayContainerId
66:             );
67:             return;
68:         }
69: 
70:         displayInput.value = text;
71:         cardText.textContent = text;
72:         card.hidden = false;
73:         empty.hidden = true;
74:         if (submit) {
75:             submit.disabled = false;
76:         }
77:         hiddenInput.dispatchEvent(new Event('change', { bubbles: true }));
78:         if (currentFetchController) {
79:             currentFetchController.abort();
80:             currentFetchController = null;
81:         }
82:         window.bootstrap.Modal.getOrCreateInstance(modal).hide();
83:     }
```

**Justificación**: el guard reordena `hiddenInput.value` y
`modal.dataset.currentPersonaId` **antes** del bloque abortable (D4). Si falta
algún elemento del contrato, emite `console.warn` (D3) y retorna sin disparar el
`change` event ni ocultar el modal (spec USBJS-02 L43-53). En caso 4/5 todos los
elementos están presentes → flujo normal sin warnings (USBJS-02 L62-73).

#### Cambio 3.1.3 — Null-guards en handler Quitar (USBJS-03)

**ANTES** (L215-228):
```js
215:     root.querySelectorAll('[data-usuario-persona-quitar]').forEach(function (button) {
216:         button.addEventListener('click', function () {
217:             hiddenInput.value = '';
218:             displayInput.value = '';
219:             cardText.textContent = '';
220:             card.hidden = true;
221:             empty.hidden = false;
222:             modal.dataset.currentPersonaId = '';
223:             if (submit) {
224:                 submit.disabled = true;
225:             }
226:             hiddenInput.dispatchEvent(new Event('change', { bubbles: true }));
227:         });
228:     });
```

**DESPUÉS** (L215-240):
```js
215:     root.querySelectorAll('[data-usuario-persona-quitar]').forEach(function (button) {
216:         button.addEventListener('click', function () {
217:             // USBJS-03: limpiar hiddenInput y currentPersonaId siempre;
218:             // abortar mutaciones del display si falta algún elemento del contrato.
219:             hiddenInput.value = '';
220:             modal.dataset.currentPersonaId = '';
221: 
222:             if (!displayInput || !cardText || !card || !empty) {
223:                 console.warn(
224:                     '[usuario-persona-buscador] Quitar aborted: missing card contract elements. '
225:                     + 'modalId=' + modal.id + ', displayContainerId=' + modal.dataset.displayContainerId
226:                 );
227:                 return;
228:             }
229: 
230:             displayInput.value = '';
231:             cardText.textContent = '';
232:             card.hidden = true;
233:             empty.hidden = false;
234:             if (submit) {
235:                 submit.disabled = true;
236:             }
237:             hiddenInput.dispatchEvent(new Event('change', { bubbles: true }));
238:         });
239:     });
```

**Justificación**: mismo patrón que `choose()`. En caso 6 no se emite el botón
Quitar (L471-472 del test existente), por lo que `querySelectorAll` itera cero
nodos y el handler cumple trivialmente (USBJS-03 L83-88). El guard cubre el
escenario defensivo de DOM manipulado (USBJS-03 L96-101).

### Archivo 2 — `tests/SGV.Tests/Web/Tests/PersonaCardPartialTests.cs`

#### Cambio 3.2.1 — Test nuevo `EditableWithPersonaNullAndNoFallback_DoesNotEmitMutableCardContractAttributes`

**Ubicación**: insertar **después** del test
`EditableWithPersonaNullAndNoFallback_EmitsEmptyStateWithBuscarPersona` (L475,
tras su `}`) y **antes** de `private static string BuildQuery(...)` (L477).

**Snippet a insertar**:
```csharp
    // ──────────────────────────────────────────────
    // USBJS-01..03 / contrato del caso 6 con JS: el JS asume que
    // `displayInput`, `cardText`, `card` y `empty` son null en caso 6 y
    // aborta limpiamente. Si la partial cambiara y empezara a emitir
    // alguno de estos atributos en caso 6, el JS asumiría que el contrato
    // está completo y trataría de escribir sobre ellos, rompiendo el flujo
    // USBJS-02/03. Este test blinda el contrato negativo del caso 6.
    // ──────────────────────────────────────────────

    [Fact]
    public async Task EditableWithPersonaNullAndNoFallback_DoesNotEmitMutableCardContractAttributes()
    {
        var query = "mode=editable";

        await using var lease = await CreateAdminLeaseAsync();
        var response = await lease.Client.GetAsync($"/tests/persona-card-harness?{query}");
        var content = HttpUtility.HtmlDecode(await response.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        // El JS aborta si encuentra estos nodos en caso 6 (causa raíz del bug #224).
        Assert.DoesNotContain("data-usuario-persona-card", content, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("data-usuario-persona-display-input", content, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("data-usuario-persona-display-text", content, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("data-usuario-persona-quitar", content, StringComparison.OrdinalIgnoreCase);
    }
```

**Justificación**: el test existente (L457-475) ya verifica `card`, `quitar` y
`display-input`; el test nuevo agrega `data-usuario-persona-display-text` y
agrupa el contrato negativo completo del caso 6 en una sola aserción
documentada. Es RED→GREEN verificable: si alguien accidentalmente agrega esos
atributos al caso 6, el test falla y recuerda que el JS no los espera.

### Archivo 3 — `docs/decisiones-implementacion.md`

#### Cambio 3.3.1 — Nueva sección `## Frontend / JS compartido`

**Estado actual**: la sección NO existe. La más cercana es
`## Frontend CRUD de Personas` (L483).

**Ubicación de inserción**: insertar la nueva sección `## Frontend / JS compartido`
**inmediatamente antes** de `## Frontend CRUD de Personas` (L483), para agrupar
los temas frontend adyacentes. Concretamente, reemplazar la línea `## Frontend
CRUD de Personas` por el bloque nuevo seguido de la línea original:

**ANTES** (L483):
```markdown
## Frontend CRUD de Personas
```

**DESPUÉS**:
```markdown
## Frontend / JS compartido

### Patrón defensivo en `usuario-persona-buscador.js` (issue #224)

> Change: `fix-persona-card-empty-state-issue-224`. Artefactos SDD completos en `openspec/changes/fix-persona-card-empty-state-issue-224/`. Spec NEW: `usuario-persona-buscador-js` (USBJS-01..03).

El script `wwwroot/js/pages/usuario-persona-buscador.js` se apega al patrón "lookup defensivo + mutación abortable": si los elementos del contrato `data-*` que la partial `_PersonaCard.cshtml` puede omitir (caso 6: `editable + PersonaDto=null + sin FallbackDisplay`) no están presentes en el DOM, las mutaciones abortan con `console.warn` en lugar de tirar `TypeError`. La selección del usuario se preserva siempre en `hiddenInput.value` y `modal.dataset.currentPersonaId` (USBJS-02).

El lookup de `empty` se hace desde `display.parentElement` (no `display`) porque la partial emite el empty state como sibling del contenedor `display` (USBJS-01).

**Decisión de no agregar Vitest/Jest**: el equipo excluye infraestructura de testing JS por scope (el fix es trivialmente detectable por inspección; los tests .NET del contrato markup son RED→GREEN verificables). Si en el futuro se introduce infra JS, será un change dedicado.

## Frontend CRUD de Personas
```

**Justificación**: documentar el patrón defensivo y la decisión de no agregar
infra JS, para que futuros cambios en scripts compartidos sigan el mismo patrón.

### Archivo 4 — `openspec/changes/fix-persona-card-empty-state-issue-224/specs/usuario-persona-buscador-js/spec.md`

Sin cambios. La spec ya está creada con USBJS-01..03 y 10 escenarios.

## 4. Estimación de líneas modificadas

| Archivo | Líneas modificadas |
|---------|--------------------|
| `usuario-persona-buscador.js` | ~25 (3 cambios: +13 en `choose()`, +9 en Quitar, 1 en lookup) |
| `PersonaCardPartialTests.cs` | ~22 (1 test nuevo + bloque de comentarios) |
| `docs/decisiones-implementacion.md` | ~12 (sección nueva insertada) |
| **Total diff de código** | **~59 líneas** (muy por debajo del budget de 400) |

| Artefacto SDD | Líneas |
|---------------|--------|
| `design.md` | ~290 (este archivo) |
| `tasks.md` | ~100 (siguiente fase) |
| `verify-report.md` | ~80 (fase verify) |
| `proposal.md`, `exploration.md`, `spec.md` | ya escritos |

> **Aclaración de budget**: el review budget de **400 líneas aplica al diff de
> código** (los ~59 líneas de la tabla). Los artefactos SDD (`design.md`,
> `tasks.md`, `verify-report.md`) son documentación del change y NO computan
> para el budget de review del PR.

## 5. Plan de validación

| Verificación | Comando / Pasos | Criterio de éxito |
|--------------|-----------------|-------------------|
| Build .NET | `dotnet build SGV.slnx` | Compila sin errores |
| Suite .NET | `dotnet test SGV.slnx` | Pasa (incluido el test nuevo) |
| Bundle frontend | `bun install && bun run build` (en `src/SGV.Web`) | Bundle generado sin errores |
| Smoke test 1 | `Ocupaciones/Create` sin `personaId` → empty state visible → abrir modal → seleccionar persona | Console sin `TypeError` ni warnings |
| Smoke test 2 | `Ocupaciones/Edit` con `PersonaId=Guid.Empty` + fetch fallido → empty state visible → seleccionar persona | Console sin errors |
| Smoke test 3 | `Usuarios/Create` con persona precargada (caso 4) → seleccionar otra persona | Modal cierra, card se actualiza, console limpia |
| Smoke test 4 | `Usuarios/Create` con persona precargada → pulsar Quitar | Card se oculta, empty aparece, console limpia |

## 6. Riesgos de implementación

| Riesgo | Severidad | Mitigación |
|--------|-----------|------------|
| El `change` event no se dispara en aborto → handler externo queda desincronizado | Media | Documentar en el comentario del código (L56-58) que el aborto no dispara `change` por diseño (USBJS-02). El form recibió `PersonaId` válido vía `hiddenInput`. |
| La partial cambia el orden de los hermanos (display antes/después del empty) | Baja | El lookup `parentElement.querySelector` busca por selector, no por posición → sigue funcionando. |
| Regresión en el flujo Quitar de casos 4/5 por el refactor del lookup | Baja | El cambio de `display.querySelector` a `display.parentElement.querySelector` es exactamente lo que el comentario partial L236-237 ya documentaba como contrato. Smoke tests 3 y 4 lo cubren. |

## 7. Próximos pasos

`tasks.md` debe derivar directamente de los 3 cambios del JS + 1 test + 1 nota en
docs. Cada task ≤ 2 horas e independientemente testeable:

1. **Task A** (lookup `empty`): 1 línea en `usuario-persona-buscador.js` L32.
2. **Task B** (null-guards `choose()`): refactor L54-71 → L54-83.
3. **Task C** (null-guards handler Quitar): refactor L215-228 → L215-239.
4. **Task D** (test .NET): insertar test nuevo en `PersonaCardPartialTests.cs`.
5. **Task E** (docs): insertar sección en `docs/decisiones-implementacion.md`.
6. **Task F** (verificación): `bun run build` + smoke tests manuales documentados
   en `verify-report.md`.