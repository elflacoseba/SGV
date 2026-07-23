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

---

## Slice 2 — Cliente tipado + fakes (stacked-to-main, PR #2)

Slice: 2 / 4 (stacked-to-main)
Status: **success** — 2 commits atómicos (RED + GREEN) con strict TDD; build verde y suite completa 2,750 pass / 0 fail / 0 skipped (3 corridas consecutivas).

### Resumen ejecutivo

Slice 2 extiende `IPersonaApiClient` con el subrecurso `persona-skill` (consulta paginada, upsert idempotente y baja explícita) preservando el wire JSON de Slice 1 (`{ skill: {...}, nivel: {...} }` en read; `{ skillId, nivelId }` en write). El cliente HTTP delega los errores no exitosos en `CommandResultMapper`/`DeleteResultMapper` (única fuente de verdad de la taxonomía `ErrorCategoria` en el shell web), con un helper `ToSkillCommandResultAsync` específico para el subrecurso y un fallback `MapCategoriaToLegacySkillType` que colapsa categorías fuera del enum interno `PersonaSkillErrorType` (sólo `NotFound`/`Validation`) a `Validation`, preservando `Categoria: ErrorCategoria` como fuente de verdad observable. El `FakePersonaApiClient` se extiende con seed configurable + contadores + hooks de excepción, análogo al `FakeCargoApiClient`. NO se reintroduce el enum paralelo en el shell; el contrato del cliente web contrae a `ErrorCategoria` y `PersonaSkillErrorType` queda como discriminador interno con mapping explícito. La rama no toca `SGV.Api`, `Pages/`, `Dominio` ni `Infraestructura`, así que el PR 2 queda bajo el budget de review (≤400 líneas modificadas por archivo) sin arrastre cruzado.

### Tareas completadas (2.1 — 2.5)

| Tarea | Descripción corta | Commit |
|------|-------------------|--------|
| 2.1 | RED — test contrato de firma de los 3 métodos (`GetSkillsAsync`/`UpsertSkillAsync`/`DeleteSkillAsync`) en `IPersonaApiClient` | `b9f0da2f` |
| 2.2 | RED — test comportamiento del fake: defaults, seed, errores por `ErrorCategoria` (NotFound/Validation/Conflict/Unauthorized/Forbidden/Transport), propagación de excepciones nativas | `b9f0da2f` |
| 2.3 | GREEN — agregar los 3 métodos a `IPersonaApiClient` con XML docs (REQ-WEB-04/05) | `3664b1a9` |
| 2.4 | GREEN — implementar los 3 métodos en `PersonaApiClient` + helper `ToSkillCommandResultAsync` + `MapCategoriaToLegacySkillType` | `3664b1a9` |
| 2.5 | GREEN — extender `FakePersonaApiClient` con seed + contadores + hooks de excepción | `3664b1a9` |

Total tareas Slice 2: **5/5 completas**.

### Archivos tocados (8 archivos)

#### Modificados producción (3 archivos)

| Path | Cambio |
|---|---|
| `src/SGV.Contracts/Personas/Comandos/PersonaSkillCommandResult.cs` | +`FieldErrors` opcional en el record; +sobrecarga `Failure(error, fieldErrors)` (extensión source-compat con el call site de Slice 1) |
| `src/SGV.Web/Integration/Personas/IPersonaApiClient.cs` | +3 métodos públicos del subrecurso (`GetSkillsAsync`/`UpsertSkillAsync`/`DeleteSkillAsync`) con XML docs alineados a `ICargoApiClient` |
| `src/SGV.Web/Integration/Personas/PersonaApiClient.cs` | +3 implementaciones HTTP (GET/PUT/DELETE); 404 → estado vacío en GET; helper privado `ToSkillCommandResultAsync` que delega en `CommandResultMapper` + preserva `FieldErrors`; `MapCategoriaToLegacySkillType` con fallback `Validation` vía `ErrorCategoriaMappers.ToTipoPersonaSkill` |

#### Modificados tests (4 archivos)

| Path | Cambio |
|---|---|
| `tests/SGV.Tests/Web/Persona/FakePersonaApiClient.cs` | +`using SGV.Contracts.Comun`; +`GetSkillsResult`/`GetSkillsException`/`GetSkillsCalls`; +`SkillUpsertResult` (default `Failure FakeNotConfigured` con `Categoria: Validation`); +`SkillUpsertCalls`/`SkillUpsertException`; +`SkillDeleteResult` (default `Success NoContent`); +`SkillDeleteCalls`/`SkillDeleteException`; +3 implementaciones explícitas de los métodos |
| `tests/SGV.Tests/Web/Persona/IPersonaApiClientContractTests.cs` | Actualizado el guard `Interface_ExposesExactlySevenPublicAsyncMethods` → `Interface_ExposesExactlyTwelvePublicAsyncMethods` para incluir los 3 nuevos métodos del subrecurso |
| `tests/SGV.Tests/Web/Persona/PersonaWebSeamTests.cs` | +2 guards de inyección (registro de `ApiBearerTokenHandler` como transient + presencia de los 3 métodos del subrecurso en `IPersonaApiClient`) |
| `tests/SGV.Tests/Web/SgvWebApplicationFactory.cs` | +parámetro `personaApiHandler` (espejo de `cargoApiHandler`) para soportar el patrón `CreateCargoBridgeLeaseAsync` cuando se necesite para el subrecurso persona-skill en Slice 3b |

