# Diseño: Recuperación de contraseña (#181)

## 1. Resumen de arquitectura

El flujo conserva `SGV.Contracts` como leaf, define el puerto en Aplicación y deja Identity/SMTP en Infraestructura. La Web actúa como BFF Razor Pages.

```text
Usuario → Forgot/Reset Razor Page → IAuthApiClient anónimo → AuthController
                                                       → IPasswordResetService
                                                       → UserManager<SgvIdentityUser>
                                                       → IEmailSender → Logger (dev) | MailKit → SMTP
Email → /auth/reset-password?userId=…&token=URL-encoded → Web → API → Identity
```

`forgot-password` nunca devuelve el token ni confirma existencia; `reset-password` aplica el token Identity de vida útil 1 hora y rota `SecurityStamp`.

## 2. Componentes por capa (archivos exactos)

| Capa / archivo | Tipo | Responsabilidad y cambio concreto |
|---|---|---|
| `src/SGV.Contracts/Auth/AuthApiRoutes.cs` | Modificado | Agregar rutas relativas/absolutas `ForgotPassword` y `ResetPassword`. |
| `src/SGV.Contracts/Seguridad/Usuarios/UsuarioContracts.cs` | Modificado | Agregar `ForgotPasswordRequest` y `ResetPasswordRequest`. |
| `src/SGV.Aplicacion/Seguridad/PasswordReset/IPasswordResetService.cs` | Nuevo | Puerto async separado, con `CancellationToken`, para solicitud y ejecución del reset. |
| `src/SGV.Aplicacion/Seguridad/PasswordReset/ForgotPasswordRequestValidator.cs` | Nuevo | Validar identificador requerido/no vacío. |
| `src/SGV.Aplicacion/Seguridad/PasswordReset/ResetPasswordRequestValidator.cs` | Nuevo | Validar id/token/password y política Identity vigente. |
| `src/SGV.Aplicacion/DependencyInjection.cs` | Modificado | El scanning existente ya registra los validadores; agregar marcador/import sólo si hace falta, sin duplicar registros. |
| `src/SGV.Infraestructura/Seguridad/PasswordResetService.cs` | Nuevo | Buscar por username/email, generar token, enviar correo y ejecutar `ResetPasswordAsync`; respuesta anti-enumeración. |
| `src/SGV.Infraestructura/Email/SmtpEmailSender.cs` | Nuevo | Implementar `IEmailSender`, construir enlace escapado y seleccionar sink Logger/MailKit. |
| `src/SGV.Infraestructura/Email/SmtpOptions.cs` | Nuevo | Options tipadas y validables, incluida `WebBaseUrl` y `Mode`. |
| `src/SGV.Infraestructura/DependencyInjection.cs` | Modificado | Registrar servicio scoped y sender según options; mantener recursos SMTP por envío. |
| `src/SGV.Infraestructura/SGV.Infraestructura.csproj` | Modificado | Referenciar `MailKit` explícitamente; `MimeKit` queda transitiva. |
| `src/SGV.Api/Program.cs` | Modificado | Token providers/lifespan, options SMTP fail-loud, políticas rate-limit y middleware. |
| `src/SGV.Api/Controllers/AuthController.cs` | Modificado | Inyectar `IPasswordResetService`; acciones `[AllowAnonymous]`, validación y policies named. |
| `src/SGV.Api/appsettings.Development.json` | Modificado | Placeholder `Smtp` en modo `Logger`. |
| `src/SGV.Web/Program.cs` | Modificado, sin cambio estructural | Mantener composition root; registrar un segundo cliente HTTP anónimo y componer `AuthApiClient` con ambos transportes. |
| `src/SGV.Web/Pages/Auth/ForgotPassword.cshtml` + `.cs` | Nuevos | Form público, validación server-side, confirmación genérica y errores 429/red/timeout. |
| `src/SGV.Web/Pages/Auth/ResetPassword.cshtml` + `.cs` | Nuevos | Query id/token, unescape, confirmación, widget de fortaleza y PRG a SignIn. |
| `src/SGV.Web/Pages/Auth/SignIn.cshtml` | Modificado | Agregar enlace con tag helper a ForgotPassword. |
| `src/SGV.Web/Integration/Auth/IAuthApiClient.cs` | Modificado | Exponer ambos métodos recovery. |
| `src/SGV.Web/Integration/Auth/AuthApiClient.cs` | Modificado | Enviar login por cliente con bearer y recovery por cliente anónimo; `EnsureSuccessStatusCode` preserva 429. |
| `tests/SGV.Tests/Api/ApiWebApplicationFactory.cs` | Modificado | Sustituir `IPasswordResetService`/`IEmailSender`, options y contadores deterministas para integración. |

