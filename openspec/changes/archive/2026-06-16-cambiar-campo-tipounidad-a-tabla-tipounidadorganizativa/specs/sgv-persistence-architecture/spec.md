# Capability: SGV Persistence Architecture (delta)

> **Status:** ADDED — capability exists at `openspec/specs/sgv-persistence-architecture/spec.md`. This delta adds one new requirement (`REQ-SPA-EVOLUTION-001`) that defines a named, scoped, and reusable exception to the existing `Observable Persistence Invariants` requirement.
> **Change:** `cambiar-campo-tipounidad-a-tabla-tipounidadorganizativa`
> **Cross-spec note:** This is the **first explicit exception** to `Observable Persistence Invariants` since the capability was introduced. The change introduces (a) a new table `TiposUnidadOrganizativa`, (b) a new column `UnidadesOrganizativas.TipoUnidadOrganizativaId`, (c) a new FK with `OnDelete(Restrict)`, (d) a new index on the FK, and (e) a contract shape change on `UnidadOrganizativa` and `UnidadOrganizativaDto`. All five are explicitly authorized by the new requirement below, **scoped to the catalog-evolution pattern** described here.
> **Original spec file is not touched in this phase.** `sdd-archive` will sync this delta when the change is archived.

## Summary of the change

The `Observable Persistence Invariants` requirement in the original spec is preserved verbatim. A new `ADDED` requirement below carves out a controlled exception for **read-only, immutable catalogs** that need to be promoted from a free-form string to a first-class table + FK. The exception is **opt-in** for future changes: any future change that wants to invoke it must declare a delta in this spec.

## ADDED Requirements

### Requirement: Catalog Evolution Exception (NEW — REQ-SPA-EVOLUTION-001)

The system MAY introduce a new table + FK + index + contract shape change when **all** of the following conditions are met:

1. **The new table is a read-only, immutable catalog.** It MUST NOT expose HTTP write endpoints (`POST`, `PUT`, `PATCH`, `DELETE`). It MUST NOT carry `IsActive` or `IsDeleted` columns. Its rows MUST be seeded exclusively by an EF Core migration.
2. **The new FK uses `OnDelete(Restrict)`.** Deleting a catalog row referenced by any business entity MUST fail at the database with a foreign key constraint violation. The catalog row is then preserved.
3. **The migration is deterministic and fail-loud.** If a pre-existing free-form string column contains any value that does not correspond to a row in the new catalog's seed, the migration MUST abort the SQL batch with a structured error (`SIGNAL SQLSTATE '45000'` or equivalent) that lists the offending values. The migration MUST NOT perform the `DROP COLUMN` of the free-form string until the backfill is complete and the FK is `NOT NULL`.
4. **The catalog seed uses static, shared `Guid` constants.** A single `internal static class` (located in `SGV.Infraestructura.Persistencia.Catalogos.*Constantes`) is the source of truth. Both the migration's `InsertData` and `DatosSemilla.HasData` MUST reference the same constants. A unit test asserts equality.

The change `cambiar-campo-tipounidad-a-tabla-tipounidadorganizativa` is the first change to invoke this exception. It introduces:

- The table `TiposUnidadOrganizativa` (Id char(36) PK, Codigo varchar(50) UNIQUE NOT NULL, Nombre varchar(100) NOT NULL).
- The column `UnidadesOrganizativas.TipoUnidadOrganizativaId char(36) NOT NULL` with FK to `TiposUnidadOrganizativa.Id` and `OnDelete(Restrict)`.
- The index `IX_UnidadesOrganizativas_TipoUnidadOrganizativaId`.
- The contract shape change: `UnidadOrganizativa.TipoUnidad: string` is replaced by `UnidadOrganizativa.TipoUnidadOrganizativaId: Guid` (Domain) and `UnidadOrganizativaEntity.TipoUnidadOrganizativaId: Guid` (Entity). The `UnidadOrganizativaDto` exposes `TipoUnidadOrganizativaId: Guid` and `TipoUnidadNombre: string` (denormalized, joined in the same query).

Any subsequent change that wants to invoke this exception MUST add a new delta to this spec, naming the change explicitly and confirming that all four conditions above are satisfied.

#### Scenario: First invocation of the exception is approved

- **GIVEN** the change `cambiar-campo-tipounidad-a-tabla-tipounidadorganizativa` is being applied
- **WHEN** the migration adds the table, FK, and index
- **AND** the seed is loaded with 7 static Guids
- **AND** the free-form `TipoUnidad` string column is dropped
- **THEN** all four conditions of REQ-SPA-EVOLUTION-001 are satisfied
- **AND** the change is an authorized exception to the `Observable Persistence Invariants` requirement.

#### Scenario: Future change invokes the exception correctly

- **GIVEN** a future change `<nombre>` wants to promote a free-form string column to a catalog FK
- **WHEN** the change is proposed
- **THEN** it MUST add a delta to this spec that:
  - Names the change explicitly.
  - Confirms all four conditions of REQ-SPA-EVOLUTION-001.
  - Lists the new table, FK, and contract shape change introduced.
- **AND** until that delta is added, the change violates the `Observable Persistence Invariants` requirement and is rejected.

#### Scenario: Migration fail-loud for dirty data

- **GIVEN** a catalog-evolution migration is running
- **WHEN** any pre-existing free-form string value is not present in the new catalog's seed
- **THEN** the migration MUST abort the SQL batch with `SIGNAL SQLSTATE '45000'`
- **AND** the error message MUST list the offending values (up to 5 examples, comma-separated)
- **AND** the migration MUST NOT proceed to the `DROP COLUMN` step
- **AND** the legacy free-form column MUST remain in the database.

#### Scenario: Seed Guid drift is impossible

- **GIVEN** the migration's `InsertData` and `DatosSemilla.HasData` both reference the same `internal static class` of `Guid` constants
- **WHEN** the unit test `DatosSemilla_SeedIdsMatch<ConstantesClass>` runs
- **THEN** it MUST pass — every `Id` declared in the migration's `InsertData` is present in `DatosSemilla` (and vice versa)
- **AND** the count of distinct `Id`s in both lists is identical.

## MODIFIED Requirements

None at the requirement level. The `Observable Persistence Invariants` requirement remains **unchanged** and **still applies** to all changes that do not satisfy the four conditions of `REQ-SPA-EVOLUTION-001`. The new requirement is a **narrow, opt-in carve-out**, not a weakening of the original invariant.

## REMOVED Requirements

None.
