# Design: Fix `ActivePuestoIdUnique` tipo a `char(36)` (issue #59)

## Technical Approach

Fix puntual de un solo archivo de configuración (`OcupacionConfiguracion.cs:35-38`) + nueva migración EF con el patrón `UPDATE defensivo → DROP INDEX → ALTER COLUMN → CREATE INDEX` dentro de una transacción. La migración es **forward-only** (`Down` lanza `NotSupportedException` antes de cualquier otra instrucción). Sin cambios en Dominio, Aplicación, API ni Web. Reproduce el patrón del precedente `Migraciones/20260624153353_ConvertirTipoAsignacionAEnumYActualizarUnicidad.cs:14-20, 99-110` (drop+recreate análogo de `ActivePersonaIdUnique`). Materializa el invariante del spec delta `2026-07-11-fix-active-puesto-id-unique-type/specs/sgv-database/spec.md` ("Coincidencia de tipo entre columna generada y columna fuente").

## Architecture Decisions

| Decisión | Elegido | Alternativas descartadas | Rationale |
|----------|---------|--------------------------|-----------|
| Tipo CLR/almacenamiento | `string?` + `HasMaxLength(36)` + `HasColumnType("char(36)")` + `UseCollation("ascii_general_ci")` | Mantener `int?`; `varchar(36)` default; `CRC32(PuestoId)` INT | Match exacto con `PuestoId char(36)` (snapshot 1019-1020); alineado con `ActivePersonaIdUnique` de `Postulantes` (snapshot 1257-1263); sin colisiones; spec `sgv-database/spec.md:292-296` preserva la decisión "columna generada + índice único" |
| `Down()` | `throw new NotSupportedException("Migración forward-only. Para revertir, escribir una migración correctiva explícita.")` al inicio | Reversión real `char(36)→int` | Decisión confirmada por el usuario; bloquea rollback accidental; correctivo explícito si falla real |
| Purga pre-`AlterColumn` | `UPDATE Ocupaciones SET ActivePuestoIdUnique = NULL WHERE FechaFin IS NULL AND IsDeleted = 0` | Sin purga (asume DB limpio) | Cubre `sql_mode` permisivo donde hay `0` truncado preexistente; no-op en CI/dev fresco; idempotente |
| Recreación del índice | `DROP INDEX → ALTER → CREATE INDEX` en una sola transacción EF | `ALTER TABLE ... DROP/ADD INDEX` inline | MySQL rechaza `ALTER COLUMN` sobre columna indexada; exige drop previo (regla de MySQL 8 InnoDB) |
| Forma de storage | `char(36)` exacto vía `HasColumnType("char(36)")` | `varchar(36)` (default Pomelo cuando solo se setea `HasMaxLength(36)`) | `PuestoId` es `char(36)`; alinear evita 1 byte de length-prefix por fila y deja el índice del mismo tamaño que el FK |

## Data Flow

```
OcupacionConfiguracion.cs (shadow, líneas 35-38)
  Property<string?>("ActivePuestoIdUnique")
    .HasMaxLength(36) .HasColumnType("char(36)")
    .UseCollation("ascii_general_ci")
    .HasComputedColumnSql("CASE WHEN FechaFin IS NULL AND IsDeleted = 0 THEN PuestoId ELSE NULL END")
            │
            ▼
EF Core model builder → SgvDbContextModelSnapshot.cs (regenerado, antes líneas 984-987)
            │
            ▼
FixActivePuestoIdUniqueType migration (Up):
  1) UPDATE Ocupaciones SET ActivePuestoIdUnique = NULL WHERE FechaFin IS NULL AND IsDeleted = 0
  2) DROP INDEX IX_Ocupaciones_ActivePuestoIdUnique
  3) ALTER COLUMN ActivePuestoIdUnique char(36) ascii_general_ci NULL AS (...)
  4) CREATE UNIQUE INDEX IX_Ocupaciones_ActivePuestoIdUnique
            │
            ▼
docs/migracion-inicial-sgv.sql (regenerado vía migrations script --idempotent)
```

Sin impacto en API runtime: `OcupacionServicioComandos.CrearAsync` ahora completa `INSERT` sin `Data truncated`; el índice único rechaza duplicados activos por `PuestoId` (escenarios del spec 22-27).

## File Changes

