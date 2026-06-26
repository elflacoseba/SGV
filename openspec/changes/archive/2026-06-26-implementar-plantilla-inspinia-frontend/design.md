# Design: Implement Inspinia frontend base layout

## Technical Approach

Make `src/SGV.Web` a neutral Razor Pages shell based on the existing Inspinia Starterkit import, not the full demo app. The implementation should centralize chrome in shared layout/partials, remove product-facing demo/auth/business content, preserve the Inspinia visual system and customizer, and leave future SGV modules to add pages incrementally.

The shell remains a frontend/composition project only. No `SGV.Dominio`, `SGV.Aplicacion`, `SGV.Infraestructura`, `SGV.Api`, database, Identity, or MySQL behavior changes are part of this change.

## Architecture Decisions

| Decision | Choice | Alternatives considered | Rationale |
|---|---|---|---|
| Layout baseline | Use a shared vertical layout as the default shell (`_VerticalLayout.cshtml` or equivalent `_BaseLayout.cshtml` composition). | Keep each page manually rendering wrapper/topbar/sidenav/footer; import full Inspinia app. | A shared layout removes duplication, prevents demo chrome from leaking page-by-page, and keeps the Starterkit as the stable baseline. |
| Demo removal | Delete or disconnect demo/auth/layout/icon pages from shell navigation; keep only shell-required pages and error handling that remains product-neutral. | Hide demo links only; leave auth pages reachable. | Specs require no demo navigation/content and no auth UI. Unreachable demo files are still review risk, so remove when safe or explicitly keep only non-product references outside `SGV.Web`. |
| Navigation | Minimal technical menu: Home/Shell only, plus layout/customizer controls where they are part of Inspinia chrome. | Add vacancy/recruitment/catalog placeholders. | User decision says no placeholder business modules; real modules will define their own entries later. |
| Branding | Replace Inspinia product-facing title/meta/logo text with neutral `SGV`; keep theme colors/assets unless they are demo-specific. | Re-theme colors now. | Preserves requested Inspinia colors and limits first delivery scope. |
| Partial casing | Standardize references and filenames to existing casing: `_Topbar.cshtml` and `_Sidenav.cshtml`. | Rename files to `_TopBar`/`_SideNav`. | Current files and docs use `_Topbar`/`_Sidenav`; `_VerticalLayout.cshtml` and `_HorizontalLayout.cshtml` currently reference `_TopBar`/`_SideNav`, which can fail on case-sensitive systems. |
| Auth seam | Keep `Program.cs` public and no auth UI/policies. Future auth can add `[Authorize]`, Identity/OIDC services, and account partials in a separate change. | Add disabled login/profile placeholders. | Avoids false auth behavior while leaving the Razor Pages extension seam intact. |
| Assets | Keep Bun/Gulp pipeline from Starterkit. Do not add packages/plugins from `InspinaTemplate/Inspinia/` unless a future module needs them. | Copy all full-template plugins. | Limits dependency surface and review size; docs recommend Starterkit as the focused base and Bun/Gulp for builds. |

## Data Flow

```text
Request / ──> Razor PageModel ──> Shared layout
                              ├── _TitleMeta / _HeadCss
                              ├── _Topbar / _Sidenav / _Footer
                              ├── page content: neutral shell status
                              └── _Customizer / _FooterScripts

Static assets ──> wwwroot css/js/plugins/images ──> browser
```

No backend API, domain, persistence, or authentication flow is introduced.

## File Changes

| File | Action | Description |
|---|---|---|
| `src/SGV.Web/Pages/_ViewStart.cshtml` | Modify | Point default pages to the chosen shared shell layout. |
| `src/SGV.Web/Pages/Shared/_BaseLayout.cshtml` | Modify | If retained as default, compose common head/body sections consistently. |
| `src/SGV.Web/Pages/Shared/_VerticalLayout.cshtml` | Modify | Fix partial casing and make it the canonical wrapper if selected. |
| `src/SGV.Web/Pages/Shared/_HorizontalLayout.cshtml` | Modify/Delete | Keep only if customizer requires it; otherwise remove from product shell. |
| `src/SGV.Web/Pages/Shared/Partials/_Topbar.cshtml` | Modify | Remove megamenus, apps, messages, notifications, user/account UI; keep menu toggle, theme/customizer controls, SGV brand. |
| `src/SGV.Web/Pages/Shared/Partials/_Sidenav.cshtml` | Modify | Replace demo/auth/layout/component menu with minimal technical navigation. |
| `src/SGV.Web/Pages/Shared/Partials/_TitleMeta.cshtml` | Modify | Replace Inspinia title/meta/author with neutral SGV metadata. |
| `src/SGV.Web/Pages/Shared/Partials/_PageTitle.cshtml` | Modify | Remove Inspinia breadcrumb brand; use SGV-neutral breadcrumbs if needed. |
| `src/SGV.Web/Pages/Shared/Partials/_Customizer.cshtml` | Modify | Preserve layout controls; remove commercial/demo-only copy such as “Buy Now”. |
| `src/SGV.Web/Pages/Index.cshtml` | Replace | Remove dashboard/demo charts and render a minimal SGV shell landing page with no module placeholders. |
| `src/SGV.Web/Pages/Auth*`, `Pages/Layouts`, `Pages/Icons`, `Pages/Pages/Empty.cshtml` | Delete or disconnect | Remove demo/auth/sample pages from product-facing shell. |
| `src/SGV.Web/wwwroot/**`, `package.json`, `gulpfile.js`, `plugins.config.js` | Modify only if needed | Keep required shell assets; avoid new plugins. Validate Bun/Gulp when changed. |

## Interfaces / Contracts

No new C# interfaces or API contracts. Page contract remains public GET `/` rendering without authentication. Future module pages should use Razor Pages, lean PageModels, tag helpers, `@section Styles`, and `@section Scripts` for page-specific assets.

## Testing Strategy

| Layer | What to Test | Approach |
|---|---|---|
| Unit | PageModels stay lean and do not require module data. | Add only if PageModel behavior appears beyond simple `OnGet`. |
| Integration | `/` returns success, contains `SGV`, omits demo/auth/menu terms, and references required assets. | Use ASP.NET Core test host if available; otherwise validate through later web smoke test. |
| Build/assets | .NET solution and frontend assets compile. | `dotnet build SGV.slnx`; if assets/config touched: from `src/SGV.Web`, `bun install` if needed, then `bun run build`. |

## Migration / Rollout

No migration required. Rollback is limited to `src/SGV.Web` layout/page/asset changes. Chained PR risk is moderate if deleting many template files; split cleanup from shell composition if review budget is exceeded.

## Open Questions

- [ ] None blocking.
