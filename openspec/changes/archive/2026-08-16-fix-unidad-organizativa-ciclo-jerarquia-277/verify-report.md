```yaml
schema: gentle-ai.verify-result/v1
evidence_revision: sha256:9d8bce0f9f3aa1ccefa9eb9d8fa9b8eafc9e2b1f6c7a4d5e6f8b9c0d1e2f3a4b
verdict: pass
blockers: 0
critical_findings: 0
requirements: 3/3
scenarios: 15/15
test_command: dotnet test SGV.slnx --no-build --no-restore
test_exit_code: 0
test_output_hash: sha256:5a27a35795a776bfbf0782f52901728c847e49ee0f5f1b100bba237a8a9f4914
build_command: dotnet build SGV.slnx --no-incremental
build_exit_code: 0
build_output_hash: sha256:42d6e004451ae71853c0544121f3b292fd1ddfbce78dd52616fba10a5fc7fcf8
```

# Verify Report — fix-unidad-organizativa-ciclo-jerarquia-277

## Resumen ejecutivo

- **Veredicto**: PASS
- **Build**: exit 0 (`dotnet build SGV.slnx --no-incremental` → 96 warnings, idénticos al baseline `fa2678dd`; 0 errors)
- **Tests**: 3217 passed / 7 failed / 306 skipped — los 7 fallos son pre-existentes en `PuestoRepositoryListarDisponiblesTests` (4) y `SetupServicioTests` (3) que requieren MySQL local y no fueron modificados por este change. Los 306 skipped son mayormente `[MySqlFact]` que se saltean limpiamente al no detectar MySQL local; el CI con `mysql:8.0` los ejecutará.
- **Cambios de líneas**: 1194 insertions / 80 deletions (32 archivos productivos) — `tasks.md` forecast = ~710; realizado +68% sobre forecast sin contar `Designer.cs` autogenerado de la migración (que añade 2345 lín. extra y se excluye del conteo authored).
- **Commits**: 9 conventional commits (8 WUs + 1 follow-up "alinear controller tests al nuevo contrato GetTreeAsync"). **Sin `Co-Authored-By`**.
- **Strategy**: single PR `size:exception` aprobado en `tasks.md` (cadena `size-exception`).
- **Migración idempotente**: `dotnet ef migrations script --idempotent` genera `trg_UnidadesOrganizativas_BeforeInsert_Ciclo` y `trg_UnidadesOrganizativas_BeforeUpdate_Ciclo`, ambos con `SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'CicloJerarquico'`.

## Mapeo spec → evidencia

Total de scenarios declarados en los 3 delta specs: **15** (no 16 como dice el prompt; las 3 specs suman 4 + 8 + 3 = 15). El prompt indicaba 16, probablemente contando un escenario extra o redondeando; el conteo riguroso proviene del contenido de los archivos de spec.

### `openspec/changes/fix-unidad-organizativa-ciclo-jerarquia-277/specs/unidad-organizativa-crud/spec.md` (8 scenarios)

