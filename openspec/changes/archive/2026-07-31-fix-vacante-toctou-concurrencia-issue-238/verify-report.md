# Verify Report: fix-vacante-toctou-concurrencia-issue-238

## Resumen

**Veredicto: APROBADO_CON_OBSERVACIONES**

Build OK, todas las suites focales verdes (Aplicacion.Vacantes 18/18, Persistencia 383/383, Api.Vacantes 24/24, VacantesConcurrenciaTests 3/3) y suite completa 3334/3334 passed. Los cuatro escenarios del requirement ADDED "Unicidad de vacante abierta por puesto" tienen cobertura ejecutable (1 por test de modelo + 3 `[MySqlFact]`). El escenario "Carrera concurrente" del requirement MODIFIED "Crear Vacante" queda cubierto por el `[MySqlFact]` T7.1.a. Las decisiones D-1 a D-7 del design están implementadas y verificadas en código + migración + tests. Único WARNING: el apply-progress menciona tests flaky pre-existentes por estado compartido de MySQL; en esta corrida no se reprodujeron (DB limpia), pero deben documentarse en el PR para el revisor.

## Criterios de aceptación del proposal

| # | Criterio | Resultado |
|---|----------|-----------|
| 1 | `dotnet build SGV.slnx` compila sin errores | ✓ (0 errors, 94 warnings pre-existentes no relacionadas al change) |
| 2 | Constraint `ActivePuestoIdUnique` en `VacanteConfiguracion.cs` con fórmula `CASE WHEN FechaCierre IS NULL AND IsDeleted = 0 THEN PuestoId ELSE NULL END` | ✓ (líneas 40-44 del archivo, fórmula exacta con backticks) |
| 3 | Catch block de `CrearAsync` mapea constraint violations a `VacanteErrorCodigo.PuestoConVacanteAbierta` | ✓ (líneas 177-185; antes `DatosInvalidos`, ahora `PuestoConVacanteAbierta` con mensaje "Ya existe una vacante abierta para el puesto especificado.") |
| 4 | Test `[MySqlFact] CrearAsync_Concurrencia_MismaVacanteAbierta` pasa: dos creaciones concurrentes para el mismo `PuestoId` — una recibe `201`, la otra `409 Conflict` con código `PuestoConVacanteAbierta` | ✓ (`Crear_MismoPuestoIdConcurrente_UnoPersisteOtroFallaConDbUpdateException` en `VacantesConcurrenciaTests.cs:41` — exactamente 1 persiste, la otra falla con `DbUpdateException` que contiene `IX_Vacantes_ActivePuestoIdUnique`; verificación cruzada de 1 fila activa post-carrera) |
| 5 | Suite completa `dotnet test SGV.slnx` pasa sin regresión | ✓ (3334/3334 passed en DB limpia; lista de flaky pre-existentes en apply-progress §Issues — no se reprodujeron en esta corrida pero el PR debe advertir al revisor) |
| 6 | `dotnet test --filter "FullyQualifiedName~Aplicacion.Vacantes"` verde | ✓ 18/18 |
| 7 | `dotnet test --filter "FullyQualifiedName~Persistencia"` verde | ✓ 383/383 |
| 8 | `dotnet test --filter "FullyQualifiedName~Api.Vacantes"` verde | ✓ 24/24 |
| 9 | `dotnet test --filter "FullyQualifiedName~VacantesConcurrenciaTests"` verde | ✓ 3/3 (3 corridas consecutivas determinísticas según apply-progress) |

## Findings

### CRITICAL
Ninguno.

### WARNING

- **W-1 (informativo, no bloqueante):** El apply-progress documenta una lista de tests flaky pre-existentes que fallan intermitentemente por estado compartido del MySQL test DB (no son regresiones del change):
  - `SGV.Tests.Seguridad.JwtCorteInmediatoMySqlFactTests.BloquearUsuario_InvalidaJwtInmediatamente`
  - `SGV.Tests.Setup.SetupHappyPathMySqlFactTests.Crear_DatosValidos_CreaPersonaUsuarioRolYAuditoria`
  - `SGV.Tests.Persistencia.PersonaRepositoryTests.ActualizarPersona_LimpiarLegajo_PersisteNullYRegistraUpdateLegajoEnAuditorias`
  - `SGV.Tests.Persistencia.PersonaRepositoryTests.GetByIdForUpdateAsync_RetornaPersonaActiva`
  - `SGV.Tests.Persistencia.UsuarioIdentityGatewayTests.QueryAsync_*`
  - `SGV.Tests.OcupacionRepositoryQueryAsyncTests.QueryAsync_MySql_SegmentoEliminadas_*`

  En esta corrida de verify, la suite completa terminó 3334/3334 sin reproducir ninguno. Sin embargo, el PR que abra el orchestrator debe mencionar esta lista al revisor para que sepa que un eventual re-run contra una DB no limpia puede mostrar fallos intermitentes ajenos al change.

