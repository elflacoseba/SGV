# Exploración: fix-buscar-persona-create-issue-226

## Issue
[#226](https://github.com/elflacoseba/SGV/issues/226) — "No abre el popup Buscar Persona al crear un Usuario o una Ocupación"

## Síntomas observados

- En `/seguridad/usuarios/crear` y `/organizacion/ocupaciones/crear`, al hacer click en el botón **"Buscar Persona"** (empty state, Caso 6: `editable + PersonaDto null + sin FallbackDisplay`), el modal no se abre.
- Los tests HTML en `Issue226CreatePageTests.cs` (2 tests) y `Issue226RegressionTests.cs` (1 test) PASS — todos verifican la estructura HTML server-rendered, no el comportamiento runtime de apertura del modal.
- El test `Issue226RegressionTests.cs` específicamente valida que el div `data-usuario-persona-empty` NO tenga atributo `hidden` en Caso 6 (el `hidden="@(hasPersona || isEditableFallback ? "hidden" : null)"` de Razor resolves correctly a ausencia de atributo).
- El bundle `vendors.min.js` (Bootstrap 5.3.8) está presente en el footer.

## Causa raíz confirmada

**Falta el prefijo `#` en el atributo `data-bs-target` del botón "Buscar Persona" en el empty state (Caso 6).**

Ubicación: `src/SGV.Web/Pages/Shared/Partials/_PersonaCard.cshtml`, **línea 245**:

```razor
data-bs-target="@modalId"
```

Se resuelve a:
```html
data-bs-target="usuario-persona-buscador-modal"
```

Bootstrap 5 requiere:
```html
data-bs-target="#usuario-persona-buscador-modal"
```

Sin el `#`, la API de datos de Bootstrap 5 (`data-bs-toggle="modal"`) no puede resolve el selector CSS y el modal no se abre. No hay ningún `preventDefault()` ni event listener en captura en `app.js` o `vendors.min.js` que interfiera — el problema es exclusivamente el selector malformado.

**Dato clave**: el test `Issue226CreatePageTests.cs` línea 246 usa una regex con `#?` (hash opcional):
```csharp
$@"data-bs-target\s*=\s*""\s*#?\s*{Regex.Escape(modalId)}\s*"""
// Acepta AMBOS formatos: con y sin '#'
```
Esta regex fue escrita deliberadamente para aceptar el bug, por eso los tests PASS aunque el atributo esté malformado. La regex valida HTML correctness pero no valida la compatibilidad con el runtime de Bootstrap.

**Análisis de los tres botones en `_PersonaCard.cshtml`**:

| Ubicación | Botón | `data-bs-target` rendered | ¿Funciona? | Notas |
|---|---|---|---|---|
| Líneas 124-126 | "Cambiar" (Caso 4) | `data-bs-target="#usuario-persona-buscador-modal"` | **Sí** | Visible en Caso 4 (`hasPersona \|\| isEditableFallback`). Ya tenía `#`. |
| Líneas 191-193 | "Cambiar" fallback (Caso 5) | `data-bs-target="#usuario-persona-buscador-modal"` | **Sí** | Visible en Caso 5. Ya tenía `#`. |
| **Línea 245** | **"Buscar Persona" (Caso 6)** | **`data-bs-target="usuario-persona-buscador-modal"`** (sin `#`) | **NO** | **Este es el bug reportado (#226)**. Visible en Caso 6. Único botón afectado. |

Para Ocupaciones aplica el mismo análisis con `ocupacion-persona-buscador-modal`.

> **Nota del agente explore (corregida en apply):** el exploration inicial reportó "los 3 botones comparten el mismo bug". Inspección línea por línea del archivo refutó esa hipótesis — sólo la línea 245 estaba mal. Las líneas 126 y 193 ya tienen `#` desde el merge original de la partial (#219). El fix es UN solo carácter (`#`) en UNA sola línea.

**¿Por qué "Cambiar" parece funcionar?**  
El bug reportado es específicamente sobre "Buscar Persona" en **Create**. En Create (Caso 6), `hasPersona = false` y `isEditableFallback = false`, por lo que el div `data-usuario-persona-empty` se muestra con el botón "Buscar Persona" y el bug se manifiesta. El botón "Cambiar" en Caso 4/5 (persona ya vinculada) no está siendo ejercitado en los tests de Create porque en esos flujos no hay persona vinculada.

## Comparación con precedente

**Modales que funcionan correctamente**:
- `_Topbar.cshtml` línea 33: `data-bs-target="#topnav-menu"` — usa el `#` como literal en el string del atributo Razor.

**Modales con el bug**:
- `_PersonaCard.cshtml` líneas 125-126, 191-193, 245: `data-bs-target="@modalId"` — el valor de `modalId` es `"usuario-persona-buscador-modal"` o `"ocupacion-persona-buscador-modal"` **sin** el `#`. El `#` nunca se incluye porque es parte del literal en Razor (`"#" + modalId`), no del valor de la variable `modalId`.

**Diferencia clave**:  
Los botones correctos usan `data-bs-target="#<id>"` con `#` como prefijo literal del string. Los botones de `_PersonaCard` usan `data-bs-target="@modalId"` donde `modalId` es el ID sin `#`, resultando en un selector CSS inválido para Bootstrap 5.

## Archivos a modificar (hipótesis)

### `src/SGV.Web/Pages/Shared/Partials/_PersonaCard.cshtml`

**Línea 245** — Fix primario (bug #226):
```diff
- data-usuario-persona-buscar data-bs-toggle="modal"
- data-bs-target="@modalId">
+ data-usuario-persona-buscar data-bs-toggle="modal"
+ data-bs-target="#@modalId">
```

**Total de cambios**: 1 línea (agregar `#` antes de `@modalId` en el único `data-bs-target` del Caso 6).

**No se requiere recompilar** `vendors.min.js` — es un bug HTML/CSHTML, no JavaScript.

### `tests/SGV.Tests/Web/Tests/Issue226CreatePageTests.cs`

**Líneas 244-251** — Actualizar regex para exigir `#` (strict TDD). La regex original usaba `#?` (cero o un `#`) y aceptaba HTML malformado:
```diff
- $@"data-bs-target\s*=\s*""\s*#?\s*{Regex.Escape(modalId)}\s*""",
+ $@"data-bs-target\s*=\s*""\s*#{Regex.Escape(modalId)}\s*""",
```
Sin este cambio, el test seguiría pasando con HTML malformado.

## Tests a agregar (strict TDD)

### Tests de regresión HTML (cambios en cshtml)
- **Ninguno nuevo necesario** — los tests existentes en `Issue226CreatePageTests.cs` y `Issue226RegressionTests.cs` cubren la estructura HTML. El fix es corrección de atributos, no de estructura.
- **Sí es necesario corregir** el test regex de `Issue226CreatePageTests.cs` línea 246 para exigir `#` (ver arriba).

### Tests runtime (si hay cambios en js)
- **Ninguno** — no hay cambios en JS. El fix es puramente HTML.

## Riesgos identificados

1. **Riesgo bajo**: el fix es 3 caracteres (`"#` antes de `@modalId`). Muy contenido, casi imposible de romper algo.
2. **Cobertura gap**: los tests HTML actuales no detectaron el bug porque la regex tenía `#?`. La corrección del test regex es necesaria para blindar el TDD.
3. **Riesgo de regresión en "Cambiar"**: los botones "Cambiar" (Caso 4 y 5) tienen el mismo bug pero no están cubiertos por los tests de Create. Tras el fix, conviene verificar manualmente o agregar un test para esos escenarios.
4. **Riesgo de build rápido**: no se requiere recompilación de JS, pero sí debe ejecutarse `dotnet build` y `dotnet test` para validar.

## Próximo paso sugerido

Ir a fase `sdd-propose` para redactar la propuesta formal del change, incluyendo:
- Fix de 3 líneas en `_PersonaCard.cshtml` (3 × `data-bs-target="#@modalId"`).
- Corrección del test regex en `Issue226CreatePageTests.cs` línea 246.
- Verificación manual de los botones "Cambiar" en Edit (Caso 4/5) post-fix.
