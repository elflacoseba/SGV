# Tasks: Confirmación modal al bloquear o desbloquear un usuario

Referencia: REQ-UCB-01..10 (`specs/usuario-web-confirmacion-bloqueo-desbloqueo/spec.md`), D-01..D-10 (`design.md`).

## Review Workload Forecast

| Field | Value |
|-------|-------|
| Estimated changed lines | ~300 |
| 400-line budget risk | Low |
| Chained PRs recommended | No |
| Suggested split | Single PR |
| Delivery strategy | single-pr-default |

Decision needed before apply: No
Chained PRs recommended: No
Chain strategy: pending
400-line budget risk: Low

### Suggested Work Units

| Unit | Goal | Likely Commit Type | Notes |
|------|------|--------------------|-------|
| 1 | Tests del modal Bloquear/Desbloquear en Index + Details | `test` | Antes del código (strict_tdd). 6 tests para Index, 4 para Details |
| 2 | Partial compartido `_ConfirmarAccionUsuarioModal.cshtml` | `feat` | Reutilizable por Index y Details |
| 3 | Modal Bloquear + Desbloquear en Index + JS inline | `feat` | Botones pasan a type=button con data-bs-toggle |
| 4 | Modal Bloquear + Desbloquear en Details + JS inline | `feat` | Reusa el partial y el patrón JS |
| 5 | Validación final (build + tests + bun) | `chore(sdd)` | Verificación de integración |

## Orden de implementación

**strict_tdd obligatorio:** todos los tests (Work Unit 1) se escriben ANTES de cualquier cambio en vistas.

1. **Work Unit 1** — Tests (RED)
2. **Work Unit 2** — Partial compartido
3. **Work Unit 3** — Index + JS (GREEN)
4. **Work Unit 4** — Details + JS (GREEN)
5. **Work Unit 5** — Validación final

---

## Phase 1: Tests (strict_tdd)

### 1.1 Tests — Botón Bloquear en Index no envía directo (REQ-UCB-01, REQ-UCB-10)

- [x] **1.1.1** `Get_Index_RendersBloquearButton_WithDataAttributeAndNoFormAction` — verifica que `[data-usuario-bloquear-button]` no tiene `formaction="?handler=Bloquear"`, tiene `data-bs-toggle="modal"` y `data-bs-target="#confirm-bloquear-modal"` (REQ-UCB-01).
- [x] **1.1.2** `Get_Index_BloquearButtonDoesNotSubmitDirectly` — verifica que el botón Bloquear NO es `type="submit"` con `formaction`; el submit se difiere al modal (REQ-UCB-01).
- [x] **1.1.3** `Get_Index_RendersBloquearModal_WithConfirmButton` — verifica que el HTML contiene `#confirm-bloquear-modal` con título "Bloquear usuario" y `[data-usuario-bloquear-confirm]` (REQ-UCB-01, REQ-UCB-04).
- [x] **1.1.4** `Get_Index_BloquearModal_HasAriaWiring` — verifica `aria-labelledby`, `aria-hidden="true"`, `tabindex="-1"` (REQ-UCB-05).
- [x] **1.1.5** `Get_Index_RendersFormDataUsuarioBloquearForm_WithHiddenInputs` — verifica que `data-usuario-bloquear-form` conserva antiforgery token + hidden `id`, `page`, `search`, `sort`, `status` y la `action="?handler=Bloquear"` del form (REQ-UCB-06, REQ-UCB-08).
- [x] **1.1.6** `Get_Index_BloquearModal_DoesNotContainPii` — verifica que el cuerpo del modal no expone `UserName`/`Email`/`Nombres`/`Apellidos` (REQ-UCB-04).

### 1.2 Tests — Botón Desbloquear en Index (REQ-UCB-02, REQ-UCB-10)

- [x] **1.2.1** `Get_Index_RendersDesbloquearButton_WithDataAttributeAndNoFormAction` — análogo a 1.1.1 pero para `[data-usuario-desbloquear-button]` y `#confirm-desbloquear-modal`.
- [x] **1.2.2** `Get_Index_RendersDesbloquearModal_WithConfirmButton` — verifica modal con título "Desbloquear usuario" y `[data-usuario-desbloquear-confirm]` (REQ-UCB-02, REQ-UCB-04).
- [x] **1.2.3** `Get_Index_DesbloquearModal_DoesNotContainPii` — verifica que el cuerpo del modal de desbloqueo no expone PII (REQ-UCB-04).

### 1.3 Tests — Modal en Details.cshtml (REQ-UCB-03, REQ-UCB-10)

