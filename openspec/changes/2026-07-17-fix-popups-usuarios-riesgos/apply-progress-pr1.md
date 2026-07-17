# Apply Progress — PR 1: Fix RIS-002 cause-root

## Estado

- **Change**: `2026-07-17-fix-popups-usuarios-riesgos`
- **Work unit**: PR 1 — `fix/seguridad-usuarios-ris-002-cause-root`
- **Modo**: Strict TDD
- **Delivery**: chained PR, estrategia `stacked-to-main`
- **Tareas asignadas**: T-01..T-06
- **Resultado**: 6/6 tareas asignadas completadas; 6/15 del change total
- **Siguiente gate**: `sdd-verify` del PR 1 antes de continuar con PR 2

## Tareas completadas

- [x] **T-01 — RED**: se alinearon ambos DTO self con `admin-test` y se reemplazó el assert de Index sensible a whitespace por una comprobación real del form asociado al usuario. Los dos tests fallaron antes del fix de producción: 0 passed, 2 failed.
- [x] **T-02 — GREEN**: se eliminó la siembra manual `ClaimTypes.NameIdentifier = UserNameOrEmail` de `AuthSessionFactory`; `ClaimTypes.Name` se conserva como display name. Los dos tests de auto-fence pasaron: 2 passed, 0 failed.
- [x] **T-03 — REFACTOR**: se mantuvo `LastOrDefault` en `CookiePrincipalRevalidator` como defensa en profundidad y se actualizaron los comentarios de producción y test. El approval test pasó: 1 passed, 0 failed.
- [x] **T-04 — Tests actualizados**: `self.Id` y `selfId` usan `admin-test`; se eliminó el comentario workaround obsoleto de Details y no quedan referencias al workaround RIS-002 en los tests de Usuario.
- [x] **T-05 — E2E**: se agregó `Index_E2E_Admin_NoVeSusPropiosBotones`, que valida la fila propia tanto en `activas` como en `bloqueadas`. El RED descubrió que la vista aún mostraba Desbloquear en la fila propia; se agregó el guard mínimo `Model.EsAdministrador && !esAuto`. El test pasó en GREEN.
- [x] **T-06 — Gate final**: build, suite enfocada, suite completa, tres corridas deterministas `--no-build`, MySqlFact y grep de seguridad completados en verde.

## Archivos modificados

| Archivo | Acción | Cambio |
|---|---|---|
| `src/SGV.Web/Integration/Auth/AuthSessionFactory.cs` | Modificado | Eliminada la siembra manual de `NameIdentifier`; el JWT queda como única fuente. |
| `src/SGV.Web/Auth/CookiePrincipalRevalidator.cs` | Modificado | Comentario actualizado; `LastOrDefault` permanece como defensa en profundidad. |
| `src/SGV.Web/Pages/Seguridad/Usuarios/Index.cshtml` | Modificado | El form Desbloquear también respeta `!esAuto` en la vista bloqueadas. |
| `tests/SGV.Tests/Web/Usuario/IndexPageTests.cs` | Modificado | Assert anti-falso-positivo, helper de forms y nuevo E2E activo/bloqueado. |
| `tests/SGV.Tests/Web/Usuario/DetailsPageTests.cs` | Modificado | Self ID alineado con el JWT y comentario workaround eliminado. |
| `tests/SGV.Tests/Seguridad/CookiePrincipalRevalidatorTests.cs` | Modificado | Comentario del contrato defensivo actualizado. |
| `openspec/changes/2026-07-17-fix-popups-usuarios-riesgos/tasks.md` | Modificado | T-01..T-06 y sus criterios marcados como completados. |
| `openspec/changes/2026-07-17-fix-popups-usuarios-riesgos/apply-progress-pr1.md` | Creado | Evidencia acumulada de aplicación del PR 1. |

## TDD Cycle Evidence

