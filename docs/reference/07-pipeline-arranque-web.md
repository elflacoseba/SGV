# R-03-07 — Pipeline de arranque de SGV.Web

Orden declarativo del pipeline de `SGV.Web` definido en `src/SGV.Web/Program.cs`. La shell Razor Pages se monta sobre cookie auth + bridge bearer (`ApiBearerTokenHandler`) hacia `SGV.Api` mediante clientes tipados.

## Servicios registrados (en orden lógico)

### Opciones tipadas

| Servicio | Sección | Validación |
| --- | --- | --- |
| `SgvApiOptions` | `SgvApi` | `BaseUrl` debe ser URI absoluta (`ValidateOnStart`) |
| `JwtOptions` | `Jwt` | `SigningKey` ≥32 bytes UTF-8 (`ValidateOnStart`) |
| `HttpClientFactoryOptions` | (PostConfigure) | `client.Timeout = 30s` global, aplicado después de cada `AddHttpClient` |

### Autenticación y autorización

| Servicio | Lifetime | Notas |
| --- | --- | --- |
| `AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme).AddCookie(...)` | — | Cookie auth; `HttpOnly=true`, `SameSite=Lax`; `SecurePolicy = IsDevelopment ? SameAsRequest : Always`. Paths: `/auth/sign-in`, `/auth/logout`, `/error/403`. `OnValidatePrincipal` invoca `CookiePrincipalRevalidator`. |
| `AddAuthorization()` | — | Sin `FallbackPolicy`; las Razor Pages declaran `[Authorize]` por página o filtro global. |
| `AddHttpContextAccessor` | Scoped | Necesario para `ApiBearerTokenHandler` |
| `CookieRevalidatorCircuitState` | Singleton | Estado del circuit breaker del revalidator |
| `CookiePrincipalRevalidator` | Scoped | Valida el JWT contra `/api/v1/auth/...` en cada request autenticada |
| `ApiBearerTokenHandler` | Transient | Bridge que reenvía el JWT desde el cookie a `Authorization: Bearer` |

### Helpers de sesión

| Servicio | Lifetime | Notas |
| --- | --- | --- |
| `IAuthSessionRedirector` / `AuthSessionRedirector` | Scoped | Traduce `ErrorCategoria.Unauthorized` a `/auth/sign-in` con guard anti open-redirect |
| `IAuthSessionFactory` / `AuthSessionFactory` | Singleton | Construye `ClaimsPrincipal` + `AuthenticationProperties` desde opciones + access token |
| `IRefreshTokenCookieAccessor` / `RefreshTokenCookieAccessor` | Singleton | Único punto de lectura/escritura de la cookie `sgv.rt` (hardening por ambiente) |

### Clientes HTTP tipados

Todos los clientes (excepto `Setup`, `Auth.Anonymous` y el health probe) llevan `ApiBearerTokenHandler` en su pipeline.

