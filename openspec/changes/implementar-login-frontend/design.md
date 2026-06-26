# Design: Implementar login frontend

## Technical Approach

`SGV.Web` seguirá siendo una app Razor Pages server-side y consumirá `SGV.Api` mediante un cliente tipado. El login hará `POST` a `api/v1/auth/login`, guardará el JWT solo en una cookie de autenticación `HttpOnly` del servidor web y reconstruirá el `ClaimsPrincipal` desde los claims del token para proteger el shell. El dashboard inicial seguirá siendo `Pages/Index` y pasará a requerir autenticación.

## Architecture Decisions

### Decision: sesión web con cookie, no JWT en scripts

| Opción | Tradeoff | Decisión |
|---|---|---|
| Guardar JWT en `localStorage/sessionStorage` | Simple, pero expone el token a JS/XSS | No |
| Reenviar JWT al navegador en cookie no controlada por web | Mezcla responsabilidades y dificulta uso server-side | No |
| `SGV.Web` guarda el JWT dentro del ticket de cookie autenticada (`HttpOnly`) y usa claims locales | Protege el token de scripts y permite futuras llamadas server-to-server | Sí |

**Rationale**: el repo ya usa Razor Pages; la UI no necesita acceso directo al token. El JWT queda disponible solo para código servidor vía `GetTokenAsync`, no para scripts del navegador.

### Decision: rutas compartidas desde `SGV.Api`, resolución absoluta en `SGV.Web`

| Opción | Tradeoff | Decisión |
|---|---|---|
| Literales en cada `PageModel` | Rápido, pero dispersa rutas | No |
| Clase pública de rutas en `SGV.Api` + resolvedor web con `BaseUrl` | Mantiene una sola fuente para paths y separa host/base address | Sí |
| Duplicar DTOs/rutas en `SGV.Web` | Más acoplamiento accidental y drift | No |

**Rationale**: `SGV.Web` debe referenciar `SGV.Api`, pero solo para contratos de integración (`LoginRequest`, `LoginResponse`, constantes de rutas), no para lógica HTTP ni middleware.

## Data Flow

```text
GET /auth/sign-in
Browser -> Razor Page pública -> _AuthLayout

POST /auth/sign-in
Browser -> SignInModel -> IAuthApiClient -> SGV.Api /api/v1/auth/login
                                   <- LoginResponse(JWT)
        -> Cookie auth ticket (claims + token) -> Redirect /

GET /
Browser -> Cookie auth middleware -> IndexModel [Authorize] -> dashboard vacío

POST /auth/logout
Browser -> Logout handler -> SignOutAsync(cookie) -> Redirect /auth/sign-in
```

## File Changes

| File | Action | Description |
|---|---|---|
| `src/SGV.Web/SGV.Web.csproj` | Modify | Referencia a `SGV.Api` y paquete de cookie auth si falta. |
| `src/SGV.Web/Program.cs` | Modify | Registrar cookie auth, authorization, options del API, `HttpClient` tipado y pipeline `UseAuthentication/UseAuthorization`. |
| `src/SGV.Web/appsettings.json` | Modify | Agregar `SgvApi:BaseUrl`. |
| `src/SGV.Web/appsettings.Development.json` | Modify | Base URL local del API. |
| `src/SGV.Web/Pages/Auth/SignIn.cshtml` | Create | Vista basada en Inspinia `Auth/SignIn`, sin forgot/register. |
| `src/SGV.Web/Pages/Auth/SignIn.cshtml.cs` | Create | `OnGet` + `OnPostAsync`, validación, error de autenticación y redirección. |
| `src/SGV.Web/Pages/Auth/Logout.cshtml.cs` | Create | Handler POST-only para cerrar sesión. |
| `src/SGV.Web/Pages/Auth/_ViewStart.cshtml` | Create | Usar layout auth. |
| `src/SGV.Web/Pages/Shared/_AuthLayout.cshtml` | Create | Layout sin topbar/sidenav, reutilizando head/scripts compartidos. |
| `src/SGV.Web/Pages/Index.cshtml(.cs)` | Modify | Convertir en dashboard vacío protegido. |
| `src/SGV.Web/Pages/Shared/Partials/_Topbar.cshtml` | Modify | Agregar acción visible de logout autenticado. |
| `src/SGV.Api/Controllers/AuthController.cs` | Modify | Reusar constantes de rutas públicas. |
| `src/SGV.Api/Contracts/AuthApiRoutes.cs` | Create | Fuente única para `Base`, `LoginRelative`, `Login`. |
| `src/SGV.Web/Integration/Auth/AuthApiClient.cs` | Create | Cliente tipado para login. |
| `src/SGV.Web/Integration/Auth/SgvApiOptions.cs` | Create | Configuración de `BaseUrl`. |
| `tests/SGV.Tests/Web/SgvWebApplicationFactory.cs` | Modify | Permitir override de servicios/`HttpMessageHandler` para pruebas del cliente API. |
| `tests/SGV.Tests/Web/WebAuthenticationTests.cs` | Create | Cobertura del flujo login/logout/redirecciones. |
| `tests/SGV.Tests/Web/WebShellSmokeTests.cs` | Modify | Ajustar asserts al shell autenticado y nueva ruta pública de login. |

## Interfaces / Contracts

```csharp
public static class AuthApiRoutes
{
    public const string Base = "api/v1/auth";
    public const string LoginRelative = "login";
    public const string Login = "/" + Base + "/" + LoginRelative;
}

public sealed class SgvApiOptions
{
    public const string SectionName = "SgvApi";
    public string BaseUrl { get; set; } = string.Empty;
}
```

## Testing Strategy

| Layer | What to Test | Approach |
|---|---|---|
| API integration | `POST /api/v1/auth/login` sigue devolviendo `LoginResponse`/401 | Mantener `tests/SGV.Tests/Api/AuthControllerTests.cs`. |
| Web integration | GET login público, POST válido redirige a `/`, POST inválido muestra error, GET `/` anónimo redirige, logout invalida cookie | `WebApplicationFactory<SGV.Web.Program>` con `HttpMessageHandler` fake para el API. |
| Web smoke | Login sin shell chrome y dashboard autenticado con logout visible | Extender `WebShellSmokeTests` + nuevo `WebAuthenticationTests`. |

## Migration / Rollout

No migration required. Requiere configuración de `SgvApi:BaseUrl` en despliegues web.

## Open Questions

- [ ] Confirmar el texto final del mensaje de error de autenticación para UX.
- [ ] Evaluar en un cambio futuro un `DelegatingHandler` común para adjuntar el JWT almacenado en cookie a otros clientes de `SGV.Api`.
