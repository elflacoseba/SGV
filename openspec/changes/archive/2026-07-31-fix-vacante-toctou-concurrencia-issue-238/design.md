# Design: fix-vacante-toctou-concurrencia-issue-238

## Contexto

La desviación **D-3.2** del change archivado `2026-07-30-feature-implementar-modulo-vacantes` documentó una ventana TOCTOU en `VacanteServicioComandos.CrearAsync`: el pre-check `ExistsAbiertaByPuestoAsync` y el `SaveChangesAsync` no son atómicos, por lo que dos `POST /api/v1/vacantes` concurrentes para el mismo `PuestoId` pueden pasar el pre-check ambos y persistir dos vacantes abiertas. Este change cierra esa ventana con un unique constraint parcial en BD (defense-in-depth), replicando el patrón probado de `OcupacionConfiguracion.cs:42-47`. El pre-check se conserva como rechazo temprano; la BD es fuente de verdad ante carrera.

## Enfoque técnico

Estrategia 1 del proposal: columna calculada `ActivePuestoIdUnique` + unique index sobre `VacanteEntity`. La columna evalúa a `PuestoId` cuando la vacante está abierta (`FechaCierre IS NULL AND IsDeleted = 0`), y a `NULL` en caso contrario — MySQL ignora `NULL` en unique indexes, permitiendo múltiples filas cerradas/soft-deleted. El catch existente en `CrearAsync:177` se actualiza de `DatosInvalidos` a `PuestoConVacanteAbierta`, alineando el código de error con la semántica de la constraint.

## Decisiones arquitectónicas

| ID | Opción | Tradeoff | Decisión |
|----|--------|----------|----------|
| **D-1** | Computed column `stored` vs `virtual` | `stored` ocupa disco y se actualiza al write; `virtual` recalcula al read (más CPU por query, no indexable directamente) | **Stored** — paridad con `OcupacionConfiguracion`; requerido por MySQL para indexar la columna |
| **D-2** | Fórmula `CASE WHEN FechaCierre IS NULL AND IsDeleted = 0 THEN PuestoId ELSE NULL END` | Alternativa: filtrar por `EstadoVacante.EsTerminal` (join a catálogo en computed SQL, no soportado en generated column) | **FechaCierre + IsDeleted** — sin join; equivalencia `!EsTerminal ↔ FechaCierre IS NULL` confirmada en `Vacante.CambiarEstado(cerrar=true)` (siempre setea `FechaCierre`) |
| **D-3** | Tipo `varchar(36)` + collation `ascii_general_ci` | `char(36)` es fixed-width pero EF Core 9 + Pomelo 9 lanzan `NullReferenceException` al combinar `HasColumnType` + `HasComputedColumnSql` + `string` (ver comentario `OcupacionConfiguracion.cs:36-41`) | **`HasMaxLength(36)` sin `HasColumnType`** — paridad exacta con `OcupacionConfiguracion`; `ascii_general_ci` para comparación binaria de GUIDs |
| **D-4** | Nombre index `IX_Vacantes_ActivePuestoIdUnique` | Alternativa: `IX_Vacantes_PuestoId_Active` | **`IX_Vacantes_ActivePuestoIdUnique`** — paridad con `IX_Ocupaciones_ActivePuestoIdUnique` (convención del módulo precedent) |
| **D-5** | Forward-only migration (sin `Down`) | Reversión manual requerida si se necesita rollback | **Forward-only** — paridad con `FixActivePuestoIdUniqueType`; `Down` lanza `NotSupportedException` |
| **D-6** | Catch `IsConstraintViolation` → `PuestoConVacanteAbierta` | Sólo `CrearAsync:177`; `CambiarEstadoAsync:286` y `ActualizarObservacionesAsync:358` retienen `DatosInvalidos` | **Sólo `CrearAsync:177`** — el dominio `CambiarEstado(cerrar=false)` no limpia `FechaCierre`, por lo que `CambiarEstadoAsync` no puede disparar `ActivePuestoIdUnique` (la columna queda `NULL` tras el cierre). El scenario spec "Reabrir vacante" es defense-in-depth documental, no alcanzable vía API hoy |
| **D-7** | Conservar `ExistsAbiertaByPuestoAsync` | Eliminarlo delegando todo a la BD ahorraría una query | **Conservar** — rechazo temprano sin round-trip a `SaveChanges`; la constraint es defensa final, no reemplazo del pre-check |

## Flujo de datos

```
CrearAsync(request)
  ├─ Validar (FluentValidation)
  ├─ ExistsAbiertaByPuestoAsync ──→ false (ambos hilos pasan)  ← TOCTOU aquí
  ├─ AddAsync(vacante) + SaveChangesAsync
  │       └─ EF transacción implícita ──→ INSERT
  │                ├─ Hilo A: computed=PuestoId ──→ 201 OK
  │                └─ Hilo B: computed=PuestoId ──→ MySQL 1062 (ER_DUP_ENTRY)
  └─ catch DbUpdateException (IsConstraintViolation) ──→ PuestoConVacanteAbierta 409
```

## Cambios por archivo

