# Design: fix-buscar-persona-create-issue-226

> Issue: #226 | PR: #227 | Change: `fix-buscar-persona-create-issue-226`
> Strict TDD MODE activo

## Decisión técnica

### DT-01 — Agregar prefijo `#` en `data-bs-target` del Caso 6

**Contexto:** Bootstrap 5 trata `data-bs-target` como selector CSS. Sin `#`, el selector no resuelve el id del modal.

**Decisión:** Cambiar `data-bs-target="@modalId"` → `data-bs-target="#@modalId"` en `_PersonaCard.cshtml` línea 245 (botón "Buscar Persona" del empty state, Caso 6: `editable + PersonaDto null + sin FallbackDisplay`).

**Alternativas consideradas:**

1. **Cambiar la convención en todos los `data-bs-target` del proyecto a usar id sin `#`** — rechazado. Bootstrap 5 lo aceptaría (la función interna `getElementFromSelector` también acepta el formato "id directo" en otros paths), pero el código fuente de Bootstrap 5.3 que está en el bundle (`vendors.min.js`) usa exclusivamente `getElementFromSelector(target)` que ejecuta `document.querySelector(target)`. Sin `#` no resuelve. Convencional: todos los demás `data-bs-target` del proyecto (≈100) ya usan `#`.

2. **Inicializar el modal manualmente vía JS** (`new bootstrap.Modal(...).show()`) — rechazado. El bug es de un solo carácter y la convención del proyecto es usar `data-bs-toggle="modal"`. Inicializar manualmente introduciría código innecesario.

3. **Cambiar el atributo a `href` en lugar de `data-bs-target`** — rechazado. Bootstrap 5 acepta `<a href="#modalId" data-bs-toggle="modal">`, pero `<button>` con `data-bs-target="#..."` es el patrón recomendado para accesibilidad (no requiere href simulado).

**Consecuencia:** único punto de cambio. Las líneas 126 y 193 (Casos 4/5) ya tenían `#` desde el merge original de la partial (#219); no se tocan.

### DT-02 — Tests con regex estricta

**Contexto:** el test ad-hoc `Issue226CreatePageTests.cs` usaba regex `#?` (cero o un `#`) que aceptaba HTML malformado, dejando pasar el bug.

**Decisión:** cambiar `#?` → `#` (regex estricta). El mensaje de error ahora documenta explícitamente la causa: "Bootstrap 5 requiere prefijo '#' porque trata el atributo como selector CSS, no como id".

**Consecuencia:** el test se vuelve un regression test efectivo. Un futuro cambio que introduzca `data-bs-target="<id>"` sin `#` será detectado.

## Cambios aplicados

### Producción
- `src/SGV.Web/Pages/Shared/Partials/_PersonaCard.cshtml` línea 245: 1 carácter (`#`).

### Tests nuevos
- `tests/SGV.Tests/Web/Tests/Issue226RegressionTests.cs` — 1 test (regression Caso 6: empty state sin hidden).
- `tests/SGV.Tests/Web/Tests/Issue226CreatePageTests.cs` — 2 tests (render completo de `/seguridad/usuarios/crear` y `/organizacion/ocupaciones/crear` con regex estricta para `data-bs-target`).

## Compatibilidad

- **API:** sin cambios.
- **Contratos wire (`SGV.Contracts`):** sin cambios.
- **JS bundle (`vendors.min.js`):** sin cambios. No se recompila.
- **Persistencia / Migraciones:** sin cambios.
- **Auth / Roles:** sin cambios.
- **Web (CSS, HTML estructura):** cambio mínimo (1 atributo HTML en 1 línea).

## Riesgos y mitigaciones

| Riesgo | Severidad | Mitigación |
|---|---|---|
| Regresión en botones "Cambiar" (Casos 4/5) | Baja | No se tocaron las líneas 126 y 193. Tests pre-existentes (`PersonaCardPartialTests.EditableWithPersona_EmitsQuitarAndBuscarButtonsAndModalBinding`) los cubren. |
| Cambio de comportamiento en navegadores antiguos | Nula | `#` en selector CSS es estándar W3C, soportado en todos los navegadores modernos. Bootstrap 5 no soporta IE. |
| Otra regresión oculta por cambio de un carácter | Baja | Suite Web completa ejecutada: 1341/1341 PASS. |

## Validación

- ✅ `dotnet build src/SGV.Web` — 0 errors, 0 warnings.
- ✅ `dotnet test .../SGV.Tests --filter ~SGV.Tests.Web` — 1341/1341 PASS, 0 FAIL, 0 SKIP.
- ✅ Tests específicos del change (`~Issue226`) — 3/3 PASS.
- ✅ Tests pre-existentes que cubren Casos 4/5 (`~PersonaCardPartialTests`, `~OcupacionCreatePageTests`) — PASS.
