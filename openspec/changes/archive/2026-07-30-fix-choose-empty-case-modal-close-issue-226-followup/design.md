# Design: fix-choose-empty-case-modal-close-issue-226-followup

> Issue: #226 | PR: #228 | Change: `fix-choose-empty-case-modal-close-issue-226-followup`
> Strict TDD MODE activo
> Predecesor: PR #227 (`fix-buscar-persona-create-issue-226`)

## Decisión técnica

### DT-01 — Eliminar el `return` temprano de `choose()`

**Contexto:** USBJS-02 del change #224 decidió abortar `choose()` cuando los elementos del contrato no están presentes. Esto dejó el modal sin cerrar y la persona sin devolver al formulario en el Caso 6 (Create).

**Decisión:** `choose()` ya no aborta. En su lugar, selecciona el camino de presentación adecuado:
- Si los elementos del contrato están presentes (casos 4/5): muta el display.
- Si NO están (caso 6): invoca `renderDynamicCard(text)`.
- SIEMPRE ejecuta las 3 operaciones críticas: cerrar modal, disparar `change`, habilitar `submit`.

**Alternativas consideradas:**

1. **Mantener el abort + reload de la página** — rechazado. UX fea (parpadeo) y la decisión de abort sigue siendo incorrecta desde el punto de vista del usuario.
2. **Render dinámico solo con texto (sin Quitar/Cambiar)** — rechazado. El usuario necesita poder quitar o cambiar la selección sin recargar.
3. **Fetch del DTO para mostrar la card enriquecida** — rechazado. Complejidad innecesaria; el render dinámico mínimo cubre el Caso 5 visualmente.

**Consecuencia:** USBJS-02 cambia su contrato (relax del "NO ocultar el modal" a "SIEMPRE ocultar el modal"). El delta spec se documenta en `specs/usuario-persona-buscador-js/spec.md`.

### DT-02 — Función `renderDynamicCard(text)` separada

**Contexto:** La lógica de creación de la card dinámica es no trivial (~50 líneas con múltiples `createElement` + `setAttribute`). Si la inlineamos en `choose()`, el cuerpo de la función se vuelve ilegible.

**Decisión:** Función nombrada `renderDynamicCard(text)` que:
- Verifica que `display` exista (defensivo).
- Limpia el contenido previo con `display.replaceChildren()`.
- Construye un wrapper `card > card-body > span[data-usuario-persona-display-text] + div[d-flex gap-2] > button[quitar] + button[cambiar]`.
- Crea el hidden input `data-usuario-persona-display-input` para que la próxima invocación de `choose()` encuentre el contrato completo y entre al camino de mutación normal.
- Oculta el `empty` state.

**Consecuencia:** la función es testeable de forma aislada y `choose()` queda compacta (~30 líneas).

### DT-03 — `handleQuitar` como función nombrada reusable

**Contexto:** El handler Quitar original era una función anónima dentro del `forEach` de carga inicial. Para que los botones Quitar creados dinámicamente por `renderDynamicCard` puedan usar el mismo handler, debe ser referenciable.

**Decisión:** Extraer la lógica del handler a `function handleQuitar()` nombrada y bindear tanto en el `forEach` inicial como en los botones dinámicos (`addEventListener('click', handleQuitar)`).

**Consecuencia:** una sola implementación, consistente entre los botones iniciales y los dinámicos.

## Cambios aplicados

### Producción
- `src/SGV.Web/wwwroot/js/pages/usuario-persona-buscador.js`:
  - `choose()` reestructurado: sin early return, con if/else de presentación.
  - Nueva función `renderDynamicCard(text)`.
  - Handler Quitar extraído a `handleQuitar()`.
  - ForEach de carga inicial bindea a `handleQuitar`.

### Tests nuevos
- `tests/SGV.Tests/Web/Tests/Issue226FollowupChooseTests.cs` — 10 tests de source inspection.

## Compatibilidad

- **API:** sin cambios.
- **Contratos wire:** sin cambios.
- **JS bundle:** sin cambios.
- **Persistencia:** sin cambios.
- **CSS/HTML:** sin cambios en el HTML server-rendered (cambios solo runtime JS).

## Riesgos y mitigaciones

| Riesgo | Severidad | Mitigación |
|---|---|---|
| Render dinámico mal estilizado (clases CSS incorrectas) | Baja | Tests de source inspection validan atributos clave. Replica Caso 5. |
| Botón Cambiar no re-abre el modal | Baja | Usa `data-bs-toggle="modal"` + `data-bs-target="#<modalId>"` (con `#`). El patrón ya funcionaba en PR #227. |
| Cambio de contrato USBJS-02 rompe consumidores | Baja | Los consumidores son los mismos formularios; el nuevo comportamiento es estrictamente más correcto. |

## Validación

- ✅ `dotnet build` — 0 errors, 0 warnings.
- ✅ TDD red (código viejo): 7/10 FAIL.
- ✅ TDD green (con fix): 10/10 PASS.
- ✅ Suite Web completa: 1348/1348 PASS, 0 FAIL, 0 SKIP.
