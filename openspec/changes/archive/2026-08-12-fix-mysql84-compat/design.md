# Design: fix-mysql84-compat

## Contexto técnico

Tres restricciones ya documentadas en la proposal/spec y verificadas contra el código:

1. **MySQL 8.4 LTS rechaza `CREATE UNIQUE INDEX` sobre columnas `GENERATED VIRTUAL`**. `InicialSgvo` (líneas 244-249) crea `Personas.ActiveEmailUnique`, `ActiveDocumentoUnique` y `ActiveLegajoUnique` como VIRTUALES, y las líneas 1145-1161 emiten `CreateIndex(unique: true)` sobre ellas. En 8.0 acepta; en 8.4 la cadena falla en el primer `CREATE UNIQUE INDEX` (línea 1145, `IX_Personas_ActiveDocumentoUnique`).
2. **`migrationBuilder.Sql()` con `;` internos se rompe en el script `--idempotent`**. Verificado leyendo `docs/migracion-inicial-sgv.sql` líneas 7-20: Pomelo envuelve cada `Sql()` en `DROP PROCEDURE IF EXISTS MigrationsScript; DELIMITER // CREATE PROCEDURE MigrationsScript() BEGIN IF NOT EXISTS(...) THEN <contenido Sql()> END IF; END // DELIMITER ; CALL MigrationsScript(); DROP PROCEDURE MigrationsScript;`. Cualquier `BEGIN ... END` anidado o `CREATE PROCEDURE` interno es ilegal en MySQL (no se pueden anidar stored procedures) — exactamente lo que la spec prohíbe.
3. **No modificar migraciones ya aplicadas** (convención fuerte). Por eso se descarta reescribir `InicialSgvo` y se introduce una compensatoria cronológicamente posterior.

## Decisiones arquitectónicas

### Decision: Patrón de la compensatoria — D.3 (statements únicos con PREPARE/EXECUTE)

**Choice**: D.3 — múltiples `migrationBuilder.Sql()` separados, cada uno con UN solo statement SQL (sin `;` internos). Lógica condicional idempotente vía `SET @var = (SELECT ... FROM information_schema ...)` + `PREPARE stmt FROM @sql` + `EXECUTE stmt` + `DEALLOCATE PREPARE stmt` en calls separadas.

**Alternatives consideradas**:
- **D.1 (Sql() simple NON-idempotente)**: rechazado. Re-aplicar el script contra DB con estado roto fallaría; la robustuez no es negociable.
- **D.2 (stored procedure local con `BEGIN ... END`)**: rechazado empíricamente. El generador idempotente de Pomelo envuelve cada `Sql()` en `MigrationsScript() BEGIN ... END`; anidar `CREATE PROCEDURE` dentro de otro procedure es ilegal en MySQL. La spec del propio cambio ya prohíbe `BEGIN ... END` en `migrationBuilder.Sql()`.

**Rationale**: D.3 cumple la restricción documentada de la spec ("statements únicos o lógica que no requiera `DELIMITER`"). Cada `mb.Sql("...")` contiene UN único statement con UN único `;` terminal; Pomelo lo envuelve individualmente sin colisión de `DELIMITER`. Idempotencia vía `information_schema` + `PREPARE/EXECUTE` para DROP INDEX condicional, DROP COLUMN condicional y CREATE INDEX condicional.

### Decision: Timestamp de la migración

**Choice**: `20260728120000_FixMySql84GeneratedUniqueIndex` (entre `InicialSgvo` y `MariaDbStoredColumnsAndCollation`).

**Alternatives**: timestamp anterior a `20260711181615_FixActivePuestoIdUniqueType` (colocaría la compensatoria antes del `DropIndex` de aquélla).

**Rationale**: respeta el orden pedido por la propuesta. Ver Open Questions: el `DropIndex` de `FixActivePuestoIdUniqueType` puede fallar en 8.4 fresh —.requires validación en fase apply.

