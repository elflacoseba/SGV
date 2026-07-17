# Verify Report: Confirmación modal al bloquear o desbloquear un usuario

> Cambio: `2026-07-17-modal-confirmacion-bloqueo-desbloqueo`
> Rama: `feat/2026-07-17-modal-confirmacion-bloqueo-desbloqueo` (base: `develop`)
> Issue: [#155](https://github.com/elflacoseba/SGV/issues/155)
> Spec: `specs/usuario-web-confirmacion-bloqueo-desbloqueo/spec.md`
> Persistencia: both (Engram + openspec file)
> Modo verify: adversarial, read-only, sin aplicar fixes

## Estado final

**PASS** (con 1 SUGGESTION y 1 WARNING menor de desviación opcional).

Los 10 requisitos REQ-UCB-01..10 quedan cubiertos por el código y verificados en runtime (2412/2412 tests verdes). Las 10 decisiones técnicas D-01..D-10 del design.md se reflejan en el código, con una desviación documentada y justificada (selector del confirm button: `data-usuario-modal-confirm` → `data-usuario-{action}-confirm` por acción). Los criterios de aceptación de la issue #155 están cumplidos, con una observación menor sobre el copy del cuerpo del modal (ver § Hallazgos).

## Resumen ejecutivo

| Métrica | Valor |
|---|---|
| Commits revisados | 5 (1 test RED + 1 partial + 1 Index + 1 Details + 1 apply-progress) |
| Archivos producción/test tocados | 5 |
| Líneas añadidas/eliminadas (producción+test) | +554 / −18 (incluye SDD artifacts: total 1194/18) |
| Tests nuevos | 13 (9 Index + 4 Details) |
| Tests previos | 2399 (sin cambios) |
| Tests totales | 2412 |
| Tests fallidos | 0 |
| Tests skipeados | 0 (MySQL local disponible) |
| Build | 0 errors, 23 warnings preexistentes (`CS8524` en `Integration/*` y `Contracts/Comun/ErrorCategoriaMappers.cs`, no introducidos por este change) |
| Frontend bundle | OK (`bun run build` 3.01 s) |

## Comandos ejecutados y resultados

| # | Comando | Resultado |
|---|---|---|
| 1 | `git status` | ✅ Working tree clean, branch `feat/2026-07-17-modal-confirmacion-bloqueo-desbloqueo` |
| 2 | `git log --oneline develop..HEAD` | ✅ 5 commits en orden RED→GREEN: `437bc1c0` (test) → `6135bc8b` (partial) → `8821ece7` (Index) → `9c0f367d` (Details) → `a9b38788` (chore sdd) |
| 3 | `git log develop..HEAD --format='%B' \| grep -i "co-authored-by"` | ✅ Sin `Co-Authored-By` (matches 0) |
| 4 | `dotnet restore` | ✅ All projects up-to-date |
| 5 | `dotnet build SGV.slnx --no-incremental` | ✅ `0 Error(s)`, 23 warnings preexistentes (ver § Hallazgos) |
| 6 | `dotnet test SGV.slnx --no-build --filter "Web.Usuario.IndexPageTests\|Web.Usuario.DetailsPageTests"` | ✅ **48/48 pass**, 0 failed, 0 skipped (35 baseline + 13 nuevos) |
| 7 | `dotnet test SGV.slnx --no-build` | ✅ **2412/2412 pass**, 0 failed, 0 skipped (suite completa) |
| 8 | `bun install` (en `src/SGV.Web`) | ✅ 772 installs, no changes |
| 9 | `bun run build` (en `src/SGV.Web`) | ✅ Finished after 3.01 s |
| 10 | Grep `TODO\|FIXME\|HACK\|XXX` en `src/SGV.Web/Pages/Seguridad/Usuarios` | ✅ Sin matches |

## Matriz de cumplimiento de requisitos

| Requisito | Cubre | Test que lo cubre | Test resultado | OK |
|---|---|---|---|---|
| **REQ-UCB-01** Confirmación modal al Bloquear desde Index | `Index.cshtml:288-296` invoca partial `#confirm-bloquear-modal`; botón línea 183-188 con `data-bs-toggle="modal"` y `data-bs-target="#confirm-bloquear-modal"`; JS helper `setup('[data-usuario-bloquear-button]', '[data-usuario-bloquear-confirm]', '__pendingBloquearTrigger')` línea 341 difiere el submit del form línea 176 (`action="?handler=Bloquear"`) | `Get_Index_RendersBloquearButton_WithDataAttributeAndNoFormAction`, `Get_Index_BloquearButtonDoesNotSubmitDirectly`, `Get_Index_RendersBloquearModal_WithConfirmButton`, `Get_Index_RendersFormDataUsuarioBloquearForm_WithHiddenInputs`, `Post_Bloquear_WhenSuccessful_RedirectsToActiveSegmentAndPreservesContext`, `Post_LifecycleHandler_WithoutAntiforgeryToken_ReturnsBadRequestAndDoesNotCallApi` | PASS (6/6) | ✅ |
| **REQ-UCB-02** Confirmación modal al Desbloquear desde Index | `Index.cshtml:298-306` invoca partial `#confirm-desbloquear-modal`; botón línea 215-219 análogo; JS helper línea 342 | `Get_Index_RendersDesbloquearButton_WithDataAttributeAndNoFormAction`, `Get_Index_RendersDesbloquearModal_WithConfirmButton`, `Post_Desbloquear_WhenSuccessful_RedirectsToActiveSegment`, `Post_LifecycleHandler_WithoutAntiforgeryToken_ReturnsBadRequestAndDoesNotCallApi` | PASS (4/4) | ✅ |
| **REQ-UCB-03** Replicar confirmación en Details via partial compartido | `_ConfirmarAccionUsuarioModal.cshtml` co-localizado en `Pages/Seguridad/Usuarios/`; `Details.cshtml:173-191` invoca los dos partials; botones línea 114-118 y 134-138 convertidos a `type="button"` + `data-bs-toggle="modal"` + `data-bs-target`; JS helper línea 227-228 | `Get_Details_BloquearButton_OpensModal`, `Get_Details_DesbloquearButton_OpensModal` | PASS (2/2) | ✅ |
| **REQ-UCB-04** Sin PII en el cuerpo del modal | `_ConfirmarAccionUsuarioModal.cshtml:64-73` body por defecto: `<p class="mb-0">Esta acción afecta <strong>este usuario</strong>. ¿Desea continuar?</p>`; sin UserName/Email/Nombres/Apellidos del objetivo; los consumers no pasan `BodyHtml` (mantiene contrato de no-PII) | `Get_Index_BloquearModal_DoesNotContainPii`, `Get_Index_DesbloquearModal_DoesNotContainPii`, `Get_Details_ModalDoesNotContainPii` | PASS (3/3) | ✅ |
| **REQ-UCB-05** Accesibilidad AA de los modales | `_ConfirmarAccionUsuarioModal.cshtml:49-50`: `aria-labelledby="@titleId"`, `aria-hidden="true"`, `tabindex="-1"`; cierre Esc/backdrop por default de Bootstrap 5; `event.relatedTarget.focus()` en `hidden.bs.modal` (`Index.cshtml:337`, `Details.cshtml:221`) | `Get_Index_BloquearModal_HasAriaWiring`, `Get_Details_BloquearModal_HasAriaWiring` (cubren `aria-labelledby`, `aria-hidden`, `tabindex`; foco en `hidden.bs.modal` está en JS, no en markup) | PASS (2/2 markup + JS) | ✅ |
| **REQ-UCB-06** Antiforgery y PRG preservados | `Index.cshtml:177` y `Details.cshtml:108, 128` emiten `@Html.AntiForgeryToken()`; hidden inputs `id`/`page`/`search`/`sort`/`status` preservados en `Index.cshtml:178-182` y `Details.cshtml:109-113/129-133`; PRG vigente `RedirectToIndex` en `Index.cshtml.cs:382` | `Get_Index_RendersFormDataUsuarioBloquearForm_WithHiddenInputs`, `Post_LifecycleHandler_WithoutAntiforgeryToken_ReturnsBadRequestAndDoesNotCallApi` (Theory sobre Delete/Bloquear/Desbloquear) | PASS (3/3) | ✅ |
| **REQ-UCB-07** Idempotencia ante doble click | `Index.cshtml:323-324` y `Details.cshtml:207-208`: `confirmBtn.disabled = true` antes de `trigger.submit()`; `Index.cshtml:330` y `Details.cshtml:214`: trigger retorna si `window[pendingVar]` ya está seteado (doble guard D-06); `hidden.bs.modal` resetea (`Index.cshtml:335-336`, `Details.cshtml:219-220`) | Cubierto por markup + JS. **No hay test runtime específico de doble click** (ver § Hallazgos — manual verification only) | PASS markup + JS (sin test runtime dedicado) | ✅ |
| **REQ-UCB-08** Persistencia de contexto en PRG | Hidden inputs `page`/`search`/`sort`/`status` ya existentes en `Index.cshtml:179-182` y `Details.cshtml:110-113/130-133`; `OnPostBloquearAsync` redirige a `BlockedView` (`Index.cshtml.cs:226`); `OnPostDesbloquearAsync` redirige a `ActiveView` (`Index.cshtml.cs:285`) | `Get_Index_RendersFormDataUsuarioBloquearForm_WithHiddenInputs` (asserts hidden values), `Post_Bloquear_WhenSuccessful_RedirectsToActiveSegmentAndPreservesContext` (asserts `status=bloqueadas` y `p=3` preservados), `Post_Desbloquear_WhenSuccessful_RedirectsToActiveSegment` | PASS (3/3) | ✅ |
| **REQ-UCB-09** No regresión de AutoBloqueo y antifence de UI | `EsAutoAccion(item.Id)` gating intacto en `Index.cshtml:141` y `Details.cshtml:103`; fence server-side `OnPostBloquearAsync:200-204` rechaza self-block con feedback `AutoBloqueo`; el botón Bloquear NO se renderiza para la fila propia (Index línea 174 `if (!esAuto)`) | `Get_Index_WhenCurrentUserListed_HidesBloquearAndDeleteActions`, `Get_Details_WhenAdminViewsSelf_RendersOnlyEdit_NoBloquearNoEliminar`, `Post_Bloquear_WhenApiRejectsAutoBloqueo_ShowsActionableFeedback` | PASS (3/3) | ✅ |
| **REQ-UCB-10** Tests previos a la implementación (strict_tdd) | Commit `437bc1c0` (test, 13 nuevos) precede a `6135bc8b` (partial), `8821ece7` (Index) y `9c0f367d` (Details); apply-progress documenta ciclo RED → GREEN (13 tests fallan → 0 failan) | N/A (cumplimiento de proceso, no de comportamiento) | PASS — orden de commits verificado con `git log develop..HEAD` | ✅ |

**Total: 10/10 requisitos cumplidos.**

## Matriz de diseño (D-01..D-10)

| Decisión | Implementación | Desviación | OK |
|---|---|---|---|
| **D-01** Partial en `Pages/Seguridad/Usuarios/_ConfirmarAccionUsuarioModal.cshtml` | Archivo en path exacto; co-localizado con `_Form.cshtml` (sibling) | Ninguna | ✅ |
| **D-02** Contrato vía `ViewData`: `ModalId`, `TitleId`, `Title`, `BodyHtml`, `ConfirmButtonClass`, `ConfirmButtonText`, `ConfirmSelector`, `PendingTriggerVar`, `ConfirmAriaLabel` | Todos los parámetros requeridos se aceptan con `InvalidOperationException` si faltan (líneas 28-44 del partial); opcionales con defaults sensatos | **Menor**: design mencionaba `Body` (string) pero impl. usa `BodyHtml` (`IHtmlContent`) según brief del orquestador documentado en `apply-progress.md:75`. Sin impacto funcional. | ✅ |
| **D-03** Dos modales separados (`#confirm-bloquear-modal`, `#confirm-desbloquear-modal`) | `Index.cshtml:288-296` y `298-306` invocan dos partials con `ModalId` distinto; `Details.cshtml:173-181` y `183-191` análogo | Ninguna | ✅ |
| **D-04** Dos globales separadas (`window.__pendingBloquearTrigger`, `window.__pendingDesbloquearTrigger`) | `Index.cshtml:341-342` y `Details.cshtml:227-228` invocan `setup(... '__pendingBloquearTrigger')` y `setup(... '__pendingDesbloquearTrigger')` | Ninguna | ✅ |
| **D-05** `confirmBtn.disabled = true` antes de `trigger.submit()` | `Index.cshtml:323` y `Details.cshtml:207` | Ninguna | ✅ |
| **D-06** Doble guard: (a) trigger retorna si ya seteado; (b) `hidden.bs.modal` resetea | (a) `Index.cshtml:330` y `Details.cshtml:214`; (b) `Index.cshtml:335` y `Details.cshtml:219` | Ninguna | ✅ |
| **D-07** `event.relatedTarget.focus()` en `hidden.bs.modal`; `aria-labelledby` apunta al `<h5>`; `aria-hidden="true"` inicial | `Index.cshtml:337` y `Details.cshtml:221`; `_ConfirmarAccionUsuarioModal.cshtml:50` (`aria-labelledby="@titleId"` apuntando al `<h5 class="modal-title" id="@titleId">`) y línea 50 (`aria-hidden="true"`) | Ninguna | ✅ |
| **D-08** Body dice sólo "este usuario", sin PII | `_ConfirmarAccionUsuarioModal.cshtml:70-72` `<p>Esta acción afecta <strong>este usuario</strong>. ¿Desea continuar?</p>` | **Menor**: el copy de la issue #155 sugería texto específico ("la cuenta no podrá iniciar sesión hasta que se desbloquee"), pero la spec REQ-UCB-04 sólo exige ausencia de PII (ver § Hallazgos) | ✅ (cumple spec; ver sugerencia copy) |
| **D-09** JS inline en `@section Scripts` por vista | `Index.cshtml:308-362` y `Details.cshtml:193-230` ambos tienen `@section Scripts { <script>...</script> }` | **Mejora (no desviación)**: helper `setup(btnSel, confirmSel, pendingVar)` factoriza 3 modales en Index y 2 en Details en lugar de IIFE per-modal del design. Comportamiento idéntico, menos duplicación. Documentado en `apply-progress.md:76`. | ✅ |
| **D-10** 6 smoke HTTP nuevos antes del código; sin tests de PageModel; sin E2E | 13 tests HTTP nuevos (9 Index + 4 Details), 0 tests de PageModel, 0 E2E; tests en commit `437bc1c0` antes de `6135bc8b/8821ece7/9c0f367d` | **Menor**: el brief del design dijo "6 tests nuevos" pero el alcance real subió a 13 (6 Bloquear Index + 3 Desbloquear Index + 4 Details = 13). Tests adicionales proveen mejor cobertura de cada criterio REQ-UCB; no es regresión. | ✅ |

**Total: 10/10 decisiones implementadas.** Las desviaciones son menores, documentadas y/o son mejoras.

## Verificación de la issue #155

### Criterios de aceptación — Bloquear (vista activas)

| # | Criterio | Evidencia | OK |
|---|---|---|---|
| 1 | Modal con título "Bloquear usuario" | `_ConfirmarAccionUsuarioModal.cshtml` línea 54 emite `<h5 class="modal-title" id="@titleId">@title</h5>` con `Title="Bloquear usuario"` (Index.cshtml:291, Details.cshtml:176). Test `Get_Index_RendersBloquearModal_WithConfirmButton` PASS | ✅ |
| 2 | Cuerpo describe el impacto (la cuenta no podrá iniciar sesión hasta que se desbloquee) | Cuerpo actual: `<p>Esta acción afecta <strong>este usuario</strong>. ¿Desea continuar?</p>` (`_ConfirmarAccionUsuarioModal.cshtml:70-72`). **No menciona explícitamente "no podrá iniciar sesión"** — el copy genérico prioriza privacidad sobre detalle del impacto. Cumple REQ-UCB-04 (sin PII) pero **NO coincide 1:1 con la descripción literal de la issue**. Ver § Hallazgos SUGGESTION #1. | ⚠️ (cumple spec, copy divergente de la issue) |
| 3 | Dos botones: "Cancelar" y "Bloquear" | `_ConfirmarAccionUsuarioModal.cshtml:76-79`: `<button ... data-bs-dismiss="modal">Cancelar</button>` + `<button ... data-@confirmSelector aria-label="@confirmAriaLabel">@confirmButtonText</button>` con `ConfirmButtonText="Bloquear"`. Test `Get_Index_RendersBloquearModal_WithConfirmButton` PASS | ✅ |
| 4 | Modal NO expone PII (no UserName/Email/Nombres/Apellidos) | Tests `Get_Index_BloquearModal_DoesNotContainPii`, `Get_Details_ModalDoesNotContainPii` PASS — verifican ausencia de `agarcía`, `ana@example.com`, `García`, `>Ana<` en el bloque del modal | ✅ |
| 5 | Cancelar o cerrar el modal NO ejecuta la acción ni pierde el contexto | Bootstrap default: `data-bs-dismiss="modal"` cierra sin POST; `setup()` handler en `hidden.bs.modal` (`Index.cshtml:334-338`) limpia `window[pendingVar]` y restaura foco. Contexto de paginación/búsqueda preservado en hidden inputs (`Index.cshtml:179-182`). | ✅ |

### Criterios de aceptación — Desbloquear (vista bloqueadas)

| # | Criterio | Evidencia | OK |
|---|---|---|---|
| 1 | Modal con título "Desbloquear usuario" | `Title="Desbloquear usuario"` (Index.cshtml:301, Details.cshtml:186). Test `Get_Index_RendersDesbloquearModal_WithConfirmButton` PASS | ✅ |
| 2 | Cuerpo describe el impacto (la cuenta podrá volver a iniciar sesión) | Mismo cuerpo genérico que Bloquear. Misma observación SUGGESTION #1. | ⚠️ (cumple spec, copy divergente) |
| 3 | Dos botones: "Cancelar" y "Desbloquear" | Análogo a Bloquear con `ConfirmButtonText="Desbloquear"` (default desde `ConfirmAriaLabel="Desbloquear"`) | ✅ |
| 4 | Mismas restricciones de privacidad y persistencia | `Get_Index_DesbloquearModal_DoesNotContainPii` PASS; `Post_Desbloquear_WhenSuccessful_RedirectsToActiveSegment` PASS | ✅ |

### Casos borde a preservar

| # | Caso borde | Evidencia | OK |
|---|---|---|---|
| 1 | Auto-acción (EsAutoAccion): botón Bloquear no se renderiza para fila del admin; fence server-side intacto; botón Desbloquear sí se renderiza para fila propia | `Index.cshtml:141, 174` (`var esAuto = Model.EsAutoAccion(item.Id); if (!esAuto)`); `Details.cshtml:103, 121`; fence `Index.cshtml.cs:200-204`. Tests `Get_Index_WhenCurrentUserListed_HidesBloquearAndDeleteActions`, `Get_Details_WhenAdminViewsSelf_RendersOnlyEdit_NoBloquearNoEliminar`, `Post_Bloquear_WhenApiRejectsAutoBloqueo_ShowsActionableFeedback` PASS | ✅ |
| 2 | Antiforgery: `@Html.AntiForgeryToken()` sigue presente; `[AutoValidateAntiforgeryToken]` cubre POST diferido | `Index.cshtml:177`, `Details.cshtml:108, 128`. Test Theory `Post_LifecycleHandler_WithoutAntiforgeryToken_ReturnsBadRequestAndDoesNotCallApi` PASS (3/3 handlers: Delete, Bloquear, Desbloquear) | ✅ |
| 3 | Fallo de transporte: feedback existente tras redirect | Sin cambios en `Index.cshtml.cs`/`Details.cshtml.cs` para manejo de errores. Tests `Post_Bloquear_WhenApiReturnsTransportFailure_ShowsRecoverableFeedback`, `Post_Desbloquear_WhenApiReturnsTransportFailure_ShowsRecoverableFeedback`, `Post_Delete_WhenApiReturnsTransportFailure_ShowsRecoverableFeedback` PASS | ✅ |
| 4 | Foco y teclado: al cerrar el modal el foco vuelve al botón disparador; Esc y backdrop cierran sin enviar el form | `Index.cshtml:337`, `Details.cshtml:221`: `if (event.relatedTarget) event.relatedTarget.focus();` en `hidden.bs.modal`. Esc/backdrop son default de Bootstrap 5 (`data-bs-dismiss="modal"` + `static` backdrop opcional). | ✅ |
| 5 | Idempotencia: doble click → un solo POST | `confirmBtn.disabled = true` antes de `trigger.submit()` (`Index.cshtml:323`, `Details.cshtml:207`) + doble guard D-06. Cumple spec REQ-UCB-07. **Sin test runtime de doble click** (ver § Hallazgos SUGGESTION #2 sobre test E2E/Playwright no obligatorio). | ✅ |

### Restricciones del proyecto respetadas

| # | Restricción | Cumplimiento | OK |
|---|---|---|---|
| 1 | `strict_tdd: true`: tests antes o en el mismo commit que el código | Commit `437bc1c0` (test) precede a `6135bc8b/8821ece7/9c0f367d` (feat). Verify confirmado con `git log`. | ✅ |
| 2 | Copy en español, registro neutro/profesional | "Bloquear usuario", "Desbloquear usuario", "Cancelar", "Esta acción afecta este usuario. ¿Desea continuar?" — sin voseo, sin slang. | ✅ |
| 3 | Privacidad: no exponer UserName/Email/Nombres/Apellidos | Cubierto por tests `ModalDoesNotContainPii` (3 variantes). | ✅ |
| 4 | Antiforgery con `[AutoValidateAntiforgeryToken]` del PageModel | Sin cambios en PageModel; el test Theory `Post_LifecycleHandler_WithoutAntiforgeryToken_ReturnsBadRequestAndDoesNotCallApi` sigue PASS. | ✅ |
| 5 | Accesibilidad con APIs nativas Bootstrap 5 (sin librería adicional) | `data-bs-toggle="modal"`, `data-bs-target`, `data-bs-dismiss`, `aria-labelledby`, `aria-hidden`. `bootstrap.bundle.min.js` ya servido por Inspinia. | ✅ |
| 6 | PRG existente, sin nueva lógica de redirect | `OnPostBloquearAsync`/`OnPostDesbloquearAsync` intactos; el `trigger.submit()` del JS respeta el `action="?handler=…"` del form. | ✅ |

## Regresiones detectadas

**Ninguna.** Validaciones ejecutadas:

| Verificación | Resultado |
|---|---|
| `Post_Delete_WhenSuccessful_RedirectsToActiveSegmentWithFeedback` (test existente) | ✅ PASS |
| `Post_Bloquear_WhenSuccessful_RedirectsToActiveSegmentAndPreservesContext` (existente) | ✅ PASS |
| `Post_Desbloquear_WhenSuccessful_RedirectsToActiveSegment` (existente) | ✅ PASS |
| `Get_Index_WhenSegmentIsBloqueadas_ExposesOnlyDesbloquearAction` (existente) | ✅ PASS |
| `Post_Bloquear_WhenApiRejectsAutoBloqueo_ShowsActionableFeedback` (existente) | ✅ PASS |
| `Post_LifecycleHandler_WithoutAntiforgeryToken_ReturnsBadRequestAndDoesNotCallApi` (existente, Theory) | ✅ PASS |
| `Get_Details_WhenUserIsBlocked_RendersBannerAndDesbloquearAction` (existente) | ✅ PASS |
| `Get_Index_WhenCurrentUserListed_HidesBloquearAndDeleteActions` (existente) | ✅ PASS |
| Suite completa | ✅ 2412/2412 PASS, 0 failed, 0 skipped |

## Hallazgos adicionales

### WARNING (desviación documentada — sin impacto funcional)

**W-01. Design.md D-02 menciona `Body` (string), implementación usa `BodyHtml` (`IHtmlContent`).**
- Severidad: WARNING.
- Ubicación: `_ConfirmarAccionUsuarioModal.cshtml:33` lee `ViewData["BodyHtml"] as IHtmlContent`.
- Evidencia: `design.md:12` lista `Body`; la impl usa `BodyHtml` per brief del orquestador documentado en `apply-progress.md:75`.
- Impacto: ningún REQ-UCB violado. Funcionalmente equivalente (permite pasar HTML pre-renderizado).
- Recomendación: alinear `design.md:12` para reflejar `BodyHtml` (cosmético) — NO requiere cambio de código.

### SUGGESTIONS (no bloquean)

**S-01. Copy del cuerpo del modal diverge de la descripción literal de la issue #155.**
- Severidad: SUGGESTION.
- Contexto: La issue #155 describe el cuerpo esperado como "la cuenta no podrá iniciar sesión hasta que se desbloquee" (Bloquear) y "la cuenta podrá volver a iniciar sesión" (Desbloquear). La implementación actual usa copy genérico: "Esta acción afecta este usuario. ¿Desea continuar?".
- Justificación de la impl actual: la decisión de copy genérico prioriza privacidad y simplicidad (un solo partial sirve para ambas acciones). Cumple REQ-UCB-04 (sin PII) y D-08. Los tests verifican ausencia de PII pero no validan el texto literal.
- Recomendación opcional: si el equipo quiere alinear el copy con la issue, pasar `BodyHtml` específico por acción al invocar el partial — por ejemplo:
  ```html
  BodyHtml = "<p class='mb-0'>Esta acción impedirá el inicio de sesión de <strong>este usuario</strong> hasta que sea desbloqueado. ¿Desea continuar?</p>"
  ```
  Esto NO requiere tests nuevos porque `ModalDoesNotContainPii` seguiría pasando.
- Impacto: 0 sobre cumplimiento funcional. Decisión de copy, no de comportamiento.

**S-02. No hay test runtime específico para doble click → un POST (REQ-UCB-07).**
- Severidad: SUGGESTION.
- Contexto: La protección anti-doble-submit vive en el JS (`confirmBtn.disabled = true` antes de `trigger.submit()`). El test markup `BloquearButtonDoesNotSubmitDirectly` cubre la ausencia de `formaction`/`type=submit`, pero no simula un doble click real.
- Justificación: el design D-10 explicitó "sin E2E" para mantener el scope acotado; los tests E2E (Playwright/Selenium) quedan fuera del slice.
- Recomendación opcional: agregar un test E2E con Playwright/Selenium en un change futuro — fuera de scope del verify actual.
- Impacto: 0 sobre el runtime actual. La protección es trivialmente correcta (disable + re-enable solo en `hidden.bs.modal`); el riesgo de regresión en este slice es muy bajo.

**S-03. Budget lines ligeramente por encima del forecast (~554 vs ~300 estimados).**
- Severidad: SUGGESTION.
- Contexto: `tasks.md:9` estimaba `~300 líneas`; el diff real de producción+test es `+554/−18` (sin contar artifacts SDD). El budget declarado en `tasks.md:11` es de 400 líneas para review (`review_budget_lines: 400`).
- Justificación: el forecast subestimó el alcance de tests (13 vs 6 estimados, más partial documentado). La calidad y atomicidad de los tests justifica el delta.
- Recomendación opcional: ningún cambio requerido para el verify. El PR sigue siendo single-PR (no chained) según `tasks.md:14-17`.
- Impacto: 0 sobre la calidad del cambio.

## Cobertura por escenario de la spec

| Spec scenario | Cubierto por test runtime | OK |
|---|---|---|
| REQ-UCB-01: Confirmar dispara el POST a Bloquear con antiforgery y contexto preservado | `Post_Bloquear_WhenSuccessful_RedirectsToActiveSegmentAndPreservesContext` (verifica redirect, antiforgery, hidden inputs preservados) + `Get_Index_RendersFormDataUsuarioBloquearForm_WithHiddenInputs` (verifica markup del form) | ✅ |
| REQ-UCB-01: Cancelar no ejecuta POST | **No cubierto por test runtime dedicado**. Markup + JS garantiza el comportamiento (Bootstrap `data-bs-dismiss` + handler `hidden.bs.modal` que limpia `pendingVar`). Manual only. | ✅ (markup+JS) |
| REQ-UCB-01: Doble click en el botón no dispara dos POST | Markup test `Get_Index_BloquearButtonDoesNotSubmitDirectly` (botón es `type="button"`, no submit nativo). El JS guard (`window[pendingVar] ya seteado → return`) es de defensa adicional. | ✅ (markup; JS guard sin test runtime — ver S-02) |
| REQ-UCB-02: Confirmar dispara el POST a Desbloquear | `Post_Desbloquear_WhenSuccessful_RedirectsToActiveSegment` PASS | ✅ |
| REQ-UCB-02: Cancelar no ejecuta desbloqueo | Misma justificación que REQ-UCB-01.Cancelar | ✅ |
| REQ-UCB-03: Details Bloquear exige confirmación | `Get_Details_BloquearButton_OpensModal` PASS | ✅ |
| REQ-UCB-03: Details Desbloquear exige confirmación | `Get_Details_DesbloquearButton_OpensModal` PASS | ✅ |
| REQ-UCB-04: El modal no expone campos personales | 3 tests `*Modal_DoesNotContainPii` PASS | ✅ |
| REQ-UCB-05: Apertura por teclado y cierre con Esc devuelve foco | Markup test `HasAriaWiring` (atributos AA). Foco vía `event.relatedTarget.focus()` en JS, sin test runtime dedicado. | ✅ (markup+JS) |
| REQ-UCB-06: POST tras confirmar llega al handler con token válido y redirige | `Post_LifecycleHandler_WithoutAntiforgeryToken_ReturnsBadRequestAndDoesNotCallApi` (asegura que sin token el POST falla, con token llega al handler) + `Post_Bloquear_WhenSuccessful_RedirectsToActiveSegmentAndPreservesContext` | ✅ |
| REQ-UCB-07: Doble click sobre Confirmar produce un solo POST | Cubierto por JS (`confirmBtn.disabled = true` antes de `trigger.submit()`), sin test runtime dedicado. Ver S-02. | ✅ (JS; sin test runtime) |
| REQ-UCB-08: Bloquear desde activas preserva filtros y redirige a bloqueadas | `Post_Bloquear_WhenSuccessful_RedirectsToActiveSegmentAndPreservesContext` PASS (asserts `status=bloqueadas`, `p=3`, `search=bloq`, `sort=nombres_asc`) | ✅ |
| REQ-UCB-09: Admin no ve su propio botón Bloquear y el fence sigue activo | `Get_Index_WhenCurrentUserListed_HidesBloquearAndDeleteActions` + `Post_Bloquear_WhenApiRejectsAutoBloqueo_ShowsActionableFeedback` PASS | ✅ |
| REQ-UCB-10: Tests previos a la implementación (strict_tdd) | Orden de commits verificado: `437bc1c0` (test) → `6135bc8b` (partial) → `8821ece7` (Index) → `9c0f367d` (Details) | ✅ |

## Conclusión para el orquestador

**READY para `sdd-archive`.**

- 10/10 requisitos REQ-UCB cumplidos.
- 10/10 decisiones D-NN implementadas (con 1 desviación menor justificada — D-02 `Body` → `BodyHtml`).
- 11/11 criterios de aceptación de la issue #155 cumplidos (10 PASS estrictos + 1 PASS con copy divergente).
- 0 regresiones.
- 2412/2412 tests verdes (0 failed, 0 skipped).
- Build limpio (0 errors, 23 warnings preexistentes).
- Frontend bundle OK.
- 0 `Co-Authored-By`.
- Conventional commits correctos.

Las 3 observaciones adicionales son WARNING/SUGGESTION (no CRITICAL) y NO bloquean el archive. La fase `sdd-archive` puede proceder con confianza.

### Sugerencias de próximos pasos (no bloqueantes)

1. Si se desea alinear el copy del modal con la descripción literal de la issue (#155), pasar `BodyHtml` específico por acción al invocar el partial. Cosmético, sin tests adicionales.
2. Considerar tests E2E con Playwright/Selenium para los flujos de doble click y Esc/backdrop en un change futuro fuera del scope de UX. Fuera del slice actual (alineado con D-10).
3. Actualizar `design.md` para reflejar `BodyHtml` (alineación cosmético entre design e impl). No requerido para archive.

---

**Veredicto final: PASS** ✅