# Design: Cambiar contraseña de usuario logueado

> Change: `2026-07-27-cambiar-contrasena-usuario-logueado` · Issue: #204
> Idioma: español. Strict TDD: true. Modo de persistencia: `hybrid` (filesystem + Engram).

## 1. Resumen del enfoque

Habilitamos un endpoint `[Authorize]` `POST /api/v1/auth/change-password` que usa
`UserManager<SgvIdentityUser>.ChangePasswordAsync` + `UpdateSecurityStampAsync`
para rotar la credencial del usuario autenticado, expuesto vía
`IAuthApiClient.ChangePasswordAsync` (cliente autenticado) y consumido por una
nueva Razor Page `/auth/cambiar-contrasena` que renderiza un formulario
inspirado en `Pages/Auth/ResetPassword.cshtml` y cierra la sesión del propio
usuario al terminar. El rate limit `ChangePassword` (5 req / 15 min) se aplica
**después** de `[Authorize]` para acotar brute force de la contraseña actual
por usuario autenticado, alineado con la convención del `ResetPasswordPolicyName`
vigente en `SGV.Api/Program.cs` (líneas 226-275).

## 2. Arquitectura y capas

```
 Contracts  ──►  Aplicacion  ──►  Infraestructura  ──►  Api  ──►  Web
    ▲                                                                  │
    └──────────────── wire-types (records + constantes) ──────────────┘
```

| Capa | Proyecto | Nuevo | Modificado | Dependencias nuevas |
|---|---|---|---|---|
| Wire-types | `SGV.Contracts` | `ChangePasswordRequest`, enum `ChangePasswordOutcome`, 3 constantes en `AuthApiRoutes` | — | `SGV.Dominio` (nada; records puros) |
| Application | `SGV.Aplicacion` | `IChangePasswordService`, `ChangePasswordService` (no — Infra), `ChangePasswordRequestValidator`, namespace `SGV.Aplicacion.Seguridad.PasswordChange` | — | `SGV.Contracts` (ya referenciado) |
| Infrastructure | `SGV.Infraestructura` | `ChangePasswordService` en `src/SGV.Infraestructura/Seguridad/PasswordChange/` | `DependencyInjection.cs` (+`AddScoped`) | `SGV.Aplicacion`, `Microsoft.AspNetCore.Identity` |
| Composition (API) | `SGV.Api` | — | `AuthController.cs` (+endpoint), `Program.cs` (+rate limit) | (usa `AddInfraestructuraServicios`) |
| Composition (Web) | `SGV.Web` | `Pages/Auth/CambiarContrasena.cshtml(.cs)`, `Pages/Shared/Partials/_Topbar.cshtml` (+ítem) | `SignIn.cshtml` (+bloque `TempData["PasswordChangeMessage"]`), `Integration/Auth/IAuthApiClient.cs` (+método), `AuthApiClient.cs` (+método) | `SGV.Contracts`, `IHttpClientFactory` (ya inyectado) |

Regla Clean Architecture: `SGV.Web` **no** referencia `SGV.Api` (vigente, ver
`AGENTS.md` § "Estructura del Proyecto"). El bridge cookie→JWT
(`ApiBearerTokenHandler`, `src/SGV.Web/Integration/Auth/`) lleva el bearer
token al endpoint sin filtrar el `UserManager` al shell.

## 3. Componentes a crear/modificar

