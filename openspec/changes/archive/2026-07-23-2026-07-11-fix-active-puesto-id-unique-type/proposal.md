# Proposal: Fix `ActivePuestoIdUnique` (issue #59)

## Intent

`ActivePuestoIdUnique` se declaró como `int?` en EF (`OcupacionConfiguracion.cs:35`), pero `PuestoId` es `char(36)` (snapshot 1019-1020; migración inicial 610). MySQL evalúa la columna generada `CASE WHEN ... THEN PuestoId ELSE NULL END` en cada INSERT/UPDATE y trunca `'a1b2c3d4-...'` a `0` o falla con `Data truncated for column 'ActivePuestoIdUnique' at row 1` bajo `STRICT_TRANS_TABLES`. Cualquier `OcupacionEntity` activa (`FechaFin IS NULL AND IsDeleted = 0`) es rechazada por MySQL → bloquea `OcupacionServicioComandos.CrearAsync` y **12/15 tests `[MySqlFact]`** de `OcupacionRepositoryTests` fallan en CI contra MySQL real. `docs/migracion-inicial-sgv.sql:533` propaga el bug a deployments frescos. Se cierra el **issue #59** alineando el tipo al patrón vigente (`ActivePersonaIdUnique` ya usa `char(36)` en `Postulantes`, snapshot 1257-1263), preservando la decisión arquitectónica "columna generada + índice único" en `docs/decisiones-implementacion.md:11-13` y `sgv-database/spec.md:292-296`.

## Scope

### In Scope

- Editar `OcupacionConfiguracion.cs:35-38`: `int?` → `string?` + `.HasMaxLength(36).UseCollation("ascii_general_ci")`.
- Nueva migración EF `FixActivePuestoIdUniqueType` con `UPDATE` defensivo pre-alter, `DROP INDEX → AlterColumn → CREATE INDEX` en una transacción. `Down()` lanza `NotSupportedException`.
- Extender `Modelo_ConfiguraColumnaGeneradaUnicaParaOcupacionVigentePorPuesto` (`ModeloPersistenciaTests.cs:44-61`) con `Assert.Equal(typeof(string), prop.ClrType)` y `Assert.Contains("char(36)", prop.GetColumnType())`.
- Agregar test canario `[MySqlFact]` que inserte ocupación activa, haga `SaveChangesAsync` y lea la columna vía `Database.SqlQueryRaw<string>` para validar `puestoId.ToString()`.
- Regenerar `docs/migracion-inicial-sgv.sql` y `SgvDbContextModelSnapshot.cs`.
- Reemplazar bloque `AGENTS.md:181-186` por línea breve de cierre.

### Out of Scope

- Cambiar tipo de `PuestoId` ni de otras FK Guid (`puesto-management/spec.md:19-44` expone `id: Guid`).
- Refactorizar PKs a auto-incrementales ni tablas puente `Guid ↔ Int`.
- Drop de `ActivePuestoIdUnique` ni mover unicidad activa a lógica de aplicación (rompería la decisión documentada y abre condición de carrera).
- Adoptar `CRC32(PuestoId)` u otras funciones hash (colisiones inaceptables).
- Migrar datos de `TipoAsignacion`, semillas u otras tablas.

## Capabilities

### New Capabilities
Ninguna.

### Modified Capabilities
- `sgv-database`: el requisito "Historial de Ocupaciones" (`spec.md:298-325`) ya documenta el invariante "una sola ocupación activa por Puesto" — el fix **materializa** ese invariante, no cambia el contrato. Una nota de mantenimiento queda a criterio de `sdd-spec`.

## Approach

Opción 1 de `exploration.md`: alterar el shadow property a `string?` con `HasMaxLength(36).UseCollation("ascii_general_ci")` para coincidir con `PuestoId char(36)`. La nueva migración ejecuta `UPDATE Ocupaciones SET ActivePuestoIdUnique = NULL WHERE FechaFin IS NULL AND IsDeleted = 0` **antes** del `AlterColumn` (purga los `0` truncados preexistentes; no-op en CI/dev); el ALTER regenera la expresión computada y los valores correctos vuelven a calcularse. MySQL exige `DROP INDEX → ALTER → CREATE INDEX` en una transacción. Patrón análogo ya aplicado en `Migraciones/20260624153353_ConvertirTipoAsignacionAEnumYActualizarUnicidad.cs:14-20, 99-110` para `ActivePersonaIdUnique`.

## Decisiones tomadas (confirmadas con el usuario)

1. **UPDATE defensivo en la migración**: `UPDATE Ocupaciones SET ActivePuestoIdUnique = NULL WHERE FechaFin IS NULL AND IsDeleted = 0` antes del `AlterColumn`. No-op en CI/dev fresco.
2. **Forward-only**: `Down()` lanza `NotSupportedException("Migración forward-only. Para revertir, escribir una migración correctiva explícita.")`. Bloquea rollback accidental.
3. **Test canario**: `[MySqlFact]` que inserta `OcupacionEntity` activa con `PuestoId = Guid.NewGuid()`, hace `SaveChangesAsync`, lee `ActivePuestoIdUnique` vía `Database.SqlQueryRaw<string>` y asserta que coincide con `puestoId.ToString()`.
4. **`ModeloPersistenciaTests` endurecido**: agregar al test existente `Assert.Equal(typeof(string), prop.ClrType)` y `Assert.Contains("char(36)", prop.GetColumnType())`.
5. **`AGENTS.md`**: reemplazar líneas 181-186 con una línea breve: `Cerrado por change archivado 2026-07-11-fix-active-puesto-id-unique-type (migración FixActivePuestoIdUniqueType).`

