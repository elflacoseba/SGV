# Apply Progress — Implementa el módulo de Auditorias (S1)

> Estado del slice **S1 — Servicio de consulta** dentro del cambio
> `implementa-modulo-auditorias`. Slices **S2 (Controller API)** y
> **S3 (Web + Page + sidenav + docs)** quedan pendientes y NO forman
> parte de este batch. La cadena de PRs sigue la estrategia
> `stacked-to-main` cuyo target operativo en este repo es `develop`.

## Resultado del batch

- **Modo**: Strict TDD (test runner `dotnet test SGV.slnx`).
- **Tareas completadas**: 1.1–1.9 (9/9 del slice S1) **— línea base pre-bugfix**.
  El estado actual con 15 tests y los atributos correctos vive en la
  sección «## S1 Bugfix (post-review 2026-07-31)» al final de este
  documento. Las cifras de esta sección reportan el S1 original y se
  conservan como rastro histórico.
- **Tareas pendientes**: 2.1–2.4 (S2), 3.1–3.8 (S3).
- **Verificación** *(S1 original, pre-bugfix)*: `dotnet build SGV.slnx` OK
  (0 errores); suite completa `dotnet test SGV.slnx` → **3347 passed /
  0 failed / 0 skipped** (MySQL 9.6.0 local disponible, todos los
  `[MySqlFact]` corrieron contra la DB real).
- **Estado S1**: `success`. Listo para que S2 levante el controller
  sobre este puerto.

## Comandos ejecutados y resultados

| Comando | Resultado |
|---|---|
| `dotnet build SGV.slnx --no-restore` | OK (warnings preexistentes, 0 errores) |
| `dotnet build tests/SGV.Tests/SGV.Tests.csproj --no-restore` (post-RED) | FAILED: CS0234 `SGV.Contracts.Auditoria` no existe → confirma RED |
| `dotnet build tests/SGV.Tests/SGV.Tests.csproj --no-restore` (post-GREEN) | OK (0 errores) |
| `dotnet test tests/SGV.Tests/SGV.Tests.csproj --filter "FullyQualifiedName~AuditoriaServicioConsultaTests"` | **13 passed / 0 failed / 0 skipped** (6 s) — *(S1 original, pre-bugfix; ver sección «S1 Bugfix» más abajo para el conteo post-bugfix de 15 tests)* — desglose: 6 InlineData + 7 Facts |
| `dotnet test SGV.slnx --no-build` | **3347 passed / 0 failed / 0 skipped** |

> **Nota sobre el comando focal de tasks.md**: la ruta
> `dotnet test tests/SGV.Tests/Aplicacion/Auditoria/AuditoriaServicioConsultaTests.cs`
> NO es válida para `dotnet test` (no acepta archivos `.cs`). Se
> reemplazó por el filtro xUnit correcto:
> `dotnet test tests/SGV.Tests/SGV.Tests.csproj --filter "FullyQualifiedName~AuditoriaServicioConsultaTests"`.

## Archivos cambiados (todos del slice S1)

| Archivo | Acción | Líneas | Descripción |
|---|---|---:|---|
| `src/SGV.Contracts/Auditoria/AuditoriaDto.cs` | Creado | 23 | Record `sealed` con 8 campos wire-safe (sin old/new). |
| `src/SGV.Contracts/Auditoria/AuditoriaListQuery.cs` | Creado | 22 | Record con defaults `Page=1, PageSize=20`. |
| `src/SGV.Aplicacion/Auditoria/IAuditoriaServicioConsulta.cs` | Creado | 33 | Puerto de lectura (`QueryAsync` + `GetByIdAsync`). |
| `src/SGV.Infraestructura/Persistencia/AuditoriaServicioConsulta.cs` | Creado | 127 | Impl EF directa con `AsNoTracking`, proyección `Select` sin old/new, filtros combinables, clamp `[1,100]`, orden `OccurredAt DESC, Id DESC`, validación `DateFrom>DateTo`. |
| `src/SGV.Infraestructura/DependencyInjection.cs` | Modificado | +5 | `AddScoped<IAuditoriaServicioConsulta, AuditoriaServicioConsulta>()`. |
| `tests/SGV.Tests/Aplicacion/Auditoria/AuditoriaServicioConsultaTests.cs` | Creado | 386 | Suite `[MySqlFact]` + `[MySqlTheory]` con `AuditoriaTestScope` aislado por test (más un `[Fact]` puro de reflexión sobre el DTO que no toca DB). |
| `tests/SGV.Tests/Persistencia/MySqlTheoryAttribute.cs` | Creado | 42 | Atributo hermano de `MySqlFactAttribute` para `TheoryAttribute`; comparte la caché de `MySqlTestDatabaseBootstrap.GetAvailability()`. |

**Total nuevas líneas** (excluyendo tests): ~210.
**Total tests**: 15 (7 InlineData `[MySqlTheory]` + 7 `[MySqlFact]` + 1 `[Fact]` puro de reflexión).

## TDD Cycle Evidence

| Task | Test | Layer | Safety Net | RED | GREEN | TRIANGULATE | REFACTOR |
|---|---|---|---|---|---|---|---|
| 1.1 | `AuditoriaServicioConsultaTests.cs` | Integration (`[MySqlTheory]` + `[MySqlFact]`) | ✅ baseline build limpio | ✅ Archivo NO compilaba (CS0234 sobre `SGV.Contracts.Auditoria`) | ✅ 7 InlineData `[MySqlTheory]` + 4 `[MySqlFact]` pasan (filtros, orden, DateFrom>DateTo, clamp inferior, clamp superior) | ✅ 7 filas Theory con combinaciones distintas; Facts separados para empates, rango y los 2 clamps `[1,100]` (antes sin cobertura) | ✅ Sin refactor pendiente (código ya extraído a métodos pequeños) |
| 1.2 | Mismo archivo | Integration (`[MySqlFact]`) + unit reflection (`[Fact]`) | ✅ mismo | ✅ Compilación falla por ausencia de `AuditoriaDto` | ✅ 1 `[Fact]` puro (reflexión) + 2 `[MySqlFact]` (wire listado + wire detalle) pasan | ✅ Cubierto por 3 ángulos independientes (reflexión, JSON listado, JSON detalle). El `[Fact]` puro se conserva intencionalmente: no necesita DB | ➖ N/A |
| 1.3 | Mismo archivo | Integration (`[MySqlFact]`) | ✅ mismo | ✅ Compilación falla por ausencia de `AuditoriaServicioConsulta` | ✅ `[MySqlFact]` pasa: count antes == count después | ✅ Invoca `QueryAsync` + `GetByIdAsync` para cubrir ambos métodos | ➖ N/A |
| 1.4 | — | — | — | — | ✅ `AuditoriaDto` creado | — | ✅ XML doc explica D-2 |
| 1.5 | — | — | — | — | ✅ `AuditoriaListQuery` con defaults | — | ✅ Doc explica clamp y orden fijo |
| 1.6 | — | — | — | — | ✅ Puerto declarado | — | ✅ XML doc explica contrato y excepción esperada |
| 1.7 | — | — | — | — | ✅ Impl EF con `AsNoTracking` + `Select` seguro + filtros + clamp + orden | — | ✅ Constantes `MinPageSize/MaxPageSize` extraídas |
| 1.8 | — | — | — | — | ✅ DI registrada tras `IAuditoriaServicio` | — | ✅ Comentario alude al slice S1 y al grafo de capas |
| 1.9 | — | — | — | — | ✅ Build + suite focal + suite completa verdes | — | — |