| Path | Tipo | Símbolo principal | Responsabilidad | Dependencias |
|---|---|---|---|---|
| `src/SGV.Contracts/Seguridad/Usuarios/UsuarioContracts.cs` | Modificación | `record ChangePasswordRequest(string CurrentPassword, string NewPassword, string ConfirmPassword)` | Wire-type del body POST. | — |
| `src/SGV.Contracts/Auth/AuthApiRoutes.cs` | Modificación | `ChangePasswordRelative`, `ChangePassword`, `ChangePasswordPolicyName` | Constantes de ruta + nombre de política rate limit. | — |
| `src/SGV.Contracts/Seguridad/Usuarios/UsuarioContracts.cs` | Modificación | `enum ChangePasswordOutcome { Success, InvalidCurrentPassword, ValidationError, RateLimited }` | Resultado discriminado para el cliente + tests. | — |
| `src/SGV.Aplicacion/Seguridad/PasswordChange/IChangePasswordService.cs` | Nuevo | `IChangePasswordService` | Puerto: `ChangePasswordAsync(userId, request, ct)`. | `SGV.Contracts` |
| `src/SGV.Aplicacion/Seguridad/PasswordChange/ChangePasswordRequestValidator.cs` | Nuevo | `ChangePasswordRequestValidator : AbstractValidator<ChangePasswordRequest>` | Política de password espejada de `ResetPasswordRequestValidator`. | `FluentValidation` |
| `src/SGV.Infraestructura/Seguridad/PasswordChange/ChangePasswordService.cs` | Nuevo | `ChangePasswordService : IChangePasswordService` | Orquesta `ChangePasswordAsync` + `UpdateSecurityStampAsync`. | `UserManager<SgvIdentityUser>`, `ILogger<>` |
| `src/SGV.Infraestructura/DependencyInjection.cs` | Modificación | `services.AddScoped<IChangePasswordService, ChangePasswordService>()` | DI (línea ~104, junto a `IPasswordResetService`). | — |
| `src/SGV.Api/Controllers/AuthController.cs` | Modificación | `Task<IActionResult> ChangePassword(ChangePasswordRequest, CancellationToken)` | `[HttpPost(AuthApiRoutes.ChangePasswordRelative)] [Authorize] [EnableRateLimiting(AuthApiRoutes.ChangePasswordPolicyName)]`. | `IChangePasswordService`, `IValidator<ChangePasswordRequest>` |
| `src/SGV.Api/Program.cs` | Modificación | `AddFixedWindowLimiter(AuthApiRoutes.ChangePasswordPolicyName, …)` | Política rate limit 5/15min. Bloque después de línea 256. | — |
| `src/SGV.Web/Integration/Auth/IAuthApiClient.cs` | Modificación | `Task<ChangePasswordOutcome> ChangePasswordAsync(ChangePasswordRequest, CancellationToken)` | Contrato cliente tipado. | `SGV.Contracts` |
| `src/SGV.Web/Integration/Auth/AuthApiClient.cs` | Modificación | Método público + helper `PostAuthenticatedAsync<TRequest,TOutcome>` | POST al endpoint con `httpClient` (autenticado, cubierto por `ApiBearerTokenHandler`). Mapea 400/429. | `HttpClient` |
| `src/SGV.Web/Pages/Auth/CambiarContrasena.cshtml` | Nuevo | `@page "/auth/cambiar-contrasena"` | Vista del formulario con `data-password="bar"`. | `_ViewImports`, `auth-password.js` |
| `src/SGV.Web/Pages/Auth/CambiarContrasena.cshtml.cs` | Nuevo | `CambiarContrasenaModel : PageModel` con `[Authorize]` | GET render + POST → API → `SignOutAsync` + `LocalRedirect("/auth/sign-in")`. | `IAuthApiClient`, `HttpContext.SignOutAsync` |
| `src/SGV.Web/Pages/Shared/Partials/_Topbar.cshtml` | Modificación | Bloque `<a href="/auth/cambiar-contrasena">` con `ti ti-key` | Ítem del dropdown del topbar, antes del form de logout (línea 68 actual). | — |
| `src/SGV.Web/Pages/Auth/SignIn.cshtml` | Modificación | Bloque `@if (TempData["PasswordChangeMessage"] is string …)` | Banner de éxito tras `LocalRedirect`. | — |

## 4. Contratos (Contracts)

```csharp
// src/SGV.Contracts/Seguridad/Usuarios/UsuarioContracts.cs (append)

/// <summary>
/// Request to change the password of the currently authenticated user.
/// <see cref="CurrentPassword"/> MUST be provided (this is not a recovery
/// flow); <see cref="NewPassword"/> and <see cref="ConfirmPassword"/>
/// MUST match and conform to <c>IdentityOptions.Password</c>.
/// </summary>
public sealed record ChangePasswordRequest(
    string CurrentPassword,
    string NewPassword,
    string ConfirmPassword);

/// <summary>
/// Outcome of <c>POST /api/v1/auth/change-password</c>. Mirrors the
/// shape of <see cref="PasswordResetOutcome"/> but with the
/// non-applicable anti-enumeration values collapsed into
/// <see cref="ValidationError"/>.
/// </summary>
public enum ChangePasswordOutcome
{
    Success = 0,
    InvalidCurrentPassword = 1,
    ValidationError = 2,
    RateLimited = 3
}
```

```csharp
// src/SGV.Contracts/Auth/AuthApiRoutes.cs (append)

/// <summary>
/// Relative route for the authenticated password-change endpoint.
/// Marked <c>[Authorize]</c>; see <c>AuthController.ChangePassword</c>
/// in <c>SGV.Api</c>.
/// </summary>
public const string ChangePasswordRelative = "change-password";

/// <summary>
/// Absolute route for the authenticated password-change endpoint.
/// </summary>
public const string ChangePassword = "/" + Base + "/" + ChangePasswordRelative;

/// <summary>
/// Rate-limit policy name for the change-password endpoint. Applied
/// AFTER authentication so the bucket is keyed by the authenticated
/// subject. See <c>AuthController.ChangePassword</c> in <c>SGV.Api</c>.
/// </summary>
public const string ChangePasswordPolicyName = "ChangePassword";
```

## 5. Capa de Aplicación

`src/SGV.Aplicacion/Seguridad/PasswordChange/IChangePasswordService.cs`:

```csharp
public interface IChangePasswordService
{
    Task<ChangePasswordOutcome> ChangePasswordAsync(
        string userId,
        ChangePasswordRequest request,
        CancellationToken cancellationToken = default);
}
```

> **Decisión**: el método recibe `userId` (no `IPrincipal`) para mantener
> al servicio HTTP-agnóstico. El controller lo resuelve con `User.FindFirstValue(ClaimTypes.NameIdentifier)`.

`ChangePasswordRequestValidator` (FluentValidation) espeja
`ResetPasswordRequestValidator` (`src/SGV.Aplicacion/Seguridad/PasswordReset/ResetPasswordRequestValidator.cs:18-42`):

