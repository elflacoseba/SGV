# Apply Progress — Implementar asignar/quitar Habilidades de un Cargo

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