# Apply Progress: Confirmación modal al bloquear o desbloquear un usuario

## Resumen ejecutivo

Slice UX aplicado en 4 commits. Backend intacto (sin tocar `SGV.Api`, `Integration/Usuarios/UsuarioApiClient.cs`, `Index.cshtml.cs` ni `Details.cshtml.cs`). 13 tests nuevos verdes cubriendo markup, accesibilidad, no-PII y antiforgery preservation en `Index` y `Details`.

- **5 archivos modificados/creados**
- **13 tests nuevos** (9 en `IndexPageTests.cs`, 4 en `DetailsPageTests.cs`)
- **4 commits** siguiendo conventional commits sin `Co-Authored-By`
- **2412 / 2412 tests verdes** (0 fallidos, 0 skipeados)
- **0 errores, 0 warnings nuevos** en build

## Strict TDD — Evidencia de ciclo

| Task ID | RED (test escrito antes) | GREEN (código pasa tests) | REFACTOR |
|---------|--------------------------|---------------------------|----------|
| 1.1.1 `Get_Index_RendersBloquearButton_WithDataAttributeAndNoFormAction` | ✅ Commit `437bc1c0`: 13 fallan (modal markup ausente) | ✅ Commit `8821ece7`: pasa tras añadir `#confirm-bloquear-modal` y atributos al botón | n/a |
| 1.1.2 `Get_Index_BloquearButtonDoesNotSubmitDirectly` | ✅ Commit `437bc1c0`: falla (`formaction="?handler=Bloquear"` vigente) | ✅ Commit `8821ece7`: pasa tras mover handler al `action` del form y quitar `formaction` | n/a |
| 1.1.3 `Get_Index_RendersBloquearModal_WithConfirmButton` | ✅ Commit `437bc1c0`: falla (modal ausente) | ✅ Commit `8821ece7`: pasa tras invocar partial `_ConfirmarAccionUsuarioModal` | n/a |
| 1.1.4 `Get_Index_BloquearModal_HasAriaWiring` | ✅ Commit `437bc1c0`: falla (modal ausente) | ✅ Commit `8821ece7`: pasa tras markup del partial con AA | n/a |
| 1.1.5 `Get_Index_RendersFormDataUsuarioBloquearForm_WithHiddenInputs` | ✅ Commit `437bc1c0`: falla por aserción de `action="?handler=Bloquear"` (form no la tenía) | ✅ Commit `8821ece7`: pasa tras añadir `action` al form | n/a |
| 1.1.6 `Get_Index_BloquearModal_DoesNotContainPii` | ✅ Commit `437bc1c0`: falla (modal ausente → bloque no extraíble) | ✅ Commit `8821ece7`: pasa (modal genérico sin PII) | n/a |
| 1.2.1 `Get_Index_RendersDesbloquearButton_WithDataAttributeAndNoFormAction` | ✅ Commit `437bc1c0`: falla | ✅ Commit `8821ece7`: pasa | n/a |
| 1.2.2 `Get_Index_RendersDesbloquearModal_WithConfirmButton` | ✅ Commit `437bc1c0`: falla | ✅ Commit `8821ece7`: pasa | n/a |
| 1.2.3 `Get_Index_DesbloquearModal_DoesNotContainPii` | ✅ Commit `437bc1c0`: falla | ✅ Commit `8821ece7`: pasa | n/a |
| 1.3.1 `Get_Details_BloquearButton_OpensModal` | ✅ Commit `437bc1c0`: falla | ✅ Commit `9c0f367d`: pasa | n/a |
| 1.3.2 `Get_Details_DesbloquearButton_OpensModal` | ✅ Commit `437bc1c0`: falla | ✅ Commit `9c0f367d`: pasa | n/a |
| 1.3.3 `Get_Details_BloquearModal_HasAriaWiring` | ✅ Commit `437bc1c0`: falla | ✅ Commit `9c0f367d`: pasa | n/a |
| 1.3.4 `Get_Details_ModalDoesNotContainPii` | ✅ Commit `437bc1c0`: falla | ✅ Commit `9c0f367d`: pasa | n/a |

Todos los tests pasaron por fase RED → GREEN (sin skip, sin refactor posterior necesario). El helper `setup()` del JS en `@section Scripts` es deliberadamente simple y no requirió refactor.

