# Tasks: Fix popups de usuarios (SweetAlert2 + RIS-002 cause-root)

## Review Workload Forecast

| Field | Value |
|-------|-------|
| PR 1 estimado | ~120 líneas |
| PR 2 estimado | ~450 líneas |
| Total estimado | ~570 líneas |
| 400-line budget risk | High (PR 2 excede solo, PR 1 no) |
| Chained PRs recommended | Yes |
| Suggested split | PR 1 (120 LoC) + PR 2 (450 LoC), stacked-to-main |
| Delivery strategy | auto-chain |
| Chain strategy | stacked-to-main |

Decision needed before apply: No
Chained PRs recommended: Yes
Chain strategy: stacked-to-main
400-line budget risk: High

---

## PR 1 — `fix/seguridad-usuarios-ris-002-cause-root`

**Base**: `main`. **Branch**: `fix/seguridad-usuarios-ris-002-cause-root`.
**Estimado**: ~120 LoC (código + tests). Self-contained, sin dependencias.

### T-01 — RED: tests existentes rotos por cambio de `NameIdentifier`

**SPEC**: REQ-UCB-09, REQ-UCB-10
**Archivo(s)**: `tests/SGV.Tests/Web/Usuario/IndexPageTests.cs:152-173`, `tests/SGV.Tests/Web/Usuario/DetailsPageTests.cs:191-216`
**Acción**: RED (TDD)

#### Pasos
1. En `IndexPageTests.cs:152-173`, mantener `self.Id = "admin-test"` y reemplazar el assert sensible a whitespace por una verificación real del form asociado al usuario.
2. En `DetailsPageTests.cs:191-216`, cambiar `const string selfId = "admin"` a `"admin-test"`.
3. Ejecutar ambos tests antes del fix de producción y confirmar que FALLAN (estado RED legítimo).

#### Acceptance Criteria
- [x] `dotnet test --filter "FullyQualifiedName~IndexPageTests.Get_Index_WhenCurrentUserListed_HidesBloquearAndDeleteActions"` falla con RED legítimo (botones de bloqueo/eliminación aparecen visualmente)
- [x] `dotnet test --filter "FullyQualifiedName~DetailsPageTests.Get_Details_WhenAdminViewsSelf_RendersOnlyEdit_NoBloquearNoEliminar"` falla con RED legítimo

### T-02 — GREEN: quitar siembra manual de `NameIdentifier` en `AuthSessionFactory`

**SPEC**: REQ-UCB-09 (auto-fence), causa raíz RIS-002
**Archivo(s)**: `src/SGV.Web/Integration/Auth/AuthSessionFactory.cs:37-41`
**Acción**: GREEN

#### Pasos
1. Eliminar la línea `new(ClaimTypes.NameIdentifier, request.UserNameOrEmail),` del bloque `claims`.
2. Mantener `new(ClaimTypes.Name, request.UserNameOrEmail)` — el display name sigue siendo necesario.
3. Re-ejecutar los tests de T-01, deben pasar.

#### Acceptance Criteria
- [x] `dotnet test --filter "FullyQualifiedName~IndexPageTests.Get_Index_WhenCurrentUserListed_HidesBloquearAndDeleteActions"` verde
- [x] `dotnet test --filter "FullyQualifiedName~DetailsPageTests.Get_Details_WhenAdminViewsSelf_RendersOnlyEdit_NoBloquearNoEliminar"` verde
- [x] `rg -n "ClaimTypes.NameIdentifier" src/SGV.Web/Integration/Auth/AuthSessionFactory.cs` no retorna siembra manual; los claims validados se agregan genéricamente en `AddValidatedTokenClaims`

### T-03 — REFACTOR: actualizar comentario en `CookiePrincipalRevalidator`

**SPEC**: (defensa en profundidad)
**Archivo(s)**: `src/SGV.Web/Auth/CookiePrincipalRevalidator.cs:105-111`
**Acción**: REFACTOR (doc)

