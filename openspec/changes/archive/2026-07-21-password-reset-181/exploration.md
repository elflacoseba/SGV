## Exploration: Password Reset (issue #181)

### Current State

SGV hoy no tiene ningún mecanismo de reseteo de contraseña. El sistema actual:

- **Identity**: `AddIdentityCore<SgvIdentityUser>` configurado en `src/SGV.Api/Program.cs` (líneas 111-121) con política de passwords (`RequireDigit=true`, `RequireLowercase=true`, `RequireUppercase=true`, `RequireNonAlphanumeric=true`, `RequiredLength=6`). **Falta `AddDefaultTokenProviders()`** — sin esto, `UserManager.GeneratePasswordResetTokenAsync()` lanza `InvalidOperationException` porque no hay ningún `TokenProvider` registrado.
- **AuthController**: solo tiene `Login`. No hay endpoints `forgot-password` ni `reset-password`.
- **AuthApiRoutes**: solo define `Login`. Sin constantes para rutas de reseteo.
- **IAuthServicio** (`SGV.Aplicacion/Seguridad/Usuarios/UsuarioContracts.cs`): solo declara `LoginAsync`. No hay interfaz de reseteo.
- **AuthServicio** (`SGV.Infraestructura/Seguridad/AuthServicio.cs`): solo implementa `LoginAsync`.
- **AuthApiClient / IAuthApiClient** (`SGV.Web/Integration/Auth/`): solo exponen `LoginAsync`.
- **SignIn.cshtml**: form de login sin enlace "Forgot Password?" — hay que agregarlo.
- **FallbackPolicy global** (`RequireAuthenticatedUser()`): todos los endpoints nuevos de reseteo necesitan `[AllowAnonymous]` explícito.
- **Rate limiting**: no existe en el codebase — hay que agregar `Microsoft.AspNetCore.RateLimiting`.
- **IEmailSender**: no hay implementación. Identity lo requiere para el envío de tokens de reseteo.
- **Configuración SMTP**: no existe. Hay que crear una sección `Smtp` en configuración de API.
- **Inspinia template**: `ResetPass.cshtml` (forgot form) y `NewPass.cshtml` (new password form + strength widget) existen como stubs en `InspinaTemplate/Inspinia/Pages/Auth/`. El widget `auth-password.js` ya tiene la lógica de barras de fortaleza (`data-password="bar"`).

### Affected Areas

#### API Layer (`SGV.Api`)

| Archivo | Por qué está afectado |
|---------|----------------------|
| `src/SGV.Api/Program.cs` (líneas 111-121) | **Falta `AddDefaultTokenProviders()`** después de `AddIdentityCore`. Hay que agregarlo para que Identity genere tokens de reseteo. También hay que agregar `options.Tokens.PasswordResetTokenLifespan = TimeSpan.FromHours(1)`. **Y** registrar el middleware `AddRateLimiter` con las políticas de fixed window. |
| `src/SGV.Api/Program.cs` | Hay que registrar `SmtpOptions` con `BindConfiguration` + `ValidateOnStart` (fail-loud fuera de Development). |
| `src/SGV.Api/Program.cs` | Hay que registrar `IEmailSender` en DI (la implementación concreta en Infraestructura). |
| `src/SGV.Api/Controllers/AuthController.cs` | Agregar endpoints `ForgotPassword` (`[AllowAnonymous]`) y `ResetPassword` (`[AllowAnonymous]`) que delegan en un nuevo `IPasswordResetService`. |
| `src/SGV.Api/Program.cs` | No existe `appsettings.json` en API (solo `appsettings.Development.json`). La config SMTP probablemente va vía user-secrets/env-var. Hay que crear el archivo o agregar la sección donde corresponda. |

#### Contracts Layer (`SGV.Contracts`)

