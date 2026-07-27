# Especificación de Password Change Web (UI autenticada)

## Propósito

Definir la capa UI en `SGV.Web` (Razor Pages) para que un usuario ya
autenticado cambie su propia contraseña. Cubre la página
`/auth/cambiar-contrasena` con `[Authorize]`, el ítem "Cambiar Contraseña"
en el dropdown del topbar (antes de "Cerrar Sesión"), la propagación del
resultado de `IAuthApiClient.ChangePasswordAsync` al `ModelState` y el
banner de éxito tras el `LocalRedirect("/auth/sign-in")`. Es **NO** un
recovery flow: la cookie vigente se invalida explícitamente porque la
rotación del `SecurityStamp` en la API rechaza el JWT en curso.

## Requisitos

### Requirement: Página CambiarContraseña autenticada

`SGV.Web/Pages/Auth/CambiarContrasena.cshtml` MUST ser una Razor Page con
`@page "/auth/cambiar-contrasena"`, marcada con `[Authorize]` y
`[AutoValidateAntiforgeryToken]`. El formulario MUST incluir tres inputs:
`CurrentPassword`, `NewPassword` y `ConfirmPassword`. El input
`NewPassword` MUST exponer el widget `data-password="bar"` que reutiliza
`wwwroot/js/pages/auth-password.js` para mostrar la fortaleza.

#### Scenario: GET autenticado renderiza el formulario

- **DADO** un usuario autenticado
- **CUANDO** navega a `/auth/cambiar-contrasena`
- **ENTONCES** MUST responder `200 OK` con el form renderizado conteniendo
  los inputs `CurrentPassword`, `NewPassword` y `ConfirmPassword`
- **Y** el input `NewPassword` MUST contener el atributo `data-password="bar"`.

#### Scenario: GET sin autenticación redirige a login

- **DADO** un usuario no autenticado
- **CUANDO** navega a `/auth/cambiar-contrasena`
- **ENTONCES** MUST redirigir a `/auth/sign-in`.

### Requirement: Ítem "Cambiar Contraseña" en el dropdown del topbar

`SGV.Web/Pages/Shared/Partials/_Topbar.cshtml` MUST incluir un
`<a href="/auth/cambiar-contrasena">` con texto "Cambiar Contraseña"
ubicado **antes** del `<form>` de logout existente en el dropdown del
usuario autenticado. El ítem MUST NO renderizarse cuando el usuario es
anónimo (el dropdown autenticado no se muestra en sign-in).

#### Scenario: Dropdown autenticado expone el ítem antes de "Cerrar Sesión"

- **DADO** un usuario autenticado navegando cualquier página con topbar
- **CUANDO** se renderiza el HTML
- **ENTONCES** MUST contener un ancla con `href="/auth/cambiar-contrasena"`
  y texto "Cambiar Contraseña"
- **Y** ese ancla MUST aparecer en el HTML antes del `<form>` de logout.

#### Scenario: Dropdown no expone el ítem para usuarios anónimos

- **DADO** un usuario no autenticado en `/auth/sign-in`
- **CUANDO** se renderiza la página
- **ENTONCES** el topbar MUST NO contener el ancla `/auth/cambiar-contrasena`
  con texto "Cambiar Contraseña".

### Requirement: POST de la Razor Page cierra sesión al éxito

`CambiarContrasenaModel.OnPostAsync` MUST llamar a
`IAuthApiClient.ChangePasswordAsync` con un `ChangePasswordRequest`
construido desde `Input`. En `ChangePasswordOutcome.Success` MUST ejecutar
`HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme)`,
seteaar `TempData["PasswordChangeMessage"]` con un mensaje en español y
MUST redirigir vía `LocalRedirect("/auth/sign-in")`. En outcomes no
exitosos MUST permanecer en la página con `ModelState` poblado en español.

#### Scenario: POST exitoso cierra sesión y redirige a sign-in

- **DADO** un usuario autenticado con `CurrentPassword` válida y
  `NewPassword` que cumple la política
- **CUANDO** envía `POST /auth/cambiar-contrasena`
- **ENTONCES** MUST invocar `SignOutAsync` con el esquema de cookie
- **Y** MUST setear `TempData["PasswordChangeMessage"]` con texto en español
- **Y** MUST ejecutar `LocalRedirect("/auth/sign-in")`.

#### Scenario: POST con CurrentPassword inválida muestra error en español

- **DADO** un usuario autenticado
- **CUANDO** envía `POST /auth/cambiar-contrasena` con `CurrentPassword`
  incorrecta
- **ENTONCES** la API MUST retornar `ChangePasswordOutcome.InvalidCurrentPassword`
- **Y** la página MUST re-renderizar con `ModelState` error en
  `Input.CurrentPassword`