#### Pasos
1. Cambiar el comentario de "RIS-002: AuthSessionFactory seeds NameIdentifier with UserNameOrEmail first..." a "Defensa en profundidad: si el JWT no incluye NameIdentifier (e.g. fallback upstream), el `LastOrDefault` evita null."
2. Mantener el código `LastOrDefault` intacto — sigue siendo defensivo válido.

#### Acceptance Criteria
- [x] `dotnet test --filter "FullyQualifiedName~CookiePrincipalRevalidatorTests.ValidateAsync_PicksLastNameIdentifierWhenMultipleClaims"` verde
- [x] `rg -n "RIS-002" src/SGV.Web/Auth/CookiePrincipalRevalidator.cs` no retorna nada (el bug referenciado ya no existe como workaround activo)

### T-04 — Tests actualizados: alinear `self.Id` con el GUID real del JWT

**SPEC**: REQ-UCB-09, REQ-UCB-10
**Archivo(s)**: `tests/SGV.Tests/Web/Usuario/IndexPageTests.cs:152-173`, `tests/SGV.Tests/Web/Usuario/DetailsPageTests.cs:191-216`
**Acción**: fix (test maintenance)

#### Pasos
1. En `IndexPageTests.cs:159`, confirmar `BuildUsuario("admin-test", ...)` — el DTO del admin coincide con el `NameIdentifier` del JWT (`AdminJwtTestHelper.cs:76`).
2. En `DetailsPageTests.cs:201`, cambiar `const string selfId = "admin"` a `const string selfId = "admin-test"`.
3. Borrar el comentario workaround RIS-002 en `DetailsPageTests.cs:194-200`.
4. Confirmar que `IndexPageTests.cs:155-158` sólo documenta el auto-fence y no conserva ningún workaround RIS-002.

#### Acceptance Criteria
- [x] `dotnet test --filter "FullyQualifiedName~IndexPageTests.Get_Index_WhenCurrentUserListed_HidesBloquearAndDeleteActions"` verde
- [x] `dotnet test --filter "FullyQualifiedName~DetailsPageTests.Get_Details_WhenAdminViewsSelf_RendersOnlyEdit_NoBloquearNoEliminar"` verde
- [x] `rg -n "workaround RIS-002\|RIS-002.*workaround" tests/SGV.Tests/Web/Usuario/` no retorna nada

### T-05 — Nuevo E2E: `Index_E2E_Admin_NoVeSusPropiosBotones`

**SPEC**: REQ-UCB-09 (auto-fence UI)
**Archivo(s)**: `tests/SGV.Tests/Web/Usuario/IndexPageTests.cs` (nuevo test al final de los tests existentes)
**Acción**: RED (nuevo test)

#### Pasos
1. Agregar test `Index_E2E_Admin_NoVeSusPropiosBotones`.
2. Usar `self.Id = "admin-test"`, renderizar Index en `activas` y `bloqueadas`, y afirmar que la fila propia no contiene forms Bloquear, Eliminar ni Desbloquear.
3. El test detectó en RED que `bloqueadas` todavía renderizaba Desbloquear para la fila propia; aplicar el guard mínimo `Model.EsAdministrador && !esAuto` y re-ejecutar en GREEN.

#### Acceptance Criteria
- [x] `dotnet test --filter "FullyQualifiedName~Index_E2E_Admin_NoVeSusPropiosBotones"` verde
- [x] `rg -n "data-usuario-bloquear-form.*{self.Id}"` dentro del test NO encuentra el form en el render

### T-06 — Validación final PR 1

**SPEC**: (gate)
**Archivo(s)**: — (validación global)
**Acción**: fix (validation)

#### Pasos
1. Ejecutar `dotnet test SGV.slnx` — toda la suite verde.
2. Ejecutar `rg -n "NameIdentifier" src/SGV.Web tests/SGV.Tests` — confirmar que no aparecen consumidores nuevos (solo los mismos sitios de siempre).
3. Commit con mensaje: `fix(web): remove manual NameIdentifier seed in AuthSessionFactory (RIS-002 cause-root)`

