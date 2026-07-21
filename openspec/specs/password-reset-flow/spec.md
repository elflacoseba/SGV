# Especificación de Password Reset Flow (backend)

## Propósito

Definir el flujo backend completo de recuperación de contraseña en `SGV.Api`,
basado en ASP.NET Core Identity. La API envía el email directamente vía
`IEmailSender` (Identity) + MailKit, de modo que el token **nunca** aparece
en respuestas HTTP. El flujo MUST ser self-service para usuarios no
autenticados, con rate limiting por IP, validación fail-loud de la
configuración SMTP y respuesta idéntica para evitar enumeración de
usuarios.

## Requisitos

### Requirement: Endpoints anónimos de reseteo

`SGV.Api` MUST exponer `POST /api/v1/auth/forgot-password` y
`POST /api/v1/auth/reset-password` marcados con `[AllowAnonymous]`. Ambos
MUST delegar en `IPasswordResetService` y responder `200 OK` con un
mensaje genérico en español en el camino feliz. La política global
`FallbackPolicy = RequireAuthenticatedUser()` MUST NOT bloquear estos
endpoints.

#### Scenario: Forgot-password siempre 200

- **DADO** una solicitud anónima a `forgot-password` con `UserNameOrEmail`
  no vacío
- **CUANDO** el endpoint procesa la solicitud
- **ENTONCES** MUST responder `200 OK` con mensaje genérico en español
- **Y** el body MUST ser byte-equivalente exista o no el usuario destino.

#### Scenario: Reset-password exitoso rota credenciales

- **DADO** un usuario con token vigente y `NewPassword` que cumple la
  política vigente
- **CUANDO** se ejecuta `reset-password`
- **ENTONCES** MUST responder `200 OK`
- **Y** `UserManager.ResetPasswordAsync` MUST rotar la contraseña
- **Y** `SecurityStamp` MUST regenerarse, invalidando tokens previos.

#### Scenario: Reset-password con token inválido o expirado

- **DADO** una solicitud con `Token` manipulado o emitido hace más de 1 h
- **CUANDO** el endpoint procesa la solicitud
- **ENTONCES** MUST responder `400 Bad Request` con mensaje en español
- **Y** MUST NOT modificar la contraseña del usuario.

### Requirement: Servicio de reseteo separado de AuthServicio

`IPasswordResetService` MUST vivir en
`SGV.Aplicacion/Seguridad/PasswordReset/` y declarar
`ForgotPasswordAsync(ForgotPasswordRequest, CancellationToken)` y
`ResetPasswordAsync(ResetPasswordRequest, CancellationToken)`.
`PasswordResetService` MUST vivir en `SGV.Infraestructura` y depender de
`UserManager<SgvIdentityUser>` + `IEmailSender`.

#### Scenario: IPasswordResetService e IAuthServicio conviven

- **DADO** el contenedor de DI de `SGV.Api`
- **CUANDO** se enumeran los servicios registrados
- **ENTONCES** `IPasswordResetService` y `IAuthServicio` MUST ser registros
  independientes.

### Requirement: Token providers y lifespan de una hora

El registro de Identity MUST llamar a `AddDefaultTokenProviders()` dentro
de la lambda de `AddIdentityCore<SgvIdentityUser>` y MUST fijar
`options.Tokens.PasswordResetTokenLifespan = TimeSpan.FromHours(1)`.

#### Scenario: Token expirado a la hora

- **DADO** un token emitido hace más de 60 min
- **CUANDO** `UserManager.ResetPasswordAsync` se invoca
- **ENTONCES** MUST devolver `IdentityResult.Failed`
- **Y** el endpoint MUST responder `400 Bad Request` con mensaje en español.

### Requirement: SMTP con URL-encoding del token

`IEmailSender` (`Microsoft.AspNetCore.Identity`) MUST estar registrado en
DI. `SmtpEmailSender` MUST usar MailKit, URL-encodear el token con
`Uri.EscapeDataString` y componer el link del email como
`{WebBaseUrl}/auth/reset-password?userId={id}&token={tokenUrlEncoded}`.
El token MUST **no** aparecer crudo en el cuerpo del email.