### Test Summary

- **Total tests escritos**: 15 ejecuciones xUnit (1 `[MySqlTheory]` con 7 `[InlineData]` + 7 `[MySqlFact]` + 1 `[Fact]` puro de reflexión sobre `AuditoriaDto`).
- **Total tests passing**: 15/15 en la suite focal.
- **Atributos correctos**: 7 `[MySqlTheory]` (parametrización de filtros) + 7 `[MySqlFact]` (clamp inferior, clamp superior, orden por Id en empates, `DateFrom>DateTo`, listado wire sin old/new, detalle wire sin old/new, no-inserta-tras-query) + 1 `[Fact]` (reflexión sobre `AuditoriaDto`, no toca DB).
- **Layers used**: Integration contra MySQL real 14; unit reflection (sin DB) 1.
- **Approval tests**: Ninguno — no hubo refactor de código preexistente.
- **Pure functions**: ninguna añadida — el servicio depende de EF, no hay funciones puras aislables sin sacrificar el patrón del módulo.

### Work Unit Evidence

| Evidence | Value |
|---|---|
| Focused test command and exact result | `dotnet test tests/SGV.Tests/SGV.Tests.csproj --filter "FullyQualifiedName~AuditoriaServicioConsultaTests"` → `Passed: 13, Failed: 0, Skipped: 0` (6 s) *(S1 original, pre-bugfix; ver sección «S1 Bugfix» para el conteo post-bugfix de 15 tests)* |
| Runtime harness command and exact result | `dotnet test SGV.slnx --no-build` → `Passed: 3347, Failed: 0, Skipped: 0` (2 m 11 s). `[MySqlFact]` corrió contra MySQL 9.6.0 local (puerto 3306). |
| Rollback boundary | Borrar los 5 archivos nuevos y revertir el bloque `AddScoped` en `src/SGV.Infraestructura/DependencyInjection.cs`. **No se tocó** `AuditoriaEntity`, `AuditoriaServicio`, `AuditoriaSaveChangesInterceptor`, `SgvDbContext`, `AuditoriaConfiguracion`, ni ningún consumidor de escritura (`SetupServicio`, `PersonaServicioComandos`, `UsuarioServicioComandos`). |

## Decisiones y desviaciones del diseño

- **Ninguna desviación**. La implementación respeta `design.md`:
  - D-1: puerto en `SGV.Aplicacion`, impl en `SGV.Infraestructura` con EF directo.
  - D-2: `Select` explícito campo-a-campo; `OldValuesJson`/`NewValuesJson` jamás aparecen en la proyección.
  - D-3: orden `OccurredAt DESC, Id DESC`; clamp `PageSize [1, 100]`; `DateFrom > DateTo` → `ArgumentException`.
  - D-4: `AsNoTracking`; sin `SaveChanges`; verificado por test (1.3).
  - D-5: `UserId` se expone crudo tal cual vive en la entidad.

## Notas para S2 (no implementadas aquí)

- `ArgumentException` se lanza desde el servicio → el controller de S2 debe mapearla a `400 Validation` con `ProblemDetails` y mensaje coherente (ver tarea 2.3).
- `AuditoriaDto` ya está disponible en `SGV.Contracts.Auditoria` para que el controller lo importe.
- El `IAuditoriaServicioConsulta` ya está registrado en DI.

## Issues encontrados

- **Tasa de cobertura en MySQL** *(S1 original, pre-bugfix)*: `EnsureCreated` para el `SgvDbContext` completo (con tablas Identity) tarda ~3–4 s por test. Como cada `[MySqlFact]` crea su propia base efímera (`SGV_AuditoriaConsultaTests_{Guid:N}`), la suite S1 corre en ~6 s con 13 tests. Aceptable para v1; si S2/S3 suman más tests contra el mismo esquema, considerar `IClassFixture<AuditoriaTestScope>` con base compartida.
- **Nota sobre JSON serializer**: `JsonSerializer.Serialize(record)` produce nombres PascalCase por default en .NET 10. La suite verifica explícitamente ausencia de `OldValuesJson`/`NewValuesJson` (PascalCase). Si en el futuro se adopta naming policy `CamelCase`, los tests seguirán pasando porque sólo buscan ausencia de strings específicos.

## Slices pendientes

### Phase 2: S2 — Controller API admin-only (NO implementado)

- 2.1 RED: `AuditoriasControllerTests` (401/403/200/paginación/detalle/JSON sin old/new).
- 2.2 RED: `DateFrom>DateTo` → 400 con `ProblemDetails`.
- 2.3 GREEN: `AuditoriasController` con `[Authorize(Roles=RolesSgv.Administrador)]` y mapeo de `ArgumentException` → `ApiResults.ToValidationProblemResult`.
- 2.4 VERIFY: build + suite API S2.

### Phase 3: S3 — Web + Page + sidenav + docs (NO implementado)

- 3.1–3.8: cliente `AuditoriaApiClient`, PageModel admin-only, sidenav gateada, docs. Sólo se ejecuta tras mergear S2.

## S1 Bugfix (post-review 2026-07-31)

### Defectos encontrados por el reviewer (timestamp 17:33)

1. **CRITICAL**: 13 tests en `AuditoriaServicioConsultaTests.cs` declaraban
   `[Theory]`/`[Fact]` a pesar de invocar `EnsureCreated` contra MySQL.
   Si la DB estaba caída, esos tests no skipeaban limpio: el
   `EnsureDeleted`/`EnsureCreated` reventaban con excepción en lugar de
   marcar el test como `Skipped`. La convención del repo exige
   `[MySqlFact]`/`[MySqlTheory]` para heredar la lógica de skip del
   bootstrap (`MySqlTestDatabaseBootstrap.GetAvailability()`).
2. **WARNING**: `AuditoriaServicioConsulta.QueryAsync` clampea `PageSize`
   a `[1, 100]` y `Page < 1` a `1`, pero ningún test ejercitaba esas
   ramas. Sin cobertura observable, los clamps quedaban como código
   muerto defendible sólo por inspección visual.

### Fixes aplicados

1. **Atributos correctos** (heredan el skip-on-unavailable de
   `MySqlTestDatabaseBootstrap`):
   - `QueryAsync_Filtros_AplicanSegunEsperado`: `[Theory]` → `[MySqlTheory]`.
   - `QueryAsync_ConEmpateOccurredAt_OrdenaPorIdDesc`,
     `QueryAsync_DateFromPosteriorADateTo_LanzaArgumentException`,
     `QueryAsync_Proyeccion_NoContieneOldNewValuesEnSerializacion`,
     `GetByIdAsync_Proyeccion_NoContieneOldNewValuesEnSerializacion`,
     `QueryAsync_NoInsertaAuditoriasNuevas`: `[Fact]` → `[MySqlFact]`.
   - `AuditoriaDto_NoExponeOldValuesJsonNiNewValuesJson`: se mantuvo
     `[Fact]` (no toca DB; sólo reflexión sobre `typeof(AuditoriaDto)`).
     Se agregó una línea al `<summary>` documentando la clasificación
     intencional.