#### Acceptance Criteria
- [x] `dotnet test SGV.slnx` verde (todo pasa, incluido MySQL)
- [x] `rg -n "NameIdentifier" src/SGV.Web tests/SGV.Tests` no muestra consumidores nuevos; `AuthSessionFactory.cs` deja de aparecer porque se eliminó la siembra manual

---

## PR 2 — `feat/usuarios-popups-sweetalert2`

**Base**: `main`. **Branch**: `feat/usuarios-popups-sweetalert2`.
**Depends on**: PR 1 mergeado (el E2E de auto-fence T-05 se re-ejecuta como no-regresión).
**Estimado**: ~450 LoC (JS 80 + Razor 120 + tests 250).

### T-07 — RED harness JS: tests Node para las 3 funciones de confirmación

**SPEC**: REQ-UCB-01, REQ-UCB-02, REQ-ULD-05, REQ-UCB-10
**Archivo(s)**: `tests/SGV.Tests/Web/Usuario/IndexPageTests.cs` (nuevos tests + helper)
**Acción**: RED (TDD — escribe harness antes de la implementación)

#### Pasos
1. Crear helper `ExecuteUsuarioConfirmationScriptAsync(UsuarioConfirmationKind kind, string? dismiss = null)` siguiendo patrón de `CargoIndexPageTests.cs:701-819`.
2. Crear `enum UsuarioConfirmationKind { Bloquear, Desbloquear, Eliminar }`.
3. Cada test: escribe harness Node `.cjs`, requiere el script desde `wwwroot/js/pages/usuarios-index.js`, mockea `root` + `Swal`, dispara click, captura stdout.
4. Tests cubren por función: confirmado (`isConfirmed: true`), cancelado (`dismiss: 'cancel'`), descartado por Esc/backdrop (`dismiss: 'backdrop'`, `dismiss: 'esc'`).
5. Ejecutar tests: FALLAN RED porque `usuarios-index.js` no existe.

#### Acceptance Criteria
- [ ] `dotnet test --filter "FullyQualifiedName~UsuarioConfirmation"` falla (RED — archivo JS no existe)
- [ ] El harness usa `Path.Combine(AppContext.BaseDirectory, "../../../../../src/SGV.Web/wwwroot/js/pages/usuarios-index.js")` (path absoluto)

### T-08 — GREEN: crear `wwwroot/js/pages/usuarios-index.js`

**SPEC**: REQ-UCB-01, REQ-UCB-02, REQ-ULD-05
**Archivo(s)**: `src/SGV.Web/wwwroot/js/pages/usuarios-index.js` (nuevo, ~80 LoC)
**Acción**: GREEN

#### Pasos
1. Implementar `wireUsuarioBloquearConfirmation(root, swal)`: `Swal.fire({ title: 'Bloquear usuario', text: 'Esta acción afecta este usuario. ¿Desea continuar?', icon: 'warning', showCancelButton: true, confirmButtonText: 'Bloquear', cancelButtonText: 'Cancelar', reverseButtons: true, focusCancel: true, allowEscapeKey: true, allowOutsideClick: true, customClass: { confirmButton: 'btn btn-secondary', cancelButton: 'btn btn-light' } })`.
2. Implementar `wireUsuarioDesbloquearConfirmation(root, swal)`: igual pero `title: 'Desbloquear usuario'`, `confirmButtonText: 'Desbloquear'`, `customClass: { confirmButton: 'btn btn-success', cancelButton: 'btn btn-light' }`.
3. Implementar `wireUsuarioDeleteConfirmation(root, swal)`: `title: 'Eliminar usuario'`, `text: 'Esta acción eliminará este usuario de forma permanente. No se puede deshacer.'`, `confirmButtonText: 'Eliminar definitivamente'`, `customClass: { confirmButton: 'btn btn-danger', cancelButton: 'btn btn-light' }`.
4. Agregar bootstrap condicional: `if (typeof window !== 'undefined') { window.wireUsuario* = ...; if (window.Swal && window.document) { wireUsuario*(window.document, window.Swal) } }`.
5. Agregar `module.exports = { wireUsuarioBloquearConfirmation, wireUsuarioDesbloquearConfirmation, wireUsuarioDeleteConfirmation }`.
6. Espejo exacto del patrón `cargos-index.js:1-85`.
7. Re-ejecutar tests T-07, deben pasar.

