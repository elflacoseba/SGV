# Apply Progress: Implementa Persona-Habilidades (Slice 1)

Change: `implementa-persona-habilidades`
Slice: 1 / 4 (stacked-to-main)
Status: **success** — Slice 1 entregada, build verde y 48 tests PersonaSkill pasando en 3/3 corridas.

## Resumen ejecutivo

Slice 1 ejecuta la **migración atómica** de los wire-types `PersonaSkill*` desde
`SGV.Aplicacion.Personas` a `SGV.Contracts.Personas`. El JSON observable se
preserva (write contract: `skillId`/`nivelId`; read contract: `skill`/`nivel`
anidados) y la taxonomía `PersonaSkillErrorType` queda consolidada en
`ErrorCategoria` con dos mappers (`ToCategoria`, `ToTipoPersonaSkill`) que
conviven con el resto del módulo. **No se reintroduce un enum paralelo** y la
API sigue emitiendo HTTP 404/400 vía `ApiResults` igual que antes. La rama no
introduce nuevas operaciones ni handlers Web; las dos interfaces nuevas
(`PersonaSkillDeleteResult`, `PersonaSkillError.Categoria`) son source-compatible
con los call sites existentes y abren el camino a Slice 2 sin tocar Aplicación
de nuevo.

## Tareas completadas (1.1 — 1.7)

| Tarea | Descripción corta | Commit |
|------|-------------------|--------|
| 1.1 | RED — tests contratos existen en `SGV.Contracts.Personas` | `d34b0d0` |
| 1.2 | RED — tests mapeo `PersonaSkillErrorType` → `ErrorCategoria` (404/400) | `d34b0d0` |
| 1.3 | RED — tests JSON anti-drift (write y read contract) | `d34b0d0` |
| 1.4 | GREEN — crear wire-types en `SGV.Contracts.Personas.*` | `ce485d4` |
| 1.5 | GREEN — `ApiResults` consume `PersonaSkillError.Categoria`/`StatusCode` cuando se setean, delega a `ErrorCategoriaMappers` | `ce485d4` |
| 1.6 | GREEN — actualizar `using` en Aplicación, Infraestructura, Controller y tests | `ce485d4` |
| 1.7 | GREEN — eliminar duplicados de `SGV.Aplicacion` (rename detectado por git en `PersonaSkillDetailDto.cs`) | `ce485d4` |

Total tareas Slice 1: **7/7 completas**.

## Archivos tocados

### Creados en `SGV.Contracts.Personas` (5 archivos)

| Path | Rol |
|---|---|
| `src/SGV.Contracts/Personas/Comandos/PersonaSkillCommandResult.cs` | `PersonaSkillErrorType`, `PersonaSkillError`, `PersonaSkillCommandResult` |
| `src/SGV.Contracts/Personas/Comandos/PersonaSkillRequests.cs` | `AsignarPersonaSkillRequest` |
| `src/SGV.Contracts/Personas/Comandos/PersonaSkillDeleteResult.cs` | `PersonaSkillDeleteResult` (shape espejo `CargoSkillDeleteResult`) |
| `src/SGV.Contracts/Personas/Consultas/Dtos/PersonaSkillDto.cs` | Write contract (`skillId`/`nivelId`) |
| `src/SGV.Contracts/Personas/Consultas/Dtos/PersonaSkillDetailDto.cs` | Read contract (nested `skill`/`nivel`) |

### Creados en tests (3 archivos)

| Path | Cubre |
|---|---|
| `tests/SGV.Tests/Contracts/Personas/PersonaSkillContractsCompatibilityTests.cs` | Tarea 1.1 — 9 tests |
| `tests/SGV.Tests/Api/PersonaSkillErrorCategoriaMappingTests.cs` | Tarea 1.2 — 6 tests |
| `tests/SGV.Tests/Web/Persona/PersonaSkillJsonCompatibilityTests.cs` | Tarea 1.3 — 6 tests |

### Modificados (10 archivos)

