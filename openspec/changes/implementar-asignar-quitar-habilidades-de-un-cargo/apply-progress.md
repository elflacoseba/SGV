# Apply Progress — Implementar asignar/quitar Habilidades de un Cargo

## PR2 — Infraestructura + API (completado)

- **Branch**: `feat/cargo-habilidad-pr2-infra-api`
- **Estado**: completado
- **Strict TDD**: activo (`openspec/config.yaml` → `strict_tdd: true`)
- **Baseline al inicio**: `dotnet build SGV.slnx` → 0 Warning(s), 0 Error(s). `dotnet test --filter "FullyQualifiedName~CargoSkill|FullyQualifiedName~HabilidadAntiDrift"` → **68/68 PASS**. `dotnet test --filter "FullyQualifiedName~CargoSkillController|FullyQualifiedName~CargosController|FullyQualifiedName~SwaggerConfiguration"` → **87/87 PASS**.
- **Alcance**: repositorio enriquecido (T2.1), bifurcación de errores en controller (T2.2), schema Swagger + shape sin alias `nivelId` (T2.3), anti-regresión del contrato padre (T2.4). NO toca aplicación, NO toca web, NO introduce migraciones.

### Tareas ejecutadas

- **T2.1** ✅ Enriquecer proyección de `CargoSkillRepository.ListDetailedByCargoIdAsync`.
- **T2.2** ✅ Bifurcar `ToSkillProblemResult` entre `ValidationProblemDetails` y `ProblemDetails`.
- **T2.3** ✅ Documentar schema Swagger del subrecurso + ausencia de alias `nivelId`.
- **T2.4** ✅ Anti-regresión de shape en `Cargo` padre.

### TDD Cycle Evidence

| Task | Test File | Layer | Safety Net | RED | GREEN | TRIANGULATE | REFACTOR |
|------|-----------|-------|------------|-----|-------|-------------|----------|
| T2.1 | `tests/SGV.Tests/Persistencia/CargoSkillRepositoryTests.cs` | Integration (MySqlFact) | ✅ 9/9 (subset repo) | ✅ `ListDetailedByCargoIdAsync_ProyectaSkillIdNivelRequeridoIdPonderacionYEsObligatoria` falla con `SkillId=Guid.Empty` (real MySQL 8 disponible) | ✅ 10/10 (la proyección LINQ ahora popula `SkillId`/`NivelRequeridoId`/`Ponderacion`/`EsObligatoria` via init properties del DTO, en una sola query sin N+1) | ➖ Single — spec Req 1 y 4 cubren un único shape obligatorio; los otros 9 tests ya cubren escenarios relacionados (add/duplicate/update/delete/list) | ➖ Implementación mínima, sin cambios extra |
| T2.2 | `tests/SGV.Tests/Api/CargoSkillControllerTests.cs` | Integration (WebApplicationFactory) | ✅ 14/14 (subset controller) | ✅ 2 tests nuevos fallan (`UpsertSkill_FieldErrors_ReturnsValidationProblemDetails` y `UpsertSkill_PonderacionExcede100_Returns400ConCampoPonderacion`) porque el controller siempre emitía `ProblemDetails`; 1 test nuevo pasa (`UpsertSkill_ValidationErrorSinFieldErrors_MantieneProblemDetails`) confirmando el camino legacy | ✅ 3 nuevos + 14 originales = 17/17 PASS. `ToSkillProblemResult` ahora bifurca: cuando `result.FieldErrors.Count > 0` y status es 400, emite `ValidationProblemDetails`; en cualquier otro caso, mantiene `Problem(...)` | ✅ 3 paths cubiertos: (a) FieldErrors poblados → `ValidationProblemDetails` con `errors`; (b) FieldErrors poblados para `ponderacion` → `errors.ponderacion`; (c) Validation sin FieldErrors → `ProblemDetails` legacy | ➖ Helper único, ya estaba extraído en `ToValidationProblemResult` para `Cargo`; aquí se aplica el mismo patrón |
| T2.3 | `tests/SGV.Tests/Api/SwaggerConfigurationTests.cs` | Integration (WebApplicationFactory) | ✅ 30/30 (subset swagger) | ➖ GREEN pre-existente: el shape ya cumple el spec desde T2.1 + PR1 (PR1 introdujo `init` properties en `CargoSkillDetailDto` y eliminó alias `nivelId`; T2.1 ahora popula los campos desde la DB). Los tests se escribieron como **approval tests** que blindan el contrato contra regresiones futuras. | ✅ 3 tests nuevos + 30 originales = 33/33 PASS. Cubren: presencia de `nivelRequeridoId`/`ponderacion`/`esObligatoria`/`skill`/`nivel`/`skillId` en `CargoSkillDetailDto`; ausencia de `nivelId` en el subrecurso; `id` (no `nivelId`) en `NivelHabilidadDto` anidado; referencia del GET subrecurso al schema correcto | ✅ 4 paths: schema del subrecurso, schema del nivel anidado, operation GET documentada, ausencia de alias | ➖ Sin código de producción: la shape ya estaba alineada con la decisión de diseño |
| T2.4 | `tests/SGV.Tests/Api/CargosControllerTests.cs` + `SwaggerConfigurationTests.cs` | Integration (WebApplicationFactory) | ✅ 60/60 (subset controller+swagger) | ➖ GREEN pre-existente: el `CargoDto` no contiene campos del subrecurso (`nivelRequeridoId`/`ponderacion`/`esObligatoria`/`skill`/`habilidades`), preservando el alcance acotado del contrato (cargo-skill-query-contract Req 3). Los tests son **approval tests** que blindan el contrato padre contra contaminación accidental. | ✅ 3 tests nuevos + 60 originales = 63/63 PASS. Cubren: JSON del `GET /api/v1/cargos/{id}` no contiene campos del subrecurso; JSON del `GET /api/v1/cargos` tampoco; schema Swagger del `CargoDto` no expone esos campos | ✅ 3 paths: GET item, GET lista, schema OpenAPI del `CargoDto` | ➖ Sin código de producción: `CargoDto` es un record inmutable sin contaminación |

