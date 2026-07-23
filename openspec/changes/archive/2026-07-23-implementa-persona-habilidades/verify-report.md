```yaml
schema: gentle-ai.verify-result/v1
evidence_revision: sha256:982462ce0437486ac784f3813fc9553daed521aa8dfd2fd07bd588c112d2e113
verdict: pass
blockers: 0
critical_findings: 0
requirements: 4/4
scenarios: 6/6
test_command: dotnet test SGV.slnx --no-build
test_exit_code: 0
test_output_hash: sha256:f7f6856dc302a11829a2a7647308cc8c3046cce3c22e2e7f2529daab7209f449
build_command: dotnet build SGV.slnx
build_exit_code: 0
build_output_hash: sha256:982462ce0437486ac784f3813fc9553daed521aa8dfd2fd07bd588c112d2e113
```

## Verification Report

**Change**: `implementa-persona-habilidades` (Slice 1 / 4 — stacked-to-main)
**Version**: proposal/design/tasks/apply-progress v1 + 3 spec deltas (v1)
**Mode**: Strict TDD

### Completeness

| Metric | Value |
|--------|-------|
| Tasks total (Slice 1) | 7 |
| Tasks complete | 7 |
| Tasks incomplete | 0 |

> Nota: el archivo `tasks.md` no usa convención literal `[x]`/`[ ]`; documenta el cierre mediante la referencia al commit (`d34b0d0`, `ce485d4`) y al resultado del comando `Verify`. Cada tarea 1.1–1.7 tiene su commit, sus archivos creados/modificados y su comando de verificación con el conteo esperado. Esto es suficiente como evidencia de cierre pero difiere del formato `[x]` que esperan algunos orquestadores — ver SUGGESTION-1.

### Build & Tests Execution

**Build**: ✅ Passed
- `dotnet build SGV.slnx` → **0 errors / 84 warnings** (warnings preexistentes: `xUnit1031`, `EF1002`, `xUnit2029`, `xUnit1026`; ninguno introducido por Slice 1).
- `dotnet --version` → `10.0.300` (alineado con `global.json`).

**Tests**: ✅ 2,705 passed / 0 failed / 0 skipped
- `dotnet test SGV.slnx --no-build` → `Passed!  - Failed: 0, Passed: 2705, Skipped: 0, Total: 2705, Duration: 1 m 14 s`.
- **Filtro `PersonaSkill`**: `Passed!  - Failed: 0, Passed: 48, Skipped: 0, Total: 48, Duration: 475 ms` — coincide con claim de `apply-progress.md`.
- **Filtro `Contracts.Personas`**: `Passed!  - Failed: 0, Passed: 9, Skipped: 0, Total: 9` — coincide con claim de tarea 1.1.
- **Filtro `Compatibilidad`**: `Passed!  - Failed: 0, Passed: 3, Skipped: 0, Total: 3` — incluye wire shape.
- **Filtro `Api`**: `Passed!  - Failed: 0, Passed: 839, Skipped: 0, Total: 9 s`.
- **Filtro `Web`**: `Passed!  - Failed: 0, Passed: 972, Skipped: 0, Total: 1 m 6 s`.
- **Filtro `PersonaSkillRepository`** (MySqlFact, requiere MySQL): `Passed!  - Failed: 0, Passed: 9, Skipped: 0, Total: 9` — DB disponible en este runner.

**Coverage**: ➖ No se ejecutó `--collect:"XPlat Code Coverage"` por no estar en el scope del Slice 1; la cobertura histórica de los archivos migrados estaba en `coverage.cobertura.xml` (stale, del baseline pre-Slice 1). Slice 1 no introduce lógica de negocio nueva, solo movimiento de tipos + mappers, así que la métrica de cobertura no es bloqueante para el cierre.

### Spec Compliance Matrix — `commandresult-error-taxonomy` (única spec in-scope para Slice 1)

