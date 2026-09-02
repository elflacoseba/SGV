# R-03-06 — Pipeline middleware de SGV.Api

Orden declarativo del pipeline HTTP de `SGV.Api` definido en `src/SGV.Api/Program.cs` (después de `var app = builder.Build();`). El orden es crítico: el rate limiter corre antes de `UseAuthentication` para que las políticas anónimas (`ForgotPassword`/`ResetPassword`/`Setup`/`Refresh`) throttleen bursts antes de que la pipeline corta por `[Authorize]`.

## Orden de ejecución

| # | Middleware | Origen | Propósito |
| --- | --- | --- | --- |
| 1 | `UseExceptionHandler()` | Built-in | Captura excepciones no manejadas y las traduce a `ProblemDetails` (wire uniforme con `ApiResults`). |
| 2 | `UseStatusCodePages()` | Built-in | Devuelve `ProblemDetails` para 404/405 sin cuerpo (mantiene una sola forma de error). |
| 3 | `UseSwagger()` + `UseSwaggerUI()` (sólo Development) | `Swashbuckle.AspNetCore` | Documentación OpenAPI en `/swagger/v1/swagger.json` y UI en `/swagger`. |
| 4 | `UseCors()` | Built-in | Aplica la política default (ver R-03-05). |
| 5 | `UseRequestLocalization()` | Built-in | Fija cultura única `es-AR` (configurada vía `Configure<RequestLocalizationOptions>` en `services`). |
| 6 | `UseRateLimiter()` | `Microsoft.AspNetCore.RateLimiting` | Aplica las políticas nombradas (`ForgotPassword`, `ResetPassword`, `Setup`, `ChangePassword`, `Refresh`). |
| 7 | `UseAuthentication()` | Built-in | Resuelve el `ClaimsPrincipal` desde el JWT bearer. |
| 8 | `Use(...)` custom (Revalidator fallback) | Inline en `Program.cs` | Segunda capa de revalidación para bearer principals que no pasaron por `OnTokenValidated` (defensa en profundidad; ver detalles abajo). |
| 9 | `UseAuthorization()` | Built-in | Aplica `[Authorize]` y la `FallbackPolicy = RequireAuthenticatedUser()`. |
| 10 | `MapHealthChecks("/health/live")` | `Microsoft.AspNetCore.Diagnostics.HealthChecks` | Liveness probe (siempre 200 si el proceso está vivo). |
| 11 | `MapHealthChecks("/health/ready")` | idem | Readiness probe: ejecuta `SgvDbContextReadinessHealthCheck` (tag `ready`). |
| 12 | `MapControllers()` | Built-in | Despacha a los controllers MVC. |

## Health checks

Ambos endpoints usan `HealthCheckResponseWriter.WriteJson` y se exponen con `.AllowAnonymous()`. `Predicate` controla qué checks corren:

| Endpoint | `Predicate` | Check ejecutado |
| --- | --- | --- |
| `/health/live` | `_ => false` | ninguno — siempre 200 si la pipeline responde |
| `/health/ready` | `check => check.Tags.Contains("ready")` | `SgvDbContextReadinessHealthCheck("mysql", tags: new[] { "ready" })` |

El check de MySQL abre un `MySqlConnection` raw contra la `ConnectionStrings:SgvDatabase` y ejecuta `SELECT 1` (no usa `DbContext`, evita `ServerVersion.AutoDetect`).

## Rate limiter — políticas y orden

`AddRateLimiter` se registra antes del middleware. Cada política se monta con `RateLimitPartition.GetFixedWindowLimiter`:

| Política | Tipo | Cuota | Ventana | Partition key |
| --- | --- | --- | --- | --- |
| `ForgotPassword` | anónimo | 3 | 15 min | IP |
| `ResetPassword` | anónimo | 5 | 15 min | IP |
| `Setup` | anónimo | 5 | 15 min | IP |
| `ChangePassword` | autenticado | 5 | 15 min | subject, fallback IP |
| `Refresh` | anónimo | `RefreshOptions.RateLimitPermitLimit` (20) | `RefreshOptions.RateLimitWindowMinutes` (15 min) | IP |

`RejectionStatusCode = 429`. `OnRejected` agrega el header `Retry-After` (en segundos) leyendo `MetadataName.RetryAfter` del lease; el fallback es `900` (15 min en segundos).

El middleware (`UseRateLimiter`) corre **antes** de `UseAuthentication` para que las ráfagas anónimas sean throttled antes de evaluar `[Authorize]`. Las políticas autenticadas (`ChangePassword`) corren igualmente porque `[Authorize]` se evalúa en `UseAuthorization`, después del rate limit.

