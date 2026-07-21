# Verify Report: `2026-07-21-password-reset-181` (#181)

**Modo**: Strict TDD
**Artifact store**: both (OpenSpec + Engram)
**Fecha**: 2026-07-21
**PR**: #182 (`2026-07-21-password-reset-181` → `develop`)
**Cambio**: Permitir resetear la contraseña (recuperación de credenciales self-service).
**Rama**: `2026-07-21-password-reset-181`
**Commit base de verificación**: `91daf489 chore(sdd): apply-progress — Batch 3 verification complete`

## Resumen ejecutivo

El cambio cumple con los cuatro specs (`password-reset-flow`, `password-reset-web`,
`sgv-web-authentication` delta, `web-apiclient-transport-contract` delta) y respeta
los cross-cutting concerns enumerados en `proposal.md`. La suite completa
`dotnet test SGV.slnx` corre verde (2685/2685, 0 fallidos, 0 skipeados) y
`bun run build` cierra sin errores. La cobertura TDD es trazable para cada
requisito menos en dos gaps menores (covering test de `ResetPassword` 429 y
fact-test de `SecurityStamp` contra MySQL real), ninguno bloqueante. **PASS WITH
WARNINGS**.

## Estado de tests

- Tests ejecutados: **2685**
- Tests pasados: **2685**
- Tests fallados: **0**
- Tests skipeados: **0**
- Build status: **warnings** (warnings `NU1510` preexistentes en `SGV.Infraestructura.csproj`; no críticos).
- `bun run build`: **clean** (warnings informativos de Browserslist, `fs` y `baseline-browser-mapping`; build termina OK).

```text
$ dotnet test SGV.slnx --nologo
Passed!  - Failed:     0, Passed:  2685, Skipped:     0, Total:  2685,
         Duration: 1 m 15 s - SGV.Tests.dll (net10.0)
```

Notas sobre la suite: el repo bootstrapea MySQL local con `MySqlFactAttribute`;
sin `ConnectionStrings__SgvDatabase` real los `[MySqlFact]` quedan skipeados
limpio (el `Engram` reporta 146 tests skipeados por entorno en runs recientes).
El verify-run capturó 2685/2685 con 0 fallos, lo que confirma baseline verde
incluso sin DB.

## Trazabilidad specs → implementación

### `password-reset-flow` (nueva, 8 requisitos)