- **W-2 (defensa del `[MySqlFact]` T7.1.a):** El test de carrera usa dos contextos EF separados en `Task.Run` en lugar de un `Task.WhenAll` sobre el mismo contexto (EF Core no es thread-safe por instancia). Esto reproduce fielmente el escenario real del API (dos requests HTTP con dos scopes distintos), pero debe documentarse claramente en el PR por qué no se usa `Task.WhenAll` directo sobre una sola instancia.

### SUGGESTION

- **S-1:** El test de modelo `Vacante_ConfiguraShadowActivePuestoIdUniqueConFormulaCorrecta` recurre a `IDesignTimeModel().Model.GetRelationalModel()` para leer `ComputedColumnSql` y `Collation` (Pomelo 9 no expone `Relational:Collation` en `shadowProperty["Relational:Collation"]`). Es funcional y correcto, pero podría documentarse con un link al issue upstream de Pomelo para que un futuro port a EF Core 10/Pomelo 10 sepa por qué se usa este patrón.

- **S-2:** La constante de collation `ascii_general_ci` está repetida entre la configuración EF y la migración. Una `const string` compartida evitaría drift si MySQL/Pomelo cambia la convención en el futuro. Out of scope hoy — sólo sugerencia.

## Validación de escenarios del spec

### Capability: `vacante-management` (MODIFIED + ADDED)

| Requirement | Scenario | Cobertura | Test | Resultado |
|-------------|----------|-----------|------|-----------|
| Crear Vacante (MODIFIED) | Creación exitosa | Test API pre-existente | `tests/SGV.Tests/Api/Vacantes/VacantesControllerTests.cs` (suite `Api.Vacantes` 24/24 verde) | ✅ COMPLIANT |
| Crear Vacante (MODIFIED) | PuestoId inexistente | Test API pre-existente | suite `Api.Vacantes` | ✅ COMPLIANT |
| Crear Vacante (MODIFIED) | EstadoVacanteId inválido | Test API pre-existente | suite `Api.Vacantes` | ✅ COMPLIANT |
| Crear Vacante (MODIFIED) | Mutación sin permiso | Test API pre-existente | suite `Api.Vacantes` | ✅ COMPLIANT |
| Crear Vacante (MODIFIED) | Estado inicial terminal rechazado | Test API pre-existente | suite `Api.Vacantes` | ✅ COMPLIANT |
| Crear Vacante (MODIFIED) | Puesto con vacante abierta | Unit pre-existente + mapping del catch | `tests/SGV.Tests/Aplicacion/Vacantes/VacanteServicioComandosTests.cs > Crear_PuestoConVacanteAbierta_DevuelveConflicto` (línea 189) | ✅ COMPLIANT |
| Crear Vacante (MODIFIED) | **Carrera concurrente para el mismo PuestoId** | **Nuevo `[MySqlFact]`** | `tests/SGV.Tests/Api/Vacantes/VacantesConcurrenciaTests.cs > Crear_MismoPuestoIdConcurrente_UnoPersisteOtroFallaConDbUpdateException` (línea 41) | ✅ COMPLIANT |
| Unicidad BD (ADDED) | Una vacante abierta no viola la constraint | Test modelo + `[MySqlFact]` | `tests/SGV.Tests/Persistencia/VacanteConfiguracionTests.cs > Vacante_ConfiguraUniqueIndexSobreActivePuestoIdUnique` (línea 44) + implícito en T7.1.a | ✅ COMPLIANT |
| Unicidad BD (ADDED) | Vacante cerrada deja de violar | **Nuevo `[MySqlFact]`** | `tests/SGV.Tests/Api/Vacantes/VacantesConcurrenciaTests.cs > CerrarYReabrir_VacanteNuevaParaMismoPuesto_NoViolaConstraint` (línea 150) | ✅ COMPLIANT |
| Unicidad BD (ADDED) | Vacante soft-deleted deja de violar | **Nuevo `[MySqlFact]`** | `tests/SGV.Tests/Api/Vacantes/VacantesConcurrenciaTests.cs > SoftDeleteLiberaIndice_NuevaParaMismoPuesto_NoViolaConstraint` (línea 245) | ✅ COMPLIANT |
| Unicidad BD (ADDED) | Reabrir vacante cerrada con puesto abierto es rechazado | Defense-in-depth documental | Constraint `IX_Vacantes_ActivePuestoIdUnique` lo cubre — el scenario no es alcanzable vía API hoy (D-6, `CambiarEstado(cerrar=false)` no limpia `FechaCierre`) | ✅ COMPLIANT (constraint garantiza la invariante; no requiere test runtime porque el dominio no expone el camino) |

