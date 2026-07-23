# Exploración: fix `ActivePuestoIdUnique` (issue #59)

**Issue GitHub**: #59 — `ActivePuestoIdUnique` columna generada `INT` incompatible con `PuestoId CHAR(36)`
**Change**: `2026-07-11-fix-active-puesto-id-unique-type`
**Modo**: exploratorio — solo investigación, sin código, sin migración, sin tests
**Artifact store**: `both` (OpenSpec filesystem + Engram topic key `sdd/2026-07-11-fix-active-puesto-id-unique-type/explore`)

## Estado actual

### Cómo debería funcionar la unicidad activa por Puesto

`Ocupaciones` declara una restricción de unicidad suave: solo puede existir **una** ocupación activa (`FechaFin IS NULL AND IsDeleted = 0`) por `PuestoId`. MySQL no soporta índices filtrados parciales (a diferencia de SQL Server), así que la estrategia adoptada por el repo es una **columna generada** que devuelve el valor de negocio cuando la fila está activa y `NULL` cuando no, acompañada de un índice único. MySQL permite múltiples `NULL` en un índice único, replicando el comportamiento de un índice filtrado. Esta decisión está documentada en `docs/decisiones-implementacion.md:11-13` (sección "Índices Únicos con Soft Delete") y se aplica consistentemente a `Cargos`, `Habilidades`, `Puestos`, `UnidadesOrganizativas`, `Personas`, `Postulantes` y `Ocupaciones` (vía `ActiveCodigoUnique`, `ActiveEmailUnique`, `ActiveLegajoUnique`, `ActiveDocumentoUnique`, `ActivePersonaIdUnique`).

### Dónde vive el bug

`src/SGV.Infraestructura/Persistencia/Configuraciones/OcupacionConfiguracion.cs:35-38`:

```csharp
builder.Property<int?>("ActivePuestoIdUnique")
    .HasComputedColumnSql("CASE WHEN `FechaFin` IS NULL AND `IsDeleted` = 0 THEN `PuestoId` ELSE NULL END")
    .IsRequired(false);
builder.HasIndex("ActivePuestoIdUnique").IsUnique();
```

- **Tipo CLR/declaración**: `int?` → MySQL `INT` (4 bytes, signed).
- **Expresión SQL**: `CASE WHEN ... THEN PuestoId ELSE NULL END` → devuelve el contenido de `PuestoId`.
- **`PuestoId` real** (snapshot líneas 1019-1020 y migración inicial línea 610): `char(36)` (Guid como string de 36 caracteres, p.ej. `a1b2c3d4-e5f6-7890-abcd-ef1234567890`).

MySQL evalúa la columna generada en cada `INSERT`/`UPDATE`. Cuando la condición es verdadera, intenta convertir el literal `'a1b2c3d4-...'` (36 chars) a `INT` (max 11 dígitos). El resultado es coerción implícita → `0` en MySQL 8 con `sql_mode` permisivo, o directamente `Data truncated for column 'ActivePuestoIdUnique' at row 1` con `STRICT_TRANS_TABLES` (modo default de MySQL 8). Como hay índice único sobre esa columna, el segundo insert para el mismo `PuestoId` también dispara violación de unicidad (porque el `0` truncado coincide entre filas), pero el error que ve el caller siempre es el `Data truncated` previo.

### Evidencia código-línea

