# Proposal: fix-buscar-persona-create-issue-226

> Issue: [#226](https://github.com/elflacoseba/SGV/issues/226) — "No abre el popup Buscar Persona al crear un Usuario o una Ocupación"
> PR: [#227](https://github.com/elflacoseba/SGV/pull/227)
> Change: `fix-buscar-persona-create-issue-226`
> Artifact store: híbrido (OpenSpec + Engram)
> Delivery: single PR (scope pequeño, 1 línea de producción)

## Contexto

Al crear un Usuario (`/seguridad/usuarios/crear`) o una Ocupación (`/organizacion/ocupaciones/crear`), el botón **Buscar Persona** del bloque "Persona vinculada" no abría el modal selector. El bug bloqueaba el alta de cualquier registro que requiriera vincular una persona. Reportado como issue #226 inmediatamente después del merge de #224 (`fix-persona-card-empty-state-issue-224`) y #219 (`reusable-persona-card`).

## Causa raíz

`src/SGV.Web/Pages/Shared/Partials/_PersonaCard.cshtml` línea 245 emitía `data-bs-target="@modalId"` sin el prefijo `#`. Bootstrap 5 trata `data-bs-target` como **selector CSS** vía `SelectorEngine.getElementFromSelector(...)`, que ejecuta `document.querySelector(target)`. Sin `#`, el selector buscaba un elemento con tag `<modalId>` (no existe) → retornaba `null` → el modal no se abría.

Confirmado por:
- 10 ocurrencias de `getElementFromSelector` en `src/SGV.Web/wwwroot/js/vendors.min.js` (Bootstrap 5.3.8).
- Conteo: de ~100 atributos `data-bs-target` en el repo, los únicos 3 sin `#` eran los emitidos por `_PersonaCard.cshtml` (de los cuales sólo la línea 245 correspondía al Caso 6 reportado). Las líneas 126 y 193 (Casos 4/5) ya tenían `#` desde el merge original de la partial (#219).

## Approach

Fix de **1 carácter** en HTML attribute: agregar `#` antes de `@modalId` en la línea 245 de `_PersonaCard.cshtml`.

No requiere recompilar `vendors.min.js` (cambio de HTML, no JS).
No toca JS, API, persistencia ni contratos.
No introduce nuevas dependencias.

## Scope

### In scope
- Agregar `#` en `data-bs-target` del botón "Buscar Persona" (Caso 6) en `_PersonaCard.cshtml`.
- Corregir la regex del test ad-hoc `Issue226CreatePageTests.cs` que usaba `#?` (cero o un `#`) y enmascaraba el bug.
- Agregar regression tests nuevos en `Issue226RegressionTests.cs` (Caso 6 sin hidden).

### Out of scope
- Ningún otro cambio. El JS, la API, la persistencia, los contratos y otros modales no se tocan.
- No se recompila el bundle `vendors.min.js`.

## Acceptance criteria

1. Click en "Buscar Persona" en `/seguridad/usuarios/crear` abre el modal `#usuario-persona-buscador-modal`.
2. Click en "Buscar Persona" en `/organizacion/ocupaciones/crear` abre el modal `#ocupacion-persona-buscador-modal`.
3. Suite Web completa (1341 tests) sigue pasando sin regresiones.
4. Los botones "Cambiar" en Edit (Casos 4/5) siguen funcionando (verificado — no se tocaron).
5. Botón "Quitar" sigue funcionando (no usa `data-bs-target`).

## Risks

- **Bajo.** Cambio de 1 carácter en HTML attribute. No toca JS / API / persistencia / contratos.
- **Back-compat:** el atributo `data-bs-target` se vuelve un selector CSS válido para Bootstrap 5; cualquier consumidor HTML que use el mismo patrón queda avisado por la corrección del test.

## Timeline

Fix puntual, sin slices. Single PR (#227). Merge a `develop` al aprobar review.
