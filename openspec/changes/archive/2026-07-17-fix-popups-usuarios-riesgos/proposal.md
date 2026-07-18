# Proposal: Fix popups de usuarios (SweetAlert2 + RIS-002 cause-root)

Dos PRs stacked-to-main cierran tres reclamos de `Seguridad/Usuarios`. PR 1 corrige en raíz `src/SGV.Web/Integration/Auth/AuthSessionFactory.cs:37-47` (siembra manual de `NameIdentifier = UserNameOrEmail`) y oculta los botones de Bloquear/Eliminar/Desbloquear en la fila del admin. PR 2 migra los popups de Bootstrap 5 modal nativo a SweetAlert2 (espejo de `cargos-index.js:1-85`), preserva REQ-UCB-* y REQ-ULD-05 (incluida la regla sin PII) y borra `_ConfirmarAccionUsuarioModal.cshtml`.

## Problemas y diagnóstico

- **#1 Popups son Bootstrap modal, no SweetAlert2** (real). `Index.cshtml:259-306` y `Details.cshtml:172-183` emiten markup; `Index.cshtml:341-356` cablea el submit diferido.
- **#2 No impide auto-bloqueo/eliminación** (real, cause-root). `AuthSessionFactory.cs:39` siembra `NameIdentifier = UserNameOrEmail`; `AddValidatedTokenClaims` agrega el GUID. Anti-duplicado compara `Type+Value` → 2 claims; `FindFirstValue` devuelve "admin" en `Index.cshtml.cs:83` / `Details.cshtml.cs:89`. Workaround `CookiePrincipalRevalidator.cs:108-110` (`LastOrDefault`) no se replica. **#3 "no funciona eliminación física" es falso**: tests verdes (`BloquearDesbloquearEliminarGatewayTests:90-115`, `UsuariosEndToEndMySqlFactTests:36-50`/`:107-125`, `UsuarioServicioComandosTests:359-372`); el 403 que ve el usuario es `AutoEliminacion` (botón visible por #2). **Tests que enmascaran #2**: `IndexPageTests.cs:152-173` y `DetailsPageTests.cs:191-216` (comentario línea 194-200 documenta workaround RIS-002).

## Decisión propuesta

**PR 1 — cause-root + tests (~120 líneas).** Sacar siembra manual de `NameIdentifier` en `AuthSessionFactory.cs:37-47` (dejar JWT como única fuente). Actualizar comentario en `CookiePrincipalRevalidator.cs:108-110` (mantener `LastOrDefault` defensivo). Reescribir `IndexPageTests.cs:152-173` y `DetailsPageTests.cs:191-216` con `self.Id` GUID real; borrar comentario RIS-002. Nuevo E2E `Index_E2E_Admin_NoVeSusPropiosBotones` (assert ausencia de `[data-usuario-bloquear-form]`/`-delete-form`/`-desbloquear-form` en fila admin).

**PR 2 — SweetAlert2 + tests JS (~450 líneas).** Crear `wwwroot/js/pages/usuarios-index.js` con `wireUsuarioBloquearConfirmation` / `wireUsuarioDesbloquearConfirmation` / `wireUsuarioDeleteConfirmation` (`Swal.fire({title, text: 'este usuario', icon, showCancelButton, confirmButtonText, cancelButtonText, reverseButtons: true})`, `module.exports` para harness Node). En `Index.cshtml`: borrar modal nativo + JS inline (líneas 259-356), quitar `data-bs-toggle`/`data-bs-target` (líneas 184-217), agregar `<link>`+`<script>` SweetAlert2 (ya en `plugins.config.js` desde change `2026-06-26`). Borrar `_ConfirmarAccionUsuarioModal.cshtml` y las 2 invocaciones en `Details.cshtml:172-183`. Tests: asserts presencia del script, asserts negativos de `#confirm-bloquear-modal`/`#confirm-delete-modal` IDs viejos; harness JS estilo `UnidadOrganizativaWebTests.cs:187` (`[Theory]` con `cancelled`/`dismissed`).