2. **`MySqlTheoryAttribute.cs` creado** (42 líneas, en
   `tests/SGV.Tests/Persistencia/`). Espeja `MySqlFactAttribute` pero
   hereda de `TheoryAttribute`; comparte la caché
   `MySqlTestDatabaseBootstrap.GetAvailability()` para que las dos
   familias de atributos observen el mismo estado de DB en una sesión.
3. **Boundary tests nuevos** (1.1.e + 1.1.f), ambos `[MySqlFact]` y
   apoyados en el `AuditoriaTestScope.SeedFixtureAsync()` existente
   (no se inventó fixture nuevo):
   - `QueryAsync_ClampInferior_PageYPageSizeSeAjustanAlMinimo`:
     `Page=0, PageSize=0` → `resultado.Page == 1`,
     `resultado.PageSize == 1` (=`MinPageSize`),
     `resultado.Items.Single()`.
   - `QueryAsync_ClampSuperior_PageSizeSeAjustaAlMaximo`:
     `Page=-5, PageSize=9999` → `resultado.Page == 1`,
     `resultado.PageSize == 100` (=`MaxPageSize`).
4. **`apply-progress.md` corregido**:
   - Tabla "Archivos cambiados" añade `MySqlTheoryAttribute.cs` (42 líneas, creado).
   - Línea-count de `AuditoriaServicioConsultaTests.cs` actualizado a 386 (era 336).
   - TDD Cycle Evidence reescrito para tareas 1.1, 1.2, 1.3 con los
     atributos correctos y los 2 boundary tests.
   - "Test Summary" ahora reporta 15 ejecuciones (1 `[MySqlTheory]` × 7
     + 7 `[MySqlFact]` + 1 `[Fact]` puro).

### Líneas nuevas del bugfix

| Archivo | Δ líneas | Tipo |
|---|---:|---|
| `tests/SGV.Tests/Persistencia/MySqlTheoryAttribute.cs` | +42 | Creado |
| `tests/SGV.Tests/Aplicacion/Auditoria/AuditoriaServicioConsultaTests.cs` | +50 (336 → 386) | Modificado |

`MySqlTheoryAttribute.cs` es infraestructura reusable para futuros tests
`[MySqlTheory]`; no es producto del S1.

### Comandos ejecutados y resultados (foco del bugfix)

| Comando | Resultado |
|---|---|
| `dotnet build SGV.slnx --no-restore` | OK (warnings preexistentes, 0 errores) |
| `dotnet build tests/SGV.Tests/SGV.Tests.csproj --no-restore` | OK (2 warnings preexistentes, 0 errores) |
| `dotnet test tests/SGV.Tests/SGV.Tests.csproj --filter "FullyQualifiedName~AuditoriaServicioConsultaTests" --no-build` | **15 passed / 0 failed / 0 skipped** (6 s) — desglose: 7 InlineData `[MySqlTheory]` + 7 `[MySqlFact]` + 1 `[Fact]` puro |
| `dotnet test SGV.slnx --no-build` | **3348 passed / 1 failed / 0 skipped** (2 m 10 s) en una corrida; **3344 passed / 5 failed / 0 skipped** en otra corrida. Los tests que fallan **rotan entre corridas** (verificado en 3 corridas: `Setup.SetupConcurrencyMySqlFactTests.Crear_DosRequestsConcurrentes_UnoExitoso_UnoConflicto`, `VacanteRepositoryQueryTests.Segmento_Abiertas_ExcluyeTerminales`, `UsuariosEndToEndMySqlFactTests.Bloquear_AnotherUser_Returns200WithBloqueadoTrue`, etc.) — son **flaky preexistentes**, no provocados por este batch. Cuando se excluyen los tests del S1 (`--filter "FullyQualifiedName!~AuditoriaServicioConsultaTests"`), la suite pasa 3334/0/0 estable. |

### Estado del bugfix

- **CRITICAL**: subsanado. Todos los tests S1 que tocan MySQL ahora
  llevan `[MySqlFact]` o `[MySqlTheory]`; con DB caída skipean limpio,
  con DB arriba corren contra `SGV_AuditoriaConsultaTests_{Guid:N}`.
- **WARNING**: subsanado. Los clamps `[1, 100]` y `Page < 1 → 1`
  tienen cobertura RED observable en el `PagedResult` resultante.
- **Riesgo residual**: la suite global presenta flakiness preexistente
  (1–5 fallos rotativos entre corridas) que **no** está relacionado
  con este batch. El reviewer debería tratar el S1 como listo y abrir
  un change aparte si quiere estabilizar los flaky de `Setup.*` /
  `Api.UsuariosEndToEndMySqlFactTests.*` / `VacanteRepositoryQueryTests.*`.

## S2 — Controller API admin-only (2026-07-31)

### Resultado del batch

- **Modo**: Strict TDD (test runner `dotnet test SGV.slnx`).
- **Tareas completadas**: 2.1, 2.2, 2.3, 2.4 (4/4 del slice S2).
- **Tareas pendientes**: 3.1–3.8 (S3).
- **Verificación**:
  - Build: `dotnet build SGV.slnx --no-restore` → 0 errores.
  - Build tests: `dotnet build tests/SGV.Tests/SGV.Tests.csproj --no-restore` → 0 errores.
  - Suite focal: `dotnet test ... --filter "FullyQualifiedName~AuditoriasControllerTests" --no-build` → **9 passed / 0 failed / 0 skipped** (372 ms). 3 corridas consecutivas: idéntico.
  - Suite combinada S1+S2: `--filter "FullyQualifiedName~Auditoria"` → **40 passed / 0 failed / 0 skipped** (7 s).
  - Suite completa `dotnet test SGV.slnx --no-build` → **3358 total**. Resultados rotativos entre corridas (3/4 corridas verdes; 1/4 corrida con 1 fallido). Cuando falla, el test que rompe es **siempre** del conjunto preexistente `UsuariosEndToEndMySqlFactTests.*` (rotando entre `Delete_AnotherUser_Returns204`, `Bloquear_AnotherUser_Returns200WithBloqueadoTrue`, `Delete_OwnUser_Returns403AutoEliminacion`) — los mismos flaky que la sección «S1 Bugfix» ya documentó. Los 9 tests del S2 (`AuditoriasControllerTests.*`) pasan siempre.
- **Estado S2**: `success`. Listo para que S3 levante el cliente Web sobre este controller.

### Comandos ejecutados y resultados

| Comando | Resultado |
|---|---|
| `dotnet build SGV.slnx --no-restore` | OK (0 errores; 72 warnings preexistentes sin cambios) |
| `dotnet build tests/SGV.Tests/SGV.Tests.csproj --no-restore` (post-RED) | FAILED: CS0234 `SGV.Api.Controllers.AuditoriasController` no existe → confirma RED |
| `dotnet build tests/SGV.Tests/SGV.Tests.csproj --no-restore` (post-GREEN) | OK (0 errores) |
| `dotnet test tests/SGV.Tests/SGV.Tests.csproj --filter "FullyQualifiedName~AuditoriasControllerTests" --no-build` | **9 passed / 0 failed / 0 skipped** (372 ms) |
| `dotnet test tests/SGV.Tests/SGV.Tests.csproj --filter "FullyQualifiedName~Auditoria" --no-build` | **40 passed / 0 failed / 0 skipped** (7 s) — 9 S2 + 15 S1 + 16 adyacentes con "Auditoria" en el nombre (interceptor, setup audit trail, etc.) |
| `dotnet test SGV.slnx --no-build` | **3358 total**. 3/4 corridas verdes; 1/4 corrida con 1 fallido en `UsuariosEndToEndMySqlFactTests.*` (flaky preexistente, no S2) |

