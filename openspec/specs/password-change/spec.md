# Especificación de Password Change (backend autenticado)

## Propósito

Definir el flujo backend de cambio de contraseña para un usuario ya
autenticado en `SGV.Api`, expuesto vía `POST /api/v1/auth/change-password`
con `[Authorize]`. A diferencia del recovery flow (`password-reset-flow`),
este endpoint exige la contraseña actual, rota el `SecurityStamp` para
invalidar cookie + JWT vigente, y aplica rate limiting por usuario
autenticado (5 req / 15 min) — no por IP — para acotar brute force sobre la
credencial actual.

## Requisitos

### Requirement: Endpoint autenticado de cambio de contraseña

`SGV.Api` MUST exponer `POST /api/v1/auth/change-password` marcado con
`[Authorize]` y `[EnableRateLimiting(AuthApiRoutes.ChangePasswordPolicyName)]`.
El endpoint MUST delegar en `IChangePasswordService.ChangePasswordAsync(userId,
request, ct)`, MUST validar el body con `IValidator<ChangePasswordRequest>`
(Mirror de `ResetPasswordRequestValidator`: `CurrentPassword` no vacío,
`NewPassword` ≥6 + minúscula + mayúscula + dígito + símbolo, `ConfirmPassword`
igual a `NewPassword`) y MUST responder `200 OK` con mensaje en español
solamente cuando `ChangePasswordOutcome.Success`.

#### Scenario: POST exitoso cambia la contraseña y rota el SecurityStamp

- **DADO** un usuario autenticado con `CurrentPassword` válida y `NewPassword`
  que cumple la política
- **CUANDO** envía `POST /api/v1/auth/change-password`
- **ENTONCES** MUST responder `200 OK` con mensaje en español
- **Y** `UserManager.ChangePasswordAsync` MUST haber rotado la credencial
- **Y** `UserManager.UpdateSecurityStampAsync` MUST haber rotado el
  `SecurityStamp` (observable en `AspNetUsers.SecurityStamp`).

#### Scenario: POST sin autenticación es rechazado

- **DADO** un usuario no autenticado
- **CUANDO** envía `POST /api/v1/auth/change-password`
- **ENTONCES** MUST responder `401 Unauthorized`.

#### Scenario: POST con CurrentPassword incorrecta

- **DADO** un usuario autenticado
- **CUANDO** envía `POST /api/v1/auth/change-password` con `CurrentPassword`
  que NO coincide con la credencial vigente
- **ENTONCES** MUST responder `400 Bad Request` con mensaje genérico en
  español sin revelar detalles de la credencial real.

#### Scenario: POST con NewPassword que no cumple la política

- **DADO** un usuario autenticado
- **CUANDO** envía `POST /api/v1/auth/change-password` con `NewPassword`
  débil (longitud <6, sin minúscula, sin mayúscula, sin dígito o sin símbolo)
- **ENTONCES** MUST responder `400 Bad Request` con al menos un error de
  `ModelState` por dimensión violada.

#### Scenario: POST con ConfirmPassword distinta de NewPassword

- **DADO** un usuario autenticado
- **CUANDO** envía `POST /api/v1/auth/change-password` con
  `ConfirmPassword != NewPassword`
- **ENTONCES** MUST responder `400 Bad Request` con `ModelState` error en el
  campo `ConfirmPassword`.

### Requirement: Rate limiting fixed-window por usuario autenticado

`AddRateLimiter` MUST registrar la política
`AuthApiRoutes.ChangePasswordPolicyName` (5 req / 15 min, `QueueLimit=0`)
aplicada **después** de `[Authorize]`, de modo que el bucket se keyed por el
`sub` autenticado y NO por IP. El sexto request dentro de la ventana MUST
responder `429 Too Many Requests` con header `Retry-After`.

#### Scenario: Sexto request en 15 min para el mismo usuario

- **DADO** un usuario autenticado con 5 requests previos a
  `/api/v1/auth/change-password` en los últimos 15 min
- **CUANDO** envía el sexto request
- **ENTONCES** MUST responder `429 Too Many Requests`
- **Y** MUST incluir header `Retry-After`.