## Estructura de la nueva migración

Archivo: `src/SGV.Infraestructura/Persistencia/Migraciones/20260728120000_FixMySql84GeneratedUniqueIndex.cs`

```csharp
namespace SGV.Infraestructura.Persistencia.Migraciones;

[DbContext(typeof(SgvDbContext))]
[Migration("20260728120000_FixMySql84GeneratedUniqueIndex")]
public partial class FixMySql84GeneratedUniqueIndex : Migration
{
    protected override void Up(MigrationBuilder mb)
    {
        // Por cada columna: detectar estado vía information_schema,
        // construir SQL dinámico con SET @sql, PREPARE, EXECUTE, DEALLOCATE.
        // Cada mb.Sql() = UN solo statement.
        ConvertUniqueVirtualToStored(mb, "Personas", "ActiveEmailUnique",
            "varchar(255)", null,
            "CASE WHEN `Email` IS NOT NULL AND `IsDeleted` = 0 THEN `Email` ELSE NULL END");
        // ... 9 columnas restantes con mismo patrón
    }

    protected override void Down(MigrationBuilder mb)
        => throw new NotSupportedException("Forward-only. Revertir requiere migración correctiva explícita.");

    private static void ConvertUniqueVirtualToStored(MigrationBuilder mb,
        string table, string column, string type, string? collation, string expr)
    {
        // 1) DROP INDEX si existe (PREPARE/EXECUTE separados, 5 mb.Sql).
        // 2) DROP COLUMN si existe (4 mb.Sql).
        // 3) ADD COLUMN STORED (1 mb.Sql — siempre seguro porque se dropeó arriba).
        // 4) CREATE UNIQUE INDEX (1 mb.Sql).
    }
}
```

El helper `ConvertUniqueVirtualToStored` encapsula el patrón repetitivo para las 10 columnas. Patrones individuales derivados de `MariaDbStoredColumnsAndCollation` ya aplicado en el repo (líneas 64-329): tipo, collation y expresión `CASE WHEN` por columna.

## Mapeo de columnas

| Tabla | Columna | Tipo | Collation | Source expression (CASE WHEN … END) |
|---|---|---|---|---|
| UnidadesOrganizativas | ActiveCodigoUnique | varchar(255) | — | `IsDeleted=0 THEN Codigo` |
| Puestos | ActiveCodigoUnique | varchar(255) | — | `IsDeleted=0 THEN Codigo` |
| Postulantes | ActivePersonaIdUnique | char(36) | ascii_general_ci | `PersonaId IS NOT NULL AND IsDeleted=0 THEN PersonaId` |
| Personas | ActiveLegajoUnique | varchar(255) | — | `Legajo IS NOT NULL AND IsDeleted=0 THEN Legajo` |
| Personas | ActiveEmailUnique | varchar(255) | — | `Email IS NOT NULL AND IsDeleted=0 THEN Email` |
| Personas | ActiveDocumentoUnique | varchar(120) | utf8mb4_unicode_ci | `TipoDocumentoId IS NOT NULL AND NumeroDocumento IS NOT NULL AND IsDeleted=0 THEN CONCAT(TipoDocumentoId,':',NumeroDocumento)` |
| Ocupaciones | ActivePuestoIdUnique | varchar(36) | ascii_general_ci | `FechaFin IS NULL AND IsDeleted=0 THEN PuestoId` |
| Ocupaciones | ActivePersonaPuestoUnique | varchar(100) | — | `FechaFin IS NULL AND IsDeleted=0 THEN CONCAT(PersonaId,':',PuestoId)` |
| Habilidades | ActiveCodigoUnique | varchar(255) | — | `IsDeleted=0 THEN Codigo` |
| Cargos | ActiveCodigoUnique | varchar(255) | — | `IsDeleted=0 THEN Codigo` |