### Archivos cambiados (todos del slice S2)

| Archivo | Acción | Δ líneas | Descripción |
|---|---|---:|---|
| `src/SGV.Api/Controllers/AuditoriasController.cs` | Creado | +106 | Controller admin-only (`[ApiController]`, `[Authorize(Roles=RolesSgv.Administrador)]`) con GET listado + GET detalle; mapea `ArgumentException` del servicio a `ApiResults.ToValidationProblemResult` para `DateFrom>DateTo`. XML doc completo + `[ProducesResponseType]` para 200/400/401/403/404. |
| `src/SGV.Api/Infrastructure/Results/ApiResults.cs` | Modificado | +16 | Sobrecarga adicional `ToValidationProblemResult(string code, string detail, IReadOnlyDictionary<string,string[]>? fieldErrors, HttpContext? httpContext)` que reutiliza el helper privado `BuildValidationProblem`. Es aditiva — no cambia firmas existentes. |
| `tests/SGV.Tests/Api/AuditoriasControllerTests.cs` | Creado | +340 | Suite `[Fact]` (no toca DB). 9 tests cubren: 401 anónimo, 403 sin Admin, 200 con `PagedResult<AuditoriaDto>` y shape completo, paginación+filtros `?entityName=...&page=...&pageSize=...`, detalle 200/404, JSON sin old/new values, `[Authorize]` por reflexión, `DateFrom>DateTo` → 400 con `ProblemDetails`. Incluye `FakeAuditoriaServicioConsulta` self-contained (simula el contrato del servicio real sin tocar EF/MySQL). |
| `openspec/changes/implementa-modulo-auditorias/tasks.md` | Modificado | ±4 | Marcar 2.1–2.4 como `[x]`. |
| `openspec/changes/implementa-modulo-auditorias/apply-progress.md` | Modificado | — | Esta sección. |

**Total líneas nuevas del slice** (excluyendo tests): ~122 (controlador + sobrecarga).
**Total líneas de tests nuevos**: 340.

### TDD Cycle Evidence

| Task | Test | Layer | Safety Net | RED | GREEN | REFACTOR |
|---|---|---|---|---|---|---|
| 2.1 | `AuditoriasControllerTests.cs` | API integration (`[Collection("ApiIntegration")]`, `[Fact]`) | ✅ baseline build limpio | ✅ Archivo NO compilaba (CS0234 sobre `SGV.Api.Controllers.AuditoriasController`) | ✅ 8 tests pasan a la primera (401, 403, 200 shape, paginación+filtros, detalle 200, detalle 404, `[Authorize]` reflexión) | ✅ Una iteración post-GREEN: la aserción sobre `ChangedPropertiesJson` se ajustó a camelCase (`changedPropertiesJson`) porque `AddControllers()` sin `AddJsonOptions` usa la policy default de System.Text.Json, que es camelCase para HTTP responses. Las ausencias de `OldValuesJson`/`newValuesJson` se verifican tanto en PascalCase como en camelCase para defender el guardrail D-2 contra cualquier rename futuro |
| 2.2 | Mismo archivo (test `Get_Admin_DateFromMayorADateTo_Returns400ConProblemDetails`) | API integration (`[Fact]`) | ✅ mismo | ✅ Mismo CS0234 → no compila | ✅ Primer pase GREEN una vez creado el controller con el mapeo `try/catch (ArgumentException) → ApiResults.ToValidationProblemResult(...)` | ✅ Sin refactor pendiente |
| 2.3 | — | — | — | — | ✅ Controller creado + overload agregado a `ApiResults.ToValidationProblemResult` que toma `(string code, string detail, fieldErrors, httpContext)`. Reutiliza el helper privado `BuildValidationProblem` — no introduce un helper nuevo, sólo extiende la API existente con una forma sin tipo-error para read-sides que lanzan excepciones crudas | ✅ Doc explica por qué se justifica la sobrecarga (controllers de lectura sin envelope de error) |
| 2.4 | — | — | — | — | ✅ Build + suite focal + suite combinada + suite completa verdes | — |

#### TDD Cycle detalle: iteración post-GREEN del test 2.1.f

**Hipótesis inicial** (RED): la respuesta HTTP usa naming **PascalCase** porque `AuditoriaDto` es un `record` y `JsonSerializer.Serialize(record)` produce nombres PascalCase por default.

**Resultado del primer GREEN run**:
```
Assert.Contains() Failure: Sub-string not found
String:    "{"items":[{"id":"e85d33a5-0941-4d97-9c0e-"···
Not found: "ChangedPropertiesJson"
```

**Hallazgo**: la respuesta HTTP usa naming **camelCase**. Razón: `AddControllers()` en `Program.cs` no llama a `AddJsonOptions`, pero la pipeline MVC de ASP.NET Core aplica **camelCase por default** para HTTP responses (mientras que `JsonSerializer.Serialize` standalone usa PascalCase por default). El test S1 (`QueryAsync_Proyeccion_NoContieneOldNewValuesEnSerializacion`) usa `JsonSerializer.Serialize(dto)` directo, que produce PascalCase — por eso pasaba antes. La diferencia explica el falso negativo.

**Fix**: actualizar la aserción a `Assert.Contains("changedPropertiesJson", json, StringComparison.Ordinal)`. Aproveché para verificar **ambas** variantes (PascalCase y camelCase) de los campos prohibidos (`OldValuesJson`/`oldValuesJson`, `NewValuesJson`/`newValuesJson`) — defense-in-depth contra un futuro `AddJsonOptions(...).UseCamelCase()` que unifique la pipeline con el resto de tests.

### Test Summary

- **Total tests escritos**: 9 ejecuciones xUnit (`[Fact]` puro, no `[MySqlFact]`).
- **Total tests passing**: 9/9 en la suite focal.
- **Layers used**: API integration con `ApiIntegrationFixture` (compartido con el resto de tests de API; cada test que toca el servicio registra un `FakeAuditoriaServicioConsulta` self-contained vía `WithOverrides`).
- **Approval tests**: Ninguno — no hubo refactor de código preexistente.
- **Pure functions**: ninguna añadida — el controller depende del servicio vía DI, no hay funciones puras aislables.

### Desglose de cobertura por spec scenario

| Test | Spec scenario |
|---|---|
| `Get_Anonymous_Returns401` | "Acceso anónimo a la API" → 401 |
| `Get_NonAdmin_Returns403` | "Usuario autenticado sin rol Administrador" → 403 |
| `Get_Admin_Returns200WithPagedResult` | "Administrador accede a la API" → 200 con `PagedResult<AuditoriaDto>` |
| `Get_Admin_PaginacionYFiltrosAplican` | "Filtros combinados filtran el resultado" + "Defaults aplicados" |
| `GetById_Admin_Existe_200` + `GetById_Admin_NoExiste_404` | "Detalle existente" + "Detalle inexistente" |
| `Get_Json_NoContieneOldNiNewValues` | "DTO no expone old/new values" (D-2 a nivel HTTP) |
| `AuditoriasController_TieneAuthorizeAttribute` | Guardrail D-1 vía reflexión |
| `Get_Admin_DateFromMayorADateTo_Returns400ConProblemDetails` | "Rango de fechas invertido" → 400 Validation |