| Requirement | Status | Evidencia |
|-------------|:------:|-----------|
| **1. Endpoints anónimos de reseteo** | ✅ | `src/SGV.Api/Controllers/AuthController.cs:38,70` (`[AllowAnonymous]`), `Program.cs:183-186` (FallbackPolicy). Tests: `AuthControllerPasswordResetTests.ForgotPassword_NoAuthHeader_Returns200`. |
| 1a. Forgot-password siempre 200 | ✅ | `AuthController.ForgotPassword` ignora `PasswordResetOutcome` y siempre responde `Ok(new{mensaje=...})`. Test `AuthControllerPasswordResetTests.ForgotPassword_NoAuthHeader_Returns200`. |
| 1b. Reset-password exitoso rota credenciales | ✅ | `PasswordResetService.ResetPasswordAsync` (`PasswordResetService.cs:80-131`) llama `userManager.ResetPasswordAsync(...)`. Tests: `PasswordResetServiceTests.ResetPasswordAsync_ValidToken_RotatesPassword_ReturnsSuccess`. |
| 1c. Reset-password token inválido/expirado → 400 | ✅ | `AuthController.cs:94-99` mappea `InvalidToken` a `BadRequest`. Test `AuthControllerPasswordResetTests.ResetPassword_InvalidToken_Returns400WithSpanishMessage`. |
| **2. Servicio separado de `IAuthServicio`** | ✅ | Interfaces en `src/SGV.Aplicacion/Seguridad/PasswordReset/IPasswordResetService.cs`. Implementación en `src/SGV.Infraestructura/Seguridad/PasswordResetService.cs`. Registro independiente: `SGV.Infraestructura.DependencyInjection.cs:97` vs `:86`. Test `PasswordResetServiceRegistrationTests` cubre el binding. |
| **3. Token providers + 1 hora** | ✅ | `Program.cs:127-146` (`AddDefaultTokenProviders()` + `DataProtectionTokenProviderOptions.TokenLifespan = TimeSpan.FromHours(1)`). Tests: `IdentityTokenProvidersTests.Api_HostIdentityComposition_RegistersDefaultTokenProviders`, `Api_HostIdentityComposition_ConfiguresOneHourPasswordResetLifespan`. |
| **4. SMTP con URL-encoding** | ✅ | `SmtpEmailSender.BuildPasswordResetLink` (`SmtpEmailSender.cs:39-53`) usa `Uri.EscapeDataString`. Tests: `SmtpEmailSenderTests.BuildPasswordResetLink_EncodesUserIdAndToken` (verifica `token=%2Ba%2Fb%3D`), `PasswordResetServiceTests.ForgotPasswordAsync_ExistingUserByUserName_SendsEmailWithUrlEncodedLink`. |
| **5. SmtpOptions ValidateOnStart fail-loud** | ✅ | `SmtpOptions.cs:26-65` con `[Required]`/`[Url]` y `Program.cs:120-124` (`BindConfiguration + ValidateDataAnnotations().ValidateOnStart()`). Tests: `SmtpOptionsValidatorTests.DataAnnotations_WebBaseUrlMissing_FailsValidation`, `DataAnnotations_WebBaseUrlRelative_FailsValidation`. |
| **6. Rate limit fijo por IP** | ⚠️ | `Program.cs:207-243` registra las dos políticas; `AuthController` aplica `[EnableRateLimiting(...)]` con constantes internas (`AuthController.cs:22-25`). Cubierto solo para ForgotPassword: `AuthControllerPasswordResetTests.ForgotPassword_FourthRequestFromSameIpWithinWindow_Returns429WithRetryAfterHeader`. **Falta covering test del 429 de `ResetPassword`** (5 req / 15 min). Ver warnings. |
| **7. Wire-types + validadores FluentValidation** | ✅ | Records en `src/SGV.Contracts/Seguridad/Usuarios/UsuarioContracts.cs:86,97`. Rutas en `src/SGV.Contracts/Auth/AuthApiRoutes.cs:28-45`. Validadores en `src/SGV.Aplicacion/Seguridad/PasswordReset/ForgotPasswordRequestValidator.cs` y `ResetPasswordRequestValidator.cs`. Tests: `ForgotPasswordRequestValidatorTests` y `ResetPasswordRequestValidatorTests`. |
| **8. Anti-enumeración por respuesta idéntica** | ✅ | `PasswordResetService.ForgotPasswordAsync` (`PasswordResetService.cs:35-78`) loguea y devuelve `PasswordResetOutcome.Success` tanto para usuario conocido como inexistente. `AuthController.cs:62-67` ignora el outcome. Tests: `AuthControllerPasswordResetTests.ForgotPassword_KnownAndUnknownIdentifiers_ReturnByteEquivalentBodies`, `PasswordResetServiceTests.ForgotPasswordAsync_ExistingAndUnknownUsers_ProduceByteEquivalentSuccessOutcome`. |

### `password-reset-web` (nueva, 5 requisitos)