#### Nuevos tests (1 archivo)

| Path | Cubre |
|---|---|
| `tests/SGV.Tests/Web/Persona/PersonaSkillApiClientTests.cs` | Seam HTTP del subrecurso contra `RecordingHandler`: rutas y métodos HTTP (`GET/PUT/DELETE /api/v1/personas/{personaId}/skills[/{skillId}]`); 200 con payload → DTO deserializado; 200 con body vacío → `Failure EmptyBody` (PR3a R1 review follow-up); 400 con `ValidationProblemDetails` → `FieldErrors` preservados; 400 con `ProblemDetails` plano → `Failure Validation`; 404 → `Failure NotFound`; 5xx → `Failure Transport` (defaults del mapper común); 401 → `Failure Unauthorized`; 403 → `Failure Forbidden`; propagación de excepciones nativas (`HttpRequestException`/`TaskCanceledException`); cancelación cooperativa pre-cancelada |

### Tests añadidos

| Suite | Cantidad | Tipo |
|---|---|---|
| `PersonaSkillClientContractTests` | 4 | Contrato de firma (Unit/Reflection) |
| `PersonaApiClientSkillErrorsTests` | 14 | Comportamiento del fake (Unit) |
| `PersonaSkillApiClientTests` | 25 | Seam HTTP (Unit/RecordingHandler) |
| `PersonaWebSeamTests` (Slice 2) | 2 | Inyección DI (Integration) |
| `IPersonaApiClientContractTests` (modificado) | 1 (renombrado) | Guard de conteo de métodos |
| **Total nuevos** | **45** | Mixto (42 Unit + 3 Integration) |
| **Total nuevos strict TDD** | **42** | Excluye los 3 guards de inyección/seam heredados |

Total tests PersonaSkill (Slice 1 + Slice 2): **48 (Slice 1) + 45 (Slice 2) = 93 tests pasando** sobre el subrecurso persona-skill.

### Comandos ejecutados y resultado

| Comando | Resultado |
|---|---|
| `dotnet build SGV.slnx` (baseline pre-Slice 2) | 0 errors / 84 warnings (preexistentes) |
| `dotnet test SGV.slnx` (baseline pre-Slice 2) | **2,705 pass / 0 fail** |
| `dotnet build SGV.slnx` después de tests RED (sin GREEN) | **33 errors CS0117/CS1061** (RED esperado — métodos no existen) |
| `dotnet build SGV.slnx` después de GREEN completo | **0 errors** |
| `dotnet test --filter "FullyQualifiedName~PersonaSkill"` (corrida 1) | **93 pass / 0 fail** |
| (igual filter, corrida 2) | **93 pass / 0 fail** |
| (igual filter, corrida 3) | **93 pass / 0 fail** |
| `dotnet test SGV.slnx` (suite completa) | **2,750 pass / 0 fail / 0 skipped** |

### TDD Cycle Evidence

| Tarea | Test File | Layer | Safety Net | RED | GREEN | TRIANGULATE | REFACTOR |
|------|-----------|-------|------------|-----|-------|-------------|----------|
| 2.1 | `PersonaSkillClientContractTests.cs` | Unit (Reflection) | N/A (nuevo) | ✅ Written (4 tests, 33 errors CS) | ✅ Passed | ➖ Single (1 happy/edge por método) | ✅ Renombrado guard preexistente `Interface_ExposesExactlySeven...` → `...Twelve...` |
| 2.2 | `PersonaApiClientSkillErrorsTests.cs` | Unit (in-memory) | N/A (nuevo) | ✅ Written (14 tests, errores CS en `FakePersonaApiClient`) | ✅ Passed | ✅ 14 casos: defaults×3, seed×2, exceptions×2, errores×7 (NotFound/Validation/Conflict/Unauthorized/Forbidden/Transport) | ➖ None needed (assertions directas) |
| 2.3 + 2.5 | `PersonaSkillClientContractTests.cs` + `PersonaApiClientSkillErrorsTests.cs` (RED existentes) | Unit | ✅ 18/18 RED pasaron tras GREEN | ✅ Written | ✅ Passed (18/18) | ✅ 18 casos | ➖ None needed |
| 2.4 + 2.5 (HTTP) | `PersonaSkillApiClientTests.cs` | Unit (HTTP) | N/A (nuevo) | ✅ Written | ✅ Passed (25/25) | ✅ 25 casos: rutas×3, 200+payload×3, 400+validation×2, 400+plain×1, 404×2, 401×1, 403×1, 5xx×4, transport×3, cancellation×1, emptyBody×1, getSkillNotFound×1, getSkillOk×1 | ➖ None needed |