## Revalidator de credenciales

Hay dos puntos donde se invoca `RevalidatorCredenciales.SigueVigenteAsync(userId, ct)`:

1. `JwtBearerEvents.OnTokenValidated` (registrado vía `AddOptions<JwtBearerOptions>().Configure<...>`): marca `context.HttpContext.Items[RevalidatorCredenciales.ValidationMarker] = true` y llama `context.Fail(...)` si la credencial fue revocada.
2. Middleware `Use(...)` defensivo entre `UseAuthentication` y `UseAuthorization`: si el principal está autenticado y NO trae el marker (porque no es un bearer real, p.ej. test scheme), chequea `iss`. Si no hay `iss`, salta; si lo hay, lee subject y revalida. Sin subject, responde 401. Sin vigência, responde 401.

`SigueVigenteAsync(userId, ct)` ejecuta un lookup en `SgvIdentityUser` por Id, evalúa `LockoutEnd <= UtcNow` y devuelve `false` si el usuario está bloqueado o eliminado.

## CORS

Política default registrada con `AddCors`. En `Development` con `AllowedOrigins` vacíos se usa `SetIsOriginAllowed(_ => true)` con `AllowAnyHeader`/`AllowAnyMethod` sin credenciales. Con `AllowedOrigins` poblada: `WithOrigins(allowedOrigins).AllowCredentials()`. La política `AllowCredentials()` exige origins explícitos (los wildcards la rompen).

## ProblemDetails global

`AddProblemDetails()` está registrado en `services`. `ApiResults` aplica `traceId` automático en cada `ProblemDetails`/`ValidationProblemDetails` emitido desde controllers (vía `Activity.Current?.Id ?? HttpContext.TraceIdentifier`).

## Localización

`Configure<RequestLocalizationOptions>` fija `DefaultRequestCulture = "es-AR"`, `SupportedCultures = ["es-AR"]`, `SupportedUICultures = ["es-AR"]`, `FallBackToParentCultures = false`. El contrato HTTP wire es invariante; la cultura afecta orden de strings (`StringComparer.Create(CultureInfo.CurrentCulture, ...)`) y formateo a nivel proceso.

## Bootstrap diagnóstico de jerarquía

`app.Lifetime.ApplicationStarted.Register(...)` ejecuta `IDiagnosticoJerarquiaService.DiagnosticarAsync()` en un `Task.Run` separado, en su propio `IServiceScope` (porque el servicio es scoped). Un fallo del diagnóstico **no aborta** el arranque: sólo se loggea `WARNING`. La jerarquía con ciclos se repara vía script SQL (`docs/script-listar-ciclos-jerarquia-unidades-organizativas.sql`).

## Diagrama de flujo

```
HTTP Request
   │
   ▼
ExceptionHandler ──► ProblemDetails (500)
   │
   ▼
StatusCodePages ──► ProblemDetails (404/405)
   │
   ▼ (sólo Development)
Swagger / SwaggerUI
   │
   ▼
CORS (preflight + headers)
   │
   ▼
RequestLocalization (es-AR)
   │
   ▼
RateLimiter ──► 429 + Retry-After
   │
   ▼
Authentication (JWT bearer; OnTokenValidated dispara Revalidator #1)
   │
   ▼
Middleware revalidator defensivo (Revalidator #2)
   │
   ▼
Authorization (FallbackPolicy = RequireAuthenticatedUser)
   │
   ▼
   ┌─ /health/live ──► 200 siempre
   ├─ /health/ready ──► SgvDbContextReadinessHealthCheck
   └─ /api/v1/* ──► MapControllers
```

## Notas operativas

- El order `UseAuthentication` antes de `UseAuthorization` es mandatorio (no se debe invertir).
- `UseRateLimiter` antes de `UseAuthentication` es mandatorio para que las políticas anónimas corten antes de evaluar el JWT.
- El `Use(...)` defensivo no debe envolverse en `try/catch` — cualquier excepción debe burbujear a `UseExceptionHandler()`.

## Referencias

- How-to: [Bloquear y desbloquear usuario](../how-to/04-bloquear-desbloquear-usuario.md)
- How-to: [Operar flujo de recuperación de contraseña](../how-to/02-operar-flujo-recuperacion-contrasena.md)
- How-to: [Diagnosticar ciclos jerárquicos](../how-to/01-diagnosticar-ciclos-jerarquia.md)
- R-03-09 — Health checks (formato JSON, semántica liveness/readiness)
- R-03-10 — Taxonomía de errores (cómo se materializa en `ProblemDetails`)