| Campo | Regla | Mensaje |
|---|---|---|
| `CurrentPassword` | `NotEmpty` | "La contraseña actual es obligatoria." |
| `NewPassword` | `NotEmpty` | "La nueva contraseña es obligatoria." |
| `NewPassword` | `MinimumLength(6)` | "La contraseña debe tener al menos 6 caracteres." |
| `NewPassword` | `Matches("[a-z]+")` | "La contraseña debe incluir al menos una letra minúscula." |
| `NewPassword` | `Matches("[A-Z]+")` | "La contraseña debe incluir al menos una letra mayúscula." |
| `NewPassword` | `Matches("[0-9]+")` | "La contraseña debe incluir al menos un dígito." |
| `NewPassword` | `Matches(@"[^a-zA-Z0-9]+")` | "La contraseña debe incluir al menos un símbolo (no alfanumérico)." |
| `ConfirmPassword` | `Equal(r => r.NewPassword)` con `StringComparison.Ordinal` | "La confirmación no coincide con la nueva contraseña." |

> **Decisión**: usamos `Equal` en vez de comparar en el PageModel, para
> consolidar la regla en el validator (mirror de la decisión aplicada
> en `ResetPasswordRequestValidator`). El PageModel mantiene
> `MeetsPasswordPolicy` como **primera barrera cliente** pero la
> validación de igualdad queda en el server (alineado con la
> propuesta).

## 6. Capa de Infraestructura

`src/SGV.Infraestructura/Seguridad/PasswordChange/ChangePasswordService.cs`:

```text
1. ArgumentNullException.ThrowIfNull(request).
2. user = await userManager.FindByIdAsync(userId, ct).
   if user is null: return ChangePasswordOutcome.InvalidCurrentPassword
     (no leak: the controller's [Authorize] already gated the request;
      falling here implies the cookie was tampered or the user was
      hard-deleted between JWT issuance and now).
3. result = await userManager.ChangePasswordAsync(user,
        request.CurrentPassword, request.NewPassword, ct).
4. if !result.Succeeded:
     a. if any error.Code == "PasswordMismatch":
          return ChangePasswordOutcome.InvalidCurrentPassword
     b. else: return ChangePasswordOutcome.ValidationError
        (Identity's policy rejected the new password; validator drift)
5. await userManager.UpdateSecurityStampAsync(user, ct).
   (Failure here is logged but treated as best-effort: even if the
    stamp rotation fails, the password DID change and the user must
    be able to log in again; the next successful login resets the
    stamp naturally via SignInAsync.)
6. logger.LogInformation("Password change succeeded for userId={UserId}.", user.Id).
7. return ChangePasswordOutcome.Success.
```

> **Decisión**: NO bloqueamos el flujo si `UpdateSecurityStampAsync`
> falla. La rotación es un **second line of defense** (la revocación
> inmediata ya está cubierta por el `SignOutAsync` explícito del
> PageModel en `OnPostAsync`). Logueamos el warning con
> `ILogger<ChangePasswordService>`.

Dependencias: `UserManager<SgvIdentityUser>` (ya registrado en
`SGV.Api/Program.cs:130-140` con
`AddIdentityCore<SgvIdentityUser>()`), `ILogger<ChangePasswordService>`.

`DependencyInjection.cs` agrega la línea **después** del registro de
`IPasswordResetService` (línea 103):

```csharp
// Cambio de contraseña para usuario ya autenticado (issue #204).
// Scoped por la misma razón que IPasswordResetService: depende de
// UserManager<SgvIdentityUser>, que es scoped.
services.AddScoped<IChangePasswordService, ChangePasswordService>();
```

## 7. Capa de API

`src/SGV.Api/Controllers/AuthController.cs` agrega (entre `Login` y
`ForgotPassword`, para mantener el orden conceptual: autenticado →
recovery → change-password):

```csharp
[HttpPost(AuthApiRoutes.ChangePasswordRelative)]
[Authorize]
[EnableRateLimiting(AuthApiRoutes.ChangePasswordPolicyName)]
[ProducesResponseType(StatusCodes.Status200OK)]
[ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
[ProducesResponseType(StatusCodes.Status429TooManyRequests)]
public async Task<IActionResult> ChangePassword(
    ChangePasswordRequest request,
    CancellationToken cancellationToken,
    [FromServices] IValidator<ChangePasswordRequest> validator,
    [FromServices] IChangePasswordService changePasswordService)
{
    if (request is null)
    {
        return BadRequest(new { mensaje = "El cuerpo de la solicitud es obligatorio." });
    }

    var validation = await validator
        .ValidateAsync(request, cancellationToken)
        .ConfigureAwait(false);
    if (!validation.IsValid)
    {
        foreach (var error in validation.Errors)
        {
            ModelState.AddModelError(error.PropertyName, error.ErrorMessage);
        }
        return ValidationProblem(ModelState);
    }

    var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)
        ?? User.FindFirstValue(JwtRegisteredClaimNames.Sub);
    if (string.IsNullOrWhiteSpace(userId))
    {
        // [Authorize] ya garantizó identidad, pero defensa en profundidad.
        return Unauthorized();
    }

    var outcome = await changePasswordService
        .ChangePasswordAsync(userId, request, cancellationToken)
        .ConfigureAwait(false);

    return outcome switch
    {
        ChangePasswordOutcome.Success =>
            Ok(new { mensaje = "Tu contraseña fue actualizada." }),
        ChangePasswordOutcome.InvalidCurrentPassword =>
            BadRequest(new { mensaje = "La contraseña actual no es correcta." }),
        ChangePasswordOutcome.ValidationError =>
            BadRequest(new { mensaje = "La nueva contraseña no cumple la política de seguridad." }),
        ChangePasswordOutcome.RateLimited =>
            StatusCode(StatusCodes.Status429TooManyRequests),
        _ => StatusCode(StatusCodes.Status500InternalServerError)
    };
}
```