| Archivo | Acción | Descripción |
|---------|--------|-------------|
| `src/SGV.Infraestructura/Persistencia/Configuraciones/OcupacionConfiguracion.cs` | Modify (35-38) | `int?` → `string?` + `HasMaxLength(36).HasColumnType("char(36)").UseCollation("ascii_general_ci")` |
| `src/SGV.Infraestructura/Persistencia/Migraciones/<timestamp>_FixActivePuestoIdUniqueType.cs` | Create | Migración EF nueva: `Up` (UPDATE + DROP INDEX + AlterColumn + CREATE INDEX); `Down` (NotSupportedException al inicio, antes de cualquier otra instrucción) |
| `src/SGV.Infraestructura/Persistencia/Migraciones/SgvDbContextModelSnapshot.cs` | Modify (984-987) | Regenerado por `dotnet ef migrations add`; pasa de `int?`/`int` a `string`/`char(36)` con `HasMaxLength(36)` |
| `docs/migracion-inicial-sgv.sql` | Modify (533, 1292) | Regenerado por `dotnet ef migrations script --idempotent`; columna computada pasa de `int AS (...)` a `char(36) AS (...)` con colación `ascii_general_ci` |
| `tests/SGV.Tests/Persistencia/ModeloPersistenciaTests.cs` | Modify (44-61) | Endurecer `Modelo_ConfiguraColumnaGeneradaUnicaParaOcupacionVigentePorPuesto`: `Assert.Equal(typeof(string), prop.ClrType)` + `Assert.Contains("char(36)", prop.GetColumnType(), …)` |
| `tests/SGV.Tests/Persistencia/OcupacionGeneratedColumnRegressionTests.cs` | Create | Nuevo `[MySqlFact] AddAsync_FilaActiva_ActivePuestoIdUniquePersisteComoGuidString`: inserta activa con `PuestoId = Guid.NewGuid()`, `SaveChangesAsync`, `Database.SqlQueryRaw<string>` lee la columna, asserta `result == puestoId.ToString()` |
| `AGENTS.md` | Modify (181-186) | Reemplazar bloque de 6 líneas (incluye "Bug conocido (issue #59)… Pendiente de SDD change") por una sola línea de cierre |

## Interfaces / Contracts

```csharp
// OcupacionConfiguracion.cs (reemplaza líneas 35-38)
builder.Property<string?>("ActivePuestoIdUnique")
    .HasMaxLength(36)
    .HasColumnType("char(36)")
    .UseCollation("ascii_general_ci")
    .HasComputedColumnSql("CASE WHEN `FechaFin` IS NULL AND `IsDeleted` = 0 THEN `PuestoId` ELSE NULL END")
    .IsRequired(false);
builder.HasIndex("ActivePuestoIdUnique").IsUnique();

// FixActivePuestoIdUniqueType.Up(MigrationBuilder mb)
mb.Sql("UPDATE `Ocupaciones` SET `ActivePuestoIdUnique` = NULL WHERE `FechaFin` IS NULL AND `IsDeleted` = 0");
mb.DropIndex(name: "IX_Ocupaciones_ActivePuestoIdUnique", table: "Ocupaciones");
mb.AlterColumn<string>(
    name: "ActivePuestoIdUnique", table: "Ocupaciones",
    type: "char(36)", nullable: true, collation: "ascii_general_ci",
    computedColumnSql: "CASE WHEN `FechaFin` IS NULL AND `IsDeleted` = 0 THEN `PuestoId` ELSE NULL END",
    oldClrType: typeof(int), oldType: "int");
mb.CreateIndex(name: "IX_Ocupaciones_ActivePuestoIdUnique", table: "Ocupaciones",
    column: "ActivePuestoIdUnique", unique: true);

// FixActivePuestoIdUniqueType.Down: PRIMERA línea antes de cualquier mb.*
// throw new NotSupportedException(
//   "Migración forward-only. Para revertir, escribir una migración correctiva explícita.");
```

`HasMaxLength(36)` es necesario aunque se fuerce `HasColumnType("char(36)")`: sin él EF no registra el shadow property como `string` en el modelo y el escenario del spec 30-35 falla. `HasColumnType("char(36)")` anula el default `varchar(36)` de Pomelo para alinear con `PuestoId`.

## Testing Strategy