#### Scenario: Link del email contiene token URL-encoded

- **DADO** usuario `id=abc` y token crudo `+a/b=`
- **CUANDO** `SmtpEmailSender` arma el link
- **ENTONCES** MUST contener `token=%2Ba%2Fb%3D`
- **Y** MUST apuntar a `WebBaseUrl + "/auth/reset-password?userId=abc&token=%2Ba%2Fb%3D"`.

### Requirement: SmtpOptions con ValidateOnStart fail-loud

`SmtpOptions` MUST bindearse con `BindConfiguration("Smtp")` y validar
con `ValidateDataAnnotations().ValidateOnStart()`. Fuera de
`Development`, la ausencia de la sección o de `Smtp:WebBaseUrl` MUST
lanzar `OptionsValidationException` y el host MUST NOT arrancar.

#### Scenario: WebBaseUrl ausente en Producción

- **DADO** `ASPNETCORE_ENVIRONMENT == "Production"` y `Smtp:WebBaseUrl == null`
- **CUANDO** se inicia la API
- **ENTONCES** MUST lanzar `OptionsValidationException`
- **Y** el host MUST NO procesar requests.

### Requirement: Rate limiting fijo por IP

`AddRateLimiter` MUST registrar dos políticas fixed window:
`forgot-password` (3 req / 15 min / IP, `QueueLimit=0`) y
`reset-password` (5 req / 15 min / IP, `QueueLimit=0`).
`UseRateLimiter()` MUST ejecutarse antes de `UseAuthentication()` y
`UseAuthorization()`. Exceso MUST responder `429 Too Many Requests` con
header `Retry-After`.

#### Scenario: Cuarto request de forgot-password en 15 min

- **DADO** una IP con 3 requests previos a `forgot-password` en los
  últimos 15 min
- **CUANDO** envía un cuarto request
- **ENTONCES** MUST responder `429 Too Many Requests`
- **Y** MUST incluir header `Retry-After`.

### Requirement: Wire-types ForgotPasswordRequest / ResetPasswordRequest

`SGV.Contracts/Seguridad/Usuarios/UsuarioContracts.cs` MUST incluir
`record ForgotPasswordRequest(string UserNameOrEmail)` y
`record ResetPasswordRequest(string UserId, string Token, string NewPassword)`.
Las rutas MUST vivir en `SGV.Contracts/Auth/AuthApiRoutes.cs` como
`ForgotPasswordRelative` / `ResetPasswordRelative` y sus versiones
absolutas. Ambos records MUST tener validador FluentValidation con
campos requeridos no vacíos y `NewPassword` cumpliendo la política
vigente de `IdentityOptions.Password`.

#### Scenario: ForgotPasswordRequest rechaza UserNameOrEmail vacío

- **DADO** un `ForgotPasswordRequest { UserNameOrEmail = "" }`
- **CUANDO** el validador procesa la entrada
- **ENTONCES** MUST emitir `ValidationFailure` con
  `PropertyName = "UserNameOrEmail"`.

### Requirement: Anti-enumeración por respuesta idéntica

`ForgotPassword` MUST devolver el mismo `status code`, body y latencia
observable (verificable con `Stopwatch` en tests) exista o no el
usuario. La rama "existe" MUST disparar el envío SMTP en
fire-and-forget **después** de construir la respuesta HTTP.

#### Scenario: Respuestas byte-equivalentes

- **DADO** dos requests idénticos a `forgot-password`, uno con email
  registrado y otro con email inexistente
- **CUANDO** se comparan status, body y headers
- **ENTONCES** MUST ser idénticos
- **Y** el header `Retry-After` MUST estar ausente en ambas respuestas.

## Fuera de alcance

- Change password autenticado (flujo aparte).
- Verificación de email al registrarse.
- MFA/2FA sobre el flujo de reset.
- Lockout por `AccessFailedCount` durante reset.
- Reenvío del email desde UI (un único submit por visita).
- Templates de email configurables (un único template inline).
- Migraciones de DB (`SecurityStamp` ya existe en `AspNetUsers`).
