# Tasks: Endurecer autorización de los controllers restantes del API

## Review Workload Forecast

| Field | Value |
|-------|-------|
| Total estimated changed lines | ~245 |
| 400-line budget risk | Low |
| Chained PRs recommended | No (2-PR split por dependencia lógica) |
| Suggested split | PR-1 (A+B: Personas + Ocupaciones + UnidadesOrganizativas) → PR-2 (C: catálogos + Program.cs + AuthController + docs) |
| Delivery strategy | ask-on-risk |
| Chain strategy | stacked-to-develop |
| Decision needed before apply | No |

Decision needed before apply: No
Chained PRs recommended: No
Chain strategy: stacked-to-develop
400-line budget risk: Low

### Suggested Work Units

| Unit | Goal | Likely PR | Notes |
|------|------|-----------|-------|
| 1 | Endurecer mutantes + tests 401/403/2xx | PR-1 (base: develop) | ~184 LOC; FallbackPolicy NO activa |
| 2 | Default-deny + catálogos + Login + docs | PR-2 (base: develop, depende de PR-1) | ~61 LOC; FallbackPolicy entra al final |

> Bajo 400 se fusionan A+B. Partición se mantiene para que la `FallbackPolicy` global solo se active cuando ya no quedan controllers anónimos accidentales.

## Phase 1: PR-1 — Endurecer controllers mutantes

### Personas
- [x] 1.1 `src/SGV.Api/Controllers/PersonasController.cs`: `using Microsoft.AspNetCore.Authorization;`, `[Authorize]` clase, `[Authorize(Roles = RolesSgv.Administrador)]` en `Create`, `Update`, `Delete`, `Reactivate`, `UpsertSkill`, `DeleteSkill`. ~10 LOC.
- [x] 1.2 `tests/SGV.Tests/Api/PersonasControllerTests.cs`: migrar ~14 llamantes a `CreateAdminClient()`, `[Theory]` 401 y `[Fact]` 403 por mutación, invertir chequeo `Controller_HasAuthorizeAttribute`. ~50 LOC. Verify: `dotnet test --filter PersonasControllerTests`.

### Ocupaciones
- [x] 1.3 `src/SGV.Api/Controllers/OcupacionesController.cs`: `[Authorize]` clase + admin override en `Create`, `Update`, `Finalize`, `Reactivate`, `Delete`. ~9 LOC.
- [x] 1.4 `tests/SGV.Tests/Api/OcupacionesControllerTests.cs`: reemplazar `IClassFixture<WebApplicationFactory<SGV.Api.Program>>` por `IClassFixture<ApiWebApplicationFactory>`, migrar a `CreateAdminClient()`, `[Theory]` 401 y `[Fact]` 403 por mutación. ~45 LOC. Verify: `dotnet test --filter OcupacionesControllerTests`.

### Unidades Organizativas
- [x] 1.5 `src/SGV.Api/Controllers/UnidadesOrganizativasController.cs`: `[Authorize]` clase + admin override en `Create`, `Update`, `ChangeParent`, `Reactivate`, `Delete`. ~10 LOC.
- [x] 1.6 `tests/SGV.Tests/Api/UnidadesOrganizativasControllerTests.cs`: migrar ~32 llamantes a `CreateAdminClient()`, `[Theory]` 401, `[Fact]` 403 por mutación, invertir chequeo reflexión. ~60 LOC. Verify: `dotnet test --filter UnidadesOrganizativasControllerTests`.
- [x] 1.7 Gate PR-1: `dotnet test SGV.slnx` verde con mutantes endurecidos y FallbackPolicy todavía NO activa. Sin este gate, no mergear.

## Phase 2: PR-2 — Default-deny global + catálogos + Login

### Catálogos read-only
- [ ] 2.1 `src/SGV.Api/Controllers/NivelesCargoController.cs`: `[Authorize]` a nivel clase. ~3 LOC.
- [ ] 2.2 `tests/SGV.Tests/Api/NivelesCargoControllerTests.cs`: anónimo→`401` y autenticado→`2xx` en `GetAll`/`GetById`. ~15 LOC. Verify: `dotnet test --filter NivelesCargoControllerTests`.
- [ ] 2.3 `src/SGV.Api/Controllers/TipoUnidadesOrganizativasController.cs`: `[Authorize]` a nivel clase. ~3 LOC.
- [ ] 2.4 `tests/SGV.Tests/Api/TipoUnidadesOrganizativasControllerTests.cs`: anónimo→`401` y autenticado→`2xx`. ~15 LOC. Verify: `dotnet test --filter TipoUnidadesOrganizativasControllerTests`.

### Login + Fallback policy
- [ ] 2.5 `src/SGV.Api/Controllers/AuthController.cs`: `[AllowAnonymous]` en `Login`. ~2 LOC.
- [ ] 2.6 `tests/SGV.Tests/Api/AuthControllerTests.cs`: `Login_AnonymousHeaderless_Returns200` con `CreateClient()` sin header. ~10 LOC.
- [ ] 2.7 `src/SGV.Api/Program.cs`: cambiar `AddAuthorization()` por `AddAuthorization(opts => opts.FallbackPolicy = new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build())`. ~3 LOC. Gate: `dotnet test SGV.slnx` con 2.1–2.6.

### Documentación
- [ ] 2.8 `docs/decisiones-implementacion.md` sección seguridad/auth: default-deny, `[Authorize]` por defecto, única excepción `[AllowAnonymous]` en `Login`, precedente Cargos e issue #90. ~10 LOC.
- [ ] 2.9 Gate PR-2: `dotnet test SGV.slnx` 100% verde con FallbackPolicy global activa y catálogos autenticados. Sin este gate, no mergear.