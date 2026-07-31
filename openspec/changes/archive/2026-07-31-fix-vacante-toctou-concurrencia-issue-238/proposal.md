# Proposal: fix-vacante-toctou-concurrencia-issue-238

## Intent

Cerrar la ventana TOCTOU en la regla de negocio "una sola vacante abierta por puesto" (`VacanteServicioComandos.CrearAsync`) agregando un unique constraint parcial en BD sobre `PuestoId` filtrado por `FechaCierre IS NULL` + `IsDeleted = 0` (patrón de columna generada ya vigente en `CargoConfiguracion`, `PuestoConfiguracion` y `OcupacionConfiguracion`). El catch existente en `CrearAsync` se actualiza para mapear la constraint violation al código de error `PuestoConVacanteAbierta` en lugar de `DatosInvalidos`.

## Scope

### In Scope
- Agregar columna calculada `ActivePuestoIdUnique` + unique index en `VacanteConfiguracion.cs` (patrón `CASE WHEN FechaCierre IS NULL AND IsDeleted = 0 THEN PuestoId ELSE NULL END`, stored, con `ascii_general_ci` collation).
- Migración EF Core forward-only que aplica el nuevo unique index.
- Actualizar el `catch (DbUpdateException)` en `VacanteServicioComandos.CrearAsync:177` para mapear la constraint violation al código `VacanteErrorCodigo.PuestoConVacanteAbierta` (el código correcto de la regla de negocio, ya usado en la verificación pre-existente `ExistsAbiertaByPuestoAsync`).
- Test de concurrencia `[MySqlFact]` que simula la carrera: dos `CrearAsync` concurrentes para el mismo `PuestoId` — una debe recibir `409 Conflict` con `PuestoConVacanteAbierta`.
- Nota de decisión en `apply-progress.md` del change archivado de Vacantes (o como entrada en el change nuevo).

### Out of Scope
- Cambiar el segmento del listado de vacantes de `EstadoVacante.EsTerminal` a `FechaCierre IS NULL` (la equivalencia es semántica, no funcional — la verificación existente sigue siendo necesaria para el pre-check).
- Extender `IUnitOfWork` con `BeginTransaction(IsolationLevel.Serializable)` (Estrategia 2 — sin precedente en el codebase, costo > beneficio).
- Implementar lock pesimista `SELECT ... FOR UPDATE` en `ExistsAbiertaByPuestoAsync` (Estrategia 3 — SQL específico del proveedor, mayor latencia).
- Refactor del `constraintDetector` o del manejo transaccional de otros módulos.

## Capabilities

### New Capabilities
Ninguna. Este change no introduce una nueva capability — fortalece la enforcement de una regla de negocio ya existente dentro de la capability `vacante-management`.

### Modified Capabilities
- `vacante-management` (spec.md existente en `openspec/specs/vacante-management/spec.md`): el requirement `Crear Vacante` escenario "Puesto con vacante abierta" se cumple ahora con defensa en BD además del pre-check en aplicación. No cambia el comportamiento observable ni los escenarios de la spec — la delta se limita al nivel de implementación (defense-in-depth).

## Approach

**Estrategia 1 — Unique constraint parcial via columna generada (patrón vigente)**

La solución replica exactamente el patrón de `OcupacionConfiguracion.cs:42-47`:
1. Se agrega una columna calculada `ActivePuestoIdUnique` en `VacanteEntity`:
   ```csharp
   builder.Property<string?>("ActivePuestoIdUnique")
       .HasMaxLength(36)
       .UseCollation("ascii_general_ci")
       .HasComputedColumnSql(
           "CASE WHEN `FechaCierre` IS NULL AND `IsDeleted` = 0 THEN `PuestoId` ELSE NULL END",
           stored: true)
       .IsRequired(false);
   builder.HasIndex("ActivePuestoIdUnique").IsUnique();
   ```
2. MySQL trata `NULL` en un unique index como valor ignorado — múltiples filas con `NULL` en la columna calculada son válidamente insertables (soft-deleted y cerradas no violan la constraint).
3. La verificación pre-existente `ExistsAbiertaByPuestoAsync(!EsTerminal)` sigue siendo necesaria como pre-check rápido; la BD actúa como fuente de verdad última ante la carrera.

**Equivalencia semántica** (`!EsTerminal` ↔ `FechaCierre IS NULL`):
- `Vacante.CambiarEstado(cerrar=true)` siempre setea `FechaCierre` (`src/SGV.Dominio/Vacantes/Vacante.cs:50`).
- El segmento "Abiertas" filtra por `EstadoVacante.EsTerminal == false`.
- Por lo tanto, `FechaCierre IS NULL` es equivalente a `!EsTerminal` en el flujo vigente — la constraint captura exactamente las vacantes activas.

