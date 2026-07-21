# Archive Report: Password Reset (issue #181)

| Campo | Valor |
|---|---|
| **Change** | `2026-07-21-password-reset-181` |
| **Issue** | #181 |
| **Fecha de archive** | 2026-07-21 |
| **Realizado por** | `sdd-archive` sub-agent |
| **Rama final** | `2026-07-21-password-reset-181` |
| **PR** | [#182](https://github.com/elflacoseba/SGV/pull/182) |
| **Modo de artifact store** | Hybrid (OpenSpec + Engram) |
| **Veredicto verify** | PASS WITH WARNINGS |

## Resumen del cambio

Permite resetear la contraseña (recuperación de credenciales self-service). `SGV.Api` expone `POST /api/v1/auth/forgot-password` y `POST /api/v1/auth/reset-password` marcados con `[AllowAnonymous]`, con rate limiting fijo por IP, anti-enumeración por respuesta byte-equivalente, validación fail-loud de `SmtpOptions`, y tokens de 1 hora. `SGV.Web` agrega `ForgotPassword` y `ResetPassword` como páginas públicas Razor Pages, alimentadas por `IAuthApiClient` con un `HttpClient` anónimo separado del bearer handler.

## Lo entregado vs lo planeado

| Aspecto | Planeado | Entregado | Estado |
|---|---|---|---|
| Backend | `IPasswordResetService` + endpoints `forgot-password` / `reset-password` | ✅ Servicio separado de `IAuthServicio`, `PasswordResetService` con anti-enumeration y rotación de `SecurityStamp`, validadores FluentValidation | Completo |
| Persistencia | Sin migraciones nuevas (tablas de Identity ya existentes) | ✅ `SecurityStamp` se rota vía `userManager.ResetPasswordAsync` (diseño Identity) | Completo |
| Contratos | `ForgotPasswordRequest`, `ResetPasswordRequest`, `AuthApiRoutes` | ✅ Records en `SGV.Contracts/Seguridad/Usuarios/UsuarioContracts.cs`, rutas en `SGV.Contracts/Auth/AuthApiRoutes.cs` | Completo |
| API | `AuthController` con dos endpoints | ✅ `[AllowAnonymous]` + `[EnableRateLimiting]`, body genérico siempre 200 | Completo |
| Configuración | `SmtpOptions` con `ValidateOnStart` | ✅ `[Required]`+`[Url]` para `WebBaseUrl`, fail-loud fuera de Development | Completo |
| Web UI | Razor Pages `ForgotPassword` / `ResetPassword` | ✅ Páginas con layout separado del shell, widget `data-password="bar"`, URL-decode del token, redirect post-success a SignIn con TempData | Completo |
| Web cliente | `IAuthApiClient` con métodos anónimos | ✅ Dos named clients (auth + anonymous), `ForgotPasswordAsync`/`ResetPasswordAsync` exceptuados de `CommandResultMapper` | Completo |
| Rate limiting | 3/15min para forgot, 5/15min para reset | ✅ `AddFixedWindowLimiter`, `Retry-After` header, `UseRateLimiter()` antes de `UseAuthentication()` | Completo |
| Packaging | Script `auth-password.js` debe terminar en `wwwroot/js/pages/` | ✅ Fix post-review: nueva tarea `inspiniaPages` en `gulpfile.js` materializa el asset vía `bun run build` | Completo |

### Desviaciones documentadas

1. **D1**: `IdentityOptions.Tokens.PasswordResetTokenLifespan` no existe en Identity 10. La lifespan de 1 hora se configura vía `DataProtectionTokenProviderOptions.TokenLifespan`. Cubierto por `IdentityTokenProvidersTests`.
2. **D2**: Constructor de `AuthApiClient` con dos `HttpClient` (auth + anonymous) en lugar del single-client original. Ctor `internal two-HttpClient` mantiene compatibilidad con overrides de tests previos.
3. **D3**: `IEmailSender` genérico para evitar dependencia de Razor UI. Identity 10 lo acepta.
4. **D4**: `SmtpEmailSender.SendPasswordResetAsync` legacy mantiene firma con `userId` crudo. No usado por el flujo productivo.
5. **D5**: Constantes `ForgotPasswordPolicyName`/`ResetPasswordPolicyName` viven en `AuthController` y se referencian por string literal en `Program.cs` (acoplamiento por convención).
6. **D6**: `auth-password.js` requiere tarea Gulp explícita para copiarse (fix post-review).

## Specs sincronizados

| Spec | Acción | Detalles |
|---|---|---|
| `openspec/specs/password-reset-flow/spec.md` | **Creado** | Spec canónica nueva: 8 requisitos (`Endpoints anónimos`, `Servicio separado de AuthServicio`, `Token providers + 1h`, `SMTP con URL-encoding`, `SmtpOptions ValidateOnStart`, `Rate limiting por IP`, `Wire-types + validadores`, `Anti-enumeración`). |
| `openspec/specs/password-reset-web/spec.md` | **Creado** | Spec canónica nueva: 5 requisitos (`ForgotPassword pública`, `ResetPassword con token en query`, `SignIn expone enlace`, `Propagación de 429 con retry copy`, `Errores de transporte sin redirigir`). |
| `openspec/specs/sgv-web-authentication/spec.md` | **Actualizado** | 2 requisitos ADDED: "SignIn expone enlace ¿Olvidaste tu contraseña?", "El enlace de recuperación es la única acción de SignIn fuera del submit de credenciales". |
| `openspec/specs/web-apiclient-transport-contract/spec.md` | **Actualizado** | 3 requisitos ADDED: "`IAuthApiClient.ForgotPasswordAsync`/`ResetPasswordAsync` son anónimos", "ForgotPassword/ResetPassword mantienen propagación de fallos nativos", "ForgotPassword/ResetPassword exceptuadas de `CommandResultMapper`". |

## Métricas finales

| Métrica | Valor |
|---|---|
| **Tests totales** | 2685/2685 PASS (0 failed, 0 skipped) |
| **Tests nuevos** | ~80 (validators, service, rate limiter, controller, registration, AuthApiClient, Razor Pages, SignIn link) |
| **Commits feature** | 18 (Batches 1/3 + 2/3 + 3/3) |
| **Commits post-review** | 1 (`fix(web): publish auth-password.js to wwwroot via Gulp pipeline`) |
| **Commits archive** | 1 (`chore(sdd): archive change 2026-07-21-password-reset-181`) |
| **Net diff total** | ~+3363 líneas (incluye `auth-password.js` snapshot) |
| **Size:exception** | Documentada (~8x el budget de 400 LoC) |

## Decisiones clave tomadas

1. **Servicio separado `IPasswordResetService`**: el flujo de reset NO comparte lógica con `IAuthServicio` (cuyo scope es autenticación activa). Vive en `SGV.Aplicacion/Seguridad/PasswordReset/` con implementación en `SGV.Infraestructura/Seguridad/PasswordResetService.cs`.
2. **Anti-enumeración por respuesta byte-equivalente**: el `ForgotPassword` ignora el outcome del servicio y siempre responde `200 OK` con un mensaje genérico. Tests verifican byte-equivalencia para usuario conocido/inexistente.
3. **Dos `HttpClient` separados**: `AuthenticatedAuthApiClient` (con bearer) y `AnonymousAuthApiClient` (sin bearer) registrados con `AddHttpClient(name, ...)` para evitar ramificación sensible por request.
4. **`[AllowAnonymous]` explícito** sobre los endpoints pese a `FallbackPolicy = RequireAuthenticatedUser()` — el orden de middlewares respeta la política global pero los endpoints quedan exceptuados.
5. **Rate limit fijo por IP** con `QueueLimit=0` para no encolar; respuestas `429` con `Retry-After` header.
6. **Packaging Gulp explícito** (post-review): la pipeline `bun run build` ahora copia `auth-password.js` desde `InspinaTemplate/` a `wwwroot/js/pages/` con `allowEmpty: true` para no romper clones sin el template.

## Verification summary

- ✅ `dotnet build SGV.slnx`: 0 errors (NU1510 preexistente en `SGV.Infraestructura.csproj`, no relacionado).
- ✅ `dotnet test SGV.slnx`: 2685/2685 PASS (0 failed, 0 skipped).
- ✅ `bun run build`: clean, ahora publica `auth-password.js` en `wwwroot/js/pages/`.

## Warnings aceptados (no bloqueantes)

1. **Sin covering test del 429 de `ResetPassword`** (5 req / 15 min): solo `ForgotPassword` tiene covering test de rate limit. Recomendado follow-up.
2. **`PasswordResetIdentityMySqlFactTests.cs` no materializado**: la rotación de `SecurityStamp` por `ResetPasswordAsync` no se valida contra MySQL real. Deuda asumida para un follow-up cuando haya DB local.
3. **Constantes de rate-limit policies duplicadas** entre `AuthController` y `Program.cs` (acoplamiento por string). Mover a `SGV.Contracts` sugerido como follow-up.

## Próximos pasos sugeridos

1. Merge del PR #182 a `develop`.
2. Issue de seguimiento para covering test de `ResetPassword_429`.
3. Issue de seguimiento para `PasswordResetIdentityMySqlFactTests` (rotación de `SecurityStamp`).
4. Considerar mover constantes `ForgotPasswordPolicyName`/`ResetPasswordPolicyName` a `SGV.Contracts`.
5. Auditar otros assets de `InspinaTemplate/` que el Web pueda necesitar (`auth-two-factor.js`, etc.) y decidir si extender `gulpfile.js` con una lista más amplia.

## SDD Cycle Complete

El change `2026-07-21-password-reset-181` ha sido completamente planificado, implementado, verificado y archivado. Los specs nuevos viven en `openspec/specs/password-reset-{flow,web}/`, los deltas se sincronizaron en `openspec/specs/{sgv-web-authentication,web-apiclient-transport-contract}/`, y los artefactos de planificación se movieron a `openspec/changes/archive/2026-07-21-password-reset-181/`. El packaging gap detectado en `verify-report.md` se cerró con un commit post-review (`fix(web): publish auth-password.js to wwwroot via Gulp pipeline`).