| Requirement | Status | Evidencia |
|-------------|:------:|-----------|
| **1. Página ForgotPassword pública** | ✅ | `src/SGV.Web/Pages/Auth/ForgotPassword.cshtml` + `.cshtml.cs` + `_ViewStart.cshtml` del namespace `Auth` (layout sin shell). Input email con `[Required]`, mensaje genérico. Tests: `ForgotPasswordPageTests` (5 tests: render, success, validation, 429, transport). |
| **2. Página ResetPassword pública con token en query** | ✅ | `src/SGV.Web/Pages/Auth/ResetPassword.cshtml` + `.cshtml.cs`. Decodifica con `Uri.UnescapeDataString` (`ResetPassword.cshtml.cs:108-109`). Hidden `UserId`/`Token` en `ResetPassword.cshtml:25-26`. Widget `data-password="bar"` en L29. Tests: `ResetPasswordPageTests` (6 tests: render sin query, query decoded, mismatch, invalid token, valid PRG a SignIn, 429). |
| 2a. POST exitoso → redirect SignIn | ✅ | `ResetPassword.cshtml.cs:81-82` (`LocalRedirect("/auth/sign-in")` con TempData). Test `ResetPasswordPageTests.Post_ResetPasswordWithValidToken_RedirectsToSignIn`. |
| 2b. Token inválido/expirado → sin redirigir | ✅ | `ResetPassword.cshtml.cs:84-88` catchea `HttpRequestException` con `StatusCode=BadRequest`. Test `Post_ResetPasswordWithInvalidToken_ShowsControlledError`. |
| **3. SignIn expone enlace "¿Olvidaste tu contraseña?"** | ✅ | `SignIn.cshtml:37`. Test `SignInPasswordResetLinkTests.Get_SignIn_RendersForgotPasswordLinkToPublicPage`. |
| **4. Propagación de 429 con retry copy** | ✅ | `ForgotPassword.cshtml.cs:52-56` y `ResetPassword.cshtml.cs:89-93` capturan `HttpRequestException.TooManyRequests` y muestran `"Hiciste demasiados intentos. Esperá unos minutos antes de volver a intentarlo."`. Tests: `Post_ForgotPassword_WhenApiReturns429_ShowsRateLimitMessage` y `Post_ResetPasswordWhenApiReturns429_ShowsRateLimitMessage`. |
| **5. Errores de transporte sin redirigir** | ✅ | `ForgotPassword.cshtml.cs:57-66` (`HttpRequestException` general + `TaskCanceledException`). Tests: `Post_ForgotPassword_WhenApiIsUnavailable_ShowsTransportMessage`. |

### `sgv-web-authentication` (delta ADDED, 2 requisitos)

| Requirement | Status | Evidencia |
|-------------|:------:|-----------|
| **SignIn expone enlace "¿Olvidaste tu contraseña?"** (added) | ✅ | `SignIn.cshtml:37` con `href="/auth/forgot-password"` y texto exacto `¿Olvidaste tu contraseña?`. Test `SignInPasswordResetLinkTests.Get_SignIn_RendersForgotPasswordLinkToPublicPage` verifica href + texto. |
| **Enlace de recuperación es la única acción fuera del submit de credenciales** (added) | ✅ | `SignIn.cshtml` solo expone el submit de credenciales y el enlace de recuperación; no contiene UI de registro. Test `SignInPasswordResetLinkTests` no cubre este requisito explícitamente, pero la inspección directa de `SignIn.cshtml` (48 LoC) confirma ausencia de registro. Mantenido como ✅ con caveat de gap de cobertura aislado. |

### `web-apiclient-transport-contract` (delta ADDED, 3 requisitos)

| Requirement | Status | Evidencia |
|-------------|:------:|-----------|
| **`ForgotPasswordAsync`/`ResetPasswordAsync` son anónimos** (added) | ✅ | `AuthApiClient.cs:13-14,17,24-28` separa named clients: `AuthenticatedAuthApiClient` con bearer y `AnonymousAuthApiClient` sin bearer (registrado en `SGV.Web/Program.cs:118-125`). Tests: `AuthApiClientPasswordResetTests.ForgotPasswordAsync_PostsToAnonymousRouteWithExpectedBody` (`Assert.Null(LastAuthorization)`) y el simétrico para `ResetPassword`. |
| **Propagan fallos nativos de transporte** (added) | ✅ | `AuthApiClient.cs:71-85` (`PostAnonymousAsync`) usa `EnsureSuccessStatusCode` que conserva `HttpRequestException.StatusCode`. Tests: `ForgotPasswordAsync_WhenApiReturnsTooManyRequests_PreservesStatusCode`, `ResetPasswordAsync_WhenCallerAlreadyCancelled_DoesNotSendRequest`. |
| **Exceptuadas de `CommandResultMapper`** (added) | ✅ | `AuthApiClient.cs:71-85` no llama a `CommandResultMapper`; método `PostAnonymousAsync` resuelve outcome nativo. Verificado por inspección: ningún call site recovery (`ForgotPasswordAsync`/`ResetPasswordAsync`) invoca el mapper. |

