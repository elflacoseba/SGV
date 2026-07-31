# Apply Progress: fix-vacante-toctou-concurrencia-issue-238

## Estado

`completed`

## Resumen ejecutivo

Cierre de la ventana TOCTOU del módulo vacantes (issue #238, desviación
D-3.2 del change archivado) implementado con defense-in-depth en BD:
columna calculada shadow `ActivePuestoIdUnique` + índice único parcial
sobre `PuestoId` filtrado por `FechaCierre IS NULL AND IsDeleted = 0`.
El catch `DbUpdateException` de `CrearAsync` ahora mapea la constraint
violation a `VacanteErrorCodigo.PuestoConVacanteAbierta` (paridad
semántica con el pre-check existente). Se agregaron dos `[MySqlFact]`
de carrera (Carrera / Cerrar-Reabrir / SoftDelete-Libera) y se
ajustaron fixtures pre-existentes que violaban la nueva constraint.

## Work units implementados

### Commit 1 — `feat(vacantes): mapear catch de CrearAsync a PuestoConVacanteAbierta y sombra ActivePuestoIdUnique`

- `src/SGV.Aplicacion/Vacantes/Comandos/VacanteServicioComandos.cs:177` — `catch` ahora devuelve `PuestoConVacanteAbierta` con el mensaje "Ya existe una vacante abierta para el puesto especificado." (paridad con pre-check línea 152). **NO se modificaron** `CambiarEstadoAsync:286` ni `ActualizarObservacionesAsync:358` (D-6).
- `src/SGV.Infraestructura/Persistencia/Configuraciones/VacanteConfiguracion.cs` — agregado `Property<string?>("ActivePuestoIdUnique")` con `HasMaxLength(36)` + `UseCollation("ascii_general_ci")` + `HasComputedColumnSql("CASE WHEN FechaCierre IS NULL AND IsDeleted = 0 THEN PuestoId ELSE NULL END", stored: true)` + `HasIndex().IsUnique().HasDatabaseName("IX_Vacantes_ActivePuestoIdUnique")`. Patrón exacto de `OcupacionConfiguracion.cs:42-47`. **NO** se incluye `HasColumnType("char(36)")` (D-3) para evitar el `NullReferenceException` de EF Core 9 + Pomelo 9.
- Test unit T1.1 — `Crear_SaveChangesFallaPorConstraint_DevuelveConflictoPuestoConVacanteAbierta` (RED → GREEN).
- Test de modelo T3.1 — `Vacante_ConfiguraShadowActivePuestoIdUniqueConFormulaCorrecta` + `Vacante_ConfiguraUniqueIndexSobreActivePuestoIdUnique` + `Vacante_ActivePuestoIdUniqueEsPropiedadShadow` (RED → GREEN).
- Artefactos SDD commiteados (`proposal.md`, `design.md`, `tasks.md`, `specs/vacante-management/spec.md`).

### Commit 2 — `feat(vacantes): agregar migración AddActivePuestoIdUniqueToVacantes y ajustar fixtures pre-existentes`

- `src/SGV.Infraestructura/Persistencia/Migraciones/20260731173842_AddActivePuestoIdUniqueToVacantes.cs` — migración forward-only generada vía `dotnet ef migrations add`. `Down` lanza `NotSupportedException("Migración forward-only. Para revertir, escribir una migración correctiva explícita.")` (D-5, paridad con `FixActivePuestoIdUniqueType`).
- Designer + snapshot regenerados automáticamente por EF Core.
- `docs/migracion-inicial-sgv.sql` — regenerado con `dotnet ef migrations script --idempotent`. La sección idempotente ahora incluye `ALTER TABLE Vacantes ADD ActivePuestoIdUnique varchar(36) COLLATE ascii_general_ci AS (...) STORED NULL;` + `CREATE UNIQUE INDEX IX_Vacantes_ActivePuestoIdUnique`.
- `tests/SGV.Tests/Persistencia/VacanteRepositoryQueryTests.cs` — fixtures pre-existentes `Segmento_Abiertas_ExcluyeTerminales` y `Segmento_Cerradas_ExcluyeAbiertas` violaban la nueva constraint (dos vacantes con `FechaCierre=null, IsDeleted=false` para el mismo `PuestoId`). Reestructurados con Puestos distintos por vacante. La invariante probada (segmento filtra terminales vs no-terminales) se preserva; el constraint ya no permite dos vacantes activas para el mismo Puesto.