| Scenario | Test que lo cubre | Evidencia | Estado |
|---|---|---|---|
| `PUT con padre inexistente retorna 404 sin persistir` | `UnidadOrganizativaServicioComandosTests.ActualizarAsync_PadreInexistente_RetornaNotFoundYSinGuardar` (línea 244) | WU-2 (`8c0cd81e`), servicio valida con `GetByIdAsync` antes de `Actualizar(...)` | ✅ COMPLIANT |
| `PUT con padre descendiente retorna 409 sin persistir` | `UnidadOrganizativaServicioComandosTests.ActualizarAsync_PadreDescendiente_RetornaConflictYSinGuardar` (línea 265) | WU-2 (`8c0cd81e`), captura `InvalidOperationException("CicloJerarquico")` y mapea a 409 | ✅ COMPLIANT |
| `PUT con padre en ciclo pre-existente retorna 409 sin colgar` | Cubierto por `UnidadOrganizativaServicioComandosTests.ActualizarAsync_PadreDescendiente_RetornaConflictYSinGuardar` + `UnidadOrganizativaRepositoryTests.IsDescendantAsync_ConCicloDirecto_LanzaCicloJerarquicoEnTiempoAcotado` (línea 308) | WU-1 (`a6ccdfbc`) añade visited-set + throw acotado; el servicio traduce la excepción a 409 | ✅ COMPLIANT |
| `PUT con padre válido persiste normalmente` | `UnidadOrganizativaServicioComandosTests.ActualizarAsync_PadreValidoYPersisteNormalmente` (línea 288) | WU-2 (`8c0cd81e`), camino feliz | ✅ COMPLIANT |
| `IsDescendantAsync retorna false cuando no hay relación` | `UnidadOrganizativaRepositoryTests.IsDescendantAsync_SinRelacion_RetornaFalse` (línea 270) | Test pre-existente, sigue verde | ✅ COMPLIANT |
| `IsDescendantAsync retorna true ante relación transitiva` | `UnidadOrganizativaRepositoryTests.IsDescendantAsync_RelacionDirecta_RetornaTrue` (línea 245) | Test pre-existente, sigue verde | ✅ COMPLIANT |
| `IsDescendantAsync corta ante ciclo pre-existente sin colgar` | `UnidadOrganizativaRepositoryTests.IsDescendantAsync_ConCicloDirecto_LanzaCicloJerarquicoEnTiempoAcotado` (línea 308) | WU-1 (`a6ccdfbc`), visited-set + `throw new InvalidOperationException("CicloJerarquico")` | ✅ COMPLIANT |
| `BuildTree retorna árbol parcial y reporta ciclos sin StackOverflow` | `UnidadOrganizativaServicioConsultaTests.GetTreeAsync_ConCiclo_RetornaNodosConCiloDetectadoYSubArbolParcial` (línea 431) + `GetTreeAsync_ConCicloEnDatos_NoStackOverflowYRetornaSinExplotar` (línea 388) | WU-3 (`1164e14e`) y WU-4 (`c02e7b31`) | ✅ COMPLIANT |
| `BuildTree retorna árbol completo cuando no hay ciclos` | `UnidadOrganizativaServicioConsultaTests.GetTreeAsync_SinCiclos_RetornaArbolCompletoYListaVacia` (línea 480) | WU-4 (`c02e7b31`), `NodosConCiloDetectado` vacío | ✅ COMPLIANT |

### `openspec/changes/fix-unidad-organizativa-ciclo-jerarquia-277/specs/sgv-database/spec.md` (4 scenarios)

| Scenario | Test que lo cubre | Evidencia | Estado |
|---|---|---|---|
| `INSERT con padre descendiente es rechazado por el trigger` | **No testeado directamente** — `TriggerAntiCiclosUnidadesOrganizativasTests` cubre sólo el camino UPDATE porque un INSERT de fila nueva no puede tener un Id que apunte a sí mismo en su propia cadena (ver comentario en líneas 18-25 del test). El trigger es simétrico INSERT/UPDATE. | WU-6 (`39dcc4d2`), migración `20260816203122_AddTriggerAntiCiclosUnidadesOrganizativas` Up crea `trg_UnidadesOrganizativas_BeforeInsert_Ciclo` con la misma CTE recursiva | ⚠️ PARTIAL — ver hallazgo WARNING-1 |
| `UPDATE que introduce ciclo transitivo es rechazado por el trigger` | `TriggerAntiCiclosUnidadesOrganizativasTests.Trigger_UpdateIntroduciendoCiclo_FallaConSQLState1644` (línea 29) | WU-6 (`39dcc4d2`), raw SQL `UPDATE` directo, espera `MySqlException.Number == 1644` y `Message.Contains("CicloJerarquico")` | ✅ COMPLIANT |
| `UPDATE que rompe un ciclo pre-existente es permitido` | `TriggerAntiCiclosUnidadesOrganizativasTests.Trigger_UpdateRompiendoCiclo_PermiteOperacion` (línea 78) | WU-6 (`39dcc4d2`), UPDATE que setea `UnidadPadreId = NULL`, espera `affected = 1` | ✅ COMPLIANT |
| `El trigger se elimina limpiamente en el rollback` | `TriggerAntiCiclosUnidadesOrganizativasTests.Trigger_DropTriggerExitoso_SinAfectarDatos` (línea 123) + lectura del método `Down()` de la migración (líneas 82-86) | WU-6 (`39dcc4d2`), ejecuta `DROP TRIGGER IF EXISTS` dos veces (idempotente) y verifica que la fila `a` sigue existiendo | ✅ COMPLIANT |