- **Y** el mensaje MUST ser "La contraseña actual no es correcta."

#### Scenario: POST con RateLimited muestra mensaje de reintento

- **DADO** un usuario autenticado cuya API devuelve 429
- **CUANDO** envía `POST /auth/cambiar-contrasena`
- **ENTONCES** MUST mostrarse "Hiciste demasiados intentos. Esperá unos
  minutos antes de volver a volver a intentarlo." en español.

#### Scenario: POST con API caída muestra error de transporte

- **DADO** la API inalcanzable (`HttpRequestException` o
  `TaskCanceledException`)
- **CUANDO** `CambiarContrasenaModel.OnPostAsync` procesa el fallo
- **ENTONCES** MUST renderizar la página con `ModelState` error en español
  "No se pudo conectar con el servidor. Verificá tu conexión y volvé a
  intentar." (transporte) o "El servidor tardó demasiado en responder.
  Volvé a intentar en unos segundos." (timeout).

#### Scenario: POST con cookie vencida redirige a sign-in sin re-render

- **DADO** un usuario cuya cookie venció mientras escribía el formulario
  (la API responde 401)
- **CUANDO** `CambiarContrasenaModel.OnPostAsync` recibe la excepción
- **ENTONCES** MUST ejecutar `LocalRedirect("/auth/sign-in")` sin
  re-renderizar el formulario.

### Requirement: Cliente HTTP tipado autenticado

`IAuthApiClient.ChangePasswordAsync` MUST usar el `HttpClient` autenticado
(cubierto por `ApiBearerTokenHandler`), NO el `anonymousHttpClient`. La
implementación en `AuthApiClient` MUST mapear `400 → InvalidCurrentPassword`,
`429 → RateLimited`, `2xx → Success`, y propagar `HttpRequestException` y
`TaskCanceledException` nativas según
`web-apiclient-transport-contract`. MUST NO usar `CommandResultMapper.Map`
(la familia `AuthApiClient` está exceptuada).

#### Scenario: IAuthApiClient.ChangePasswordAsync envía POST al endpoint

- **DADO** un usuario autenticado en `SGV.Web`
- **CUANDO** el PageModel invoca `IAuthApiClient.ChangePasswordAsync` con
  un `ChangePasswordRequest`
- **ENTONCES** MUST enviar `POST AuthApiRoutes.ChangePassword` con body
  `ChangePasswordRequest` serializado vía `System.Text.Json`
- **Y** el request MUST incluir el header `Authorization: Bearer <jwt>`
  provisto por `ApiBearerTokenHandler`.

#### Scenario: CambioPasswordAsync mapea 400 a InvalidCurrentPassword

- **DADO** la API responde `400 Bad Request`
- **CUANDO** el cliente procesa la respuesta
- **ENTONCES** MUST retornar `ChangePasswordOutcome.InvalidCurrentPassword`.

#### Scenario: CambioPasswordAsync mapea 429 a RateLimited

- **DADO** la API responde `429 Too Many Requests`
- **CUANDO** el cliente procesa la respuesta
- **ENTONCES** MUST retornar `ChangePasswordOutcome.RateLimited`.

#### Scenario: CambioPasswordAsync propaga HttpRequestException nativa

- **DADO** el pipeline HTTP finaliza con `HttpRequestException`
- **CUANDO** el cliente procesa la falla
- **ENTONCES** la excepción MUST propagarse al PageModel sin traducción a
  `ChangePasswordOutcome`.

### Requirement: Banner de éxito post-cambio en SignIn

`SGV.Web/Pages/Auth/SignIn.cshtml` MUST renderizar un bloque que muestre
`TempData["PasswordChangeMessage"]` cuando esté presente. El bloque MUST
coexistir con el banner vigente de `TempData["PasswordResetMessage"]` sin
interferir visualmente con él y MUST estar en español.

#### Scenario: SignIn muestra banner tras cambio exitoso

- **DADO** un visitante navegando a `/auth/sign-in` con
  `TempData["PasswordChangeMessage"]` seteado
- **CUANDO** la página se renderiza
- **ENTONCES** MUST incluir el banner con el texto de éxito en español.

## Fuera de alcance

- Cambio de contraseña para terceros (endpoint admin).
- MFA / 2FA en el flujo de cambio.
- Re-autenticación previa (Step-up auth) — la `CurrentPassword` cumple ese rol.
- Animaciones o UI enriquecida post-submit.
- Internacionalización (UI en español únicamente).
- Migraciones de BD (`SecurityStamp` ya existe en `AspNetUsers`).