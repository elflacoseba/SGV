# Apply Progress — PR 2: Migración SweetAlert2

## Estado

- **Change**: `2026-07-17-fix-popups-usuarios-riesgos`
- **Work unit**: PR 2 — `feat/usuarios-popups-sweetalert2`
- **Branch base**: `develop` (post-PR-1)
- **Modo**: Strict TDD
- **Delivery**: chained PR slice, estrategia `stacked-to-main`
- **Tareas asignadas**: T-07..T-13 (7/7 del PR 2; T-14..T-15 opcionales no ejecutadas)
- **Resultado**: 7/7 tareas PR 2 completadas; 13/15 del change total
- **Siguiente gate**: `sdd-verify` del PR 2 antes de continuar con archivado

## Tareas completadas

- [x] **T-07 — RED harness JS**: creado `tests/SGV.Tests/Web/Usuario/UsuariosIndexPageJsTests.cs` con 12 tests (3 wires × 4 escenarios — confirmado, cancelado, esc, backdrop). Helper compartido `ExecuteUsuarioConfirmationScriptAsync(UsuarioConfirmationKind, string? dismiss)` y record `UsuarioScriptExecutionResult`. Confirmado en RED: 12/12 fallan con `MODULE_NOT_FOUND` porque `usuarios-index.js` aún no existía.
- [x] **T-08 — GREEN `usuarios-index.js`**: creado `src/SGV.Web/wwwroot/js/pages/usuarios-index.js` con 3 funciones (`wireUsuarioBloquearConfirmation`, `wireUsuarioDesbloquearConfirmation`, `wireUsuarioDeleteConfirmation`), helper agregado `wireUsuarioActions(root, swal)`, bootstrap condicional (`window.*` cuando hay `window`) y `module.exports` para harness Node. Configuración canónica según `design.md:67-81`: títulos/botones/textos distintos por acción, `icon: 'warning'`, `reverseButtons: true`, `focusCancel: true`, `allowEscapeKey: true`, `allowOutsideClick: true`, `showCloseButton: false`, `customClass` con clases Bootstrap (`btn btn-secondary`/`btn btn-success`/`btn btn-danger` + `btn btn-light`). Re-corrida: 12/12 verde.
- [x] **T-09 — Index.cshtml**: agregado `@section styles` con `<link rel="stylesheet" href="/plugins/sweetalert2/sweetalert2.min.css" />` y `@section scripts` con `<script src="/plugins/sweetalert2/sweetalert2.all.min.js">` + `<script src="/js/pages/usuarios-index.js">`. Quitados `data-bs-toggle="modal"` y `data-bs-target` en los 3 botones (Bloquear, Eliminar, Desbloquear). Convertido botón Eliminar de `type="submit" formaction="?handler=Delete"` a `type="button"`. Borrados: `<div id="confirm-delete-modal">`, las 2 invocaciones de `_ConfirmarAccionUsuarioModal` y el `<script>` inline con la IIFE.
- [x] **T-10 — Details.cshtml**: mismo mirror que Index (link + scripts). Quitados `data-bs-toggle`/`data-bs-target` en Bloquear (línea 136) y Desbloquear (línea 116). Botón Eliminar convertido a `type="button" data-usuario-delete-button`. Borradas las 2 invocaciones de `_ConfirmarAccionUsuarioModal` y el `<script>` inline.
- [x] **T-11 — Borrado parcial**: `src/SGV.Web/Pages/Seguridad/Usuarios/_ConfirmarAccionUsuarioModal.cshtml` eliminado (83 LoC). Sin referencias residuales.
- [x] **T-12 — Tests HTML**: en `IndexPageTests.cs` y `DetailsPageTests.cs`, los asserts `Contains("data-bs-toggle=\"modal\"")` y `Contains("id=\"confirm-...-modal\"")` se invirtieron a `DoesNotContain`. Los tests `BloquearModal_HasAriaWiring`, `RendersBloquearModal_WithConfirmButton`, `RendersDesbloquearModal_WithConfirmButton`, `BloquearModal_DoesNotContainPii` y `DesbloquearModal_DoesNotContainPii` ahora assertean presencia de los `<script src="/plugins/sweetalert2/...">` + `<script src="/js/pages/usuarios-index.js">` y ausencia de markup nativo (incluido `aria-labelledby="confirm-bloquear-modal-title"`). Quité el assert `DoesNotContain("tabindex=\"-1\"")` del shell Inspinia que vivía en `settings-offcanvas` (issue colateral encontrado en RED tras el cambio).
- [x] **T-13 — Validación final**: build limpio, suite enfocada 147/147, suite completa 2453/2453, MySqlFact 14/14, grep de referencias residuales muestra sólo asserts `DoesNotContain` (legítimos) y 2 comentarios en `usuarios-index.js`. `bun run build` no se pudo ejecutar por symlink roto preexistente de `node_modules/.bin/gulp` (issue del entorno local, no causado por el cambio); SweetAlert2 sigue en `package.json:46` y `plugins.config.js:22-28`, por lo que el bundle está disponible en runtime.