`Personas.ActiveDocumentoUnique` preserva collation `utf8mb4_unicode_ci` (la misma que usa MariaDb) para que la subsiguiente `MariaDbStoredColumnsAndCollation` sea idempotente. Las expresiones se mantienen idénticas a las de `InicialSgvo` (líneas 244-248) y `MariaDbStoredColumnsAndCollation` (líneas 82-322) para no divergir del snapshot.

## Flujo de integración con `MariaDbStoredColumnsAndCollation`

```
InicialSgvo (06-14)  ──► VIRTUAL + UNIQUE INDEX (8.0 ok, 8.4 falla)
       │
       ▼
FixActivePuestoIdUniqueType (07-11)  ──► alter varchar(36) STORED + index (8.0)
       │
       ▼
FixMySql84GeneratedUniqueIndex (07-28) NUEVA  ──► detecta VIRTUAL, convierte a STORED + UNIQUE INDEX
       │                                            (idempotente: si ya STORED+index → no-op)
       ▼
MariaDbStoredColumnsAndCollation (07-29)  ──► redefinición DROP+ADD+INDEX STORED (idempotente)
       │
       ▼
Resto (~agosto)  ──► sin cambio
```

`MariaDbStoredColumnsAndCollation` ya hace DROP+ADD+CREATE INDEX idempotente (líneas 69-90), por lo que redefinir una columna que ya es STORED+INDEX es válido en MySQL (no falla aunque el estado sea el mismo). La nueva compensatoria NO rompe a MariaDb.

## Regeneración del script standalone

```
dotnet ef migrations script \
  --project src/SGV.Infraestructura/SGV.Infraestructura.csproj \
  --startup-project src/SGV.Infraestructura/SGV.Infraestructura.csproj \
  --idempotent \
  --output docs/migracion-inicial-sgv.sql
```

Validación local contra MySQL 8.4:
```
mysql -h <host> -u root -e "DROP DATABASE IF EXISTS sc_test; CREATE DATABASE sc_test;"
mysql -h <host> -u root sc_test < docs/migracion-inicial-sgv.sql
mysql -h <host> -u root sc_test -e "SELECT COUNT(*) FROM __EFMigrationsHistory;"  # → 18
```

## Cambios en archivos

| Archivo | Action | Descripción |
|------|--------|-------------|
| `src/SGV.Infraestructura/Persistencia/Migraciones/20260728120000_FixMySql84GeneratedUniqueIndex.cs` | Create | Migración compensatoria con helper `ConvertUniqueVirtualToStored` |
| `src/SGV.Infraestructura/Persistencia/Migraciones/SgvDbContextModelSnapshot.cs` | No tocar | El snapshot no cambia: la compensatoria restaura el estado de modelo que `InicialSgvo` ya declaraba (STORED via MariaDb snapshot posterior). Solo se verifica que compile y el designer reste consistente. |
| `docs/migracion-inicial-sgv.sql` | Regenerar | `dotnet ef migrations script --idempotent` (artifacto regenerado; diff grande) |
| `tests/SGV.Tests/Persistencia/ScriptStandaloneSmokeMySqlFactTests.cs` | Modify | `ExpectedMigrationCount: 17 → 18` (línea 44) + comentario explicativo |
| `docs/decisiones-implementacion.md` §6 | Modify | Sub-sección "Compatibilidad con MySQL 8.4 LTS": motivo, tabla VIRTUAL/STORED×8.0/8.4, orden migraciones, instrucción de regeneración |

## Testing strategy

| Layer | Qué | Cómo |
|-------|-----|------|
| Unit | Lógica de helper `ConvertUniqueVirtualToStored` | Difícil de testear sin MySQL — el helper emite SQL declarativo. Se omite. |
| Integration | `[MySqlFact]` cadena completa contra MySQL 8.4 LTS remoto | `MySqlTestDatabaseBootstrap.Migrate()` corre las 18 migraciones contra `sgv_test` limpia. |
| Integration | Script standalone smoke | `ScriptStandaloneSmokeMySqlFactTests` con `ExpectedMigrationCount = 18` contra DB efímera |
| Integration | Idempotencia script | `Script_ApplyTwice_IsIdempotent` (ya existe) — segunda corrida no-op |