- [x] **1.3.1** `Get_Details_BloquearButton_OpensModal` — verifica `#confirm-bloquear-modal` presente, botón Bloquear con `data-bs-toggle="modal"`, sin `formaction` (REQ-UCB-03).
- [x] **1.3.2** `Get_Details_DesbloquearButton_OpensModal` — análogo para el botón Desbloquear cuando el usuario está bloqueado (REQ-UCB-03).
- [x] **1.3.3** `Get_Details_BloquearModal_HasAriaWiring` — verifica atributos de accesibilidad (REQ-UCB-05).
- [x] **1.3.4** `Get_Details_ModalDoesNotContainPii` — verifica que ningún modal expone `UserName`/`Email`/`Nombres`/`Apellidos` (REQ-UCB-04).

Ubicación: `tests/SGV.Tests/Web/Usuario/IndexPageTests.cs` (1.1.x, 1.2.x), `tests/SGV.Tests/Web/Usuario/DetailsPageTests.cs` (1.3.x).

---

## Phase 2: Partial compartido

### 2.1 Crear `_ConfirmarAccionUsuarioModal.cshtml` (D-01, D-02, D-08, REQ-UCB-03, REQ-UCB-04)

- [x] **2.1** Crear `src/SGV.Web/Pages/Seguridad/Usuarios/_ConfirmarAccionUsuarioModal.cshtml` con markup Bootstrap 5 modal (D-02):
  - Contrato vía `ViewData`: `ModalId` (required), `Title` (required), `ConfirmButtonClass` (required), `PendingTriggerVar` (required), `ConfirmSelector` (required); opcionales con defaults sensatos: `TitleId`, `BodyHtml` (IHtmlContent, default "este usuario"), `ConfirmButtonText`, `ConfirmAriaLabel`.
  - Estructura: `modal fade` → `modal-dialog modal-dialog-centered` → `modal-content` → `modal-header` (con `btn-close`), `modal-body` (texto fijo "este usuario" o `BodyHtml` del view), `modal-footer` (Cancelar + Confirmar).
  - Botón confirmar con `data-@{ConfirmSelector}` (e.g., `data-usuario-bloquear-confirm`). El `data-usuario-modal-confirm` propuesto en design.md se reemplazó por el sufijo específico por acción, alineado con los tests del spec.

---

## Phase 3: Implementación Index.cshtml

### 3.1 Convertir botón Bloquear en disparador de modal (D-03, D-04, D-06, REQ-UCB-01)

- [x] **3.1.1** En `Index.cshtml` línea 183-187: cambiar `<button ... formaction="?handler=Bloquear" type="submit">` → `type="button" data-bs-toggle="modal" data-bs-target="#confirm-bloquear-modal"`. Eliminar `formaction` y `data-bs-toggle="tooltip"` (conflicto con `data-bs-toggle="modal"`).
- [x] **3.1.2** Agregar `action="?handler=Bloquear"` al `<form data-usuario-bloquear-form>` para que el submit diferido por el JS conserve el handler.
- [x] **3.1.3** Agregar `@await Html.PartialAsync("_ConfirmarAccionUsuarioModal", null, new ViewDataDictionary(ViewData) { ... })` con `ModalId="confirm-bloquear-modal"`, `Title="Bloquear usuario"`, `ConfirmButtonClass="btn-secondary"`, `ConfirmSelector="usuario-bloquear-confirm"`, `PendingTriggerVar="__pendingBloquearTrigger"`, `ConfirmAriaLabel="Bloquear"`.

### 3.2 Convertir botón Desbloquear en disparador de modal (D-03, D-04, D-06, REQ-UCB-02)

- [x] **3.2.1** En `Index.cshtml` línea 215-219: cambiar `<button ... formaction="?handler=Desbloquear" type="submit">` → `type="button" data-bs-toggle="modal" data-bs-target="#confirm-desbloquear-modal"`. Eliminar `formaction` y `data-bs-toggle="tooltip"`.
- [x] **3.2.2** Agregar `action="?handler=Desbloquear"` al `<form data-usuario-desbloquear-form>`.
- [x] **3.2.3** Agregar `@await Html.PartialAsync(...)` con `ModalId="confirm-desbloquear-modal"`, `Title="Desbloquear usuario"`, `ConfirmButtonClass="btn-success"`, `ConfirmSelector="usuario-desbloquear-confirm"`, `PendingTriggerVar="__pendingDesbloquearTrigger"`, `ConfirmAriaLabel="Desbloquear"`.