#### Acceptance Criteria
- [ ] `dotnet test --filter "FullyQualifiedName~UsuarioConfirmation"` verde (harness pasa)
- [ ] `node -e "const m = require('./src/SGV.Web/wwwroot/js/pages/usuarios-index.js'); console.log(typeof m.wireUsuarioBloquearConfirmation)"` imprime `"function"`
- [ ] `rg -n "module.exports" src/SGV.Web/wwwroot/js/pages/usuarios-index.js` confirma exports

### T-09 — Modificar `Index.cshtml`: SweetAlert2 + quitar modales nativos + JS inline

**SPEC**: REQ-UCB-01, REQ-UCB-02, REQ-ULD-05, REQ-UCB-03
**Archivo(s)**: `src/SGV.Web/Pages/Seguridad/Usuarios/Index.cshtml`
**Acción**: GREEN

#### Pasos
1. Agregar `<link rel="stylesheet" href="/plugins/sweetalert2/sweetalert2.min.css" />` en `<head>` (espejo `Cargos/Index.cshtml:10`).
2. En `@section Scripts`: agregar `<script src="/plugins/sweetalert2/sweetalert2.all.min.js"></script>` + `<script src="/js/pages/usuarios-index.js"></script>`.
3. Botón Bloquear (líneas 183-185): quitar `data-bs-toggle="modal" data-bs-target="#confirm-bloquear-modal"`.
4. Botón Eliminar (líneas 196-198): quitar `data-bs-toggle="modal" data-bs-target="#confirm-delete-modal"`, cambiar `formaction="?handler=Delete" type="submit"` → `type="button"` (eliminar `formaction`).
5. Botón Desbloquear (línea 217): quitar `data-bs-toggle="modal" data-bs-target="#confirm-desbloquear-modal"`.
6. Borrar modal `<div id="confirm-delete-modal">` (líneas 258-280).
7. Borrar las 2 invocaciones de `_ConfirmarAccionUsuarioModal` (líneas 287-306).
8. Borrar el `<script>` inline completo (líneas 308-361).

#### Acceptance Criteria
- [ ] `dotnet test --filter "FullyQualifiedName~IndexPageTests.Get_Index_RendersBloquearButton"` verde
- [ ] `rg -n "id=\"confirm-(bloquear|delete|desbloquear)-modal\"" src/SGV.Web/Pages/Seguridad/Usuarios/Index.cshtml` no retorna nada
- [ ] `rg -n "data-bs-toggle=\"modal\"" src/SGV.Web/Pages/Seguridad/Usuarios/Index.cshtml` no retorna nada
- [ ] `rg -n "_ConfirmarAccionUsuarioModal" src/SGV.Web/Pages/Seguridad/Usuarios/Index.cshtml` no retorna nada

### T-10 — Modificar `Details.cshtml`: SweetAlert2 + quitar modales nativos + JS inline

**SPEC**: REQ-UCB-03, REQ-UCB-04
**Archivo(s)**: `src/SGV.Web/Pages/Seguridad/Usuarios/Details.cshtml`
**Acción**: GREEN