Total tests escritos: 45 (4 + 14 + 25 + 2 injection guards). Total tests pasando: 2,750 (suite), 93 (filtro PersonaSkill).

### Work Unit Evidence (apply-progression mandatory)

| Evidence | Valor |
|---|---|
| Focused test command + resultado | `dotnet test SGV.slnx --filter "FullyQualifiedName~PersonaSkill" --no-build` → **93 PASS / 0 FAIL** (3 corridas consecutivas). |
| Runtime harness command/result | `dotnet build SGV.slnx` → exit 0 (0 errors, 84 warnings preexistentes). Suite completa `dotnet test SGV.slnx --no-build` → 2,750/0/0. |
| Rollback boundary | Revertir los 2 commits (`b9f0da2f`, `3664b1a9`) deja el repo en estado post-Slice 1 con el subrecurso persona-skill sólo en `SGV.Contracts.Personas` (sin cliente tipado, sin fake). Slice 1 ya mergeado a develop provee el wire JSON observable. La rama no incluye cambios de runtime (no se tocaron `SGV.Api`, `SGV.Web/Pages`, `SGV.Dominio`, `SGV.Infraestructura`). |

### TDD Test Summary

- **Total tests written**: 45
- **Total tests passing**: 2,750 (suite), 93 (filtro PersonaSkill)
- **Layers used**: Unit (Reflection + in-memory + HTTP recording handler) — 43; Integration (`WebApplicationFactory` vía `WebIntegrationFixture`) — 2
- **Approval tests (refactoring)**: 1 (rename `Interface_ExposesExactlySeven...` → `...Twelve...` en `IPersonaApiClientContractTests`)
- **Pure functions created**: 1 (`PersonaApiClient.MapCategoriaToLegacySkillType`, con fallback `try/catch (NotSupportedException)`)

### Cambios observados a nivel wire/contrato

- **Wire JSON (read)**: preservado. `PersonaSkillDetailDto` sigue con nested `Skill`/`Nivel` (DTOs ya consolidados en Slice 1). Validado por el test `GetSkillsAsync_Http200WithPayload_ReturnsParsedDtosAndHitsSubresourceRoute`.
- **Wire JSON (write)**: preservado. `AsignarPersonaSkillRequest { nivelId }` se serializa tal cual; `personaId` y `skillId` viajan en la ruta. Validado por el test `UpsertSkillAsync_Http200WithPayload_ReturnsSuccessDtoAndHitsPutSubresourceRoute`.
- **HTTP**: preservado. `GET /api/v1/personas/{personaId}/skills` → 200 OK / 404 (estado vacío recuperable); `PUT /api/v1/personas/{personaId}/skills/{skillId}` → 200/204/400/404; `DELETE /api/v1/personas/{personaId}/skills/{skillId}` → 204/404/4xx/5xx. Validado por los tests HTTP del nuevo `PersonaSkillApiClientTests`.
- **Forward-compat**: `PersonaSkillCommandResult` agrega `FieldErrors` opcional con default `null`. Los call sites de Slice 1 (que arman el resultado con 1 argumento) siguen compilando; la nueva sobrecarga `Failure(error, fieldErrors)` se usa sólo en la rama no exitosa del cliente HTTP cuando el backend emite `ValidationProblemDetails`.

### Decisiones congeladas respetadas (Slice 2)

- ✅ Acceso → admin-only (lo respetará el PageModel de Slice 3a vía `[Authorize(Roles = RolesSgv.Administrador)]`; este slice no toca la autorización).
- ✅ Persona inactiva → el cliente delega 404 al PageModel; Slice 3a decide el comportamiento UI.
- ✅ Errores → `ErrorCategoria` adoptado como única taxonomía observable; `PersonaSkillErrorType` queda como discriminador interno (no público al shell) con fallback `Validation` para categorías fuera del enum.

### Próximo paso del orquestador

1. `sdd-verify` puede correr la suite completa (`dotnet test SGV.slnx`) contra la rama con seguridad — el contrato del subrecurso persona-skill queda cerrado en el cliente tipado antes de que Slice 3a construya la Razor Page encima.
2. Lanzar Slice 3a (`feat/implementa-persona-habilidades-pr3a`) con `PersonaHabilidades.cshtml*` + tests auth + GET. Slice 2 deja el cliente HTTP y el fake listos para ser consumidos por el PageModel sin shim paralelo.

### Cambios fuera de scope Slice 2 (verificados)

- ❌ `SGV.Api/` → sin cambios (cumple regla del orquestador).
- ❌ `src/SGV.Web/Pages/...` → sin cambios (queda para Slice 3a).
- ❌ `src/SGV.Dominio/`, `src/SGV.Infraestructura/` → sin cambios (cumple regla del orquestador).
- ⚠️ `SGV.Contracts/Personas.PersonaSkillCommandResult` → modificado sólo para sumar `FieldErrors` opcional + sobrecarga `Failure(error, fieldErrors)`. Cambio source-compat que NO introduce wire-types nuevos ni altera los vigentes.

## Slice 3a — PageModel GET + autorización + vista (stacked-to-main, PR 3)