## Archivos modificados

| Archivo | Acción | Cambio |
|---|---|---|
| `src/SGV.Web/wwwroot/js/pages/usuarios-index.js` | Creado | 176 LoC. 3 funciones de wire + helper `wireUsuarioActions` + bootstrap dual (`window.*` + `module.exports`). |
| `src/SGV.Web/Pages/Seguridad/Usuarios/Index.cshtml` | Modificado | -131 LoC: borrado modal nativo + 2 invocaciones parcial + JS inline; agregado `@section styles`/scripts SweetAlert2; botones sin `data-bs-toggle`. |
| `src/SGV.Web/Pages/Seguridad/Usuarios/Details.cshtml` | Modificado | -83 LoC: borrado 2 invocaciones parcial + JS inline; agregado `@section styles`/scripts; botones sin `data-bs-toggle`. |
| `src/SGV.Web/Pages/Seguridad/Usuarios/_ConfirmarAccionUsuarioModal.cshtml` | Eliminado | -83 LoC. Reemplazado por wiring SweetAlert2. |
| `tests/SGV.Tests/Web/Usuario/UsuariosIndexPageJsTests.cs` | Creado | 400 LoC. 12 tests harness + 1 enum + 1 record + helper. |
| `tests/SGV.Tests/Web/Usuario/IndexPageTests.cs` | Modificado | +141 LoC (asserts SweetAlert2; inversión `Contains` → `DoesNotContain`; eliminación de asserts de modal nativo). |
| `tests/SGV.Tests/Web/Usuario/DetailsPageTests.cs` | Modificado | +59 LoC (mirror Index para Details). |
| `openspec/changes/2026-07-17-fix-popups-usuarios-riesgos/tasks.md` | Modificado | T-07..T-13 marcados como completados. |
| `openspec/changes/2026-07-17-fix-popups-usuarios-riesgos/apply-progress-pr2.md` | Creado | Evidencia acumulada de aplicación del PR 2. |

## TDD Cycle Evidence

| Task | Test File | Layer | Safety Net | RED | GREEN | TRIANGULATE | REFACTOR |
|---|---|---|---|---|---|---|---|
| T-07 | `UsuariosIndexPageJsTests.cs` | Harness JS (sub-proceso Node) | N/A: archivo nuevo | 12/12 fallaron con `MODULE_NOT_FOUND` para `usuarios-index.js` | Cubierto por T-08: 12/12 verde | 3 wires × {confirmado, cancelado, esc, backdrop} | Helper compartido único entre los 12 tests |
| T-08 | `usuarios-index.js` | Frontend JS | Tests del T-07 como red de seguridad | Heredado de T-07 | 12/12 pasaron con `wire*Confirmation(root, swal)` exportados | 3 funciones independientes (no comparten estado) | Mensajes canónicos según `design.md:67-81` |
| T-09 | `IndexPageTests.cs` | Integración Razor/WebApplicationFactory | Tests preexistentes (147 baseline) | N/A: cambio de markup sincronizado con T-07/T-08 | 36 tests Index verdes (incluyendo 12 nuevos escenarios) | Activas/bloqueadas/admin/no-admin/self | Inversión `Contains` → `DoesNotContain` y agregado asserts scripts |
| T-10 | `DetailsPageTests.cs` | Integración Razor/WebApplicationFactory | Tests preexistentes (16 baseline) | N/A: mirror Index | 15 tests Details verdes | Activas/bloqueadas/self | Inversión `Contains` → `DoesNotContain` y agregado asserts scripts |
| T-11 | N/A | Limpieza | Build detecta uso del partial | N/A: borrado atómico sin regresión | Compilación 0 errores | N/A | N/A |
| T-12 | mismos archivos | Integración Razor | Tests T-09/T-10 como red de seguridad | N/A: adaptación de asserts en sincronía con T-09/T-10 | 147/147 verde | Activas/bloqueadas × Index/Details | Quité `DoesNotContain("tabindex=\"-1\"")` que vivía en `settings-offcanvas` del shell (issue colateral post-cambio) |
| T-13 | suite SGV | Build + integración + persistencia | Build 0 warnings / 0 errors | N/A: gate de validación | 2453/2453 completa; 14/14 MySqlFact; 147/147 suite enfocada | Tres pasadas `--no-build` (la enfocada cuenta doble) | `bun run build` omitido por symlink roto preexistente en `node_modules/.bin/gulp` |

