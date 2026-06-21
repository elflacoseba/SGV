# Design: Implementar Identity para Usuarios y Roles

## Technical Approach

Enable ASP.NET Core Identity without moving authentication concepts into Domain. `SGV.Aplicacion` will own user/role use-case contracts and DTOs; `SGV.Infraestructura` will adapt Identity (`UserManager`, `RoleManager`, EF stores) behind those contracts; `SGV.Api` will expose protected user-management endpoints and keep existing read endpoints anonymous.

Identity users will use an Infrastructure type with required `PersonaId`, backed by a MySQL FK to `Personas`. Roles are a fixed first-slice catalog: `Administrador`, `GestorVacantes`, `Consultor`.

## Architecture Decisions

| Decision | Choice | Alternatives considered | Rationale |
|---|---|---|---|
| Identity boundary | Create `SgvIdentityUser : IdentityUser` in Infrastructure and change `SgvDbContext` to `IdentityDbContext<SgvIdentityUser, IdentityRole, string>` | Add auth fields to Domain `Persona`; expose `IdentityUser` in Application | Keeps Domain clean and lets EF enforce required Persona linkage. |
| Persona linkage | `SgvIdentityUser.PersonaId` is required, FK `AspNetUsers.PersonaId -> Personas.Id`, `Restrict`, unique index | Nullable FK; cascade delete; no unique index | Users cannot be standalone; restrict preserves history when Persona is deactivated/deleted logically; unique prevents multiple auth accounts per Persona in this slice. |
| Role catalog | Constants in Application plus deterministic Infrastructure seed for only three roles | Dynamic role CRUD; keep existing five seeded roles | Specs require fixed assignable roles and no SGV role management. Existing seed must be replaced. |
| API auth rollout | Add authentication/authorization middleware but no global fallback policy; protect only Identity/user-management controllers | Global `[Authorize]`; `MapIdentityApi` | Preserves anonymous read endpoints and avoids default registration endpoints that could create users without Persona. |

## Data Flow

```text
POST /api/v1/usuarios ──[Authorize Administrador]──> Application use case
      │                                               │
      │ validates Persona exists + role catalog        ▼
      └──────────────────────────────> Identity adapter/UserManager
                                                   │
                                                   ▼
                                       AspNetUsers(PersonaId FK), AspNetUserRoles
```

Login uses custom API endpoints over Identity services. User creation and role assignment never accept arbitrary role names beyond the fixed catalog.

## File Changes

| File | Action | Description |
|---|---|---|
| `src/SGV.Dominio/**` | No change | Domain remains Identity-agnostic. |
| `src/SGV.Aplicacion/Seguridad/Usuarios/**` | Create | Requests, DTOs, result/error types, role constants, user-management use-case interfaces/services. |
| `src/SGV.Aplicacion/Seguridad/IUsuarioActual.cs` | Modify | Extend current-user contract only if needed for authenticated `UserId`, `PersonaId`, roles. |
| `src/SGV.Infraestructura/Seguridad/SgvIdentityUser.cs` | Create | Identity user with required `Guid PersonaId`. |
| `src/SGV.Infraestructura/Persistencia/SgvDbContext.cs` | Modify | Use `SgvIdentityUser` and apply Identity configuration. |
| `src/SGV.Infraestructura/Persistencia/Configuraciones/SgvIdentityUserConfiguracion.cs` | Create | Maps `PersonaId`, FK, unique index, delete restrict. |
| `src/SGV.Infraestructura/Persistencia/DatosSemilla.cs` | Modify | Replace old roles with the three fixed roles and deterministic IDs/stamps. |
| `src/SGV.Infraestructura/Seguridad/**` | Create | Identity adapter for user creation, login, role listing and role assignment. |
| `src/SGV.Api/Program.cs` | Modify | Register Identity stores, bearer auth, authorization, Swagger security, and middleware order: `UseAuthentication()` then `UseAuthorization()`. |
| `src/SGV.Api/Controllers/UsuariosController.cs`, `AuthController.cs` | Create | Protected user/role management and login endpoints with safe DTOs. |
| `src/SGV.Infraestructura/Persistencia/Migraciones/*` | Create | Pomelo migration for `PersonaId`, FK/index, and role seed replacement. |

## Interfaces / Contracts

Application contracts should expose SGV-safe types only:

```csharp
public sealed record CrearUsuarioRequest(Guid PersonaId, string UserName, string Email, string Password, IReadOnlyCollection<string> Roles);
public sealed record UsuarioDto(string Id, Guid PersonaId, string UserName, string Email, IReadOnlyCollection<string> Roles);
public interface IUsuarioServicioComandos { Task<UsuarioCommandResult> CrearAsync(CrearUsuarioRequest request, CancellationToken ct = default); }
public interface IRolServicioConsulta { Task<IReadOnlyList<string>> ListAsync(CancellationToken ct = default); }
```

## Testing Strategy

| Layer | What to Test | Approach |
|---|---|---|
| Domain | No Identity dependency; Persona deactivate/reactivate does not model users | Reflection/unit tests over Domain assembly and Persona behavior. |
| Application | Persona existence validation, fixed-role validation, result mapping | xUnit tests with fakes for Persona repository and Identity adapter. |
| Persistence | `AspNetUsers.PersonaId` required FK, unique index, restrict delete, fixed role seed | EF model tests plus MySQL/Pomelo integration tests where available. |
| API | Anonymous reads remain accessible; user endpoints require auth/role; Swagger has bearer security only for protected operations | `WebApplicationFactory` tests with fake auth/Identity services. |

## Migration / Rollout

Generate a Pomelo migration adding non-null `PersonaId` to `AspNetUsers`. Because there is no known mapping for existing users, migration should fail loudly or require an explicit backfill if `AspNetUsers` contains rows. Replace existing seeded roles with the fixed catalog; pre-deploy verification must ensure removed roles have no user assignments. Rollback reverts endpoints, auth registration, Identity user customization, and the migration.

## Open Questions

- [ ] Confirm token lifetime and password policy values for the first API slice.