| Requirement | Scenario | Test cubriente | Resultado |
|-------------|----------|----------------|-----------|
| REQ-TAXO-01 — `PersonaSkillCommandResult`/`PersonaSkillError` viven en `SGV.Contracts.Personas` con `Categoria` | Scenario: Build compila contra `SGV.Contracts.Personas` | `tests/SGV.Tests/Contracts/Personas/PersonaSkillContractsCompatibilityTests.cs` (9 tests, ej. `Contracts_ExposesPersonaSkillCommandResult`, `Contracts_PersonaSkillError_ExposesCategoriaPropertyOfTypeErrorCategoria`) | ✅ COMPLIANT |
| REQ-TAXO-01 — Wire JSON preservado | Scenario: Wire JSON preservado | `tests/SGV.Tests/Web/Persona/PersonaSkillJsonCompatibilityTests.cs` (5 tests: serialización `skillId`/`nivelId` en `PersonaSkillDto`, nested `skill`/`nivel` en `PersonaSkillDetailDto`, `nivelId` en `AsignarPersonaSkillRequest`; deserialización de los dos primeros) | ✅ COMPLIANT |
| REQ-TAXO-02 — Mapeo `PersonaSkillErrorType → ErrorCategoria` consolidado | Scenario: `NotFound` → `ErrorCategoria.NotFound` | `PersonaSkillErrorCategoriaMappingTests.PersonaSkillError_NotFound_MapsToErrorCategoriaNotFound_404` | ✅ COMPLIANT |
| REQ-TAXO-02 — Mapeo `PersonaSkillErrorType → ErrorCategoria` consolidado | Scenario: `Validation` → `ErrorCategoria.Validation` | `PersonaSkillErrorCategoriaMappingTests.PersonaSkillError_Validation_MapsToErrorCategoriaValidation_400` | ✅ COMPLIANT |
| REQ-TAXO-02 — Mapeo `PersonaSkillErrorType → ErrorCategoria` consolidado | Scenario: `PersonaSkillDeleteResult` expone `Categoria` | `PersonaSkillErrorCategoriaMappingTests.PersonaSkillDeleteResult_ConstructionWithCategoria_ExposesCategoriaAndStatusCode` + `_ConstructionWithoutCategoria_DefaultsToNotFound` | ✅ COMPLIANT |
| REQ-TAXO-03 — `PersonaSkill` no reintroduce un enum paralelo | Scenario: Cliente usa el mapper común | `PersonaSkillContractsCompatibilityTests.Contracts_PersonaSkillErrorTypeEnum_StillExists` (enum interno, NO público) + `ErrorCategoriaMappers.ToCategoria(PersonaSkillErrorType)` / `ToTipoPersonaSkill(ErrorCategoria)` + `ApiResults.MapPersonaSkillStatus(PersonaSkillErrorType)` delega a `ErrorCategoriaMappers` (sin switch privado duplicado) | ✅ COMPLIANT |

**Compliance summary**: 6/6 escenarios `commandresult-error-taxonomy` compliant.

> Las specs `persona-skill-web-management` (8 escenarios) y `persona-management` (3 escenarios) están **explícitamente fuera de scope de Slice 1**. Slice 2/3a/3b las cubren. No se evalúan como fallidas acá y no hay evidencia de regresión que las bloquee: `PersonasController` sigue admin-only, el endpoint `/api/v1/personas/{personaId}/skills/{skillId}` mantiene sus semánticas HTTP y `PersonaSkillDeleteResult` queda con `Categoria`/`StatusCode` listo para que `DeleteResultMapper` lo consuma sin shim paralelo (ver propuesta §Próximos pasos en `apply-progress.md`).

### Correctness (Static Evidence)