| Archivo | Por qué está afectado |
|---------|----------------------|
| `src/SGV.Contracts/Auth/AuthApiRoutes.cs` | Agregar `ForgotPasswordRelative`, `ForgotPassword`, `ResetPasswordRelative`, `ResetPassword`. |
| `src/SGV.Contracts/Seguridad/Usuarios/UsuarioContracts.cs` | Agregar records `ForgotPasswordRequest` (con `UserNameOrEmail`) y `ResetPasswordRequest` (con `UserId`, `Token`, `NewPassword`). |

#### Application Layer (`SGV.Aplicacion`)

| Archivo | Por qué está afectado |
|---------|----------------------|
| `src/SGV.Aplicacion/DependencyInjection.cs` | Registrar validadores FluentValidation nuevos vía `AddValidatorsFromAssemblyContaining`. |
| (nuevo) `SGV.Aplicacion/Seguridad/PasswordReset/IPasswordResetService.cs` | Nueva interfaz con `ForgotPasswordAsync` y `ResetPasswordAsync`. Separada de `IAuthServicio` por SRP. |
| (nuevo) `SGV.Aplicacion/Seguridad/PasswordReset/ForgotPasswordRequestValidator.cs` | Validador FluentValidation: `UserNameOrEmail` requerido, no vacío. |
| (nuevo) `SGV.Aplicacion/Seguridad/PasswordReset/ResetPasswordRequestValidator.cs` | Validador FluentValidation: `UserId`, `Token`, `NewPassword` requeridos; `NewPassword` cumple políticas Identity. |

#### Infrastructure Layer (`SGV.Infraestructura`)

| Archivo | Por qué está afectado |
|---------|----------------------|
| `src/SGV.Infraestructura/DependencyInjection.cs` | Registrar `IPasswordResetService` → `PasswordResetService` y `IEmailSender` → `SmtpEmailSender`. |
| (nuevo) `SGV.Infraestructura/Seguridad/PasswordResetService.cs` | Implementación que usa `UserManager<SgvIdentityUser>` para generar/validar tokens y `IEmailSender` para enviar el mail. |
| (nuevo) `SGV.Infraestructura/Email/SmtpEmailSender.cs` | Implementación de `IEmailSender` de ASP.NET Core Identity usando SMTP (vía `SmtpClient` o MailKit). |
| (nuevo) `SGV.Infraestructura/Email/SmtpOptions.cs` | POCO de configuración: `Host`, `Port`, `EnableSsl`, `UserName`, `Password`, `FromAddress`, `FromName`, `WebBaseUrl`. |

#### Web Layer (`SGV.Web`)

| Archivo | Por qué está afectado |
|---------|----------------------|
| `src/SGV.Web/Pages/Auth/SignIn.cshtml` | Agregar enlace "¿Olvidaste tu contraseña?" que apunte a `/auth/forgot-password`. |
| (nuevo) `src/SGV.Web/Pages/Auth/ForgotPassword.cshtml` | Formulario inspirado en `InspinaTemplate/Inspinia/Pages/Auth/ResetPass.cshtml` con layout SGV. Input de email, submit. |
| (nuevo) `src/SGV.Web/Pages/Auth/ForgotPassword.cshtml.cs` | PageModel con `OnGet` y `OnPostAsync`. Llama a `IAuthApiClient.ForgotPasswordAsync`. Maneja errores de transporte y rate-limit (429). |
| (nuevo) `src/SGV.Web/Pages/Auth/ResetPassword.cshtml` | Formulario inspirado en `InspinaTemplate/Inspinia/Pages/Auth/NewPass.cshtml` con widget de fortaleza (`data-password="bar"`). Inputs: nueva password + confirmación. |
| (nuevo) `src/SGV.Web/Pages/Auth/ResetPassword.cshtml.cs` | PageModel con `OnGet` (recibe `userId` + `token` query params) y `OnPostAsync`. Llama a `IAuthApiClient.ResetPasswordAsync`. |
| `src/SGV.Web/Integration/Auth/IAuthApiClient.cs` | Agregar métodos `ForgotPasswordAsync` y `ResetPasswordAsync`. |
| `src/SGV.Web/Integration/Auth/AuthApiClient.cs` | Implementar los nuevos métodos. El `ForgotPasswordAsync` **no usa** `ApiBearerTokenHandler` (es anónimo). El `ResetPasswordAsync` tampoco. |

