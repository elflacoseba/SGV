# Delta para sgv-persistence-architecture

## MODIFIED Requirements

### Requirement: Catalog Evolution Exception (REQ-SPA-EVOLUTION-001)

The system MAY introduce a new table + FK + index + contract shape change when **all** of the following conditions are met:

1. **The new table is a read-only, immutable catalog.** It MUST NOT expose HTTP write endpoints (`POST`, `PUT`, `PATCH`, `DELETE`). It MUST NOT carry `IsActive` or `IsDeleted` columns. Its rows MUST be seeded exclusively by an EF Core migration.
2. **The new FK uses `OnDelete(Restrict)`.** Deleting a catalog row referenced by any business entity MUST fail at the database with a foreign key constraint violation. The catalog row is then preserved.
3. **The migration is deterministic and fail-loud.** If a pre-existing free-form string column contains any value that does not correspond to a row in the new catalog's seed, the migration MUST abort the SQL batch with a structured error (`SIGNAL SQLSTATE '45000'` or equivalent) that lists the offending values. The migration MUST NOT perform the `DROP COLUMN` of the free-form string until the backfill is complete and the FK is `NOT NULL`.
4. **The catalog seed uses static, shared `Guid` constants.** A single `internal static class` (located in `SGV.Infraestructura.Persistencia.Catalogos.*Constantes`) is the source of truth. Both the migration's `InsertData` and `DatosSemilla.HasData` MUST reference the same constants. A unit test asserts equality.

The change `cambiar-campo-tipounidad-a-tabla-tipounidadorganizativa` is the first invocation of this exception. The change `implementar-modulo-cargos` is the second invocation. It introduces:

- The table `NivelesCargo` (Id char(36) PK, Codigo varchar(50) UNIQUE NOT NULL, Nombre varchar(100) NOT NULL, ValorNumerico tinyint NOT NULL, Orden int NOT NULL) with check constraint on `ValorNumerico`.
- The column `Cargos.NivelId char(36) NOT NULL` with FK to `NivelesCargo.Id` and `OnDelete(Restrict)`.
- The index `IX_Cargos_NivelId`.
- The contract shape change: `Cargo.Nivel: string` is replaced by `Cargo.NivelId: Guid` (Domain) and `CargoEntity.NivelId: Guid` (Entity). The `CargoDto` exposes `NivelId: Guid` and `NivelNombre: string` (denormalized, joined in the same query).
- The `DROP COLUMN` of the legacy free-form `Cargos.Nivel` string column after backfill.

Any subsequent change that wants to invoke this exception MUST add a new delta to this spec, naming the change explicitly and confirming that all four conditions above are satisfied.
(Previously: the exception listed only the first invocation for TiposUnidadOrganizativa.)

#### Scenario: First invocation of the exception is approved

- **GIVEN** the change `cambiar-campo-tipounidad-a-tabla-tipounidadorganizativa` is being applied
- **WHEN** the migration adds the table, FK, and index
- **AND** the seed is loaded with 7 static Guids
- **AND** the free-form `TipoUnidad` string column is dropped
- **THEN** all four conditions of REQ-SPA-EVOLUTION-001 are satisfied
- **AND** the change is an authorized exception to the `Observable Persistence Invariants` requirement.

#### Scenario: Second invocation of the exception is approved

- **GIVEN** the change `implementar-modulo-cargos` is being applied
- **WHEN** the migration adds the `NivelesCargo` table, the `Cargos.NivelId` FK with `OnDelete(Restrict)`, the `IX_Cargos_NivelId` index, and the contract shape change from `Cargo.Nivel: string` to `Cargo.NivelId: Guid`
- **AND** the seed is loaded with static Guids from a shared constants class
- **AND** the free-form `Cargos.Nivel` string column is dropped after backfill
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