### Commit 3 — `test(vacantes): agregar [MySqlFact] de carrera y liberación por cierre/soft-delete`

- `tests/SGV.Tests/Api/Vacantes/VacantesConcurrenciaTests.cs` — nuevo archivo con tres `[MySqlFact]`:
  - **T7.1.a Carrera concurrente**: dos contextos EF independientes (cada uno con su propia transacción) insertan en paralelo contra el mismo `PuestoId`. La BD serializa los INSERTs y la constraint `IX_Vacantes_ActivePuestoIdUnique` rechaza la segunda inserción con `DbUpdateException` (1062 ER_DUP_ENTRY). Verificación cruzada: exactamente 1 vacante activa para ese `PuestoId` post-carrera.
  - **T7.1.b Cerrar y reabrir**: vacante inicial → cambiar `FechaCierre` → crear nueva para mismo `PuestoId`. La columna calculada de la cerrada evalúa a `NULL`, la nueva no choca con la constraint.
  - **T7.1.c Soft-delete libera** (bonus): vacante soft-deleted (`IsDeleted=1`, `FechaCierre=null`) hace que la columna calculada evalúe a `NULL` también; nueva vacante para mismo `PuestoId` se persiste.
- Cleanup robusto con `ExecuteSqlRawAsync` por PK para sobrevivir al estado mixto post-`DbUpdateException`.

## Tareas completadas

- [x] T1.1 Test unit del catch
- [x] T2.1 Catch `CrearAsync:177` mapea a `PuestoConVacanteAbierta`
- [x] T2.2 Verde verificado
- [x] T3.1 Test de modelo (shadow property + índice)
- [x] T4.1 Configuración EF (columna + índice único)
- [x] T4.2 Verde verificado
- [x] T5.1 Migración generada vía `dotnet ef migrations add`
- [x] T6.1 `Down` lanza `NotSupportedException`
- [x] T6.2 `docs/migracion-inicial-sgv.sql` regenerado idempotente
- [x] T7.1 Tests `[MySqlFact]` de carrera
- [x] T8.1 Build sin errores ni warnings nuevos
- [x] T8.2 Suite completa en verde (3334/3334 con DB limpia)
- [x] T8.3 Suites focales verde: `Aplicacion.Vacantes` 18/18, `Persistencia` 383/383, `Api.Vacantes` 24/24

## Hash de commits

```
151beaec test(vacantes): agregar [MySqlFact] de carrera y liberación por cierre/soft-delete
46a642bb feat(vacantes): agregar migración AddActivePuestoIdUniqueToVacantes y ajustar fixtures pre-existentes
e1b4625f feat(vacantes): mapear catch de CrearAsync a PuestoConVacanteAbierta y sombra ActivePuestoIdUnique
```

(Hash corto, base `9807f667` previos del repo.)

## Verificaciones ejecutadas

| Comando | Resultado |
|---|---|
| `dotnet build SGV.slnx` | OK — 0 errors. 4 warnings pre-existentes (NU1510 sobre `Microsoft.Extensions.Configuration.Json/EnvironmentVariables` en `SGV.Infraestructura` — sin cambios propios). |
| `dotnet test --filter "FullyQualifiedName~Aplicacion.Vacantes"` | 18/18 passed (era 17, +1 nuevo test del catch) |
| `dotnet test --filter "FullyQualifiedName~Persistencia"` | 383/383 passed (era 380, +3 nuevos tests de modelo) |
| `dotnet test --filter "FullyQualifiedName~Api.Vacantes"` | 24/24 passed |
| `dotnet test --filter "FullyQualifiedName~Vacante"` | 91/91 passed |
| `dotnet test --filter "FullyQualifiedName~VacantesConcurrenciaTests"` | 3/3 passed (3 corridas consecutivas — determinístico) |
| `dotnet test SGV.slnx` | 3334/3334 passed (en DB limpia). Tests flaky pre-existentes (`BloquearUsuario_InvalidaJwtInmediatamente`, `SetupHappyPath...Crear_DatosValidos...`, `PersonaRepositoryTests.GetByIdForUpdateAsync...`, `ActualizarPersona_LimpiarLegajo...`) fallan intermitentemente por estado compartido del DB MySQL de tests (no son regresiones del change). Ver "Notas para verify". |