| Requirement | Status | Notas |
|-------------|--------|-------|
| Wire-types `PersonaSkill*` en `SGV.Contracts.Personas` (REQ-TAXO-01) | ✅ Implementado | 5 archivos nuevos en `src/SGV.Contracts/Personas/{Comandos,Consultas/Dtos}/` (`PersonaSkillCommandResult.cs`, `PersonaSkillRequests.cs`, `PersonaSkillDeleteResult.cs`, `PersonaSkillDto.cs`, `PersonaSkillDetailDto.cs`). `git show --stat HEAD~1` confirma la creación atómica. |
| Sin duplicación en `SGV.Aplicacion` (REQ-TAXO-01) | ✅ Implementado | `find src/SGV.Aplicacion/Personas -name "PersonaSkill*"` retorna **solo** `PersonaSkillServicio.cs` (servicio, no DTO). `git log --diff-filter=D --name-only` confirma los 4 archivos borrados en el mismo commit `ce485d4` (`PersonaSkillCommandResult.cs`, `PersonaSkillRequests.cs`, `Consultas/Dtos/PersonaSkillDto.cs`, `Consultas/Dtos/PersonaSkillDetailDto.cs` con rename detectado a Contracts). El directorio `src/SGV.Aplicacion/Personas/Consultas/Dtos/` ya no existe. |
| `PersonaSkillError.Categoria: ErrorCategoria` + `StatusCode: int?` (REQ-TAXO-02) | ✅ Implementado | `src/SGV.Contracts/Personas/Comandos/PersonaSkillCommandResult.cs:30-35` — record con `Categoria: ErrorCategoria = ErrorCategoria.Unexpected` (default back-compat) y `StatusCode: int? = null`. |
| `PersonaSkillDeleteResult` con `Categoria` + `StatusCode` (REQ-TAXO-02) | ✅ Implementado | `src/SGV.Contracts/Personas/Comandos/PersonaSkillDeleteResult.cs:21-26` — record con `Categoria: ErrorCategoria = ErrorCategoria.NotFound` (default conservador, mismo shape que `CargoSkillDeleteResult`). |
| Mappers consolidados (REQ-TAXO-02 / REQ-TAXO-03) | ✅ Implementado | `src/SGV.Contracts/Comun/ErrorCategoriaMappers.cs:240-261` — `ToCategoria(PersonaSkillErrorType)` y `ToTipoPersonaSkill(ErrorCategoria)` con `_ => throw new ArgumentOutOfRangeException` que rechaza variantes no documentadas (anti-reintroducción de enum paralelo). |
| `ApiResults` consume Contracts + mappers comunes (REQ-TAXO-02) | ✅ Implementado | `src/SGV.Api/Infrastructure/Results/ApiResults.cs:11` ahora tiene `using SGV.Contracts.Personas.Comandos;`. `MapPersonaSkillStatus(PersonaSkillError)` (líneas 310-313) respeta `Categoria`/`StatusCode` explícitos y cae al switch legacy solo cuando vienen vacíos; `MapPersonaSkillStatus(PersonaSkillErrorType)` (líneas 315-316) delega en `ErrorCategoriaMappers.ToCategoria`. Sin switch privado paralelo. |
| `using` actualizados en Aplicación/Infraestructura/Api/Tests (REQ-TAXO-01) | ✅ Implementado | `grep` confirma `SGV.Contracts.Personas.*` en `PersonaSkillServicio.cs`, `IPersonaSkillServicio.cs`, `IPersonaSkillRepository.cs`, `PersonaSkillRepository.cs`, `PersonasController.cs`, `ApiResults.cs` y los 3 archivos de tests afectados. |

### Coherence (Design)