## 3. Decisiones técnicas clave

| Decisión | Alternativas / tradeoff | Elección y justificación |
|---|---|---|
| Servicio recovery | Extender `IAuthServicio` reduce archivos pero mezcla autenticación y recuperación. | `IPasswordResetService`: SRP y patrón de interfaces acotadas del repo. |
| Transporte SMTP | `System.Net.Mail.SmtpClient` evita paquete pero no se recomienda para desarrollo nuevo. | MailKit: API async moderna y mantenimiento activo. |
| Rate limit | Política global simplifica configuración pero impondría un único umbral. | Policies named por acción: 3/15 min y 5/15 min/IP. |
| HTTP anónimo | Hacer opcional `ApiBearerTokenHandler` introduce ramificación sensible por request. Crear otro `IAuthApiClient` fragmenta el contrato. | Mantener un `AuthApiClient` con **dos `HttpClient`**: typed autenticado actual y named `AnonymousAuthApiClient` sin handler, inyectado mediante `IHttpClientFactory`. Un contrato, pipelines físicamente separados y testeables. |
| Desarrollo local | SMTP real obliga Docker/credenciales. | `Smtp:Mode=Logger` en Development; `Smtp` real en ambientes configurados. |
| Token en URL | Token crudo puede contener `+`, `/`, `=`. | `Uri.EscapeDataString` al generar y `Uri.UnescapeDataString` en Web antes de API. |
| Anti-enumeración | Fire-and-forget directo elimina diferencia de espera pero pierde observabilidad/errores y viola el patrón async seguro. | Respuesta idéntica y trabajo desacoplado mediante cola/`BackgroundService`; no usar `_ = SendEmailAsync`. Si tasks no incorpora cola durable/in-memory, el diseño queda bloqueado por la spec de latencia. |

## 4. Pipeline del middleware (`SGV.Api/Program.cs`)

```text
app.UseExceptionHandler();
app.UseStatusCodePages();
[Development] app.UseSwagger(); app.UseSwaggerUI();
app.UseCors();
app.UseRateLimiter();                 // nuevo, antes de autenticación
app.UseAuthentication();
app.Use(async (context, next) => …);  // revalidator vigente
app.UseAuthorization();
app.MapHealthChecks(...);
app.MapControllers();
```

## 5. Configuración SMTP

```json
"Smtp": {
  "Mode": "Logger",
  "Host": "localhost",
  "Port": 1025,
  "EnableSsl": false,
  "UserName": "",
  "Password": "",
  "FromAddress": "noreply@sgv.local",
  "FromName": "SGV",
  "WebBaseUrl": "http://localhost:5266"
}
```

`Mode` admite únicamente `Logger|Smtp`. `WebBaseUrl` debe ser URI absoluta sin query; siempre se valida al inicio. En modo `Smtp`, también se validan host, puerto y remitente; credenciales pueden ser vacías para relay local. Equivalentes: `Smtp__Mode`, `Smtp__Host`, `Smtp__Port`, `Smtp__EnableSsl`, `Smtp__UserName`, `Smtp__Password`, `Smtp__FromAddress`, `Smtp__FromName`, `Smtp__WebBaseUrl`. Secretos sólo por user-secrets/env/vault.

## 6. Paquetes NuGet

El repo no usa `Directory.Packages.props`: las versiones están inline. Agregar `MailKit` estable compatible con `net10.0` directamente en `src/SGV.Infraestructura/SGV.Infraestructura.csproj`, fijando la versión resuelta tras `dotnet restore` (objetivo: última estable 4.x disponible). No agregar referencia directa a `MimeKit` salvo que compile-time lo requiera: `MailKit` ya la aporta transitivamente. Validar árbol con `dotnet list package --include-transitive`.

## 7. Pruebas (TDD)