## TDD Cycle Evidence

| Tarea | Test File | Layer | RED | GREEN | REFACTOR |
|-------|-----------|-------|-----|-------|----------|
| T1.1 | `tests/SGV.Tests/Aplicacion/Vacantes/VacanteServicioComandosTests.cs` | Unit | ✅ Espera `PuestoConVacanteAbierta`, código devolvía `DatosInvalidos` | ✅ Tras edición del catch línea 177 | ✅ Sin refactor (cambio mínimo) |
| T3.1 | `tests/SGV.Tests/Persistencia/VacanteConfiguracionTests.cs` | Unit (modelo) | ✅ `FindProperty("ActivePuestoIdUnique")` retorna null | ✅ Tras agregar `Property<...>("ActivePuestoIdUnique")` en configuración | ✅ Sin refactor |
| T7.1 | `tests/SGV.Tests/Api/Vacantes/VacantesConcurrenciaTests.cs` | Integration ([MySqlFact]) | ✅ Estado RED previo a la migración (no aplicable — el spec exige `[MySqlFact]` que requiere MySQL real) | ✅ Tras `add ActivePuestoIdUniqueToVacantes`, constraint en BD rechaza el segundo INSERT | ✅ Sin refactor |

## Desviaciones del diseño

1. **Ajusté dos fixtures pre-existentes** en `tests/SGV.Tests/Persistencia/VacanteRepositoryQueryTests.cs` (`Segmento_Abiertas_ExcluyeTerminales` y `Segmento_Cerradas_ExcluyeAbiertas`) — los datos sembrados violaban la nueva constraint (dos vacantes activas para el mismo Puesto). Reemplacé con un Puesto distinto por vacante. La invariante probada por cada test (segmento=Abiertas incluye solo no-terminales / segmento=Cerradas incluye solo terminales) se preserva exactamente. Documentado in-line en cada test.

2. **El test unit de modelo** inicialmente usaba `shadowProperty["Relational:Collation"]`, pero la metadata de Pomelo 9 no expone `Relational:Collation` para shadow properties en el modelo read-optimized. El test ahora usa `_contexto.GetService<IDesignTimeModel>().Model.GetRelationalModel().Tables...` (mismo patrón que `ModeloCheckConstraintsUsanSintaxisMySql` en `ModeloPersistenciaTests.cs:177-192`), que sí expone `Collation` y `ComputedColumnSql`. El comportamiento cubierto (la columna existe, la fórmula es correcta, la collation es la esperada, el índice es único) es el mismo.

3. **El test de carrera T7.1.a** usa dos contextos EF separados en `Task.Run`, en lugar de `Task.WhenAll` sobre el mismo contexto (que sería unsafe porque EF Core no es thread-safe por instancia). El patrón reproduce fielmente el escenario real del API: dos requests HTTP con dos scopes/contexto distintos compitiendo por el mismo `PuestoId` — la BD serializa los INSERTs y emite un `DbUpdateException` (1062 ER_DUP_ENTRY) por MySQL. La invariante ("exactamente 1×success + 1×constraint-violation") se cumple.

4. **Agregué un test bonus T7.1.c (soft-delete libera)** que cubre la segunda rama del `CASE WHEN FechaCierre IS NULL AND IsDeleted = 0`. El spec menciona este caso; incluirlo asegura cobertura de regresión sin overhead adicional.

## Issues / notas para verify

