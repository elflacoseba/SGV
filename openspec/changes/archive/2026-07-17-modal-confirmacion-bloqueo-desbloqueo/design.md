# Design: Confirmación modal al bloquear o desbloquear un usuario

## Resumen

Replica el patrón UX de `#confirm-delete-modal` sobre `Bloquear`/`Desbloquear` en `Index.cshtml` y `Details.cshtml`, sin tocar backend, handlers ni contrato HTTP. Dos modales separados instanciados por un partial co-localizado + `@section Scripts` por vista que difiere el submit y limpia `window.__pendingXTrigger` al cerrar.

## Decisiones técnicas

| ID | Decisión | Rationale |
|---|---|---|
| D-01 | Partial en `Pages/Seguridad/Usuarios/_ConfirmarAccionUsuarioModal.cshtml` | Co-localización con consumidores; paridad con `_Form.cshtml`. |
| D-02 | Contrato vía `ViewData`: `ModalId`, `TitleId`, `Title`, `Body`, `ConfirmButtonClass`, `ConfirmButtonText`, `ConfirmSelector`, `TriggerSelector`, `PendingTriggerVar` | Plano como `_PageTitle.cshtml`. |
| D-03 | Dos modales separados (`#confirm-bloquear-modal`, `#confirm-desbloquear-modal`) | Decidido en spec. Paridad con `#confirm-delete-modal`. |
| D-04 | Dos globales separadas (`window.__pendingBloquearTrigger`, `window.__pendingDesbloquearTrigger`) | Decidido en spec. Mínimo cambio vs `__pendingDeleteTrigger`. |
| D-05 | `confirmBtn.disabled = true` antes de `trigger.submit()` | Cubre REQ-UCB-07. |
| D-06 | Doble guard: (a) trigger retorna si `window[pendingVar]` ya está seteado; (b) `hidden.bs.modal` resetea la var | Cierra el riesgo medio de sdd-spec. |
| D-07 | `event.relatedTarget.focus()` en `hidden.bs.modal`; `aria-labelledby` apunta al `<h5>`; `aria-hidden="true"` inicial | Cubre REQ-UCB-05. |
| D-08 | Body dice sólo "este usuario", sin PII | Cubre REQ-UCB-04. |
| D-09 | JS inline en `@section Scripts` por vista | Paridad con `Index.cshtml:282-300`. |
| D-10 | 6 smoke HTTP nuevos antes del código; sin tests de PageModel; sin E2E | Cubre el slice UX sin inflar la suite. |

## Estructura del modal

```html
<div class="modal fade" id="@ViewData["ModalId"]" tabindex="-1"
     aria-labelledby="@ViewData["TitleId"]" aria-hidden="true">
    <div class="modal-dialog modal-dialog-centered">
        <div class="modal-content">
            <div class="modal-header">
                <h5 class="modal-title" id="@ViewData["TitleId"]">@ViewData["Title"]</h5>
                <button type="button" class="btn-close" data-bs-dismiss="modal" aria-label="Cerrar"></button>
            </div>
            <div class="modal-body">@Html.Raw(ViewData["Body"])</div>
            <div class="modal-footer">
                <button type="button" class="btn btn-light" data-bs-dismiss="modal">Cancelar</button>
                <button type="button" class="btn @ViewData["ConfirmButtonClass"]"
                        data-usuario-modal-confirm>@ViewData["ConfirmButtonText"]</button>
            </div>
        </div>
    </div>
</div>
```

`data-usuario-modal-confirm` es el selector interno; la JS resuelve desde el `ModalId` actual.

## Lógica JavaScript

```js
(function () {
    var modal = document.getElementById('@ViewData["ModalId"]');
    var confirmBtn = modal.querySelector('[data-usuario-modal-confirm]');
    var triggerSelector = '@ViewData["TriggerSelector"]';
    var pendingVar = '@ViewData["PendingTriggerVar"]';

    // Disparador: anula submit, guarda form; ignora si ya hay modal pendiente (D-06a)
    document.querySelectorAll(triggerSelector).forEach(function (btn) {
        btn.addEventListener('click', function (event) {
            event.preventDefault();
            if (window[pendingVar]) return;
            window[pendingVar] = btn.closest('form');
        });
    });

    // Confirmar: POST único (antiforgery + hidden inputs viven en el form). D-05
    confirmBtn.addEventListener('click', function () {
        if (confirmBtn.disabled) return;
        var trigger = window[pendingVar];
        if (!trigger) return;
        confirmBtn.disabled = true;
        trigger.submit();
        window[pendingVar] = null;
    });

    // Cleanup al cerrar (D-06b, D-07)
    modal.addEventListener('hidden.bs.modal', function (event) {
        window[pendingVar] = null;
        confirmBtn.disabled = false;
        if (event.relatedTarget) event.relatedTarget.focus();
    });
})();
```