#### Pasos
1. Agregar `<link rel="stylesheet" href="/plugins/sweetalert2/sweetalert2.min.css" />` en `<head>`.
2. En `@section Scripts`: agregar `<script src="/plugins/sweetalert2/sweetalert2.all.min.js"></script>` + `<script src="/js/pages/usuarios-index.js"></script>`.
3. Botón Desbloquear (línea 116): quitar `data-bs-toggle="modal" data-bs-target="#confirm-desbloquear-modal"`.
4. Botón Bloquear (líneas 135-136): quitar `data-bs-toggle="modal" data-bs-target="#confirm-bloquear-modal"`.
5. Borrar las 2 invocaciones de `_ConfirmarAccionUsuarioModal` (líneas 173-191).
6. Borrar el `<script>` inline (líneas 193-230).

#### Acceptance Criteria
- [ ] `dotnet test --filter "FullyQualifiedName~DetailsPageTests.Get_Details_BloquearButton_OpensModal"` verde
- [ ] `rg -n "id=\"confirm-(bloquear|desbloquear)-modal\"" src/SGV.Web/Pages/Seguridad/Usuarios/Details.cshtml` no retorna nada
- [ ] `rg -n "_ConfirmarAccionUsuarioModal" src/SGV.Web/Pages/Seguridad/Usuarios/Details.cshtml` no retorna nada

### T-11 — Borrar `_ConfirmarAccionUsuarioModal.cshtml`

**SPEC**: (limpieza)
**Archivo(s)**: `src/SGV.Web/Pages/Seguridad/Usuarios/_ConfirmarAccionUsuarioModal.cshtml` (eliminar)
**Acción**: fix (removal)

#### Pasos
1. Eliminar `src/SGV.Web/Pages/Seguridad/Usuarios/_ConfirmarAccionUsuarioModal.cshtml` (83 líneas).
2. Verificar que nadie más lo referencia.

#### Acceptance Criteria
- [ ] El archivo ya no existe: `test -f src/SGV.Web/Pages/Seguridad/Usuarios/_ConfirmarAccionUsuarioModal.cshtml` retorna exit code 1
- [ ] `rg -l "_ConfirmarAccionUsuarioModal" src/SGV.Web/` no retorna nada (sin referencias residuales)

### T-12 — Tests HTML actualizados: presencia SweetAlert2, ausencia modales nativos

**SPEC**: REQ-UCB-01, REQ-UCB-02, REQ-ULD-05, REQ-UCB-10
**Archivo(s)**: `tests/SGV.Tests/Web/Usuario/IndexPageTests.cs:572-786`, `tests/SGV.Tests/Web/Usuario/DetailsPageTests.cs:218-306`
**Acción**: fix (test adaptation)

#### Pasos
1. En `IndexPageTests.cs`:
   - `RendersBloquearButton_WithDataAttributeAndNoFormAction`: cambiar asserts de `Contains("data-bs-toggle=\"modal\"")` → `DoesNotContain("data-bs-toggle=\"modal\"")`.
   - `RendersBloquearModal_WithConfirmButton` → reemplazar con asserts de presencia de scripts SweetAlert2 (`/plugins/sweetalert2/sweetalert2.all.min.js`) y usuarios-index (`/js/pages/usuarios-index.js`). Assert negativo de `#confirm-bloquear-modal`.
   - `BloquearModal_HasAriaWiring` → reemplazar con asserts de SweetAlert2 y ausencia de IDs viejos.
   - `BloquearModal_DoesNotContainPii` → buscar PII en toda la response (no en un bloque de modal).
   - `RendersDesbloquearButton_WithDataAttributeAndNoFormAction`: `DoesNotContain("data-bs-toggle")`.
   - `RendersDesbloquearModal_WithConfirmButton` → assert scripts + assert negativo `#confirm-desbloquear-modal`.
   - `DesbloquearModal_DoesNotContainPii` → PII en toda la response.
2. En `DetailsPageTests.cs`:
   - `Get_Details_BloquearButton_OpensModal`: `DoesNotContain("data-bs-toggle=\"modal\"")`.
   - `Get_Details_DesbloquearButton_OpensModal`: `DoesNotContain("data-bs-toggle=\"modal\"")`.
   - `Get_Details_BloquearModal_HasAriaWiring` → assert scripts + ausencia `#confirm-bloquear-modal`.
   - `Get_Details_ModalDoesNotContainPii` → PII en toda la response.

