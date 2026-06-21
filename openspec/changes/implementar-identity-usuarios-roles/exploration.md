## Exploration: Implementar Identity para el manejo de Usuarios y asignación de Roles

### Current State

The solution already has partial ASP.NET Core Identity persistence in place. `SgvDbContext` inherits from `IdentityDbContext<IdentityUser>`, `Microsoft.AspNetCore.Identity.EntityFrameworkCore` is referenced from Infrastructure, the initial migration already creates the standard `AspNet*` tables, and `DatosSemilla` seeds five `IdentityRole` rows: `Administrador`, `RecursosHumanos`, `GestorOrganizacional`, `EvaluadorSeleccion`, and `Lector`.

Identity is not yet activated at the API/application boundary. `Program.cs` registers only controllers, Swagger, `SgvDbContext`, an anonymous `IUsuarioActual`, application services, and infrastructure services. There is no `AddIdentityCore`/`AddIdentity`, no `AddRoles`, no `AddEntityFrameworkStores`, no authentication scheme, no `UseAuthentication`, no `UseAuthorization`, no user/role management service, and no endpoints for creating users or assigning roles. Current API tests explicitly assert that controllers and Swagger do not require authorization.

Clean Architecture is currently enforced by keeping SGV domain entities EF-agnostic and mapped to Infrastructure `*Entity` types, while framework-owned Identity internals are an explicit exception. Therefore, Identity should remain an Infrastructure/API security concern and should not be modeled as a Domain aggregate unless the business introduces user-specific domain rules beyond authentication/authorization. Application should own use-case contracts and orchestration interfaces; Infrastructure should adapt ASP.NET Core Identity managers.

Personas are already managed as administrative SGV records under `/api/v1/personas`, including skills subresources. There is no current relationship between `Persona` and `IdentityUser`, so the proposal must decide whether users are standalone accounts or optionally linked to Personas.

### Affected Areas

- `src/SGV.Api/Program.cs` — must register Identity services, authentication/authorization middleware, Swagger security metadata if protected endpoints are introduced, and replace the anonymous user adapter for authenticated requests.
- `src/SGV.Api/Seguridad/UsuarioActualAnonimo.cs` — current audit user always returns `null`; the Identity change likely needs an authenticated adapter reading `ClaimsPrincipal` while preserving fallback behavior for anonymous/public endpoints.
- `src/SGV.Aplicacion/Seguridad/IUsuarioActual.cs` — already provides audit-facing `UserId`; may remain stable, but it becomes observable once requests are authenticated.
- `src/SGV.Infraestructura/Persistencia/SgvDbContext.cs` — already inherits from `IdentityDbContext<IdentityUser>`; may need custom Identity user type only if a Persona link or additional account fields are required.
- `src/SGV.Infraestructura/Persistencia/DatosSemilla.cs` — already seeds roles with role name as `Id`; any role changes must preserve deterministic IDs or explicitly migrate existing `AspNetRoles`/`AspNetUserRoles` data.
- `src/SGV.Infraestructura/Persistencia/Migraciones/*` — current migrations already include `AspNetUsers`, `AspNetRoles`, role claims, user claims, logins, tokens, and user-role join tables. New schema migrations are needed only for custom user fields, Persona linkage, or changed role seed strategy.
- `src/SGV.Infraestructura/DependencyInjection.cs` — likely place for Infrastructure adapters around `UserManager<IdentityUser>` and `RoleManager<IdentityRole>` if Application exposes user/role use cases through interfaces.
- `src/SGV.Api/Controllers/*` — currently no `[Authorize]`; management endpoints may need new controllers and/or policies without breaking existing public-read contracts unless specs are modified.
- `tests/SGV.Tests/Api/*` — several tests assert controllers do not require authorization and Swagger contains no security definitions; these must be updated only if the new spec intentionally changes that behavior.
- `tests/SGV.Tests/Persistencia/ModeloPersistenciaTests.cs` — already asserts Identity types remain framework-owned and app entities use Guid keys except Identity; custom Identity modeling would require careful updates.
- `openspec/specs/sgv-readonly-api/spec.md` — currently states read-only endpoints MUST remain anonymously accessible; any authorization change must preserve public reads or explicitly modify this requirement.
- `openspec/specs/sgv-persistence-architecture/spec.md` — allows framework-owned Identity internals to keep provider-owned types; custom Identity persistence must not weaken the Domain/Infrastructure boundary.
- `openspec/specs/sgv-database/spec.md` — requires MySQL/Pomelo, EF Core 9.x packages, and audit records with user IDs; Identity user IDs are string and audit columns already use `varchar(450)`.
- `docs/decisiones-implementacion.md` — documents the existing decision to keep `IdentityUser` with string keys and avoid premature customization.