| Capa | Archivo propuesto | Cobertura |
|---|---|---|
| Unit Aplicación | `tests/SGV.Tests/Aplicacion/Seguridad/PasswordResetValidatorsTests.cs` | `FluentValidation.TestHelper`: vacíos y matriz mínima de password sin tests redundantes. |
| Unit Infraestructura | `tests/SGV.Tests/Infraestructura/PasswordResetServiceTests.cs` | `UserManager<SgvIdentityUser>` con store mock, `IEmailSender` fake/cola fake; existente/no existente, link escapado y resultado Identity. |
| API integración | `tests/SGV.Tests/Api/PasswordResetApiTests.cs` | Anonimato bajo fallback, bodies iguales, email capturado, reset completo, 4.ª forgot→429, 6.ª reset→429 y `Retry-After`. Aislar IP/policies por factory para paralelismo. |
| Web integración | `tests/SGV.Tests/Web/PasswordResetWebTests.cs` | Render público, layout auth, enlace SignIn, antiforgery, validación server-side, unescape, PRG y conservación de input ante 429. |
| Cliente HTTP | `tests/SGV.Tests/Web/AuthApiClientPasswordResetTests.cs` | Ausencia de `Authorization`, rutas/body, cancelación previa, 429 preservado, sin `CommandResultMapper`. |
| Persistencia/Identity | `tests/SGV.Tests/Persistencia/PasswordResetIdentityMySqlFactTests.cs` | Con MySQL real: cambia `SecurityStamp`, password anterior falla y token previo no se reutiliza. Sin migración. |

Además: test de options en Production/Development y tres corridas consecutivas `dotnet test SGV.slnx --no-build`; `bun run build` por cambios Web.

## 8. Riesgos técnicos residuales

- **Orden de middleware**: regresión deja endpoints sin cuota; test anónimo hasta 429.
- **Encoding doble**: model binding ya decodifica query; aplicar `UnescapeDataString` exactamente una vez y testear `+a/b=`.
- **`WebBaseUrl` incorrecta**: `ValidateOnStart` y test de URL absoluta.
- **Policies named/global**: usar constantes compartidas internas y atributos explícitos; no `GlobalLimiter`.
- **MailKit**: versión exacta se confirma por restore/build; fijarla en csproj.
- **Fallback + `[AllowAnonymous]`**: integración sin Authorization para ambas acciones.
- **Rate limit por IP detrás de proxy**: hoy se verá IP del proxy; `UseForwardedHeaders` permanece fuera de alcance y debe configurarse en un change operativo.
- **Anti-enumeración temporal**: una cola in-memory puede perder mensajes al apagar; documentar entrega best-effort o adoptar cola durable en follow-up.

## 9. Cumplimiento de specs

Los specs no asignan IDs `REQ-XXXX-NNN`; se mapean por requirement canónico.

| Spec / requirement | Implementación |
|---|---|
| `password-reset-flow`: endpoints anónimos | `AuthController.cs`, `Program.cs` |
| Servicio separado | `IPasswordResetService.cs`, `PasswordResetService.cs`, ambas `DependencyInjection.cs` |
| Providers y 1 hora | `SGV.Api/Program.cs` |
| SMTP y encoding | `SmtpEmailSender.cs`, `SmtpOptions.cs` |
| Options fail-loud | `SmtpOptions.cs`, `SGV.Api/Program.cs`, `appsettings.Development.json` |
| Rate limit por IP | `Program.cs`, `AuthController.cs` |
| Wire-types/rutas/validadores | `UsuarioContracts.cs`, `AuthApiRoutes.cs`, validadores |
| Anti-enumeración | `PasswordResetService.cs`, sender/cola, `AuthController.cs` |
| `password-reset-web`: Forgot pública | `ForgotPassword.cshtml(.cs)` |
| Reset pública/query/widget | `ResetPassword.cshtml(.cs)` |
| Enlace SignIn | `SignIn.cshtml` |
| 429 y transporte | ambos PageModels, `AuthApiClient.cs` |
| `sgv-web-authentication`: enlace/única salida | `SignIn.cshtml` |
| `web-apiclient-transport-contract`: anónimo | `Program.cs` Web, `AuthApiClient.cs` |
| Fallos nativos/cancelación | `AuthApiClient.cs`, PageModels |
| Excepción de mapper | `AuthApiClient.cs` |

## 10. Notas para `tasks.md`

- Entrega en un único PR con `size:exception` aprobada; mantener commits por unidad backend/Web/tests.
- Orden RED→GREEN→REFACTOR: contratos y tests de validación; servicio/Identity/email; API/rate limit; cliente anónimo; Razor Pages; integración MySQL; validación total.
- Crear exactamente los archivos de test indicados en §7 y ampliar factories existentes, evitando duplicar escenarios.
- Resolver primero la cola segura para envío diferido: la spec exige latencia equivalente, pero el fire-and-forget no observado no es aceptable.
- Gates: restore/build, suite relevante, `bun run build`, suite completa y gate de tres corridas deterministas.