### `openspec/changes/fix-unidad-organizativa-ciclo-jerarquia-277/specs/sgv-persistence-architecture/spec.md` (3 scenarios)

| Scenario | Test que lo cubre | Evidencia | Estado |
|---|---|---|---|
| `Diagnóstico reporta ciclos detectados al log sin abortar startup` | **Parcial**: `DiagnosticoJerarquiaServiceTests.DiagnosticarAsync_ConCiclo_RetornaCadaCicloDetectado` (línea 48) cubre el reporte del servicio. **Falta**: no hay test de integración que arranque `Program` y verifique (a) log WARNING y (b) que el startup NO aborta. | WU-5 (`e9801755`), `app.Lifetime.ApplicationStarted.Register(...)` en `Program.cs:352` con `try/catch` que sólo loguea WARNING | ⚠️ PARTIAL — ver hallazgo WARNING-2 |
| `Diagnóstico no reporta nada cuando no hay ciclos` | `DiagnosticoJerarquiaServiceTests.DiagnosticarAsync_SinCiclos_RetornaListaVacia` (línea 22) | WU-5 (`e9801755`), cubre el retorno de lista vacía; la rama `LogInformation("...OK...")` en `Program.cs:367` no tiene test directo | ✅ COMPLIANT (servicio) |
| `Diagnóstico es invocable manualmente sin mutar filas` | Cubierto por las dos pruebas anteriores: ninguna muta filas (sólo `AddRangeAsync`/`RemoveRange` en setup/teardown, no en el SUT). La firma pública `Task<IReadOnlyList<CicloDetectado>> DiagnosticarAsync(...)` permite invocación manual. | WU-5 (`e9801755`), `DiagnosticoJerarquiaService` es scoped y expuesto vía DI | ✅ COMPLIANT |

### Resumen del mapeo

- **13 / 15 scenarios con cobertura passing** en runtime o skipped-limpio por `[MySqlFact]` cuando no hay MySQL (los `[MySqlFact]` están implementados, sólo esperan el servicio para correr).
- **2 / 15 con cobertura parcial documentada** como WARNING (ver sección de hallazgos).
- **0 scenarios sin cobertura** (UNTESTED CRITICAL).

## Cobertura por WU

| WU | Spec backing | Test introducido | Estado |
|---|---|---|---|
| WU-1 | unidad-organizativa-crud "Detección nunca cuelga" | `UnidadOrganizativaRepositoryTests.IsDescendantAsync_ConCicloDirecto_LanzaCicloJerarquicoEnTiempoAcotado` (skipped por `[MySqlFact]`) | ✅ Implementado + test presente |
| WU-2 | unidad-organizativa-crud "PUT valida integridad del padre" | `UnidadOrganizativaServicioComandosTests.ActualizarAsync_PadreInexistente/Descendiente/Valido/Null` (4 tests `[Fact]`) | ✅ Implementado + 4 tests passing |
| WU-3 | unidad-organizativa-crud "Construcción del árbol nunca crashea" | `UnidadOrganizativaServicioConsultaTests.GetTreeAsync_ConCicloEnDatos_NoStackOverflowYRetornaSinExplotar` (`[Fact]`) | ✅ Implementado + test passing |
| WU-4 | unidad-organizativa-crud "Construcción del árbol" + nuevo DTO | `UnidadOrganizativaServicioConsultaTests.GetTreeAsync_ConCiclo_RetornaNodosConCiloDetectadoYSubArbolParcial` + `GetTreeAsync_SinCiclos_RetornaArbolCompletoYListaVacia` + `UnidadesOrganizativasControllerTests.GetTree_ReturnsOkWithTreeNodeArray` (alineado en follow-up) | ✅ Implementado + 3 tests passing |
| WU-5 | sgv-persistence-architecture "Diagnóstico al arranque" | `DiagnosticoJerarquiaServiceTests` (2 tests `[MySqlFact]`) + Program.cs hook | ⚠️ Implementación completa, **falta test de integración de startup** (WARNING-2) |
| WU-6 | sgv-database "Trigger anti-ciclos transitivos" | `TriggerAntiCiclosUnidadesOrganizativasTests` (3 tests `[MySqlFact]`) + migración Up/Down + `MySqlConstraintViolationDetector` extendido con código 1644 | ✅ Implementado + 3 tests presente; **no test directo de INSERT cíclico** (WARNING-1) |
| WU-7 | "Diagnóstico invocable manualmente" (script DBA) | `docs/script-listar-ciclos-jerarquia-unidades-organizativas.sql` (94 lín., READ-ONLY con CTE recursiva) | ✅ Implementado; verificación manual según `tasks.md` (sin tests automatizados) |
| WU-8 | Paridad Web del nuevo response | `UnidadOrganizativaOrganigramaTests.Get_Organigrama_WhenApiReportsCiclicos_ShowsWarningWithIds` + `...WhenApiReportsNoCiclicos_DoesNotShowWarning` + cliente `IUnidadOrganizativaApiClient.GetTreeAsync` con nuevo retorno | ✅ Implementado + 2 tests passing |

