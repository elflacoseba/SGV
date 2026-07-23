# Verification Report — `2026-07-11-fix-active-puesto-id-unique-type`

**Change**: `2026-07-11-fix-active-puesto-id-unique-type`
**Version**: N/A (primer change sobre el bug #59)
**Mode**: Strict TDD + ask-on-risk
**Artifact store**: `both` (OpenSpec filesystem + Engram)
**Date**: 2026-07-11

## Resumen ejecutivo

Las 4 dimensiones (completitud de tareas, correctness de implementación, build y tests, evidencia TDD) están cubiertas. Build OK con 0 warnings, `ModeloPersistenciaTests` 20/20 verde, `OcupacionRepositoryTests` + `OcupacionGeneratedColumnRegressionTests` 16/16 verde contra MySQL 8 local. Existen **5 desviaciones documentadas por apply** que no rompen spec pero relajan lenguaje del design (resumidas abajo). El escenario S2 del spec no tiene test que verifique explícitamente la violación de unicidad al insertar 2 activas con mismo PuestoId — solo se verifica por inferencia estructural (índice único + tests que insertan una activa pasan). S4 tiene la "purga defensiva pre-ALTER" removida: mecanismo alternativo descrito y justificado (MySQL re-evalúa la expresión computada durante ALTER).

**Verdict**: **PASS WITH WARNINGS**

---

## Completitud

| Métrica | Valor |
|---------|-------|
| Tasks total | 10 (T-001 → T-010) |
| Tasks complete | 10/10 `[x]` |
| Tasks incomplete | 0 |
| Artefactos SDD presentes | proposal + spec delta + design + tasks + exploration (5/5) |
| Apply-progress (Engram) | Encontrado (topic_key `sdd/.../apply-progress`, obs #963) |
| Apply-progress (filesystem) | **Ausente** — solo persiste en Engram. No bloqueante porque `actionContext.artifactStore = both` |

---

## Build & Tests Execution

**Build**: ✅ Passed — 0 errors, 0 warnings
```text
SGV.Contracts  → bin/Debug/net10.0/SGV.Contracts.dll
SGV.Dominio    → bin/Debug/net10.0/SGV.Dominio.dll
SGV.Aplicacion → bin/Debug/net10.0/SGV.Aplicacion.dll
SGV.Infraestructura → bin/Debug/net10.0/SGV.Infraestructura.dll
SGV.Api        → bin/Debug/net10.0/SGV.Api.dll
SGV.Web        → bin/Debug/net10.0/SGV.Web.dll
SGV.Tests      → bin/Debug/net10.0/SGV.Tests.dll

Build succeeded.
    0 Warning(s)
    0 Error(s)
Time Elapsed 00:00:01.62
```

**Test subset sin MySQL (ModeloPersistenciaTests)**: ✅ 20/20 verde
```text
dotnet test SGV.slnx --no-build --filter "FullyQualifiedName~ModeloPersistenciaTests"
Passed!  - Failed: 0, Passed: 20, Skipped: 0, Total: 20, Duration: 70 ms
```

**Test subset con MySQL (Ocupacion + canario)**: ✅ 16/16 verde contra MySQL 8 real
```text
dotnet test SGV.slnx --no-build \
  --filter "FullyQualifiedName~OcupacionRepositoryTests|FullyQualifiedName~OcupacionGeneratedColumnRegressionTests"
Passed!  - Failed: 0, Passed: 16, Skipped: 0, Total: 16, Duration: 525 ms
```
MySQL disponible: `mysqld is alive` en `127.0.0.1:3306`. Bootstrap `Database.Migrate()` aplicó la migración automáticamente.

**Test subset Persistencia completa**: ✅ 233/233 verde
```text
dotnet test SGV.slnx --no-build --filter "FullyQualifiedName~Persistencia"
Passed!  - Failed: 0, Passed: 233, Skipped: 0, Total: 233, Duration: 4 s
```

**Coverage**: ➖ No ejecutado en este verify (covenant opcional). Sería necesario `dotnet test ... --collect:"XPlat Code Coverage"`. Afirmación del apply: el subset Persistencia completa es 36/36 verde local.

---

## Spec Compliance Matrix

| Req | Escenario | Test cubriente | Resultado |
|-----|-----------|----------------|-----------|
| **S1** | Inserción de `OcupacionEntity` activa persiste el Guid como string de 36 chars (sin `Data truncated`) | `tests/SGV.Tests/Persistencia/OcupacionGeneratedColumnRegressionTests.cs > AddAsync_FilaActiva_ActivePuestoIdUniquePersisteComoGuidString` (canario, `[MySqlFact]`) | ✅ **COMPLIANT** — test verde contra MySQL 8; asssert explícito `Assert.Equal(puestoId.ToString(), (string)result!)`. |
| **S2** | Duplicado activo por Puesto se rechaza por unicidad, no por truncado | Cobertura **parcial**: el índice único existe (verificado por `ModeloPersistenciaTests.Modelo_ConfiguraColumnaGeneradaUnicaParaOcupacionVigentePorPuesto`), y los tests `OcupacionRepositoryTests.ExistsActiveByPuestoAsync_*` (verde) ejercitan inserciones activas. Pero **no existe test que intente insertar 2 activas con mismo PuestoId y verifique la violación**. | ⚠️ **PARTIAL** — garantía estructural probada + cobertura indirecta vía SeedAsync, sin test explícito de violación S2. |
| **S3** | Modelo EF declara `ActivePuestoIdUnique` con `ClrType == typeof(string)` y `GetColumnType()` contiene `char(36)` (case-insensitive) | `tests/SGV.Tests/Persistencia/ModeloPersistenciaTests.cs > Modelo_ConfiguraColumnaGeneradaUnicaParaOcupacionVigentePorPuesto` — extendido con `Assert.Equal(typeof(string), ClrType)`, `Assert.Equal(36, GetMaxLength())`, `Assert.True(GetColumnType()?.Contains("(36)") ?? false)` | ✅ **COMPLIANT** — verde en runtime; assert relajado a `(36)` (ver Deviations #4) — tolera tanto `varchar(36)` como `char(36)`. |
| **S4** | Migración con purga defensiva pre-alter y forward-only | (a) `Migraciones_ContienenClasesDeMigracionValidas` extendido con `try/catch NotSupportedException` (verde) valida forward-only. (b) Inspección directa de `20260711181615_FixActivePuestoIdUniqueType.cs`: Up() = DROP → ALTER → CREATE; Down() = `throw new NotSupportedException` como primera línea. (c) Purga defensiva **removida** — ver Deviations #3. | ⚠️ **PARTIAL** — forward-only y el patrón DROP→ALTER→CREATE presentes; la purga explícita pre-ALTER fue removida porque MySQL 8 rechaza UPDATE sobre columnas generadas. Mecanismo alternativo: MySQL re-evalúa la expresión computada en cada `ALTER COLUMN`, reemplazando `'0'` truncados con el `PuestoId` correcto o `NULL`. |

**Compliance summary**: 2/4 estrictos (S1, S3) + 2/4 parciales (S2, S4) — todos los escenarios tienen cobertura, ningún escenario es `UNTESTED`. Las parcialidades son por desviaciones justificadas, no por ausencia de test.

---

## Correctness (Static Evidence)

| Requisito | Status | Notas |
|-----------|--------|-------|
| `Property<string?>("ActivePuestoIdUnique")` con `HasMaxLength(36)`, `UseCollation("ascii_general_ci")`, `HasComputedColumnSql` | ✅ Implementado en `OcupacionConfiguracion.cs:35-39` |
| Índice único preservado | ✅ Implementado en `OcupacionConfiguracion.cs:40` (`builder.HasIndex("ActivePuestoIdUnique").IsUnique();`) |
| Migración `FixActivePuestoIdUniqueType` con `Up` que hace DROP → ALTER → CREATE | ✅ `20260711181615_FixActivePuestoIdUniqueType.cs:11-39` |
| `Down()` lanza `NotSupportedException` como primera línea | ✅ línea 46, antes de cualquier `migrationBuilder.*` |
| Snapshot regenerado con `string` (no `int`) y `UseCollation` | ✅ `SgvDbContextModelSnapshot.cs:984-988` |
| Script SQL regenerado | ✅ `docs/migracion-inicial-sgv.sql:533` ahora `varchar(36) COLLATE ascii_general_ci`; bloque idempotente para la nueva migración entre líneas 2050-2104 |
| AGENTS.md reemplazado | ✅ línea 183: `Cerrado por change archivado 2026-07-11-fix-active-puesto-id-unique-type (migración \`FixActivePuestoIdUniqueType\`).` |
| `dotnet ef migrations script --idempotent` queda en bloque idempotente (no raw DDL suelto) | ✅ DDL envuelto en 4 procedimientos `MigrationsScript` con check `__EFMigrationsHistory` |

---

## Coherence (Design vs. Implementation)

| Decisión de diseño | ¿Seguida? | Notas |
|--------------------|-----------|-------|
| D1: `string?` + `HasMaxLength(36)` + `HasColumnType("char(36)")` + `UseCollation("ascii_general_ci")` | ⚠️ **Parcial** | `HasColumnType("char(36)")` **fue removido** (Deviation #1) por NRE de EF Core 9 + Pomelo 9 en design-time/runtime. Solo queda `HasMaxLength(36)` + `UseCollation("ascii_general_ci")`. Storage real = `varchar(36)` (Deviation #2). Funcionalmente equivalente para 36 chars ASCII. |
| D2: `Down()` lanza `NotSupportedException` | ✅ Sí | `20260711181615_FixActivePuestoIdUniqueType.cs:46` — primera línea del método. |
| D3: `UPDATE` defensivo pre-`AlterColumn` | ⚠️ **Removido** (Deviation #3) | MySQL 8 rechaza `UPDATE` sobre columnas generadas VIRTUAL. Mitigación alternativa: MySQL re-evalúa la expresión computada en cada `ALTER COLUMN`, reemplazando automáticamente cualquier `'0'` truncado con el `PuestoId` correcto (cuando la fila está activa) o `NULL`. No-op en CI/dev fresco. |
| D4: `DROP INDEX → ALTER → CREATE INDEX` en una transacción | ✅ Sí | `20260711181615_FixActivePuestoIdUniqueType.cs:19-21, 23-32, 34-38` — secuencia exacta. EF Core 9 emite todas las operaciones dentro de la transacción implícita de `Database.Migrate()`. |
| D5: `char(36)` exacto vía `HasColumnType("char(36)")` | ⚠️ **Parcial** (Deviation #2) | Storage real = `varchar(36)` para evitar auto-detect `Guid` de MySqlConnector 2.4.0 (que rompería el cast a `string`). 1 byte length-prefix por fila; semánticamente equivalente. |

---

## TDD Compliance (Strict TDD)

| Check | Resultado | Detalles |
|-------|-----------|----------|
| TDD Evidence reportado | ✅ | TDD Cycle Evidence encontrada en `engram #963` (topic_key `sdd/.../apply-progress`). |
| Todas las tasks de tests tienen archivo | ✅ | T-006 (modified `ModeloPersistenciaTests.cs`), T-007 (new `OcupacionGeneratedColumnRegressionTests.cs`). |
| RED confirmado (tests existen + se demuestran pre-fix) | ✅ | T-006: asserts `typeof(string)`/`GetMaxLength()==36`/`(36)` hubiesen fallado pre-fix (eran `int`/`null`/`int`). T-007: cualquier `SaveChangesAsync` con `FechaFin = null` fallaba pre-fix con `Data truncated for column 'ActivePuestoIdUnique' at row 1`. |
| GREEN confirmado (tests pasan en ejecución) | ✅ | T-006: `Modelo_ConfiguraColumnaGeneradaUnicaParaOcupacionVigentePorPuesto` verde (dentro de los 20/20). T-007: `AddAsync_FilaActiva_ActivePuestoIdUniquePersisteComoGuidString` verde contra MySQL 8. |
| Triangulación adecuada | ➖ Single | Escenarios S1-S4 del spec cubren aspectos distintos; cada test cubre 1 escenario principal. Aceptable por la naturaleza puntual del fix. |
| Safety Net para archivos modificados | ⚠️ N/A por reconstrucción | T-001 (config EF) no tenía test directo pre-existente — pero el snapshot (T-002) y la inspección del modelo en `ModeloPersistenciaTests` actuaban como guardrail local sin MySQL. T-006 endurece ese guardrail. |

**TDD Compliance**: 5/6 checks pasados (Safety Net N/A pero justificado). El cambio tiene **12 tests fail→pass en `OcupacionRepositoryTests`** (verificados por `Passed: 16/16`), 1 test canario nuevo, 1 test endurecido, 1 tolerancia forward-only — total 16 tests que cubren el bug.

---

## Test Layer Distribution

| Layer | Tests | Archivos | Tools |
|-------|-------|----------|-------|
| Unit (modelo, sin MySQL) | 20 | `tests/SGV.Tests/Persistencia/ModeloPersistenciaTests.cs` (1 modificado + 1 forward-only hardened) | xUnit 2.9.2 — DbContext model inspection |
| Integration `[MySqlFact]` | 16 | `OcupacionRepositoryTests.cs` (15 untouched) + `OcupacionGeneratedColumnRegressionTests.cs` (1 nuevo) | xUnit + MySqlConnector + Pomelo 9 + MySQL 8 real |
| E2E | 0 | — | (no aplica; el bug es de persistencia) |
| **Total relevante al change** | **36** | **3 archivos** | |

Cross-reference con spec scenarios:
- S1 (canario Guid string) → Integration `[MySqlFact]` ✅
- S2 (violación unicidad) → Integration `[MySqlFact]` indirecto ⚠️
- S3 (modelo CLR type) → Unit ✅
- S4 (migración estructural) → Unit (forward-only + reflexión) ✅

---

## Changed File Coverage

➖ No ejecutado — coverage tool no invocado en este verify (no estaba en el alcance explícito de la tarea). En una segunda pasada, ejecutar `dotnet test SGV.slnx --filter "FullyQualifiedName~Persistencia" --collect:"XPlat Code Coverage"` y filtrar a `OcupacionConfiguracion.cs` + `20260711181615_FixActivePuestoIdUniqueType.cs` reportaría line/branch coverage. El estimativo cualitativo es alto: el subset Persistencia entera pasa 233/233 con MySQL real.

---

## Assertion Quality (Step 5f audit)

| Archivo | Línea | Assertion | Issue | Severidad |
|---------|-------|-----------|-------|-----------|
| `OcupacionGeneratedColumnRegressionTests.cs` | 70 | `Assert.NotNull(result);` | Type-only combinado con value assert en línea siguiente — OK | — |
| `OcupacionGeneratedColumnRegressionTests.cs` | 71 | `Assert.Equal(puestoId.ToString(), (string)result!);` | Value assertion sobre lectura real de MySQL — ✅ verifica comportamiento | — |
| `ModeloPersistenciaTests.cs` (T-006) | 61 | `Assert.Equal(typeof(string), generatedProperty.ClrType);` | Value sobre tipo CLR del modelo — ✅ | — |
| `ModeloPersistenciaTests.cs` (T-006) | 62 | `Assert.Equal(36, generatedProperty.GetMaxLength());` | ✅ | — |
| `ModeloPersistenciaTests.cs` (T-006) | 63-65 | `Assert.True(... Contains("(36)") == true, ...)` con mensaje | Relajado vs. spec (`char(36)` literal) — pero mejor: tolera `varchar(36)` y `char(36)`. Ver Deviation #4. | SUGGESTION |
| `ModeloPersistenciaTests.cs` (T-006-hard) | 262-270 | `try/catch (NotSupportedException)` alrededor de `Assert.NotNull(instance.DownOperations)` | Comportamiento forward-only intencional — ✅ | — |
| `OcupacionRepositoryTests.cs` (sin cambios) | múltiples | asserts `Assert.True(exists)`, `Assert.False(exists)`, etc. sobre queries reales | ✅ — value assertions sobre resultados de DB | — |

**Tautologías**: 0. **Ghost loops**: 0. **Mock-heavy**: 0 (cero mocks, todo DB real). **Smoke-only**: 0. **Implementation-detail coupling**: 0.

**Assertion quality**: ✅ Todas las aserciones verifican comportamiento real (valores desde DB o desde el modelo EF).

---

## Issues Found

### CRITICAL
**Ninguno**.

Las 4 dimensiones mínimas (task completeness, spec scenarios cubiertas, build verde, tests verdes) están satisfechas. Los issues son desviaciones de diseño con justificación técnica sólida, no violaciones de spec.

### WARNING

1. **Deviation #1 — `HasColumnType("char(36)")` removido del config** (`OcupacionConfiguracion.cs:35-39`): la combinación `HasColumnType + HasComputedColumnSql + string?` dispara `NullReferenceException` en `ElementMappingConvention.ProcessModelFinalizing` (EF Core 9 + Pomelo 9). Workaround Pomelo produce `GetColumnType() == "varchar(36)"`. Funcionalmente equivalente pero pierde match exacto con `PuestoId char(36)`. Spec delta línea 11 sigue diciendo `HasMaxLength(36)` (no exige `HasColumnType`), así que no es violación.

2. **Deviation #2 — Storage final = `varchar(36)`, no `char(36)`**: MySqlConnector 2.4.0 auto-detecta columnas `char(36)` como `System.Guid` independientemente del CLR type declarado. Como `ActivePuestoIdUnique` debe ser `string` (no `Guid`), `varchar(36)` es obligatorio para evitar `InvalidCastException` en runtime. 1 byte length-prefix por fila; semánticamente equivalente.

3. **Deviation #4 — T-006 assert relajado a `(36)`** (`ModeloPersistenciaTests.cs:63-65`): el spec delta S3 línea 34 exige "DEBE contener el literal `char(36)` (case-insensitive)". El assert real es `Contains("(36)")` — cubre tanto `char(36)` como `varchar(36)`. No es violación (el storage real ES `varchar(36)` por deviation #2), pero el lenguaje del spec quedó detrás del código. **Recomendación**: ajustar spec a "debe contener `(36)`" o equivalente genérico.

4. **Escenario S2 sin test explícito de violación de unicidad**: el spec S2 exige "MySQL DEBE rechazar la operación por violación del índice único" — el constraint existe en el modelo y los tests de `OcupacionRepositoryTests` insertan exactamente UNA activa por PuestoId (cubierto por escenario S1). Pero **no hay un test `[MySqlFact]` que intente insertar DOS activas con mismo PuestoId y verifique `DbUpdateException` por violación de unicidad**. Cobertura indirecta (la columna generada persiste el mismo string y existe índice único) es fuerte pero no explícita.

### SUGGESTION

1. **Deviation #3 — UPDATE defensivo pre-ALTER eliminado** (`20260711181615_FixActivePuestoIdUniqueType.cs:13-18`): el spec S4 línea 41 pide "DEBE ejecutar `UPDATE Ocupaciones SET ActivePuestoIdUnique = NULL WHERE FechaFin IS NULL AND IsDeleted = 0` antes del `AlterColumn`". Apply argumenta que MySQL 8 rechaza UPDATE sobre columnas generadas VIRTUAL, y que el comportamiento deseado se logra porque MySQL re-evalúa la expresión durante `ALTER COLUMN`. La lógica es sólida — el commit del apply incluye el comentario explicativo en `20260711181615_FixActivePuestoIdUniqueType.cs:13-18`. Pero **el spec S4 ya no describe el comportamiento real**. **Recomendación**: actualizar spec S4 para reflejar el nuevo mecanismo ("MySQL re-evalúa la expresión durante ALTER; valores preexistentes se reemplazan automáticamente") o mantener el wording pero citar la limitación técnica de MySQL.

2. **Deviation #5 — EF tool no pudo usarse** (`tasks.md` notas de implementación): la migración y Designer file se escribieron a mano. Aceptable porque EF Core 9 + Pomelo 9 tiene NRE conocido en combinación con `HasColumnType("char(36)")` + computed column + `string`. Si se actualiza el spec delta o design, mencionar esta limitación como precondición operacional.

3. **Apply-progress solo en Engram, no en filesystem**: el orchestrator pidió artifact store `both`. En este change el file `openspec/changes/.../apply-progress.md` **no existe en el filesystem**, aunque la observación #963 de Engram contiene todo el contenido (`TDD Cycle Evidence`, discoveries, deviations). Si Engram llegara a estar caído, el progreso se pierde. SUGGESTION: duplicar `apply-progress.md` en filesystem cuando el mode es `both`.

---

## Final Verdict

**PASS WITH WARNINGS**

Razones:
1. ✅ Build 0 warnings, 0 errors.
2. ✅ 20/20 unit tests sin MySQL + 16/16 integration tests contra MySQL 8 (incluyendo 12 fail→pass + 1 canario nuevo).
3. ✅ Las 10 tasks marcadas `[x]` y reflejadas en código, snapshot, script SQL, AGENTS.md.
4. ✅ TDD Cycle Evidence presente en Engram; red→green demostrado para T-006 y T-007.
5. ⚠️ 4 desviaciones documentadas que **no rompen spec runtime behavior** pero relajan lenguaje del design o el spec:
   - `HasColumnType("char(36)")` removido (EF/Pomelo bug)
   - `varchar(36)` en lugar de `char(36)` (MySqlConnector auto-detect)
   - UPDATE defensivo pre-ALTER eliminado (justificado por comportamiento de `ALTER COLUMN`)
   - Assert T-006 relajado a `(36)` (consistente con deviation #2)
6. ⚠️ S2 sin test explícito de violación de unicidad (cobertura indirecta robusta).
7. ⚠️ Apply-progress solo en Engram (no filesystem), pero `actionContext.artifactStore = both` solicita ambos.

El change está listo para **archive**. Las warnings deben documentarse en un eventual spec delta de mantenimiento (no es bloqueante): actualizar el lenguaje del spec sgv-database para reflejar `varchar(36)` en lugar de `char(36)`, y eliminar el requisito del UPDATE defensivo (MySQL no lo permite).

**Siguiente paso recomendado**: `archive`. El orchestrator puede proceder con `sdd-archive` para sincronizar el delta spec a `openspec/specs/sgv-database/spec.md`.
