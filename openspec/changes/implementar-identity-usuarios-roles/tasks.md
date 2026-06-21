# Tasks: Implementar Identity para Usuarios y Roles

## Review Workload Forecast

| Field | Value |
|-------|-------|
| Estimated changed lines | 850-1,200 |
| 400-line budget risk | High |
| Chained PRs recommended | Yes |
| Suggested split | Single PR requires `size:exception`; otherwise split by work units |
| Delivery strategy | single-pr |
| Chain strategy | size-exception |

Decision needed before apply: Yes
Chained PRs recommended: Yes
Chain strategy: size-exception
400-line budget risk: High

### Suggested Work Units

| Unit | Goal | Likely PR | Notes |
|------|------|-----------|-------|
| 1 | Application contracts/tests | PR 1 | Commit `feat(identity): add user role contracts` |
| 2 | Persistence/migration | PR 1 | Commit `feat(identity): enforce persona linked users` |
| 3 | API auth/endpoints | PR 1 | Commit `feat(api): expose identity endpoints` |
| 4 | Verification | PR 1 | Commit `test(identity): cover user roles` |

## Phase 1: Test-First Boundaries

- [x] 1.1 Add `tests/SGV.Tests/Aplicacion/Seguridad/UsuarioServicioComandosTests.cs` for Persona existence, fixed roles, invalid role, and missing user assignment.
- [x] 1.2 Add `tests/SGV.Tests/Persistencia/SgvIdentityUserConfiguracionTests.cs` for required `PersonaId`, FK, unique index, restrict delete.
- [x] 1.3 Extend `tests/SGV.Tests/Persistencia/DatosSemillaTests.cs` for only `Administrador`, `GestorVacantes`, `Consultor`.
- [x] 1.4 Add `tests/SGV.Tests/Api/UsuariosControllerTests.cs`, `AuthControllerTests.cs`; extend `SwaggerConfigurationTests.cs` for auth and anonymous reads.

## Phase 2: Application Contracts

- [x] 2.1 Create `src/SGV.Aplicacion/Seguridad/RolesSgv.cs` with fixed catalog and validation.
- [x] 2.2 Create `src/SGV.Aplicacion/Seguridad/Usuarios/**` SGV-safe requests, DTOs, results, ports, and services.
- [x] 2.3 Validate `IPersonaRepository` and role catalog before Identity adapter calls.
- [x] 2.4 Extend `IUsuarioActual.cs` only if needed; update `src/SGV.Api/Seguridad/UsuarioActualAnonimo.cs`.

## Phase 3: Infrastructure and Database

- [x] 3.1 Create `src/SGV.Infraestructura/Seguridad/SgvIdentityUser.cs` with required `Guid PersonaId`.
- [x] 3.2 Update `src/SGV.Infraestructura/Persistencia/SgvDbContext.cs` to `IdentityDbContext<SgvIdentityUser, IdentityRole, string>`.
- [x] 3.3 Create `src/SGV.Infraestructura/Persistencia/Configuraciones/SgvIdentityUserConfiguracion.cs` for MySQL FK/index/restrict.
- [x] 3.4 Modify `src/SGV.Infraestructura/Persistencia/DatosSemilla.cs` for deterministic fixed roles.
- [x] 3.5 Create `src/SGV.Infraestructura/Seguridad/**` Identity adapters; use configurable initial password policy and token lifetime defaults.
- [x] 3.6 Register adapters in `src/SGV.Infraestructura/DependencyInjection.cs`.
- [x] 3.7 Generate Pomelo migration under `src/SGV.Infraestructura/Persistencia/Migraciones/` with fail-loud existing-user backfill guard.

## Phase 4: API Integration

- [x] 4.1 Update `src/SGV.Api/Program.cs` with Identity stores, bearer auth, authorization, Swagger bearer security, middleware order.
- [x] 4.2 Create `src/SGV.Api/Controllers/UsuariosController.cs` protected by `Administrador`.
- [x] 4.3 Create `src/SGV.Api/Controllers/AuthController.cs` for login with configured initial token lifetime.
- [x] 4.4 Extend `tests/SGV.Tests/Api/ApiWebApplicationFactory.cs` with fake auth/user-role services.

## Phase 5: Verification

- [x] 5.1 Run `dotnet build` and `dotnet test`; ensure Identity specs are covered.
- [x] 5.2 Confirm `src/SGV.Dominio/**` has no `Microsoft.AspNetCore.Identity` dependency.
- [x] 5.3 Update `docs/migracion-inicial-sgv.sql` only if migration script regeneration is part of apply scope.

## Remediation: Verify Gate Gaps

- [x] R1 Add valid role assignment tests (`AsignarRolesAsync_WithValidRoles_AssignsRoles`, `AsignarRolesAsync_WithUnsupportedRole_RejectsWithoutAssignment`) in `UsuarioServicioComandosTests.cs`.
- [x] R2 Add persona deactivation-preserves-link persistence test (`IdentityUser_LinkedToPersona_SurvivesPersonaDeactivateAndReactivate`) in `IdentityUserPersistenceTests.cs`.
- [x] R3 Add invalid PersonaId DB rejection test (`SaveIdentityUser_WithInvalidPersonaId_ThrowsDbUpdateException`) in `IdentityUserPersistenceTests.cs`.