## Cambios en las vistas

| Vista | Bloque | Acción |
|---|---|---|
| Index 183-187 | Bloquear btn | `type="submit"` → `type="button"` + `data-bs-toggle="modal" data-bs-target="#confirm-bloquear-modal"`; quitar `formaction`. |
| Index 215-219 | Desbloquear btn | Análogo → `#confirm-desbloquear-modal`. |
| Index 280-301 | Delete modal | Se conserva. |
| Index (nuevo) | Partials + JS | Dos `@await Html.PartialAsync("_ConfirmarAccionUsuarioModal", null, viewDataX)` + `<script>` en `@section Scripts`. |
| Details 107-117 | Desbloquear | Botón submit → `type="button" data-bs-toggle="modal" data-bs-target="#confirm-desbloquear-modal"`. |
| Details 125-135 | Bloquear | Análogo. |
| Details 136-146 | Delete | Sin cambios. |
| Details (nuevo, fin) | Partials + JS | Los dos `PartialAsync(...)` + `@section Scripts` con la misma lógica que Index. |

Los `<form data-usuario-bloquear-form>` / `data-usuario-desbloquear-form` **conservan** `@Html.AntiForgeryToken()` y los hidden `id`/`page`/`search`/`sort`/`status` (REQ-UCB-06).

## Tests

| Test | Cubre |
|---|---|
| `..._RendersBloquearConfirmModal` (Index + Details) | HTML contiene `#confirm-bloquear-modal` + `data-usuario-bloquear-confirm`. |
| `..._RendersDesbloquearConfirmModal` (Index + Details) | Análogo. |
| `..._ModalDoesNotContainPii` (Index + Details) | HTML NO contiene `UserName`/`Email`/`Nombres`/`Apellidos`. |
| `Get_Index_BloquearButtonDoesNotSubmitDirectly` | Botón sin `formaction="?handler=Bloquear"` ni `type="submit"`. |
| `Get_Index_ConfirmModalHasAriaWiring` | `aria-labelledby` + `aria-hidden="true"` correctos. |
| POST `Bloquear`/`Desbloquear`/`Delete` existentes | Regresión: handlers + antiforgery + PRG verdes. |

## Riesgos mitigados

| Riesgo | Requisito | Mitigación |
|---|---|---|
| Doble submit en "Confirmar" | REQ-UCB-07 | `confirmBtn.disabled = true` antes de `submit()` (D-05). |
| Doble click en disparador / ref. colgante | REQ-UCB-01 escenario / riesgo medio sdd-spec | Doble guard en D-06. |
| Foco perdido al cerrar | REQ-UCB-05 | `event.relatedTarget.focus()` en `hidden.bs.modal` (D-07). |
| PII en el modal | REQ-UCB-04 | Body "este usuario" + test `ModalDoesNotContainPii`. |
| Duplicación Index ↔ Details | REQ-UCB-03 | Partial compartido (D-01). |
| Auto-bloqueo accidental | REQ-UCB-09 | Render oculta botón del admin actual; fence server-side intacto. |

## Compatibilidad y regresiones

Cero cambios en backend (`OnPostBloquearAsync`/`OnPostDesbloquearAsync`/`OnPostDeleteAsync`/`IUsuarioApiClient`), contrato HTTP, DI, `Program.cs` ni MySQL. Modal y JS de `#confirm-delete-modal` se conservan. Tests POST existentes verdes: `<form>` mantienen `data-*` y los handlers no cambian.

## Definición de "Hecho"

- [ ] `dotnet build SGV.slnx`, `dotnet test SGV.slnx`, `bun run build` (en `src/SGV.Web`) verdes.
- [ ] Manual: click en Bloquear/Desbloquear (Index + Details) abre modal; confirmar dispara POST único; `Esc`/backdrop cierran sin POST y devuelven foco; doble click en "Confirmar" produce un solo POST.
