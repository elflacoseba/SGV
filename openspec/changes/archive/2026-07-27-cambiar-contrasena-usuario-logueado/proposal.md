# Proposal: Cambiar contraseña de usuario logueado

> Change: `2026-07-27-cambiar-contrasena-usuario-logueado` · Issue: #204 · Idioma: español
> Sin `exploration.md`: el contexto completo está provisto por la issue #204 enriquecida.

## Resumen

Habilitar a un usuario ya autenticado para que cambie su propia contraseña desde
la UI web. El flujo MUST ser [Authorize] (no es recovery), exigir la contraseña
actual, validar contra la política de Identity y cerrar la sesión activa
rotando el `SecurityStamp` para invalidar cookie, JWT vigente y tokens en otras
ventanas/dispositivos.

## Motivación y problema

Hoy un usuario logueado no tiene cómo rotar su credencial desde la shell web.
El camino de recuperación por email (`forgot-password` / `reset-password`)
existe pero NO aplica: es `[AllowAnonymous]`, no exige la contraseña actual y
depende de un token de un solo uso. Esta gap fuerza a support/admin a
ejecutar `reset-password` por el usuario, lo que rompe ownership de la
credencial y deja al propio usuario sin acción directa.

## Alcance

### In scope (entregables)

- `record ChangePasswordRequest(string CurrentPassword, string NewPassword, string ConfirmPassword)` en `src/SGV.Contracts/Seguridad/Usuarios/UsuarioContracts.cs`.
- `AuthApiRoutes.ChangePasswordRelative` + `AuthApiRoutes.ChangePassword` + `AuthApiRoutes.ChangePasswordPolicyName` en `src/SGV.Contracts/Auth/AuthApiRoutes.cs`.
- `IChangePasswordService` en `src/SGV.Aplicacion/Seguridad/PasswordChange/` (carpeta nueva, SRP, separada del recovery flow).
- `ChangePasswordService` en `src/SGV.Infraestructura/Seguridad/` que use `UserManager.ChangePasswordAsync` + `UpdateSecurityStampAsync`.
- `ChangePasswordRequestValidator` (FluentValidation) en `src/SGV.Aplicacion/Seguridad/PasswordChange/` espejando la política de `IdentityOptions.Password` (≥6, lower, upper, digit, no alfanum).
- Endpoint `POST /api/v1/auth/change-password` en `AuthController`, `[Authorize]`, con política rate limit `ChangePassword` (5 req / 15 min / usuario, fixed window).
- `IAuthApiClient.ChangePasswordAsync` (autenticado, usa `httpClient` cubierto por `ApiBearerTokenHandler`) + implementación en `AuthApiClient`.
- Nueva Razor Page `/auth/cambiar-contrasena` (`Pages/Auth/CambiarContrasena.cshtml` + `.cshtml.cs`) con GET (form) y POST (handler).
- Ítem "Cambiar Contraseña" en `src/SGV.Web/Pages/Shared/Partials/_Topbar.cshtml` antes del form de logout (línea 68), ícono `ti ti-key`.
- En éxito: `SignOutAsync` + `LocalRedirect("/auth/sign-in")` con `TempData["PasswordChangeMessage"]` similar al patrón de `ResetPassword.cshtml.cs`.

### Out of scope

- MFA / 2FA.
- Rate limiting por IP (la cuota es por usuario autenticado).
- Historial de contraseñas (no hay tabla de auditoría de contraseñas previa).
- Forzar cambio de contraseña en próximo login.
- Notificación por email del cambio.
- Migración de BD (no se requiere; `SecurityStamp` ya existe en `AspNetUsers`).

## Criterios de aceptación

1. El usuario autenticado ve "Cambiar Contraseña" en el dropdown del topbar, **antes** de "Cerrar Sesión".
2. Click navega a `/auth/cambiar-contrasena` con formulario: contraseña actual, nueva, confirmación.
3. La nueva contraseña MUST cumplir la política de Identity. Se reutiliza `auth-password.js` (selector `[data-password="bar"]`) para mostrar fortaleza.
4. Si la contraseña actual es incorrecta → 400 con mensaje genérico en español, sin revelar si el usuario existe.
5. Si el cambio es exitoso → 200 + `SignOutAsync` en la Web → redirect a `/auth/sign-in` con `TempData` de confirmación. El `SecurityStamp` rotado invalida cookies y JWT vigente.
6. Usuario no autenticado accediendo a `/auth/cambiar-contrasena` → redirect a `/auth/sign-in`.