> **Decisión**: mensajes de error diferenciados
> (`InvalidCurrentPassword` vs. `ValidationError`) **NO** leak información
> porque el endpoint ya pasó `[Authorize]`: el cliente conoce su propia
> identidad, así que distinguir "contraseña actual mala" de "contraseña
> nueva mala" es UX legítimo y no rompe anti-enumeración (que sólo
> aplica a endpoints anónimos como `ForgotPassword`).

El constructor del controller pasa de 5 a 7 dependencias; se documenta en
el comentario XML del nuevo endpoint el porqué de cada nueva
inyección.

## 8. Capa de Integración Web

`IAuthApiClient.cs` agrega:

```csharp
/// <summary>
/// Changes the password of the currently authenticated user. Uses
/// the AUTHENTICATED HTTP client pipeline (with <c>ApiBearerTokenHandler</c>);
/// calling this from an anonymous context yields a 401 from the API.
/// </summary>
Task<ChangePasswordOutcome> ChangePasswordAsync(
    ChangePasswordRequest request,
    CancellationToken cancellationToken = default);
```

`AuthApiClient.cs` implementa usando `httpClient` (no
`anonymousHttpClient`) — idéntico patrón al de `LoginAsync` (línea 41):

```csharp
/// <inheritdoc />
public async Task<ChangePasswordOutcome> ChangePasswordAsync(
    ChangePasswordRequest request,
    CancellationToken cancellationToken = default)
{
    cancellationToken.ThrowIfCancellationRequested();

    using var response = await httpClient.PostAsJsonAsync(
        AuthApiRoutes.ChangePassword,
        request,
        cancellationToken);

    return response.StatusCode switch
    {
        HttpStatusCode.TooManyRequests => ChangePasswordOutcome.RateLimited,
        HttpStatusCode.BadRequest      => ChangePasswordOutcome.InvalidCurrentPassword,
        _ when response.IsSuccessStatusCode
                                        => ChangePasswordOutcome.Success,
        _                               => throw new HttpRequestException(
                                                $"Change password returned {(int)response.StatusCode}.",
                                                statusCode: response.StatusCode)
    };
}
```

> **Decisión**: usamos `httpClient` (pipeline con
> `ApiBearerTokenHandler`, registrado en `SGV.Web/Program.cs:122-132`)
> porque la API exige `[Authorize]`. Si el Web está autenticado por
> cookie y la cookie trae el access_token (vía `AuthTokenNames.AccessToken`),
> el handler lo inyecta automáticamente. Si el contexto es anónimo
> (background, host lifetime), el handler pasa el request sin bearer
> y la API responde 401 — propagamos como `HttpRequestException` con
> `StatusCode=401` para que el PageModel lo distinga.

## 9. Capa de Web Pages

`src/SGV.Web/Pages/Auth/CambiarContrasena.cshtml.cs`:

