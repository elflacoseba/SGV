# Tasks: Endurecer cookie Web y CORS API para deploy real

> Cambio `2026-07-10-endurecer-cookie-cors-deploy` (issue #101). Strict TDD:
> tests RED → implementación GREEN → docs → PR. Cada tarea ≤2 h e
> independientemente testeable. Sigue `work-unit-commits` (un commit por unidad).

## Review Workload Forecast

| Field                | Value                                                       |
|----------------------|-------------------------------------------------------------|
| Estimated changed lines | ~230-310 (4 modificados + 2 archivos de tests nuevos)     |
| 400-line budget risk  | Low                                                         |
| Chained PRs recommended | No                                                       |
| Suggested split       | Single PR hacia `develop`                                  |
| Delivery strategy     | single-pr (cacheada por orchestrator)                       |
| Chain strategy        | size-exception (PR único justificado por budget OK)          |

Decision needed before apply: No
Chained PRs recommended: No
Chain strategy: size-exception
400-line budget risk: Low

## Fase 1 — Test RED (CORS API)

### T-01 — `CorsAllowedOriginsValidationTests.cs` con 4 tests `[Fact]` ✅
- **Capa**: Tests · **Tamaño**: 1.5 h · **TDD**: RED primero
- **Patrón**: `WebApplicationFactory<SGV.Api.Program>` + `ConfigureAppConfiguration(AddInMemoryCollection(...))` ya vigente en `tests/SGV.Tests/Seguridad/JwtOptionsTests.cs:30-40`. Cada test override `Jwt:SigningKey` (clave 32+ bytes) para no chocar con `ValidateOnStart`.
- **Body**: crear `tests/SGV.Tests/Api/CorsAllowedOriginsValidationTests.cs` con 4 `[Fact]` (sin `[Theory]` por convención orchestrator):
  1. `HostBuild_Production_SinAllowedOrigins_LanzaInvalidOperationException` — `UseEnvironment("Production")` + `["AllowedOrigins:0"] = null`; assert `Throws<InvalidOperationException>` con mensaje conteniendo `"AllowedOrigins"`.
  2. `HostBuild_Production_AllowedOriginsPoblado_Arranca` — `UseEnvironment("Production")` + `["AllowedOrigins:0"] = "https://app.example.com"`; `CreateClient()` no lanza.
  3. `HostBuild_Development_AllowedOriginsVacio_Arranca` — `UseEnvironment("Development")` sin override; `CreateClient()` no lanza.
  4. `ProgramCs_Api_NoCombinaAllowAnyOriginConAllowCredentials` — lee `src/SGV.Api/Program.cs`, regex que niegue coexistencia de `AllowAnyOrigin()` y `AllowCredentials()` dentro del mismo bloque `AddCors(...)` (regression estructural, análogo a `JwtOptionsTests:101-117`).
- **Acceptance**: 3 funcionales RED + 1 estructural RED; commit independiente.
- **Commit**: `test(api): add CorsAllowedOriginsValidationTests red`.

### T-02 — Validación fail-loud en `src/SGV.Api/Program.cs:110-125` (GREEN) ✅
- **Capa**: API · **Tamaño**: 1 h · **TDD**: GREEN
- **Body**: mover lectura de `AllowedOrigins` ANTES de `AddCors`. Insertar guard `if (!builder.Environment.IsDevelopment() && allowedOrigins.Length == 0) throw new InvalidOperationException("SGV.Api: la sección de configuración 'AllowedOrigins' es obligatoria fuera del ambiente Development. Configure AllowedOrigins__0, AllowedOrigins__1, ... vía variables de entorno.");`. Reemplazar rama `else` por `policy.SetIsOriginAllowed(_ => true).AllowAnyHeader().AllowAnyMethod()` (SIN `AllowCredentials()`).
- **Acceptance**: `dotnet test --filter "CorsAllowedOriginsValidationTests"` 4/4 verde; `grep -R "AllowAnyOrigin" src/SGV.Api/` no devuelve match combinado con `AllowCredentials`.
- **Commit**: `feat(api): enforce AllowedOrigins validation, drop AllowAnyOrigin+AllowCredentials`.

## Fase 2 — Test RED (Cookie Web)

### T-03 — `WebCookieAuthenticationOptionsTests.cs` con 2 tests `[Fact]` ✅
- **Capa**: Tests · **Tamaño**: 1.5 h · **TDD**: RED primero
- **Body**: crear `tests/SGV.Tests/Web/WebCookieAuthenticationOptionsTests.cs` con 2 `[Fact]`. Reusar `SgvWebApplicationFactory`; resolver `IOptionsMonitor<CookieAuthenticationOptions>.Get(CookieAuthenticationDefaults.AuthenticationScheme)` desde `factory.Services`. Override `SgvApiOptions.BaseUrl` (URL absoluta válida) para que la factoría no rompa por validación:
  1. `WebCookieAuthOptions_Production_SecurePolicyAlways` — `UseEnvironment("Production")`; assert `Cookie.HttpOnly == true && SameSite == SameSiteMode.Lax && SecurePolicy == CookieSecurePolicy.Always`.
  2. `WebCookieAuthOptions_Development_SecurePolicySameAsRequest` — `UseEnvironment("Development")`; assert `Cookie.HttpOnly == true && SameSite == SameSiteMode.Lax && SecurePolicy == CookieSecurePolicy.SameAsRequest`.
- **Acceptance**: 2 tests RED.
- **Commit**: `test(web): add WebCookieAuthenticationOptionsTests red`.

### T-04 — Ternario `CookieSecurePolicy` en `src/SGV.Web/Program.cs:26` (GREEN) ✅
- **Capa**: Web · **Tamaño**: 0.5 h · **TDD**: GREEN
- **Body**: reemplazar `SecurePolicy = CookieSecurePolicy.SameAsRequest;` por `SecurePolicy = builder.Environment.IsDevelopment() ? CookieSecurePolicy.SameAsRequest : CookieSecurePolicy.Always;`. NO tocar `HttpOnly`, `SameSite`, `LoginPath`, `LogoutPath`.
- **Acceptance**: `dotnet test --filter "WebCookieAuthenticationOptionsTests"` 2/2 verde.
- **Commit**: `feat(web): make cookie SecurePolicy conditional on environment`.

## Fase 3 — Validación integral

### T-05 — Suite completa (gate de PR, sin commit propio) ✅
- **Tamaño**: 1 h
- **Body**: `dotnet build SGV.slnx` + `dotnet test SGV.slnx --no-build --configuration Release`. Esperado: verde, incluidos 6 tests nuevos, existentes, y `[MySqlFact]` que se skipean limpio si MySQL no está (issue #98 ya corregido).
- **Acceptance**: 0 failures; sin warnings nuevos. (Resultado: 1608 pass / 12 fail pre-existentes bug #59, 0 nuevos fallos.)

## Fase 4 — Documentación

### T-06 — Docs (`decisiones-implementacion.md` + `AGENTS.md`) ✅
- **Capa**: Docs · **Tamaño**: 1 h
- **Body**:
  - `docs/decisiones-implementacion.md`: nueva sección "Hardening runtime: cookie y CORS por ambiente" con (a) matriz ambiente↔seguridad (`HttpOnly`, `SameSite`, `SecurePolicy`, `AllowedOrigins`, HSTS), (b) env vars `AllowedOrigins__0=https://app.example.com` (sin slash final, nota explícita), (c) snippet `UseForwardedHeaders` con `KnownProxies`/`KnownNetworks` para reverse proxy — **solo doc, NO implementar**.
  - `AGENTS.md`: bullet en "Decisiones Técnicas que NO conviene romper" referenciando cookie/CORS por ambiente y enlazando a la sección detallada.
- **Acceptance**: ambos archivos actualizados; enlaces internos válidos; matriz coherente con el código.
- **Commit**: `docs: document runtime hardening matrix and proxy headers guide`.

## Fase 5 — PR

### T-07 — Branch + 5 commits cohesivos + PR ✅
- **Tamaño**: 0.5 h
- **Body**: branch `feature/101-cookie-cors-deploy-hardening` desde `develop`; commits 1-5 según T-01..T-06 (cada commit pasa `dotnet build SGV.slnx` por sí solo); conventional commits sin `Co-Authored-By` ni atribución a IA; `gh pr create --base develop` con descripción que refiera #101 y liste los criterios de éxito de `proposal.md:101-107`.
- **Acceptance**: `git log --oneline develop..HEAD` muestra 5 commits; `gh pr view` muestra PR abierto. (Resultado: PR #106 abierto en https://github.com/elflacoseba/SGV/pull/106 con labels `security` + `bug`.)

## Work-unit commits (resumen)

1. `test(api): add CorsAllowedOriginsValidationTests red`
2. `feat(api): enforce AllowedOrigins validation, drop AllowAnyOrigin+AllowCredentials`
3. `test(web): add WebCookieAuthenticationOptionsTests red`
4. `feat(web): make cookie SecurePolicy conditional on environment`
5. `docs: document runtime hardening matrix and proxy headers guide`

## Definition of Done / Out of scope

- DoD: build limpio, suite verde, 6 tests nuevos `[Fact]`, 2 archivos modificados, 2 docs actualizados, `grep AllowAnyOrigin` no combinado, 5 commits granulares, PR abierto contra develop con #101, sin `Co-Authored-By`, sin nuevas deps NuGet, sin migraciones EF.
- Out of scope: JWT format/firma, rate limiting/captcha, `ApiBearerTokenHandler`, política de autorización del API, implementación de `UseForwardedHeaders` (queda solo como doc).
