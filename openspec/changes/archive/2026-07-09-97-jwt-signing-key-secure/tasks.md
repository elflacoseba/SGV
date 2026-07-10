# Tasks: #97 — JWT signing key seguro

> Plan de implementación del change `97-jwt-signing-key-secure`. Sigue el orden
> **strict-TDD** del repo (`openspec/config.yaml:11`): tests RED → implementación
> GREEN. Cada tarea ≤2h, verificable independientemente, con commit cohesivo
> según `work-unit-commits`.

## Review Workload Forecast

| Field | Value |
|-------|-------|
| Estimated changed lines | ~270–330 |
| 400-line budget risk | Low |
| Chained PRs recommended | No |
| Suggested split | Single PR |
| Delivery strategy | ask-on-risk |
| Chain strategy | size-exception (single PR con budget OK) |

Decision needed before apply: Yes
Chained PRs recommended: No
Chain strategy: size-exception
400-line budget risk: Low

### Suggested Work Units

| Unit | Goal | Likely PR | Notes |
|------|------|-----------|-------|
| 1 | feat(security): JWT signing key validado al arranque + tests reales + docs + CI | PR #1 único | incluye 5 tests fail-loud + 2 tests end-to-end + docs |

## Phase 1 — Fundamentos y tests RED

### T-01 — Vaciar default hardcodeado de `JwtOptions.SigningKey`
- **Capa**: Infraestructura
- **Tamaño**: 0.1h
- **TDD**: prerequisite de T-03 — sin esto, el fallback `?? new JwtOptions()` en `Program.cs:71` tapa el fail-loud
- **Body**: en `src/SGV.Infraestructura/Seguridad/JwtOptions.cs:11`, reemplazar `SigningKey { get; set; } = "SGV-development-signing-key-change-before-production-2026";` por `SigningKey { get; set; } = string.Empty;`
- **Acceptance**: `grep -rn "SGV-development-signing-key" src/` → 0 matches
- **Commit**: `wip(security): empty default for JwtOptions.SigningKey` (atómico con T-02)
- **Dependencias**: ninguna — **atómico con T-02**, no commitear solo