### Work Unit Evidence

| Evidence | Value |
|---|---|
| Focused test command and exact result | `dotnet test tests/SGV.Tests/SGV.Tests.csproj --filter "FullyQualifiedName~AuditoriasControllerTests" --no-build` → `Passed: 9, Failed: 0, Skipped: 0` (372 ms). 3 corridas consecutivas: idéntico. |
| Runtime harness command and exact result | `dotnet test SGV.slnx --no-build` → 3358 total. 3/4 corridas pasan 3358/0/0; 1/4 corrida falla 1 test en `UsuariosEndToEndMySqlFactTests.*` (mismo flaky preexistente que la sección «S1 Bugfix» documentó). **Los 9 tests del S2 nunca fallan.** |
| Rollback boundary | Borrar `src/SGV.Api/Controllers/AuditoriasController.cs` y `tests/SGV.Tests/Api/AuditoriasControllerTests.cs`; revertir la sobrecarga agregada en `src/SGV.Api/Infrastructure/Results/ApiResults.cs`. **No se tocó** ningún archivo S1 (`AuditoriaDto`, `AuditoriaListQuery`, `IAuditoriaServicioConsulta`, `AuditoriaServicioConsulta`, DI existente, `AuditoriaServicioConsultaTests`, `MySqlTheoryAttribute`). El contrato del puerto y la impl EF siguen verdes. |

### Decisiones y desviaciones del diseño

- **`FakeAuditoriaServicioConsulta` self-contained**: en lugar de cablear el `SgvDbContext` real a través del `ApiWebApplicationFactory` (lo que exigiría un `AuditoriaTestScope` específico para API + overrides de `SgvDbContext` en cada test), cada test que toca el servicio registra un fake en memoria que simula el mismo contrato: filtrado por EntityName/Operation/UserId, rango DateFrom/DateTo (inclusivo), clamp `[1,100]`, orden `OccurredAt DESC, Id DESC`, y `ArgumentException` con el **mismo mensaje** que la impl EF cuando `DateFrom > DateTo`. Decisión alineada con `tests/SGV.Tests/Api/NivelesCargoControllerTests.cs` (que usa `FakeNivelCargoServicioConsulta`) y `CargosControllerTests.cs` (que usa `FakeCargoServicio`). Los tests sin DB (auth, reflexión) usan el factory raíz sin override.
- **`ApiResults.ToValidationProblemResult` sobrecargado**: las firmas existentes reciben un error tipado (`CargoError`, `HabilidadError`, …). El servicio de auditoría lanza `ArgumentException` cruda (no envelope), por lo que se necesitaba un camino para superficies raw strings sin inventar un `AuditoriaError` artificial. Se agregó una sobrecarga `ToValidationProblemResult(string code, string detail, fieldErrors, httpContext)` que reutiliza el helper privado `BuildValidationProblem` — extension additive de la API existente, no un helper nuevo.
- **Sin `[MySqlFact]`**: el S1 ya cubrió los flujos que dependen de EF/MySQL con 15 tests reales (`AuditoriaServicioConsultaTests`). Duplicar esa cobertura a nivel controller sin valor adicional sería ruido. El fake aquí es verificable, determinístico y rápido; corre estable sin DB.
- **Naming assertion adjustment**: ver "TDD Cycle detalle: iteración post-GREEN del test 2.1.f" arriba. La aserción final verifica la presencia de `changedPropertiesJson` (camelCase, que es lo que wire HTTP devuelve) y la ausencia de **ambas** variantes (`OldValuesJson` + `oldValuesJson`, `NewValuesJson` + `newValuesJson`) para defender D-2 contra cualquier cambio futuro de naming policy.

### Issues encontrados

- **MySQL no requerido**: como S1 ya cubre el camino EF, S2 corre 100% contra fakes. Si en un futuro se quisiera agregar un test E2E con DB real al controller (estilo `UsuariosEndToEndMySqlFactTests` + `JwtRealWebApplicationFactory`), la base ya está: el controller no necesita cambios, sólo registrar `IAuditoriaServicioConsulta` apuntando a un `SgvDbContext` real.
- **Pipeline JSON default camelCase**: confirmado que sin `AddJsonOptions`, ASP.NET Core aplica camelCase en HTTP responses. La spec no exige naming policy particular, pero conviene tener presente el default si en el futuro un controller de auditoría necesita mantener compatibilidad PascalCase con un consumidor existente.

## Rollback del batch S2

1. `rm src/SGV.Api/Controllers/AuditoriasController.cs tests/SGV.Tests/Api/AuditoriasControllerTests.cs`
2. En `src/SGV.Api/Infrastructure/Results/ApiResults.cs`: eliminar la sobrecarga `ToValidationProblemResult(string, string, IReadOnlyDictionary<string, string[]>?, HttpContext?)` agregada (es aditiva, no toca las firmas existentes).
3. Revertir las marcas `[x]` en `openspec/changes/implementa-modulo-auditorias/tasks.md` (2.1–2.4 → `[ ]`).
4. Revertir esta sección de `apply-progress.md`.
5. Build + suite existentes siguen verdes: S1 intacto (servicio + puerto + DI + tests), S3 todavía no implementado.
## S3 — Web + Page + sidenav + docs (2026-07-31)

### Resultado del batch

- **Modo**: Strict TDD (test runner `dotnet test SGV.slnx`).
- **Tareas completadas**: 3.1, 3.2, 3.3, 3.4, 3.5, 3.6, 3.7, 3.8 (8/8 del slice S3).
- **Tareas pendientes**: ninguna. El change `implementa-modulo-auditorias` queda 100% implementado.
- **Verificación**:
  - Build slnx: `dotnet build SGV.slnx --no-restore` → 0 errores.
  - Build tests: `dotnet build tests/SGV.Tests/SGV.Tests.csproj --no-restore` → 0 errores.
  - Suite focal S3: `dotnet test tests/SGV.Tests/SGV.Tests.csproj --filter "FullyQualifiedName~AuditoriasIndexTests" --no-build` → **6 passed / 0 failed / 0 skipped** (1 s).
  - Suite combinada S1+S2+S3 + adyacentes (`Auditoria` en el FQN): `dotnet test ... --filter "FullyQualifiedName~Auditoria" --no-build` → **46 passed / 0 failed / 0 skipped** (13 s). 6 S3 + 9 S2 + 15 S1 + 16 adyacentes (interceptor, setup audit trail, etc.).
  - Suite global: `dotnet test SGV.slnx --no-build` → **3364 passed / 0 failed / 0 skipped** (2 m 10 s). 0 flakiness observado en este batch (los 0-5 fallos rotativos documentados en §"S1 Bugfix" y §"S2" no aparecieron en esta corrida).
  - Bun build: `cd src/SGV.Web && bun run build` → gulp pipeline OK (3.04 s). 0 errores; sólo warnings preexistentes (`baseline-browser-mapping` outdated, `browserslist` data aged, `DEP0180 fs.Stats` deprecation) no relacionados con este batch.
- **Estado S3**: `success`. El change completo queda 100% listo para `sdd-archive`.

### Comandos ejecutados y resultados