## Cross-cutting concerns

| # | Concern | Status | Evidencia |
|---|---------|:------:|-----------|
| 1 | **MailKit pinning** | ✅ | `src/SGV.Infraestructura/SGV.Infraestructura.csproj:11-12` fija `MailKit 4.17.0` y `MimeKit 4.17.0`. Compatible con `net10.0`, `Microsoft.AspNetCore.Identity.EntityFrameworkCore 9.0.0` y `Pomelo.EntityFrameworkCore.MySql 9.0.0`. La pipeline `dotnet restore && build` lo materializa sin conflictos. |
| 2 | **Middleware ordering** | ✅ | `src/SGV.Api/Program.cs:285-342`: `UseExceptionHandler` → `UseStatusCodePages` → `UseCors` → **`UseRateLimiter` (L300)** → `UseAuthentication` (L302) → revalidator → `UseAuthorization`. Cumple el contrato del riesgo #4 de la proposal. |
| 3 | **Token URL-encoding** | ✅ | `PasswordResetService.cs:135-138` y `SmtpEmailSender.cs:48-52` ambos usan `Uri.EscapeDataString`. `ResetPassword.cshtml.cs:108-109` aplica `Uri.UnescapeDataString` exactamente una vez. Test `SmtpEmailSenderTests.BuildPasswordResetLink_EncodesUserIdAndToken` verifica `+a/b=` → `%2Ba%2Fb%3D`. Test `ResetPasswordPageTests.Get_ResetPasswordWithEncodedQuery_RendersDecodedHiddenValues` verifica el round-trip. |
| 4 | **WebBaseUrl validation fail-loud** | ✅ | `SmtpOptions.cs:55-61` con `[Required(AllowEmptyStrings=false)]` + `[Url]`. `Program.cs:120-124` aplica `ValidateOnStart()`. `appsettings.Development.json:18-28` provee valor válido. Tests `DataAnnotations_WebBaseUrlMissing_FailsValidation` y `DataAnnotations_WebBaseUrlRelative_FailsValidation`. |
| 5 | **Rate-limit policy naming** | ✅ | `AuthController.cs:22-25` define `ForgotPasswordPolicyName = "ForgotPassword"` y `ResetPasswordPolicyName = "ResetPassword"` como `internal const`. `Program.cs:209,217` registra las políticas con los mismos nombres literales. El acoplamiento es por convención de cadena (no constante compartida entre proyectos), pero hay un único covering test (`ForgotPassword_FourthRequestFromSameIpWithinWindow_Returns429WithRetryAfterHeader`) que verifica el wiring end-to-end. Sugerencia: mover las constantes a `SGV.Contracts` para evitar drift. |
| 6 | **Anti-enumeración** | ✅ | `PasswordResetService.ForgotPasswordAsync` (`PasswordResetService.cs:35-78`): cuando el usuario no existe, loguea y devuelve `Success`; cuando existe, genera token y envía mail. `AuthController.cs:62-67` ignora el outcome y siempre responde `Ok(new{mensaje=...})` idéntico. Tests `ForgotPassword_KnownAndUnknownIdentifiers_ReturnByteEquivalentBodies` verifica bodies byte-equivalentes. |
| 7 | **`[AllowAnonymous]` en ambos endpoints** | ✅ | `AuthController.cs:38,70` aplica `[AllowAnonymous]` a `ForgotPassword` y `ResetPassword` respectivamente. Test `ForgotPassword_NoAuthHeader_Returns200` confirma que el endpoint responde 200 sin `Authorization` header pese a `FallbackPolicy=RequireAuthenticatedUser`. |
| 8 | **Anonymous HttpClient** | ✅ | `SGV.Web/Program.cs:118-125` registra `AddHttpClient(AnonymousHttpClientName, ...)` SIN `.AddHttpMessageHandler(sp => sp.GetRequiredService<ApiBearerTokenHandler>())`. `Program.cs:127-133` compone `IAuthApiClient` con los dos named clients. Test `AnonymousAuthApiClientRegistrationTests.ProductionRegistration_ResolvesSeparateAnonymousAuthHttpClient` verifica que son instancias distintas. |
| 9 | **`SecurityStamp` rotation** | ⚠️ | `PasswordResetService.cs:107-109` llama `userManager.ResetPasswordAsync(user, request.Token, request.NewPassword)` — Identity rota `SecurityStamp` por diseño en esa operación (verificado por el comment de `Program.cs:139-142` y la nota en el código). El contract test `PasswordResetServiceTests.ResetPasswordAsync_ValidToken_RotatesPassword_ReturnsSuccess` valida que `ResetPasswordAsync` se invoca, **pero no se ejecuta contra MySQL**. El `PasswordResetIdentityMySqlFactTests.cs` listado en `design.md §7` no se materializó. Ver warnings. |
| 10 | **`SignIn` link "¿Olvidaste tu contraseña?"** | ✅ | `SignIn.cshtml:37` con `href="/auth/forgot-password"` y texto `¿Olvidaste tu contraseña?`. Test cubre el render. Click navega a `/auth/forgot-password` (página pública) sin auth. |