### T-02 — Placeholder dev pinned en `appsettings.Development.json`
- **Capa**: API (config)
- **Tamaño**: 0.1h
- **TDD**: prerequisite de T-03 (test estructural #5 lee este archivo)
- **Body**: en `src/SGV.Api/appsettings.Development.json`, agregar clave `"Jwt": { "SigningKey": "DEV-PLACEHOLDER-DO-NOT-USE-IN-PROD-0000000000000000" }` (51 bytes ASCII puros, 34 prefijo + 16 ceros — ver `design.md:36`)
- **Acceptance**: JSON parsea OK; placeholder mide exactamente 51 bytes UTF-8
- **Commit**: `feat(security): empty JwtOptions.SigningKey default + dev placeholder` (atómico con T-01)
- **Dependencias**: T-01 (commit atómico)

### T-03 — `JwtOptionsTests.cs` con 5 tests (4 RED + 1 regression guard)
- **Capa**: Tests
- **Tamaño**: 1.5h
- **TDD**: **RED primero** — escribir los 5 tests ANTES de T-04/T-05; correr `dotnet test --filter "FullyQualifiedName~JwtOptionsTests"` y confirmar 4 fallan + 1 pasa (regression guard del placeholder)
- **Body**: crear `tests/SGV.Tests/Seguridad/JwtOptionsTests.cs` con 5 tests `[Fact]` (sin `[MySqlFact]` — la validación no toca DB):
  1. `HostBuild_SinSigningKey_LanzaOptionsValidationException` — `WithWebHostBuilder(b => b.ConfigureAppConfiguration(cb => cb.AddInMemoryCollection(new Dictionary<string,string?> { ["Jwt:SigningKey"] = "" })))`; assert `Assert.Throws<OptionsValidationException>(() => factory.CreateClient())`.
  2. `HostBuild_SigningKeyCorto_LanzaOptionsValidationException` — override `["Jwt:SigningKey"] = "short-key"`.
  3. `HostBuild_SigningKey31Bytes_Lanza` — override con string de 31 bytes UTF-8 exactos.
  4. `HostBuild_PlaceholderDev_Arranca` — sin override; `CreateClient()` no lanza (regression guard).
  5. `appsettings_Development_Tiene_Placeholder_Valido_≥32Bytes` — `File.ReadAllText` + `JsonDocument.Parse`, assert `Jwt.SigningKey.GetByteCount(UTF8) >= 32`.
- **Acceptance**: 4 RED en commit T-03; los 5 GREEN tras T-05
- **Commit**: `test(security): add JwtOptions fail-loud + placeholder structural tests`
- **Dependencias**: T-01 + T-02 (commit atómico previo, branch ya con default vacío + placeholder)

## Phase 2 — Validator + Program.cs (GREEN)

### T-04 — `ConfigureJwtBearerFromJwtOptions.cs` (post-configurador diferido)
- **Capa**: API
- **Tamaño**: 0.5h
- **TDD**: implementación que el GREEN de T-03/T-05 necesita
- **Body**: crear `src/SGV.Api/Seguridad/ConfigureJwtBearerFromJwtOptions.cs` con `internal sealed class ConfigureJwtBearerFromJwtOptions(IOptions<JwtOptions> options) : IPostConfigureOptions<JwtBearerOptions>`. Guard defensivo `if (name != JwtBearerDefaults.AuthenticationScheme) return;`. Arma `TokenValidationParameters` con `ValidateIssuer/Audience/Lifetime/IssuerSigningKey = true`, `IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.SigningKey))`. `InternalsVisibleTo("SGV.Tests")` ya está en `SGV.Api.csproj:26-28`.
- **Acceptance**: compila; `name != JwtBearerDefaults.AuthenticationScheme` corto-circuita (futuro multi-scheme)
- **Commit**: `feat(security): defer JwtBearer IssuerSigningKey via IPostConfigureOptions`
- **Dependencias**: T-03 (RED confirmado)

### T-05 — Refactor de `Program.cs` (AddOptions/Validate/ValidateOnStart)
- **Capa**: API
- **Tamaño**: 0.7h
- **TDD**: **GREEN** — al terminar, los 5 tests de T-03 pasan
- **Body**: en `src/SGV.Api/Program.cs`:
  1. Borrar líneas 71-72 (`GetSection().Get<JwtOptions>() ?? new JwtOptions()` + `Configure<JwtOptions>`).
  2. Insertar: `builder.Services.AddOptions<JwtOptions>().BindConfiguration(JwtOptions.SectionName).Validate(o => !string.IsNullOrWhiteSpace(o.SigningKey) && Encoding.UTF8.GetByteCount(o.SigningKey) >= 32, "Jwt:SigningKey must be configured and ≥32 UTF-8 bytes").ValidateOnStart();`.
  3. Vaciar el cuerpo de `AddJwtBearer(options => { })` (lógica ahora en T-04).
  4. Agregar `builder.Services.AddSingleton<IPostConfigureOptions<JwtBearerOptions>, ConfigureJwtBearerFromJwtOptions>();` justo después.
- **Acceptance**: `dotnet build SGV.slnx` sin warnings nuevos; `dotnet test --filter "FullyQualifiedName~JwtOptionsTests"` 5/5 verde; suite completa verde (incluido `AuthControllerTests.Login_WithValidCredentials_ReturnsAccessToken` que sigue fakeando auth vía `ApiWebApplicationFactory:869`)
- **Commit**: `feat(security): validate JwtOptions.SigningKey at startup with ValidateOnStart`
- **Dependencias**: T-03, T-04

## Phase 3 — Validación end-to-end con JWT real

### T-06 — `JwtRealWebApplicationFactory.cs` (factoría sin fakes)
- **Capa**: Tests
- **Tamaño**: 1.5h
- **TDD**: prerequisite de T-07 — los tests reales no pueden correr sin factoría sin fakes
- **Body**: crear `tests/SGV.Tests/Seguridad/JwtRealWebApplicationFactory.cs`. Clase `internal sealed class JwtRealWebApplicationFactory(string signingKey) : WebApplicationFactory<SGV.Api.Program>`. Override `ConfigureWebHost` con `AddInMemoryCollection(new Dictionary<string,string?> { ["Jwt:SigningKey"] = signingKey })`. Método público `async Task InitializeAsync()`:
  1. `var _ = Server` fuerza `Build()` → `ValidateOnStart`.
  2. Scope + `SgvDbContext` + `UserManager<SgvIdentityUser>` + `RoleManager<IdentityRole>`.
  3. Siembra idempotente: `RolesSgv.Administrador` (`RoleExistsAsync` → `CreateAsync`); `PersonaEntity { Id = Guid.NewGuid(), Nombres = "Admin", Apellidos = "Seed", IsActive = true }` ANTES de `SgvIdentityUser` (FK obligatoria `SgvIdentityUserConfiguracion.cs:19-23`, OnDelete=Restrict); `SgvIdentityUser` con `PersonaId = persona.Id` (property pública, `UserManager.CreateAsync` no la setea); `CreateAsync(admin, "Admin#12345")`; `AddToRoleAsync(admin, RolesSgv.Administrador)`. Connection string por env/JSON/default (vía `TestSgvDbContextFactory.ResolveConnectionString()`). **NO implementar `IAsyncLifetime`** — xUnit v2.9.2 no la invoca sobre `new ...()` en cuerpo de test (observación Engram #771); `DisposeAsync() => Task.CompletedTask;`.
- **Acceptance**: compila; test manual con `await factory.InitializeAsync()` siembra rol + persona + admin sin `DbUpdateException`
- **Commit**: `test(security): add JwtRealWebApplicationFactory with idempotent admin seed`
- **Dependencias**: T-05 (validator funcionando; el host debe arrancar)

### T-07 — `JwtRealAuthTests.cs` con 2 tests `[MySqlFact]`
- **Capa**: Tests
- **Tamaño**: 1.0h
- **TDD**: Patrón A del `design.md:175-214` — cada test invoca `InitializeAsync()` explícito con `using var factory`
- **Body**: crear `tests/SGV.Tests/Seguridad/JwtRealAuthTests.cs`. Constante: `private static class TestKeys { public const string Host = "TEST-KEY-HOST-MIN-32-BYTES-PADDING!!"; public const string Foreign = "TEST-KEY-FOREIGN-32-BYTES-PADDING!!"; }`. Tests:
  1. `[MySqlFact] TokenEmitido_ConClaveConfigurada_AccedeEndpointProtegido_200` — `using var factory = new JwtRealWebApplicationFactory(TestKeys.Host); await factory.InitializeAsync(); using var client = factory.CreateClient();` → `POST /api/v1/auth/login` con `LoginRequest("admin", "Admin#12345")` → extraer `AccessToken` → `GET /api/v1/usuarios` con `Bearer` → assert `200 OK`.
  2. `[MySqlFact] TokenFirmado_ConClaveDistinta_Rechazado_401` — `using var factory = new JwtRealWebApplicationFactory(TestKeys.Host); await factory.InitializeAsync();` → firma `JwtSecurityToken` HS256 con `TestKeys.Foreign` (issuer/audience = "SGV") → `GET /api/v1/usuarios` con `Bearer <foreign>` → assert `401 Unauthorized`.
- **Acceptance**: ambos pasan localmente con MySQL corriendo; skip limpio sin MySQL
- **Commit**: `test(security): add JwtRealAuthTests covering real signing key roundtrip`
- **Dependencias**: T-06

## Phase 4 — Documentación y CI

### T-08 — Documentación de secretos JWT
- **Capa**: Docs
- **Tamaño**: 0.5h
- **Body**: (a) en `docs/decisiones-implementacion.md`, agregar sección `## Gestión de secretos JWT` tras la sección `## SgvDbContextFactory fail-loud`: contrato fail-loud (`OptionsValidationException` con mensaje que nombra `Jwt:SigningKey`), comando `dotnet user-secrets set "Jwt:SigningKey" "<random ≥32 bytes>" --project src/SGV.Api`, prod/CI con `Jwt__SigningKey` env var o secret manager, nota explícita de que el placeholder dev NO es apto para producción; (b) en `AGENTS.md`, agregar paso `0.5` en "Ruta rápida para trabajar": setear `Jwt:SigningKey` vía `dotnet user-secrets` antes del primer `dotnet run` de `SGV.Api`.
- **Acceptance**: `grep -n "Gestión de secretos JWT" docs/decisiones-implementacion.md` encuentra la sección; `grep -n "Jwt:SigningKey.*user-secrets" AGENTS.md` encuentra el comando
- **Commit**: `docs(security): document JWT signing key management per environment`
- **Dependencias**: T-05 (comportamiento estable y documentable)

### T-09 — CI exporta `Jwt__SigningKey` (defense-in-depth)
- **Capa**: CI
- **Tamaño**: 0.3h
- **Body**: en `.github/workflows/ci.yml:42-43`, agregar `Jwt__SigningKey: ${{ secrets.JWT_SIGNING_KEY }}` al bloque `env:` del step "Run tests (with MySQL)". Decisión operativa (no bugfix): hoy CI corre con el placeholder dev; este export garantiza que la suite no dependa del placeholder si alguien lo borra a futuro. Valor del secreto dedicado (≥32 bytes), rotado manualmente.
- **Acceptance**: workflow exporta env var; instrucción para el mantenedor en `docs/decisiones-implementacion.md` (agregar nota al final de la sección de T-08)
- **Commit**: `ci(security): export Jwt__SigningKey from secrets.JWT_SIGNING_KEY`
- **Dependencias**: T-08 (doc lista para mencionar el secreto)

## Phase 5 — Verificación final

### T-10 — Build + suite completa
- **Capa**: Verificación
- **Tamaño**: 0.3h
- **Body**: (a) `dotnet restore && dotnet build SGV.slnx` — sin warnings nuevos; (b) `dotnet test SGV.slnx` — suite completa verde; los 7 tests nuevos (5 `JwtOptionsTests` + 2 `JwtRealAuthTests`) pasan o se skipean limpio; (c) `grep -rn "SGV-development-signing-key" src/` → 0 matches; (d) confirmar placeholder pinned en `src/SGV.Api/appsettings.Development.json`. PR description declara out-of-scope (#59 no se toca) y referencia #97.
- **Acceptance**: cumple criterios de éxito del `proposal.md:118-127`; PR listo para review
- **Commit**: ninguno (verificación, no commit)
- **Dependencias**: T-01 a T-09