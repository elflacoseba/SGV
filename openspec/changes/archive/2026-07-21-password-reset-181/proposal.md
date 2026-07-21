# Propuesta: Permitir resetear la contraseña (#181)

## Resumen

Este change cierra la brecha de recuperación de credenciales que hoy tiene SGV: un usuario que olvidó su contraseña no tiene ningún camino de auto-servicio y depende de un `Administrador`. Vamos a implementar el flujo estándar de ASP.NET Core Identity (token + email + nueva contraseña) cerrando el lazo en la capa Web con Razor Pages y limitando el abuso con rate limiting nativo.

## Contexto y motivación

Hoy `AuthController` solo expone `POST /api/v1/auth/login` y `SignIn.cshtml` no ofrece salida cuando el usuario no recuerda su password. El único workaround es pedirle a un `Administrador` que ejecute un reset manual (que tampoco existe como flujo formal). Esto degrada la UX, recarga a soporte y bloquea el auto-servicio en escenarios de uso legítimos (rotación tras sospecha de compromiso, usuario nuevo que nunca recibió credenciales, etc.).

La solución MUST ser backend-driven: el token nunca viaja en una respuesta HTTP, viaja exclusivamente en el email. Identity ya provee `UserManager.GeneratePasswordResetTokenAsync` + `ResetPasswordAsync`; solo falta cablearlo, registrar `AddDefaultTokenProviders()`, implementar `IEmailSender` con SMTP, agregar los dos endpoints anónimos con rate limiting y darle al usuario una UI coherente con el shell SGV.

## Decisiones de diseño tomadas

- **Opción B**: la API envía el email directamente. El token **nunca** aparece en una respuesta HTTP — solo en el cuerpo del mail.
- **`IEmailSender` de Identity** implementado con SMTP (no wrapper propio). `UserManager` ya lo conoce nativamente.
- **MailKit + MimeKit** como cliente SMTP (`SmtpClient` está obsoleto para dev nuevo en .NET 10).
- **`IPasswordResetService` separado** de `IAuthServicio` por SRP, siguiendo el patrón del codebase (`IUsuarioServicioComandos` vs `IUsuarioServicioConsulta`).
- **Rate limiting** con `Microsoft.AspNetCore.RateLimiting` middleware, fixed window:
  - `forgot-password`: **3 req / 15 min / IP**.
  - `reset-password`: **5 req / 15 min / IP** (umbral mayor para no castigar al usuario legítimo ante errores de tipeo).
  - `UseRateLimiter()` registrado **antes** de `UseAuthentication()`.
- **Token lifespan = 1 hora** vía `options.Tokens.PasswordResetTokenLifespan`.
- **`[AllowAnonymous]`** explícito en los dos endpoints por la `FallbackPolicy = RequireAuthenticatedUser()`.
- **`SmtpOptions.WebBaseUrl`** requerido: la API construye el link de reseteo. `ValidateOnStart` (fail-loud fuera de Development).
- **`AuthApiClient`**: `ForgotPasswordAsync` y `ResetPasswordAsync` son anónimos → **no** usan `ApiBearerTokenHandler`.
- **UI**: `SignIn.cshtml` agrega "¿Olvidaste tu contraseña?" → `/auth/forgot-password`. Los formularios nuevos reutilizan los stubs de `InspinaTemplate/Inspinia/Pages/Auth/ResetPass.cshtml` y `auth-password.js` (`data-password="bar"`).
- **Anti-enumeración**: respuesta idéntica (status + cuerpo) cuando el usuario existe o no.

## Alcance (qué incluye)