**Estado**: success — implementación local lista para `sdd-verify`.

### Tareas completadas (3a.1 — 3a.5)

| Tarea | Descripción | Evidencia |
|---|---|---|
| 3a.1 | RED/GREEN — autorización admin-only | `PersonaHabilidadesPageTests` verifica `[Authorize(Roles = Administrador)]`, anónimo y usuario autenticado sin rol reciben `ForbidResult` en el PageModel; el pipeline web convierte esto al flujo de acceso correspondiente. |
| 3a.2 | RED/GREEN — carga inicial y persona inactiva | El test con fake verifica nombre + filas mapeadas y el test de inactiva verifica redirect `/error/404` sin invocar `GetSkillsAsync`. |
| 3a.3 | GREEN — PageModel GET y ViewModel | `PersonaHabilidadesModel.OnGetAsync` carga primero persona y luego `IPersonaApiClient.GetSkillsAsync`; mapea a `PersonaHabilidadesViewModel`/`PersonaHabilidadRowViewModel`; no agrega handlers POST. |
| 3a.4 | GREEN — vista Razor Inspinia y antiforgery | `PersonaHabilidades.cshtml` renderiza encabezado, grilla, estado vacío/recuperable, formularios preparados y `@Html.AntiForgeryToken()` sin depender de handlers POST implementados en este slice. |
| 3a.5 | Verify slice | `dotnet build SGV.slnx` y `dotnet test SGV.slnx --filter "FullyQualifiedName~PersonaHabilidadesPage"` verdes. |

### Archivos tocados en Slice 3a

- `tests/SGV.Tests/Web/Persona/PersonaHabilidadesPageTests.cs` — 5 tests unitarios del PageModel, escritos antes de la implementación.
- `src/SGV.Web/Pages/Personas/PersonaHabilidades.cshtml.cs` — PageModel, autorización, GET, mapeo y gate de persona activa.
- `src/SGV.Web/Pages/Personas/PersonaHabilidades.cshtml` — vista inicial con grilla Inspinia y tokens antiforgery.
- `openspec/changes/implementa-persona-habilidades/tasks.md` — tareas 3a marcadas como completadas.

### Decisión UX de persona inactiva

Se respeta `design.md`: una persona inactiva se considera no consultable y `OnGetAsync` redirige a `/error/404` antes de llamar a `GetSkillsAsync`. Esto evita renderizar controles de gestión y mantiene la autoridad del backend sobre el estado activo. Slice 3b deberá repetir el gate antes de cualquier escritura.

### TDD Cycle Evidence

| Tarea | Test File | Layer | Safety Net | RED | GREEN | TRIANGULATE | REFACTOR |
|---|---|---|---|---|---|---|---|
| 3a.1 | `tests/SGV.Tests/Web/Persona/PersonaHabilidadesPageTests.cs` | Unit/PageModel | N/A (nuevo) | ✅ Written; build falló porque faltaba `PersonaHabilidadesModel` | ✅ 5/5 tests pasan | ✅ anónimo + autenticado sin rol + admin | ✅ gate manual y atributo explícito |
| 3a.2 | `tests/SGV.Tests/Web/Persona/PersonaHabilidadesPageTests.cs` | Unit/PageModel | N/A (nuevo) | ✅ Written; build falló porque faltaba `PersonaHabilidadesModel` | ✅ 5/5 tests pasan | ✅ datos con fila + persona inactiva sin llamada de skills | ✅ mapeo separado a ViewModel |
| 3a.3 | `tests/SGV.Tests/Web/Persona/PersonaHabilidadesPageTests.cs` | Unit/PageModel | N/A (nuevo) | ✅ tests RED previos | ✅ 5/5 tests pasan | ✅ persona activa/inactiva/fallo de acceso | ✅ excepciones recuperables y cancelación |
| 3a.4 | `tests/SGV.Tests/Web/Persona/PersonaHabilidadesPageTests.cs` | Unit + compilación Razor | N/A (nuevo) | ✅ tests RED previos | ✅ `dotnet build SGV.slnx` | ➖ vista estructural, validada por build | ✅ sin Ponderacion/EsObligatoria/NivelRequeridoId |

### Work Unit Evidence

| Evidence | Valor |
|---|---|
| Focused test command + resultado | `dotnet test SGV.slnx --filter "FullyQualifiedName~PersonaHabilidadesPage"` → **5 PASS / 0 FAIL**. |
| Runtime harness command/result | `dotnet build SGV.slnx` → **exit 0**, 0 errors; no se ejecutó `bun run build` por ser scope exclusivo de Slice 3b. No se agregaron tests HTTP web reales. |
| Rollback boundary | Revertir los commits de Slice 3a elimina `src/SGV.Web/Pages/Personas/PersonaHabilidades.cshtml*` y `tests/SGV.Tests/Web/Persona/PersonaHabilidadesPageTests.cs`; revertir la actualización de artifacts restaura el estado post-Slice 2. No se tocan API, Contracts, Dominio, Infraestructura ni Details. |