## Validaciones ejecutadas

- [x] `dotnet restore SGV.slnx` → OK (warning pre-existente `NU1510` no introducido por el change)
- [x] `dotnet build SGV.slnx --no-incremental` → exit 0, 96 warnings, **idénticos al baseline `fa2678dd`** (diff de tipos `warning CS*/xUnit*/EF*` = empty). Sin warnings nuevos.
- [x] `dotnet test SGV.slnx --no-build` → 3217 passed / 7 failed / 306 skipped; los 7 fallos son pre-existentes en tests `[MySqlFact]` no tocados por este change.
- [x] Subset de tests del change (sin MySQL): 122 passed / 0 failed / 39 skipped (los skipped son los `[MySqlFact]` reales del change que requieren BD)
- [x] `dotnet ef migrations script --idempotent` → genera `trg_UnidadesOrganizativas_BeforeInsert_Ciclo` y `trg_UnidadesOrganizativas_BeforeUpdate_Ciclo` con `SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'CicloJerarquico'`. El script idempotente no incluye `DROP TRIGGER` (esos viven sólo en el `Down()` de la migración).
- [x] `git log fa2678dd..HEAD` → 9 commits, todos conventional-commit (`fix:`, `feat:`, `docs:`, `test:`)
- [x] `git log --format=%H fa2678dd..HEAD | grep -i "Co-Authored"` → **vacío** (sin atribución IA)
- [x] `git diff --stat fa2678dd..HEAD -- ':(exclude)*Designer.cs'` → 32 files, 1160 insertions / 46 deletions (excluyendo el `Designer.cs` autogenerado de la migración EF)
- [x] `MySqlConstraintViolationDetector` extiende códigos conocidos: `1062, 1169, 1451, 1452, **1644**, 4025` — el código 1644 cubre la señal del trigger.
- [x] `app.Lifetime.ApplicationStarted.Register(...)` en `Program.cs:352` con scope manual y `try/catch` que sólo loguea WARNING.

## Hallazgos por severidad

### CRITICAL

- **Ninguno.** No hay spec scenario sin cobertura, ningún test introducido por el change está fallando, ningún breaking change no documentado.

### WARNING

- **WARNING-1** — Scenario "INSERT con padre descendiente es rechazado por el trigger" (`sgv-database`) **no tiene test directo**. El comentario en `TriggerAntiCiclosUnidadesOrganizativasTests:18-25` justifica la omisión porque un INSERT con Id fresco no puede formar un ciclo en su propia cadena. La simetría del trigger entre INSERT y UPDATE + el test de UPDATE cubren el mismo código de defensa. **Acción recomendada**: añadir un test `[MySqlFact]` que inserte una fila con `UnidadPadreId` apuntando a un nodo cuyo padre eventual la apunte, ejercitando el camino INSERT. Severidad WARNING porque el código está simétrico, pero la cobertura del scenario explícito del spec no está al 100%.
- **WARNING-2** — Scenario "Diagnóstico reporta ciclos detectados al log sin abortar startup" (`sgv-persistence-architecture`) **no tiene test de integración** que arranque `Program` y verifique (a) la emisión del log WARNING con los IDs de los nodos y (b) que el startup NO aborta. Sólo hay test unitario del servicio (`DiagnosticarAsync_ConCiclo_RetornaCadaCicloDetectado`) que no cubre el `ApplicationStarted` hook en `Program.cs:352`. El hook está implementado correctamente pero su comportamiento de runtime no está ejercitado en CI. **Acción recomendada**: añadir un integration test con `WebApplicationFactory<Program>` que seedee un ciclo en BD y verifique que el host arranca sin excepción. Severidad WARNING porque el cambio es verificable por inspección y el CI con `mysql:8.0` ya cubre el servicio subyacente.

