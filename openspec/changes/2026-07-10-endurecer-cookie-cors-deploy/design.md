# Design: Endurecer cookie Web y CORS API

## Technical Approach

Concentrar todos los cambios en los composition roots (`SGV.Web/Program.cs` y `SGV.Api/Program.cs`). Introducir una validación fail-loud de `AllowedOrigins` antes de `AddCors` y un ternario sobre `CookieSecurePolicy` en el registro de la cookie. Cubrir los invariantes con tests de integración `WebApplicationFactory<TEntryPoint>` siguiendo el patrón ya vigente en `tests/SGV.Tests/Seguridad/JwtOptionsTests.cs`.

## Architecture Decisions

| Decisión | Alternativas | Elijo y por qué |
|----------|--------------|-----------------|
| Validación de `AllowedOrigins` antes de `AddCors` con `throw new InvalidOperationException` | `IValidateOptions<CorsOptions>`; `AddOptions<>().Validate().ValidateOnStart()` | Throw directo: `AllowedOrigins` no es una opción tipada, vive en `IConfiguration`. `ValidateOnStart` exige un tipo options y agregaría indirección sin valor. El mensaje del throw es la única retroalimentación al operador. |
| En `Development` sin origins: `SetIsOriginAllowed(_ => true).AllowAnyHeader().AllowAnyMethod()` SIN `AllowCredentials()` | Mantener `AllowAnyOrigin().AllowCredentials()` (status quo); usar `WithOrigins("http://localhost:5266","https://localhost:7298")` hardcodeado | El segundo pierde el fallback si el dev borra `appsettings.Development.json` por accidente. El primero replica cualquier origen sin credenciales: seguro para dev (no hay sesión que exfilte), respeta el spec escenario 4-5. |
| Ternario inline sobre `CookieSecurePolicy` en `AddCookie` | Mover a un `ICookiePolicyProvider`/clase aparte; opciones tipadas con `Configure<CookieAuthenticationOptions>` por ambiente | El cambio es de 1 línea en un bloque de 9. Mantenerlo inline facilita lectura y diff review; cualquier abstracción agregaría fricción sin reducir riesgo. |
| Inspección de cookie vía `IOptionsMonitor<CookieAuthenticationOptions>.Get(CookieAuthenticationDefaults.AuthenticationScheme)` desde el test factory | Llamar a `/auth/sign-in` y leer `Set-Cookie`; bindear opciones vía `IConfigureOptions<CookieAuthenticationOptions>` | Las opciones que `AddCookie` registra son la fuente de verdad; leer el header HTTP sería flaky porque algunos servers reescriben atributos en el wire. |

## File Changes

| File | Acción | Descripción |
|------|--------|-------------|
| `src/SGV.Api/Program.cs:110-125` | Modificar | Mover lectura de `AllowedOrigins` antes de `AddCors`; throw si `!IsDevelopment && Length == 0`; rama `else` con `SetIsOriginAllowed(_ => true).AllowAnyHeader().AllowAnyMethod()` sin credenciales. |
| `src/SGV.Web/Program.cs:22-30` | Modificar | Reemplazar `SecurePolicy = SameAsRequest` por ternario sobre `builder.Environment.IsDevelopment()` → `Always` o `SameAsRequest`. |
| `docs/decisiones-implementacion.md` | Modificar | Nueva sección "Hardening runtime: cookie y CORS por ambiente" con matriz ambiente↔seguridad, env vars `AllowedOrigins__0`, snippet de `UseForwardedHeaders` (doc, NO implementar). |
| `AGENTS.md` | Modificar | Bullet corto en "Decisiones Técnicas que NO conviene romper" referenciando cookie/CORS por ambiente. |
| `tests/SGV.Tests/Api/CorsAllowedOriginsValidationTests.cs` | Crear | 4 tests `[Fact]`: ausente+Production→throw; poblado+Production→arranca; ausente+Development→arranca; búsqueda estática que prohíba la combinación `AllowAnyOrigin().AllowCredentials()`. |
| `tests/SGV.Tests/Web/WebCookieAuthenticationOptionsTests.cs` | Crear | 2 tests `[Fact]`: Production→`SecurePolicy==Always`; Development→`SecurePolicy==SameAsRequest`. Ambos inspeccionan `IOptionsMonitor<CookieAuthenticationOptions>` desde `factory.Services`. |

NO se tocan `SGV.Dominio`, `SGV.Aplicacion`, `SGV.Infraestructura`. Justificación: cookie auth y CORS son decisiones de borde runtime, no reglas de negocio.

## Interfaces / Contracts

No hay nuevas interfaces. Cambios sobre contratos vigentes:

- `AllowedOrigins` (sección de config): requerida fuera de `Development`. Mensaje de error sugerido: `"SGV.Api: la sección de configuración 'AllowedOrigins' es obligatoria fuera del ambiente Development. Configure AllowedOrigins__0, AllowedOrigins__1, ... vía variables de entorno."`
- `SGV.Web` cookie `CookieOptions`: tabla del spec (`HttpOnly=true`, `SameSite=Lax`, `SecurePolicy={Always|SameAsRequest}`).

## Testing Strategy

| Capa | Qué | Cómo |
|------|-----|------|
| Integración API | Fail-loud `AllowedOrigins` en `Production` | `WebApplicationFactory<SGV.Api.Program>().WithWebHostBuilder(b => b.UseEnvironment("Production").ConfigureAppConfiguration(c => c.AddInMemoryCollection(...)))`; `Assert.Throws<InvalidOperationException>(() => factory.CreateClient())`. Reusa patrón de `JwtOptionsTests.cs:30-40`. |
| Integración API | `AllowedOrigins` poblado en `Production` arranca | Mismo factory con `["AllowedOrigins:0"]="https://test.example.com"`; `factory.CreateClient()` no lanza. Inspección opcional de `ICorsPolicyProvider.GetPolicyAsync(null)` para verificar origins. |
| Integración API | `Development` sin origins arranca | `factory.WithWebHostBuilder(b => b.UseEnvironment("Development"))`; usa el placeholder dev; `factory.CreateClient()` no lanza. |
| Estático | Sin `AllowAnyOrigin()`+`AllowCredentials()` combinados | Lee `src/SGV.Api/Program.cs` como string; regex que niegue la coexistencia dentro de un mismo bloque `AddCors(...)`. |
| Integración Web | `CookieSecurePolicy` por ambiente | Factory con `UseEnvironment("Production")`/`"Development"`; resuelve `IOptionsMonitor<CookieAuthenticationOptions>`; assert `Cookie.HttpOnly`, `SameSite`, `SecurePolicy`. |

## Migration / Rollout

Sin migraciones ni cambios de esquema. El fail-loud es la red de seguridad: si el operador despliega sin `AllowedOrigins`, el pod no arranca y el orquestador lo surface. Rollback = revertir el commit; restaura `SecurePolicy=SameAsRequest` y `AllowAnyOrigin().AllowCredentials()`.

## Open Questions

Ninguna bloqueante. Detalles no críticos resueltos por convención vigente del repo:
- Tests parametrizados con `[Theory]` quedan descartados por la decisión del orchestrator (escenarios trivialmente parametrizables → `[Fact]`).
- Origins con slash final ya se mencionan en el doc de la propuesta; no se agrega validación runtime porque el middleware CORS los rechaza solo.