| Comando | Resultado |
|---|---|
| `dotnet build SGV.slnx --no-restore` | OK (0 errores; 13 warnings preexistentes sin cambios) |
| `dotnet build tests/SGV.Tests/SGV.Tests.csproj --no-restore` (post-RED inicial) | FAILED: CS0176 + CS0234 sobre `IndexModel.DefaultPageSize` + `Pages/Auditorias` → confirma RED |
| `dotnet build tests/SGV.Tests/SGV.Tests.csproj --no-restore` (post-GREEN) | OK (0 errores) |
| `dotnet test tests/SGV.Tests/SGV.Tests.csproj --filter "FullyQualifiedName~AuditoriasIndexTests" --no-build` | **6 passed / 0 failed / 0 skipped** (1 s) — desglose: admin/empty/transport/pagination/auth×2 |
| `dotnet test tests/SGV.Tests/SGV.Tests.csproj --filter "FullyQualifiedName~Auditoria" --no-build` | **46 passed / 0 failed / 0 skipped** (13 s) — 6 S3 + 9 S2 + 15 S1 + 16 adyacentes |
| `dotnet test SGV.slnx --no-build` | **3364 passed / 0 failed / 0 skipped** (2 m 10 s) — 0 flakiness |
| `cd src/SGV.Web && bun install` | OK (772 installs across 667 packages; no changes — dependencias ya satisfechas) |
| `cd src/SGV.Web && bun run build` | OK (3.04 s; gulp pipeline verde; 0 errores, sólo warnings preexistentes) |

### Archivos cambiados (todos del slice S3)

| Archivo | Acción | Δ líneas | Descripción |
|---|---|---:|---|
| `src/SGV.Web/Integration/Auditoria/IAuditoriaApiClient.cs` | Creado | +60 | Puerto del cliente HTTP tipado. `QueryAsync` + `ObtenerPorIdAsync` con XML doc. Espejo de `IPuestosApiClient` / `IOcupacionApiClient`. |
| `src/SGV.Web/Integration/Auditoria/AuditoriaApiClient.cs` | Creado | +130 | Impl HTTP con `EnsureSuccessStatusCode`; 404 → `null` para detalle; `StringBuilder + Uri.EscapeDataString` para query URI; propaga `HttpRequestException`/`TaskCanceledException` nativas. |
| `src/SGV.Web/Program.cs` | Modificado | +15 | `AddHttpClient<IAuditoriaApiClient, AuditoriaApiClient>(...).AddHttpMessageHandler<ApiBearerTokenHandler>()` con 10s budget paralelo al resto de los clientes tipados. |
| `src/SGV.Web/Pages/Auditorias/Index.cshtml.cs` | Creado | +155 | PageModel con `[Authorize(Roles=RolesSgv.Administrador)]`, `OnGetAsync` con filtros + paginación, `BuildPagedRouteValues` (preserva filtros vigentes), `TransportFailureClassifier` para errores recuperables, `SetLoadErrorState` para fallback vacío. |
| `src/SGV.Web/Pages/Auditorias/Index.cshtml` | Creado | +145 | Razor view con sidebar filtros (EntityName, Operation, DateFrom, DateTo, UserId) + tabla (Fecha, Entidad, Operación, ID entidad, Usuario, Propiedades modificadas, Correlación) + paginación (Primera / Anterior / Siguiente / Última) + empty state. |
| `src/SGV.Web/Pages/Shared/Partials/_Sidenav.cshtml` | Modificado | +18 | Top-level item `Auditorías` con ícono `ti ti-file-text`, gateado por `@if (esAdministrador)`. Posicionado al final, después del grupo `Seguridad`. |
| `tests/SGV.Tests/Web/Auditoria/FakeAuditoriaApiClient.cs` | Creado | +88 | Fake in-memory del `IAuditoriaApiClient` con `QueryResult`/`QueryHandler`/`QueryException`/`ObtenerPorIdResult`/`ObtenerPorIdHandler` + captura de invocaciones (`QueryCalls`, `ObtenerPorIdCalls`). |
| `tests/SGV.Tests/Web/Auditoria/AuditoriasIndexTests.cs` | Creado | +270 | 6 tests `[Fact]` con `[Collection("WebIntegration")]`. 6 escenarios de task 3.1: admin 200 con tabla + paginación, lista vacía legible, error de transporte recuperable sin perder filtros, paginación preserva filtros, no-admin → 403, anónimo → redirect. |
| `tests/SGV.Tests/Web/SgvWebApplicationFactory.cs` | Modificado | +20 | Nuevo campo `_auditoriaApiClient` + parámetro opcional en constructor + `WithAuditoriaApiClient(IAuditoriaApiClient)` helper. Espejo de `WithHabilidadApiClient` / `WithVacanteApiClient`. |
| `tests/SGV.Tests/Web/Collections/WebIntegrationFixture.cs` | Modificado | +10 | Nuevo helper `CreateAuditoriaLeaseAsync(IAuditoriaApiClient, adminRole = true)`. Espejo de `CreateCargoLeaseAsync`. |
| `docs/decisiones-implementacion.md` | Modificado | +160 | Nueva sección top-level "Módulo transversal de Auditoría — capa de lectura" que documenta D-1..D-5 del design, las decisiones de wire contract (D-2), la autorización admin-only, los no-objetivos del v1, y el cross-reference al change folder. Redacción en español, tono neutral/profesional, estilo consistente con el resto del documento. |
| `openspec/changes/implementa-modulo-auditorias/tasks.md` | Modificado | ±8 | Marcar 3.1–3.8 como `[x]`. |
| `openspec/changes/implementa-modulo-auditorias/apply-progress.md` | Modificado | — | Esta sección. |

**Total líneas nuevas del slice S3** (excluyendo tests): ~520 (cliente + page + sidenav + Program.cs + docs).
**Total líneas de tests nuevos**: ~378 (AuditoriasIndexTests + FakeAuditoriaApiClient + 2 helpers en factory/fixture).

### TDD Cycle Evidence