| Task | Test File | Layer | Safety Net | RED | GREEN | TRIANGULATE | REFACTOR |
|---|---|---|---|---|---|---|---|
| T-01 | `IndexPageTests.cs`, `DetailsPageTests.cs` | Integración Razor | 3/3 tests relevantes en baseline | 2/2 fallaron legítimamente al encontrar los forms propios | Cubierto por T-02: 2/2 pasaron | Caso Index + caso Details | Assert de Index desacoplado de whitespace |
| T-02 | mismos archivos | Integración auth + Razor | RED confirmado antes de producción | Heredado de T-01 | 2/2 pasaron tras quitar el claim manual | Index y Details consumen el mismo principal JWT | Cambio mínimo de una línea de claim |
| T-03 | `CookiePrincipalRevalidatorTests.cs` | Unitario / approval | 1/1 pasó antes del refactor | N/A: refactor documental con approval test verde | 1/1 siguió pasando | Duplicados cubiertos por dos valores distintos | Comentarios alineados con la defensa vigente |
| T-04 | `IndexPageTests.cs`, `DetailsPageTests.cs` | Integración Razor | 2/2 verdes tras T-02 | Cubierto por el RED de T-01 | 2/2 pasaron | Self ID consistente en ambas páginas | Workaround obsoleto eliminado |
| T-05 | `IndexPageTests.cs` | Integración end-to-end de la shell Web | Auto-fence activo ya verde | 1/1 falló al encontrar Desbloquear en la fila propia bloqueada | 1/1 pasó tras el guard mínimo | Dos caminos: `activas` y `bloqueadas` | Helper común para asociar forms con IDs |
| T-06 | suite SGV | Build + integración + persistencia | Build 0 warnings / 0 errors | N/A: gate de validación | 2441/2441 suite completa; 14/14 MySqlFact | Tres corridas `--no-build` idénticas | Sin cambios adicionales |

## Resumen de tests

- **Tests nuevos**: 1 E2E (`Index_E2E_Admin_NoVeSusPropiosBotones`).
- **Tests existentes endurecidos**: 2 auto-fence (Index y Details).
- **Approval tests**: 1 (`ValidateAsync_PicksLastNameIdentifierWhenMultipleClaims`).
- **Funciones puras creadas**: ninguna.
- **Capas usadas**: integración Razor/WebApplicationFactory y unitario de revalidación de cookie.

### Resultados ejecutados

| Comando | Resultado |
|---|---|
| `dotnet build SGV.slnx` | 0 warnings, 0 errors |
| Filtro RED T-01 | 0 passed, 2 failed (esperado) |
| Filtro GREEN T-02/T-03 | 3 passed, 0 failed |
| `dotnet test ... --filter "...IndexPageTests|...DetailsPageTests|...CookiePrincipalRevalidatorTests|...UsuariosEndToEndMySqlFact"` | 149 passed, 0 failed, 0 skipped |
| `dotnet test SGV.slnx --filter "FullyQualifiedName~MySqlFact"` | 14 passed, 0 failed, 0 skipped; MySQL local disponible |
| `dotnet test SGV.slnx` | 2441 passed, 0 failed, 0 skipped |
| Tres corridas `dotnet test SGV.slnx --no-build` | 2441/2441 en las tres; sin `MSB4166` |

## Grep de seguridad

`rg -n "NameIdentifier" src/SGV.Web tests/SGV.Tests` no mostró consumidores nuevos. `AuthSessionFactory.cs` dejó de aparecer, que es precisamente el efecto del cause-root fix. Permanecen los consumidores esperados en `CookiePrincipalRevalidator`, `Index.cshtml.cs`, `Details.cshtml.cs` y las referencias de tests existentes.

## Desviaciones y hallazgos

1. El diseño esperaba que T-05 pasara inmediatamente después del cause-root fix. Al cubrir también `IsBlockedView`, el test encontró que `Index.cshtml` ocultaba Bloquear/Eliminar para self pero no Desbloquear. Se corrigió con un guard mínimo de una condición, requerido por REQ-UCB-09.
2. La instrucción original de T-01 en `tasks.md` pedía cambiar el ID de Index a `admin`, pero eso habría preservado el workaround y ocultado el bug. Se corrigió el artefacto para mantener `admin-test` y endurecer el assert sensible a whitespace.
3. El inventario textual esperado para el grep no coincidía literalmente con el repo: tras el fix ya no debe aparecer `AuthSessionFactory.cs`, y el scope incluye referencias de tests API preexistentes. No se detectaron consumidores nuevos ni supuestos adicionales sobre el claim manual.

## Workload / límite de PR

- **Modo**: chained PR slice, `stacked-to-main`.
- **Boundary**: desde el principal con doble `NameIdentifier` hasta un principal cuya única identidad proviene del JWT, con auto-fence UI en vistas activa y bloqueada.
- **Runtime harness**: N/A separado; los tests con `WebApplicationFactory` ejercitan el boundary HTTP real de SGV.Web y el bridge de autenticación.
- **Rollback**: revertir el commit del work unit restaura la siembra manual, el comentario anterior, el guard de Desbloquear y los tests asociados, sin tocar datos ni migraciones.
- **Forecast final**: 162 líneas modificadas staged (141 adiciones + 21 eliminaciones), por debajo del budget de 400 líneas.

## Tareas restantes

- [ ] T-07..T-13 — PR 2 SweetAlert2.
- [ ] T-14..T-15 — validaciones opcionales/post-merge.

## Estado final

PR 1 listo para commit y `sdd-verify`. No se realizó push ni se creó pull request.
