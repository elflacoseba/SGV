## Exploration: Update .NET projects to .NET 10 while keeping EF Core 9 for Pomelo compatibility

### Current State
The solution uses `SGV.slnx` with four .NET projects: `SGV.Dominio`, `SGV.Aplicacion`, `SGV.Infraestructura`, and `SGV.Tests`. All projects currently target `net9.0`. `global.json` pins SDK `9.0.100` with `rollForward: latestMajor`, but the local machine only has .NET 10 SDKs/runtimes installed (`10.0.203`, `10.0.300`; runtimes `10.0.7`, `10.0.8`). Current `dotnet test --no-restore --no-build` fails because the existing `net9.0` testhost requires `Microsoft.NETCore.App 9.0.0`, which is not installed.

`SGV.Infraestructura` is the only project with EF-related package references. It uses EF Core 9.0.0 packages and SQL Server provider packages today: `Microsoft.AspNetCore.Identity.EntityFrameworkCore`, `Microsoft.EntityFrameworkCore.SqlServer`, and `Microsoft.EntityFrameworkCore.Design`. Pomelo is not currently referenced. NuGet metadata for `Pomelo.EntityFrameworkCore.MySql 9.0.0` shows it depends on `Microsoft.EntityFrameworkCore.Relational >= 9.0.0 && < 9.0.999`, so upgrading EF packages to 10.x would break Pomelo 9 compatibility. Keeping EF packages on 9.x while targeting `net10.0` is the correct compatibility direction.

OpenSpec currently describes the stack as `.NET 9`, `Entity Framework Core 9.0`, and `SQL Server`, and the database spec has a requirement explicitly saying SQL Server and EF Core over .NET 9. A later spec/proposal phase should decide whether this change is only a runtime/TFM upgrade or also starts the provider migration toward Pomelo/MySQL.

### Affected Areas
- `global.json` — SDK pin is `9.0.100`; should move to a .NET 10 SDK version available to the team or remove pinning intentionally.
- `SGV.slnx` — includes all four projects that must remain build/test aligned.
- `src/SGV.Dominio/SGV.Dominio.csproj` — targets `net9.0`; no package implications.
- `src/SGV.Aplicacion/SGV.Aplicacion.csproj` — targets `net9.0`; references domain project.
- `src/SGV.Infraestructura/SGV.Infraestructura.csproj` — targets `net9.0`; owns EF/Identity/provider package versions that should remain 9.x for Pomelo compatibility.
- `tests/SGV.Tests/SGV.Tests.csproj` — targets `net9.0`; testhost currently cannot run without .NET 9 runtime. Retargeting to `net10.0` removes that local runtime blocker.
- `src/SGV.Infraestructura/Persistencia/SgvDbContextFactory.cs` — currently configures `UseSqlServer`, which is unrelated to TFM update but relevant if Pomelo/MySQL migration is in scope.
- `src/SGV.Infraestructura/Persistencia/Migraciones/` — existing migrations and model snapshot contain SQL Server-specific annotations; provider migration would need separate design/spec work.
- `openspec/config.yaml` and `openspec/specs/sgv-database/spec.md` — currently document .NET 9 and SQL Server assumptions that would become stale after this change.
- `docs/decisiones-implementacion.md` — documents the previous decision to target `net9.0` because only SDK 10 was locally installed.

### Approaches
1. **Minimal TFM/SDK upgrade, keep persistence provider unchanged** — Retarget all projects to `net10.0`, update `global.json` to a .NET 10 SDK, and keep EF/Identity/SQL Server packages on 9.x.
   - Pros: Smallest safe change; fixes the local .NET 9 runtime test blocker; preserves migrations and SQL Server behavior; maintains future Pomelo 9 compatibility by avoiding EF 10.
   - Cons: OpenSpec and docs must be updated to stop saying `.NET 9`; SQL Server provider remains, so Pomelo compatibility is only protected, not implemented.
   - Effort: Low

2. **TFM/SDK upgrade plus Pomelo provider preparation** — Retarget to `net10.0`, keep EF packages on 9.x, add `Pomelo.EntityFrameworkCore.MySql 9.0.0`, and start replacing SQL Server-specific provider configuration.
   - Pros: Moves closer to the stated Pomelo goal.
   - Cons: Larger behavioral change; existing SQL Server migrations/snapshots become provider-specific debt; database spec currently says SQL Server, so this needs explicit proposal/spec/design before implementation.
   - Effort: Medium/High

3. **Upgrade everything to .NET/EF 10** — Retarget to `net10.0` and move EF packages to 10.x.
   - Pros: Uses the latest EF Core line.
   - Cons: Conflicts with Pomelo 9.0.0 dependency range (`< 9.0.999`); not aligned with the user request.
   - Effort: Low technically, but not viable for the stated constraint

### Recommendation
Proceed with Approach 1 for this change: update all project TFMs and SDK configuration to .NET 10 while pinning all EF-related packages in `SGV.Infraestructura` to EF Core/Identity EF Core 9.x. Treat adding Pomelo and changing from SQL Server to MySQL as a separate SDD change unless the proposal explicitly expands this change's scope, because provider migration touches configuration, migrations, model annotations, test strategy, and OpenSpec requirements.

### Risks
- EF Core 9 can be referenced from `net10.0`, but package updates must avoid accidental `Microsoft.EntityFrameworkCore*` 10.x upgrades.
- Current OpenSpec source of truth says SQL Server and .NET 9; downstream phases must update specs/docs or intentionally scope them out.
- Existing SQL Server migrations and `UseSqlServer` remain incompatible with a real Pomelo/MySQL provider switch.
- Test packages are older (`Microsoft.NET.Test.Sdk 17.12.0`, xUnit 2.9.2); they may run under .NET 10, but verification should confirm and only upgrade test packages if necessary.
- `global.json` must choose an SDK version available in CI/developer machines; pinning to `10.0.300` matches this machine but may not match other environments.

### Ready for Proposal
Yes — the proposal should scope this as a .NET 10 TFM/SDK upgrade with EF Core packages intentionally kept on 9.x for Pomelo compatibility. It should explicitly mark full Pomelo/MySQL provider migration as a non-goal unless the user confirms that migration should be included now.