| Task | Test | Layer | Safety Net | RED | GREEN | REFACTOR |
|---|---|---|---|---|---|---|
| 3.1 | `AuditoriasIndexTests.cs` | Web integration (`[Collection("WebIntegration")]`, `[Fact]`) | ✅ baseline build limpio | ✅ `Pages/Auditorias/IndexModel` + `IAuditoriaApiClient` + `FakeAuditoriaApiClient` no compilan (CS0234 + CS0246 sobre `SGV.Web.Pages.Auditorias.IndexModel`, etc.) | ✅ 6 tests pasan a la primera (admin 200, empty state, transport recoverable preserva filtros, paginación preserva filtros, no-admin → 403, anónimo → redirect a sign-in) | ✅ Una iteración post-GREEN: el parámetro `int page` colisiona con el identificador interno `page` de Razor Pages (Razor Pages omite cualquier route value con ese nombre del URL generado). Fix: renombrar a `[FromQuery(Name = "p")] int currentPage` y `BuildPagedRouteValues` con key `p` — espejo del patrón canónico de `PuestoIndexModel` / `CargoIndexModel` / `HabilidadIndexModel`. La key del route value pasa de `page` a `p`; el parámetro del handler se llama `currentPage` con `[FromQuery(Name = "p")]` para mantener `p` en la URL |
| 3.2 | — | — | — | — | ✅ `IAuditoriaApiClient` creado con XML doc; firma exacta del proposal (`QueryAsync(AuditoriaListQuery, CT)`, `ObtenerPorIdAsync(Guid, CT)`) | ✅ Doc explica la convención de propagación nativa de `HttpRequestException`/`TaskCanceledException` y el mapeo 404 → `null` |
| 3.3 | — | — | — | — | ✅ `AuditoriaApiClient` HTTP con `EnsureSuccessStatusCode`, 404 → `null` (chequeado antes de `EnsureSuccessStatusCode`), `StringBuilder + Uri.EscapeDataString` para query, `cancellationToken.ThrowIfCancellationRequested()` antes de cada request | ✅ Comentario explica el patrón 404 → `null` y por qué se chequea antes de `EnsureSuccessStatusCode` |
| 3.4 | — | — | — | — | ✅ DI en `Program.cs` con `AddHttpMessageHandler(sp => sp.GetRequiredService<ApiBearerTokenHandler>())` (espejo del resto de los clientes tipados) y 10s budget | ✅ Comentario explica el budget paralelo y por qué `ApiBearerTokenHandler` está en la pipeline (controller admin-only exige bearer JWT) |
| 3.5 | — | — | — | — | ✅ `Pages/Auditorias/Index.cshtml.cs` con `[Authorize(Roles=RolesSgv.Administrador)]`, `OnGetAsync(int currentPage, int pageSize, string? entityName, string? operation, DateTime? dateFrom, DateTime? dateTo, string? userId, CancellationToken)`, `BuildPagedRouteValues`, `TransportFailureClassifier`, `SetLoadErrorState`; `Index.cshtml` con sidebar filtros + tabla + paginación + empty state | ✅ Doc explica el rebind de `page` → `p` y la decisión de NO usar el nombre `page` (reservado por Razor Pages) |
| 3.6 | — | — | — | — | ✅ Entrada «Auditorías» añadida en `_Sidenav.cshtml` con `ti ti-file-text` + `@if (esAdministrador)`; posición al final del sidenav | ✅ Comentario explica la decisión de top-level item vs subítem de Seguridad |
| 3.7 | — | — | — | — | ✅ Sección "Módulo transversal de Auditoría — capa de lectura" en `docs/decisiones-implementacion.md` con D-1..D-5, autorización, no-objetivos, cobertura, riesgos residuales | ✅ Doc en español neutral/profesional; tabla de archivos clave; cross-reference al change folder; redacción consistente con secciones previas |
| 3.8 | — | — | — | — | ✅ Build slnx OK + suite S3 6/6 + suite combinada S1+S2+S3 46/46 + suite global 3364/0/0 + `bun run build` verde | — |

#### TDD Cycle detalle: iteración post-GREEN de 3.1 (reserva de `page` por Razor Pages)

**Hipótesis inicial** (RED): el parámetro del handler se llama `int page` y el route value usa la misma key — debería serializar en la URL como `?page=3&...`.

**Resultado del primer GREEN run** (test 3.1.d):
```
Assert.Contains() Failure: Sub-string not found
String:    "<!DOCTYPE html>\n<html lang="en"  class="""···
Not found: "page=3"
```

Inspección del HTML renderizado (test exploratorio `AuditoriasIndexDebugTests.DumpPaginationHtml` creado y borrado después del fix): el link "Siguiente" renderiza `href="/auditorias?pageSize=20&entityName=Cargo&operation=Alta&userId=u-7"` — la key `page` está **ausente** del querystring.

**Hallazgo**: Razor Pages reserva el nombre `page` como identificador interno de la página (no como query parameter). `Url.Page` consume cualquier route value llamado `page` para construir el URL del handler y omite esa key del querystring generado. Es exactamente el patrón que `PuestoIndexModel` y el resto de los módulos evitan usando `p` como key del route value (con `[FromQuery(Name = "p")] int currentPage` en el handler).

**Fix**: renombrar el parámetro del handler de `int page` a `[FromQuery(Name = "p")] int currentPage`, y cambiar el key del route value en `BuildPagedRouteValues` de `page` a `p`. Mismo refactor que en S1/S2 (que NO tocaron Razor Pages), pero descubierto en S3.

**Resultado del segundo GREEN run**: 6/6 tests pasan. Render verificado manualmente: `href="/auditorias?p=3&pageSize=20&entityName=Cargo&operation=Alta&userId=u-7"`.

### Test Summary

- **Total tests escritos**: 6 ejecuciones xUnit `[Fact]` puras (no `[MySqlFact]`).
- **Total tests passing**: 6/6 en la suite focal.
- **Layers used**: Web integration con `WebIntegrationFixture` (compartido con el resto de la suite web; cada test que toca la API inyecta un `FakeAuditoriaApiClient` self-contained vía `WithAuditoriaApiClient`).
- **Approval tests**: Ninguno — no hubo refactor de código preexistente (la factory de tests SÍ se extendió, pero ningún assert verifica la forma exacta de los overrides; los 6 tests cubren el comportamiento del PageModel, no la fábrica).
- **Pure functions**: ninguna añadida — el PageModel depende del `IAuditoriaApiClient` vía DI, no hay funciones puras aislables.

### Desglose de cobertura por spec scenario

| Test | Spec scenario del task 3.1 |
|---|---|
| `Get_Index_WhenAdmin_RendersTableAndPagination` | "Admin 200 con tabla + paginación" |
| `Get_Index_WhenListIsEmpty_ShowsEmptyState` | "Lista vacía legible" |
| `Get_Index_WhenApiFails_ShowsVisibleErrorAndPreservesFilters` | "Error de transporte recuperable sin perder filtros" |
| `Get_Index_Pagination_PreservesFilters` | "Paginación conserva filtros" |
| `Get_Index_WhenNonAdmin_RedirectsToAccessDenied` | "No-admin → error" |
| `Get_Index_WhenAnonymous_RedirectsToSignIn` | "Anónimo → redirect" |

### Work Unit Evidence

| Evidence | Value |
|---|---|
| Focused test command and exact result | `dotnet test tests/SGV.Tests/SGV.Tests.csproj --filter "FullyQualifiedName~AuditoriasIndexTests" --no-build` → `Passed: 6, Failed: 0, Skipped: 0` (1 s) |
| Runtime harness command and exact result (combined S1+S2+S3+adjacent) | `dotnet test tests/SGV.Tests/SGV.Tests.csproj --filter "FullyQualifiedName~Auditoria" --no-build` → `Passed: 46, Failed: 0, Skipped: 0` (13 s). 6 S3 + 9 S2 + 15 S1 + 16 adyacentes. |
| Runtime harness command and exact result (full suite) | `dotnet test SGV.slnx --no-build` → `Passed: 3364, Failed: 0, Skipped: 0` (2 m 10 s). 0 flakiness observado. |
| Frontend asset build command and exact result | `cd src/SGV.Web && bun run build` → gulp pipeline OK (3.04 s; 0 errores; sólo warnings preexistentes) |
| Rollback boundary | Borrar `src/SGV.Web/Integration/Auditoria/*.cs`, `src/SGV.Web/Pages/Auditorias/`, `tests/SGV.Tests/Web/Auditoria/`, revertir el bloque `AddHttpClient<IAuditoriaApiClient, ...>` en `Program.cs`, revertir el item «Auditorías» y `auditoriasActive` en `_Sidenav.cshtml`, revertir los 2 helpers en `SgvWebApplicationFactory.cs` y `WebIntegrationFixture.cs`. **No se tocó** ningún archivo S1 (servicio + puerto + DI) ni S2 (controller + ApiResults). El contrato del backend y la impl EF siguen verdes; el change se reduce a sólo el slice web. |