**Catch block update** (`VacanteServicioComandos.CrearAsync:177`):
- Actualmente mapea constraint violations a `VacanteErrorCodigo.DatosInvalidos`.
- Se actualiza a `VacanteErrorCodigo.PuestoConVacanteAbierta`, alineando el código de error con la semántica de la constraint (el código ya existe y es el mismo usado en el pre-check).

## Affected Areas

| Area | Impact | Description |
|------|--------|-------------|
| `src/SGV.Infraestructura/Persistencia/Configuraciones/VacanteConfiguracion.cs` | Modified | Agregar columna calculada + unique index `ActivePuestoIdUnique` |
| `src/SGV.Infraestructura/Persistencia/Migraciones/` | New migration | Forward-only migration para el nuevo unique index |
| `src/SGV.Aplicacion/Vacantes/Comandos/VacanteServicioComandos.cs` | Modified | Mapear `DbUpdateException` constraint → `PuestoConVacanteAbierta` |
| `tests/SGV.Tests/` | New test | `[MySqlFact]` Concurrencia_CrearAsync_MismaVacanteAbierta_UnaRechazada |
| `openspec/changes/archive/.../apply-progress.md` | Note | Registrar la resolución de D-3.2 |

## Decisions

| Decision | Rationale |
|----------|----------|
| **Unique constraint parcial (Estrategia 1) sobre Estrategias 2 y 3** | El patrón de columna generada tiene precedente directo y probado en `Cargo`, `Puesto` y `Ocupacion`. La BD es la fuente de verdad; la constraint actúa como defensa final ante TOCTOU. `IUnitOfWork` no requiere cambios. El catch block existente ya maneja `DbUpdateException`. Costo de implementación bajo, riesgo mínimo. |
| **No cambiar el segmento `EstadoVacante.EsTerminal` por `FechaCierre IS NULL`** | Equivalencia confirmada: `CambiarEstado(cerrar=true)` siempre setea `FechaCierre`. Modificar el segmento implicaría cambiar el contrato de consulta y los DTOs sin beneficio funcional. El pre-check sigue siendo útil para rechazar temprano sin ir a la BD. |
| **No cambiar `constraintDetector`** | El detector existente ya distingue constraint violations de FK violations. La constraint `ActivePuestoIdUnique` es de tipo `Unique` (MySQL error 1062), diferenciable de la FK que lanza `Ocupacion` (`1452`). No se requiere extensión. |

## Risks

| Risk | Likelihood | Mitigation |
|------|------------|------------|
| MySQL reinvierte la constraint UNIQUE sobre `NULL` de forma inesperada | Low | El patrón `CASE WHEN … ELSE NULL END` + stored computed column con `IsRequired(false)` es idéntico al de `OcupacionConfiguracion` (生产). Test `[MySqlFact]` cubre el escenario. |
| La migración forward-only falla si ya existen filas duplicadas en producción | Low (no aplica en dev) | Se generará script idempotente (`--idempotent`). Si existieran duplicados en prod, la constraint arrojaría error en deploy — se documenta en el rollforward del apply-progress. |
| El catch block `IsConstraintViolation` no detecta la constraint nueva | Low | `MySqlException` 1062 (`ER_DUP_ENTRY`) es universal en Pomelo para unique violations. El detector ya lo reconoce. |

## Rollback Plan

1. Revertir la migración (`dotnet ef migrations remove --project src/SGV.Infraestructura --startup-project src/SGV.Infraestructura`).
2. Eliminar la columna calculada y el unique index de `VacanteConfiguracion.cs`.
3. Restaurar el catch block en `VacanteServicioComandos.CrearAsync` a `VacanteErrorCodigo.DatosInvalidos`.
4. Eliminar el test de concurrencia.
5. Sin efecto sobre otros módulos o datos.

## Dependencies

- MySQL 8 con soporte para computed columns stored (`STORED` keyword en `GENERATED ALWAYS ... AS ... STORED`).
- Pomelo.EntityFrameworkCore.MySql 9.x (ya en uso).
- EF Core 9 (ya en uso).
- `dotnet ef` CLI disponible para generar la migración.

## Success Criteria

- [ ] `dotnet build SGV.slnx` compila sin errores.
- [ ] La constraint `ActivePuestoIdUnique` existe en `VacanteConfiguracion.cs` con la fórmula `CASE WHEN FechaCierre IS NULL AND IsDeleted = 0 THEN PuestoId ELSE NULL END`.
- [ ] El catch block de `CrearAsync` mapea constraint violations a `VacanteErrorCodigo.PuestoConVacanteAbierta`.
- [ ] Test `[MySqlFact] CrearAsync_Concurrencia_MismaVacanteAbierta` pasa: dos creaciones concurrentes para el mismo `PuestoId` — una recibe `201`, la otra `409 Conflict` con código `PuestoConVacanteAbierta`.
- [ ] Suite completa `dotnet test SGV.slnx` pasa sin regresión.