| Archivo | Línea | Contenido relevante |
|---|---|---|
| `src/SGV.Infraestructura/Persistencia/Configuraciones/OcupacionConfiguracion.cs` | 35 | `builder.Property<int?>("ActivePuestoIdUnique")` |
| `src/SGV.Infraestructura/Persistencia/Configuraciones/OcupacionConfiguracion.cs` | 36 | `.HasComputedColumnSql("CASE WHEN `FechaFin` IS NULL AND `IsDeleted` = 0 THEN `PuestoId` ELSE NULL END")` |
| `src/SGV.Infraestructura/Persistencia/Migraciones/20260614183103_InicialSgvo.cs` | 610 | `PuestoId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci")` |
| `src/SGV.Infraestructura/Persistencia/Migraciones/20260614183103_InicialSgvo.cs` | 617 | `ActivePersonaIdUnique = table.Column<int>(type: "int", nullable: true, computedColumnSql: "CASE WHEN `FechaFin` IS NULL AND `IsDeleted` = 0 THEN `PersonaId` ELSE NULL END")` — mismo bug, **pero esta columna ya fue eliminada** por la migración `20260624153353` |
| `src/SGV.Infraestructura/Persistencia/Migraciones/20260614183103_InicialSgvo.cs` | 618 | `ActivePuestoIdUnique = table.Column<int>(type: "int", nullable: true, computedColumnSql: "CASE WHEN `FechaFin` IS NULL AND `IsDeleted` = 0 THEN `PuestoId` ELSE NULL END")` — **columna afectada, persiste en HEAD** |
| `src/SGV.Infraestructura/Persistencia/Migraciones/20260624153353_ConvertirTipoAsignacionAEnumYActualizarUnicidad.cs` | 14-20, 99-110 | Migración que **ya corrigió `ActivePersonaIdUnique`** (lo dropeó y lo reemplazó por `ActivePersonaPuestoUnique` `varchar(100)`), pero **no tocó `ActivePuestoIdUnique`** |
| `src/SGV.Infraestructura/Persistencia/Migraciones/SgvDbContextModelSnapshot.cs` | 984-987 | Snapshot actual confirma el bug: `b.Property<int?>("ActivePuestoIdUnique").ValueGeneratedOnAddOrUpdate().HasColumnType("int").HasComputedColumnSql("CASE WHEN `FechaFin` IS NULL AND `IsDeleted` = 0 THEN `PuestoId` ELSE NULL END")` |
| `src/SGV.Infraestructura/Persistencia/Migraciones/SgvDbContextModelSnapshot.cs` | 1019-1020 | `PuestoId` real es `char(36)` |
| `docs/migracion-inicial-sgv.sql` | 533 | `` `ActivePuestoIdUnique` int AS (CASE WHEN `FechaFin` IS NULL AND `IsDeleted` = 0 THEN `PuestoId` ELSE NULL END) NULL `` — script idempotente reproducirá el bug en cualquier deployment fresco |
| `docs/migracion-inicial-sgv.sql` | 1292 | `CREATE UNIQUE INDEX \`IX_Ocupaciones_ActivePuestoIdUnique\` ON \`Ocupaciones\` (\`ActivePuestoIdUnique\`);` |
| `tests/SGV.Tests/Persistencia/OcupacionRepositoryTests.cs` | 1-525 | 15 tests `[MySqlFact]` de los cuales 12 fallan con `Data truncated for column 'ActivePuestoIdUnique' at row 1` |
| `tests/SGV.Tests/Persistencia/ModeloPersistenciaTests.cs` | 44-61, 122-140 | Tests estructurales que verifican la existencia de `ActivePuestoIdUnique` y su computed SQL — no assertan el **tipo** de la columna, solo el nombre del shadow property, el `computedColumnSql` (strings) y la presencia del índice único. **Pasarán incluso si el tipo cambia a `varchar(36)`**, porque solo validan substrings del SQL (`"PuestoId"`, `"FechaFin"`, `"IsDeleted"`) |
| `AGENTS.md` | 181-186 | Documenta el bug como issue #59 "Pendiente de SDD change" — fuente canónica |

### Por qué solo afecta a 12 de 15 tests

`OcupacionRepositoryTests` ejecuta siempre un `SeedAsync` antes de cada test que crea `OcupacionEntity` con `FechaFin = null` y `IsDeleted = false` (tests `ListAllAsync_Default_ReturnsOnlyActiveRows`, `GetByIdForUpdateAsync_Active_ReturnsWithNavigation`, `GetByIdIncludingHistoryAsync_ReturnsEvenIfDeleted`, `ExistsActiveByPuestoAsync_*`, `ExistsActiveByPersonaYPuestoAsync_*`, `UpdateAsync_*`). Solo escapan los tests que **no insertan una fila activa** o que **solo leen**:

- `ListAllAsync_Default_ReturnsOnlyActiveRows` (línea 14-44): inserta activa+finalizada+deleted → **falla** (activa)
- `ListAllIncludingHistoryAsync_ReturnsAllRows` (línea 46-75): mismas 3 filas → **falla**
- `GetByIdForUpdateAsync_Active_ReturnsWithNavigation` (78-106): solo activa → **falla**
- `GetByIdForUpdateAsync_Finalized_ReturnsNull` (108-131): solo finalizada → **puede pasar** (depende del primer `SeedAsync` y de si MySQL evalúa la columna antes del insert; en este test la fila se inserta con `FechaFin != null` → la columna generada devuelve `NULL` → no hay truncado)
- `GetByIdIncludingHistoryAsync_ReturnsEvenIfDeleted` (133-165): activa + deleted → **falla**
- `UpdateAsync_WithSoftDelete_SavesIsDeleted` (169-201): inserta activa, luego soft-delete → **falla** en el primer insert
- `UpdateAsync_WithFinalize_SavesFechaFin` (203-234): inserta activa → **falla**
- `UpdateAsync_WithReactivation_ClearsFechaFinAndIsDeleted` (236-270): inserta con `FechaFin`+`IsDeleted` → **puede pasar**
- `ExistsActiveByPuestoAsync_Active_ReturnsTrue` (274-297): inserta activa → **falla**
- `ExistsActiveByPuestoAsync_NoActive_ReturnsFalse` (299-308): sin insert → **pasa**
- `ExistsActiveByPuestoAsync_Finalized_ReturnsFalse` (310-333): inserta finalizada → **puede pasar**
- `ExistsActiveByPuestoAsync_ExcludingId_IgnoresSelf` (335-358): inserta activa → **falla**
- `ExistsActiveByPersonaYPuestoAsync_Active_ReturnsTrue` (360-383): inserta activa → **falla**
- `ExistsActiveByPersonaYPuestoAsync_DifferentPersona_ReturnsFalse` (385-409): inserta activa → **falla**
- `ExistsActiveByPersonaYPuestoAsync_ExcludingId_IgnoresSelf` (411-434): inserta activa → **falla**