## Enfoque

Patrón espejo del recovery flow (`IPasswordResetService`/`PasswordResetService`/`AuthController`):

1. **Contracts** — `ChangePasswordRequest` + constantes de ruta + nombre de política rate limit.
2. **Aplicación** — `IChangePasswordService` con método `ChangePasswordAsync(userId, request, ct)` que devuelve `ChangePasswordOutcome` (Success | InvalidCurrentPassword | ValidationFailed | RateLimited). Separar del `IPasswordResetService` por SRP.
3. **Infraestructura** — Implementación con `UserManager<SgvIdentityUser>`: `ChangePasswordAsync` + `UpdateSecurityStampAsync`. Mapea `PasswordMismatch` → `InvalidCurrentPassword`.
4. **API** — `[HttpPost(ChangePasswordRelative)] [Authorize] [EnableRateLimiting(ChangePasswordPolicyName)]` con `IValidator<ChangePasswordRequest>`.
5. **Web Integration** — Método `ChangePasswordAsync` en `IAuthApiClient` usando el HTTP client autenticado (no `anonymousHttpClient`).
6. **Web Pages** — Razor Page con `[Authorize]`, `BindProperty InputModel`, `OnGetAsync` (render form), `OnPostAsync` (POST → API → `SignOutAsync` + `LocalRedirect`). Reutilizar `MeetsPasswordPolicy` (cliente) complementado por el validador FluentValidation (server).
7. **UI** — En `_Topbar.cshtml`, insertar `<a href="/auth/cambiar-contrasena">` con `ti ti-key` antes del form de logout.

## No-objetivos

- No introducir un `IPasswordChanger` genérico (la operación es específica de "logueado") — el nombre `IChangePasswordService` comunica la intención.
- No cambiar `IdentityOptions.Password` ni sus reglas.
- No relajar `[Authorize]` a `[AllowAnonymous]`.
- No exponer endpoints de admin para cambiar contraseñas de terceros en este cambio.

## Dependencias

- `UserManager<SgvIdentityUser>` (Identity): `ChangePasswordAsync`, `UpdateSecurityStampAsync`.
- `ApiBearerTokenHandler` (ya en `SGV.Web/Integration/Auth/`): cubre el JWT en `IAuthApiClient.ChangePasswordAsync`.
- `CookiePrincipalRevalidator` (`src/SGV.Web/Auth/`): invalida la cookie vigente al rotar `SecurityStamp`.
- `LogoutModel.OnPostAsync` (`src/SGV.Web/Pages/Auth/Logout.cshtml.cs`): patrón de `SignOutAsync` + `LocalRedirect`.
- `wwwroot/js/pages/auth-password.js`: indicador de fortaleza vía `data-password="bar"`.
- `TempData["PasswordResetMessage"]` (patrón vigente en `SignIn.cshtml`): usar `TempData["PasswordChangeMessage"]` para el mensaje de éxito post-cambio.
- FluentValidation: registración análoga a `ForgotPasswordRequestValidator`.

## Riesgos y suposiciones

| Riesgo | Probabilidad | Mitigación |
|---|---|---|
| Race entre `ChangePasswordAsync` y `UpdateSecurityStampAsync` permite cookie zombie | Baja | Web hace `SignOutAsync` explícito; `CookiePrincipalRevalidator` rechaza si el stamp no coincide. |
| Endpoint sin rate limit expuesto a brute force de contraseña actual | Media | Política `ChangePassword` (5 req / 15 min / usuario) en `AddRateLimiter` de `SGV.Api/Program.cs`. |
| `UpdateSecurityStampAsync` no invocado → sesiones sobreviven | Baja | Test de integración cubre que `SecurityStamp` cambia después del POST exitoso. |
| `ConfirmPassword` no se valida en cliente y llega al server | Baja | Validator FluentValidation verifica coincidencia; `MeetsPasswordPolicy` en PageModel como primera barrera. |
| Icono `ti ti-key` no disponible en el bundle Inspinia | Baja | Si falla, fallback a `ti ti-lock` (ya usado en `Pages/Seguridad/Usuarios`). |

## Estrategia de pruebas

Anclada en `strict_tdd: true` y los patrones vigentes.

