# Apply Progress: fix-vacante-estado-inicial-no-terminal-issue-236

## Estado

**completed (with documented pre-existing failures).** Las tres fases (RED, GREEN, verify) están cerradas, los 38 tests del scope pasan en verde y el commit `2bfa58c` quedó registrado sobre `develop`. Los 6 fallos de la suite completa están documentados como preexistentes y no fueron introducidos por este change.

## Tareas completadas

- [x] 1.1 `Crear_EstadoInicialTerminalCubierta_RetornaValidationFailure` agregado.
- [x] 1.2 `Crear_EstadoInicialTerminalCancelada_RetornaValidationFailure` agregado.
- [x] 1.3 `Create_EstadoInicialTerminal_Returns400WithValidationProblemDetails` agregado.
- [x] 1.4 RED confirmado: los 2 tests unitarios que dependen del código nuevo fallaron; el test API de wiring pasó por diseño.
- [x] 2.1 Validación `estadoVacante.EsTerminal` implementada en `VacanteServicioComandos.CrearAsync`.
- [x] 2.2 GREEN enfocado confirmado: 38/38 tests pasaron.
- [x] 3.1 `dotnet build SGV.slnx` pasó (warnings NU1510 preexistentes, 0 errores).
- [x] 3.2 Filtro enfocado 38/38 verde. Suite completa con 6 fallos preexistentes documentados.
- [x] 3.3 `git diff` confirma scope y commit `2bfa58c` creado sobre `develop`.

## Tests agregados

1. `Crear_EstadoInicialTerminalCubierta_RetornaValidationFailure` — test unitario (Caso Cubierta).
2. `Crear_EstadoInicialTerminalCancelada_RetornaValidationFailure` — test unitario (Caso Cancelada, triangulación).
3. `Create_EstadoInicialTerminal_Returns400WithValidationProblemDetails` — test de integración API para el wiring existente de `400 ValidationProblemDetails`.

## Evidencia TDD

| Tarea | Archivo | Safety net | RED | GREEN | TRIANGULATE | REFACTOR |
|---|---|---|---|---|---|---|
| 1.1 | `tests/SGV.Tests/Aplicacion/Vacantes/VacanteServicioComandosTests.cs` | ✅ Suite enfocada previa preservada | ✅ `Assert.False`: esperado `false`, actual `true` | ✅ 38/38 enfocados | ✅ Caso Cubierta | ➖ No fue necesario; implementación mínima y legible |
| 1.2 | `tests/SGV.Tests/Aplicacion/Vacantes/VacanteServicioComandosTests.cs` | ✅ Suite enfocada previa preservada | ✅ `Assert.False`: esperado `false`, actual `true` | ✅ 38/38 enfocados | ✅ Caso Cancelada | ➖ No fue necesario; implementación mínima y legible |
| 1.3 | `tests/SGV.Tests/Api/VacantesControllerTests.cs` | ✅ Wiring previo protegido | ⚠️ Pasó antes de producción (`1 passed`) por usar un fake que devuelve el failure esperado | ✅ Incluido en 38/38 enfocados | ➖ El mapeo API existente ya cubre este comportamiento | ➖ Sin cambios de controlador |

## Evidencia de work units

| Work unit | Evidencia | Resultado |
|---|---|---|
| WU-1 RED | Comando enfocado | `dotnet test SGV.slnx --filter "FullyQualifiedName~EstadoInicialTerminal"` — exit 1; `Failed: 2, Passed: 1, Skipped: 0, Total: 3`. |
| WU-1 RED | Harness runtime | El test API ejecutó `WebApplicationFactory` y confirmó `400 ValidationProblemDetails`; pasó antes de producción por diseño. |
| WU-1 RED | Rollback | Eliminar los 3 tests nuevos de los 2 archivos de tests. |
| WU-2 GREEN | Comando enfocado | `dotnet test SGV.slnx --no-restore --filter "FullyQualifiedName~VacanteServicioComandosTests\|FullyQualifiedName~VacantesControllerTests"` — exit 0; `Passed: 38, Failed: 0, Skipped: 0, Total: 38`, duration 366 ms. |
| WU-2 GREEN | Harness runtime | El mismo comando incluye `VacantesControllerTests` con `WebApplicationFactory`; 38/38 pasaron. |
| WU-2 GREEN | Rollback | Revertir exclusivamente el bloque `if (estadoVacante.EsTerminal)` de `VacanteServicioComandos.cs`. |
| WU-3 verify | Build | `dotnet build SGV.slnx` — exit 0; 0 errores, 4 warnings NU1510 preexistentes. |
| WU-3 verify | Suite completa | `dotnet test SGV.slnx` — exit 1; `Passed: 3320, Failed: 6, Skipped: 0, Total: 3326`. Los 6 fallos son **preexistentes**, no introducidos por este change (ver § Fallos preexistentes). |
| WU-3 verify | Sanity final | Filtro enfocado reejecutado tras el commit: 38/38 verde. |
| WU-3 verify | Rollback | N/A: la verificación no agregó código ni un commit adicional. |