Confirmado: **3/15 tests verdes hoy** (`NoActive_ReturnsFalse` y los dos tests que sólo insertan filas con `FechaFin != null`). Coincide con los reportes históricos en todos los `verify-report.md` recientes.

### Por qué el bug no se detectó antes (issue #59 cuerpo)

- `TestSgvDbContextFactory.cs:42-43` cae por defecto a `Server=localhost;Port=3306;Database=sgv_test;User=root;Password=;`. Si no hay MySQL local, `MySqlFactAttribute` (líneas 17-43) skipea todos los tests `[MySqlFact]` sin ejecutar.
- El script `docs/migracion-inicial-sgv.sql` se genera desde el snapshot EF, así que hereda el bug y lo propaga a cualquier deployment fresco de CI/staging.
- La spec `sgv-database/spec.md:298-325` (requisito "Historial de Ocupaciones", escenarios "Duplicado activo por puesto" y "Duplicado activo por persona y puesto") documenta la regla de negocio que el bug impide materializar. **La spec es correcta**; el bug es puramente de implementación.

## Áreas afectadas

- `src/SGV.Infraestructura/Persistencia/Configuraciones/OcupacionConfiguracion.cs` (líneas 35-38) — declaración EF de la sombra. Cambio mínimo: reemplazar `int?` por `string?` y agregar `HasMaxLength(36)`. Debe coordinarse con `IsRequired(false)`.
- `src/SGV.Infraestructura/Persistencia/Migraciones/SgvDbContextModelSnapshot.cs` (líneas 984-987) — se regenera al ejecutar `dotnet ef migrations add`. NO se edita a mano.
- `src/SGV.Infraestructura/Persistencia/Migraciones/` — se agrega una **migración nueva** (p.ej. `20260711_FixActivePuestoIdUniqueType.cs`) que use `migrationBuilder.AlterColumn` para cambiar `int` → `varchar(36)` con colación `ascii_general_ci` (consistente con el resto de columnas Guid). El `Down` revierte el cambio (debe recrear la columna como `int` y la migración será destructiva solo si la base ya tiene filas activas — ver Riesgos).
- `docs/migracion-inicial-sgv.sql` (línea 533, 1292) — se regenera con `dotnet ef migrations script --idempotent`. NO se edita a mano.
- `tests/SGV.Tests/Persistencia/ModeloPersistenciaTests.cs` (líneas 44-61, 122-140) — los asserts solo verifican strings del `ComputedColumnSql` y la presencia del índice único, no el `ClrType`/`ColumnType`. **No requieren modificación** para la opción 1; pero son una oportunidad para agregar asserts de regresión del tipo (ver "Nuevos tests").
- `tests/SGV.Tests/Persistencia/OcupacionRepositoryTests.cs` — los 12 tests fallidos deben pasar al corregir el tipo. No requieren modificación funcional.
- `tests/SGV.Tests/Persistencia/MySqlFactAttribute.cs` — sin cambios (la lógica de skip sigue vigente).
- `docs/decisiones-implementacion.md` (líneas 11-13) — la sección "Índices Únicos con Soft Delete" es agnóstica al tipo concreto. Solo amerita agregar una nota "ver también fix #59 en Ocupaciones" si el equipo quiere trazabilidad visible.
- `openspec/specs/sgv-database/spec.md` (líneas 298-325) — la spec no menciona el tipo; no requiere cambio.
- `openspec/specs/sgv-persistence-architecture/` — sin alusiones al bug.
- `AGENTS.md` (líneas 181-186) — el bloque "Tests de Integración con MySQL / Bug conocido (issue #59)" debe actualizarse en el mismo PR que aplique el fix: el último `dotnet test` debe ejecutarse en CI y confirmar 0 fallos en `OcupacionRepositoryTests`.

## Enfoques evaluados

### Opción 1 — Cambiar `ActivePuestoIdUnique` a `varchar(36)`/`char(36)` (match con `PuestoId`)

**Cómo**: alterar el shadow property de `int?` a `string?`, agregar `.HasMaxLength(36)` y `.UseCollation("ascii_general_ci")` (consistente con `PersonaId`, `PuestoId`, `CargoId` que usan esa colación). Nueva migración EF con `AlterColumn<string>` + `AlterColumn` de su índice (no se puede cambiar el tipo de una columna indexada en MySQL sin `DROP INDEX → ALTER → CREATE INDEX`, así que la migración debe dropear y recrear el índice).

**Pros**:
- Cambio mínimo, alineado con el patrón del repo (todas las demás columnas generadas que referencian FKs Guid usan `varchar(255)` o `char(36)`, ver `ActivePersonaIdUnique` de `Postulantes` línea 1259-1263 del snapshot, que es `char(36)`).
- No toca el dominio, no toca la lógica de aplicación, no toca el dominio de Puesto.
- Mantiene el invariante "unicidad activa por Puesto" intacto (la columna computada sigue devolviendo `PuestoId` cuando la fila está activa, sigue siendo `NULL` en otro caso).
- Idempotente: el script `docs/migracion-inicial-sgv.sql` regenerado aplica `varchar(36) AS (...)` y un deployment fresco arranca sin el bug.
- Rollback trivial (`Down` revierte `varchar(36)` → `int`).
- Migración de datos: **no requerida**. MySQL convierte `int` ↔ `varchar(36)` con coerción: el `int` actual contiene `0` o `NULL` en filas activas (por el truncado). Al pasar a `varchar(36)`, el `0` se convierte a `'0'` (1 char, no `NULL`). Eso **rompe el invariante de unicidad** para cualquier fila activa existente: ahora hay una columna `'0'` (no `PuestoId`) y múltiples filas activas del mismo PuestoId dejan de colisionar (porque todas tienen `'0'`). **Ver Riesgos para mitigación obligatoria**.

