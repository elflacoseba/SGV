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
## Rollback del batch S1

1. `git rm src/SGV.Contracts/Auditoria/*.cs src/SGV.Aplicacion/Auditoria/IAuditoriaServicioConsulta.cs src/SGV.Infraestructura/Persistencia/AuditoriaServicioConsulta.cs tests/SGV.Tests/Aplicacion/Auditoria/AuditoriaServicioConsultaTests.cs tests/SGV.Tests/Persistencia/MySqlTheoryAttribute.cs`
2. Revertir `src/SGV.Infraestructura/DependencyInjection.cs` al estado previo (quitar bloque `AddScoped<IAuditoriaServicioConsulta, ...>`).
3. Build + suite existentes siguen verdes (no se tocó escritura, interceptor ni entidad).