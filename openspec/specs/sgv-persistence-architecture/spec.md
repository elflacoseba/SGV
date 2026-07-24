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
3. **The migration is deterministic and safe.** Before the backfill the migration MUST run a pre-flight that lists every distinct value in the legacy free-form string column that does not match a `Codigo` of the new catalog's seed. The migration MUST NOT perform the `DROP COLUMN` of the free-form string until the backfill is complete and the FK is in place. By default the pre-flight MUST abort the SQL batch with a structured error (`SIGNAL SQLSTATE '45000'` or equivalent) listing the offending values. A change MAY explicitly opt in to a relaxed variant of this condition when the FK is **nullable** (`char(36) NULL`) and the application can survive `NumeroDocumento` orphan values: under that variant unknown legacy values are persisted with the FK column set to `NULL` and the original string column value (or equivalent identifier) preserved for post-deploy remediation, the migration MUST still complete the `DROP COLUMN`, and the audit interceptor MUST record the transition (`legacy string → NULL` or, when the legacy value is preserved in another column, `legacy string → NULL`) in `Auditorias`. Any change opting into the relaxed variant MUST name it explicitly in this spec and document the mitigation strategy.
4. **The catalog seed uses static, shared `Guid` constants.** A single `internal static class` (located in `SGV.Infraestructura.Persistencia.Catalogos.*Constantes`) is the source of truth. Both the migration's `InsertData` and `DatosSemilla.HasData` MUST reference the same constants. A unit test asserts equality.

The change `cambiar-campo-tipounidad-a-tabla-tipounidadorganizativa` is the first invocation of this exception. The change `implementar-modulo-cargos` is the second invocation. The change `2026-07-20-147-tipos-documento-catalogo` is the third invocation. It introduces:

- The table `TiposDocumento` (Id char(36) PK, Codigo varchar(50) UNIQUE NOT NULL, Nombre varchar(100) NOT NULL, PatronValidacion varchar(255) NULL, LongitudMinima int NULL, LongitudMaxima int NULL).
- The column `Personas.TipoDocumentoId char(36) NULL` (nullable on purpose) with FK to `TiposDocumento.Id` and `OnDelete(Restrict)`.
- The index `IX_Personas_TipoDocumentoId`.
- The reconstructed generated column `Personas.ActiveDocumentoUnique` with formula `CONCAT(TipoDocumentoId, ':', NumeroDocumento)` for active rows (`IsDeleted = 0`), preserving the active uniqueness contract.
- The contract shape change: `Persona.TipoDocumento: string?` is replaced by `Persona.TipoDocumentoId: Guid?` (Domain) and `PersonaEntity.TipoDocumentoId: Guid?` (Entity). The `PersonaDto` exposes `TipoDocumentoId: Guid?` and `TipoDocumento: TipoDocumentoDto?` (denormalized, joined in the same query).
- The `DROP COLUMN` of the legacy free-form `Personas.TipoDocumento` string column after the backfill completes.
- **Relaxation of condition #3 (opt-in variant):** because the FK is nullable, unknown legacy values map to `TipoDocumentoId = NULL` with `NumeroDocumento` preserved. The audit interceptor records the transition in `Auditorias` so the orphan value can be remediated post-deploy.

The change `migrar-campo-categoria-habilidades-a-tabla` is the fourth invocation of this exception. It introduces:

