# Capability: SGV Database (delta)

> **Status:** MODIFIED — capability exists at `openspec/specs/sgv-database/spec.md`. This delta adds two new requirements to the existing `Requisitos AGREGADOS` block. The original spec file is **not** touched in this phase; `sdd-archive` will sync the delta.
> **Change:** `cambiar-campo-tipounidad-a-tabla-tipounidadorganizativa`

## Summary of the change

A new catalog table `TiposUnidadOrganizativa` is introduced. `UnidadesOrganizativas.TipoUnidadOrganizativaId` becomes a `NOT NULL` foreign key with `OnDelete(Restrict)` and a supporting index. The legacy `TipoUnidad varchar(50)` string column is dropped. A fail-loud backfill gate in the EF Core migration aborts the operation (and lists offending values) if any pre-existing `TipoUnidad` string does not correspond to a `Codigo` in the seed.

> **Cross-spec note (open question, not blocking):** The capability `sgv-persistence-architecture` contains a `Observable Persistence Invariants` requirement that forbids new tables, columns, FKs, indexes, and contract changes for refactor-style work. This change is **not** a refactor but an evolutionary schema change, and it is the **first explicit exception** to that invariant. See the `open_questions` block in the orchestrator report.

## ADDED Requirements

### REQ-SDB-NEW — Catálogo `TiposUnidadOrganizativa` con FK `OnDelete(Restrict)`

El sistema DEBE persistir un catálogo inmutable `TiposUnidadOrganizativa` con PK `Id` Guid (`char(36)`), `Codigo varchar(50)` `UNIQUE NOT NULL` y `Nombre varchar(100) NOT NULL`. El catálogo NO DEBE tener columnas `IsActive` ni `IsDeleted`. La columna `UnidadesOrganizativas.TipoUnidadOrganizativaId` DEBE ser una FK `char(36) NOT NULL` con `OnDelete(Restrict)` y DEBE estar indexada.

#### Escenario: Enforcement de la FK

- **DADO** que existe una `UnidadOrganizativa` que referencia el tipo con id `X`
- **CUANDO** se ejecuta `DELETE FROM TiposUnidadOrganizativa WHERE id = X`
- **ENTONCES** MySQL DEBE rechazar la operación con un error de foreign key constraint
- **Y** la fila `X` DEBE permanecer en la tabla.

#### Escenario: Índice sobre la FK

- **DADO** que la migración se ejecutó
- **CUANDO** se consulta `SHOW INDEX FROM UnidadesOrganizativas`
- **ENTONCES** DEBE existir un índice sobre la columna `TipoUnidadOrganizativaId`
- **Y** ese índice DEBE ser el que usa la FK en `REFERENCES`.

#### Escenario: Catálogo sin flags de estado

- **DADO** que existe la tabla `TiposUnidadOrganizativa`
- **CUANDO** se consultan sus columnas con `DESCRIBE TiposUnidadOrganizativa`
- **ENTONCES** NO DEBE existir una columna `IsActive` ni una columna `IsDeleted`.

### REQ-SDB-NEW-BACKFILL — Migración fail-loud con pre-flight de strings sucios

La migración que introduce la FK `TipoUnidadOrganizativaId` DEBE ejecutar un `SELECT` de pre-flight que liste todo valor distinto de `UnidadesOrganizativas.TipoUnidad` (string) que no se corresponda con un `Codigo` del seed. Si existe al menos un valor ofensivo, la migración DEBE abortar lanzando `InvalidOperationException` con un mensaje que liste los valores ofensivos, **sin** hacer backfill ni `DROP COLUMN`. Si no hay valores ofensivos, el backfill completa, la columna FK queda `NOT NULL` y la columna string se elimina.

#### Escenario: Backfill limpio

- **DADO** que todas las filas existentes en `UnidadesOrganizativas.TipoUnidad` tienen un valor que coincide con un `Codigo` del seed (por ejemplo, todas son `Direccion`, `Area`, `Departamento`, etc.)
- **CUANDO** la migración corre
- **ENTONCES** el backfill de `TipoUnidadOrganizativaId` desde el `Codigo` DEBE completarse
- **Y** la columna `TipoUnidadOrganizativaId` DEBE quedar `NOT NULL`
- **Y** la columna string `TipoUnidad` DEBE eliminarse con `DROP COLUMN`.

#### Escenario: Fail-loud aborta antes del ALTER

- **DADO** que al menos una fila tiene `TipoUnidad = "FooBar"` (un valor que no aparece en el seed de códigos)
- **CUANDO** la migración corre
- **ENTONCES** DEBE lanzar `InvalidOperationException`
- **Y** el mensaje de la excepción DEBE listar el o los valores ofensivos (por ejemplo, `["FooBar"]`)
- **Y** la migración DEBE detenerse **antes** de cualquier `ALTER TABLE` que cambie `TipoUnidadOrganizativaId` a `NOT NULL`
- **Y** la columna `TipoUnidad` (string) DEBE permanecer intacta en la base de datos.

#### Escenario: Seed presente después de la migración

- **DADO** que la migración corrió sobre una base de datos limpia
- **CUANDO** se consulta `SELECT COUNT(*) FROM TiposUnidadOrganizativa`
- **ENTONCES** el resultado DEBE ser 7
- **Y** los 7 códigos (`Institucion`, `Facultad`, `Secretaria`, `Direccion`, `Departamento`, `Division`, `Area`) DEBEN estar presentes.

## MODIFIED Requirements

None at the requirement level. The new requirements above are additive and do not contradict any existing requirement in this capability. (See cross-spec note in the summary about `sgv-persistence-architecture`.)

## REMOVED Requirements

None.