#### Tests

| Archivo | Por qué está afectado |
|---------|----------------------|
| `tests/SGV.Tests/Api/AuthControllerTests.cs` | Tests de integración API: forgot-password (200 siempre, rate-limit 429), reset-password (200 ok, 400 bad token). |
| (nuevo) `tests/SGV.Tests/Api/AuthForgotPasswordTests.cs` o similar | Tests específicos de forgot-password: timing attack (misma respuesta usuario existe/no existe), rate limiting. |
| (nuevo) `tests/SGV.Tests/Api/AuthResetPasswordTests.cs` o similar | Tests de reset-password: token válido, token inválido, token expirado, rate limiting. |
| `tests/SGV.Tests/Api/ApiWebApplicationFactory.cs` | Agregar `FakeEmailSender` y `FakePasswordResetService` a las sustituciones. El `FakeAuthServicio` no necesita cambio. |
| (nuevo) `tests/SGV.Tests/Aplicacion/PasswordReset` | Tests unitarios de los validadores FluentValidation. |
| (nuevo) `tests/SGV.Tests/Persistencia/PasswordResetGatewayTests.cs` | Tests de persistencia: SecurityStamp cambia tras reset, token anterior queda invalidado. |

### Approaches

1. **Extender IAuthServicio** — Agregar `ForgotPasswordAsync` y `ResetPasswordAsync` a `IAuthServicio` y `AuthServicio`.
   - Pros: Mínimo cambio, reusa DI existente, AuthController solo necesita inyectar una interfaz.
   - Cons: Viola SRP — `IAuthServicio` es "autenticación", no "recuperación de contraseña". Mezcla concerns.
   - Effort: Bajo

2. **Nuevo IPasswordResetService** (Recomendado) — Crear `IPasswordResetService` en `SGV.Aplicacion` y `PasswordResetService` en `SGV.Infraestructura`.
   - Pros: Sigue SRP, consistente con el patrón de separación del codebase (`IUsuarioServicioComandos`, `IUsuarioServicioConsulta`, etc.). El `IAuthServicio` sigue siendo solo para autenticación.
   - Cons: Un archivo más (interfaz) + un archivo más (implementación). AuthController necesita inyectar dos dependencias en vez de una.
   - Effort: Medio

3. **Password reset totalmente server-side** — API genera token, construye la URL completa del link (incluyendo WebBaseUrl), envía email, expone endpoint de validación que el Web luego consume.
   - Pros: El token nunca está expuesto al frontend (solo viaja en el email).
   - Cons: Este es el flujo que ya determinó la issue — es el que vamos a implementar. No hay alternativa real.
   - Effort: N/A (es el diseño base, no una alternativa)

### Recommendation

**Approach 2: Nuevo `IPasswordResetService` independiente.**

Motivos:
1. Sigue el patrón del codebase: cada interfaz tiene una responsabilidad clara (`IAuthServicio` = autenticar, `IPasswordResetService` = resetear password).
2. Coincide con cómo están organizados los otros servicios en `SGV.Aplicacion/` (comandos separados de consultas, roles separados de usuarios).
3. Facilita el testeo: se puede mockear `IPasswordResetService` sin afectar `IAuthServicio`, y viceversa.
4. `AuthController` queda con dos dependencias inyectadas, lo cual es perfectamente aceptable.

### Sub-approach: `IEmailSender` de Identity vs propio