| Cliente | Nombre interno | `BaseAddress` | `Timeout` | Handler bearer | Notas |
| --- | --- | --- | --- | --- | --- |
| `SgvApiHealthProbeHttpClient` | `SgvApiHealthProbeHttpClient.Name` | `SgvApi:BaseUrl` | 3 s | No | Health probe anónimo |
| `CookiePrincipalRevalidator.HttpClientName` | idem | idem | 10 s | No | Revalida el bearer contra la API |
| `AuthApiClient.AuthenticatedHttpClientName` | idem | idem | 10 s | Sí | Login + flujo autenticado |
| `AuthApiClient.AnonymousHttpClientName` | idem | idem | 10 s | No | Password recovery (explícitamente anónimo) |
| `UnidadOrganizativaApiClient` | `AddHttpClient<I..., T...>` | idem | 10 s | Sí | Listado / árbol / dropdowns |
| `CargoApiClient` | idem | idem | 10 s | Sí | CRUD |
| `PuestosApiClient` | idem | idem | 10 s | Sí | CRUD + disponibles |
| `HabilidadApiClient` | idem | idem | 10 s | Sí | CRUD |
| `CategoriaHabilidadApiClient` | idem | idem | 10 s | Sí | Catálogo |
| `PersonaApiClient` | idem | idem | 10 s | Sí | CRUD + typeahead |
| `OcupacionApiClient` | idem | idem | 10 s | Sí | Read-only (Slice 3a agregará mutaciones) |
| `VacanteApiClient` | idem | idem | 10 s | Sí | CRUD |
| `UsuarioApiClient` | idem | idem | 10 s | Sí | CRUD + roles + lockout |
| `AuditoriaApiClient` | idem | idem | 10 s | Sí | Admin-only |
| `SetupApiClient` | idem | idem | 10 s | No | Anónimo explícito (issue #195) |

Cross-cutting defaults para `IHttpClientFactory`: `ConfigureHttpClientDefaults` setea `PooledConnectionLifetime = 2 min` en el `SocketsHttpHandler` primario para reciclar conexiones y evitar `ObjectDisposedException` post keep-alive idle.

### Health check

| Servicio | Tag | Notas |
| --- | --- | --- |
| `SgvApiUpstreamHealthCheck` | `ready` | Probe upstream contra `SGV.Api` (no toca MySQL directo) |

### Localización

Idéntica al pipeline de la API: `es-AR` único, `FallBackToParentCultures = false`.

### Cache

| Servicio | Notas |
| --- | --- |
| `AddMemoryCache()` | Usado por `SetupApiClient` para el cache de status (issue #195) |

## Orden de ejecución del pipeline

| # | Middleware | Origen | Propósito |
| --- | --- | --- | --- |
| 1 | `UseExceptionHandler("/Error")` (sólo !Development) | Built-in | Traduce a página de error |
| 2 | `UseHsts()` (sólo !Development) | Built-in | HSTS 30 días por default |
| 3 | `UseHttpsRedirection()` | Built-in | Redirige HTTP → HTTPS |
| 4 | `UseRouting()` | Built-in | Resuelve endpoint |
| 5 | `UseRequestLocalization()` | Built-in | Cultura `es-AR` |
| 6 | `UseAuthentication()` | Built-in | Cookie auth + `OnValidatePrincipal` |
| 7 | `UseAuthorization()` | Built-in | Filtros `[Authorize]` por página |
| 8 | `MapGet("/api/v1/personas/consulta", ...)` | Minimal API | BFF same-origin para el typeahead (issue #101) |
| 9 | `MapHealthChecks("/health/live")` | `Microsoft.AspNetCore.Diagnostics.HealthChecks` | Liveness probe (200 siempre) |
| 10 | `MapHealthChecks("/health/ready")` | idem | Readiness: `SgvApiUpstreamHealthCheck` |
| 11 | `MapStaticAssets()` + `MapRazorPages().WithStaticAssets()` | Built-in | Razor Pages + bundler de `wwwroot` |

## BFF same-origin — `/api/v1/personas/consulta`

Ruta interna expuesta por la shell Web que reenvía la búsqueda a `SGV.Api` manteniendo el JWT del lado servidor:

| Aspecto | Valor |
| --- | --- |
| Verbo / ruta | `GET /api/v1/personas/consulta` |
| Parámetros query | `p`, `pageSize`, `search`, `sort`, `segmento`, `soloSinUsuario` |
| Cap de `search` | `SearchMaxLength = 200` bytes UTF-8 (responde 400 si excede) |
| Whitelist de `sort` | `apellidos_asc/desc`, `nombres_asc/desc`, `legajo_asc/desc`, `email_asc/desc`, `documento_asc/desc` |
| Whitelist de `segmento` | `activas`, `eliminadas` |
| Authz | `.RequireAuthorization()` (cookie auth) |
| Mapeo de errores upstream | `PersonaBffUpstreamProblems.Build` para `HttpRequestException` y `TaskCanceledException` |

Normalización aplicada:
- `p < 1 ⇒ 1`
- `pageSize ∉ [1, 100] ⇒ clamp`
- `sort` ausente ⇒ `apellidos_asc`
- `segmento` ausente ⇒ `Activas`

El BFF existe para mantener el JWT en el servidor: el navegador sólo envía la cookie Web (`sgv.auth`) y el cliente tipado reenvía el bearer a la API.

## Manejo del refresh token

`RefreshTokenCookieAccessor` centraliza la escritura/lectura de la cookie `sgv.rt`:

| Aspecto | Detalle |
| --- | --- |
| Atributos cookie | `HttpOnly=true`, `SameSite=Lax`, `SecurePolicy` por ambiente (igual que la cookie de auth) |
| Lifetime | `RefreshTokenOptions.RefreshTokenLifetimeDays` (default 14) |
| Path | `/` (default) |
| Lectura | Sólo desde `AuthSessionFactory` y `CookiePrincipalRevalidator` |

## Cultura

`Configure<RequestLocalizationOptions>` fija `es-AR` para `DefaultRequestCulture`, `SupportedCultures` y `SupportedUICultures`. `FallBackToParentCultures = false`. La capa UI (model binding, validación, orden de strings, formato de fechas) usa esta cultura; el contrato HTTP wire con la API sigue siendo invariante (`System.Text.Json` default).

## Diagrama de flujo

```
HTTP Request
   │
   ▼
ExceptionHandler (/Error en !Development)
   │
   ▼
HSTS (30d en !Development)
   │
   ▼
HttpsRedirection
   │
   ▼
Routing
   │
   ▼
RequestLocalization (es-AR)
   │
   ▼
Authentication (cookie) — OnValidatePrincipal → revalidator
   │
   ▼
Authorization (Authorize por página)
   │
   ▼
   ┌─ /api/v1/personas/consulta ──► BFF upstream (con bearer bridge)
   ├─ /health/live ──► 200 siempre
   ├─ /health/ready ──► SgvApiUpstreamHealthCheck
   ├─ /api/v1/* (vía cliente tipado, desde PageModel)
   └─ /* ──► MapRazorPages + MapStaticAssets
```

## Notas operativas

- El orden `UseRouting` antes de `UseAuthentication`/`UseAuthorization` es mandatorio en .NET 10.
- `MapStaticAssets` requiere que `bun.lock` y `wwwroot` estén commiteados al día (ver CI en `.github/workflows/ci.yml`).
- `MapGet` para el BFF precede a `MapRazorPages` para evitar captura por routing de Razor.
- `SetupApiClient` se registra sin `ApiBearerTokenHandler` por chicken-and-egg del setup inicial.

## Referencias

- Tutorial: [Levantar el sistema local](../tutorials/01-levantar-sistema-local.md)
- Tutorial: [Primera mutación de unidad organizativa](../tutorials/02-primera-mutacion-unidad-organizativa.md)
- How-to: [Operar flujo de recuperación de contraseña](../how-to/02-operar-flujo-recuperacion-contrasena.md)
- R-03-09 — Health checks
- R-03-10 — Taxonomía de errores (cómo `ApiProblemReader`/`CommandResultMapper` mapean respuestas API)