- The table `CategoriasHabilidad` (Id `char(36)` PK, `Codigo varchar(50)` `UNIQUE NOT NULL`, `Nombre varchar(100)` `NOT NULL`) seeded from the `72000000-…` GUID block.
- The column `Habilidades.CategoriaId` `char(36) NULL` with FK to `CategoriasHabilidad.Id` and `OnDelete(Restrict)`.
- The index `IX_Habilidades_CategoriaId`.
- The contract shape change on `HabilidadDto`: `Categoria: string?` is replaced by `CategoriaId: Guid?` (Domain) and `CategoriaId: Guid?` + `CategoriaNombre: string?` (wire).
- The `DROP COLUMN` of the legacy free-form `Habilidades.Categoria` `varchar(100)` after the backfill completes.
- **Relaxation of condition #3 (opt-in variant):** the FK is nullable (`char(36) NULL`); unknown legacy values (`Categoria` strings that do not match any seeded `CategoriasHabilidad.Nombre`) are persisted with `CategoriaId = NULL` for post-deploy remediation; the audit interceptor MUST record the transition `legacy string → NULL` in `Auditorias` so the orphan rows can be remediated after the deploy.

Any subsequent change that wants to invoke this exception MUST add a new delta to this spec, naming the change explicitly and confirming which variant of condition #3 applies (default fail-loud or opt-in relaxed).
(Previously: the exception listed only the first two invocations and condition #3 mandated fail-loud with no relaxation path.)

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

#### Scenario: Third invocation of the exception is approved with opt-in relaxed variant

- **GIVEN** the change `2026-07-20-147-tipos-documento-catalogo` (issue #147) is being applied
- **WHEN** the migration adds the `TiposDocumento` table, the `Personas.TipoDocumentoId` FK with `OnDelete(Restrict)`, the `IX_Personas_TipoDocumentoId` index, and the contract shape change from `Persona.TipoDocumento: string?` to `Persona.TipoDocumentoId: Guid?`
- **AND** the FK is declared nullable (`char(36) NULL`) on purpose
- **AND** the seed is loaded with 4 static Guids from `TipoDocumentoConstantes` (block `71000000-…`)
- **AND** the `ActiveDocumentoUnique` generated column is recreated with the new formula
- **AND** the free-form `Personas.TipoDocumento` string column is dropped after the backfill completes
- **THEN** conditions #1, #2 and #4 of REQ-SPA-EVOLUTION-001 are satisfied
- **AND** the change opts into the relaxed variant of condition #3 (nullable FK, orphan-tolerant)
- **AND** the change is an authorized exception to the `Observable Persistence Invariants` requirement.

#### Scenario: Fourth invocation of the exception is approved with opt-in relaxed variant

- **GIVEN** the change `migrar-campo-categoria-habilidades-a-tabla` is being applied
- **WHEN** the migration adds the `CategoriasHabilidad` table, the `Habilidades.CategoriaId` FK with `OnDelete(Restrict)`, the `IX_Habilidades_CategoriaId` index, and the wire shape change from `HabilidadDto.Categoria: string?` to `CategoriaId: Guid? + CategoriaNombre: string?`
- **AND** the FK is declared nullable (`char(36) NULL`) on purpose
- **AND** the seed is loaded with 4 static Guids from `CategoriaHabilidadConstantes` (block `72000000-…`, positions `…000`, `…001`, `…002`, `…003`)
- **AND** the free-form `Habilidades.Categoria` `varchar(100)` column is dropped after the backfill completes
- **THEN** conditions #1, #2 and #4 of REQ-SPA-EVOLUTION-001 are satisfied
- **AND** the change opts into the relaxed variant of condition #3 (nullable FK, orphan-tolerant)
- **AND** the change is an authorized exception to the `Observable Persistence Invariants` requirement.

#### Scenario: Future change invokes the exception correctly

- **GIVEN** a future change `<nombre>` wants to promote a free-form string column to a catalog FK
- **WHEN** the change is proposed
- **THEN** it MUST add a delta to this spec that:
  - Names the change explicitly.
  - Confirms conditions #1, #2 and #4 of REQ-SPA-EVOLUTION-001.
  - Declares whether condition #3 follows the default fail-loud variant or the opt-in relaxed variant.
  - If the relaxed variant is chosen, the FK MUST be declared nullable and the mitigation strategy MUST be documented.
  - Lists the new table, FK, and contract shape change introduced.
- **AND** until that delta is added, the change violates the `Observable Persistence Invariants` requirement and is rejected.

#### Scenario: Migration fail-loud for dirty data (default variant)

- **GIVEN** a catalog-evolution migration running under the default variant of condition #3
- **WHEN** any pre-existing free-form string value is not present in the new catalog's seed
- **THEN** the migration MUST abort the SQL batch with `SIGNAL SQLSTATE '45000'`
- **AND** the error message MUST list the offending values (up to 5 examples, comma-separated)
- **AND** the migration MUST NOT proceed to the `DROP COLUMN` step
- **AND** the legacy free-form column MUST remain in the database.

#### Scenario: Opt-in relaxed variant maps unknown legacy values to NULL with preserved identifier

- **GIVEN** a catalog-evolution migration running under the opt-in relaxed variant of condition #3 (e.g. `2026-07-20-147-tipos-documento-catalogo`)
- **AND** the FK column is declared nullable
- **WHEN** any pre-existing free-form string value is not present in the new catalog's seed
- **THEN** the migration MUST persist the row with the FK column set to `NULL`
- **AND** the original identifier (`NumeroDocumento`) MUST remain intact
- **AND** the migration MUST NOT abort the SQL batch
- **AND** the audit interceptor MUST record the transition (`legacy string → NULL`) in `Auditorias` so the orphan can be remediated post-deploy.

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

### Requirement: REQ-124-1 — Reconstitute factories tipadas

Las entidades de dominio que requieren reconstitución desde la capa de persistencia DEBEN exponer una factory estática interna `Reconstitute(...)` con setters tipados (sin reflexión). Los parámetros DEBEN incluir todos los campos persistibles en el orden canónico: `Id + auditoría + IsDeleted` → datos primarios → `IsActive` → propiedades de navegación.

#### Scenario: Las 6 entidades exponen `internal static Reconstitute(...)`

- **GIVEN** las entidades Cargo, Habilidad, Puesto, Persona, Ocupacion y UnidadOrganizativa son reconstituidas desde persistencia
- **WHEN** se invoca `ToDomain(TEntity)` en `PersistenceToDomainMapper`
- **THEN** cada una DEBE delegar a su factory `internal static Reconstitute(...)` con la signatura exacta definida en su diseño
- **AND** los setters DEBEN ser tipados (sin `PropertyInfo.SetValue` ni `BindingFlags.NonPublic`)

#### Scenario: `PersistenceToDomainMapper.ToDomain(TEntity)` delega al factory

- **GIVEN** el mapper recibe una entidad EF Core (`CargoEntity`, `HabilidadEntity`, etc.)
- **WHEN** se ejecuta `ToDomain(TEntity)`
- **THEN** el mapper DEBE invocar directamente `Entidad.Reconstitute(...)`
- **AND** NO DEBE pasar por `PropertyInfo.SetValue` ni `SetProperty<T>`

### Requirement: REQ-124-2 — IL Guards estructurales

Cada entidad con `Reconstitute` DEBE tener un test IL estructural que verifique que ningún código del mapper reintroduce `PropertyInfo.SetValue` ni el helper `SetProperty<T>`.

#### Scenario: 6 IL guards verifican la ausencia de reflexión

- **GIVEN** la implementación actual de `PersistenceToDomainMapper` usa factories tipados
- **WHEN** se ejecutan los 6 tests `ToDomain_*_NoLlamaSetPropertyReflectionHelper`
- **THEN** los 6 DEBEN pasar (Cargo, Habilidad, Puesto, Persona, Ocupacion — 5 nuevos — más UnidadOrganizativa existente)
- **AND** cada test DEBE inspeccionar `MethodBody.GetILAsByteArray()`, decodificar tokens `0x28`/`0x6F`, y fallar si resuelve `SetProperty` declarada en `PersistenceToDomainMapper`

#### Scenario: Build limpio sin `using System.Reflection`

- **GIVEN** el archivo `PersistenceToDomainMapper.cs` fue limpiado
- **WHEN** se ejecuta `grep -n "System.Reflection\|PropertyInfo\|SetProperty" src/SGV.Infraestructura/Persistencia/Mapeos/PersistenceToDomainMapper.cs`
- **THEN** DEBE devolver 0 hits
- **AND** `grep -rn "PropertyInfo\.SetValue" src/` DEBE devolver 0 hits

### Requirement: REQ-124-3 — Sin nuevas migraciones EF Core

El refactor de reconstitución NO DEBE introducir cambios en el schema de base de datos.

#### Scenario: Sin migraciones nuevas

- **GIVEN** el cambio solo modifica clases C# de dominio y el mapper
- **WHEN** se ejecuta `git status -- src/SGV.Infraestructura/Persistencia/Migraciones/`
- **THEN** DEBE estar limpio (sin archivos nuevos ni modificados)

### Requirement: REQ-SPA-EVOLUTION-004 — Cuarta invocación: `CategoriasHabilidad` (opt-in relajada)

El cambio `migrar-campo-categoria-habilidades-a-tabla` invoca el REQ-SPA-EVOLUTION-001 por cuarta vez aplicando la variante opt-in relajada. El sistema DEBE introducir la tabla `CategoriasHabilidad` (Id `char(36)` PK, `Codigo varchar(50)` `UNIQUE NOT NULL`, `Nombre varchar(100)` `NOT NULL`) con seed del bloque `72000000-…`. La columna `Habilidades.CategoriaId` `char(36) NULL` DEBE ser FK hacia `CategoriasHabilidad.Id` con `OnDelete(Restrict)` e indexada (`IX_Habilidades_CategoriaId`). El contrato wire de `HabilidadDto` DEBE reemplazar `Categoria: string?` por `CategoriaId: Guid?` (dominio) y `CategoriaId: Guid?` + `CategoriaNombre: string?` (wire). La columna string legacy `Habilidades.Categoria` `varchar(100)` DEBE eliminarse tras el backfill. Bajo la variante relajada, los valores legacy sin match en el seed DEBEN quedar con `CategoriaId = NULL` y el interceptor DEBE auditar la transición.

#### Scenario: Bloque GUID `72000000-…` reservado y registrado

- **GIVEN** la matriz "Mapa de bloques GUID reservados por catálogo" en `docs/decisiones-implementacion.md`
- **WHEN** el cambio es archivado
- **THEN** el bloque `72000000-0000-0000-0000-000000000000` … `72000000-0000-0000-0000-00000000000F` DEBE aparecer como reservado para `CategoriaHabilidad`
- **AND** las cuatro posiciones seed (`…000`, `…001`, `…002`, `…003`) DEBEN estar etiquetadas como `Conduccion`, `Tecnica`, `Dominio`, `Academica`.

#### Scenario: Sin desvío de GUIDs seed entre migración y `DatosSemilla`

- **GIVEN** la migración (`InsertData`) y `DatosSemilla.HasData` referencian las mismas constantes `CategoriaHabilidadConstantes`
- **WHEN** se ejecuta el test `DatosSemilla_CategoriaHabilidad_SeedIdsMatchConstantes`
- **THEN** todo `Id` del `InsertData` está presente en `DatosSemilla` (y viceversa)
- **AND** la cantidad de `Id` distintos en ambas fuentes es idéntica (4).

#### Scenario: El catálogo `CategoriasHabilidad` rechaza escritura HTTP

- **GIVEN** el `CategoriasHabilidadController` publicado
- **WHEN** un cliente invoca `POST`, `PUT`, `PATCH` o `DELETE` sobre `/api/v1/categorias-habilidad` o `/api/v1/categorias-habilidad/{id:guid}`
- **THEN** la API DEBE responder `405 Method Not Allowed` (o `404` cuando no existe acción)
- **AND** ninguna fila de `CategoriasHabilidad` DEBE insertarse, actualizarse ni eliminarse.