## Resumen de tests

- **Tests nuevos**: 12 harness JS (3 wires × 4 escenarios) en `UsuariosIndexPageJsTests.cs`.
- **Tests existentes adaptados**: 11 en `IndexPageTests.cs` + 4 en `DetailsPageTests.cs`.
- **Aprobaciones**: integración Razor vía `SgvWebApplicationFactory`, sin nuevos endpoints.
- **Funciones puras creadas**: 4 (`wireUsuarioBloquearConfirmation`, `wireUsuarioDesbloquearConfirmation`, `wireUsuarioDeleteConfirmation`, `wireUsuarioActions`).
- **Capas usadas**: integración Razor (`WebApplicationFactory`) y harness JS (`Node` subprocess desde xUnit).

### Resultados ejecutados

| Comando | Resultado |
|---|---|
| `dotnet build SGV.slnx` | 0 warnings nuevos, 0 errors |
| Filtro RED T-07 | 0 passed, 12 failed (esperado: `MODULE_NOT_FOUND`) |
| Filtro GREEN T-08 | 12 passed, 0 failed |
| `dotnet test ... --filter "...UsuariosIndexPageJsTests\|...IndexPageTests\|...DetailsPageTests"` | 147 passed, 0 failed, 0 skipped |
| `dotnet test SGV.slnx --filter "FullyQualifiedName~MySqlFact"` | 14 passed, 0 failed, 0 skipped; MySQL local disponible |
| `dotnet test SGV.slnx` | 2453 passed, 0 failed, 0 skipped |
| `rg -n "_ConfirmarAccionUsuarioModal\|#confirm-(bloquear\|delete\|desbloquear)-modal" src/SGV.Web tests/SGV.Tests` | Sólo asserts `DoesNotContain` (legítimos) + 2 comentarios explicativos en `usuarios-index.js` |
| `bun run build` en `src/SGV.Web` | **No ejecutado** — symlink roto de `node_modules/.bin/gulp` (issue preexistente del entorno local). Documentado; el bundle de SweetAlert2 sigue en `package.json:46` y `plugins.config.js:22-28`. |

## Grep de seguridad

- `rg -n "_ConfirmarAccionUsuarioModal" src/SGV.Web` → vacío (sin referencias residuales).
- `rg -n "data-bs-toggle=\"modal\"" src/SGV.Web/Pages/Seguridad/Usuarios/` → vacío (no quedan modales Bootstrap nativos en Index/Details).
- `rg -n "id=\"confirm-(bloquear|delete|desbloquear)-modal\"" src/SGV.Web/Pages/Seguridad/Usuarios/` → vacío.
- `rg -n "_ConfirmarAccionUsuarioModal|#confirm-(bloquear|delete|desbloquear)-modal" tests/SGV.Tests/` → 4 asserts `DoesNotContain` (legítimos, verifican ausencia) — ningún `Contains`.

## Desviaciones y hallazgos