## Fallos preexistentes de la suite

Los siguientes 6 tests fallaron en baseline (sin cambios del change) y en la corrida de verificación. **No son bloqueantes para `fix-vacante-estado-inicial-no-terminal-issue-236`** porque ninguno toca archivos ni tests del scope.

| # | Test | Razón del fallo | Bloqueante |
|---|---|---|---|
| 1 | `SwaggerConfigurationTests.NonOrgResources_OnlyExposeGetOperations` | Asume solo operaciones GET, pero el PR #231 agregó `POST /api/v1/vacantes`. Asumción desactualizada por código vigente. | No |
| 2 | `PersonaRepositoryTests.QueryAsync_SoloSinUsuarioFalseONull_PreservaBackCompat` | `[MySqlFact]` sin MySQL local accesible; se skipea solo si el bootstrap llega a la BD, pero cae en una incompatibilidad previa. | No (entorno) |
| 3 | `MigracionD7MySqlFactTests.UniqueIndex_PersonaId_PreventsDuplicateAssignment` | `[MySqlFact]`; falla por FK `FK_AspNetUsers_Personas_PersonaId` al no tener MySQL local accesible. | No (entorno) |
| 4 | `SetupHappyPathMySqlFactTests.Crear_DatosValidos_CreaPersonaUsuarioRolYAuditoria` | `[MySqlFact]`; esperado `OK`, actual `Conflict` por estado de la BD local sin migrar. | No (entorno) |
| 5 | `SoftDeletedUserLoginTests.Login_AfterFiveFailedAttempts_EvenCorrectPasswordReturns401` | `[MySqlFact]`; usuario `admin` duplicado en la BD local. | No (entorno) |
| 6 | `SoftDeletedUserLoginTests.Login_WithUnlockedUser_AfterPreviousLockout_Returns200AndIssuesToken` | `[MySqlFact]`; falla por FK `FK_AspNetUsers_Personas_PersonaId` sin MySQL local accesible. | No (entorno) |

Los 4 fallos con `[MySqlFact]` se skipean solos cuando no hay MySQL accesible y solo aparecen en este entorno porque la cadena de fallback (`127.0.0.1:1`) devolvió un error de transporte en lugar de un skip limpio. Es un problema de configuración del runner local, no del código.

## Archivos modificados

### Código del change (commiteado en `2bfa58c`)

- `src/SGV.Aplicacion/Vacantes/Comandos/VacanteServicioComandos.cs` — bloque `if (estadoVacante.EsTerminal)` insertado antes de `ExistsAbiertaByPuestoAsync`.
- `tests/SGV.Tests/Aplicacion/Vacantes/VacanteServicioComandosTests.cs` — 2 tests nuevos (`Cubierta`, `Cancelada`).
- `tests/SGV.Tests/Api/VacantesControllerTests.cs` — 1 test nuevo (`Create_EstadoInicialTerminal_Returns400WithValidationProblemDetails`).

### Artefactos SDD (untracked, sin commitear)

- `openspec/changes/fix-vacante-estado-inicial-no-terminal-issue-236/proposal.md`
- `openspec/changes/fix-vacante-estado-inicial-no-terminal-issue-236/design.md`
- `openspec/changes/fix-vacante-estado-inicial-no-terminal-issue-236/tasks.md`
- `openspec/changes/fix-vacante-estado-inicial-no-terminal-issue-236/specs/**`
- `openspec/changes/fix-vacante-estado-inicial-no-terminal-issue-236/apply-progress.md` (este archivo)

> Los artefactos SDD quedan sin commitear por decisión del orquestador: se commitean junto al PR de `sdd-archive` o se dejan untracked si el change se archiva en otra corrida. **No se incluyen en este commit de feat/test.**

## Commits creados

| Hash corto | Hash completo | Mensaje | Archivos |
|---|---|---|---|
| `2bfa58c` | `2bfa58c07ad023f450246bdeffb7beb7d836226f` | `feat(vacantes): reject terminal estado inicial on create` | 3 archivos, 87 insertions |

`HEAD` ahora está `1 commit ahead of origin/develop` sobre `develop`. No se creó el commit `chore(vacantes): validate suite` porque la verificación no agregó cambios.

## Revisión opcional del cliente Web

**No completada en esta corrida.** Sigue anotada como follow-up fuera de scope del change. Ver `tasks.md` § Riesgos (cliente Web asume `409` para `EstadoTerminalInmutable`; ahora es `400` con estado terminal).

## Próximo paso

`sdd-verify` para auditoría independiente de la implementación contra `proposal.md`, `design.md` y `tasks.md`. Después de verificar, queda decidir entre:

1. Push del commit `2bfa58c` y abrir PR de feat/test contra `develop`.
2. `sdd-archive` para mover el delta spec bajo `openspec/specs/vacantes/` y limpiar `openspec/changes/`.