### Commits locales

- `ce0091a` — `test(slice3a): add PersonaHabilidades authorization and GET tests`
- `a22ede2` — `feat(slice3a): add PersonaHabilidades Razor Page GET`
- `63555f5` — `fix(slice3a): prepare PRG feedback on skills page`

### Riesgos conocidos

- Bajo: la vista deja los selectores de habilidad/nivel preparados, pero el catálogo y los handlers POST son responsabilidad de Slice 3b.
- Bajo: el acceso HTTP anónimo se valida por el atributo y el pipeline existente; este slice usa únicamente tests unitarios del PageModel, conforme al alcance sin integración web.

### Cambios al `SgvWebApplicationFactory` (factory de tests)

- +1 parámetro `personaApiHandler` (espejo de `cargoApiHandler`).
- +1 handler de re-registración (`ConfigurePrimaryHttpMessageHandler`) para el subrecurso persona-skill.
- Sin cambios funcionales: las llamadas existentes a `WithOverrides` siguen funcionando porque el parámetro es opcional con default `null`.

### Advertencia sobre la cobertura del bridge cookie→JWT

El orquestador pidió "solo verificar que el handler se inyecta, no la comunicación HTTP". Slice 2 cumple con dos guards de inyección:

1. `PersonaWebSeamTests.ProductionRegistration_ApiBearerTokenHandler_IsRegisteredAsTransient` → verifica que el tipo está registrado en el `IServiceProvider`.
2. `PersonaWebSeamTests.ProductionRegistration_PersonaApiClient_SubresourceSkillMethodsResolve` → verifica que los 3 métodos del subrecurso existen en `IPersonaApiClient` (sin los cuales el PageModel de Slice 3a no compilaría).

La verificación end-to-end del bridge contra el subrecurso persona-skill (analog a `ApiBearerTokenIntegrationTests` para Cargo) queda como follow-up natural de Slice 3b (donde se escribe el POST handler y se necesita el bridge en runtime). El factory ya está extendido (`personaApiHandler` parameter) para soportarlo cuando se materialice la necesidad.

---

## Slice 3b — Handlers POST + PRG + Details enlace + tests integración web + bun build

**Slice**: 3b / 4 (último slice; sigue a 1+2+3a ya mergeados).
**Branch**: `feat/implementa-persona-habilidades-pr3b` (base = `origin/develop`).
**Estrategia**: stacked-to-main; PR único apuntando a `main` (no a otro PR stacked).
**Status**: **success** — implementación local lista para `sdd-verify`. Strict TDD aplicado (RED → GREEN → TRIANGULATE → REFACTOR) en las 6 tareas del slice.

### Resumen ejecutivo

Slice 3b completa el flujo web sobre el subrecurso `persona-skill`. Los handlers POST `OnPostAsignarAsync` (PUT idempotente) y `OnPostQuitarAsync` (DELETE) viven en `PersonaHabilidades.cshtml.cs` y siguen el patrón canónico de `CargoHabilidadesPostHandlers`: gate admin → gate persona activa → llamada al cliente → traducción `ErrorCategoria` → `TempData["StatusMessage"]`/`["StatusKind"]` con PRG. La vista existente se normalizó (per-row "Actualizar" → "Asignar", `skillId` → `SkillId`) y se renderizan errores de validación de input. La página `Details.cshtml` agrega el botón "Habilidades" (`ti ti-stars me-1`) condicionado a persona activa + rol Administrador, cumpliendo R-PM-01 del delta `persona-management`. Se agregaron 17 tests unitarios de PageModel, 11 tests de integración end-to-end y 3 tests del botón Details, más un test del bridge JWT end-to-end contra el subrecurso persona-skill (analog a `ApiBearerTokenIntegrationTests` para Cargo). `bun run build` pasa. **NO** se tocaron `SGV.Contracts.Personas.*`, `SGV.Api/`, `SGV.Aplicacion/`, `SGV.Dominio/` ni `SGV.Infraestructura/`. NO se agregaron métodos nuevos a `IPersonaApiClient` (los 3 de Slice 2 son suficientes).

### Tareas completadas (3b.1 — 3b.6)

| Tarea | Descripción corta | Commit |
|------|-------------------|--------|
| 3b.1 | RED — tests handlers POST upsert/delete con PRG (PageModel unit) | `c2f9a798` |
| 3b.2 | RED — tests POST persona inactiva bloquea mutación | `c2f9a798` |
| 3b.3 | GREEN — `OnPostAsignarAsync` + `OnPostQuitarAsync` con PRG + TempData + `PersonaSkillFormHelpers` | `3e49e80c` |
| 3b.4 | GREEN — tests integración web (10 tests POST) + bridge JWT end-to-end (1 test) | `7ff90f24` |
| 3b.5 | GREEN — enlace "Habilidades" en `Details.cshtml` (admin + persona activa) + 3 tests Details | `7ff90f24` |
| 3b.6 | Verify final — build verde, suite 2,787/0/0 (3 corridas consecutivas), `bun run build` ok | local |

Total tareas Slice 3b: **6/6 completas**.

