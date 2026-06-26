# Proposal: Implement Inspinia frontend base layout

## Intent

Make `src/SGV.Web` the functional Razor Pages frontend shell for SGV using the Inspinia Starterkit baseline. The current imported template contains demo pages/navigation that can expose non-SGV content; future modules need a clean, stable layout and asset convention.

## Scope

### In Scope
- Establish the base layout in `SGV.Web` from `InspinaTemplate/Starterkit/`.
- Remove demo content and demo navigation.
- Keep only minimal technical navigation; do not add placeholder business modules.
- Use neutral `SGV` branding, keep Inspinia colors, and preserve layout controls/customizer.
- Treat `InspinaTemplate/Inspinia/` and `InspinaTemplate/Docs/index.html` as references for later module features.

### Non-goals / Out of Scope
- Authentication or authorization UI/policies.
- Business modules, dashboards, CRUD screens, fake data, or placeholder SGV pages.
- Importing the full Inspinia demo application.
- Backend domain, persistence, API, Identity, audit, or MySQL changes.

## Capabilities

### New Capabilities
- `sgv-web-shell`: Base Razor Pages frontend shell, layout, navigation, branding, and asset validation expectations for SGV.Web.

### Modified Capabilities
- None.

## Approach

Use the Starterkit already present in `SGV.Web` as the product baseline. Clean the shell down to SGV-neutral layout, minimal technical navigation, and no demo content. Add richer Inspinia examples/plugins only in future module-specific changes.

## Affected Areas

| Area | Impact | Description |
|------|--------|-------------|
| `src/SGV.Web/Pages/` | Modified | Remove/replace demo pages with a minimal shell. |
| `src/SGV.Web/Pages/Shared/Partials/` | Modified | Branding, navigation, layout controls, CSS/JS includes. |
| `src/SGV.Web/wwwroot/` | Modified | Keep required Inspinia assets; avoid unused demo assets where safe. |
| `src/SGV.Web/package.json`, `gulpfile.js`, `plugins.config.js` | Modified if assets change | Preserve Bun/Gulp pipeline; add plugins only intentionally. |

## Risks

| Risk | Likelihood | Mitigation |
|------|------------|------------|
| Demo UI leaks into product shell | High | Explicit removal criterion in specs/tasks. |
| `_TopBar`/`_SideNav` casing mismatch on case-sensitive systems | Medium | Verify shared layout partial references. |
| Asset pipeline omitted from validation | Medium | Require Bun/Gulp validation when assets are touched. |
| Over-importing full Inspinia | Medium | Use full template as reference only. |

## Rollback Plan

Revert `SGV.Web` layout/page/asset changes for this change. No database or backend migration rollback is expected.

## Dependencies

- .NET SDK `10.0.300` via `global.json`.
- Bun/Gulp asset workflow from `SGV.Web` when implementation touches frontend assets.

## Success Criteria

- [ ] SGV.Web shows a functional Inspinia-based SGV shell with no demo content.
- [ ] Navigation is minimal and technical only.
- [ ] No authentication or business module placeholders are introduced.
- [ ] Later implementation validates `dotnet build SGV.slnx`; run `dotnet test SGV.slnx` for behavior changes.
- [ ] If assets are touched later, validate with Bun/Gulp (`bun install` as needed, then `bun run build` or equivalent project script).

## Proposal Question Round

Assumption for user review: the first slice is a neutral shell only; module-specific UX, menus, permissions, and content will be proposed separately.
