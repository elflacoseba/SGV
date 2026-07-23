# Delta for SGV Persistence Architecture

## ADDED Requirements

> Delta introducida por el change `migrar-campo-categoria-habilidades-a-tabla`. Es la **cuarta invocación** de `REQ-SPA-EVOLUTION-001`. La FK es **nullable** (`char(36) NULL`) y la variante aplicada es la **opt-in relajada** (orphan-tolerant). Verifica la matriz de bloques GUID registrados en `docs/decisiones-implementacion.md` antes de mergear.

### Requirement: Cuarta invocación de REQ-SPA-EVOLUTION-001 — `CategoriasHabilidad` (opt-in relajada)

The change `migrar-campo-categoria-habilidades-a-tabla` introduces:
- The table `CategoriasHabilidad` (Id `char(36)` PK, `Codigo varchar(50)` `UNIQUE NOT NULL`, `Nombre varchar(100)` `NOT NULL`) seeded from the `72000000-…` GUID block.
- The column `Habilidades.CategoriaId` `char(36) NULL` with FK to `CategoriasHabilidad.Id` and `OnDelete(Restrict)`.
- The index `IX_Habilidades_CategoriaId`.
- The contract shape change on `HabilidadDto`: `Categoria: string?` is replaced by `CategoriaId: Guid?` (Domain) and `CategoriaId: Guid?` + `CategoriaNombre: string?` (wire).
- The `DROP COLUMN` of the legacy free-form `Habilidades.Categoria` `varchar(100)` after the backfill completes.
- **Relaxation of condition #3 (opt-in variant):** the FK is nullable (`char(36) NULL`); unknown legacy values (`Categoria` strings that do not match any seeded `CategoriasHabilidad.Nombre`) are persisted with `CategoriaId = NULL` for post-deploy remediation; the audit interceptor MUST record the transition `legacy string → NULL` in `Auditorias` so the orphan rows can be remediated after the deploy.

Conditions #1 (read-only immutable catalog, no `IsActive`/`IsDeleted`, seeded only by an EF migration), #2 (`OnDelete(Restrict)` FK), and #4 (static shared `Guid` constants in `CategoriaHabilidadConstantes`) are fully satisfied. All endpoints over `CategoriasHabilidad` MUST be read-only (`GET` list + `GET` by id) and MUST require authentication.

#### Scenario: Fourth invocation is approved with opt-in relaxed variant

- **GIVEN** the change `migrar-campo-categoria-habilidades-a-tabla` is being applied
- **WHEN** the migration adds the `CategoriasHabilidad` table, the `Habilidades.CategoriaId` FK with `OnDelete(Restrict)`, the `IX_Habilidades_CategoriaId` index, and the wire shape change from `HabilidadDto.Categoria: string?` to `CategoriaId: Guid? + CategoriaNombre: string?`
- **AND** the FK is declared nullable (`char(36) NULL`) on purpose
- **AND** the seed is loaded with 4 static Guids from `CategoriaHabilidadConstantes` (block `72000000-…`, positions `…000`, `…001`, `…002`, `…003`)
- **AND** the free-form `Habilidades.Categoria` `varchar(100)` column is dropped after the backfill completes
- **THEN** conditions #1, #2 and #4 of REQ-SPA-EVOLUTION-001 are satisfied
- **AND** the change opts into the relaxed variant of condition #3 (nullable FK, orphan-tolerant)
- **AND** the change is an authorized exception to the `Observable Persistence Invariants` requirement.

#### Scenario: Block GUID `72000000-…` reserved and registered

- **GIVEN** the matrix "Mapa de bloques GUID reservados por catálogo" in `docs/decisiones-implementacion.md`
- **WHEN** the change is archived
- **THEN** the block `72000000-0000-0000-0000-000000000000` … `72000000-0000-0000-0000-00000000000F` MUST appear as reserved for `CategoriaHabilidad`
- **AND** the four seed positions (`…000`, `…001`, `…002`, `…003`) MUST be labeled as `Conduccion`, `Tecnica`, `Dominio`, `Academica`.

#### Scenario: Seed Guid drift is impossible for `CategoriasHabilidad`

- **GIVEN** the migration's `InsertData` and `DatosSemilla.HasData` both reference the same `CategoriaHabilidadConstantes`
- **WHEN** the unit test `DatosSemilla_CategoriaHabilidad_SeedIdsMatchConstantes` runs
- **THEN** every `Id` declared in the migration's `InsertData` is present in `DatosSemilla` (and vice versa)
- **AND** the count of distinct `Id`s in both lists is identical (4).

#### Scenario: Catálogo `CategoriasHabilidad` rechaza escritura HTTP

- **GIVEN** `CategoriasHabilidadController` published
- **WHEN** any client invokes `POST`, `PUT`, `PATCH` or `DELETE` over `/api/v1/categorias-habilidad` or `/api/v1/categorias-habilidad/{id:guid}`
- **THEN** the API MUST respond `405 Method Not Allowed` (or `404` when no matching action exists)
- **AND** no row in `CategoriasHabilidad` MUST be inserted, updated or deleted.