| Archivo | Acción | Descripción |
|---------|--------|-------------|
| `src/SGV.Infraestructura/Persistencia/Configuraciones/VacanteConfiguracion.cs` | Modify | Agregar `Property<string?>("ActivePuestoIdUnique")` con `HasMaxLength(36)` + `UseCollation("ascii_general_ci")` + `HasComputedColumnSql("CASE WHEN `FechaCierre` IS NULL AND `IsDeleted` = 0 THEN `PuestoId` ELSE NULL END", stored: true)` + `HasIndex("ActivePuestoIdUnique").IsUnique()`. Eliminar el comentario obsoleto líneas 31-33 ("application logic enforces FechaCierre") |
| `src/SGV.Infraestructura/Persistencia/Migraciones/<ts>_AddActivePuestoIdUniqueToVacantes.cs` | Create | Forward-only: `AddColumn` (computed `varchar(36)`, `nullable: true`, collation) + `CreateIndex` unique. `Down` lanza `NotSupportedException` |
| `src/SGV.Aplicacion/Vacantes/Comandos/VacanteServicioComandos.cs` | Modify | Línea 177-185: cambiar `VacanteErrorCodigo.DatosInvalidos` → `VacanteErrorCodigo.PuestoConVacanteAbierta` y mensaje a "Ya existe una vacante abierta para el puesto especificado." (paridad con el pre-check línea 152). `CambiarEstadoAsync:286` y `ActualizarObservacionesAsync:358` **sin cambios** |
| `tests/SGV.Tests/Api/Vacantes/VacantesConcurrenciaTests.cs` | Create | `[MySqlFact]` carrera: dos `POST /api/v1/vacantes` concurrentes (mismo `PuestoId`, `Task.WhenAll`, clientes distintos) → 1×201 + 1×409 con `PuestoConVacanteAbierta`. Patrón `SetupConcurrencyMySqlFactTests` |

## Atomicidad

EF Core envuelve cada `SaveChangesAsync` en una transacción implícita (comportamiento default). La constraint violation (MySQL 1062) se detecta al momento del `INSERT`, dentro de esa transacción, que EF aborta automáticamente. El `catch (DbUpdateException) when (IsConstraintViolation(ex))` captura post-rollback. No se requiere `BeginTransaction` explícito ni `IsolationLevel.Serializable`.

## Migración de datos

**Riesgo de duplicados existentes**: bajo. Las reglas de aplicación (`ExistsAbiertaByPuestoAsync`) ya filtraban creates concurrentes en el caso normal; sólo la ventana TOCTOU pudo producir duplicados, y la operación es de baja frecuencia (apertura manual por `GestorVacantes`). Procedimiento de producción:

1. Antes de deploy, ejecutar query de detección: `SELECT PuestoId, COUNT(*) FROM Vacantes WHERE FechaCierre IS NULL AND IsDeleted = 0 GROUP BY PuestoId HAVING COUNT(*) > 1`.
2. Si devuelve filas, resolver manualmente (cerrar todas menos una por negoiso) antes de aplicar la migración — la constraint fallaría al crear el indexUnique sobre duplicados.
3. La migración es segura en dev/CI (databases frescas).

## Plan de rollback

Forward-only: la migración no expone `Down` (lanza `NotSupportedException`, paridad con `FixActivePuestoIdUniqueType`). Para revertir:
1. Autorizar una migración correctiva explícita: `DropIndex("IX_Vacantes_ActivePuestoIdUnique")` + `DropColumn("ActivePuestoIdUnique")`.
2. Revertir el mapeo en `VacanteConfiguracion.cs` y el catch en `VacanteServicioComandos.cs:177`.
3. Sin efecto sobre datos existentes (la columna calculada es derivada).

## Estrategia de testing

| Capa | Qué | Cómo |
|------|-----|------|
| Integración DB | Constraint rechaza duplicado; cerrada/soft-deleted no viola | `[MySqlFact]` en `VacantesConcurrenciaTests` — `Task.WhenAll` con dos creates, assert 1×201 + 1×409 `PuestoConVacanteAbierta`; test separado: cerrar vacante + crear nueva para mismo puesto → 201 |
| Unit | Catch mapea a `PuestoConVacanteAbierta` | Fake repo que lanza `DbUpdateException` en `SaveChangesAsync`; assert `ErrorCategoria.Conflict` + código |
| Regresión | Suite existente sin cambios | `dotnet test SGV.slnx` verde |

## Threat Matrix

N/A — sin routing/shell/subprocess/VCS/PR automation/process-integration boundary.

## Open Questions

- [ ] **OQ-1**: ¿Crear subcarpeta `tests/SGV.Tests/Api/Vacantes/` o ubicar el test junto a `VacantesControllerTests.cs`? Aplicar sigue la propuesta; si la convención es flat, mover.
- [ ] **OQ-2 (no bloqueante)**: El scenario spec "Reabrir vacante cerrada" no es alcanzable hoy (`CambiarEstado` no limpia `FechaCierre`). ¿Dejarlo como defense-in-depth documental o cerrar la gap del dominio en este change? Propuesta: dejarlo documental (out of scope del fix TOCTOU).

## Notas para tasks/apply

- `IConstraintViolationDetector` ya registrado (usado por Ocupaciones) — reutilizar sin cambios.
- El migration generator de EF (`dotnet ef migrations add`) produce `AddColumn` + `CreateIndex` automáticamente desde el cambio en `VacanteConfiguracion.cs`; ajustar a mano para inyectar el constraint name exacto y el `Down` forward-only.
- El mensaje del catch debe ser el mismo del pre-check (line 152) para garantizar paridad semántica de error al cliente.