| Decision (de `design.md`) | ¿Seguida? | Notas |
|---------------------------|-----------|-------|
| Migración atómica sin período de coexistencia | ✅ Sí | Mismo commit `ce485d4` crea en Contracts y borra de Aplicación; `find` y `git log --diff-filter=D` lo confirman. |
| Preservar wire JSON (`skillId`/`nivelId` write, `skill`/`nivel` nested read) | ✅ Sí | `PersonaSkillJsonCompatibilityTests` valida 5 escenarios (serialización + deserialización de los tres DTOs). `assert.False(... "skillId"/"nivelId" ...)` en el read-contract test evita el drift inverso. |
| HTTP preservado (404 NotFound, 400 Validation) | ✅ Sí | `MapPersonaSkillStatus(PersonaSkillError)` mantiene el contrato observable. `PersonaSkillControllerTests` y `PersonasControllerTests` siguen verdes (parte de los 839 tests `Api`). |
| Eliminar `PersonaSkillErrorType` como discriminador público | ✅ Sí | Sigue como enum interno en `SGV.Contracts.Personas.Comandos` (cerrado, 2 variantes) pero el cliente web NO debe ramificar por él — usa `PersonaSkillError.Categoria`/`PersonaSkillDeleteResult.Categoria`. `CommandResultMapper`/`DeleteResultMapper` quedan libres de matriz duplicada para Slices siguientes (cumple REQ-TAXO-03). |
| `PersonaSkillDeleteResult` shape espejo `CargoSkillDeleteResult` | ✅ Sí | Mismo orden de parámetros (`Succeeded`, `StatusCode`, `Code`, `Message`, `Categoria`); defaults alineados. |
| Acceso admin-only y bloqueo persona inactiva | ✅ Sin cambios en este slice | `PersonasController` ya vigente; sin regresión ni aflojamiento. Validado por la no-modificación de `PersonasController` salvo el `using`. |

### TDD Compliance (Strict TDD Module)

| Check | Resultado | Detalle |
|-------|-----------|---------|
| Evidencia TDD reportada en `apply-progress.md` | ✅ | Tabla "TDD Cycle Evidence" presente con 3 filas (tareas 1.1/1.2/1.3). |
| Todas las tareas RED tienen archivo de test | ✅ | `tests/SGV.Tests/Contracts/Personas/PersonaSkillContractsCompatibilityTests.cs`, `tests/SGV.Tests/Api/PersonaSkillErrorCategoriaMappingTests.cs`, `tests/SGV.Tests/Web/Persona/PersonaSkillJsonCompatibilityTests.cs` — los 3 existen en disco. |
| RED confirmado (test files existen) | ✅ | Verificado por `ls` directo a los 3 paths. |
| GREEN confirmado (tests pasan en ejecución) | ✅ | Re-corrida local del filtro `PersonaSkill` reporta 48/0/0. Re-corrida local de `Contracts.Personas` reporta 9/0/0. Re-corrida local de `PersonaSkillRepository` (MySqlFact) reporta 9/0/0 (MySQL disponible). |
| Triangulación adecuada | ✅ | Tarea 1.1 cubre 6 tipos (CommandResult, Error, DeleteResult, Requests, Dto, DetailDto) + 3 propiedades (Categoria, StatusCode, enum). Tarea 1.2 cubre NotFound, Validation, with-Categoria, without-Categoria, DeleteResult with/without Categoria. Tarea 1.3 cubre write serialización ×3, deserialización ×2. Más que suficiente para un movimiento de tipos. |
| Safety Net para archivos modificados | ✅ N/A | Los 3 archivos de tests son NUEVOS. Los tests pre-existentes modificados (10 archivos) son `using`-only — sin cambio de semántica. La suite completa (2,705 tests) los cubre como safety net. |

**TDD Compliance**: 6/6 checks passed.

### Test Layer Distribution

| Layer | Tests | Archivos | Tool |
|-------|-------|----------|------|
| Unit | 20 (nuevos) + 28 (PersonaSkill preexistentes, sin contar MySqlFact) | 3 nuevos + varios modificados | xUnit v3 (`xunit` 2.9.2) |
| Integration | 9 ([MySqlFact] en `PersonaSkillRepositoryTests`) | 1 | xUnit + `TestSgvDbContextFactory` (MySQL) |
| E2E | 0 | — | No aplica en Slice 1 (sin endpoints nuevos) |
| **Total PersonaSkill** | **48** (no cuenta MySQL persistence) + 9 MySQL | **8 archivos** | |

### Assertion Quality Audit (Strict TDD Step 5f)

