# Tasks: Implement Inspinia frontend base layout

## Review Workload Forecast

| Field | Value |
|---|---|
| Estimated changed lines | 900-1400 |
| 800-line budget risk | High |
| 400-line budget risk | High |
| Chained PRs recommended | Yes |
| Suggested split | PR 1 shell composition + shell smoke test; PR 2 demo page/file cleanup + asset cleanup |
| Delivery strategy | size:exception |
| Chain strategy | N/A (single PR with exception) |

Decision needed before apply: Yes
Chained PRs recommended: Yes
Chain strategy: pending
400-line budget risk: High

### Suggested Work Units

| Unit | Goal | Likely PR | Notes |
|---|---|---|---|
| 1 | Deliver the neutral SGV shell and make `/` pass smoke validation | PR 1 | Keep deletions limited to files directly blocking the shell |
| 2 | Remove remaining demo/auth/layout/icon pages and trim demo-only assets | PR 2 | Base on PR 1; safest slice for large cleanup |

## Phase 1: Baseline and RED

- [x] 1.1 Add/extend a web smoke test in `tests/SGV.Tests` for `GET /` asserting success, visible `SGV`, no login/account/demo menu entries, and shared asset references.
- [x] 1.2 Run `dotnet test SGV.slnx --filter SGV.Web` or the narrowest new test target and confirm it fails for current demo shell.

## Phase 2: Shell composition GREEN

- [x] 2.1 Update `src/SGV.Web/Pages/_ViewStart.cshtml`, `Pages/Shared/_BaseLayout.cshtml`, and `Pages/Shared/_VerticalLayout.cshtml` so the default shell uses one canonical shared layout with `_Topbar` and `_Sidenav` casing fixed.
- [x] 2.2 Replace product-facing demo branding in `_TitleMeta.cshtml`, `_PageTitle.cshtml`, `_Topbar.cshtml`, `_Footer.cshtml`, and `_Customizer.cshtml` with neutral SGV copy while preserving Inspinia colors and layout controls.
- [x] 2.3 Replace `src/SGV.Web/Pages/Index.cshtml` with a minimal shell landing page and keep `Index.cshtml.cs` lean with no module-specific data dependency.
- [x] 2.4 Reduce `src/SGV.Web/Pages/Shared/Partials/_Sidenav.cshtml` to technical shell navigation only; keep no auth, business placeholders, or demo links.

## Phase 3: Demo cleanup and REFACTOR

- [x] 3.1 Delete or disconnect demo Razor pages under `src/SGV.Web/Pages/Auth*`, `Pages/Layouts`, `Pages/Icons`, and `Pages/Pages/Empty.*`; keep only product-neutral error handling that still serves the shell.
- [x] 3.2 Remove or stop referencing demo-only frontend artifacts such as `wwwroot/js/pages/dashboard-projects.js`, `auth-password.js`, and `auth-two-factor.js`; update `package.json`, `plugins.config.js`, or styles only if shell assets actually require it.
- [x] 3.3 Verify `Program.cs` keeps public Razor Pages access with no auth redirect or account chrome introduced.

## Phase 4: Validation

- [x] 4.1 Run `dotnet build SGV.slnx`.
- [x] 4.2 Run `dotnet test SGV.slnx`.
- [x] 4.3 If frontend assets changed, run `bun install` (if needed) and `bun run build` from `src/SGV.Web`.