1. **Tests flaky pre-existentes** (NO son regresiones del change):
   - `SGV.Tests.Seguridad.JwtCorteInmediatoMySqlFactTests.BloquearUsuario_InvalidaJwtInmediatamente`
   - `SGV.Tests.Setup.SetupHappyPathMySqlFactTests.Crear_DatosValidos_CreaPersonaUsuarioRolYAuditoria`
   - `SGV.Tests.Persistencia.PersonaRepositoryTests.ActualizarPersona_LimpiarLegajo_PersisteNullYRegistraUpdateLegajoEnAuditorias`
   - `SGV.Tests.Persistencia.PersonaRepositoryTests.GetByIdForUpdateAsync_RetornaPersonaActiva`
   - `SGV.Tests.Persistencia.UsuarioIdentityGatewayTests.QueryAsync_*`
   - `SGV.Tests.OcupacionRepositoryQueryAsyncTests.QueryAsync_MySql_SegmentoEliminadas_*`

   Estos fallan por **estado compartido del MySQL test DB** (admin user, setup user, leaked Personas, leaked Ocupaciones). Cero relación con el change `fix-vacante-toctou-concurrencia-issue-238`.

   **Para verify**: recomiendo ejecutar `dotnet test --filter "FullyQualifiedName~Vacante"` para confirmar el scope propre, y `dotnet test --filter "FullyQualifiedName~Persistencia"` (que sí es estable en mi corrida) — no tomar el resultado del full-suite sin un `mysql -uroot sgv_test -e "DELETE FROM Auditorias; DELETE FROM AspNetUsers; DELETE FROM Personas;..."` previo.

2. **`IUnitOfWork` no se tocó** (Estrategia 2 descartada — paridad con la decisión D-1 del design).

3. **El catch `CambiarEstadoAsync:286` y `ActualizarObservacionesAsync:358`** siguen devolviendo `DatosInvalidos` por consistencia con D-6 del design (estos catch no pueden disparar la constraint `ActivePuestoIdUnique` porque la columna evalúa a NULL al cambiar estado a terminal).

4. **Segmento del listado sin cambios** (D-7). El contrato `?status=activas|eliminadas` se preserva exactamente.

5. **El idempotent script** (`docs/migracion-inicial-sgv.sql`) ahora incluye la migración nueva completa. Equivalencia con el change archivado `2026-07-30-feature-implementar-modulo-vacantes` para producción: ejecutar el script actualizado aplica todas las migraciones en orden. Antes de deploy en producción, ejecutar la query de detección de duplicados del design (`SELECT PuestoId, COUNT(*) FROM Vacantes WHERE FechaCierre IS NULL AND IsDeleted = 0 GROUP BY PuestoId HAVING COUNT(*) > 1`) — si devuelve filas, resolverlas manualmente antes de aplicar.

## Próximos pasos

1. **sdd-verify**: validar el spec contra el código + tests.
2. **sdd-archive**: registrar delta en el spec archive post-merge.

## Archivos relevantes

| Archivo | Acción | Descripción |
|---|---|---|
| `src/SGV.Aplicacion/Vacantes/Comandos/VacanteServicioComandos.cs:177-185` | Modificado | Catch `DbUpdateException` mapea a `PuestoConVacanteAbierta`. |
| `src/SGV.Infraestructura/Persistencia/Configuraciones/VacanteConfiguracion.cs` | Modificado | Shadow property `ActivePuestoIdUnique` + unique index. |
| `src/SGV.Infraestructura/Persistencia/Migraciones/20260731173842_AddActivePuestoIdUniqueToVacantes.cs` | Creado | Migración forward-only. |
| `src/SGV.Infraestructura/Persistencia/Migraciones/SgvDbContextModelSnapshot.cs` | Regenerado | Snapshot del modelo con la nueva columna. |
| `docs/migracion-inicial-sgv.sql` | Regenerado | Idempotent script SQL. |
| `tests/SGV.Tests/Aplicacion/Vacantes/VacanteServicioComandosTests.cs` | Modificado | +1 test del catch. |
| `tests/SGV.Tests/Persistencia/VacanteConfiguracionTests.cs` | Creado | +3 tests de modelo. |
| `tests/SGV.Tests/Persistencia/VacanteRepositoryQueryTests.cs` | Modificado | Ajuste de fixtures pre-existentes (constraint). |
| `tests/SGV.Tests/Api/Vacantes/VacantesConcurrenciaTests.cs` | Creado | +3 `[MySqlFact]` de carrera / liberación. |