- 2 endpoints API nuevos: `POST /api/v1/auth/forgot-password`, `POST /api/v1/auth/reset-password`.
- 2 páginas Web nuevas: `Pages/Auth/ForgotPassword.cshtml(.cs)` y `Pages/Auth/ResetPassword.cshtml(.cs)`.
- 2 validadores FluentValidation en `SGV.Aplicacion/Seguridad/PasswordReset/`.
- `IPasswordResetService` (Aplicación) + `PasswordResetService` (Infraestructura).
- `SmtpEmailSender` implementando `IEmailSender` con MailKit.
- `SmtpOptions` con `ValidateOnStart` + registro de `AddDefaultTokenProviders()` y `PasswordResetTokenLifespan = 1h`.
- Pipeline de rate limiting con dos políticas named.
- Extensiones en `AuthApiRoutes`, `UsuarioContracts` (records `ForgotPasswordRequest`, `ResetPasswordRequest`).
- Extensión de `IAuthApiClient`/`AuthApiClient` con los dos métodos anónimos.
- Tests: unitarios (validadores, servicio), integración API (rate limit + flujo), web (render + SignIn link).

## Capabilities (contrato con sdd-spec)

### Nuevas

- **`password-reset-flow`** → `openspec/specs/password-reset-flow/spec.md` (nuevo). Cubre: endpoints API, `IPasswordResetService`, `IEmailSender`/`SmtpEmailSender` (MailKit), `SmtpOptions` con `ValidateOnStart`, rate limiting 3/15min y 5/15min, contratos `ForgotPasswordRequest`/`ResetPasswordRequest`, token lifespan 1h, URL encoding del token, anti-enumeración.
- **`password-reset-web`** → `openspec/specs/password-reset-web/spec.md` (nuevo). Cubre: páginas `ForgotPassword.cshtml` y `ResetPassword.cshtml` (con widget `data-password="bar"`), propagación de `429`, manejo de `userId`+`token` en query string, copy en español.

### Modificadas (delta en el change folder)

- **`sgv-web-authentication`** → delta en `openspec/changes/2026-07-21-password-reset-181/specs/sgv-web-authentication/spec.md`. El requisito "Pantalla de inicio de sesión web" se extiende con el link "¿Olvidaste tu contraseña?" → `/auth/forgot-password`; el scenario "Flujos fuera de alcance no aparecen" se actualiza (la recuperación ahora sí está en alcance).
- **`web-apiclient-transport-contract`** → delta en `openspec/changes/2026-07-21-password-reset-181/specs/web-apiclient-transport-contract/spec.md`. Agrega requirement: `IAuthApiClient.ForgotPasswordAsync` y `ResetPasswordAsync` son anónimos y NO atraviesan `ApiBearerTokenHandler`.

## Fuera de alcance (qué NO incluye)

- **Change password** autenticado (escenario distinto, flujo separado).
- **Verificación de email** al registrarse (no es requisito de reset).
- **MFA / 2FA** en el flujo de reset.
- **Lockout por `AccessFailedCount`** en reset (Identity solo lo aplica a login).
- **Reenvío del email** desde la UI de forgot-password (un solo submit).
- **Animaciones o UI enriquecida** post-submit (alcanza con mensaje estático).
- **Catálogo de templates de email** configurable (un único template HTML inline suficiente).
- **Migración de DB** (no hay cambios de schema — `AspNetUsers.SecurityStamp` ya existe).
- **Internacionalización** del email (se envía solo en español por ahora).

## Criterios de aceptación

- [ ] `POST /api/v1/auth/forgot-password` responde `200 OK` con mensaje genérico idéntico exista o no el usuario.
- [ ] Usuario existente con email válido recibe un correo con link `https://{WebBaseUrl}/auth/reset-password?userId={id}&token={tokenUrlEncoded}`.
- [ ] El token en el link está URL-encoded (`Uri.EscapeDataString`); el Web lo URL-decodea antes de enviarlo a la API.
- [ ] El cuarto request a `forgot-password` desde la misma IP en <15 min responde `429 Too Many Requests` con header `Retry-After`.
- [ ] El sexto request a `reset-password` desde la misma IP en <15 min responde `429` con `Retry-After`.
- [ ] `POST /api/v1/auth/reset-password` con token válido rota la contraseña, regenera `SecurityStamp`, e invalida tokens previos.
- [ ] `POST /api/v1/auth/reset-password` con token expirado (>1 h) o inválido responde `400 Bad Request` con mensaje en español.
- [ ] La nueva contraseña debe cumplir las políticas vigentes en `IdentityOptions.Password` (`RequiredLength=6`, mayúscula, minúscula, dígito, no-alfanumérico).
- [ ] `GET /auth/forgot-password` renderiza el formulario con input de email (layout Inspinia, sin shell).
- [ ] `GET /auth/reset-password?userId=...&token=...` renderiza el formulario con widget `data-password="bar"`.
- [ ] `SignIn.cshtml` expone el enlace "¿Olvidaste tu contraseña?" apuntando a `/auth/forgot-password`.
- [ ] Si la sección `Smtp` falta o `WebBaseUrl` está vacía fuera de Development → `OptionsValidationException` al arranque.
- [ ] `Smtp` config ausente en Development → el host arranca con un `SmtpEmailSender` que escribe a `ILogger` (no rompe dev local sin SMTP real).
- [ ] `IAuthApiClient.ForgotPasswordAsync` y `ResetPasswordAsync` NO atraviesan `ApiBearerTokenHandler`.
- [ ] `dotnet test SGV.slnx` verde; `bun run build` sin errores.