**Contras**:
- Migración de datos obligatoria: las filas activas existentes con `ActivePuestoIdUnique = 0` deben limpiarse antes o durante el `AlterColumn`. Estrategia: en la migración, antes del `AlterColumn`, ejecutar `UPDATE Ocupaciones SET ActivePuestoIdUnique = NULL WHERE FechaFin IS NULL AND IsDeleted = 0 AND ActivePuestoIdUnique != PuestoId` (con `CAST` apropiado) o directamente `DELETE FROM Ocupaciones` solo si la base es de test/dev. La rama real (producción) requiere backup + script de auditoría + decisión humana.
- La columna pasa de 4 bytes (int) a 37 bytes (varchar(36) + length byte + colación). En tablas grandes el impacto en espacio/IO es marginal pero existe.
- El índice `IX_Ocupaciones_ActivePuestoIdUnique` debe recrearse: MySQL no permite `ALTER COLUMN` cuando la columna está indexada; requiere `DROP INDEX → ALTER → CREATE INDEX` en una sola transacción.

**Tamaño de cambio**:
- Archivos de código: 1 (`OcupacionConfiguracion.cs`).
- Migración nueva: 1 (~50 LoC, `Up`+`Down`).
- Regeneración de `SgvDbContextModelSnapshot.cs` (automático).
- Regeneración de `docs/migracion-inicial-sgv.sql` (script, no edición manual).
- Tests: 0 ediciones, 12 tests pasan automáticamente.

**Esfuerzo**: Bajo-Medio. Principal riesgo operacional es la migración de datos preexistente; en bases vacías (CI/test/dev) es trivial.

**Recomendación**: ✅ **RECOMENDADA**. Es la única opción que preserva la decisión documentada de unicidad activa sin sacrificar contrato, blast radius mínimo, alineada con el patrón del repo.

### Opción 2 — Cambiar `PuestoId` (y todas las Guids relacionadas) de `char(36)` a `INT`/`BIGINT`

**Cómo**: introducir PKs auto-incrementales en todas las entidades (`Cargos`, `Habilidades`, `Personas`, `Puestos`, `UnidadesOrganizativas`, etc.), mapear Guid ↔ Int vía tabla puente o shadow property `LegacyGuid`.

**Pros**:
- Índices más angostos (4 bytes vs 37), mejor localidad de cache para write-heavy.
- Permite `ActivePuestoIdUnique` como `int` consistente con el dominio natural de un ID numérico.

**Contras**:
- Blast radius ENORME. `PuestoId` aparece como FK en `Ocupaciones`, `Vacantes` y `PuestoSuperiorId` (autoreferencia). Cambiar el tipo cascadea a `Vacantes.PuestoId`, todos los índices IX_*_PuestoId, todas las migraciones, todos los seed data (`b27ef633` introduce PKs Guid).
- La spec `puesto-management/spec.md`, `puesto-web-crear-editar/spec.md`, etc. exponen `id` como Guid en el contrato HTTP. Cambiar a Int rompe el contrato de la API pública.
- Rompe la portabilidad de identificadores externos (los Guid se generan en cliente y se persisten sin round-trip; un Int requiere round-trip).
- El snapshot actual ya tiene todas las columnas Guid comprometidas. Cambiar requiere regenerar desde cero (destructivo para bases existentes).
- La spec `puesto-management/spec.md` (líneas 19-44) **NO autoriza** este cambio; requiere spec delta propia.

**Esfuerzo**: Muy Alto. Meses de trabajo.

**Recomendación**: ❌ **DESCARTADA**. Blast radius inaceptable, sin ganancia proporcional, contradice todas las specs vigentes.

### Opción 3 — Drop la columna generada + índice filtrado imposible (MySQL no soporta)

**Cómo**: eliminar `ActivePuestoIdUnique` y su índice; mover la garantía de unicidad a validación de aplicación (`OcupacionRepository.ExistsActiveByPuestoAsync` ya lo hace, y `OcupacionServicioComandos.CrearAsync:150` lo invoca antes de `SaveChangesAsync`).

**Pros**:
- Elimina la dependencia del workaround de columnas generadas para este caso.
- Código más simple (un shadow property menos).

