# SGV Persistence Architecture

## Requirements

### Requirement: EF Persistence Model Boundary

The system MUST keep Entity Framework persistence models in the Infrastructure layer and MUST NOT require Domain entities to know about Entity Framework mapping, tracking, or configuration concerns. EF-mapped SGV infrastructure persistence types MUST be identifiable as persistence types by using the `Entity` suffix, except framework-owned Identity internals.

#### Scenario: Domain model remains EF-agnostic

- GIVEN the SGV persistence model is used by Infrastructure
- WHEN Domain entities are inspected as business model types
- THEN they MUST NOT require EF Core mapping metadata or persistence configuration
- AND they MUST remain usable as Domain concepts independent of the database provider.

#### Scenario: EF-mapped SGV tables use persistence entities

- GIVEN an SGV table is mapped by the Infrastructure persistence context
- WHEN the mapped CLR type represents SGV application data
- THEN the mapped type MUST be an Infrastructure persistence type suffixed with `Entity`
- AND framework-owned Identity internals MAY keep their provider-owned types.

### Requirement: Observable Persistence Invariants

This refactor MUST preserve the existing database schema, persisted seed content, query results, repository-visible behavior, and public application/API contracts. It MUST NOT introduce table renames, column renames, key changes, index changes, constraint changes, data transformations, or contract shape changes.

#### Scenario: Schema remains unchanged

- GIVEN the current SGV MySQL/Pomelo persistence schema is the baseline
- WHEN the refactor is applied and persistence metadata is compared to the baseline
- THEN the database tables, columns, keys, indexes, constraints, and relationships MUST remain equivalent.

#### Scenario: Consumers observe the same behavior

- GIVEN existing persisted data and seed data are available
- WHEN application repositories or public read-only API contracts are exercised
- THEN returned results and contract shapes MUST remain equivalent to the pre-refactor behavior
- AND no new behavior or unsupported operation MUST be exposed.

### Requirement: Audit Logical Name Preservation

Audit records MUST preserve the existing logical entity names and observable audit semantics. Persistence CLR type names introduced for the refactor MUST NOT leak `Entity` suffixes into audit data when that would change previously observable logical names.

#### Scenario: Audit entries keep logical entity names

- GIVEN an audited SGV entity is created, modified, or deleted
- WHEN audit records are persisted after the refactor
- THEN the audited entity name MUST match the pre-refactor logical name
- AND audit operation, entity identifier, user, timestamp, old values, and new values MUST retain their observable semantics.

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

### Requirement: Identity Infrastructure Boundary

The system MUST treat authentication users and roles as Infrastructure/API concerns and MUST NOT require Domain entities to depend on Identity framework types. Application-facing contracts MAY describe authenticated users and roles, but MUST NOT expose persistence entities or framework-owned Identity internals as SGV Domain models.

#### Scenario: Domain remains Identity-agnostic

- GIVEN authentication support is enabled for SGV
- WHEN Domain model types are inspected
- THEN they MUST NOT depend on Identity framework types
- AND Persona MUST remain a Domain concept independent of authentication storage.

#### Scenario: Consumer contracts hide framework internals

- GIVEN a consumer manages users or roles
- WHEN the system returns user or role data
- THEN the response MUST use consumer-safe contracts
- AND MUST NOT expose persistence tracking or framework-owned internals.

### Requirement: Approved Identity Persistence Evolution

The system MAY introduce Identity-specific persistence customization only to satisfy SGV authentication behavior: mandatory Persona association, fixed first-slice roles, and role assignments constrained to that catalog. This evolution MUST preserve the Clean Architecture boundary described by the persistence model requirements.

#### Scenario: Identity persistence change is scoped

- GIVEN this change introduces user and role management
- WHEN persistence behavior changes for authentication data
- THEN the change MUST be limited to mandatory Persona linkage and fixed role catalog behavior
- AND MUST NOT alter unrelated SGV domain persistence behavior.