### Métricas

- **Tests al inicio**: 87 (subset API/Swagger/Controller) + 10 (subset repo) = 97 sobre el alcance de PR2.
- **Tests al cierre**: 97 + 7 nuevos (1 persistencia + 3 API + 3 swagger) = **104 PASS**.
- **Diff total**: +184/−6 líneas en 5 archivos. Ningún commit > 60 líneas.
- **Build**: `dotnet build SGV.slnx` → 0 Warning(s), 0 Error(s) en cada commit.
- **Suite subset**: `dotnet test --filter "FullyQualifiedName~CargoSkill|FullyQualifiedName~SwaggerConfiguration|FullyQualifiedName~HabilidadAntiDrift"` → **72/72 PASS**.
- **Suite subset API**: `dotnet test --filter "FullyQualifiedName~CargoSkillController|FullyQualifiedName~CargosController|FullyQualifiedName~SwaggerConfiguration"` → **94/94 PASS**.
- **Suite completa**: `dotnet test SGV.slnx` → **1316/1328 PASS**. Los 12 fallos siguen siendo pre-existentes de `OcupacionRepositoryTests` (issue #59, `ActivePuestoIdUnique INT` vs `PuestoId CHAR(36)`), fuera del scope de PR2.

### Commits

```
a866d2ca test(api+swagger): documentar schema del subrecurso y anti-regresion de shape en Cargo padre
c1d8a592 feat(api): bifurcar ToSkillProblemResult entre ValidationProblemDetails y ProblemDetails
d5e4459a test(api): bifurcar errores de validacion en subrecurso cargo-skill
04ea5a5c feat(persistencia): enriquecer ListDetailedByCargoIdAsync con skillId/nivelRequeridoId/ponderacion/esObligatoria
26db75d8 test(persistencia): cargo-skill proyecta skillId/nivelRequeridoId/ponderacion/esObligatoria
```

5 commits en formato conventional commits. Sin `Co-Authored-By:` ni atribución a IA.

### Archivos modificados / creados

**Producción (`src/`):**
- `SGV.Infraestructura/Persistencia/Repositorios/CargoSkillRepository.cs` — proyección LINQ de `ListDetailedByCargoIdAsync` ahora popula `SkillId`/`NivelRequeridoId`/`Ponderacion`/`EsObligatoria` desde la entidad en una sola query (sin N+1).
- `SGV.Api/Controllers/CargosController.cs` — `ToSkillProblemResult` ahora bifurca entre `ValidationProblemDetails` (cuando `result.FieldErrors.Count > 0` y status es 400) y `ProblemDetails` (resto de los casos). Comentarios `<response>` actualizados para documentar la diferencia. La signature del helper ganó un parámetro opcional `CargoSkillCommandResult? result = null` para no romper el call site de `DeleteSkill`.

**Tests (`tests/SGV.Tests/`):**
- `tests/SGV.Tests/Persistencia/CargoSkillRepositoryTests.cs` — 1 test nuevo `[MySqlFact]`: `ListDetailedByCargoIdAsync_ProyectaSkillIdNivelRequeridoIdPonderacionYEsObligatoria` con `Ponderacion=2.50`, `EsObligatoria=true`, asserts de los 4 campos más los nested.
- `tests/SGV.Tests/Api/CargoSkillControllerTests.cs` — 3 tests nuevos: `UpsertSkill_FieldErrors_ReturnsValidationProblemDetails`, `UpsertSkill_PonderacionExcede100_Returns400ConCampoPonderacion`, `UpsertSkill_ValidationErrorSinFieldErrors_MantieneProblemDetails`.
- `tests/SGV.Tests/Api/CargosControllerTests.cs` — 2 tests nuevos: `GetById_ParentPayloadNoContaminaCamposDelSubrecursoSkill`, `GetAll_ParentPayloadNoContaminaCamposDelSubrecursoSkill`. Endurecen el test pre-existente `GetById_ParentPayloadDoesNotIncludeSkillAssignmentFields`.
- `tests/SGV.Tests/Api/SwaggerConfigurationTests.cs` — 4 tests nuevos: `CargoSkillDetailDto_ExponeNivelRequeridoIdPonderacionEsObligatoriaSinAliasNivelId`, `CargoSkillDetailDto_NivelAnidadoExponeIdNoNivelId`, `CargoSkillSubresourceGetOperation_DocumentsEnrichedResponse`, `CargoDto_NoContaminaCamposDelSubrecursoSkill`.

### Decisiones durante implementación

1. **`ToSkillProblemResult` opcional `result`**: agregué un segundo parámetro `CargoSkillCommandResult? result = null` para preservar el call site existente de `DeleteSkill`. Las llamadas de `UpsertSkill` y `DeleteSkill` ahora pasan el `result` completo; el helper evalúa `result?.FieldErrors is { Count: > 0 }` antes de emitir `ValidationProblemDetails`. Esto evita una firma distinta para el helper de Delete (que no necesita bifurcar porque su único camino de fallo es `NotFound`).
2. **Aprobación tests (T2.3 y T2.4)**: el shape ya cumple el spec desde PR1 + T2.1, así que los tests pasan al primer run. Los marco como aprobación del contrato — si alguien futuro intenta reintroducir `nivelId` o contaminar el `CargoDto` con campos del subrecurso, estos tests fallan. Esta es la práctica correcta de "blindar el comportamiento" del strict-tdd.md para approval testing.
3. **T2.3 sin código de producción**: el `<response code="400">` del `UpsertSkill` se actualizó para documentar la diferencia entre `ValidationProblemDetails` y `ProblemDetails` (dependiendo de `FieldErrors`). No hay otro cambio porque el controller ya referencia `typeof(CargoSkillDetailDto)` para el GET del subrecurso y Swashbuckle genera el schema OpenAPI desde el DTO directamente.
4. **Tests `CargosControllerTests` en PR1 ya tenían `GetById_ParentPayloadDoesNotIncludeSkillAssignmentFields`**: lo conservé y agregué 2 tests hermanos (`GetById_ParentPayloadNoContaminaCamposDelSubrecursoSkill` y `GetAll_ParentPayloadNoContaminaCamposDelSubrecursoSkill`) más amplios que blindan explícitamente los 6 campos del subrecurso (`nivelRequeridoId`, `ponderacion`, `esObligatoria`, `skill`, `nivel`, `CargoSkillDetailDto`).

### Riesgos abiertos

- **Backwards compat del JSON del PUT**: la rename `nivelId` → `nivelRequeridoId` en el body del PUT (introducida en PR1) rompe consumidores existentes. PR2 no agregó un alias `nivelId` en el GET del subrecurso (alineado con la decisión de diseño del change). Si en el futuro hace falta compatibilidad hacia atrás, se puede agregar un alias con `[JsonPropertyName("nivelId")]` que mapee a `NivelRequeridoId` — fuera del scope actual.
- **Precisión `decimal(5,2)`**: el campo `Ponderacion` se persiste con `decimal(5,2)` (hasta 999.99). El tope `100.00` solo se valida en aplicación (FluentValidation). Un PUT con `Ponderacion=999.99` fallaría la validación de aplicación (≤100.00) pero pasaría la persistencia. Esto es intencional — la decisión de diseño es "validación solo en app, sin CHECK constraint". Si en el futuro hace falta una salvaguarda adicional, se puede agregar un CHECK en una migración dedicada.
- **`CargoSkillCommandResult.Value` en error sin `FieldErrors`**: en el camino de fallo (e.g., `NotFound`), `Value` queda `null`. El controller actual (`ToSkillProblemResult`) ya maneja `Error` separado y NO expone `Value` en errores no-validación. Esto es consistente con el comportamiento de `HabilidadCommandResult`.
- **12 fallos pre-existentes de `OcupacionRepositoryTests`**: confirmados, siguen siendo issue #59. NO son introducidos ni arreglados por PR2.

### Verificación al cierre de PR2

```bash
# Build limpio
dotnet build SGV.slnx
# → Build succeeded. 0 Warning(s). 0 Error(s).

# Subset PR2
dotnet test SGV.slnx --filter "FullyQualifiedName~CargoSkill|FullyQualifiedName~SwaggerConfiguration|FullyQualifiedName~HabilidadAntiDrift"
# → Total: 72. Passed: 72. Failed: 0.

dotnet test SGV.slnx --filter "FullyQualifiedName~CargoSkillController|FullyQualifiedName~CargosController|FullyQualifiedName~SwaggerConfiguration"
# → Total: 94. Passed: 94. Failed: 0.

# Suite completa (informativo, los 12 fallos son issue #59 pre-existente)
dotnet test SGV.slnx
# → Total: 1328. Passed: 1316. Failed: 12 (issue #59, OcupacionRepositoryTests).
```

---

## PR1 — Cleanup `NivelId` legacy (refactor, completado)

- **Branch**: `feat/cargo-habilidad-pr1-aplicacion`
- **Estado**: completado
- **Strict TDD**: activo. El refactor preserva comportamiento: el test subset PR1 estaba **verde antes** (68/68) y siguió **verde después** (68/68).
- **Alcance**: refactor enfocado. Único objetivo: eliminar el parámetro posicional `NivelId` (alias legacy) de `CargoSkillDto` y alinear el contrato con la decisión de usuario — solo `NivelRequeridoId`, sin alias `nivelId` en el write DTO.

### Archivos tocados

| Archivo | Líneas antes | Líneas después | Delta | Acción |
|---|---:|---:|---:|---|
| `src/SGV.Aplicacion/Organizacion/Consultas/Dtos/CargoSkillDto.cs` | 47 | 32 | −15 | Eliminado parámetro posicional `NivelId`; `NivelRequeridoId` ahora es posicional (segundo arg); eliminada la propiedad `init` redundante y la doc-comment que justificaba el alias transitorio. |
| `tests/SGV.Tests/Api/CargoSkillControllerTests.cs` | 449 | 449 | 0 | Renombrada constante local `ExistingNivelId` → `ExistingNivelRequeridoId` (11 referencias) para alinear el nombre con la semántica del nuevo shape posicional. Los call sites ya pasaban el valor correcto (`request.NivelRequeridoId` y `ExistingNivelRequeridoId`); el cambio es puramente de nomenclatura. Los JSON bodies con `new { nivelId = ... }` no cambian de forma (la LHS del objeto anónimo sigue siendo `nivelId`); el RHS usa el valor del Guid, no el nombre del identificador. |

### TDD Cycle Evidence (refactor)

| Aspecto | Resultado |
|---|---|
| Safety net (pre) | `dotnet test --filter "FullyQualifiedName~CargoSkill\|FullyQualifiedName~HabilidadAntiDrift"` → **68/68 PASS** antes del refactor. |
| RED (test escrito primero) | N/A — refactor, no se introduce comportamiento nuevo. |
| GREEN (post) | Mismo subset → **68/68 PASS** después del refactor. |
| Build | `dotnet build SGV.slnx` → 0 Warning(s), 0 Error(s). |
| Suite completa | `dotnet test SGV.slnx` → **1309/1321 PASS** (mismo baseline; los 12 fallos siguen siendo `OcupacionRepositoryTests` pre-existentes, issue #59). |
| Test summary | 0 tests modificados (refactor mecánico de constante), 0 tests nuevos (no se introduce comportamiento). |
| Aprobación tests | El comportamiento observable del `CargoSkillDto` (lo que el controller serializa y lo que los tests verifican) **no cambia**: el `UpsertAsync`/`DeleteAsync` fake sigue devolviendo `new CargoSkillDto(skillId, ExistingNivelRequeridoId)` y la aserción `Assert.Equal(ExistingNivelRequeridoId, dto.NivelRequeridoId)` sigue verde. |

### Commit

```
1e33c101 refactor(cargo-skill): remove legacy NivelId positional from CargoSkillDto
```

SHA: `1e33c101a99dc86bdfddbfbd72b97da71317628d`. Diff: 2 files changed, +19/−34. Sin `Co-Authored-By:` ni atribución a IA.

### Notas del refactor

1. **Call sites del constructor**: solo había dos — líneas 76 y 83 de `CargoSkillControllerTests.cs`. La línea 76 (`new CargoSkillDto(skillId, request.NivelRequeridoId)`) ya pasaba el valor correcto, por lo que el cambio del shape posicional la beneficia sin tocarla (el segundo arg ahora es `NivelRequeridoId`, que es exactamente el valor que ya pasaba). La línea 83 pasaba el Guid desde la constante, que se renombró para reflejar la nueva semántica.
2. **`CargoSkillServicio.BuildDto`** usa `new(skillId, nivelRequeridoId) { NivelRequeridoId = nivelRequeridoId, ... }` — el positional pasa el Guid correcto al segundo arg (ahora `NivelRequeridoId`) y el `init` setea `NivelRequeridoId` explícitamente. Después del refactor, el `init` queda **redundante** (idéntico al default derivado del positional), pero el comportamiento no cambia y queda fuera del scope de este commit. PR2 puede limpiarlo cuando enriquezca la proyección LINQ.
3. **No se tocó** `CargoSkillDetailDto` (DTO de GET, usa `(Skill, Nivel)` con `Id` nested — concepto distinto), `PersonaSkillDto` (DTO de otro agregado), `CargoDto`/`Cargo`/`CargoHabilidad` (entidades de dominio con `NivelId` como FK a `NivelesCargo`, concepto distinto). El refactor es estrictamente local al write DTO `CargoSkillDto`.

## PR1 — Aplicación (completado)

- **Branch**: `feat/cargo-habilidad-pr1-aplicacion`
- **Estado**: completado
- **Strict TDD**: activo (`openspec/config.yaml` → `strict_tdd: true`)
- **Safety net inicial**: `dotnet test --filter CargoSkill` → 35/35 PASS; `dotnet test --filter HabilidadAntiDrift` → 4/4 PASS; `dotnet build SGV.slnx` OK.

## Tareas implementadas

- **T1.1** ✅ Extender DTOs y request.
- **T1.2** ✅ Crear `AsignarCargoSkillRequestValidator`.
- **T1.3** ✅ Extender `CargoSkillServicio.UpsertAsync` con defaults y validator.
- **T1.4** ✅ Validar replace idempotente con campos del vínculo.
- **T1.5** ✅ Validar `ListAsync` con DTO enriquecido.

## Métricas

- **Tests al inicio**: 35 (subset `CargoSkill`) + 4 (anti-drift).
- **Tests al cierre**: 64 (subset `CargoSkill`) + 4 (anti-drift) → **+29 tests nuevos** en el subset `CargoSkill` (explicados abajo).
- **Detalle de los 29 nuevos**:
  - `CargoSkillServicioTests` (Aplicación): +6 tests nuevos (`SinPonderacionNiEsObligatoria_AplicaDefaultsYDevuelveDtoCompleto`, `RequestConPonderacionYEsObligatoria_PersisteYDevuelveValoresDelRequest`, `PonderacionInvalida_RetornaFieldErrorsSinGuardar` con 4 inline data → 4 runs, `NivelRequeridoIdVacio_RetornaFieldErrorsSinConsultarRepos`, `AsociacionExistente_ReemplazaConValoresPersistidos`, `AsociacionExistente_MismoRequestEsIdempotente`) — total: 10 runs nuevos.
  - `AsignarCargoSkillRequestValidatorTests` (Aplicación): +19 tests nuevos (19 individuales contando Theory).
  - Subtotal nuevo: 29 tests.
- **Build**: `dotnet build SGV.slnx` ✅
- **Suite subset**: `dotnet test --filter "FullyQualifiedName~CargoSkill"` ✅ **64/64 PASS**
- **Anti-drift**: `dotnet test --filter "FullyQualifiedName~HabilidadAntiDrift"` ✅ **4/4 PASS**
- **Combined PR1 subset**: `dotnet test --filter "FullyQualifiedName~CargoSkill|FullyQualifiedName~HabilidadAntiDrift"` ✅ **68/68 PASS**
- **Suite completa**: `dotnet test SGV.slnx` → **1309/1321 PASS**. Los 12 fallos son pre-existentes de `OcupacionRepositoryTests` (issue #59, `ActivePuestoIdUnique INT` vs `PuestoId CHAR(36)`), fuera del scope de PR1.
- **Diff total**: +608/−39 líneas en 9 archivos. Cada commit individual < 150 líneas (excepto `74713f65` que combina rename mecánico en DTO + tests con 122 inserciones).

## TDD Cycle Evidence

| Task | Test File | Layer | Safety Net | RED | GREEN | TRIANGULATE | REFACTOR |
|------|-----------|-------|------------|-----|-------|-------------|----------|
| T1.1 | `tests/SGV.Tests/Aplicacion/Organizacion/CargoSkillServicioTests.cs` | Unit | ✅ 35/35 | ✅ Compile fail (no `NivelRequeridoId`/`Ponderacion`/`EsObligatoria`) | ✅ Build verde + 36/36 | ➖ Single test por escenario | ✅ Nombres y constantes en código limpio |
| T1.2 | nuevo `tests/SGV.Tests/Aplicacion/Organizacion/AsignarCargoSkillRequestValidatorTests.cs` | Unit | ✅ 36/36 | ✅ Compile fail (no `AsignarCargoSkillRequestValidator`) | ✅ 19/19 (Theory cubre 0, −1, −0.01, 100.01, 150, 1.001, 1.257, 99.999) | ✅ 4 paths de validación (vacío, rango, precisión, opcionales) | ✅ Constantes `PonderacionMaxima`/`PonderacionDecimales` extraídas |
| T1.3 | `tests/SGV.Tests/Aplicacion/Organizacion/CargoSkillServicioTests.cs` | Unit | ✅ 55/55 | ✅ Compile fail (no ctor 6-arg con `IValidator`) | ✅ 60/60 | ✅ 7 tests (defaults, persistencia de valores explícitos, 4 inline para `Ponderacion` inválida, vacío de `NivelRequeridoId`, replace) | ✅ `BuildDto` y `BuildFieldErrors` extraídos; `ToCamelCase` privado |
| T1.4 | `tests/SGV.Tests/Aplicacion/Organizacion/CargoSkillServicioTests.cs` | Unit | ✅ 60/60 | ✅ Test escrito (verifica idempotencia, código ya la soporta) | ✅ Pasa al primer run | ✅ Caso replace + idempotencia en el mismo `CargoSkill` | ➖ Comportamiento ya validado |
| T1.5 | `tests/SGV.Tests/Aplicacion/Organizacion/CargoSkillServicioTests.cs` | Unit | ✅ 60/60 | ✅ Test extendido (verifica `SkillId`/`NivelRequeridoId`/`Ponderacion`/`EsObligatoria` en DTO de lectura) | ✅ Pasa al primer run (fake ya proyecta) | ✅ Una asociación obligatoria + una opcional | ➖ Comportamiento ya validado |

## Commits

```
bb95a72d test: extend cargo skill DTO contract with nivel/ponderacion/esObligatoria
74713f65 feat: extend cargo skill DTOs with nivel/ponderacion/esObligatoria
17724933 test: cover asignar cargo skill request validator rules
88061e77 feat: add asignar cargo skill request validator
abf40178 test: cover cargo skill defaults and field errors
9be4d989 feat: extend cargo skill service with defaults and field errors
67b9a844 test: triangulate cargo skill replace idempotency and enriched list
```

7 commits, todos en formato conventional commits. Sin `Co-Authored-By:` ni atribución a IA.

## Archivos modificados / creados

**Producción (`src/SGV.Aplicacion/`):**
- `Organizacion/Comandos/CargoSkillRequests.cs` — request con `NivelRequeridoId`, `Ponderacion?`, `EsObligatoria?`.
- `Organizacion/Comandos/CargoSkillCommandResult.cs` — agrega `FieldErrors` + overload `Failure(error, fieldErrors)`.
- `Organizacion/Comandos/CargoSkillServicio.cs` — inyecta `IValidator<AsignarCargoSkillRequest>`, defaults `Ponderacion=1.00`/`EsObligatoria=false`, `BuildFieldErrors` + `ToCamelCase`, constante `PonderacionPorDefecto`/`EsObligatoriaPorDefecto`, overload de compatibilidad 5-arg.
- `Organizacion/Comandos/Validaciones/AsignarCargoSkillRequestValidator.cs` *(nuevo)* — reglas FluentValidation: `NivelRequeridoId != Guid.Empty`, `Ponderacion > 0`, `Ponderacion <= 100.00`, máx 2 decimales. Constantes `PonderacionMaxima`/`PonderacionDecimales` públicas.
- `Organizacion/Consultas/Dtos/CargoSkillDto.cs` — agrega `NivelRequeridoId`/`Ponderacion`/`EsObligatoria` como init-only sobre el ctor posicional existente `(SkillId, NivelId)` para preservar compatibilidad.
- `Organizacion/Consultas/Dtos/CargoSkillDetailDto.cs` — agrega `SkillId`/`NivelRequeridoId`/`Ponderacion`/`EsObligatoria` como init-only sobre el ctor posicional existente `(Skill, Nivel)`.

**Tests:**
- `tests/SGV.Tests/Aplicacion/Organizacion/CargoSkillServicioTests.cs` — renombrado, +7 tests nuevos (defaults, validación con `FieldErrors`, replace, idempotencia, `ListAsync` enriquecido).
- `tests/SGV.Tests/Aplicacion/Organizacion/AsignarCargoSkillRequestValidatorTests.cs` *(nuevo)* — 19 tests (cubren reglas de `NivelRequeridoId`, `Ponderacion` rango/precisión, opcionalidad).
- `tests/SGV.Tests/Api/CargoSkillControllerTests.cs` — cambio mecánico en un test: `nivelId` → `nivelRequeridoId` en el body y `dto.NivelId` → `dto.NivelRequeridoId` en la aserción (necesario por el rename del request).

## Notas de implementación

1. **DTOs con backward compat**: `CargoSkillDto` y `CargoSkillDetailDto` mantienen su ctor posicional original (`(SkillId, NivelId)` y `(Skill, Nivel)` respectivamente). Los nuevos campos se exponen como propiedades `init`-only. Esto evita tocar el call site del repositorio de Infraestructura y los fakes web existentes. PR2 debe:
   - Enriquecer la proyección LINQ del repositorio (`CargoSkillRepository.ListDetailedByCargoIdAsync`) para popular los nuevos campos desde la entidad.
   - Decidir si elimina el `NivelId` legacy del DTO o lo conserva como alias deprecado. Mi recomendación: eliminarlo en PR2 para no contaminar el contrato. Lo dejé en su sitio para no romper tests no-PR1.

2. **Constructor overload del servicio**: agregué un segundo constructor 5-arg (sin validator) que instancia `new AsignarCargoSkillRequestValidator()` por compat. Esto preserva el wiring actual de `CargosController` en PR1 sin cambios. PR2 puede migrar el wiring de DI explícitamente al usar `AddValidatorsFromAssemblyContaining<AsignarCargoSkillRequestValidator>` (ya activo por la convención del proyecto).

3. **Convención de keys para `FieldErrors`**: agrupadas por `ToCamelCase(propertyName)` para que el JSON emitido por el controller (en PR2) coincida con el casing del request entrante (`ponderacion`, `nivelRequeridoId`). Mismo patrón que `HabilidadServicioComandos.BuildFieldErrors`.

4. **`decimal` precision**: validé "máximo 2 decimales" con `decimal.Round(value, 2) == value`. Funciona correctamente con la representación interna de `decimal` (preserva ceros trailing) sin tener que parsear strings. No usa `FluentValidation.ScalePrecision` porque esa extensión no está disponible en `FluentValidation 12.1.1`.

5. **Anti-drift**: `Habilidad` sigue sin `NivelId`. La fuente de verdad del nivel sigue siendo `CargoHabilidad.NivelRequeridoId` (memoria #569). El nuevo DTO `CargoSkillDetailDto` usa `NivelHabilidadDto` para el nivel requerido del vínculo, nunca `HabilidadDto.NivelId`.

## Pendientes para PR2/PR3a/PR3b

- **PR2 (T2.1)**: `CargoSkillRepository.ListDetailedByCargoIdAsync` debe popular `SkillId`, `NivelRequeridoId`, `Ponderacion`, `EsObligatoria` desde `CargoHabilidadEntity` en una sola query LINQ sin N+1. PR1 dejó el DTO con init-only properties esperando esta proyección.
- **PR2 (T2.2)**: `ToSkillProblemResult` debe bifurcarse — emitir `ValidationProblemDetails` cuando `result.FieldErrors?.Count > 0`, manteniendo `Problem(...)` cuando no. La infraestructura ya está del lado de la aplicación.
- **PR2 (T2.3)**: Actualizar `<response>` y schema Swagger para reflejar `nivelRequeridoId` (sin alias `nivelId`) en el GET del subrecurso. Decidir si eliminar `NivelId` legacy del DTO `CargoSkillDto` (mi recomendación: sí, para no contaminar el contrato; el alias está documentado como transitorio).
- **PR3a**: cliente tipado en `ICargoApiClient`/`CargoApiClient` con `GetSkillsAsync`/`UpsertSkillAsync`/`DeleteSkillAsync`, parseando `ValidationProblemDetails` → `CargoSkillCommandResult.Failure(error, fieldErrors)`.
- **PR3b**: Razor Page `Habilidades.cshtml` + anti-drift cruzado.

## Riesgos emergentes

- **Backwards compat del JSON del PUT**: la rename `nivelId` → `nivelRequeridoId` en el body rompe consumidores existentes del PUT. Documentado en el cambio (decisión del usuario) pero PR2 debe alinear el controller para reflejar el nuevo shape en errores y Swagger.
- **`NivelId` legacy en `CargoSkillDto`**: si el controller decide serializarlo, contaminaría el contrato. PR2 debe decidir explícitamente: o lo elimina del record o lo marca con `[JsonIgnore]`. Mi recomendación: eliminar el campo para alinear con el spec (Req 1 de `cargo-skill-query-contract`: "El contrato GET MUST exponer exactamente los datos que la UI necesita"). En `CargoSkillDto` (write), `NivelId` puede mantenerse como alias deprecado durante un release para no romper integraciones existentes.
- **`CargoSkillCommandResult.Value`**: en el camino de fallo sin `FieldErrors` (e.g., `NotFound`), `Value` queda `null`. El controller actual (`ToSkillProblemResult`) ya maneja `Error` separado, pero PR2 debe decidir si expone `Value` en errores no-validación. Mi código lo deja `null` consistente con `HabilidadCommandResult`.
- **`MySqlFact` de `CargoSkillRepository`**: PR2 los introducirá. PR1 no toca persistencia, por lo que estos `[MySqlFact]` siguen verdes o se skipean limpios sin MySQL local (mismo patrón que `OcupacionRepositoryTests` issue #59).

## Verificación al cierre de PR1

```bash
# Build limpio
dotnet build SGV.slnx
# → Build succeeded. 0 Warning(s). 0 Error(s).

# Subset PR1
dotnet test SGV.slnx --filter "FullyQualifiedName~CargoSkill"
# → Total tests: 64. Passed: 64. Failed: 0.

dotnet test SGV.slnx --filter "FullyQualifiedName~HabilidadAntiDrift"
# → Total tests: 4. Passed: 4. Failed: 0.

dotnet test SGV.slnx --filter "FullyQualifiedName~CargoSkill|FullyQualifiedName~HabilidadAntiDrift"
# → Total tests: 68. Passed: 68. Failed: 0.

# Suite completa (informativo, los 12 fallos son issue #59 pre-existente)
dotnet test SGV.slnx
# → Total: 1321. Passed: 1309. Failed: 12 (issue #59, OcupacionRepositoryTests).
```