| Capa | Test | Approach |
|------|------|----------|
| Unit (modelo, sin MySQL) | `Modelo_ConfiguraColumnaGeneradaUnicaParaOcupacionVigentePorPuesto` endurecido | Asserciones de `ClrType == typeof(string)` + `GetColumnType().Contains("char(36)")`; guardrail local contra reintroducir `int` |
| Integration `[MySqlFact]` (canario nuevo) | `OcupacionGeneratedColumnRegressionTests.AddAsync_FilaActiva_ActivePuestoIdUniquePersisteComoGuidString` | Inserta `OcupacionEntity` activa con `PuestoId = Guid.NewGuid()`, `SaveChangesAsync`, `Database.SqlQueryRaw<string>("SELECT ActivePuestoIdUnique FROM Ocupaciones WHERE Id = {0}", entity.Id)` lee la columna, asserta == `puestoId.ToString()` |
| Integration `[MySqlFact]` (recuperación) | 12 tests de `OcupacionRepositoryTests` que hoy fallan (`ListAll*`, `GetByIdForUpdateAsync_Active`, `UpdateAsync_*`, `ExistsActiveByPuestoAsync_*`, `ExistsActiveByPersonaYPuestoAsync_*`) | Fail → pass sin edición de código; el fix de tipo destraba el truncado |
| Schema drift | `Migraciones_ScriptIdempotenteNoGeneraDDL` (`[MySqlFact]`) | El script delta entre la última migración y HEAD no debe contener DDL (snapshot ya alineado por `migrations add`) |

## Migration / Rollout

**Orden de aplicación (humano, paso a paso)**:

1. Editar `OcupacionConfiguracion.cs:35-38` (único archivo de código de producción).
2. `dotnet ef migrations add FixActivePuestoIdUniqueType --project src/SGV.Infraestructura/SGV.Infraestructura.csproj --startup-project src/SGV.Infraestructura/SGV.Infraestructura.csproj`.
3. Editar la migración generada: insertar `mb.Sql(...)` con la purga defensiva como **primera** línea de `Up`; reemplazar el cuerpo autogenerado de `Down` por `throw new NotSupportedException(...)` como **primera** línea.
4. `dotnet ef migrations script --idempotent --output docs/migracion-inicial-sgv.sql` (sobrescribe el script completo).
5. Endurecer `ModeloPersistenciaTests.cs:44-61` con asserts de tipo.
6. Crear `tests/SGV.Tests/Persistencia/OcupacionGeneratedColumnRegressionTests.cs` con el canario.
7. Editar `AGENTS.md:181-186` (reemplazar bloque 6→1 línea).
8. `dotnet build SGV.slnx` (sanidad).
9. `dotnet test SGV.slnx --filter "FullyQualifiedName~ModeloPersistenciaTests"` (verde local sin MySQL).
10. Commit + push → CI corre suite completa contra MySQL 8 (`.github/workflows/ci.yml`).

**Rollback**: forward-only por decisión 2 del proposal. Si falla en producción real, **no** ejecutar `dotnet ef migrations remove` (crearía conflicto inverso); escribir **migración correctiva explícita** que revierta `char(36)→int` aplicando el mismo `UPDATE` defensivo inverso. `ActivePersonaPuestoUnique varchar(100)` mantiene enforcing "Persona + Puesto activos" durante la ventana de transición.

**Verificación**:

- **Local sin MySQL**: `dotnet test --filter "FullyQualifiedName~ModeloPersistenciaTests"` debe pasar; el test endurecido es el guardrail local.
- **Local con MySQL**: `dotnet test --filter "FullyQualifiedName~OcupacionRepositoryTests"` 15/15 + canario nuevo = verde.
- **CI (obligatorio, bloqueante)**: `.github/workflows/ci.yml` levanta MySQL 8 y corre `dotnet test --no-build --configuration Release`. Si la suite completa falla, el PR se bloquea.

## Open Questions

Ninguna. La exploración (`exploration.md`) cerró las 4 opciones; el usuario confirmó vía chat las 5 decisiones (UPDATE defensivo, forward-only, test canario, endurecimiento de `ModeloPersistenciaTests`, línea de cierre en `AGENTS.md`). El spec delta y el proposal son consistentes con el código real: `PuestoId char(36)` (snapshot 1019-1020), `ActivePuestoIdUnique int` (snapshot 984-987), precedente `20260624153353` con el mismo patrón drop+create.