## Riesgos identificados

| # | Riesgo | Mitigación |
|---|--------|-----------|
| 1 | **MailKit dependency**: agregar `MailKit` + `MimeKit` puede chocar con versiones de EF Core 9 / Pomelo 9.0.0. | Fijar versiones compatibles con `Microsoft.Extensions.*` 10.x en `Directory.Packages.props`; validar con `dotnet restore` antes del primer commit. |
| 2 | **Token con caracteres no URL-safe**: el token generado por Identity puede contener `+`, `/`, `=` que rompen el link si no se encodean. | `Uri.EscapeDataString(token)` al construir el link; `Uri.UnescapeDataString(token)` al recibirlo en el PageModel antes de reenviar a la API. Cubierto por test. |
| 3 | **`WebBaseUrl` mal configurada**: links de email rotos sin error visible. | `ValidateOnStart` con `ValidateDataAnnotations().ValidateOnStart()` en `Program.cs` fuera de Development; el host no arranca si falta. |
| 4 | **Orden del middleware**: si `UseRateLimiter()` va después de `UseAuthentication()`, los endpoints anónimos no quedan limitados. | Registrar `app.UseRateLimiter()` **antes** de `app.UseAuthentication()` y `app.UseAuthorization()`. Cubierto por test que verifica `429` sin Authorization header. |
| 5 | **`FallbackPolicy` + `[AllowAnonymous]` + rate limiting**: tres middlewares interactúan; un cambio de orden rompe el flujo. | Tests de integración que pegan `forgot-password` y `reset-password` sin Authorization y validan tanto `200` (éxito) como `429` (rate limit). |
| 6 | **User enumeration vía timing**: aunque la respuesta es idéntica, la rama "usuario existe" tarda más (token + SMTP). | El body y el status son idénticos desde el inicio; el SMTP se hace fire-and-forget después de responder. Cubierto por test que asserte `Response.Body` y `StatusCode` iguales. |
| 7 | **SMTP no disponible en dev**: desarrolladores sin Mailpit/MailHog quedan sin flujo verificable. | `SmtpEmailSender` resuelve su transporte por ambiente: en Development usa `ILogger` como sink; en otros ambientes usa MailKit. Configurable vía `Smtp:Mode` (`Logger`/`Smtp`). |
| 8 | **`AddDefaultTokenProviders()` cambia superficie**: agregar providers puede modificar el comportamiento de `GenerateUserTokenAsync` para otros flujos. | Audit: los únicos tokens emitidos hoy son de reset (no hay `GenerateEmailConfirmationTokenAsync`, etc.). Cubierto por tests unitarios que asserten que el provider por defecto (`DefaultProvider`) sigue siendo el de reset. |

## Plan de PRs propuesto

**Recomendación: chained PR (2 PRs)** — el total estimado es ~420 LoC entre código + tests + configuración, justo en el borde del budget de 400 LoC y con concerns claramente separables.

### PR 1 — Backend: API, SMTP, validators, tests (~240 LoC)