```csharp
[Authorize]
[AutoValidateAntiforgeryToken]
public sealed class CambiarContrasenaModel(
    IAuthApiClient authApiClient,
    ILogger<CambiarContrasenaModel> logger) : PageModel
{
    private const string SuccessMessage = "Tu contraseña se cambió correctamente. Volvé a iniciar sesión.";
    private const string TransportMessage = "No se pudo conectar con el servidor. Verificá tu conexión y volvé a intentar.";
    private const string TimeoutMessage = "El servidor tardó demasiado en responder. Volvé a intentar en unos segundos.";
    private const string RateLimitMessage = "Hiciste demasiados intentos. Esperá unos minutos antes de volver a intentarlo.";
    private const string InvalidCurrentMessage = "La contraseña actual no es correcta.";

    [BindProperty]
    public InputModel Input { get; set; } = new();

    public void OnGet() { /* render del formulario */ }

    public async Task<IActionResult> OnPostAsync(CancellationToken cancellationToken)
    {
        // Mismatch cliente (primera barrera — UX inmediata)
        if (!string.Equals(Input.NewPassword, Input.ConfirmPassword, StringComparison.Ordinal))
        {
            ModelState.AddModelError(
                $"{nameof(Input)}.{nameof(InputModel.ConfirmPassword)}",
                "Las contraseñas no coinciden.");
        }

        if (!string.IsNullOrEmpty(Input.NewPassword) && !MeetsPasswordPolicy(Input.NewPassword))
        {
            ModelState.AddModelError(
                $"{nameof(Input)}.{nameof(InputModel.NewPassword)}",
                "La contraseña debe tener al menos 6 caracteres, una minúscula, una mayúscula, un número y un símbolo.");
        }

        if (!ModelState.IsValid)
        {
            return Page();
        }

        try
        {
            var outcome = await authApiClient.ChangePasswordAsync(
                new ChangePasswordRequest(
                    Input.CurrentPassword,
                    Input.NewPassword,
                    Input.ConfirmPassword),
                cancellationToken);

            switch (outcome)
            {
                case ChangePasswordOutcome.Success:
                    // PRG: cerramos la cookie local (rotación de stamp en
                    // la API ya invalidó el JWT y, en el siguiente request,
                    // el CookiePrincipalRevalidator rechazará la cookie
                    // porque la cookie cookie-store no se borra sola).
                    await HttpContext.SignOutAsync(
                        CookieAuthenticationDefaults.AuthenticationScheme);
                    TempData["PasswordChangeMessage"] = SuccessMessage;
                    return LocalRedirect("/auth/sign-in");

                case ChangePasswordOutcome.InvalidCurrentPassword:
                    ModelState.AddModelError(
                        $"{nameof(Input)}.{nameof(InputModel.CurrentPassword)}",
                        InvalidCurrentMessage);
                    return Page();

                case ChangePasswordOutcome.RateLimited:
                    ModelState.AddModelError(string.Empty, RateLimitMessage);
                    return Page();

                case ChangePasswordOutcome.ValidationError:
                    ModelState.AddModelError(
                        $"{nameof(Input)}.{nameof(InputModel.NewPassword)}",
                        "La nueva contraseña no cumple la política de seguridad.");
                    return Page();
            }
        }
        catch (HttpRequestException ex) when (ex.StatusCode == System.Net.HttpStatusCode.Unauthorized)
        {
            // La cookie se venció mientras el usuario escribía.
            return LocalRedirect("/auth/sign-in");
        }
        catch (HttpRequestException)
        {
            logger.LogWarning("Change password API request failed.");
            ModelState.AddModelError(string.Empty, TransportMessage);
        }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            logger.LogWarning("Change password API request timed out.");
            ModelState.AddModelError(string.Empty, TimeoutMessage);
        }

        return Page();
    }

    /// <summary>
    /// Mirror cliente de <c>IdentityOptions.Password</c> en
    /// <c>SGV.Api/Program.cs</c>. MUST stay in sync con
    /// <c>ChangePasswordRequestValidator</c> y con el
    /// <c>RequiredLength=6</c> + lower/upper/digit/symbol de
    /// <c>AddIdentityCore</c>.
    /// </summary>
    private static bool MeetsPasswordPolicy(string password)
        => password.Length >= 6
           && password.Any(char.IsLower)
           && password.Any(char.IsUpper)
           && password.Any(char.IsDigit)
           && password.Any(c => !char.IsLetterOrDigit(c));

    public sealed class InputModel
    {
        [Required(ErrorMessage = "La contraseña actual es obligatoria.")]
        [DataType(DataType.Password)]
        public string CurrentPassword { get; set; } = string.Empty;

        [Required(ErrorMessage = "La nueva contraseña es obligatoria.")]
        [DataType(DataType.Password)]
        public string NewPassword { get; set; } = string.Empty;

        [Required(ErrorMessage = "La confirmación de contraseña es obligatoria.")]
        [DataType(DataType.Password)]
        public string ConfirmPassword { get; set; } = string.Empty;
    }
}
```

`src/SGV.Web/Pages/Auth/CambiarContrasena.cshtml` es mirror de
`Pages/Auth/ResetPassword.cshtml` con estos cambios:

- `@page "/auth/cambiar-contrasena"`
- Quita los `asp-for="UserId"` y `asp-for="Token"` (hidden).
- Agrega el bloque `data-password="bar"` para `Input.NewPassword`.
- Cambia el `<h4>` a "Cambiar contraseña" y el copy del `<p>` a
  "Tu sesión se cerrará al confirmar para que vuelvas a iniciar sesión
  con la nueva contraseña."
- Reutiliza el bloque `@section Scripts` con jQuery validate +
  `auth-password.js`.

## 10. UI y dropdown del topbar

Diff de `src/SGV.Web/Pages/Shared/Partials/_Topbar.cshtml` (líneas 64-73
actuales):

```diff
 <div class="dropdown-menu dropdown-menu-end">
 <div class="dropdown-header noti-title">
 <h6 class="text-overflow m-0">¡Bienvenido!</h6>
 </div>
+<a class="dropdown-item" href="/auth/cambiar-contrasena">
+<i class="ti ti-key me-1 fs-lg align-middle"></i>
+<span class="align-middle">Cambiar Contraseña</span>
+</a>
 <form asp-page="/Auth/Logout" method="post" class="dropdown-item p-0">
 <button type="submit" class="dropdown-item text-danger fw-semibold">
 <i class="ti ti-logout me-1 fs-lg align-middle"></i>
 <span class="align-middle">Cerrar Sesión</span>
 </button>
 </form>
 </div>
```