El `Microsoft.AspNetCore.Identity.IEmailSender` es la interfaz estándar de Identity (namespace `Microsoft.AspNetCore.Identity`). Implementarla directamente es lo más limpio porque `UserManager` ya la acepta. Alternativamente, podríamos crear `ISmtpEmailSender` propia. **Recomendación: implementar `IEmailSender` de Identity** directamente, ya que:
- `UserManager` la conoce nativamente.
- No requiere wrappers ni adapters.
- Identity ya sabe llamar a `IEmailSender` cuando se necesita.
- No necesitamos `IUserEmailStore` adicionales.

### Diseño de flujo

```
Usuario                    SGV.Web                     SGV.Api               Identity            SMTP
  |                          |                           |                    |                   |
  |-- GET /auth/forgot-pwd ->|                           |                    |                   |
  |<- forgot form -----------|                           |                    |                   |
  |-- POST (email) --------->|                           |                    |                   |
  |                          |-- POST /api/v1/auth/      |                    |                   |
  |                          |   forgot-password -------->|                    |                   |
  |                          |                           |-- FindByNameOrEmail |                   |
  |                          |                           |-- GeneratePassword  |                   |
  |                          |                           |   ResetToken() ---->|                   |
  |                          |                           |                     |-- token generado  |
  |                          |                           |-- SendEmailAsync()  |                   |
  |                          |                           |   (SMTP) ------------------------------>|
  |                          |<- 200 siempre ------------|                    |                   |
  |<- "Revisá tu email" -----|                           |                    |                   |
  |                          |                           |                    |                   |
  |== EMAIL con link ========|===========================|====================|===================|
  |                          |                           |                    |                   |
  |-- GET /auth/reset-pwd?   |                           |                    |                   |
  |   userId=&token= ------->|                           |                    |                   |
  |<- new password form -----|                           |                    |                   |
  |-- POST (newPassword) --->|                           |                    |                   |
  |                          |-- POST /api/v1/auth/      |                    |                   |
  |                          |   reset-password --------->|                    |                   |
  |                          |                           |-- ResetPassword() ->|                   |
  |                          |                           |<- Ok/Error ---------|                   |
  |                          |<- redirect /auth/sign-in -|                    |                   |
  |-- GET /auth/sign-in ---->|                           |                    |                   |
```

**Nota importante sobre el envío de email**: Dado que la decisión es **Opción B** (token nunca expuesto en HTTP response), la API debe enviar el email directamente. Esto significa que la API necesita conocer la URL base del Web para construir el link de reseteo. Se debe agregar `WebBaseUrl` a la config SMTP (o una propiedad separada en `SmtpOptions`).

**Libería SMTP**: Para .NET 10, `SmtpClient` está obsoleto para nuevos desarrollo. Se recomienda **MailKit** (`MailKit` + `MimeKit` NuGet packages) como cliente SMTP moderno, con `using var smtp = new SmtpClient(); smtp.ConnectAsync(...)`.

### Configuración

```json
// appsettings.Development.json de API
{
  "Smtp": {
    "Host": "localhost",
    "Port": 1025,
    "EnableSsl": false,
    "UserName": "",
    "Password": "",
    "FromAddress": "noreply@sgv.local",
    "FromName": "SGV",
    "WebBaseUrl": "http://localhost:5266"
  }
}
```

`SmtpOptions` con `ValidateOnStart`: fuera de Development, si falta la config → `OptionsValidationException` (fail-loud).

### Token Lifespan

```csharp
options.Tokens.PasswordResetTokenLifespan = TimeSpan.FromHours(1);
```

Esto se configura dentro de la lambda de `AddIdentityCore` en `Program.cs`.

### Rate Limiting

Agregar al pipeline de `Program.cs`:

```csharp
builder.Services.AddRateLimiter(options =>
{
    options.AddFixedWindowLimiter("ForgotPassword", opt =>
    {
        opt.PermitLimit = 3;
        opt.Window = TimeSpan.FromMinutes(15);
        opt.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
        opt.QueueLimit = 0;
    });
    options.AddFixedWindowLimiter("ResetPassword", opt =>
    {
        opt.PermitLimit = 5;
        opt.Window = TimeSpan.FromMinutes(15);
        opt.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
        opt.QueueLimit = 0;
    });
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
});
```