- `SGV.Api/Program.cs`: registrar `SmtpOptions`, `AddDefaultTokenProviders`, `AddRateLimiter`, `IEmailSender`.
- `SGV.Contracts/Auth/AuthApiRoutes.cs`: agregar rutas.
- `SGV.Contracts/Seguridad/Usuarios/UsuarioContracts.cs`: records `ForgotPasswordRequest`, `ResetPasswordRequest`.
- `SGV.Aplicacion/Seguridad/PasswordReset/`: `IPasswordResetService`, validadores.
- `SGV.Infraestructura/Seguridad/PasswordResetService.cs`.
- `SGV.Infraestructura/Email/SmtpEmailSender.cs`, `SmtpOptions.cs`.
- `SGV.Api/Controllers/AuthController.cs`: endpoints nuevos con `[AllowAnonymous]` + `[EnableRateLimiting]`.
- `SGV.Api/appsettings.Development.json`: bloque `Smtp` placeholder.
- Tests: unit (validadores, service con `IEmailSender` fake), integración API (rate limit + flujo + SMTP fake).

### PR 2 — Web UI: Razor Pages, AuthApiClient, SignIn link, tests (~180 LoC)

- `SGV.Web/Pages/Auth/SignIn.cshtml`: agregar link "¿Olvidaste tu contraseña?".
- `SGV.Web/Pages/Auth/ForgotPassword.cshtml(.cs)`: nuevo.
- `SGV.Web/Pages/Auth/ResetPassword.cshtml(.cs)`: nuevo (con `auth-password.js`).
- `SGV.Web/Integration/Auth/IAuthApiClient.cs` + `AuthApiClient.cs`: métodos anónimos (sin `ApiBearerTokenHandler`).
- Tests: web (`SgvWebApplicationFactory`) — render de ambas páginas, validación server-side, link desde SignIn, propagación de `429`.

Ambos PRs son independientes: PR1 deja la API funcional y verificable vía `curl`/Postman; PR2 le da la cara visible al usuario sin tocar contratos.

## Preguntas abiertas / supuestos

1. **¿Mailpit/MailHog/Papercut en dev o el sink `ILogger`?** Asumimos sink `ILogger` (configurable) para que `dotnet run` no requiera Docker. Confirmar si querés Mailpit preconfigurado.
2. **¿Confirmación de email al recibir el reset?** Asumimos que NO se verifica `EmailConfirmed` antes de generar el token (alineado con la decisión de la issue). Confirmar.
3. **¿Política de contraseña al resetear más estricta que en alta?** Asumimos la misma (`RequiredLength=6` + las 4 clases). Si querés endurecerla (p.ej. `RequiredLength=8` o `RequireUniqueChars`) es un cambio adicional.
4. **¿Bloquear reenvío si ya hay un token vigente?** Asumimos NO: cada submit genera token nuevo e invalida el anterior. Confirmar.
5. **¿Auditoría del reset?** Asumimos que `Auditorias` lo captura automáticamente vía el `AuditoriaInterceptor` (mismo camino que login/cambio de password). Si necesitás un tipo de evento dedicado (`PasswordResetRequested`, `PasswordResetCompleted`), se agrega.
6. **¿Notificación al `Administrador` cuando un usuario se resetea?** Asumimos NO. Si querés un email de auditoría al admin, es alcance extra.

## Referencias

- Issue: GitHub #181 — *Permitir resetear la contraseña*.
- Exploración: `openspec/changes/2026-07-21-password-reset-181/exploration.md`.
- Specs vigentes que se modifican: `openspec/specs/sgv-web-authentication/spec.md` (por el link en SignIn).
- Specs relacionadas: `openspec/specs/identity-user-role-management/spec.md`, `openspec/specs/web-apiclient-transport-contract/spec.md`.
- Plantillas UI: `InspinaTemplate/Inspinia/Pages/Auth/ResetPass.cshtml`, `InspinaTemplate/Inspinia/wwwroot/js/pages/auth-password.js`.
- Política de contraseña actual: `src/SGV.Api/Program.cs:112-118`.
- Decisiones técnicas previas: `docs/decisiones-implementacion.md` § "Gestión de secretos JWT" (paradigma fail-loud replicado para SMTP).