## Capabilities

- **New**: ninguna.
- **Modified**: `usuario-web-confirmacion-bloqueo-desbloqueo` (REQ-UCB-01..10: Bootstrap 5 → SweetAlert2 vía `usuarios-index.js`; observable idéntico) y `usuario-web-listado-detalle-baja` (REQ-ULD-05: `#confirm-delete-modal` → `wireUsuarioDeleteConfirmation`).

## Affected Areas

- **Modified**: `src/SGV.Web/Integration/Auth/AuthSessionFactory.cs:37-47`, `src/SGV.Web/Pages/Seguridad/Usuarios/Index.cshtml:176-217` (sin `data-bs-*`), `src/SGV.Web/Auth/CookiePrincipalRevalidator.cs:108-110` (comentario), `tests/SGV.Tests/Web/Usuario/{IndexPageTests,DetailsPageTests}.cs:152-173/:191-216`.
- **Removed**: `src/SGV.Web/Pages/Seguridad/Usuarios/Index.cshtml:259-356`, `Details.cshtml:172-183` (invocaciones), `_ConfirmarAccionUsuarioModal.cshtml`.
- **New**: `src/SGV.Web/wwwroot/js/pages/usuarios-index.js` + E2E + harness JS.

## Criterios de éxito

- [ ] `FindFirstValue(ClaimTypes.NameIdentifier)` retorna GUID real; `EsAutoAccion(admin.Id) == true`.
- [ ] E2E `Index_E2E_Admin_NoVeSusPropiosBotones` verde; comentario RIS-002 borrado.
- [ ] Confirmaciones via `window.Swal.fire` desde `usuarios-index.js`.
- [ ] `dotnet test SGV.slnx` verde; `bun run build` sin errores; `DoesNotContain` PII.

## Riesgos y mitigaciones

- Otro consumidor asume `NameIdentifier = UserNameOrEmail` → `grep -r "NameIdentifier" src/SGV.Web tests/SGV.Tests` antes de mergear PR 1.
- SweetAlert2 ausente en CI → ya integrado desde `2026-06-26`; `bun run build` lo valida.
- PR 2 antes de PR 1 reintroduce confusión → stacked-to-main; dependencia explícita; E2E de PR 1 es pre-requisito.
- Tests JS no cubren `Esc`/backdrop → `[Theory]` + `[InlineData("cancelled")]`/`[InlineData("dismissed")]` estilo `PuestoIndexPageTests`.

## Out of scope

Tocar `SGV.Aplicacion`, `SGV.Infraestructura`, `SGV.Api` ni migraciones EF. Refactor del pipeline Gulp/Bun ni `plugins.config.js`. Cambiar copy/i18n/temas de SweetAlert2. Renombrar `data-usuario-*-form` (contrato de tests).

## Estrategia de entrega (chained stacked-to-main)

1. **PR 1 — `fix/seguridad-usuarios-ris-002-cause-root`** (~120 líneas): cause-root + tests adaptados + E2E. Self-contained.
2. **PR 2 — `feat/usuarios-popups-sweetalert2`** (~450 líneas): depende de PR 1. Branch desde `main`, `--base main`.

Validación: `dotnet test SGV.slnx` (MySQL 8) + `bun run build` (PR 2). Archivo OpenSpec tras merge del PR 2.

## Rollback Plan

- **PR 1**: `git revert` + re-siembra `NameIdentifier` en `AuthSessionFactory.cs:37-47` + restaurar comentario `CookiePrincipalRevalidator.cs:108-110`.
- **PR 2**: `git revert` + restaurar `_ConfirmarAccionUsuarioModal.cshtml`, modal nativo + JS inline en `Index.cshtml:259-356`, dos invocaciones en `Details.cshtml:172-183`. Borrar `usuarios-index.js`.
- Datos: `Persons`/`Auditorias`/`AspNetUsers` intactos. Cero migraciones.

## Dependencies

`sweetalert2` ya en bundle. Cero paquetes nuevos. Cero migraciones EF. Cero cambios de config.