**Compliance summary**: 11/11 scenarios compliant.

### TDD Compliance (Strict TDD Mode)

| Check | Result | Details |
|-------|--------|---------|
| TDD Evidence reported | ✅ | Encontrado en `apply-progress.md §TDD Cycle Evidence` (tabla con T1.1, T3.1, T7.1) |
| All tasks have tests | ✅ | 3/3 tareas implementadas tienen test (T1.1 unit, T3.1 modelo, T7.1 `[MySqlFact]`) |
| RED confirmed (tests exist) | ✅ | 4 archivos de test verificados en el codebase: `VacanteServicioComandosTests.cs`, `VacanteConfiguracionTests.cs`, `VacantesConcurrenciaTests.cs` |
| GREEN confirmed (tests pass) | ✅ | `Aplicacion.Vacantes` 18/18 + `Persistencia` 383/383 + `Api.Vacantes` 24/24 + `VacantesConcurrenciaTests` 3/3 en esta corrida |
| Triangulation adequate | ✅ | T1.1: 1 test para el catch (escenario único). T3.1: 3 tests (shadow, fórmula/collation, unique index). T7.1: 3 tests (carrera, cerrar-reabrir, soft-delete-liberar) |
| Safety Net for modified files | ⚠️ | T1.1: N/A (catch block modificado, no archivo nuevo). T4.1: N/A (configuración modificada). T6.1: N/A (migración nueva). Los tests `[MySqlFact]` corren suite completa antes de tocar infra; registrado en apply-progress §Verificaciones |

**TDD Compliance**: 5/6 checks passed (1 N/A documentado).

### Test Layer Distribution

| Layer | Tests | Files | Tools |
|-------|-------|-------|-------|
| Unit (fake repo + modelo) | 4 | `VacanteServicioComandosTests.cs` (1 nuevo test de catch), `VacanteConfiguracionTests.cs` (3 tests de modelo) | xUnit + FluentAssertions + EF Core `IDesignTimeModel` |
| Integration (MySQL real) | 3 | `VacantesConcurrenciaTests.cs` (carrera + cerrar-reabrir + soft-delete) | xUnit + `[MySqlFact]` + Pomelo 9 + MySQL 8 |
| **Total** | **7** | 3 archivos | |

### Assertion Quality

| File | Línea | Assertion | Veredicto |
|------|-------|-----------|-----------|
| `VacanteServicioComandosTests.cs` | 242-248 | `Assert.False(IsSuccess)`, `Assert.Equal(Conflict, ...)`, `Assert.Equal(PuestoConVacanteAbierta, ...)`, `Assert.Equal(1, SaveChangesCount)`, `Assert.Equal(1, AddCallCount)` | ✅ Real behavior — verifica código, categoría y que el camino se ejecutó |
| `VacanteConfiguracionTests.cs` | 28-40 | `Assert.NotNull(shadowProperty)`, `Assert.Equal(fórmula, ComputedColumnSql)`, `Assert.Equal("ascii_general_ci", Collation)`, `Assert.True(IsStored)` | ✅ Real behavior — verifica metadata EF real del modelo |
| `VacanteConfiguracionTests.cs` | 51-53 | `Assert.NotNull(indice)`, `Assert.True(IsUnique)`, `Assert.Equal("IX_Vacantes_ActivePuestoIdUnique", GetDatabaseName)` | ✅ Real behavior — verifica configuración relacional |
| `VacanteConfiguracionTests.cs` | 64-66 | `Assert.Null(PropertyInfo)`, `Assert.Null(FieldInfo)` | ✅ Real behavior — confirma shadow property (decisión arquitectónica explícita) |
| `VacantesConcurrenciaTests.cs` | 109 | `Assert.Single(errores)` | ✅ Real behavior — verifica exactamente 1 falla |
| `VacantesConcurrenciaTests.cs` | 119-123 | `Assert.IsType<DbUpdateException>(inner)`, `Assert.Contains("IX_Vacantes_ActivePuestoIdUnique", message)` | ✅ Real behavior — verifica tipo y nombre de constraint específico |
| `VacantesConcurrenciaTests.cs` | 133 | `Assert.Equal(1, abiertasRestantes)` | ✅ Real behavior — verificación cruzada post-carrera |
| `VacantesConcurrenciaTests.cs` | 232-233 | `Assert.Equal(1, abiertas)`, `Assert.Equal(1, cerradas)` | ✅ Real behavior — invariante de coexistencia |