| Archivo | Línea | Patrón | Issue | Severidad |
|---------|-------|--------|-------|-----------|
| `PersonaSkillContractsCompatibilityTests.cs` | 26-122 | Múltiples `Assert.Equal(name, type.Name)` + `Assert.NotNull(type)` | Son guards de contrato tipados (reference + nombre). Verifican referencia en compile-time y forma del tipo. No son tautologías porque un rename/move los rompe. | ✅ OK (guard-type) |
| `PersonaSkillErrorCategoriaMappingTests.cs` | 24-110 | `Assert.Equal(ErrorCategoria.X, ...)` + `Assert.Equal(404/400, ...)` | Cubren valor real y side-effects observables (HTTP). Triangulan default/explícito. | ✅ OK |
| `PersonaSkillJsonCompatibilityTests.cs` | 30-118 | `Assert.True(root.TryGetProperty(...))` + `Assert.Equal(guid, prop.GetGuid())` + `Assert.False(... "skillId"/"nivelId" ...)` | Anti-drift regression: presencia + valor + ausencia de propiedades prohibidas. Verifican comportamiento observable. | ✅ OK |

**Assertion quality**: ✅ Todas las assertions verifican comportamiento real (contratos de tipo, mapping de taxonomía, shape JSON observable). No se encontraron tautologías, ghost loops, mocks huérfanos ni smoke tests. Tests de DTOs: NO se agregaron tests específicos para constructores/serialización trivial — los 5 tests JSON validan **anti-drift** del wire (regression guards), no "el record serializa" per se, lo cual cumple la filosofía del repo (calidad > cantidad).

### Issues Found

**CRITICAL**: None.

**WARNING**: None.

**SUGGESTION**:

1. **SUGGESTION-1** — `tasks.md` no usa checkboxes literales `[x]`/`[ ]`; documenta el cierre de tareas 1.1–1.7 mediante referencia al commit + resultado del comando Verify. Funcionalmente equivalente, pero si el orquestador o el dashboard de OpenSpec esperan parseo de checkboxes, podría ser conveniente alinearlo (ej. `- [x]` antes del encabezado de cada tarea). Severidad sugerida por no-bloqueo de funcionalidad y porque la evidencia de cierre es inequívoca.

2. **SUGGESTION-2** — El helper privado `MapCategoriaToHttp` en `PersonaSkillErrorCategoriaMappingTests.cs:115-125` duplica el switch `MapCategoria` de `ApiResults.cs:252-262`. Es deliberado (el test evita arrastrar `SGV.Api` a `xUnit`), pero el comentario debería referenciar el path canónico (`ApiResults.MapCategoria`) para que un cambio ahí recuerde sincronizar el test. Severidad: cosmético.

3. **SUGGESTION-3** — Considerar en `tasks.md` (sección "Validación de forecast por slice") una fila explícita para Slice 1 que confirme que la corrida local del orquestador reportó 48 PASS (vs. 21 esperados en diseño) — la métrica final termina 27 tests por encima del forecast mínimo, lo cual vale documentarlo para auditorías futuras.

### Verdict

**PASS** — Slice 1 cumple todos los criterios de aceptación de `commandresult-error-taxonomy`, ejecuta el strict TDD correctamente (RED → GREEN → atomicidad verificada) y deja el repo en estado listo para que Slice 2 extienda `IPersonaApiClient` sin tocar `SGV.Aplicacion` ni duplicar DTOs.

---

### Artifacts

- `openspec/changes/implementa-persona-habilidades/verify-report.md` (este archivo)
- Engram observation `sdd/implementa-persona-habilidades/verify-report` (a persistir tras esta ejecución)

### Próximo paso del orquestador

`archive` no aplica todavía (Slice 1 es solo 1/4 del change). Tras merge de este PR a `main`, lanzar Slice 2 (`IPersonaApiClient.GetSkillsAsync/UpsertSkillAsync/DeleteSkillAsync` + fakes + tests, 195-290 líneas estimadas). El Slice 1 deja el terreno limpio para que Slice 2 NO toque `SGV.Aplicacion` ni duplique DTOs.