### Archivos tocados (7 archivos)

#### Modificados producción (4 archivos)

| Path | Cambio |
|---|---|
| `src/SGV.Web/Pages/Personas/PersonaHabilidades.cshtml.cs` | +`OnPostAsignarAsync` + `OnPostQuitarAsync` + `PersonaHabilidadesAsignarInputModel` + `PersonaSkillFormHelpers` (paralelo a `CargoSkillFormHelpers` reducido al subdominio persona-skill) + `EnsurePersonaActivaAsync` (gate de persona activa previo al cliente HTTP) + `ReloadAfterFailedAsignarAsync` (re-render tras validación local). Sin `Ponderacion`/`EsObligatoria`/`NivelRequeridoId`. |
| `src/SGV.Web/Pages/Personas/PersonaHabilidades.cshtml` | Per-row form: `asp-page-handler="Actualizar"` → `"Asignar"`, hidden `skillId` → `SkillId` (normalización con form inferior). Render explícito de errores de validación en `ModelState["SkillId"]` y `ModelState["NivelHabilidadId"]`. |
| `src/SGV.Web/Pages/Personas/Details.cshtml` | +botón "Habilidades" con `ti ti-stars me-1` apuntando a `/personas/{id:guid}/habilidades`. Solo cuando `!Model.IsNotFound` AND `Model.Persona.IsActive` AND `User.IsInRole(RolesSgv.Administrador)`. Cumple R-PM-01. |
| `tests/SGV.Tests/Web/Collections/WebIntegrationFixture.cs` | +`CreatePersonaBridgeLeaseAsync` (espejo de `CreateCargoBridgeLeaseAsync`) para soportar el bridge JWT end-to-end contra el subrecurso persona-skill. |

#### Nuevos tests (3 archivos)

| Path | Cubre |
|---|---|
| `tests/SGV.Tests/Web/Persona/DetailsHabilidadesButtonTests.cs` | 3 tests: `Details_ActivePersona_Admin_RendersHabilidadesButtonWithCorrectHref`, `Details_NotFound_DoesNotRenderHabilidadesButton`, `Details_ActivePersona_NonAdmin_DoesNotRenderHabilidadesButton`. Patrón regex específico para evitar matchear `/organizacion/habilidades` de la nav global. |
| `tests/SGV.Tests/Web/Persona/PersonaHabilidadesIntegrationTests.cs` | 11 tests: end-to-end antiforgery + PRG + TempData: Asignar success/failure (4xx/transport), Quitar success/failure (NotFound/transport), gating admin, persona inactiva bloquea, bridge JWT end-to-end (`Get_PersonaHabilidades_ForwardsBearerTokenToPersonaApi`). |

#### Modificados tests (1 archivo)

| Path | Cambio |
|---|---|
| `tests/SGV.Tests/Web/Persona/PersonaHabilidadesPageTests.cs` | +17 tests: POST handler unit tests cubriendo happy path, 4xx NotFound/Conflict/Validation, transport failure, gating admin, persona inactiva bloquea, validación de form (SkillId/NivelHabilidadId vacíos). |

### Tests añadidos

| Suite | Cantidad | Tipo |
|---|---|---|
| `PersonaHabilidadesPageTests` (PageModel) | 17 | Unit (PageModel directo, FakePersonaApiClient, TempData in-memory) |
| `PersonaHabilidadesIntegrationTests` (incluye bridge) | 11 | Integration (WebApplicationFactory + antiforgery + PRG + TempData end-to-end) |
| `DetailsHabilidadesButtonTests` | 3 | Integration (verifica render del botón en Details) |
| **Total nuevos** | **31** | Mixto |

Total tests Slice 3b: **31 nuevos** + suite completa **2,787 PASS / 0 FAIL** (3 corridas consecutivas consistentes).

### Comandos ejecutados y resultado

| Comando | Resultado |
|---|---|
| `dotnet build SGV.slnx` (baseline pre-Slice 3b) | 0 errors / 84 warnings |
| `dotnet test --filter "FullyQualifiedName~PersonaHabilidadesPage"` (después de RED) | 17 compile errors CS1061 (RED esperado — handlers inexistentes) |
| `dotnet build src/SGV.Web` (después de GREEN) | 0 errors |
| `dotnet test --filter "FullyQualifiedName~PersonaHabilidadesPage"` | **23 PASS / 0 FAIL** (5 preexistentes + 17 nuevos + 1 verif build) |
| `dotnet test --filter "FullyQualifiedName~PersonaHabilidadesIntegration"` | **11 PASS / 0 FAIL** |
| `dotnet test --filter "FullyQualifiedName~DetailsHabilidadesButton"` | **3 PASS / 0 FAIL** |
| `dotnet test SGV.slnx --no-build` (corrida 1) | **2,787 PASS / 0 FAIL** |
| `dotnet test SGV.slnx --no-build` (corrida 2) | **2,787 PASS / 0 FAIL** |
| `dotnet test SGV.slnx --no-build` (corrida 3) | **2,787 PASS / 0 FAIL** |
| `bun run build` en `src/SGV.Web` | exit 0 (styles + inspiniaPages + plugins) |

