# Design: Update to .NET 10 and Replace SQL Server with MySQL/Pomelo

## Technical Approach

Retarget the four SGV projects (`SGV.Dominio`, `SGV.Aplicacion`, `SGV.Infraestructura`, `SGV.Tests`) to `net10.0`, align `global.json` to a .NET 10 SDK, and keep all EF Core-related packages on compatible `9.x` versions. Replace SQL Server as the active persistence provider with Pomelo/MySQL in infrastructure, migrations, generated SQL documentation, OpenSpec, and repository guidance. Business/domain behavior stays unchanged.

Evidence: only `SGV.Infraestructura` owns EF packages/provider configuration; `SgvDbContextFactory` uses `UseSqlServer`; migrations/snapshot and `docs/migracion-inicial-sgv.sql` are SQL Server-specific; several EF configurations use SQL Server filtered-index/check SQL syntax.

## Architecture Decisions

| Decision | Alternatives considered | Rationale |
|---|---|---|
| Target `net10.0` for every project and pin SDK in `global.json` to a team-approved .NET 10 SDK. | Keep `net9.0`; remove `global.json`. | Scope requires .NET 10 everywhere; pinning avoids accidental SDK drift while keeping restore/build reproducible. |
| Keep `Microsoft.EntityFrameworkCore*`, `Microsoft.AspNetCore.Identity.EntityFrameworkCore`, and `Pomelo.EntityFrameworkCore.MySql` on `9.x`. | Upgrade EF packages to 10.x. | Pomelo 9 depends on EF relational `>= 9.0.0 && < 9.0.999`; EF 10 would break provider compatibility. |
| Use Pomelo as the only active provider via `UseMySql` and a configured MySQL 8 server version. | Keep SQL Server side-by-side; use provider auto-detection everywhere. | Proposal rejects SQL Server support. A fixed server version keeps design-time migrations deterministic. |
| Replace current SQL Server migrations/snapshot with a fresh MySQL/Pomelo migration baseline. | Edit SQL Server migrations in place; add a cross-provider data migration. | Existing artifacts are provider-specific and the repo appears pre-production. A clean provider baseline is safer than hand-translating SQL Server annotations. If production data exists, export/import is an operational task outside this change. |
| Adapt filtered unique constraints to MySQL-compatible modeling. | Preserve `.HasFilter(...)` SQL Server predicates. | MySQL does not support SQL Server filtered indexes. Preserve business rules through generated-column indexes or provider-specific equivalent patterns validated against MySQL. |

## Data Flow

```text
Tests / app host
  -> configuration: ConnectionStrings:Default + Database:ServerVersion
  -> SgvDbContextFactory / DI registration
  -> Pomelo UseMySql
  -> MySQL schema generated from EF Core 9 model and Pomelo migrations
```

## File Changes

| File | Action | Description |
|---|---|---|
| `global.json` | Modify | Pin/select .NET 10 SDK; keep intentional roll-forward policy. |
| `src/*/*.csproj`, `tests/SGV.Tests/SGV.Tests.csproj` | Modify | Change TFMs to `net10.0`; keep test packages unless verification proves an update is required. |
| `src/SGV.Infraestructura/SGV.Infraestructura.csproj` | Modify | Remove `Microsoft.EntityFrameworkCore.SqlServer`; add `Pomelo.EntityFrameworkCore.MySql` 9.x; keep EF/Identity EF 9.x. |
| `src/SGV.Infraestructura/Persistencia/SgvDbContextFactory.cs` | Modify | Replace `UseSqlServer` localdb string with `UseMySql` using a MySQL connection string and fixed MySQL 8 server version. |
| `src/SGV.Infraestructura/Persistencia/Configuraciones/*.cs` | Modify | Replace SQL Server filter/check SQL fragments with MySQL-compatible expressions; preserve existing domain constraints. |
| `src/SGV.Infraestructura/Persistencia/Migraciones/*` | Replace | Remove SQL Server-specific migrations/snapshot and generate Pomelo/MySQL artifacts. |
| `docs/migracion-inicial-sgv.sql`, `docs/decisiones-implementacion.md`, `AGENTS.md` | Modify | Replace SQL Server/.NET 9 guidance with MySQL/Pomelo/.NET 10 guidance. |
| `openspec/config.yaml`, `openspec/specs/sgv-database/spec.md` | Modify | Update source-of-truth stack and provider requirement. |
| `tests/SGV.Tests/Persistencia/ModeloPersistenciaTests.cs` | Modify | Assert provider-compatible constraints and migration model behavior, not SQL Server syntax. |

## Interfaces / Contracts

No public domain contracts change. Persistence configuration contract becomes:

- `ConnectionStrings:Default`: MySQL connection string.
- `Database:ServerVersion`: MySQL version used by Pomelo for SQL generation, defaulting to MySQL 8.0 in development.

## Testing Strategy

| Layer | What to Test | Approach |
|---|---|---|
| Build/restore | `net10.0` and package graph | `dotnet restore`, `dotnet build`; inspect resolved EF/Pomelo versions remain 9.x. |
| Unit | Domain/application behavior | Existing xUnit tests unchanged except TFM. |
| Persistence model | MySQL-compatible model, indexes, constraints, migrations | Update model tests; add migration/script generation verification against Pomelo. |
| Integration | Real provider behavior | Prefer ephemeral MySQL 8 container/local instance for `dotnet test`; skip or mark explicitly when MySQL is unavailable. |

## Migration / Rollout

1. Retarget TFMs and package references.
2. Replace provider configuration and MySQL-specific constraint/index modeling.
3. Regenerate Pomelo migrations/snapshot and SQL script from the EF model.
4. Update OpenSpec/docs and run repository search for SQL Server references.
5. Verify restore/build/tests and, when available, apply migrations to a clean MySQL database.

Rollback: revert the implementation commits to restore `net9.0`, SQL Server packages/provider, previous migrations/snapshot, docs, and specs. If MySQL schema was applied, drop/recreate the MySQL database from backup or discard the clean test database.

## Risks

- MySQL filtered-index gap can weaken uniqueness rules if modeled casually; tests must prove equivalent behavior.
- Pomelo/EF version drift to 10.x will break compatibility; package verification is mandatory.
- Exact MySQL server version and CI database availability are not defined yet; design assumes MySQL 8.x and local/ephemeral verification.

## Open Questions

- [ ] Which exact MySQL 8.x version should CI and local development standardize on?