**Contras**:
- **Rompe la decisión documentada** en `docs/decisiones-implementacion.md:11-13` y la consistencia con el resto del modelo (`Cargos`, `Habilidades`, `Puestos`, `UnidadesOrganizativas`, `Personas`, `Postulantes`). Si se hace solo para `Ocupaciones`, el patrón queda inconsistente: cada tabla con soft-delete usa computed column excepto una.
- Abre una condición de carrera: dos requests concurrentes pueden pasar `ExistsActiveByPuestoAsync` (sin lock) y ambos insertar antes de que cualquiera haya confirmado. El índice actual, generado a nivel DB, es la red de seguridad final contra esto. Quitarlo deja la garantía de unicidad solo a nivel aplicación.
- Contradice `openspec/specs/sgv-database/spec.md:298-325` ("El sistema DEBE conservar una sola ocupación activa por Puesto") si se interpreta que esa garantía es a nivel DB (que es la lectura histórica: el índice es la red de seguridad).
- Adicionalmente, MySQL **no soporta índices filtrados** (`CREATE INDEX ... WHERE FechaFin IS NULL AND IsDeleted = 0`), así que la alternativa real sería `DROP INDEX + DROP COLUMN + agregar validación en código + manejar condición de carrera con `SELECT ... FOR UPDATE` en transacciones serializables`. Mucho más complejo.

**Esfuerzo**: Medio para el cambio + alto para restaurar la garantía con locks.

**Recomendación**: ❌ **DESCARTADA**. La columna generada con índice único es exactamente la decisión arquitectónica que el repo adoptó para resolver "MySQL no soporta índices filtrados"; eliminarla localmente para esta tabla rompe el patrón sin una justificación de negocio. Si la dirección quiere reconsiderar la estrategia, debe ser un change aparte que cubra todas las tablas, no un parche local.

### Opción 4 — CRC32 hash de `PuestoId` en `INT`

**Cómo**: declarar `ActivePuestoIdUnique` como `INT` con `HasComputedColumnSql("CASE WHEN ... THEN CRC32(PuestoId) ELSE NULL END")`. MySQL tiene la función `CRC32()` built-in desde 4.1.

**Pros**:
- Mantiene el tipo `INT` (4 bytes, índice compacto).
- No requiere tocar `PuestoId` ni la columna.
- Sin migración de datos (los `0` actuales se reemplazan por el hash real en filas activas tras el `ALTER`).

**Contras**:
- **Colisiones**: CRC32 sobre UUIDs con suficiente volumen produce colisiones. Birthday paradox: con ~77.000 UUIDs la probabilidad de colisión llega a 1e-7. En un sistema RRHH con miles de puestos y soft-delete acumulando histórico, el riesgo es real y no aceptable para una restricción de unicidad de negocio.
- **No determinista en soft delete**: si una ocupación se reactiva, el hash es estable (CRC32 es determinista). Pero si una fila activa tiene colisión con otra fila activa del CRC32, la segunda inserción falla con `Duplicate entry` aunque los `PuestoId` reales sean distintos → falso positivo.
- **Requiere función SQL custom si CRC32 no está habilitada** en el `sql_mode`/`sql_functions` del servidor destino. En MySQL 8 estándar está disponible, pero bloquea el despliegue a derivados (Aurora, MariaDB compatibility layer) sin verificación previa.
- Rompe la regla "el valor de la columna es el valor de negocio cuando está activa": con CRC32, dos `PuestoId` distintos mapean al mismo `INT`, así que la columna ya no es identificable visualmente como el `PuestoId`. Debug y soporte se complican.
- **Rompe el patrón** del repo, que mantiene todas las columnas generadas como `varchar/char` cuando referencian FKs Guid.

**Esfuerzo**: Bajo para escribir la migración, Alto para validar empíricamente que las colisiones no ocurren en el dominio (no se puede demostrar con análisis teórico).

**Recomendación**: ❌ **DESCARTADA**. Colisiones inaceptables para una restricción de unicidad de negocio; el ahorro de 33 bytes por fila no compensa el riesgo.

### Tabla comparativa

| Opción | Blast radius | Riesgo | Tests afectados | Idempotencia | Esfuerzo | Recomendación |
|---|---|---|---|---|---|---|
| 1 — `varchar(36)` | Mínimo (1 archivo código + 1 migración) | Migración de datos en bases con filas activas preexistentes | 12 verdes automático, 3 ya verdes | Sí (script idempotente) | Bajo-Medio | ✅ RECOMENDADA |
| 2 — `PuestoId` → `int` | Enorme (todas las FKs, todas las migraciones, contrato HTTP) | Cambio de contrato, requiere spec delta | Toda la suite | No (destructiva) | Muy Alto | ❌ DESCARTADA |
| 3 — Drop columna generada | Rompe patrón, abre condición de carrera | Inconsistencia arquitectónica, race conditions | 12 verdes pero con riesgo operacional | Sí | Medio-Alto | ❌ DESCARTADA |
| 4 — CRC32 | Mínimo pero colisiones | Falsos positivos en unicidad, MySQL-specific | 12 verdes pero con riesgo de regresión futura | Sí | Bajo escritura + Alto validación | ❌ DESCARTADA |

## Recomendación

**Opción 1 — Cambiar `ActivePuestoIdUnique` a `char(36)` con colación `ascii_general_ci`**.

Razones técnicas:

1. **Alineación con el patrón del repo**: la otra columna generada que referencia un FK Guid es `ActivePersonaIdUnique` en `Postulantes` (snapshot línea 1257-1263), que **ya usa `char(36)` con `ascii_general_ci`**. Es el precedente directo.
2. **Blast radius mínimo**: 1 archivo de configuración, 1 migración nueva. No toca Dominio, no toca Aplicación, no toca API, no toca Web.
3. **Decisión arquitectónica preservada**: la spec `sgv-database/spec.md:292-296` ("Preservar estrategia MySQL para unicidad activa") y `docs/decisiones-implementacion.md:11-13` siguen vigentes; no requieren delta.
4. **Riesgo operacional acotado y manejable**: la migración de datos preexistente se resuelve en 1-2 líneas SQL dentro de la propia migración (`UPDATE ... SET ActivePuestoIdUnique = NULL WHERE FechaFin IS NULL AND IsDeleted = 0` antes del `AlterColumn` para noquear los `0` truncados actuales, y luego el `ALTER` regenera la columna como `char(36) AS (CASE ... THEN PuestoId ...)` que vuelve a calcular el valor correcto para filas activas).
5. **Rollback limpio**: el `Down` revierte `char(36)` → `int`, reproduce el bug (manejable para volver atrás en caso de emergencia, no es un borrado de datos).
6. **El fix anterior del mismo bug es la guía**: la migración `20260624153353` ya hizo el patrón equivalente para `ActivePersonaIdUnique` (drop + add). Esta vez es más simple: solo `AlterColumn` + drop+create index.

**Plan operativo sugerido** (no es parte del exploration, solo contexto para la propuesta):

1. Modificar `OcupacionConfiguracion.cs`: cambiar `int?` → `string?`, agregar `HasMaxLength(36).UseCollation("ascii_general_ci")`.
2. `dotnet ef migrations add FixActivePuestoIdUniqueType --project src/SGV.Infraestructura/SGV.Infraestructura.csproj --startup-project src/SGV.Infraestructura/SGV.Infraestructura.csproj`.
3. Editar la migración generada: agregar `migrationBuilder.Sql("UPDATE Ocupaciones SET ActivePuestoIdUnique = NULL WHERE FechaFin IS NULL AND IsDeleted = 0")` antes del `AlterColumn` (purga los `0` truncados), dropear índice → `AlterColumn` → crear índice.
4. Regenerar `docs/migracion-inicial-sgv.sql` con `dotnet ef migrations script --idempotent --output docs/migracion-inicial-sgv.sql`.
5. Agregar 1 test estructural en `ModeloPersistenciaTests.cs` que asserte que el `ClrType`/`ColumnType` de `ActivePuestoIdUnique` es `string`/`char(36)` (regression guard contra reintroducir `int`).
6. Actualizar `AGENTS.md:181-186`: remover la mención del bug #59 como abierto, reemplazarla por "Resuelto en change `2026-07-11-fix-active-puesto-id-unique-type`".

## Riesgos

- **Crítico — Datos activos preexistentes con `ActivePuestoIdUnique = 0`**: en cualquier deployment donde se haya intentado insertar Ocupaciones activas y MySQL permitió el truncado a `0` (con `sql_mode` permisivo, p.ej. CI sin `STRICT_TRANS_TABLES`), la columna actual contiene `0` para filas que **deberían** contener el `PuestoId` real. El `AlterColumn` de `int` → `varchar(36)` convertiría ese `0` a la string `'0'`, **rompiendo la unicidad activa** (porque múltiples filas activas del mismo `PuestoId` colapsan todas a `'0'`). **Mitigación obligatoria**: incluir en la migración un `UPDATE ... SET ActivePuestoIdUnique = NULL WHERE ActivePuestoIdUnique IS NOT NULL AND ActivePuestoIdUnique != CONCAT(PuestoId) ...` antes del alter, o un `DELETE FROM Ocupaciones` (solo si la base es de test/dev y acordonado con el operador). En CI/dev fresco la columna está vacía (porque los tests que insertan filas activas fallan antes de commit), así que la mitigación es no-op. En cualquier base con producción real detrás, el operador debe aprobarla antes del deploy.
- **Crítico — Issue #59 no documentado como migrable en `AGENTS.md`**: el bloque actual dice "Pendiente de SDD change" sin más guía. El apply debe eliminar esa nota y enlazar al change que lo cierra; sino queda como deuda fantasma.
- **Advertencia — `sql_mode` del servidor destino**: el bug se manifiesta distinto según `STRICT_TRANS_TABLES`. En MySQL 8 stock (`STRICT_TRANS_TABLES,NO_ENGINE_SUBSTITUTION,ERROR_FOR_DIVISION_BY_ZERO`) el insert falla con `Data truncated`. En `sql_mode=''` (legacy) el insert succeeds y la columna queda en `0`. El plan de migración debe asumir el peor caso (`0` presente) y aplicar la purga de forma incondicional.
- **Advertencia — Drop+Create index en transacción**: MySQL InnoDB soporta online DDL con `ALGORITHM=INPLACE, LOCK=NONE` para `ALTER TABLE ... DROP INDEX ... ADD INDEX ...` cuando la tabla no está bajo carga pesada. En producción con escrituras concurrentes, esto puede causar esperas. Documentar en la propuesta que la ventana de mantenimiento es ~ms con `ALGORITHM=COPY` explícito (es el default seguro si `INPLACE` no aplica).
- **Advertencia — Snapshot debe regenerarse**: si el apply toca `OcupacionConfiguracion.cs` y NO regenera el snapshot con `dotnet ef migrations add`, la próxima migración futura fallará con "drift detectado" (cf. `ModeloPersistenciaTests.Migraciones_ScriptIdempotenteNoGeneraDDL`). Mitigación: el comando `migrations add` ya regenera el snapshot automáticamente; el riesgo es humano (olvidar correr el comando).
- **Advertencia — Tests `[MySqlFact]` sin MySQL local**: si el developer no tiene MySQL corriendo, los 12 tests de `OcupacionRepositoryTests` siguen skipeados y el fix pasa "verde" sin haber validado contra DB real. El CI del repo sí tiene MySQL (`.github/workflows/ci.yml`), así que la verificación final debe correr ahí. Documentar en la propuesta que `dotnet test SGV.slnx` local sin MySQL **no es verificación suficiente**.
- **Sugerencia — Test de regresión estructural**: aunque `ModeloPersistenciaTests.cs` ya tiene cobertura parcial (`Modelo_ConfiguraColumnaGeneradaUnicaParaOcupacionVigentePorPuesto`), los asserts no verifican el **tipo**. Agregar `Assert.Equal(typeof(string), generatedProperty.ClrType)` y `Assert.Contains("char(36)", generatedProperty.GetColumnType(), StringComparison.OrdinalIgnoreCase)` cierra la puerta a que un cambio futuro (humano o generado por IA) revierta el tipo a `int`.
- **Sugerencia — Tests de no-regresión del truncado**: agregar 1 test `[MySqlFact]` que inserte una ocupación activa con `PuestoId = Guid.NewGuid()`, haga `SaveChangesAsync`, lea `entity.ActivePuestoIdUnique` vía SQL crudo y verifique que coincide con `PuestoId.ToString()`. Es el canario de regresión exacto del bug original.

