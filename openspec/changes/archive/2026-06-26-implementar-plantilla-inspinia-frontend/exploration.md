## Exploration: Implement Inspinia template for SGV frontend

### Current State
SGV now includes `src/SGV.Web/SGV.Web.csproj` in `SGV.slnx` as the frontend project. The project targets `net10.0`, enables nullable reference types and implicit usings, and currently contains a Razor Pages application imported from Inspinia Starterkit rather than only an empty project file.

The active frontend baseline is the Inspinia Razor Pages Starterkit structure:
- `Pages/` contains page-focused Razor UI, auth samples, error pages, layout demos, shared layouts, and shared partials.
- `Pages/Shared/Partials/` contains template shell partials such as `_Topbar.cshtml`, `_Sidenav.cshtml`, `_PageTitle.cshtml`, `_HeadCss.cshtml`, and `_FooterScripts.cshtml`.
- `wwwroot/scss/`, `wwwroot/css/`, `wwwroot/js/`, and `wwwroot/plugins/` contain the theme assets and generated bundles.
- `package.json`, `bun.lock`, `gulpfile.js`, and `plugins.config.js` define the frontend asset pipeline.

The Inspinia docs describe the intended workflow: use Bun to install dependencies, use Gulp through `bun run dev` or `bun run build` to compile assets, keep custom SCSS separate from core `app.scss`, and use the Starterkit as a lightweight production-ready baseline. The full `InspinaTemplate/Inspinia/` project is the reference library for richer page examples and plugin integrations.

Existing SGV architecture remains Clean Architecture for backend concerns. `SGV.Web` should stay a Razor Pages frontend/composition layer and should not move domain or persistence logic out of `SGV.Dominio`, `SGV.Aplicacion`, `SGV.Infraestructura`, or `SGV.Api`.

### Affected Areas
- `src/SGV.Web/SGV.Web.csproj` — frontend project that future UI implementation must target.
- `src/SGV.Web/Program.cs` — Razor Pages startup; currently adds Razor Pages, authorization middleware, static assets, and Razor page endpoints.
- `src/SGV.Web/Pages/` — future SGV pages should be added here using Razor Pages conventions and Inspinia layout/components.
- `src/SGV.Web/Pages/Shared/Partials/_Sidenav.cshtml` — current navigation is still template/demo navigation and will need SGV module entries as frontend scope is implemented.
- `src/SGV.Web/Pages/Shared/Partials/_HeadCss.cshtml` and `_FooterScripts.cshtml` — shared CSS/JS bundle inclusion points.
- `src/SGV.Web/package.json`, `src/SGV.Web/gulpfile.js`, `src/SGV.Web/plugins.config.js` — asset dependency and bundle pipeline; plugin additions should be intentional and copied from full Inspinia only when needed.
- `InspinaTemplate/Docs/index.html` — source documentation for asset pipeline, layout attributes, customization, Starterkit use, and multi-language guidance.
- `InspinaTemplate/Starterkit/` — baseline reference matching the imported SGV.Web project.
- `InspinaTemplate/Inspinia/` — full example reference for forms, tables, DataTables, tree view, plugins, dashboards, and page layouts.
- `openspec/config.yaml` — enforces strict TDD and SGV project rules for later phases.
- `docs/decisiones-implementacion.md` — backend persistence/Identity/audit constraints remain relevant when frontend features touch data workflows.

### Approaches
1. **Keep Starterkit as the SGV baseline and copy examples selectively** — Treat `SGV.Web` as the frontend app, keep its Starterkit footprint, and copy/adapt full Inspinia examples only per feature.
   - Pros: Lowest risk, keeps dependencies smaller, aligns with the user's imported project, limits review size, and preserves a clear source of truth for future frontend work.
   - Cons: Each feature may require searching the full template for matching examples and adding assets incrementally.
   - Effort: Medium

2. **Import the full Inspinia project into SGV.Web** — Replace or expand SGV.Web with the full demo/reference project.
   - Pros: All examples and plugins available immediately.
   - Cons: Very large review, many demo pages unrelated to SGV, larger dependency surface, harder navigation cleanup, and high risk of template code becoming product code accidentally.
   - Effort: High

3. **Use Inspinia only as visual reference and rebuild layouts manually** — Keep SGV.Web minimal and manually recreate only needed visual patterns.
   - Pros: Maximum control and minimum template coupling.
   - Cons: Wastes the imported Starterkit value, duplicates theme decisions, and increases design drift from Inspinia.
   - Effort: High

### Recommendation
Use Approach 1: keep `src/SGV.Web` as the Starterkit-based Razor Pages frontend and adopt full Inspinia examples incrementally by feature. Future frontend specs should state that UI pages live in `src/SGV.Web`, use Starterkit shared layouts/partials as the baseline, and reference `InspinaTemplate/Inspinia/` for specific UI patterns such as forms, tables, DataTables, selectors, date pickers, dashboards, and tree views.

Recommended conventions for later phases:
- Use Razor Pages PageModels for page orchestration and keep them lean.
- Keep business rules in application/API layers; SGV.Web should call application-facing services or HTTP clients rather than implementing domain behavior in `.cshtml`.
- Prefer static/server-rendered Bootstrap markup for simple lists and forms; add JavaScript plugins only when the UX requires them.
- When adding a plugin from full Inspinia, update `package.json`, `plugins.config.js`, page sections, and relevant `wwwroot/js/pages/*` files as one coherent work unit.
- Preserve Inspinia's page section pattern: `@section Styles` for page-specific CSS and `@section Scripts` for page-specific JS.
- Replace demo navigation and copy progressively with SGV-specific modules instead of carrying unused demo entries.

### Risks
- `SGV.Web` currently contains many template demo pages and strings; shipping them unchanged would expose non-SGV UI and confuse users.
- The shared `_VerticalLayout.cshtml` references `_TopBar.cshtml` and `_SideNav.cshtml`, while existing files are `_Topbar.cshtml` and `_Sidenav.cshtml`; this casing mismatch is risky on case-sensitive environments if that layout is used.
- Full Inspinia examples often depend on extra packages/plugins not present in the Starterkit `package.json` or `plugins.config.js`.
- Several rich examples use jQuery-based plugins such as DataTables and jsTree; these should be isolated to pages that need them.
- Asset generation depends on Bun/Gulp, which is outside the current .NET-only validation path and should be included in frontend verification once SGV.Web changes assets.
- Strict TDD is enabled in `openspec/config.yaml`; later implementation should plan PageModel tests and/or integration tests for real frontend behavior.

### Ready for Proposal
Yes. The proposal should define SGV.Web as the frontend project, preserve the Starterkit baseline, use full Inspinia only as a reference/examples library, and keep this change focused on establishing the frontend adoption conventions before implementing SGV business screens.