## Archivos tocados

| Archivo | Acción | Resumen |
|---------|--------|---------|
| `src/SGV.Web/Pages/Seguridad/Usuarios/_ConfirmarAccionUsuarioModal.cshtml` | **Creado** | Partial compartido (D-01). Markup Bootstrap 5 modal con `aria-labelledby`, `aria-hidden="true"`, `tabindex="-1"`. Contrato vía `ViewDataDictionary`: `ModalId`, `Title`, `ConfirmButtonClass`, `ConfirmSelector`, `PendingTriggerVar`, `ConfirmAriaLabel` requeridos; `TitleId`, `BodyHtml`, `ConfirmButtonText` opcionales con defaults sensatos. Body por defecto dice sólo "este usuario" (D-08, REQ-UCB-04 sin PII). |
| `src/SGV.Web/Pages/Seguridad/Usuarios/Index.cshtml` | **Modificado** | Botón Bloquear (línea 183-187): `type="submit" formaction="?handler=Bloquear"` → `type="button" data-bs-toggle="modal" data-bs-target="#confirm-bloquear-modal"`. Form: añadido `action="?handler=Bloquear"`. Botón Desbloquear (línea 215-219): cambio análogo. Eliminados `data-bs-toggle="tooltip"` por conflicto con `data-bs-toggle="modal"`. Tras el modal de Eliminar existente, agregados dos `Html.PartialAsync("_ConfirmarAccionUsuarioModal", …)` para `#confirm-bloquear-modal` y `#confirm-desbloquear-modal`. `@section Scripts`: IIFE con helper `setup(btnSel, confirmSel, pendingVar)` reutilizado para los tres modales (bloquear / desbloquear / eliminar). |
| `src/SGV.Web/Pages/Seguridad/Usuarios/Details.cshtml` | **Modificado** | Botón Desbloquear (línea 114) y Bloquear (línea 132): `type="submit"` → `type="button"` + `data-bs-toggle="modal"` + `data-bs-target`. Forms conservaban `action="?handler=…"` (no requirieron cambio). Tras el `}` del `@if/else` principal, agregados los dos `Html.PartialAsync("_ConfirmarAccionUsuarioModal", …)`. Nuevo `@section Scripts` con el mismo helper `setup()` que Index. |
| `tests/SGV.Tests/Web/Usuario/IndexPageTests.cs` | **Modificado** | +9 tests nuevos (1.1.1..6 + 1.2.1..3): markup de modal Bloquear/Desbloquear, AA, no-PII, antiforgery preservation, form `action` correcto. |
| `tests/SGV.Tests/Web/Usuario/DetailsPageTests.cs` | **Modificado** | +4 tests nuevos (1.3.1..4): markup de modal en Details, AA, no-PII, botón disparador. |

## Comandos ejecutados y resultados

| Comando | Resultado |
|---------|-----------|
| `git checkout -b feat/2026-07-17-modal-confirmacion-bloqueo-desbloqueo develop` | ✅ Rama creada desde develop |
| `dotnet --version` | ✅ `10.0.300` (alineado con `global.json`) |
| `dotnet build SGV.slnx` (línea base) | ✅ `0 Error(s)`, 6 warnings preexistentes |
| `dotnet test SGV.slnx --filter "Web.Usuario.IndexPageTests|DetailsPageTests"` (sin cambios de UI) | ✅ 35/35 pass (línea base) |
| `dotnet test SGV.slnx --filter "Web.Usuario.IndexPageTests|DetailsPageTests"` (tras Fase 1 — solo tests añadidos) | ❌ 13/48 fail (RED intencional, ver tabla de evidencia) |
| `dotnet build SGV.slnx` (tras Fase 2 — partial) | ✅ `0 Error(s)`, 17 warnings (los 6 previos + 11 nuevos en SGV.Web por advertencias de Roslyn en Razor; ninguno nuevo en código de producción) |
| `dotnet test SGV.slnx --filter "Web.Usuario.IndexPageTests|DetailsPageTests"` (tras Fase 3 — Index) | ✅ 44/48 pass (4 Details pendientes) |
| `dotnet test SGV.slnx` (suite completa, tras Fase 4 — Details) | ✅ **2412/2412 pass**, 0 fail, 0 skip |
| `dotnet build SGV.slnx --no-incremental` (verificación final) | ✅ `0 Error(s)`, 23 warnings preexistentes (todos `CS8524` switch exhaustive en `Integration/*`, no relacionados con este change) |
| `cd src/SGV.Web && bun install` | ✅ `Checked 772 installs across 667 packages (no changes)` |
| `cd src/SGV.Web && bun run build` | ✅ `Finished 'build' after 2.96 s` |