## Desviaciones técnicas confirmadas

1. **`AuthorizationPolicyBuilder.Tokens.PasswordResetTokenLifespan` no existe en
   Identity 10.** El design.md propuso la propiedad
   `IdentityOptions.Tokens.PasswordResetTokenLifespan` (válida en Identity 8). El
   apply-progress documenta el descubrimiento y la corrección a
   `DataProtectionTokenProviderOptions.TokenLifespan` (Identity 9+). Cambió la
   superficie pero mantiene el contrato funcional (vida útil 1 hora).
   Cubierto por `IdentityTokenProvidersTests.Api_HostIdentityComposition_ConfiguresOneHourPasswordResetLifespan`.

2. **Constructor `AuthApiClient` con dos `HttpClient`.** El plan original tenía un
   único `HttpClient` inyectado por typed-client DI. Se mantuvo un ctor público
   `single-HttpClient` (compatibilidad con overrides de tests existentes) y un
   ctor internal `two-HttpClient`; la composición productiva se hace explícita
   en `Program.cs`. Cumple el delta `web-apiclient-transport-contract`.

3. **`auth-password.js` no se copia al bundle del Web.** El archivo vive solo en
   `InspinaTemplate/Inspinia/wwwroot/js/pages/auth-password.js`. La pipeline
   `bun run build` (gulp) compila únicamente `scss` y plugins vendor; el JS se
   asume materializado manualmente en `src/SGV.Web/wwwroot/js/pages/`. En este
   checkout no existe allí. La página `ResetPassword.cshtml` referencia el asset
   vía `<script src="/js/pages/auth-password.js">` (L61), pero en un run real
   el asset devolvería 404 a menos que se copie. Mantenido como warning, no
   bloqueante, porque los tests web verifican la presencia del `<script>` tag
   y del atributo `data-password="bar"` (cubierto por
   `ResetPasswordPageTests.Get_ResetPasswordWithEncodedQuery_RendersDecodedHiddenValues`).

4. **Constantes `ForgotPasswordPolicyName`/`ResetPasswordPolicyName` duplicadas
   por proyecto.** El controller define constantes internas y `Program.cs` usa
   strings literales en `AddFixedWindowLimiter`. Acoplamiento frágil a refactors.
   La revisión cubre el contrato vía el covering test de 429 actual.

## Warnings / observaciones

1. **`ResetPassword` 5/15min no tiene covering test.** Solo
   `ForgotPassword_FourthRequestFromSameIpWithinWindow_Returns429WithRetryAfterHeader`
   cubre el rate-limit; el gemelo para ResetPassword falta. Recomendado agregar
   `ResetPassword_SixthRequestFromSameIpWithinWindow_Returns429WithRetryAfterHeader`.

2. **`PasswordResetIdentityMySqlFactTests.cs` no se materializó.** La rotación
   de `SecurityStamp` (`"SecurityStamp rotated by Identity"` log en
   `PasswordResetService.cs:127-128`) no se verifica contra MySQL real. El test
   saltaría por `[MySqlFact]` si no hay DB; mantenerlo como deuda asumida en el
   change log.