> **Decisión**: el ítem va **antes** del form de logout (línea 68
> actual) para que el usuario vea la acción neutra antes que la
> destructiva. Ícono `ti ti-key` (Tabler Icons ya cargado en
> Inspinia); fallback documentado en el proposal: `ti ti-lock` si
> falla el bundling.

## 11. Rate limiting

`src/SGV.Api/Program.cs` agrega **después** de la política
`SetupApiRoutes.SetupPolicyName` (línea 254) y antes de
`RejectionStatusCode` (línea 257):

```csharp
// Issue #204: cambio de contraseña del usuario ya autenticado.
// 5 req / 15 min, alineado con ResetPasswordPolicyName, pero
// aplicado DESPUÉS de [Authorize]: el bucket se keyed por el
// subject autenticado, no por IP. Ver AuthController.ChangePassword.
options.AddFixedWindowLimiter(AuthApiRoutes.ChangePasswordPolicyName, policy =>
{
    policy.PermitLimit = 5;
    policy.Window = TimeSpan.FromMinutes(15);
    policy.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
    policy.QueueLimit = 0;
});
```

El `OnRejected` global (líneas 259-274) ya cubre el `Retry-After`; no
se necesita código nuevo.

> **Decisión**: posición del middleware en el pipeline **no cambia**.
> `app.UseRateLimiter()` (línea 334) corre antes de `UseAuthentication()`
> y `UseAuthorization()` (líneas 336, 376), pero `[Authorize]` ya
> cortocircuita anónimos al endpoint `ChangePassword` antes de
> alcanzar el rate limiter. El `RejectionStatusCode = 429` se aplica
> sólo a requests autenticados que ya pasaron `[Authorize]`.

## 12. Manejo de errores

| `ChangePasswordOutcome` | HTTP | TempData / `ModelState` | Mensaje al usuario |
|---|---|---|---|
| `Success` | `200` | `TempData["PasswordChangeMessage"]` | "Tu contraseña se cambió correctamente. Volvé a iniciar sesión." |
| `InvalidCurrentPassword` | `400` | `ModelState[Input.CurrentPassword]` | "La contraseña actual no es correcta." |
| `ValidationError` | `400` | `ModelState[Input.NewPassword]` | "La nueva contraseña no cumple la política de seguridad." |
| `RateLimited` | `429` | `ModelState[string.Empty]` | "Hiciste demasiados intentos. Esperá unos minutos antes de volver a intentarlo." |
| `HttpRequestException(401)` | `LocalRedirect("/auth/sign-in")` | — | (sesión venció durante el flujo) |
| `HttpRequestException` (genérico) | `200` (re-render) | `ModelState[string.Empty]` | "No se pudo conectar con el servidor. Verificá tu conexión y volvé a intentar." |
| `TaskCanceledException` | `200` (re-render) | `ModelState[string.Empty]` | "El servidor tardó demasiado en responder. Volvé a intentar en unos segundos." |
| `ValidationProblemDetails` del validator | `400` | `ModelState` por campo | Mensaje exacto del `ChangePasswordRequestValidator` |

## 13. Estrategia de pruebas

Espejo 1:1 de los archivos vigentes. Cobertura mínima viable
(`strict_tdd: true`), sin tests redundantes.

| Capa | Archivo | Cobertura |
|---|---|---|
| Aplicación (unit) | `tests/SGV.Tests/Aplicacion/Seguridad/ChangePasswordRequestValidatorTests.cs` | `NotEmpty` por campo, política de password (6/lower/upper/digit/symbol), `Equal(ConfirmPassword, NewPassword)`. **1 test parametrizado** con `Theory`+`InlineData` que cubra el happy path + 4 fallas por dimensión. |
| API (integración) | `tests/SGV.Tests/Api/AuthControllerChangePasswordTests.cs` (espejo de `AuthControllerPasswordResetTests.cs`) | 401 sin auth; 200 con stamp rotado (usa `MySqlFact` para verificar `SecurityStamp`); 400 con `CurrentPassword` inválida; 400 con política débil; 429 al sexto request. |
| Web integration | `tests/SGV.Tests/Web/AuthApiClientChangePasswordTests.cs` (espejo de `AuthApiClientPasswordResetTests.cs`) | POST al endpoint con `Authorization: Bearer` (assert sobre `LastAuthorization`), body `ChangePasswordRequest` correcto, 400→`InvalidCurrentPassword`, 429→`RateLimited`, cancelación del token. |
| Web razor | `tests/SGV.Tests/Web/CambiarContrasenaPageTests.cs` (espejo de `ResetPasswordPageTests.cs`) | GET anónimo redirige a sign-in; GET autenticado renderiza form con `data-password="bar"`; POST exitoso hace `SignOutAsync` + redirect a sign-in + `TempData`; POST con `CurrentPassword` inválida muestra error sin revelar detalles; POST con policy débil 400; POST con 429 muestra mensaje. |
| Web shell smoke | `tests/SGV.Tests/Web/WebShellSmokeTests.cs` (extender) | Test que verifica que el dropdown del topbar autenticado contiene el texto "Cambiar Contraseña" y el link `/auth/cambiar-contrasena`. |

Patrón de fake: `FakeChangePasswordService` (mirror de
`FakePasswordResetService` en `tests/SGV.Tests/Api/`) — expone
`Func<…, ChangePasswordOutcome>` para que cada test invoque la rama
que necesita.