#### Scenario: Dos bearer distintos del mismo subject comparten bucket

- **DADO** el mismo usuario autenticado abriendo dos sesiones distintas
  (dos bearer distintos, mismo `sub`)
- **CUANDO** ambos emiten `POST /api/v1/auth/change-password`
- **ENTONCES** los dos requests MUST compartir el mismo bucket por usuario.

### Requirement: Rotación del SecurityStamp tras éxito

`IChangePasswordService` MUST invocar `userManager.UpdateSecurityStampAsync`
inmediatamente después de un `ChangePasswordAsync` exitoso. La falla del
stamp rotation MUST loguearse como warning y NO bloquear el flujo (la
contraseña ya cambió; el `SignOutAsync` explícito en Web cubre la
revocación de la cookie). Tras un POST exitoso, el `SecurityStamp`
persistente MUST ser distinto del previo.

#### Scenario: SecurityStamp cambia después del POST exitoso

- **DADO** un usuario autenticado con `SecurityStamp = stamp_previo`
- **CUANDO** envía `POST /api/v1/auth/change-password` con credenciales
  válidas
- **ENTONCES** MUST persistirse un `SecurityStamp` distinto de `stamp_previo`
  en `AspNetUsers.SecurityStamp`.

### Requirement: Mensajes uniformizados en español

Los mensajes de respuesta MUST estar en español neutro/profesional y MUST
NO filtrar detalles internos (paths, stack traces, nombres de columna). El
endpoint ya pasó `[Authorize]`, por lo que distinguir "contraseña actual
incorrecta" de "nueva contraseña no cumple la política" es UX legítimo y
NO rompe anti-enumeración (que aplica sólo a endpoints anónimos).

#### Scenario: Mensaje de éxito no revela detalles internos

- **DADO** un POST exitoso a `/api/v1/auth/change-password`
- **CUANDO** se inspecciona el body de respuesta
- **ENTONCES** MUST ser un mensaje en español neutro
- **Y** MUST NO contener `SecurityStamp`, IDs internos ni paths.

#### Scenario: Mensaje de error diferencia campos pero no leak interno

- **DADO** un POST con `CurrentPassword` incorrecta
- **CUANDO** se inspecciona el body `400 Bad Request`
- **ENTONCES** MUST estar en español
- **Y** MUST referenciar el campo correcto (`CurrentPassword`) sin filtrar
  hashes, tokens ni detalles de almacenamiento.

### Requirement: Wire-types de cambio de contraseña

`SGV.Contracts` MUST exponer
`record ChangePasswordRequest(string CurrentPassword, string NewPassword,
string ConfirmPassword)` en `Seguridad/Usuarios/UsuarioContracts.cs`,
`enum ChangePasswordOutcome { Success, InvalidCurrentPassword,
ValidationError, RateLimited }` y las constantes
`ChangePasswordRelative`, `ChangePassword` y `ChangePasswordPolicyName` en
`Auth/AuthApiRoutes.cs`. Los records MUST ser puros (sin lógica) y el
namespace MUST permanecer leaf.

#### Scenario: CambioPasswordRequest expone los tres campos

- **DADO** el wire-type `ChangePasswordRequest`
- **CUANDO** un test inspecciona sus propiedades
- **ENTONCES** MUST exponer `CurrentPassword`, `NewPassword` y
  `ConfirmPassword` como `string`.

#### Scenario: AuthApiRoutes expone las constantes requeridas

- **DADO** `SGV.Contracts.Auth.AuthApiRoutes`
- **CUANDO** un test inspecciona sus constantes
- **ENTONCES** MUST contener `ChangePasswordRelative`, `ChangePassword` y
  `ChangePasswordPolicyName`.

## Fuera de alcance

- Recovery flow (`forgot-password` / `reset-password`) — vive en
  `password-reset-flow`.
- MFA / 2FA sobre el cambio.
- Rate limit por IP (la cuota es por usuario autenticado).
- Historial de contraseñas (no hay tabla previa).
- Forzar cambio en próximo login.
- Notificación por email del cambio.
- Migraciones de BD (no se requieren; `SecurityStamp` ya existe en
  `AspNetUsers`).