# Especificación de Password Reset Web (UI)

## Propósito

Definir la capa UI de recuperación de credenciales en `SGV.Web`
(Razor Pages). Cubre los formularios `ForgotPassword` y `ResetPassword`,
la propagación de `429 Too Many Requests` desde la API al usuario, el
manejo de los query string params `userId` y `token` (con URL-decoding),
y la coherencia lingüística del copy en español neutro/profesional.

## Requisitos

### Requirement: Página ForgotPassword pública

`SGV.Web/Pages/Auth/ForgotPassword.cshtml` MUST ser una página anónima
renderizada en `/auth/forgot-password` con layout separado del shell
(Inspinia Auth). MUST incluir un input para email/username y un botón
"Enviar enlace". El copy MUST estar en español neutro/profesional.
Ningún endpoint autenticado es necesario para operar la página.

#### Scenario: GET renderiza el formulario

- **DADO** un usuario no autenticado
- **CUANDO** navega a `/auth/forgot-password`
- **ENTONCES** MUST renderizar el formulario con input de email
- **Y** la página MUST renderizarse sin sidebar ni topbar del shell.

#### Scenario: POST exitoso muestra confirmación genérica

- **DADO** el formulario completo con email válido
- **CUANDO** el usuario envía el form
- **ENTONCES** MUST llamar a `IAuthApiClient.ForgotPasswordAsync`
- **Y** la página MUST mostrar un mensaje en español tipo
  "Si el email existe, recibirás un enlace para restablecer tu contraseña"
  (sin confirmar ni negar existencia del usuario).

### Requirement: Página ResetPassword pública con token en query string

`SGV.Web/Pages/Auth/ResetPassword.cshtml` MUST ser una página anónima
renderizada en `/auth/reset-password?userId=...&token=...` con layout
separado del shell. El PageModel MUST URL-decodear `userId` y `token`
(`Uri.UnescapeDataString`) **antes** de invocar la API. El formulario
MUST incluir dos inputs de contraseña (nueva + confirmación) y el widget
de fortaleza `data-password="bar"` reutilizando
`InspiniaTemplate/.../auth-password.js`.

#### Scenario: GET carga el formulario con widget de fortaleza

- **DADO** una URL `/auth/reset-password?userId=abc&token=%2Ba%2Fb%3D`
- **CUANDO** el usuario abre la URL
- **ENTONCES** MUST renderizar el formulario con widget
  `data-password="bar"`
- **Y** los campos hidden `userId` y `token` MUST contener los valores
  URL-decoded (`%2Ba%2Fb%3D` → `+a/b=`).

#### Scenario: POST exitoso redirige a SignIn

- **DADO** un usuario con token vigente y nueva contraseña válida
- **CUANDO** envía el formulario
- **ENTONCES** MUST llamar a `IAuthApiClient.ResetPasswordAsync`
- **Y** MUST redirigir a `/auth/sign-in` con TempData de éxito en español.

#### Scenario: Token inválido muestra error controlado

- **DADO** un `token` manipulado o expirado (>1 h)
- **CUANDO** envía el formulario
- **ENTONCES** MUST mostrar un mensaje de error en español
- **Y** MUST permanecer en `/auth/reset-password` sin redirigir.

### Requirement: SignIn expone enlace "¿Olvidaste tu contraseña?"

`SGV.Web/Pages/Auth/SignIn.cshtml` MUST incluir un enlace visible
"¿Olvidaste tu contraseña?" apuntando a `/auth/forgot-password`. El
enlace MUST estar fuera del flujo principal de submit de credenciales
pero ser accesible desde la misma pantalla de login.

#### Scenario: SignIn renderiza el enlace

- **DADO** un usuario anónimo en `/auth/sign-in`
- **CUANDO** la página se renderiza
- **ENTONCES** MUST incluir un ancla visible con texto
  "¿Olvidaste tu contraseña?"
- **Y** `href` MUST ser `/auth/forgot-password`.

### Requirement: Propagación de 429 al usuario con retry copy en español

Ambos PageModels (`ForgotPasswordModel`, `ResetPasswordModel`) MUST
capturar `HttpRequestException` con status code `429` propagado por
`AuthApiClient` y mostrar un mensaje en español con instrucción de
reintento. MUST NO redirigir a `/Error` ni perder el contenido ya
ingresado por el usuario.

#### Scenario: 429 en ForgotPassword muestra mensaje de reintento

- **DADO** que `AuthApiClient.ForgotPasswordAsync` recibió `429`
- **CUANDO** el endpoint retorna al PageModel
- **ENTONCES** MUST mostrarse en español: "Hiciste demasiados intentos.
  Esperá unos minutos antes de volver a intentarlo."
- **Y** el email capturado MUST permanecer en el input.

### Requirement: Errores de transporte en español sin redirigir

Ambos PageModels MUST capturar `HttpRequestException` (red/DNS) y
`TaskCanceledException` (timeout) propagados por `AuthApiClient` y
mostrar mensajes en español neutro/profesional. El usuario MUST
permanecer en la página (`/auth/forgot-password` o
`/auth/reset-password`) — nunca redirigir a `/Error`.

#### Scenario: HttpRequestException en ForgotPassword

- **DADO** que la API es inalcanzable
- **CUANDO** `AuthApiClient.ForgotPasswordAsync` lanza `HttpRequestException`
- **ENTONCES** MUST mostrarse: "No se pudo conectar con el servidor.
  Verificá tu conexión y volvé a intentar."

#### Scenario: TaskCanceledException no cancelada por caller

- **DADO** que la API no responde dentro del timeout del cliente
- **Y** el `CancellationToken` NO fue cancelado por el usuario
- **CUANDO** `AuthApiClient.ForgotPasswordAsync` lanza `TaskCanceledException`
- **ENTONCES** MUST mostrarse: "El servidor tardó demasiado en
  responder. Volvé a intentar en unos segundos."

## Fuera de alcance

- Catálogo de motivos de error diferenciados en UI (un único genérico
  en español basta para el copy de recuperación).
- Animaciones o UI enriquecida post-submit.
- Cambio de password autenticado (flujo distinto).
- Internacionalización (UI en español únicamente).