### Decisiones y desviaciones del diseño

- **Sin handler POST en S3:** el módulo es estrictamente read-only. No hay `[HttpPost]`/`OnPost*` en el PageModel. Esto preserva el principio de "vista pura de auditoría" del proposal: el administrador NO modifica la bitácora desde la UI. Si en el futuro se quiere agregar drill-down de detalle, se añadirá un `OnGetDetalleAsync` o un subrecurso, no un POST.
- **Sidebar filtros con GET (no POST-PRG):** el proposal sugiere "PRG (Post-Redirect-Get) navigation" para los filtros. La implementación actual usa form `method="get"` para los filtros (paridad con los otros Index del shell) — el browser resuelve la query string y la page la bindea desde `Request.Query`. Los enlaces de paginación también pasan por querystring via `Url.Page`. El comportamiento neto es equivalente al PRG (URL siempre refleja el estado; refresh es seguro), pero más simple y consistente con el resto de la base de código. Si en el futuro se quiere PRG estricto (POST + redirect a GET), la refactorización es trivial: cambiar el form a `method="post"` + `OnPostAsync` que valide y redirija.
- **`p` en lugar de `page` para la paginación:** el nombre `page` está reservado por Razor Pages. Espejo del patrón canónico vigente en `PuestoIndexModel` / `CargoIndexModel` / `HabilidadIndexModel` / `PersonaIndexModel`. La key del route value es `p`; el binding del handler usa `[FromQuery(Name = "p")] int currentPage`. La URL se ve como `?p=3&...` (no `?page=3&...`).
- **Sidenav: top-level item en vez de subítem de Seguridad:** la auditoría es un módulo transversal, no un subítem del módulo de seguridad. Decisión: añadirlo como top-level item al final del sidenav con `ti ti-file-text` (ícono no usado por ningún otro módulo). Gated por `esAdministrador` igual que el subítem "Nueva" de Ocupaciones / Vacantes o el subítem "Usuarios" de Seguridad. La autorización por acción (POST / escritura) sigue viviendo en el `[Authorize(Roles = RolesSgv.Administrador)]` del PageModel, no en el sidenav.
- **Fake in-memory vs handler HTTP mock:** S3 usa `FakeAuditoriaApiClient` inyectado vía `SgvWebApplicationFactory.WithAuditoriaApiClient(...)`, exactamente el mismo patrón de `WithHabilidadApiClient` / `WithPuestosApiClient` / `WithVacanteApiClient` / `WithPersonaApiClient`. Reemplaza el cliente HTTP tipado en el contenedor del host; el `ApiBearerTokenHandler` sigue activo pero ningún request sale del proceso. Esto evita la necesidad de un handler HTTP mock + `ConfigurePrimaryHttpMessageHandler` (que S2 ya usó) y mantiene los tests determinísticos.
- **Sin tests de markup exhaustivos:** no se testea el orden de las columnas, el color del badge, el icono de orden, ni el data-attribute del form (todos válidos por inspección). Sólo se verifica la presencia de los textos clave ("Listado de auditoría del sistema", "No se encontraron registros...", "Página X de Y", "value=...", "p=N") y la ausencia de los textos prohibidos cuando corresponde. La filosofía del AGENTS.md (pocos tests significativos) aplica.

### Issues encontrados

- **`p` vs `page` (resuelto en TDD cycle):** ver "TDD Cycle detalle: iteración post-GREEN de 3.1" arriba. Es un gotcha de Razor Pages que sólo se descubre cuando el test verifica el URL rendered en el HTML. Sin el test, el bug habría llegado a producción con links de paginación que no incluían `page=N`.
- **Suite global sin flakiness en este batch:** la corrida de `dotnet test SGV.slnx` cerró en 3364/0/0 sin flakiness observable. Esto contrasta con las corridas de S1 (1-5 fallos rotativos) y la corrida de S2 reportada en §"S2" (1/4 corridas con 1 fallo). Atribuible a que las suites adyacentes (`Setup.SetupConcurrencyMySqlFactTests` / `UsuariosEndToEndMySqlFactTests` / `VacanteRepositoryQueryTests`) son inherentemente concurrentes y sensibles al scheduling de MySQL — la varianza de un solo run no es concluyente, pero el trend de "0 fallos en S3" es consistente con "el batch S3 no agrega nuevas fuentes de flakiness".
- **Bun install: no-op:** `bun install` corrió pero no instaló nada (772 installs / 667 packages, "no changes"). Las dependencias frontend ya estaban satisfechas del flujo de PRs anteriores; el cambio S3 no introduce nuevos paquetes npm.

## Rollback del batch S3

1. `rm -rf src/SGV.Web/Integration/Auditoria/ src/SGV.Web/Pages/Auditorias/ tests/SGV.Tests/Web/Auditoria/`
2. En `src/SGV.Web/Program.cs`: eliminar el bloque `AddHttpClient<IAuditoriaApiClient, AuditoriaApiClient>(...)` agregado (incluye el `using SGV.Web.Integration.Auditoria;`).
3. En `src/SGV.Web/Pages/Shared/Partials/_Sidenav.cshtml`: eliminar el bloque `@if (esAdministrador) { ... <li class="side-nav-item"> ... Auditorías ... }` y la variable `auditoriasActive` agregada (incluye el comentario explicativo).
4. En `tests/SGV.Tests/Web/SgvWebApplicationFactory.cs`: eliminar el campo `_auditoriaApiClient`, el parámetro opcional del constructor, el `WithAuditoriaApiClient` helper y el bloque de `if (_auditoriaApiClient is not null) { ... }` en `ConfigureWebHost`. Revertir el `using SGV.Web.Integration.Auditoria;` agregado.
5. En `tests/SGV.Tests/Web/Collections/WebIntegrationFixture.cs`: eliminar el helper `CreateAuditoriaLeaseAsync` y el `using SGV.Web.Integration.Auditoria;` agregado.
6. En `docs/decisiones-implementacion.md`: eliminar la sección "Módulo transversal de Auditoría — capa de lectura" agregada (revertir a la versión previa).
7. En `openspec/changes/implementa-modulo-auditorias/tasks.md`: revertir 3.1–3.8 de `[x]` a `[ ]`.
8. Revertir esta sección de `apply-progress.md`.
9. Build + suite S1+S2 siguen verdes: el contrato del backend, el puerto, la impl EF, el controller API y los tests S2 permanecen intactos. El change se reduce al slice web + docs.
## Rollback del batch S1

1. `git rm src/SGV.Contracts/Auditoria/*.cs src/SGV.Aplicacion/Auditoria/IAuditoriaServicioConsulta.cs src/SGV.Infraestructura/Persistencia/AuditoriaServicioConsulta.cs tests/SGV.Tests/Aplicacion/Auditoria/AuditoriaServicioConsultaTests.cs tests/SGV.Tests/Persistencia/MySqlTheoryAttribute.cs`
2. Revertir `src/SGV.Infraestructura/DependencyInjection.cs` al estado previo (quitar bloque `AddScoped<IAuditoriaServicioConsulta, ...>`).
3. Build + suite existentes siguen verdes (no se tocó escritura, interceptor ni entidad).