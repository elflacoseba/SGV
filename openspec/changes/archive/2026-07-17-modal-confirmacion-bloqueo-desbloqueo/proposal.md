# Proposal: Confirmación modal al bloquear o desbloquear un usuario

## Intent

Los botones `Bloquear` y `Desbloquear` en `Index.cshtml` (líneas 176-188 y 208-220) y `Details.cshtml` (líneas 107-117 y 125-135) someten el formulario directo a `?handler=Bloquear` / `?handler=Desbloquear`, mientras `Eliminar` ya exige modal irreversible (`#confirm-delete-modal` en `Index` 259-300). Esta proposal replica ese patrón para `Bloquear` y `Desbloquear` en ambas vistas, sin tocar backend.

## Scope

**In Scope.**
1. `Index.cshtml`: convertir `data-usuario-bloquear-button` y `data-usuario-desbloquear-button` en disparadores de modal; agregar `#confirm-bloquear-modal` y `#confirm-desbloquear-modal` (o uno parametrizado); diferir el submit con `window.__pendingBloquearTrigger` / `window.__pendingDesbloquearTrigger`.
2. `Details.cshtml`: aplicar el mismo diferimiento a `data-usuario-bloquear-form` y `data-usuario-desbloquear-form`; extraer `_ConfirmarAccionUsuarioModal.cshtml` partial compartido para evitar duplicación.
3. Accesibilidad AA: `aria-labelledby`, `aria-hidden`, cierre con `Esc`/backdrop, foco devuelto al disparador.

**Out of Scope.** Backend (`UsuariosController.cs`, `IUsuarioApiClient.*Async`, `OnPostBloquearAsync` / `OnPostDesbloquearAsync`): auto-bloqueo, idempotencia y feedback ya cubiertos por `usuario-lockout-administrativo` y sus tests. Copy de `PageFeedback.SetSuccess/SetDanger`. Nueva librería (Bootstrap 5 + Bundle JS en `~/lib/bootstrap`). Acciones irreversibles nuevas.

## Capabilities

**New.** `usuario-web-confirmacion-bloqueo-desbloqueo` — slice UX que define la confirmación modal obligatoria de `Bloquear` y `Desbloquear` en `Index` y `Details`.

**Modified.** Ninguna.

**Capabilities referenciadas (no modificadas).** `usuario-lockout-administrativo` (API + auto-bloqueo + idempotencia + auditoría); `usuario-web-listado-detalle-baja` (segmentación REQ-ULD-02, gating REQ-ULD-03, PRG REQ-ULD-07, modal de `Eliminar` REQ-ULD-05 que se replica); `sgv-web-authentication` e `identity-user-role-management` (corte cookie/JWT tras lockout vigente).

## Approach

Backend intacto. Frontend: dos modales (o uno parametrizado) en `Index`, handler JS que difiere el submit replicando `window.__pendingDeleteTrigger`, partial compartido por ambas vistas. Tests `SgvWebApplicationFactory` en `tests/SGV.Tests/Web/Usuario/IndexPageTests.cs` y `DetailsPageTests.cs` redactados antes del código (`strict_tdd: true`).

**Por qué NEW y no MODIFIED.** Ninguna spec vigente exige confirmación modal UI de bloqueo/desbloqueo — son specs backend/segmentación. El requisito UX no tiene cobertura previa.

## Affected Areas

`src/SGV.Web/Pages/Seguridad/Usuarios/Index.cshtml`, `…/Details.cshtml`, nuevo partial `…/_ConfirmarAccionUsuarioModal.cshtml`, `tests/SGV.Tests/Web/Usuario/IndexPageTests.cs`, `…/DetailsPageTests.cs`.

## Risks

| Riesgo | Mitigación |
|--------|------------|
| Foco perdido al cerrar modal | `Modal.hidden` devuelve foco al disparador previo |
| Doble submit por doble clic | Deshabilitar "Confirmar" antes de `trigger.submit()` |
| Duplicación entre vistas | Partial compartido |
| Auto-bloqueo accidental | Gating server-side (`AutoBloqueo`) + modal obligatorio |

## Rollback Plan

Revertir `Index`/`Details` a `type="submit"` directos a `?handler=Bloquear` / `?handler=Desbloquear`; eliminar modales y partial. Sin migración ni cambio de API.

## Dependencies

Bootstrap 5 + Bundle JS ya servido por `~/lib/bootstrap`. `strict_tdd: true`.

## Success Criteria

- [ ] `Index`: clicks en `data-usuario-bloquear-button` y `data-usuario-desbloquear-button` abren modal; submit sólo tras confirmación.
- [ ] `Details`: misma confirmación en `data-usuario-bloquear-form` y `data-usuario-desbloquear-form`.
- [ ] Sin PII en modales (no `UserName`/`Email`/`Nombres`/`Apellidos`).
- [ ] AA: `aria-labelledby`, `aria-hidden`, `Esc`/backdrop, foco devuelto al disparador; antiforgery preservado.
- [ ] Doble clic produce un único POST; `AutoBloqueo` server-side → feedback UI accionable.
- [ ] Tests previos a la implementación.
- [ ] `dotnet build SGV.slnx`, `dotnet test SGV.slnx`, `bun run build` verdes.

## Open Questions

1. ¿Un único modal parametrizado o dos separados (`#confirm-bloquear-modal`, `#confirm-desbloquear-modal`)?
2. ¿Variables globales separadas (`window.__pendingBloquearTrigger`, `window.__pendingDesbloquearTrigger`) o un mapa `window.__pendingUsuarioTriggerByAction` indexado por acción?
