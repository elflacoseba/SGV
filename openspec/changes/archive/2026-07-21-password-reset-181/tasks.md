# Tasks: Permitir resetear la contraseña (#181)

**Change**: `2026-07-21-password-reset-181` | **Issue**: #181 | **Modo**: Strict TDD
**Delivery**: single PR, `size:exception` (~420 LoC, budget 400 LoC, maintainer override)

## Review Workload Forecast

Decision needed before apply: No
Chained PRs recommended: No
Chain strategy: size-exception
400-line budget risk: High

### Work Units (20)

WU-01 MailKit dep, WU-02 SmtpOptions+ValidateOnStart, WU-03 AddDefaultTokenProviders+1h, WU-04 SmtpEmailSender, WU-05 IEmailSender DI, WU-06 IPasswordResetService+validators, WU-07 PasswordResetService, WU-08 IPasswordResetService DI, WU-09 Routes/DTOs Contracts, WU-10 Rate limiting, WU-11 AuthController endpoints, WU-12 Orden middleware, WU-13 Config SMTP Dev, WU-14 IAuthApiClient anónimo, WU-15 AuthApiClient recovery, WU-16 HttpClient anónimo Web, WU-17 ForgotPassword page, WU-18 ResetPassword page, WU-19 Link SignIn, WU-20 Full suite

## Phase 1: Foundation

- [x] 1.1 WU-01: `MailKit` en csproj. `dotnet restore && build`.
- [x] 1.2 WU-02: `SmtpOptions.cs`. `BindConfiguration("Smtp").ValidateOnStart()`.
- [x] 1.3 WU-03: `.AddDefaultTokenProviders()` + `PasswordResetTokenLifespan = 1h`.
- [x] 1.4 WU-04: `SmtpEmailSender` (IEmailSender). Logger/MailKit switch. Link con `EscapeDataString`.
- [x] 1.5 WU-05: `AddSingleton<IEmailSender, SmtpEmailSender>`.

## Phase 2: Core

- [x] 2.1 WU-06: `IPasswordResetService` + 2 validadores FluentValidation.
- [x] 2.2 WU-07: `PasswordResetService`. UserManager, email fire-and-forget, anti-enumeración.
- [x] 2.3 WU-08: `AddScoped<IPasswordResetService, PasswordResetService>`.

## Phase 3: API

- [x] 3.1 WU-09: Routes + `ForgotPasswordRequest`/`ResetPasswordRequest` records.
- [x] 3.2 WU-10: `AddRateLimiter`: ForgotPassword 3/15min, ResetPassword 5/15min.
- [x] 3.3 WU-11: Endpoints `[AllowAnonymous]` `[EnableRateLimiting]` en AuthController.
- [x] 3.4 WU-12: `UseRateLimiter()` antes de `UseAuthentication()`.
- [x] 3.5 WU-13: `Smtp { Mode:Logger, FromAddress, WebBaseUrl }` en `appsettings.Development.json`.

## Phase 4: Web

- [x] 4.1 WU-14: `ForgotPasswordAsync`/`ResetPasswordAsync` en IAuthApiClient.
- [x] 4.2 WU-15: Implementar con HttpClient anónimo. 429 como HttpRequestException.
- [x] 4.3 WU-16: `AddHttpClient("AnonymousAuthApiClient")` sin bearer handler.
- [x] 4.4 WU-17: `ForgotPassword.cshtml`+.cs. Capturar 429/red/timeout.
- [x] 4.5 WU-18: `ResetPassword.cshtml`+.cs. `UnescapeDataString`. Widget `data-password="bar"`.
- [x] 4.6 WU-19: Link "¿Olvidaste?" → `/auth/forgot-password` en SignIn.

## Phase 5: Verification

- [x] 5.1 WU-20: `dotnet test SGV.slnx` verde. `bun run build` sin errores.

## PR Boundary

Branch `2026-07-21-password-reset-181` desde `develop`. Commits atómicos. PR body: `size:exception`, #181, checklist.

## Tests

Validators → `PasswordResetValidators` · Service → `PasswordResetService` · Email → `SmtpEmailSender` · API → `Api.PasswordReset` · Web → `Web.PasswordReset` · Client → `AuthApiClientPasswordReset` · MySQL → `PasswordResetIdentityMySqlFact`

## Riesgos

1. MailKit conflict: fijar versión, validar árbol transitivo.
2. Rate limit tras auth: `UseRateLimiter()` antes de `UseAuthentication()`.
3. Token URL-encoding: `UnescapeDataString` exactamente 1 vez; test `+a/b=`.
4. WebBaseUrl: `ValidateOnStart` fail-loud fuera de Dev.
5. Policy name mismatch: constantes compartidas, test 429.