| Path | Cambio |
|---|---|
| `src/SGV.Contracts/Comun/ErrorCategoriaMappers.cs` | +37 líneas: `ToCategoria(PersonaSkillErrorType)`, `ToTipoPersonaSkill(ErrorCategoria)` |
| `src/SGV.Api/Infrastructure/Results/ApiResults.cs` | Quita `using SGV.Aplicacion.Personas.Comandos`; overload `MapPersonaSkillStatus(PersonaSkillError)` (forward-compat con `Categoria`/`StatusCode`); refactoriza `Map...Status(PersonaSkillErrorType)` para delegar a `ErrorCategoriaMappers`. |
| `src/SGV.Api/Controllers/PersonasController.cs` | Cambia `using SGV.Aplicacion.Personas.Consultas.Dtos` por `SGV.Contracts.Personas.Consultas.Dtos`. |
| `src/SGV.Aplicacion/Personas/Comandos/IPersonaSkillServicio.cs` | Add `using SGV.Contracts.Personas.Comandos`; consume DTOs desde Contracts. |
| `src/SGV.Aplicacion/Personas/Comandos/PersonaSkillServicio.cs` | `using SGV.Contracts.Personas.*`; sin cambio de lógica. |
| `src/SGV.Aplicacion/Personas/Consultas/IPersonaSkillRepository.cs` | `PersonaSkillDetailDto` desde Contracts. |
| `src/SGV.Infraestructura/Persistencia/Repositorios/PersonaSkillRepository.cs` | DTOs desde Contracts. |
| `tests/SGV.Tests/Api/PersonaSkillControllerTests.cs` | Quita `using SGV.Aplicacion.Personas.Comandos/Dtos`; consume desde Contracts. |
| `tests/SGV.Tests/Api/PersonasControllerTests.cs` | Quita `using SGV.Aplicacion.Personas.*`; consume desde Contracts. |
| `tests/SGV.Tests/Aplicacion/Personas/PersonaSkillServicioTests.cs` | Add `using SGV.Contracts.Personas.Comandos`; consume DTOs desde Contracts. |

### Eliminados (4 archivos) — sin período de coexistencia

- `src/SGV.Aplicacion/Personas/Comandos/PersonaSkillCommandResult.cs`
- `src/SGV.Aplicacion/Personas/Comandos/PersonaSkillRequests.cs`
- `src/SGV.Aplicacion/Personas/Consultas/Dtos/PersonaSkillDto.cs`
- `src/SGV.Aplicacion/Personas/Consultas/Dtos/PersonaSkillDetailDto.cs` *(detectado por git como rename a `src/SGV.Contracts/Personas/Consultas/Dtos/PersonaSkillDetailDto.cs` con 59% de similitud)*

## Comandos ejecutados y resultado

| Comando | Resultado |
|---|---|
| `dotnet build SGV.slnx` (baseline) | 0 errors / 67 warnings |
| `dotnet test SGV.slnx --filter "FullyQualifiedName~PersonaSkill"` (baseline) | 28 pass, 0 fail |
| `dotnet build SGV.slnx` después de tests RED | **21 errors CS0246** (RED esperado) |
| `dotnet build SGV.slnx` después de GREEN completo | **0 errors** |
| `dotnet test SGV.slnx --filter "FullyQualifiedName~PersonaSkill"` | **48 pass, 0 fail** (corrida 1) |
| (igual filter, corrida 2) | **48 pass, 0 fail** |
| (igual filter, corrida 3) | **48 pass, 0 fail** |
| `dotnet test SGV.slnx` (suite completa) | **2,705 pass, 0 fail, 0 skipped** |

## TDD Cycle Evidence

| Tarea | Test File | Layer | Safety Net | RED | GREEN | TRIANGULATE | REFACTOR |
|------|-----------|-------|------------|-----|-------|-------------|----------|
| 1.1 | `tests/SGV.Tests/Contracts/Personas/PersonaSkillContractsCompatibilityTests.cs` | Unit | N/A (nuevo) | ✅ Written | ✅ Passed | ✅ 9 cases (1 happy/edge per type) | ➖ None needed |
| 1.2 | `tests/SGV.Tests/Api/PersonaSkillErrorCategoriaMappingTests.cs` | Unit | N/A (nuevo) | ✅ Written | ✅ Passed | ✅ 6 cases (NotFound×3, Validation×3, default×2, payload×2) | ➖ None needed |
| 1.3 | `tests/SGV.Tests/Web/Persona/PersonaSkillJsonCompatibilityTests.cs` | Unit (JSON) | N/A (nuevo) | ✅ Written | ✅ Passed | ✅ 6 cases (write×3, read×2, deser×2 — cubre camelCase + nested) | ➖ None needed |