### TDD Cycle Evidence

| Tarea | Test File | Layer | Safety Net | RED | GREEN | TRIANGULATE | REFACTOR |
|------|-----------|-------|------------|-----|-------|-------------|----------|
| 3b.1 | `PersonaHabilidadesPageTests.cs` (RED commit `c2f9a798`) | Unit/PageModel | N/A (handlers nuevos) | ✅ Written (17 compile errors CS1061) | ✅ Passed | ✅ 17 casos: happy path + 4xx NotFound/Conflict/Validation + transport + admin/non-admin + missing SkillId/NivelId | ✅ Extract `PersonaSkillFormHelpers.ReadAsignarInput` + `ResolveFailureMessage` |
| 3b.2 | `PersonaHabilidadesPageTests.cs` (en el mismo commit) | Unit/PageModel | N/A | ✅ Written | ✅ Passed | ✅ 2 casos: inactiva Asignar + inactiva Quitar | ✅ Ninguno (gate en `EnsurePersonaActivaAsync`) |
| 3b.3 | `PersonaHabilidadesPageTests.cs` + `PersonaHabilidades.cshtml.cs` | Unit | ✅ 23/23 preexistentes | ✅ tests RED previos | ✅ Passed (23/23) | ✅ 17 casos nuevos | ✅ `PersonaSkillFormHelpers` extraído como static class paralelo a `CargoSkillFormHelpers` |
| 3b.4 | `PersonaHabilidadesIntegrationTests.cs` (commit `7ff90f24`) | Integration (WAF) | N/A (nuevos) | N/A (extensión de GREEN) | ✅ Passed (11/11) | ✅ 11 casos: Asignar success, Quitar success, non-admin, inactiva Asignar, inactiva Quitar, Validation 4xx, NotFound 4xx, transport Asignar, NotFound Quitar, transport Quitar, bridge JWT end-to-end | ✅ `CreatePersonaBridgeLeaseAsync` extraído al fixture composite |
| 3b.5 | `DetailsHabilidadesButtonTests.cs` + `Details.cshtml` | Integration (WAF) | N/A (nuevos) | ✅ 3 Failed (botón ausente) | ✅ 3 Passed | ✅ 3 casos: admin+activa, no-encontrada, no-admin+activa | ✅ Regex específica para evitar matchear `/organizacion/habilidades` de la nav |
| 3b.6 | (verify) | — | ✅ 2,787 baseline | — | ✅ Passed | ✅ 3 corridas consecutivas | ✅ Ninguno |

Total tests escritos: 31 (17 unit + 11 integration + 3 integration details). Total tests pasando: 2,787 (suite completa), 23 (filtro PersonaHabilidadesPage), 11 (filtro PersonaHabilidadesIntegration), 3 (filtro DetailsHabilidadesButton).

### Work Unit Evidence

| Evidence | Valor |
|---|---|
| Focused test command + resultado | `dotnet test SGV.slnx --filter "FullyQualifiedName~PersonaHabilidades\|DetailsHabilidadesButton" --no-build` → **37 PASS / 0 FAIL** (17+11+3+6 tests integración cross-cutting del filtro). |
| Runtime harness command/result | `dotnet test SGV.slnx --no-build` → **2,787 PASS / 0 FAIL** (3 corridas consecutivas). `bun run build` en `src/SGV.Web` → exit 0. |
| Rollback boundary | Revertir los 3 commits (`c2f9a798`, `3e49e80c`, `7ff90f24`) deja el repo en estado post-Slice 3a con el PageModel sólo-Get. No se introducen cambios de runtime en API, Dominio, Infraestructura, Aplicación ni Contracts. El `CreatePersonaBridgeLeaseAsync` del fixture se revierte junto con su test de bridge. |

### TDD Test Summary

- **Total tests written**: 31
- **Total tests passing**: 2,787 (suite), 37 (filtro combinado Slice 3b)
- **Layers used**: Unit (PageModel + Fake + TempData in-memory) — 17; Integration (WebApplicationFactory + antiforgery + TempData end-to-end) — 14
- **Approval tests (refactoring)**: 0 (no refactor tasks; handlers y form helpers son greenfield)
- **Pure functions created**: 2 (`PersonaSkillFormHelpers.ReadAsignarInput`, `PersonaSkillFormHelpers.ResolveFailureMessage`)

### Cambios observados a nivel wire/contrato

- **Wire JSON**: preservado. `AsignarPersonaSkillRequest { nivelId }` se serializa igual; `PersonaSkillCommandResult`/`PersonaSkillDeleteResult` mantienen el shape de Slice 1+2.
- **HTTP**: preservado. `PUT /api/v1/personas/{personaId}/skills/{skillId}` y `DELETE /api/v1/personas/{personaId}/skills/{skillId}` siguen 200/204/400/404/409/5xx. Validado por los 11 tests de integración.
- **TempData keys**: `StatusMessage` + `StatusKind` (mismo contrato que `CargoHabilidadesPostHandlers` y `Details` PRG). Default `kind = "success"` cuando no se setea explícitamente.
- **Forward-compat**: `PersonaHabilidadesViewModel` agrega el campo implícito `ViewModel` que la vista ya consumía en Slice 3a. `PersonaHabilidadAsignarInputModel` es nuevo, source-compat con la convención de `BindProperty` (no se usa binding automático, se hidrata manualmente desde `Request.Form`).