### Approaches

1. **Activate built-in Identity with framework `IdentityUser` and role management services** — Register `AddIdentityCore<IdentityUser>().AddRoles<IdentityRole>().AddEntityFrameworkStores<SgvDbContext>()`, add authentication/authorization middleware, add application-facing user/role services, and expose explicit management endpoints.
   - Pros: smallest schema impact; matches existing migrations, docs, and persistence tests; preserves the documented `IdentityUser` string-key decision; uses standard `UserManager`/`RoleManager` APIs.
   - Cons: no direct Persona linkage unless added separately; API authentication scheme and token strategy still need an explicit product decision.
   - Effort: Medium

2. **Introduce a custom `ApplicationUser : IdentityUser` linked to `Persona`** — Replace `IdentityDbContext<IdentityUser>` with a custom user type and add optional/required `PersonaId` mapping.
   - Pros: supports business workflows that need an account-to-person relationship; enables querying user profile/persona data consistently.
   - Cons: higher migration risk; changes Identity CLR types and snapshot expectations; requires deciding cardinality and lifecycle between accounts and Personas; may exceed the 800-line review budget.
   - Effort: High

3. **Keep Identity persistence but build a custom user/role abstraction without activating authentication yet** — Add administrative user/role CRUD over `UserManager`/`RoleManager` while leaving existing endpoints anonymous.
   - Pros: isolates user provisioning from authorization rollout; minimizes disruption to current anonymous API tests.
   - Cons: incomplete security story; risks creating accounts that cannot authenticate through a defined scheme; may defer the key decision the change is probably meant to solve.
   - Effort: Medium

### Recommendation

Proceed with Approach 1 as the proposal baseline: activate ASP.NET Core Identity using the existing framework-owned `IdentityUser`/`IdentityRole` model, add role assignment use cases through Application contracts backed by Infrastructure adapters, and keep the SGV Domain independent from Identity. The proposal should explicitly preserve anonymous access for existing read endpoints unless the user asks for broader API protection, and it should define the authentication scheme before implementation.

If the business requires tying accounts to staff/person records, treat that as a deliberate extension of the proposal or a follow-up change. Do not silently introduce `ApplicationUser`/`PersonaId` coupling during the first Identity activation because it changes schema, tests, and lifecycle semantics.

### Risks

- Existing tests and specs currently assert no authentication/authorization requirement; adding `[Authorize]` broadly would violate current contracts unless OpenSpec deltas modify them.
- `DatosSemilla` uses role names as `IdentityRole.Id`; changing IDs later can break existing `AspNetUserRoles` references and role assignment behavior.
- Enabling Identity services without choosing a concrete authentication mechanism (cookie, bearer token, Identity API endpoints, external provider) leaves the feature ambiguous and hard to test.
- Custom Identity user types or Persona linkage can cause migration drift, snapshot updates, and review-size growth beyond the 800 changed-line budget.
- Swagger tests currently expect no `security`, `bearer`, or `oauth2` content; protected endpoints need targeted documentation updates and test changes.
- Audit currently records `null` user IDs through `UsuarioActualAnonimo`; switching to authenticated users must preserve safe behavior for anonymous endpoints and avoid logging sensitive Identity fields.
- Identity tables use string keys while SGV application tables use Guid keys; tests already encode this exception, so new application-owned user tables must not blur the boundary accidentally.

### Ready for Proposal

Yes — the next phase should create a proposal for activating ASP.NET Core Identity over the existing `AspNet*` schema, adding user management and role assignment capabilities, preserving Clean Architecture boundaries, and explicitly defining non-goals: custom `ApplicationUser`, Persona linkage, password reset/email confirmation flows, external providers, and broad authorization of existing endpoints unless confirmed by the user.