## 14. Estrategia de datos / migraciones

**No requiere migración de BD.** `SecurityStamp` ya existe en la tabla
`AspNetUsers` (heredada de `IdentityUser`, configurada por
`AddEntityFrameworkStores<SgvDbContext>()` en `Program.cs:139`). La
operación `UpdateSecurityStampAsync` modifica el valor de la columna
`SecurityStamp` sin requerir cambio de esquema.

`docs/migracion-inicial-sgv.sql` se regenera al final con
`dotnet ef migrations script --idempotent` para mantener la
concordancia, pero **no se crea ninguna migración nueva** de EF
(`migrations add`).

## 15. Compatibilidad y rollback

Pasos de rollback (todos unitarios, no destructivos):

1. **Topbar**: borrar el bloque `<a href="/auth/cambiar-contrasena">`
   agregado en `_Topbar.cshtml` (líneas 71-74 del diff). 1 commit
   independiente.
2. **SignIn banner**: borrar el bloque `@if (TempData["PasswordChangeMessage"] is string …)`
   en `SignIn.cshtml`. 1 commit independiente.
3. **Endpoint + DI + cliente**: remover el endpoint
   `AuthController.ChangePassword`, el registro en
   `DependencyInjection.cs`, el método en `IAuthApiClient` /
   `AuthApiClient`, y el `AddFixedWindowLimiter` en `Program.cs`.
   1 commit independiente.
4. **Razor Page**: borrar `Pages/Auth/CambiarContrasena.cshtml(.cs)`.
   1 commit independiente.
5. **Contracts**: remover `ChangePasswordRequest`, las 3 constantes en
   `AuthApiRoutes` y `ChangePasswordOutcome` (con cuidado: si quedan
   tests que los referencien, primero esos tests). 1 commit
   independiente.

> **Decisión**: el rollback es **forward-compatible**. Ningún cambio
> borra tipos ya en uso por código pre-existente (sólo agrega). Si
> el PR se mergea y luego se decide retirar, los pasos 1-5 se pueden
> ejecutar en cualquier orden sin invalidar la suite vigente. La
> Razor Page y el endpoint quedan como "código muerto" durante el
> período de transición; ningún script automático los elimina.

## 16. Riesgos identificados

Heredados del proposal + nuevos detectados durante la lectura del
código:

| # | Riesgo | Probabilidad | Mitigación | Detectado en |
|---|---|---|---|---|
| R1 | Race `ChangePasswordAsync` ↔ `UpdateSecurityStampAsync` deja cookie zombie | Baja | `SignOutAsync` explícito en `CambiarContrasenaModel.OnPostAsync`; `CookiePrincipalRevalidator` rechaza si el stamp no matchea. | Proposal |
| R2 | Endpoint sin rate limit expuesto a brute force | Media | Política `ChangePassword` 5/15min, aplicada después de `[Authorize]`. | Proposal |
| R3 | `UpdateSecurityStampAsync` no invocado → sesiones sobreviven | Baja | Test `[MySqlFact]` verifica cambio de `SecurityStamp` tras POST exitoso. | Proposal + diseño |
| R4 | `ConfirmPassword` no se valida en cliente | Baja | Validator server con `Equal`; `MeetsPasswordPolicy` en PageModel como primera barrera. | Proposal |
| R5 | Ícono `ti ti-key` no disponible en el bundle | Baja | Fallback `ti ti-lock` (ya usado en `Pages/Seguridad/Usuarios`). | Proposal |
| **R6** | **`HttpRequestException` con `StatusCode=401` cuando la cookie venció durante el flujo (mientras el usuario escribe la nueva contraseña)** | Media | Catch explícito en `OnPostAsync` que hace `LocalRedirect("/auth/sign-in")` sin re-renderizar el form (no tiene sentido pedirle que reescriba las contraseñas si su sesión ya venció). | Detectado durante diseño (línea 12 de `CambiarContrasenaModel`) |
| **R7** | **`UserManager.ChangePasswordAsync` con `NewPassword` que pasa el validator server pero falla la policy de Identity por drift entre `ChangePasswordRequestValidator` y `IdentityOptions.Password`** | Baja | El branch "otros errores" en `ChangePasswordService` (paso 4b del pseudocódigo) devuelve `ValidationError`; el controller mapea a `400` con mensaje genérico; el test `[Fact] DriftValidatorVsIdentity` cubre el caso. | Detectado durante diseño (comparación con `PasswordResetService.cs:107-124`) |
| **R8** | **El usuario llega a `/auth/cambiar-contrasena` con un JWT vencido pero cookie todavía vigente** | Baja | `app.UseAuthentication()` rechaza el bearer (revalidator); `CookiePrincipalRevalidator` rechaza la cookie si el subject no existe en la API; el endpoint queda con 401 → el Web redirige a sign-in. | Detectado durante diseño (lectura de `Program.cs:330-375` y `CookiePrincipalRevalidator.cs:101-130`) |
| **R9** | **Bug latente en la rama `UpdateSecurityStampAsync` que falla** (no la tratamos como fatal) | Baja | Logged como `LogWarning`; documentado en el comentario XML de `ChangePasswordService`; ningún test assertivo sobre el éxito del stamp rotation (sólo verifica que se invoca). | Detectado durante diseño (paso 5 del pseudocódigo) |
| **R10** | **`SignIn.cshtml` recibe TempData de dos features distintos (`PasswordResetMessage` y `PasswordChangeMessage`); un merge descuidado podría romper el primero** | Baja | El bloque nuevo va en `SignIn.cshtml` después del bloque existente de `PasswordResetMessage` (líneas 29-35 actuales); comentario XML aclara que es un segundo banner independiente. | Detectado durante diseño (lectura de `SignIn.cshtml`) |

