# Apply Progress: fix-buscar-persona-create-issue-226

> Change: `fix-buscar-persona-create-issue-226`
> Issue: [#226](https://github.com/elflacoseba/SGV/issues/226)
> Artifact store: Engram + OpenSpec (híbrido)
> Delivery strategy: Single PR (scope pequeño, 1 línea de producción + 1 línea de test)
> Review budget: 400 líneas de código
> Branch: `feat/fix-buscar-persona-create-issue-226`
> Strict TDD MODE: activo

## Estado

**Apply aplicado en working tree, suite Web pasando 1341/1341.** Pendiente commit, push, PR y verify formal.

## Resumen del fix

Causa raíz: `_PersonaCard.cshtml` línea 245 emite `data-bs-target="@modalId"` sin el prefijo `#`. Bootstrap 5 trata `data-bs-target` como selector CSS (`document.querySelector(...)`), no como id directo. Sin `#`, el selector no resuelve el modal y el botón "Buscar Persona" no abre nada.

## Cambios aplicados (TDD strict)

### 1. Red — Test que falla

Modificación de `tests/SGV.Tests/Web/Tests/Issue226CreatePageTests.cs` (regex `#?` → `#`):

```diff
-            $@"data-bs-target\s*=\s*""\s*#?\s*{Regex.Escape(modalId)}\s*""",
+            $@"data-bs-target\s*=\s*""\s*#{Regex.Escape(modalId)}\s*""",
```

Resultado: ambos tests `Get_UsuarioCrear_RenderizaModalYEmptyStateSinHidden` y `Get_OcupacionCrear_RenderizaModalYEmptyStateSinHidden` fallan (Expected `data-bs-target="#<id>"`, actual `<id>`).

### 2. Green — Fix de producción

Modificación de `src/SGV.Web/Pages/Shared/Partials/_PersonaCard.cshtml` línea 245 (botón "Buscar Persona" del empty state, Caso 6):

```diff
-        <button type="button" class="btn btn-outline-primary"
-                data-usuario-persona-buscar data-bs-toggle="modal"
-                data-bs-target="@modalId">
+        <button type="button" class="btn btn-outline-primary"
+                data-usuario-persona-buscar data-bs-toggle="modal"
+                data-bs-target="#@modalId">
```

Resultado: los 2 tests del Issue226CreatePageTests ahora pasan.

### 3. Refactor — Sin cambios adicionales

El fix es 1 carácter (`#`). No requiere refactor.

## Validación ejecutada

- `dotnet test .../SGV.Tests --filter "FullyQualifiedName~Issue226"` → **3/3 PASS** (1 regression test + 2 create page tests).
- `dotnet test .../SGV.Tests --filter "FullyQualifiedName~SGV.Tests.Web"` → **1341/1341 PASS, 0 FAIL, 0 SKIP**. Sin regresiones en la suite Web completa.

## Archivos modificados

- `src/SGV.Web/Pages/Shared/Partials/_PersonaCard.cshtml` — 1 línea (línea 245: agregar `#` antes de `@modalId`).
- `tests/SGV.Tests/Web/Tests/Issue226CreatePageTests.cs` — 1 línea (regex estricta con `#`).
- `tests/SGV.Tests/Web/Tests/Issue226RegressionTests.cs` — 1 archivo nuevo (~50 líneas, regression test del contrato del Caso 6).
- `openspec/changes/fix-buscar-persona-create-issue-226/exploration.md` — creado y corregido (refutación "3 líneas" → 1 línea).

## Pendiente

1. **Commit** con mensaje conventional + descripción del cambio.
2. **Push** a `origin/feat/fix-buscar-persona-create-issue-226`.
3. **PR** desde la rama hacia `develop` con cuerpo describiendo el bug + fix + verificación.
4. **`sdd-verify`** adversarial para confirmar fix end-to-end.
5. **`sdd-archive`** para cerrar el change y mergear deltas al spec baseline.

## Riesgos identificados

- **Riesgo bajo.** Cambio de 1 carácter en HTML attribute. No toca JS, ni API, ni persistencia, ni contratos.
- **Cobertura post-fix:** los tests del Caso 6 blindan el `data-bs-target` con `#`; los tests de los casos 4 y 5 (botones "Cambiar" en Edit, que ya tenían `#`) siguen pasando sin cambios.
- **Back-compat:** ninguno. El atributo `data-bs-target` se vuelve un selector CSS válido para Bootstrap 5; cualquier consumidor HTML que use el mismo patrón queda avisado por la corrección del test.

## Próximo paso

`sdd-verify` (validación adversarial) y luego `sdd-archive`.
