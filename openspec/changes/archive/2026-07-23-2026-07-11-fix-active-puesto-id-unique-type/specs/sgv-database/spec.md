# Delta para sgv-database

## Purpose

Este delta captura el invariante técnico que **debe** cumplirse cuando una columna generada con `HasComputedColumnSql` referencia una columna base `Guid` mapeada como `char(36)` con colación `ascii_general_ci`. El invariante surgió del issue #59: `ActivePuestoIdUnique` se había declarado como `int?` aunque computaba el contenido de `PuestoId char(36)`, provocando `Data truncated` en MySQL bajo `STRICT_TRANS_TABLES`. La invariante es transversal a cualquier futura columna generada que indexe un FK Guid y debe evitar regresiones futuras.

## ADDED Requirements

### Requirement: Coincidencia de tipo entre columna generada y columna fuente

Cuando una entidad persista una restricción de unicidad activa implementada vía columna generada con `HasComputedColumnSql`, y la expresión de la columna generada referencie una columna base `Guid` mapeada como `char(36)` con colación `ascii_general_ci`, el sistema DEBE declarar el shadow property con `ClrType == typeof(string)`, `HasMaxLength(36)` y `UseCollation("ascii_general_ci")`. El tipo de almacenamiento MySQL DEBE coincidir textualmente con el de la columna referenciada, sin coerción implícita ni truncado. La migración que modifique el tipo de la columna generada DEBE ejecutar un `UPDATE <tabla> SET <columna_generada> = NULL WHERE <condición_de_activo>` antes del `AlterColumn`, y DEBE recrear el índice único dentro de la misma operación (`DROP INDEX → ALTER → CREATE INDEX`).

(Anteriormente: el spec no explicitaba la invariante de coincidencia de tipos entre una columna generada y la columna base que referencia. El cambio documenta el invariante a raíz del issue #59 para prevenir regresiones futuras por mismatched types.)

#### Scenario: Inserción de OcupacionEntity activa persiste el Guid como string

- **DADO** una `OcupacionEntity` activa (con `FechaFin IS NULL` y `IsDeleted = 0`) y `PuestoId = Guid.NewGuid()`
- **CUANDO** el repositorio ejecuta `SaveChangesAsync()` contra MySQL
- **ENTONCES** la columna generada `ActivePuestoIdUnique` DEBE almacenar el valor `PuestoId.ToString()` de 36 caracteres
- **Y** el comando DEBE completarse sin disparar `MySqlException: Data truncated for column 'ActivePuestoIdUnique'`.

#### Scenario: Duplicado activo por Puesto se rechaza por unicidad, no por truncado

- **DADO** una `OcupacionEntity` activa persistida para `PuestoId = X`
- **CUANDO** se intenta persistir una segunda `OcupacionEntity` activa con el mismo `PuestoId = X`
- **ENTONCES** MySQL DEBE rechazar la operación por violación del índice único `IX_Ocupaciones_ActivePuestoIdUnique`
- **Y** la capa de aplicación DEBE traducir la violación a un error de conflicto semánticamente claro (distinto del mensaje `Data truncated` previo al fix).

#### Scenario: El modelo EF declara `ActivePuestoIdUnique` como `string`/`char(36)`

- **DADO** la configuración de `OcupacionConfiguracion.cs` registrada en `SgvDbContext`
- **CUANDO** se inspecciona el modelo relacional (`_contexto.Model`)
- **ENTONCES** el shadow property `ActivePuestoIdUnique` DEBE tener `ClrType == typeof(string)`
- **Y** `GetColumnType()` DEBE contener el literal `char(36)` (case-insensitive)
- **Y** la propiedad DEBE conservar la expresión `HasComputedColumnSql` que devuelve `PuestoId` cuando la fila está activa y `NULL` en otro caso.

#### Scenario: Migración con purga defensiva pre-alter y forward-only

- **DADO** la migración `FixActivePuestoIdUniqueType` aplicada contra una base con filas activas históricas (donde `ActivePuestoIdUnique` podría contener `0` por truncado permisivo previo)
- **CUANDO** el método `Up` ejecuta
- **ENTONCES** la migración DEBE ejecutar `UPDATE Ocupaciones SET ActivePuestoIdUnique = NULL WHERE FechaFin IS NULL AND IsDeleted = 0` antes del `AlterColumn`
- **Y** DEBE ejecutar `DROP INDEX IX_Ocupaciones_ActivePuestoIdUnique → ALTER COLUMN ... char(36) → CREATE UNIQUE INDEX IX_Ocupaciones_ActivePuestoIdUnique (ActivePuestoIdUnique)` en una sola transacción
- **Y** el método `Down` DEBE lanzar `NotSupportedException` (forward-only intencional; revertir requiere migración correctiva explícita).