## Affected Areas

| Área | Impacto |
|------|---------|
| `src/SGV.Infraestructura/Persistencia/Configuraciones/OcupacionConfiguracion.cs` | Modified (tipo shadow computed) |
| `src/SGV.Infraestructura/Persistencia/Migraciones/` | New (`FixActivePuestoIdUniqueType`) |
| `src/SGV.Infraestructura/Persistencia/Migraciones/SgvDbContextModelSnapshot.cs` | Regenerado (auto por `migrations add`) |
| `docs/migracion-inicial-sgv.sql` | Regenerado (`migrations script --idempotent`) |
| `tests/SGV.Tests/Persistencia/ModeloPersistenciaTests.cs` | Modified (aserciones de tipo) |
| `tests/SGV.Tests/Persistencia/OcupacionRepositoryTests.cs` | Modified (1 test canario nuevo; 12 fail→pass) |
| `AGENTS.md` | Modified (bloque del bug reemplazado) |

## Impacto

- **Usuarios**: inserciones de Ocupaciones activas vuelven a funcionar; `POST /api/ocupaciones` no explota con `Data truncated`.
- **Datos**: `UPDATE` defensivo purga los `0` truncados antes del `AlterColumn`; idempotente y no destructivo.
- **API**: sin cambios de contrato (wire-types en `SGV.Contracts` intactos).
- **Tests**: 12 fail→pass en `OcupacionRepositoryTests`, 1 test canario nuevo, 1 test estructural endurecido. Suite total verde en CI.

## Risks

| Riesgo | Prob. | Mitigación |
|--------|-------|------------|
| Filas activas preexistentes con `ActivePuestoIdUnique = 0` rompen unicidad al pasar a `varchar(36)` (`'0'` colapsa múltiples filas) | Crítico | `UPDATE ... SET ... = NULL WHERE FechaFin IS NULL AND IsDeleted = 0` antes del `AlterColumn`. No-op en CI/dev. Auditoría humana en producción. |
| Drift snapshot vs migraciones | Media | `dotnet ef migrations add` regenera el snapshot; `git diff` obligatorio en apply. |
| Tests `[MySqlFact]` skipeados sin MySQL local | Media | Verificación final en CI (`.github/workflows/ci.yml`); documentar en `tasks.md`. |
| Variabilidad de `sql_mode` (permisivo vs `STRICT_TRANS_TABLES`) | Baja | Purga incondicional cubre ambos casos. |
| `Down()` ausente bloquea rollback operacional | Baja (intencional) | Forward-only por decisión 2; correctivo explícito si falla real (ver §Rollback). |

## Rollback Plan

Migración **forward-only**: `Down()` lanza `NotSupportedException`. Si falla en producción real:

1. **No** ejecutar `dotnet ef migrations remove` (crearía conflicto inverso).
2. Escribir **migración correctiva explícita** que revierta `char(36)` → `int` aplicando el mismo `UPDATE` defensivo inverso.
3. `ActivePersonaPuestoUnique` (`varchar(100)`) sigue enforcing "una Persona + un Puesto activos" durante la ventana de transición — garantía funcional se mantiene aunque `ActivePuestoIdUnique` quede momentáneamente fuera del índice.

## Dependencies

- **Precedente**: `Migraciones/20260624153353_ConvertirTipoAsignacionAEnumYActualizarUnicidad.cs:14-20, 99-110` (drop+recreación análoga de `ActivePersonaIdUnique`).
- **Decisión arquitectónica vigente**: `docs/decisiones-implementacion.md:11-13`, `sgv-database/spec.md:292-296`.
- **Issue de trazabilidad**: GitHub #59.
- Sin nuevas dependencias externas (mismo Pomelo 9.x, mismo `Database.Migrate()`).

## Trazabilidad

- **Issue**: GitHub #59 — fuente canónica del bug.
- **Exploración**: `openspec/changes/2026-07-11-fix-active-puesto-id-unique-type/exploration.md` — root cause, 4 opciones evaluadas, recomendación Opción 1, lista completa de tests fail→pass, gotchas.
- **Precedente del patrón**: migración `20260624153353` (fix análogo sobre `ActivePersonaIdUnique`).

## Success Criteria

- [ ] `dotnet ef migrations add FixActivePuestoIdUniqueType` genera una sola migración con `Up` que ejecuta el `UPDATE` defensivo + `DROP INDEX` + `AlterColumn<string>` + `CREATE INDEX`, y `Down()` que lanza `NotSupportedException`.
- [ ] `Modelo_ConfiguraColumnaGeneradaUnicaParaOcupacionVigentePorPuesto` asserta `typeof(string)` y `GetColumnType()` contiene `"char(36)"`.
- [ ] Test canario `AddAsync_FilaActiva_ActivePuestoIdUniquePersisteComoGuidString` pasa contra MySQL real en CI.
- [ ] Los 12 tests de `OcupacionRepositoryTests` que hoy fallan pasan (lista en `exploration.md §"Tests que deben pasar al aplicar el fix"`); los 3 ya verdes siguen verdes.
- [ ] `dotnet test SGV.slnx --no-build --configuration Release` en CI muestra 0 fallos en `OcupacionRepositoryTests`.
- [ ] `docs/migracion-inicial-sgv.sql:533` regenerado contiene el shadow computed con `varchar(36)` o `char(36)`.
- [ ] `AGENTS.md` ya no menciona issue #59 como bug abierto; muestra la línea de cierre del change.