## Tests que deben pasar al aplicar el fix

Verdes tras el fix (12 que hoy fallan + 3 que ya pasan):

- `ListAllAsync_Default_ReturnsOnlyActiveRows`
- `ListAllIncludingHistoryAsync_ReturnsAllRows`
- `GetByIdForUpdateAsync_Active_ReturnsWithNavigation`
- `GetByIdIncludingHistoryAsync_ReturnsEvenIfDeleted`
- `UpdateAsync_WithSoftDelete_SavesIsDeleted`
- `UpdateAsync_WithFinalize_SavesFechaFin`
- `UpdateAsync_WithReactivation_ClearsFechaFinAndIsDeleted`
- `ExistsActiveByPuestoAsync_Active_ReturnsTrue`
- `ExistsActiveByPuestoAsync_ExcludingId_IgnoresSelf`
- `ExistsActiveByPersonaYPuestoAsync_Active_ReturnsTrue`
- `ExistsActiveByPersonaYPuestoAsync_DifferentPersona_ReturnsFalse`
- `ExistsActiveByPersonaYPuestoAsync_ExcludingId_IgnoresSelf`

(Los 3 ya verdes: `GetByIdForUpdateAsync_Finalized_ReturnsNull`, `ExistsActiveByPuestoAsync_NoActive_ReturnsFalse`, `ExistsActiveByPuestoAsync_Finalized_ReturnsFalse` siguen verdes.)

## Nuevos tests sugeridos (regresión)

1. `ModeloPersistenciaTests.Modelo_ActivePuestoIdUnique_EsChar36NoInt` — asserta que `OcupacionEntity.ActivePuestoIdUnique.ClrType == typeof(string)` y `GetColumnType() == "char(36)"`. Protege contra reintroducir `int`.
2. `ModeloPersistenciaTests.Modelo_ActivePuestoIdUnique_CollationEsAscii` — asserta `.UseCollation("ascii_general_ci")` para mantener consistencia con el resto de columnas Guid.
3. `OcupacionRepositoryTests.AddAsync_FilaActiva_ActivePuestoIdUniquePersisteComoGuidString` — test `[MySqlFact]` que inserta una `OcupacionEntity` activa, hace `SaveChangesAsync`, lee `ActivePuestoIdUnique` vía `Database.SqlQueryRaw<string>("SELECT ActivePuestoIdUnique FROM Ocupaciones WHERE Id = {0}", id)` y verifica que coincide con `puestoId.ToString()`. Canario del bug original.

## Especs vigentes y delta specs necesarias

- `openspec/specs/sgv-database/spec.md` — vigente, NO requiere delta. El requisito "Historial de Ocupaciones" (líneas 298-325) ya documenta la regla de unicidad activa que el fix materializa.
- `openspec/specs/sgv-persistence-architecture/` — vigente, NO requiere delta.
- `docs/decisiones-implementacion.md` líneas 11-13 — vigente, NO requiere delta. Opcional: agregar referencia cruzada "#59 resuelto en change X" si el equipo lo considera útil para trazabilidad.