- **Unit (Aplicación)**: `ChangePasswordRequestValidator` cubre: campos requeridos, política de password, coincidencia `NewPassword == ConfirmPassword`. `IChangePasswordService` con `FakeUserManager` para mapear `PasswordMismatch` → `InvalidCurrentPassword`.
- **Unit (Infraestructura)**: `ChangePasswordService` contra `UserManager` real con `MySqlFact` verifica rotación de `SecurityStamp` tras éxito.
- **Integración API** (`AuthControllerChangePasswordTests`, estilo `AuthControllerPasswordResetTests`): 401 sin auth, 400 con `CurrentPassword` inválida, 400 con política débil, 200 y rotación de stamp en éxito, 429 fuera de cuota.
- **Integración Web** (`AuthApiClientChangePasswordTests`, estilo `AuthApiClientPasswordResetTests`): el cliente autenticado envía `Authorization: Bearer`, ruta correcta, mapea 400→`InvalidCurrentPassword` y 429→`RateLimited`.
- **Web razor tests** (`CambiarContrasenaPageTests`, estilo `ResetPasswordPageTests`): GET autenticado renderiza form, GET anónimo redirige a login, POST exitoso hace `SignOutAsync` + redirect a sign-in con `TempData`, POST con `CurrentPassword` inválida muestra error sin revelar detalles.
- **Smoke web** (`WebShellSmokeTests`): el ítem del topbar aparece cuando el usuario está autenticado.

## Impacto en la arquitectura

Clean Architecture estrictamente respetada:

```
Contracts  ──►  Aplicacion  ──►  Infraestructura  ──►  Api  ──►  Web
   ▲                                                                  │
   └──────────────── wire-types (records + constantes) ──────────────┘
```

- `SGV.Contracts` crece con un record + tres constantes. No depende de nadie.
- `SGV.Aplicacion/Seguridad/PasswordChange/` es nueva y solo depende de `SGV.Dominio`/`SGV.Contracts`.
- `SGV.Infraestructura` agrega `ChangePasswordService` (depende de `IChangePasswordService` + `UserManager<SgvIdentityUser>`).
- `SGV.Api` registra `IChangePasswordService` + validador en DI (`AddInfraestructuraServicios` ya está separado y se respeta); añade `AuthController.ChangePassword`.
- `SGV.Web` consume vía `IAuthApiClient` y renderiza la Razor Page. **No** referencia `SGV.Api` (regla vigente). El bridge cookie→JWT sigue intacto.

## Notas de seguridad

- **No es recovery flow**: `[Authorize]` obligatorio, exige `CurrentPassword`.
- **Invalidación de `SecurityStamp`**: tras `ChangePasswordAsync` exitoso, `UpdateSecurityStampAsync` rota el stamp. `CookiePrincipalRevalidator` rechaza la cookie vigente en la próxima request. `ApiBearerTokenHandler` también rechaza el JWT porque el `sub` ya no matchea con un usuario vigente.
- **Rate limiting por usuario autenticado**: 5 req / 15 min, alineado con `ResetPasswordPolicyName` pero aplicado después de `[Authorize]` (no por IP).
- **Mensajes uniformes**: respuestas genéricas en español ("No se pudo cambiar la contraseña" / "Verificá los datos e intentá de nuevo") para no filtrar si el usuario existe o si la password es la única causa del fallo.
- **SignOut explícito en Web**: la Razor Page ejecuta `SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme)` antes de `LocalRedirect("/auth/sign-in")` para una UX consistente aunque la cookie ya hubiera sido rechazada.
- **Sin token en URL**: la request no transporta tokens en query string; sólo viaja el body `Json` por HTTPS.
- **Política de password sincronizada**: cliente (`MeetsPasswordPolicy`), validator (`ChangePasswordRequestValidator`) y runtime (`IdentityOptions.Password`) reflejan la misma regla. Si el validator se desincroniza, la prueba de integración falla porque Identity rechaza la nueva password con `400 Bad Request`.

## Plan de implementación (orden preliminar)

1. Contracts: `ChangePasswordRequest` + constantes de ruta en `AuthApiRoutes.cs`.
2. Aplicación: `IChangePasswordService` + `ChangePasswordRequestValidator` + enum `ChangePasswordOutcome`.
3. Infraestructura: `ChangePasswordService` + registro en `AddInfraestructuraServicios`.
4. API: endpoint en `AuthController` + política `ChangePassword` en `AddRateLimiter`.
5. Web Integration: `IAuthApiClient.ChangePasswordAsync` + `AuthApiClient` implementación.
6. Web Pages: `CambiarContrasena.cshtml(.cs)` + ítem en `_Topbar.cshtml` + `TempData` en `SignIn.cshtml`.
7. Tests: unit + integración API + integración Web + smoke.