## 17. Plan de commits (work units)

Cada commit = un cambio reversible de forma independiente. Máximo ~2h
de trabajo, suite verde en cada paso. Se ejecutan en este orden para
que cada commit introduzca valor testeable y el revert sea surgical.

| WU | Commit | Archivos | Tests asociados | Tiempo |
|---|---|---|---|---|
| WU-1 | `feat: add ChangePasswordRequest + AuthApiRoutes constants` | `src/SGV.Contracts/Seguridad/Usuarios/UsuarioContracts.cs`, `src/SGV.Contracts/Auth/AuthApiRoutes.cs` | (compila; sin tests nuevos — los tests de validator vienen en WU-2) | 30 min |
| WU-2 | `feat: add IChangePasswordService + ChangePasswordRequestValidator + ChangePasswordOutcome` | `src/SGV.Aplicacion/Seguridad/PasswordChange/IChangePasswordService.cs`, `src/SGV.Aplicacion/Seguridad/PasswordChange/ChangePasswordRequestValidator.cs` | `tests/SGV.Tests/Aplicacion/Seguridad/ChangePasswordRequestValidatorTests.cs` (1 Theory + InlineData) | 1h |
| WU-3 | `feat: add ChangePasswordService in Infrastructure + DI registration` | `src/SGV.Infraestructura/Seguridad/PasswordChange/ChangePasswordService.cs`, `src/SGV.Infraestructura/DependencyInjection.cs` | (compila; tests de integración en WU-5) | 1.5h |
| WU-4 | `feat: add ChangePasswordPolicy rate limiter in Program.cs` | `src/SGV.Api/Program.cs` | (verificación manual: `dotnet build` confirma compile; el primer test de 429 viene en WU-5) | 15 min |
| WU-5 | `feat: add AuthController.ChangePassword endpoint [Authorize]` | `src/SGV.Api/Controllers/AuthController.cs` | `tests/SGV.Tests/Api/AuthControllerChangePasswordTests.cs` (5 tests: 401, 200+stamp, 400-current, 400-policy, 429) | 1.5h |
| WU-6 | `feat: add IAuthApiClient.ChangePasswordAsync + AuthApiClient implementation` | `src/SGV.Web/Integration/Auth/IAuthApiClient.cs`, `src/SGV.Web/Integration/Auth/AuthApiClient.cs` | `tests/SGV.Tests/Web/AuthApiClientChangePasswordTests.cs` (3 tests: happy, 400, 429) | 1h |
| WU-7 | `feat: add CambiarContrasena Razor Page with [Authorize]` | `src/SGV.Web/Pages/Auth/CambiarContrasena.cshtml`, `src/SGV.Web/Pages/Auth/CambiarContrasena.cshtml.cs` | `tests/SGV.Tests/Web/CambiarContrasenaPageTests.cs` (5 tests: get-anon, get-auth, post-success, post-current-invalid, post-429) | 2h |
| WU-8 | `feat: add 'Cambiar Contraseña' item in topbar dropdown` | `src/SGV.Web/Pages/Shared/Partials/_Topbar.cshtml` | Extender `tests/SGV.Tests/Web/WebShellSmokeTests.cs` con 1 test (assert markup) | 30 min |
| WU-9 | `feat: render PasswordChangeMessage banner in SignIn page` | `src/SGV.Web/Pages/Auth/SignIn.cshtml` | (verificación manual: e2e visual del flujo + smoke test de WU-7) | 15 min |
| WU-10 | `docs: regenerate docs/migracion-inicial-sgv.sql (no schema changes)` | `docs/migracion-inicial-sgv.sql` | `dotnet ef migrations script --idempotent` debe producir archivo byte-equivalente al vigente. | 15 min |

Total: **~9h netas**, repartibles en 2-3 sesiones.

> **Decisión**: WU-5 (endpoint) va **antes** que WU-6/WU-7 (cliente +
> page) porque el endpoint es lo que define el contrato HTTP y los
> tests de integración de la API (con `FakeChangePasswordService` y
> `MySqlFact` para el stamp) son la **primera barrera de regresión**.
> Si la API retrocede, los tests del cliente Web y del page model
> detectan la falla en CI; si el Web rompe pero la API está OK, sólo
> falla la suite web.

> **Decisión**: el ítem del topbar (WU-8) va al final porque su
> rollback (borrar 4 líneas) es trivial y porque su comportamiento
> depende de que el endpoint WU-5 esté operativo. Un ítem del topbar
> que apunta a un endpoint 404 sería peor que no tener el ítem.