#### Acceptance Criteria
- [ ] `dotnet test --filter "FullyQualifiedName~IndexPageTests"` verde (todos)
- [ ] `dotnet test --filter "FullyQualifiedName~DetailsPageTests"` verde (todos)
- [ ] `rg -n "id=\"confirm-(bloquear|delete|desbloquear)-modal\"" tests/SGV.Tests/` no retorna asserts de presencia (solo pueden ser `DoesNotContain`)

### T-13 — Validación final PR 2

**SPEC**: (gate)
**Archivo(s)**: — (validación global)
**Acción**: fix (validation)

#### Pasos
1. `bun run build` en `src/SGV.Web` — sin errores (SweetAlert2 ya en bundle).
2. `dotnet test SGV.slnx` verde.
3. `rg -n "_ConfirmarAccionUsuarioModal\|#confirm-(bloquear|delete|desbloquear)-modal" src/SGV.Web tests/SGV.Tests` no retorna nada.
4. Commit con mensaje: `feat(web): migrate Usuarios confirmation popups to SweetAlert2`

#### Acceptance Criteria
- [ ] `bun run build` en `src/SGV.Web` exitoso (0 errores)
- [ ] `dotnet test SGV.slnx` verde
- [ ] `rg -n "_ConfirmarAccionUsuarioModal\|#confirm-(bloquear|delete|desbloquear)-modal" src/SGV.Web tests/SGV.Tests` vacío

---

## Opcionales

### T-14 — Cleanup Bun/Gulp (opcional)

**SPEC**: (housekeeping)
**Archivo(s)**: `src/SGV.Web/` (verificar que no haya referencias huérfanas a los modales viejos en assets)
**Acción**: doc/cleanup

#### Pasos
1. Verificar que `plugins.config.js` sigue listando SweetAlert2 (ya presente).
2. No hay cambios necesarios — solo validación.

#### Acceptance Criteria
- [ ] `rg -n "confirm-delete-modal\|_ConfirmarAccionUsuarioModal" src/SGV.Web/` vacío

### T-15 — Validación post-merge (opcional)

**SPEC**: (housekeeping)
**Acción**: doc

#### Pasos
1. Luego de mergear PR 1 y PR 2, ejecutar `dotnet test SGV.slnx` contra main actualizado.
2. Verificar que `docs/decisiones-implementacion.md` no necesita actualización.

#### Acceptance Criteria
- [ ] `dotnet test SGV.slnx` verde en main
- [ ] Sin regresiones en auto-fence ni en popups

---

## Forecast

| PR | Código | Tests | Total | Over 400? |
|----|--------|-------|-------|-----------|
| PR 1 (RIS-002) | ~40 LoC | ~80 LoC | ~120 LoC | No |
| PR 2 (SweetAlert2) | ~200 LoC | ~250 LoC | ~450 LoC | Sí (+50) |
| **Total** | ~240 LoC | ~330 LoC | **~570 LoC** | **Sí** |

**Over budget**: Sí (PR 2 individualmente excede por ~50 LoC). El split actual ya es adecuado: PR 1 (~120 LoC) dentro de budget, PR 2 (~450 LoC) ligeramente por encima pero justificado por:
- ~80 LoC JS nuevo (`usuarios-index.js`) con 3 funciones + bootstrap + exports — código directo, espejo de `cargos-index.js`.
- ~120 LoC de cambios Razor (Index + Details): borrados y agregados, neto pequeño.
- ~250 LoC de tests: harness Node (~120 LoC) + asserts HTML actualizados (~100 LoC) + helpers (~30 LoC).

No se recomienda un PR 3 adicional porque el overhead de split (crear branch, CI, revisión) supera el ahorro de ~50 LoC sobre el budget.