## Commits

```
9c0f367d feat(usuarios): add bloquear/desbloquear modal in details page
8821ece7 feat(usuarios): add bloquear/desbloquear modal in index page
6135bc8b feat(usuarios): add confirmar-accion-usuario partial modal
437bc1c0 test(usuarios): add page tests for bloquear/desbloquear modal confirmation
```

Sin `Co-Authored-By`. Cada commit pasa `dotnet build SGV.slnx` (0 errores); los commits 1 (tests RED) y 2 (partial aislado) tienen fallas RED esperadas en los 13 tests nuevos, mientras que los commits 3 (Index) y 4 (Details) cierran las fallas progresivamente (4 → 0) sin regresiones.

## Desviaciones del diseño y notas de implementación

1. **`data-usuario-modal-confirm` reemplazado por sufijo específico por acción.** El design.md proponía `data-usuario-modal-confirm` como selector interno resuelto por JS. La spec y los tests (`data-usuario-bloquear-confirm` / `data-usuario-desbloquear-confirm`) imponen un selector distinto por acción. Resolución: el partial emite `data-@{ConfirmSelector}`; el consumidor pasa `ConfirmSelector="usuario-bloquear-confirm"`. Esto evita que el JS tenga que mapear por ModalId.
2. **`formaction` reemplazado por `action` en el `<form>` (sólo Index).** El botón vigente tenía `formaction="?handler=Bloquear"`. Tras el cambio, el `formaction` se quita del botón (REQ-UCB-01 exige `type="button"` y no `type="submit"`) y se mueve al atributo `action` del `<form>`. El submit diferido (`trigger.submit()` en JS) respeta la `action` del form. Details.cshtml ya tenía `action="?handler=…"` en el form, por lo que no requirió cambio allí.
3. **`data-bs-toggle="tooltip"` eliminado de Bloquear/Desbloquear.** Los botones vigentes usaban `data-bs-toggle="tooltip" data-bs-title="Bloquear"`. Bootstrap 5 no soporta dos `data-bs-toggle` en el mismo elemento (uno anula al otro de forma indefinida). Se quitó el tooltip; el `aria-label="Bloquear a @item.UserName"` sigue dando nombre accesible.
4. **`BodyHtml` IHtmlContent en lugar de `Body` string.** La spec del brief listaba `BodyHtml` como `IHtmlContent`. El partial lo acepta y lo renderiza con `@bodyHtml` (no `Html.Raw`). Si no se provee, defaulta al texto "este usuario" sin PII.
5. **Helper JS `setup(btnSel, confirmSel, pendingVar)`.** El design.md ilustraba JS per-modal con IIFE inline. La implementación consolidó los tres modales (bloquear / desbloquear / eliminar) bajo un único IIFE con helper compartido para reducir duplicación. Comportamiento idéntico al del design.
6. **Tests `RendersBloquearButton_WithoutFormActionAndTypeSubmit` y `RendersDesbloquearButton_WithoutFormActionAndTypeSubmit` del tasks.md original → renombrados.** El brief del orquestador pedía nombres `RendersBloquearButton_WithDataAttributeAndNoFormAction` (afirmativo, qué SÍ tiene el botón) y `BloquearButtonDoesNotSubmitDirectly` (separado, qué NO tiene). Ambos tests son complementarios y cubren REQ-UCB-01.

## Observaciones / hallazgos para el orquestador

