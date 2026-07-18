# Archive Report — Fix popups de usuarios (SweetAlert2 + RIS-002 cause-root)

**Change**: `2026-07-17-fix-popups-usuarios-riesgos`
**Status**: **ARCHIVED** ✅
**Fecha de archivado**: 2026-07-17

## Resumen ejecutivo

Change de dos PRs stacked-to-main que corrige la causa raíz de RIS-002 (siembra manual de `ClaimTypes.NameIdentifier` en `AuthSessionFactory`) y migra los popups de confirmación de `Seguridad/Usuarios` (Bloquear, Desbloquear, Eliminar) desde modales Bootstrap 5 nativos a SweetAlert2 via `wwwroot/js/pages/usuarios-index.js`. PR 1 (#166) removió la siembra manual, alineando `CurrentUserId` con el GUID real del JWT y habilitando el auto-fence UI correcto. PR 2 (#167) creó 3 funciones SweetAlert2 (`wireUsuarioBloquearConfirmation`, `wireUsuarioDesbloquearConfirmation`, `wireUsuarioDeleteConfirmation`), borró el partial `_ConfirmarAccionUsuarioModal.cshtml` (83 LoC), los modales nativos y el JS inline de `Index.cshtml`/`Details.cshtml`. La suite completa pasa **2453/2453 tests deterministas** + 14 MySqlFact, con 11/11 requirements COMPLIANT.

## PRs mergeados

| PR | SHA | Título | URL |
|----|-----|--------|-----|
| #166 | `c63caf38` | `fix(web): remove manual NameIdentifier seed in AuthSessionFactory (RIS-002 cause-root)` | `/pull/166` |
| #167 | `2d81e4c4` | `feat(web): migrate Usuarios confirmation popups to SweetAlert2` | `/pull/167` |

## Issue cerrada

- **#165** — COMPLETED ✅

## Spec coverage

| Spec | Requirements | Status |
|------|-------------|--------|
| `usuario-web-confirmacion-bloqueo-desbloqueo` | REQ-UCB-01..10 (10/10 modificadas a SweetAlert2) | COMPLIANT ✅ |
| `usuario-web-listado-detalle-baja` | REQ-ULD-05 (1/1 modificada a SweetAlert2) | COMPLIANT ✅ |

### Detalle de modificaciones en specs canónicas

**`openspec/specs/usuario-web-confirmacion-bloqueo-desbloqueo/spec.md`**:
- REQ-UCB-01: `#confirm-bloquear-modal` → `wireUsuarioBloquearConfirmation` + `Swal.fire`
- REQ-UCB-02: `#confirm-desbloquear-modal` → `wireUsuarioDesbloquearConfirmation`
- REQ-UCB-03: partial Bootstrap → wiring SweetAlert2 desde `usuarios-index.js`
- REQ-UCB-04: modales Bootstrap → SweetAlert2, mismo contrato sin PII
- REQ-UCB-05: accesibilidad Bootstrap AA → SweetAlert2 (`focusCancel`, `Esc`, foco)
- REQ-UCB-06: submit Bootstrap → `form.requestSubmit(button)` con PRG intacto
- REQ-UCB-07: `window.__pending*Trigger` → SweetAlert2 v11 no encola alerts
- REQ-UCB-08: contexto PRG preservado (sin cambios estructurales)
- REQ-UCB-09: auto-fence UI + cause-root RIS-002 corregido
- REQ-UCB-10: strict_tdd con harness JS Node subprocess

**`openspec/specs/usuario-web-listado-detalle-baja/spec.md`**:
- REQ-ULD-05: `#confirm-delete-modal` Bootstrap → `wireUsuarioDeleteConfirmation` SweetAlert2, 6 escenarios

## Tests finales

| Suite | Resultado |
|-------|-----------|
| `dotnet test SGV.slnx --no-build` (3 corridas) | 2453/2453 PASS ✅ |
| MySQL 8 local `MySqlFact` | 14/14 PASS ✅ |
| UsuariosIndexPageJsTests (harness JS) | 12/12 PASS ✅ |
| IndexPageTests | 105/105 PASS ✅ |
| DetailsPageTests | 30/30 PASS ✅ |
| CookiePrincipalRevalidatorTests | 9/9 PASS ✅ |

## Cumplimiento de criterios de éxito (proposal.md)

| Criterio | Estado | Evidencia |
|----------|--------|-----------|
| `FindFirstValue(ClaimTypes.NameIdentifier)` retorna GUID real; `EsAutoAccion(admin.Id) == true` | ✅ PASS | `AuthSessionFactory.cs:37-41` sin siembra manual; JWT emite `"admin-test"`. Tests `Get_Index_WhenCurrentUserListed_HidesBloquearAndDeleteActions` y `Get_Details_WhenAdminViewsSelf_RendersOnlyEdit_NoBloquearNoEliminar` verdes. |
| E2E `Index_E2E_Admin_NoVeSusPropiosBotones` verde; comentario RIS-002 borrado | ✅ PASS | Test creado en T-05. `rg -n "RIS-002" tests/SGV.Tests/Web/Usuario/` vacío. `CookiePrincipalRevalidator.cs:105` comentario actualizado a "Defense in depth". |
| Confirmaciones via `window.Swal.fire` desde `usuarios-index.js` | ✅ PASS | 3 funciones en `usuarios-index.js:16-146`, testeadas via harness JS 12/12 y asserts HTML en IndexPageTests/DetailsPageTests. |
| `dotnet test SGV.slnx` verde | ✅ PASS | 2453/2453 determinista, 3 corridas consecutivas. |
| `bun run build` sin errores | ⚠️ PASS WITH WARNINGS | Symlink roto preexistente en `node_modules/.bin/gulp` impide ejecución local. SweetAlert2 ya en `plugins.config.js:23-28` y `package.json:46`. Bundle validado por 6+ asserts `Assert.Contains` sobre scripts SweetAlert2 en tests HTML. |
| `DoesNotContain` PII | ✅ PASS | `usuarios-index.js` no interpola PII (literales `'este usuario'`). Tests `BloquearModal_DoesNotContainPii`, `DesbloquearModal_DoesNotContainPii`, `ModalDoesNotContainPii` verdes. |

## Lecciones aprendidas

1. **RIS-002 era un bug de identidad, no de UI**: la causa raíz era la siembra manual de `ClaimTypes.NameIdentifier` con `UserNameOrEmail` en `AuthSessionFactory`, no un bug en los modales. Al corregirla, el auto-fence UI empezó a funcionar correctamente sin cambios en la lógica de negocio.
2. **Los tests que "pasaban" enmascaraban el bug**: `IndexPageTests.cs:152-173` y `DetailsPageTests.cs:191-216` usaban `self.Id = "admin"` que coincidía con el `UserNameOrEmail` sembrado, ocultando el problema. El comentario `// RIS-002 workaround` documentaba la anomalía pero el test no fallaba. Lección: los tests deben reflejar el comportamiento real del JWT, no valores hardcodeados que coincidan con workarounds.
3. **SweetAlert2 simplifica el código frontend significativamente**: reemplazar ~280 líneas de modales Bootstrap + JS inline + partial compartido por ~176 líneas de JS puro con 3 funciones independientes y un bootstrap genérico. El harness JS con subprocess Node permite testear el comportamiento real de Swal.fire sin dependencias del browser.
4. **Stacked-to-main con dos PRs funcionó bien**: PR 1 (~120 LoC, cause-root) y PR 2 (~450 LoC, SweetAlert2) se mantuvieron independientes sin contaminación cross-PR. PR 1 mergeado antes que PR 2 evitó reintroducir el bug.
5. **Determinismo de tests es crítico**: 3 corridas consecutivas de 2453/2441 tests sin flaky tests confirman que la suite es estable. La migración de MySQL a Testcontainers y la limpieza de estado entre tests (change `2026-07-11-hacer-suite-tests-determinista`) fueron pre-requisitos necesarios.

## Follow-ups

- Ninguno bloqueante. El symlink roto `node_modules/.bin/gulp` es preexistente y no afecta CI (GitHub Actions regenera `node_modules` con `bun install --frozen-lockfile`). Si se desea resolver localmente: `rm -rf src/SGV.Web/node_modules && cd src/SGV.Web && bun install`.

## Source of Truth Actualizado

- `openspec/specs/usuario-web-confirmacion-bloqueo-desbloqueo/spec.md` — REQ-UCB-01..10 actualizados a SweetAlert2
- `openspec/specs/usuario-web-listado-detalle-baja/spec.md` — REQ-ULD-05 actualizado a SweetAlert2