Y luego en el middleware:

```csharp
app.UseRateLimiter();
```

Los endpoints usan `[EnableRateLimiting("ForgotPassword")]` y `[EnableRateLimiting("ResetPassword")]`.

### Tabla de rutas finales

| Ruta | Método | Auth | Rate Limit | Descripción |
|------|--------|------|------------|-------------|
| `/auth/forgot-password` | GET | Anónimo | N/A | Web form para solicitar reseteo |
| `/auth/forgot-password` | POST | Anónimo | N/A | Web submit → llama a API |
| `/auth/reset-password` | GET | Anónimo | N/A | Web form con token (userId + token en query) |
| `/auth/reset-password` | POST | Anónimo | N/A | Web submit → llama a API |
| `POST /api/v1/auth/forgot-password` | API | `[AllowAnonymous]` | 3 req/15min/IP | Genera token, envía email |
| `POST /api/v1/auth/reset-password` | API | `[AllowAnonymous]` | 5 req/15min/IP | Valida token, rota password |

### Risks

- **MailKit dependency**: Agregar `MailKit` + `MimeKit` al proyecto `SGV.Infraestructura`. Validar que no haya conflictos de versiones con las dependencias existentes (EF Core 9.x, Pomelo 9.0.0, Identity).
- **SMTP configuration management**: La API no tiene `appsettings.json` en su proyecto. La sección `Smtp` debe configurarse vía user-secrets y environment variables en producción. El `appsettings.Development.json` existente tiene JWT placeholder — agregar SMTP placeholder allí también.
- **Link construction**: La API necesita conocer la URL base del Web para construir el link de reseteo en el email. Si `WebBaseUrl` está mal configurada, los links no funcionan. Validar al startup.
- **Rate limiting middleware ordering**: `UseRateLimiter()` debe ir ANTES de `UseAuthentication()` en el pipeline, o el rate limiting no se aplica a endpoints anónimos (porque el middleware de auth cortocircuita antes).
- **Token encoding en URL**: El token generado por Identity contiene caracteres no seguros para URL (`+`, `/`, `=`). **IMPORTANTE**: URL-encode el token antes de ponerlo en el link, y URL-decode al recibirlo. Identity los genera con `Base64Url` en algunos casos, pero `GeneratePasswordResetTokenAsync` usa un formato con caracteres especiales. Se debe usar `UrlEncoder.Default.Encode()` o `Uri.EscapeDataString()`.
- **AuthApiClient no necesita bearer handler**: Los métodos `ForgotPasswordAsync` y `ResetPasswordAsync` son anónimos. No deben pasar por `ApiBearerTokenHandler`. Considerar crear un `HttpClient` separado sin handler, o pasar el token handler como opcional.
- **FallbackPolicy**: Como la `FallbackPolicy` global es `RequireAuthenticatedUser()`, los endpoints de reseteo deben tener `[AllowAnonymous]` en el Controller/acción. Verificar que esto también funcione con el rate limiting middleware.
- **Contaminación de SignInModel**: Actualmente `SignInModel` está en `SGV.Web.Pages.Auth`. Las nuevas páginas deben seguir la misma namespace y patrón.
- **Test de timing attack**: El endpoint `forgot-password` debe responder en tiempo constante tanto si el usuario existe como si no. Probar con `Assert.Equal` en el tiempo de respuesta no es práctico — mejor verificar que el mensaje y status code sean idénticos.

### Ready for Proposal

**Sí** — listo para pasar a fase `propose`. Todos los hallazgos están documentados, los gaps identificados, y las decisiones principales ya fueron tomadas (IEmailSender con SMTP, rate limiting con fixed window, token lifespan 1 hora, IPasswordResetService separado). Lo que queda para la proposal es formalizar el alcance, no-goals y criterios de aceptación.