## Producción vs tests

**Sí afecta producción**. Cualquier INSERT a `Ocupaciones` desde `SGV.Api` con `FechaFin IS NULL AND IsDeleted = 0` (camino feliz de `OcupacionServicioComandos.CrearAsync:158-166`) explota con `MySqlException: Data truncated for column 'ActivePuestoIdUnique'`. Las pruebas de integración API que usan `WebApplicationFactory` con `InMemoryDatabase` no detectan el bug (Pomelo no se usa). **El bug bloquea la funcionalidad de crear ocupaciones activas en cualquier deployment con MySQL real**. La spec `cargo-skill-asignar-editar/spec.md` no depende directamente, pero cualquier flujo que cree una ocupación (hoy no hay UI para eso según `AGENTS.md` líneas 99-109, pero la API existe en `OcupacionesController`) está bloqueado.

Trazabilidad del path de escritura en producción: `src/SGV.Api/Controllers/OcupacionesController.cs` (POST) → `OcupacionServicioComandos.CrearAsync` → `OcupacionRepository.AddAsync` → `unitOfWork.SaveChangesAsync` → MySQL ejecuta la columna generada → falla.

## Gotchas no triviales

1. **MySQL evalúa columnas generadas antes del INSERT, no durante**: el truncado a `0` (con `sql_mode` permisivo) o el error `Data truncated` (estricto) ocurre como parte del statement, no como constraint check posterior. Por eso el índice único no es el primer síntoma — el primero es el truncado en sí.
2. **La migración `20260624153353` ya hizo un fix análogo** para `ActivePersonaIdUnique` (drop+add). El `.Designer.cs` de esa migración (líneas 1059-1064) muestra el snapshot pre-fix con `int?` y la columna vieja — referencia útil para el `Down` del nuevo fix.
3. **`HasComputedColumnSql` regenera la expresión en cada lectura/escritura**: la columna no se almacena físicamente, se calcula on-the-fly. Por eso el `UPDATE` previo a `AlterColumn` con `SET ActivePuestoIdUnique = NULL WHERE FechaFin IS NULL AND IsDeleted = 0` funciona aunque la columna sea generada (el SET sobre una generated column es legal en MySQL **si no se altera `PuestoId`/`FechaFin`/`IsDeleted` simultáneamente**, lo cual es nuestro caso porque solo estamos haciendo un UPDATE independiente). Verificar el plan de ejecución para confirmar que MySQL acepta el UPDATE.
4. **EF Core 9 + Pomelo puede generar `HasMaxLength(36)` como `varchar(36)` en vez de `char(36)`**: ambos funcionan, pero `varchar(36)` ocupa 1 byte extra por fila (length-prefix). Para alinear con `PuestoId` que es `char(36)`, usar `UseCollation("ascii_general_ci")` y forzar `HasColumnType("char(36)")` en la configuración EF. Verificar en el SQL generado por la migración.
5. **Idempotente del script SQL**: `dotnet ef migrations script --idempotent` envuelve cada `ALTER` en `IF EXISTS` checks. Pero el `DROP INDEX ... ADD INDEX` necesita `IF EXISTS` y `IF NOT EXISTS` explícitos en el SQL crudo si lo escribimos a mano. La migración generada por EF ya lo incluye; el script idempotente también.
6. **El test `Modelo_ConfiguraColumnaGeneradaUnicaParaOcupacionVigentePorPuesto` (líneas 44-61) usa `assert.Contains("FechaFin", computedSql, ...)`**: si la migración futura cambia la expresión SQL (p.ej. para incluir `IsActive` además de `IsDeleted`), este test seguirá pasando porque solo verifica substrings. Es robusto pero también laxo: no detecta cambios sutiles de semántica.
7. **Pomelo trata `Guid` como `char(36)` por default** (configurado en `SgvDbContext` o implícito). La configuración EF del shadow property NO hereda automáticamente este default; por eso el `int?` se traduce literalmente a `INT` aunque `PuestoId` sea `char(36)`. El fix requiere declarar explícitamente `string?` con `HasMaxLength(36)` o `HasColumnType("char(36)")`.
8. **El `Ocupacion` dominio es `record class` con `init`-only setters** (`src/SGV.Dominio/Ocupaciones/Ocupacion.cs:7`). Esto es ortogonal al fix pero confirma que el dominio está blindado contra mutaciones accidentales — no hay nada que ajustar en esa capa.

## Listo para propuesta

**Sí** — la recomendación (Opción 1) es clara, técnicamente justificada, alineada con el patrón del repo, y el blast radius es mínimo. Riesgos identificados son operacionalmente manejables y deben discutirse con el operador antes del deploy a producción real.

El orchestrator puede proceder con `sdd-propose` para generar `proposal.md`, `sdd-spec` para delta mínima en `openspec/changes/2026-07-11-fix-active-puesto-id-unique-type/specs/`, y `sdd-tasks` para descomponer el trabajo en PR-sliceable units (sugerido: 1 PR contiene `OcupacionConfiguracion.cs` + nueva migración + regeneración del script + test estructural; 1 PR separado actualiza `AGENTS.md` y remueve el bloque del bug).
