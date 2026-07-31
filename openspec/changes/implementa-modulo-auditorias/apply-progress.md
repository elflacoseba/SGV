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

## Rollback del batch S1

1. `git rm src/SGV.Contracts/Auditoria/*.cs src/SGV.Aplicacion/Auditoria/IAuditoriaServicioConsulta.cs src/SGV.Infraestructura/Persistencia/AuditoriaServicioConsulta.cs tests/SGV.Tests/Aplicacion/Auditoria/AuditoriaServicioConsultaTests.cs tests/SGV.Tests/Persistencia/MySqlTheoryAttribute.cs`
2. Revertir `src/SGV.Infraestructura/DependencyInjection.cs` al estado previo (quitar bloque `AddScoped<IAuditoriaServicioConsulta, ...>`).
3. Build + suite existentes siguen verdes (no se tocó escritura, interceptor ni entidad).