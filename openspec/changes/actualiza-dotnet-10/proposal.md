# Proposal: Update to .NET 10 and Replace SQL Server with MySQL/Pomelo

## Intent

Move SGV to .NET 10 and switch persistence from SQL Server to MySQL/Pomelo. EF Core-related packages must remain on 9.x because Pomelo 9 requires EF Core relational packages `< 9.0.999`.

## Scope

### In Scope
- Retarget all .NET projects to `net10.0` and align SDK configuration.
- Replace SQL Server provider, configuration, migrations, docs, and specs with MySQL/Pomelo.
- Keep `Microsoft.EntityFrameworkCore*`, Identity EF Core, and Pomelo packages on 9.x, not 10.x.

### Out of Scope
- Upgrading EF Core packages to 10.x.
- Redesigning the SGV domain model or changing business behavior.
- Production hosting, backup automation, or cloud provisioning.

## Non-goals

- Do not preserve SQL Server as an active supported provider.
- Do not rewrite archived OpenSpec audit artifacts unless a later phase requires notes.

## Capabilities

### New Capabilities
- None.

### Modified Capabilities
- `sgv-database`: update compatibility from SQL Server/.NET 9 to MySQL/Pomelo on .NET 10 while preserving EF Core 9.x.

## Approach

Retarget projects and SDK settings, replace `Microsoft.EntityFrameworkCore.SqlServer` and `UseSqlServer`, then regenerate or replace provider-specific migrations/snapshot. Update active OpenSpec config/specs and docs to state .NET 10, MySQL/Pomelo, and EF Core 9.x.

## Affected Areas

| Area | Impact | Description |
|------|--------|-------------|
| `global.json`, `*.csproj` | Modified | .NET 10 TFM/SDK alignment; EF packages stay 9.x. |
| `src/SGV.Infraestructura/` | Modified | Pomelo package, DbContext provider, migrations. |
| `tests/SGV.Tests/` | Modified | Retargeting and persistence verification updates. |
| `openspec/config.yaml`, `openspec/specs/sgv-database/spec.md` | Modified | Replace stale .NET 9/SQL Server assumptions. |
| `docs/`, `AGENTS.md` | Modified | Replace current SQL Server guidance. |

## Risks

| Risk | Likelihood | Mitigation |
|------|------------|------------|
| EF 10 installed accidentally | Medium | Pin packages to 9.x and verify dependencies. |
| Provider migration changes schema | High | Review migrations/snapshot and run MySQL-compatible tests. |
| SQL Server references remain | Medium | Search repository for SQL Server/provider terms before verification. |

## Rollback Plan

Revert the change commit(s): restore `net9.0`, SQL Server package/provider configuration, previous migrations/snapshot, and prior OpenSpec/docs references. If MySQL migration was applied, restore from the pre-migration backup or recreate from the previous SQL Server baseline.

## Dependencies

- .NET 10 SDK available in development and CI.
- `Pomelo.EntityFrameworkCore.MySql` 9.x and compatible EF Core 9.x packages.
- MySQL verification target or test connection strategy.

## Success Criteria

- [ ] All projects target `net10.0`; EF Core-related packages remain on 9.x.
- [ ] SQL Server provider/configuration/migration assumptions are replaced with Pomelo/MySQL.
- [ ] `sgv-database` spec reflects MySQL/Pomelo on .NET 10 with EF Core 9.x.
- [ ] Build and test verification pass for the updated solution.