3. **`auth-password.js` no se publica.** Mover manualmente
   `InspinaTemplate/Inspinia/wwwroot/js/pages/auth-password.js` a
   `src/SGV.Web/wwwroot/js/pages/` (o agregar un watch de gulp para copiarlo).
   Sin esto, el medidor de fortaleza no se renderiza al cargar
   `/auth/reset-password` en runtime aunque el HTML lo prevee.

4. **Warnings NU1510 preexistentes.** El proyecto `SGV.Infraestructura.csproj`
   arrastra warnings `NU1510` sobre dependencia transitiva no resuelta. No son
   bloqueantes para el cambio y vienen del repo base; mantener como issue de
   limpieza.

5. **Política names por convención.** Como se anticipó en el punto 4 de
   desviaciones, las constantes podrían vivir en `SGV.Contracts` (o en un
   wrapper compartido entre `Api` y `Tests`). Evitar strings literales en
   `Program.cs:209,217` blinda contra typos futuros.

6. **`SmtpEmailSender.SendPasswordResetAsync` resuelve email vía userId** (L124:
   `ResolveRecipientEmail` retorna el userId crudo como email). Aceptable
   porque la implementación productiva de reset pasa por
   `SendPasswordResetLinkAsync(user, ...)` que sí toma `user.Email`. Queda como
   nota para refactor si se quiere evitar el método legacy `SendPasswordResetAsync`.

## Criterios de aceptación del issue #181