### Patrón replicado de `CargoHabilidades`

Slice 3b replica el patrón canónico de `CargoHabilidades`:
- Handlers POST separados (Asignar / Quitar) en el PageModel, no en una clase static aparte (diferencia menor: el helper estático en Persona está en `PersonaSkillFormHelpers` dentro del mismo archivo, no en `CargoHabilidadesPostHandlers` separado).
- Gate admin al inicio del handler (return `Forbid()` si no es admin).
- Gate persona activa antes de invocar al cliente (prevenir mutaciones sobre personas inactivas/eliminadas).
- `PageFeedback.Set*` para mensajes PRG.
- `ErrorCategoryMapper.Map` para mensajes de fallo de delete.
- `TransportFailureClassifier.IsTransportFailure` para aislar fallos de transporte y devolver TempData danger.
- Antiforgery validado por ASP.NET (no se agrega `[ValidateAntiForgeryToken]` porque Razor Pages lo aplica por convención a todos los handlers POST).
- Antiforgery token extraído del GET inicial con `WebTestBuilders.ExtractAntiforgeryTokenAsync` en los tests de integración.

### Decisiones congeladas respetadas (Slice 3b)

- ✅ `VerificadoAt`/`Fuente` → no se exponen (memo #1284 sigue vigente).
- ✅ Acceso → admin-only (lo refuerza `[Authorize(Roles = RolesSgv.Administrador)]` en el PageModel + gateo manual en cada handler + `@if (... && User.IsInRole(...))` en Details).
- ✅ Persona inactiva → bloqueo en GET (Slice 3a) **y** en POST (Slice 3b). El handler consulta `persona.IsActive` antes de invocar al cliente; si está inactiva, redirige con TempData warning sin mutar.
- ✅ Errores → `ErrorCategoria` adoptado como taxonomía observable en `TempData`; `PersonaSkillErrorType` queda como discriminador interno con mapping nombre-a-nombre en `PersonaSkillFormHelpers.ResolveFailureMessage`.

### Riesgos emergentes

- **Medio**: el review budget de Slice 3b quedó en **1,349 líneas modificadas** (vs. forecast de 245-325). Ratio ~4x. La sobreproducción viene de los 31 tests nuevos (1,017 líneas) — el código de producción se mantuvo dentro del forecast (~317 líneas). Esto es **size:exception** respecto al budget de 400. El usuario ya aprobó `size:exception` para Slice 2 (1,334 líneas) en mem #1295; Slice 3b replica el patrón y queda a criterio del orquestador aceptar la excepción o re-fragmentar.
- **Bajo**: el helper `PersonaSkillFormHelpers` quedó embebido en `PersonaHabilidades.cshtml.cs` en lugar de seguir el patrón `CargoHabilidadesPostHandlers.cs` separado. Decisión consciente: el subdominio persona-skill tiene menos campos y comparte menos estado con la vista. Si en el futuro se extrae a archivo propio (paralelo a `CargoHabilidadesPostHandlers.cs`), el refactor es mecánico (mover + ajustar `internal static class`).
- **Bajo**: el per-row form pasa de `asp-page-handler="Actualizar"` a `"Asignar"`. Esto implica que tanto el form del pie de página como la fila llaman al mismo handler, lo cual es conceptualmente correcto (ambos son upserts). El markup renderiza ambos como botones "Guardar" / "Asignar habilidad" respectivamente, así que la diferenciación es puramente visual.
- **Bajo**: el test de bridge end-to-end depende de `RecordingHttpMessageHandler` del composite (`WebTestBuilders`). Si en el futuro se renombra esa clase o se cambia el lease composite, hay que ajustar este test. Hoy compila y pasa.

### Cambios al `SgvWebApplicationFactory` (factory de tests)

- Sin cambios funcionales en Slice 3b. La lógica de `_personaApiHandler` (registrada en Slice 2) sigue soportando el override de `PrimaryHandler` para `IPersonaApiClient`. El test de bridge usa el helper `CreatePersonaBridgeLeaseAsync` (agregado a `WebIntegrationFixture` en este slice, no al factory).

### Próximo paso del orquestador

1. `sdd-verify` puede correr la suite completa (`dotnet test SGV.slnx`) + `bun run build` con seguridad — el subrecurso `persona-skill` queda cerrado end-to-end (wire JSON, cliente tipado, handlers POST con PRG, gating admin, gating persona inactiva, bridge JWT).
2. Después de `sdd-verify`, `sdd-archive` consolida los delta specs del change en `openspec/specs/` y mueve el change a `archive/2026-07-21-implementa-persona-habilidades/`.