### 3.3 JS de manejo de modales en `@section Scripts` (D-05, D-06, D-07, D-09, REQ-UCB-05, REQ-UCB-07)

- [x] **3.3.1** Agregar en `@section Scripts` (sustituyendo el IIFE previo) el JS para los tres modales (bloquear / desbloquear / eliminar) usando un helper `setup(btnSel, confirmSel, pendingVar)`:
  - Disparador: `event.preventDefault()`, guardar `btn.closest('form')` en `window[pendingVar]`, retornar si ya está seteado (D-06a).
  - Confirmar: `confirmBtn.disabled = true`, `trigger.submit()`, limpiar `window[pendingVar]` (D-05).
  - `hidden.bs.modal`: resetear `window[pendingVar]`, re-habilitar botón, `event.relatedTarget.focus()` (D-06b, D-07).
- [x] **3.3.2** El helper se invoca para `[data-usuario-bloquear-button]` y `[data-usuario-desbloquear-button]`; el bloque de Eliminar se conserva inline (patrón vigente con `__pendingDeleteTrigger`).

---

## Phase 4: Implementación Details.cshtml

### 4.1 Convertir botones en Details (D-03, D-04, D-06, REQ-UCB-03)

- [x] **4.1.1** En `Details.cshtml` línea 114 (Desbloquear): cambiar `<button class="btn btn-success" type="submit">` → `type="button" data-usuario-desbloquear-button data-bs-toggle="modal" data-bs-target="#confirm-desbloquear-modal"`.
- [x] **4.1.2** En `Details.cshtml` línea 132 (Bloquear): cambiar `<button class="btn btn-secondary" type="submit">` → `type="button" data-usuario-bloquear-button data-bs-toggle="modal" data-bs-target="#confirm-bloquear-modal"`.
- [x] **4.1.3** Agregar los dos `@await Html.PartialAsync("_ConfirmarAccionUsuarioModal", ...)` al final de Details.cshtml (después del `}` del `@if/else` principal), con los mismos parámetros que en Index.
- [x] **4.1.4** Agregar `@section Scripts` en Details.cshtml con el mismo JS helper que Index (3.3.1), invocando `setup(...)` para bloquear y desbloquear.

---

## Phase 5: Validación final

### 5.1 Build y tests (REQ-UCB-10)

- [x] **5.1.1** `dotnet build SGV.slnx` sin errores, 0 warnings nuevos (23 warnings preexistentes en `Integration/*` por `CS8524` switch exhaustive; sin relación con este change).
- [x] **5.1.2** `dotnet test SGV.slnx` — 13 tests nuevos verdes + 2399 tests existentes verdes (total 2412/2412, 0 fallidos, 0 skipeados con MySQL local disponible).
- [x] **5.1.3** `cd src/SGV.Web && bun install && bun run build` — bundle frontend OK.

### 5.2 Verificación manual

- [ ] **5.2.1** Login como admin → `/seguridad/usuarios` → click Bloquear → modal se abre → confirmar → POST único → usuario aparece en bloqueadas.
- [ ] **5.2.2** Click Bloquear → `Esc` o backdrop → modal se cierra sin POST → foco vuelve al botón.
- [ ] **5.2.3** Click Desbloquear desde bloqueadas → confirmar → POST único → usuario aparece en activas.
- [ ] **5.2.4** Repetir 5.2.1..5.2.3 desde `/seguridad/usuarios/detalle/{id}`.
- [ ] **5.2.5** Doble click en "Confirmar" → un solo POST (verificar con herramientas de red).
- [ ] **5.2.6** Verificar accesibilidad con teclado: `Tab` al botón → `Enter` abre modal → `Tab` recorre controles → `Esc` cierra y devuelve foco.

> Nota: 5.2.x son verificaciones manuales del navegador; las cubre la fase `sdd-verify` cuando el orquestador lo dispare, no el ejecutor `sdd-apply`.

## Definition of Done

- [x] Todos los tests de Phase 1 escritos ANTES del código de Phase 2-4.
- [x] `dotnet build SGV.slnx` exitoso.
- [x] `dotnet test SGV.slnx` — 13 tests nuevos + suite existente verdes.
- [x] `bun run build` en `src/SGV.Web` exitoso.
- [x] Sin cambios en backend, API, DI, migraciones ni MySQL.
- [x] Conventional commits: `test:` para Phase 1, `feat:` para Phase 2-4. Sin `Co-Authored-By`.
- [x] Modales no exponen PII (REQ-UCB-04), tienen `aria-labelledby` (REQ-UCB-05), preservan antiforgery (REQ-UCB-06).