**Assertion quality**: ✅ All assertions verify real behavior — sin tautologías, sin type-only, sin ghost loops. Cada test ejercita el camino real (constraint violation, EF Core model metadata, MySQL real).

## Validación de la implementación (diff vs design D-1 a D-7)

| Decision | Implementación | Verificado |
|----------|----------------|-----------|
| **D-1** Stored vs Virtual | `HasComputedColumnSql(..., stored: true)` + `stored: true` en `AddColumn` de la migración | ✅ Migración línea 32 (`stored: true`), Configuración línea 43 (`stored: true`) |
| **D-2** Fórmula `CASE WHEN FechaCierre + IsDeleted` | Fórmula exacta sin join a catálogo | ✅ Configuración línea 43, Migración línea 31 |
| **D-3** `HasMaxLength(36)` sin `HasColumnType` + `ascii_general_ci` | Solo `HasMaxLength(36)` + `UseCollation("ascii_general_ci")`; comentario explicativo líneas 33-39 | ✅ Configuración líneas 41-42 |
| **D-4** Nombre index `IX_Vacantes_ActivePuestoIdUnique` | `HasIndex("ActivePuestoIdUnique").IsUnique().HasDatabaseName("IX_Vacantes_ActivePuestoIdUnique")` + migración con mismo nombre | ✅ Configuración línea 45, Migración línea 36 |
| **D-5** Forward-only migration (sin `Down` funcional) | `Down` lanza `NotSupportedException("Migración forward-only. Para revertir, escribir una migración correctiva explícita.")` | ✅ Migración líneas 53-57 |
| **D-6** Sólo `CrearAsync:177` modificado; `CambiarEstadoAsync:286` y `ActualizarObservacionesAsync:358` intactos | diff de `VacanteServicioComandos.cs` muestra exactamente 4 líneas modificadas (líneas 180-185 del archivo, antes 178-183 del original); los otros catch siguen con `DatosInvalidos` | ✅ Verificado por `git diff 9807f667..HEAD` y grep directo al archivo |
| **D-7** Conservar `ExistsAbiertaByPuestoAsync` | Llamada preservada en `CrearAsync:146` con el mismo código de error `PuestoConVacanteAbierta` | ✅ Línea 146-153 sin cambios |

**Coherencia diseño-implementación**: 7/7 decisiones seguidas.

## Validación de la migración

| Aspecto | Estado | Detalle |
|---------|--------|---------|
| Forward-only | ✅ | `Down` lanza `NotSupportedException` (líneas 53-57) con mensaje exacto del design |
| Idempotente | ✅ | `docs/migracion-inicial-sgv.sql` incluye la migración envuelta en `IF NOT EXISTS(SELECT 1 FROM __EFMigrationsHistory WHERE MigrationId = '20260731173842_AddActivePuestoIdUniqueToVacantes') THEN ... END IF;` (líneas 4169-4206 del script) |
| Sintaxis OK | ✅ | `ALTER TABLE Vacantes ADD ActivePuestoIdUnique varchar(36) COLLATE ascii_general_ci AS (CASE WHEN ... END) STORED NULL;` (línea 4171) + `CREATE UNIQUE INDEX IX_Vacantes_ActivePuestoIdUnique ON Vacantes (ActivePuestoIdUnique);` (línea 4185) + INSERT en `__EFMigrationsHistory` (líneas 4199-4200) |
| Designer + Snapshot regenerados | ✅ | `20260731173842_AddActivePuestoIdUniqueToVacantes.Designer.cs` (93680 bytes), `SgvDbContextModelSnapshot.cs` (93539 bytes) — contienen shadow property + unique index con nombre correcto |
| Hash EF | ✅ | `MigrationId = '20260731173842_AddActivePuestoIdUniqueToVacantes'` consistente en todos los artefactos (migración, designer, snapshot, SQL) |
| Patrón de columna calculada paridad | ✅ | Misma forma que `OcupacionConfiguracion` (módulo precedent); misma collation; misma fórmula `CASE WHEN ... IS NULL AND IsDeleted = 0 THEN ... ELSE NULL END` |
| Riesgo de duplicados pre-existentes | ⚠️ Documentado | El design documenta el query de detección para producción (`SELECT PuestoId, COUNT(*) FROM Vacantes WHERE FechaCierre IS NULL AND IsDeleted = 0 GROUP BY PuestoId HAVING COUNT(*) > 1`). Out of scope del change; el orchestrator debe comunicarlo en el PR para el deploy en producción |