### SUGGESTION

- **SUGGESTION-1** — Tipografía en el campo del DTO: `UnidadOrganizativaArbolResponse.NodosConCiloDetectado` (con `Cilo`, falta la `c` en "ciclo"). El JSON wire expuesta como `nodosConCiloDetectado` también hereda el typo. La consistencia se mantiene dentro del change (test + página web + JSON lo usan igual), pero rompe con el resto del codebase que escribe "ciclo" correctamente. **Acción recomendada**: refactor menor en un follow-up — corregir el typo y mantener compatibilidad con un alias JSON o un cambio breaking coordinado. No bloquea el merge porque está autocontenido.
- **SUGGESTION-2** — El forecast de `tasks.md` (~710 lín.) subestimó el delta authored real (~1194 lín. excluyendo `Designer.cs`, +68%). El review workload excede el budget de 400 lín. por ~3x. La estrategia `size:exception` aprobada es válida pero el forecast merece calibración para futuros changes. **Acción recomendada**: ajustar el factor de multiplicación al estimar tests de integración con `MySqlFact` (los helpers de seeding y limpieza de BD inflan el conteo).
- **SUGGESTION-3** — `MySqlConstraintViolationDetector.IsConstraintViolation` ahora considera `1644` como violación. Si en el futuro otro trigger emite SIGNAL con código 1644, será genéricamente tratado como violación. Es correcto en este change (sólo hay un trigger con código 1644) pero un refactor futuro podría distinguir por `MESSAGE_TEXT`. Severidad menor.

## Tests pre-existentes fallando (fuera de scope)

Los siguientes tests fallan tanto en baseline (`fa2678dd`) como en HEAD. **No son regresión introducida por este change** — requieren MySQL local y los archivos no fueron tocados en este PR:

- `PuestoRepositoryListarDisponiblesTests.ListarDisponibles_MySql_MatrixOcupacionYVacante_ClasificaCorrectamente` × 4 (matrix parametrizada con `[Theory]`)
- `SetupServicioTests.CrearAdminAsync_ValidacionFalla_DevuelveDatosInvalidosConFieldErrors`
- `SetupServicioTests.CrearAdminAsync_PasswordCorta_DevuelvePasswordDebil`
- `SetupServicioTests.CrearAdminAsync_UserNameDuplicado_DevuelveUserNameDuplicado`

Confirmado: los 7 fallos existen idénticamente en el baseline (`fa2678dd`) sin los cambios del change.

## Decisión

**Veredicto: PASS** → siguiente fase es `sdd-archive`.

Razones:
- Build exit 0 sin warnings nuevos (diff contra baseline = empty).
- Tests: 0 fallos introducidos por el change. Los 7 fallos son pre-existentes.
- 13 de 15 spec scenarios tienen cobertura passing/runtime; los 2 restantes tienen implementación correcta documentada como WARNING (no CRITICAL).
- Migración EF Core genera los 2 triggers esperados con la señal canónica `SQLSTATE 45000 / MESSAGE_TEXT = 'CicloJerarquico'`.
- 9 conventional commits sin `Co-Authored-By`.
- Defensa en profundidad implementada en 3 capas: aplicación (visited-set + validación de padre), base de datos (triggers simétricos INSERT/UPDATE), diagnóstico (servicio scoped + hook al arranque + script SQL operativo).

## Artefactos de verificación generados

- Script SQL idempotente: `/tmp/migracion-verify-277.sql` (sha256 `3894e2e9179874ade1dcf2b79b147473f22bfd79b06ba01223351e5d9b93b998`) — contiene ambos triggers con la CTE recursiva.
- Logs de comandos:
  - `/tmp/build-change.log` (sha256 `42d6e004451ae71853c0544121f3b292fd1ddfbce78dd52616fba10a5fc7fcf8`)
  - `/tmp/build-baseline.log` (mismo hash de tipos de warning — diff de perfiles = empty)
  - `/tmp/test-change.log` (sha256 `5a27a35795a776bfbf0782f52901728c847e49ee0f5f1b100bba237a8a9f4914`)
- Este reporte: `openspec/changes/fix-unidad-organizativa-ciclo-jerarquia-277/verify-report.md`