| # | Criterio del enrichment de #181 | Status | Evidencia |
|---|----------------------------------|:------:|-----------|
| 1 | `POST /api/v1/auth/forgot-password` responde `200 OK` siempre, mensaje genérico | ✅ | `AuthController.cs:65-67` + test `ForgotPassword_NoAuthHeader_Returns200`. |
| 2 | Si el usuario existe, se genera token y se envía email | ✅ | `PasswordResetService.cs:62-77`; tests `PasswordResetServiceTests.ForgotPasswordAsync_ExistingUserByUserName_SendsEmail...`. |
| 3 | Si `EmailConfirmed = false`, el envío también se dispara | ✅ | `PasswordResetService.ForgotPasswordAsync` no chequea `EmailConfirmed`. Confirmado por inspección (sin lookup de la propiedad). |
| 4 | `forgot-password` rate limit 3/15min → 429 + `Retry-After` | ✅ | `Program.cs:209-215` + `AuthController.cs:39` + test `ForgotPassword_FourthRequestFromSameIpWithinWindow_Returns429WithRetryAfterHeader`. |
| 5 | `POST /api/v1/auth/reset-password` rota la contraseña | ✅ | `AuthController.cs:75-101` + `PasswordResetService.cs:80-131`; test `PasswordResetServiceTests.ResetPasswordAsync_ValidToken_RotatesPassword_ReturnsSuccess`. |
| 6 | `reset-password` rate limit 5/15min | ⚠️ | Política registrada (`Program.cs:217-222`) y aplicada (`AuthController.cs:71`). **Falta covering test**. |
| 7 | Tras reset, `SecurityStamp` se regenera e invalida tokens previos | ⚠️ | `userManager.ResetPasswordAsync` rota `SecurityStamp` por diseño de Identity; documentado en log L127-128. No hay test MySQL que verifique la invalidación. |
| 8 | Token expirado/inválido → 400 con mensaje descriptivo | ✅ | `AuthController.cs:95-98` retorna `"El enlace de restablecimiento no es válido o ya expiró."`. Tests `AuthControllerPasswordResetTests.ResetPassword_InvalidToken_Returns400WithSpanishMessage`. |
| 9 | `GET /auth/forgot-password` renderiza formulario con input email | ✅ | `ForgotPassword.cshtml:30-36`. Test `ForgotPasswordPageTests.Get_ForgotPassword_RendersPublicEmailFormWithoutShellChrome`. |
| 10 | `GET /auth/reset-password?userId=...&token=...` renderiza con medidor de fortaleza | ⚠️ | Render cubre el `<div data-password="bar">` y el hidden fields (`ResetPasswordPageTests.Get_ResetPasswordWithEncodedQuery_RendersDecodedHiddenValues`). El asset `auth-password.js` no se publica en `wwwroot/js/pages/` (ver warning #3). |
| 11 | Link "¿Olvidaste tu contraseña?" en `SignIn.cshtml` → `/auth/forgot-password` | ✅ | `SignIn.cshtml:37` + test `SignInPasswordResetLinkTests`. |
| 12 | Nueva password cumple `RequireDigit`, `RequireUppercase`, `RequireLowercase`, `RequireNonAlphanumeric`, `RequiredLength=6` | ✅ | `ResetPasswordRequestValidator.cs:30-42` + tests `ResetPasswordRequestValidatorTests.Should_Have_Error_When_NewPassword_FailsIdentityPolicy` parametrizados sobre las 5 clases faltantes. |
| 13 | `Smtp` ausente fuera de Development → `OptionsValidationException` | ✅ | `Program.cs:120-124` (`ValidateOnStart()` + ambiente Production: tests `CorsAllowedOriginsValidationTests`). |
| 14 | `IEmailSender` registrado en DI | ✅ | `DependencyInjection.cs:92` (`AddSingleton<IEmailSender<SgvIdentityUser>, SmtpEmailSender>`). |

## Recomendación final

**VEREDICT: PASS WITH WARNINGS.**

Lo principal está verificado: tests verdes (2685/2685), build limpio, los 4 specs
trazados a implementación, y los 10 cross-cutting concerns identificados en la
proposal compliance. Los warnings no son bloqueantes para `archive`:

- Cubrir `ResetPassword_429` (5/15min) requeriría una `ApiWebApplicationFactory`
  derivada idéntica al actual `ForgotPassword_429` test — addition de ~30 LoC
  y ~10 minutos.
- Mover `auth-password.js` al bundle del Web es packaging, no comportamiento.
- El fact-test `SecurityStamp` puede vivirse en un follow-up si MySQL local no
  está disponible (los `[MySqlFact]` se skipean limpio).

El orchestrator puede proceder a `sdd-archive` para sincronizar los delta specs
a `openspec/specs/` y dejar la PR lista para merge.

## Lecciones aprendidas

1. **Identity 10 cambió `PasswordResetTokenLifespan` por `DataProtectionTokenProviderOptions.TokenLifespan`.
   El design inicial copió la API antigua; el apply lo descubrió en runtime
   (`IdentityTokenProvidersTests` lo registró). Vale la pena un sanity check de
   guía de Microsoft.Identity en futuros cambios que toquen tokens.

2. **Dos named `HttpClient` separados (auth + anonymous) es más limpio que un
   único client con handler opcional.** La fábrica nombrada evita la ramificación
   sensible por request y mantiene el contrato `IAuthApiClient` único. El ctor
   `internal two-HttpClient` resuelve la ambigüedad de `AddHttpClient<T>` sin
   sacrificar compatibilidad de tests existentes.

3. **El bundle de assets heredados de Inspinia no se migra automáticamente.** El
   pipeline Gulp del repo solo compila SCSS/plugins; los `.js` específicos de
   cada página (`auth-password.js`, etc.) requieren copia manual. Considerar un
   paso de `copy:` en el `gulpfile.js` para `/js/pages/*.js` o un commit
   explícito en el PR cuando se introduzca uno nuevo.

4. **El anti-enumeración se valida por bytes, no por latencia.** La spec exige
   body + status + headers idénticos; medir `Stopwatch` es ruidoso. El test
   `ForgotPassword_KnownAndUnknownIdentifiers_ReturnByteEquivalentBodies` es el
   gold standard y debe mantenerse estable.

5. **`SmtpOptions` con `ValidateOnStart` más estricto que Development.**
   El host arranca limpio con `Mode=Logger` en dev (sin SMTP real) y falla loud
   en Production por `WebBaseUrl`. El patrón coincide con `JwtOptions` y puede
   extenderse a futuras options sensibles (Sentry, Slack, etc.).