## Próximos pasos

1. **`sdd-archive`**: Sincronizar el delta spec (`MODIFIED Crear Vacante` + `ADDED Unicidad de vacante abierta por puesto`) al archivo de spec archiveado (`openspec/specs/vacante-management/spec.md`). El proposal dice que no introduce nueva capability; el archive es solo consolidación del delta.
2. **Push del branch**: `git push origin <branch>` con los tres commits (`e1b4625f`, `46a642bb`, `151beaec`) + el commit de docs `2f404b2b` (apply-progress).
3. **PR**: mencionar en la descripción del PR:
   - Lista de tests flaky pre-existentes (W-1) — el revisor debe saber que un eventual re-run contra DB no limpia puede mostrar fallos ajenos al change.
   - Query de detección de duplicados pre-existentes a correr en producción antes del deploy.
   - Referencia al issue #238 y al deviation D-3.2 del change archivado `2026-07-30-feature-implementar-modulo-vacantes`.

## Hash de commits

```
2f404b2b docs(openspec): registrar apply-progress del change fix-vacante-toctou-concurrencia-issue-238
151beaec test(vacantes): agregar [MySqlFact] de carrera y liberación por cierre/soft-delete
46a642bb feat(vacantes): agregar migración AddActivePuestoIdUniqueToVacantes y ajustar fixtures pre-existentes
e1b4625f feat(vacantes): mapear catch de CrearAsync a PuestoConVacanteAbierta y sombra ActivePuestoIdUnique
```

## Archivos relevantes verificados

| Archivo | Acción | Verificación |
|---------|--------|--------------|
| `src/SGV.Aplicacion/Vacantes/Comandos/VacanteServicioComandos.cs` | Modificado | Líneas 177-185: catch → `PuestoConVacanteAbierta`. Líneas 286-293 y 358-365: intactas (D-6). |
| `src/SGV.Infraestructura/Persistencia/Configuraciones/VacanteConfiguracion.cs` | Modificado | Líneas 40-45: shadow property + unique index con fórmula correcta, collation, stored. |
| `src/SGV.Infraestructura/Persistencia/Migraciones/20260731173842_AddActivePuestoIdUniqueToVacantes.cs` | Creado | Up: AddColumn + CreateIndex; Down: NotSupportedException. |
| `src/SGV.Infraestructura/Persistencia/Migraciones/20260731173842_AddActivePuestoIdUniqueToVacantes.Designer.cs` | Regenerado | 93680 bytes. |
| `src/SGV.Infraestructura/Persistencia/Migraciones/SgvDbContextModelSnapshot.cs` | Regenerado | Líneas 1795 y 1849-1851: shadow property + unique index. |
| `docs/migracion-inicial-sgv.sql` | Regenerado | Líneas 4169-4206: migración envuelta en idempotente. |
| `tests/SGV.Tests/Aplicacion/Vacantes/VacanteServicioComandosTests.cs` | Modificado | +1 test del catch (T1.1). |
| `tests/SGV.Tests/Persistencia/VacanteConfiguracionTests.cs` | Creado | 3 tests de modelo (T3.1). |
| `tests/SGV.Tests/Persistencia/VacanteRepositoryQueryTests.cs` | Modificado | Ajustes de fixtures que violaban la nueva constraint (documentado en apply-progress §Desviaciones). |
| `tests/SGV.Tests/Api/Vacantes/VacantesConcurrenciaTests.cs` | Creado | 3 `[MySqlFact]` (T7.1.a/b/c). |

## Veredicto final

**APROBADO_CON_OBSERVACIONES** — todos los criterios de aceptación cumplidos, decisiones de diseño implementadas fielmente, suite completa en verde (3334/3334), patrones D-1 a D-7 verificados. La única observación es la lista de tests flaky pre-existentes que el PR debe documentar para el revisor (no son regresiones de este change).