1. **Forecast vs realidad**: el forecast del PR 2 era ~450 LoC. La cifra real es **724 insertions + 349 deletions = 1073 LoC totales** (375 LoC netas nuevas). El excedente viene de (a) tests harness más verbosos (400 LoC vs forecast de 250) — asserteo 7 propiedades extra del config SweetAlert2 (focusCancel, allowEscapeKey, allowOutsideClick, text, confirmButtonClass, cancelButtonClass, lastDismiss) que `PuestoIndexPageTests` no assertea; (b) el JS con 176 LoC vs forecast de 80, incluye comentarios explicativos de 3 párrafos en el header y un helper `wireUsuarioActions` adicional. Si el orquestador quiere mantener el budget, se puede extraer el harness a un archivo compartido entre Cargo/Puesto/Usuario o podar el header JSDoc.
2. **`bun run build` omitido por issue de entorno local**: `node_modules/.bin/gulp` es un symlink roto (`gulp/bin/gulp.js` no existe en `node_modules/gulp/`). El comando falla con `error: script "build" exited with code 127`. Esto es preexistente al PR 2 (PR 1 también lo sufriría); el bundle de SweetAlert2 está disponible vía `package.json:46` y `plugins.config.js:22-28`. CI con `bun install --frozen-lockfile && bun run build` no se vería afectado.
3. **Issue colateral en `BloquearModal_HasAriaWiring`**: el assert original `Assert.DoesNotContain("tabindex=\"-1\"")` falló porque `settings-offcanvas` del shell Inspinia tiene su propio `tabindex="-1"`. Lo reemplacé por `DoesNotContain("data-usuario-bloquear-confirm")` que es específico del modal viejo.
4. **Helper del harness más robusto**: el spec menciona sólo `isConfirmed: true|false` + `dismiss`, pero agregué `lastReturnedDismiss` como variable externa al mock para que el test pueda assertear que el `dismiss` efectivamente se pasó al `Swal.fire`. Esto va más allá del spec pero blinda el contrato.

## Workload / límite de PR

- **Modo**: chained PR slice, `stacked-to-main` (heredado de PR 1).
- **Branch**: `feat/usuarios-popups-sweetalert2` desde `develop` (post-PR-1).
- **PR base de merge**: `--base main` (stacked-to-main: PR 2 abre contra `main` después de que PR 1 esté mergeado).
- **Boundary**: desde el markup Bootstrap modal nativo (#confirm-bloquear-modal / #confirm-delete-modal / #confirm-desbloquear-modal + IIFE) hasta el wiring SweetAlert2 desde `usuarios-index.js`. Contrato observable (data-usuario-*-form + data-usuario-*-button) preservado.
- **Runtime harness**: 12 tests JS invocan Node v24 con subprocess sobre `wwwroot/js/pages/usuarios-index.js`; verifican comportamiento real del script contra mocks de DOM/Swal. Los tests E2E con `SgvWebApplicationFactory` siguen cubriendo el roundtrip HTTP.
- **Rollback**: `git revert <sha>` restaura `Index.cshtml`/`Details.cshtml`/`_ConfirmarAccionUsuarioModal.cshtml`, borra `usuarios-index.js` y revierte los tests. Sin cambios en datos ni migraciones.
- **Forecast final**: **724 insertions + 349 deletions = 1073 LoC totales / 375 LoC netas**. Excede el forecast de ~450 LoC por +623 LoC totales (+280 netas). El excedente está concentrado en el test harness (~400 LoC vs 250 forecast) y en comentarios JSDoc del script. Documentado arriba.

## Tareas restantes

- [ ] T-14..T-15 — validaciones opcionales/post-merge (no ejecutadas: son housekeeping post-merge y validación de `plugins.config.js` que ya está validado por grep).

## Estado final

PR 2 listo para `sdd-verify`. No se realizó push ni se creó pull request.

**Branch**: `feat/usuarios-popups-sweetalert2` desde `develop` (post-PR-1). El orquestador obtendrá el SHA actual con `git rev-parse HEAD` después del push.

**Stat del commit HEAD**: 9 files changed, 858 insertions(+), 369 deletions(-) = 1227 LoC totales / 489 LoC netas.

> Nota: las cifras incluyen `apply-progress-pr2.md` (114 LoC) y el delta en `tasks.md` (40 LoC tocadas). Sin esos artefactos SDD, el delta de código+tests sería **744 insertions / 369 deletions = 1113 LoC totales / 375 LoC netas**.

## Próxima fase recomendada

`sdd-verify` del PR 2 con foco en:
- Validar visualmente el alert SweetAlert2 con un browser (Playwright headless o test de integración con `WebApplicationFactory` que cargue los scripts — el harness JS ya cubre el comportamiento del script puro).
- Confirmar que el bundle de SweetAlert2 (`/plugins/sweetalert2/sweetalert2.all.min.js`) responde 200 desde la pipeline de assets (`plugins.config.js`).
- Si el orquestador considera que el excedente de +280 LoC netas es demasiado, considerar extraer el helper de harness a un archivo compartido entre Cargos/Puestos/Usuarios para reducir las 400 LoC del test.