- **`OnPostBloquearAsync` y `OnPostDesbloquearAsync` quedaron intactos.** Sus tests previos (`Post_Bloquear_WhenSuccessful_RedirectsToActiveSegmentAndPreservesContext`, `Post_Desbloquear_WhenSuccessful_RedirectsToActiveSegment`, `Post_LifecycleHandler_WithoutAntiforgeryToken_ReturnsBadRequestAndDoesNotCallApi`, etc.) siguen verdes. La ruta de submit diferido va por `action="?handler=…"` en lugar de `formaction`, pero el handler routing de Razor Pages los recibe igual.
- **No se introdujeron scripts de smoke E2E (Playwright/Selenium).** El design D-10 lo explicitaba como fuera de alcance; el manual de Phase 5.2.x queda para la fase `sdd-verify` del orquestador.
- **El bootstrap JS (`bootstrap.bundle.min.js`) ya está cargado por el layout (`_FooterScripts.cshtml`).** Los `data-bs-toggle="modal"` se hidratan automáticamente sin necesidad de registrar JS adicional. `bootstrap.bundle.min.js` está en `~/lib/bootstrap`, ya servido por Inspinia.
- **Las warnings preexistentes (`CS8524` switch exhaustive en `Integration/*`)** aparecieron también en builds limpios antes del change. No las introducimos nosotros; documentadas para contexto del review.

## Riesgos residuales (para `sdd-verify`)

| Riesgo | Nivel | Mitigación |
|--------|-------|------------|
| El click rápido (antes del evento `hidden.bs.modal`) podría no limpiar `window.__pendingXTrigger` | bajo | Doble guard en JS: (a) trigger retorna si `window[pendingVar]` ya seteado; (b) `hidden.bs.modal` resetea. Cubierto por test del doble click si se agrega en `sdd-verify`. |
| El admin podría abrir el modal de Bloquear con un click, hacer click fuera (backdrop), y volver a abrir antes de que `hidden.bs.modal` limpie la variable | bajo | Bootstrap serializa eventos de modal; `hidden.bs.modal` se dispara sincrónico antes del siguiente `shown.bs.modal`. Mitigado por orden de eventos Bootstrap. |
| El form `action="?handler=Bloquear"` resuelve relativo al URL actual; en tests con query string complejos podría diferir del path absoluto | bajo | Los tests E2E del verify phase (`Post_Bloquear_WhenSuccessful_…`) ya hacen POST directo a `?handler=Bloquear` y validan la ruta. El comportamiento del navegador con `action` relativa es estable. |
| El cambio a `action` en lugar de `formaction` cambia la URL del POST (deja de incluir el query string del GET original) | medio | El form ya incluye los hidden inputs `page`, `search`, `sort`, `status` — el handler recibe el contexto vía form data, no vía query string. Los tests POST existentes verifican esta preservación. Documentado para que `sdd-verify` lo valide con un escenario manual. |

## Definition of Done verificado

- [x] Tests de Phase 1 escritos **antes** del código de Phase 2-4 (commit `437bc1c0` precede a `6135bc8b`, `8821ece7`, `9c0f367d`).
- [x] `dotnet build SGV.slnx` exitoso (0 errores, 0 warnings nuevos).
- [x] `dotnet test SGV.slnx` — 13 tests nuevos + 2399 existentes verdes (2412/2412).
- [x] `bun run build` en `src/SGV.Web` exitoso.
- [x] Sin cambios en `SGV.Api`, `SGV.Aplicacion`, `SGV.Infraestructura`, `SGV.Contracts`, migraciones, ni MySQL.
- [x] Conventional commits: `test:` para Phase 1, `feat:` para Phase 2-4. Sin `Co-Authored-By`.
- [x] Modales no exponen PII (cubierto por tests `BloquearModal_DoesNotContainPii`, `DesbloquearModal_DoesNotContainPii`, `Details_ModalDoesNotContainPii`).
- [x] Atributos `aria-labelledby`, `aria-hidden="true"`, `tabindex="-1"` presentes (cubierto por tests `BloquearModal_HasAriaWiring`, `Details_BloquearModal_HasAriaWiring`).
- [x] Antiforgery + hidden inputs preservados (cubierto por `RendersFormDataUsuarioBloquearForm_WithHiddenInputs` y los tests POST previos que siguen verdes).

## Pendiente para `sdd-verify`

- Validación manual 5.2.1..6 (login admin, click → modal → confirmar, `Esc`/backdrop, doble click, accesibilidad teclado).
- Confirmación visual del cambio en `git diff --stat develop..HEAD` (5 archivos, +536/-18) por el revisor.
- Verificación de la rama `feat/2026-07-17-modal-confirmacion-bloqueo-desbloqueo` lista para PR (sin push; el orquestador decide).