No se agregan nuevos tests: la suite `[MySqlFact]` existente cubre la cadena completa. Solo se ajusta el conteo esperado.

## Migration / Rollout

No requiere feature flags ni data migration. La compensatoria es DDL idempotente. Rollback vía `dotnet ef migrations remove` + borrar fila en `__EFMigrationsHistory` (documentado en proposal).

## Riesgos y mitigaciones

- **Riesgo**: orden cronológico `FixActivePuestoIdUniqueType` (jul-11) corre ANTES de la compensatoria (jul-28). En MySQL 8.4 fresh, su `DropIndex` fallaría si `InicialSgvo` no creó el index. **Mitigación**: Open Question — validar en apply; si falla, mover timestamp a pre-jul-11.
- **Riesgo**: el patrón `PREPARE/EXECUTE` con `SET @var` requiere `Allow User Variables=true` en MySqlConnector. Documentado en `decisiones-implementacion.md` línea 425. **Mitigación**: el script standalone se aplica con CLI `mysql` que lo soporta nativamente.
- **Riesgo**: re-aplicación del script `--idempotent` contra DB 8.0 con todas las columnas ya STORED+INDEX. **Mitigación**: cada helper detecta estado y omite operaciones; los `IF NOT EXISTS` de `__EFMigrationsHistory` saltean la migración entera si ya aplicó.
- **Riesgo**: `ExpectedMigrationCount = 18` se acopla al conteo — futuras compensatorias exigen re-contar. **Mitigación**: comentario en el test.

## Estimación de changed lines (PR budget 400)

| Archivo | Estimación |
|---|---|
| `20260728120000_FixMySql84GeneratedUniqueIndex.cs` (nuevo) | ~140-180 líneas (helper + 10 invocaciones) |
| `ScriptStandaloneSmokeMySqlFactTests.cs` | ~3 líneas |
| `docs/decisiones-implementacion.md` | ~30-40 líneas |
| `docs/migracion-inicial-sgv.sql` (regenerado) | diff grande (~100-200 líneas por bloques nuevos) |
| `SgvDbContextModelSnapshot.cs` | solo lectura; posiblemente sin diff |
| **Total estimado** | **~270-420 changed lines** |

Está en el límite budget de 400. Si el diff del script `.sql` supera 200 líneas, evaluar división del PR en `(1) migración + test count` y `(2) script regenerado + docs`.

## Open Questions

- [ ] **Orden cronológico vs `FixActivePuestoIdUniqueType` (jul-11)**: la compensatoria propuesta (jul-28) corre DESPUÉS. En MySQL 8.4 fresh donde `InicialSgvo` falló y no creó unique indexes, el `DropIndex` de `FixActivePuestoIdUniqueType` fallaría primero. Validar empíricamente en apply; si confirma, mover timestamp a `20260710120000` (pre-FixActivePuestoIdUniqueType) u orquestar otra compensatoria para ese DropIndex.
- [ ] **Verificar empíricamente que MySQL 8.4 LTS rechaza `CREATE UNIQUE INDEX` sobre GENERATED VIRTUAL**. El supuesto está asumido por proposal/spec pero no validado en este design. Si 8.4 lo acepta (distinto de MaríaDB), el cambio entero queda como no-op y debe re-evaluarse el alcance.
- [ ] **¿Es `SgvDbContextModelSnapshot.cs`Impactado?** La compensatoria restaura el estado del modelo que el snapshot posterior ya refleja. Necesita ejecución `dotnet ef migrations add` para comprobar si genera un diff en el snapshot — si el snapshot ya describe todas las columnas como STORED (porque MariaDb ya las definió así), no hay diff. Verificar en apply.