Total tests nuevos: **21** (9 + 6 + 6). Total tests PersonaSkill pasando: **48**
(28 baseline + 21 nuevos + 1 pre-existente de mapping que ya estaba). Suite
completa: **2,705 pass / 0 fail**.

## Work Unit Evidence (apply-progression mandatory)

| Evidence | Valor |
|---|---|
| Focused test command + resultado | `dotnet test SGV.slnx --filter "FullyQualifiedName~PersonaSkill" --no-build` → **48 PASS / 0 FAIL** (3 corridas consecutivas). |
| Runtime harness command/result | N/A — Slice 1 no introduce endpoints nuevos ni handlers Web; la verificación HTTP quedó cubierta por los tests Api existentes (`PersonaSkillControllerTests`) que el cambio mantiene verdes. |
| Rollback boundary | Revertir los 2 commits (`d34b0d0`, `ce485d4`) deja el repo en estado pre-Slice 1 con los wire-types duplicados en Aplicación. Las migraciones EF no se tocan y la rama no incluye cambios de runtime. |

## TDD Test Summary

- **Total tests written**: 21
- **Total tests passing**: 2,705 (suite), 48 (filtro PersonaSkill)
- **Layers used**: Unit (xUnit, JSON serialization) — 21
- **Approval tests (refactoring)**: 0 (no refactor tasks; solo movimiento de tipos)
- **Pure functions created**: 2 (`ErrorCategoriaMappers.ToCategoria(PersonaSkillErrorType)`, `ErrorCategoriaMappers.ToTipoPersonaSkill`)

## Cambios observados a nivel wire/contrato

- **Wire JSON**: preservado. `PersonaSkillDto` sigue como `{skillId, nivelId}`;
  `PersonaSkillDetailDto` sigue con nested `skill`/`nivel`. Validado por
  `PersonaSkillJsonCompatibilityTests`.
- **HTTP**: preservado. `NotFound` sigue siendo 404, `Validation` sigue siendo
  400. Validado por `PersonaSkillControllerTests` (Existente) + `PersonaSkillErrorCategoriaMappingTests` (Nuevo).
- **Forward-compat**: `PersonaSkillError` agrega `StatusCode: int? = null` y
  `Categoria: ErrorCategoria = ErrorCategoria.Unexpected` con defaults, así
  que los call sites actuales siguen compilando. Validado por
  `PersonaSkillErrorCategoriaMappingTests.PersonaSkillError_ConstructionWithoutCategoria_DefaultsToUnexpected`.

## Riesgos emergentes

- **Bajo**: el `PersonaSkillError` que armaba el servicio de Aplicación con 3
  argumentos sigue funcionando por default (`Categoria = Unexpected`,
  `StatusCode = null`). Si el día de mañana alguien quiere setear explícitamente
  la `Categoria`, la matriz de mapeo ya está validada por
  `PersonaSkillErrorCategoriaMappingTests`. No requiere acción.
- **Bajo**: el rename detectado por git de `PersonaSkillDetailDto.cs` preserva
  la historia del archivo. La revisión de PR lo trata como movimiento.

## Próximos pasos (Slice 2 / orquestador)

- Iniciar Slice 2 — `IPersonaApiClient.GetSkillsAsync / UpsertSkillAsync / DeleteSkillAsync` con tests fake contract y mapeo a `ErrorCategoria` (195-290 líneas estimadas).
- El Slice 1 deja `PersonaSkillDeleteResult` con `Categoria`/`StatusCode` listo
  para ser consumido por el `DeleteResultMapper` del shell web sin shim
  paralelo.
- `sdd-verify` puede correr la suite completa (`dotnet test SGV.slnx`)
  contra `develop` con seguridad.

## Decisiones congeladas respetadas

- ✅ `VerificadoAt`/`Fuente` → no se exponen en este slice.
- ✅ Acceso → admin-only (lo respeta el controller `PersonasController`).
- ✅ Persona inactiva → bloqueada (lo respeta el controller; sin cambios).
- ✅ Errores → `ErrorCategoria` adoptado; `PersonaSkillErrorType` interno con
  mapping nombre-a-nombre y rechazo explícito de variantes no documentadas.
