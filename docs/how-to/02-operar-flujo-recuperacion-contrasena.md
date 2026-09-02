# H-02-02 — Operar el flujo de recuperación de contraseña

El usuario perdió la contraseña y necesita entrar de nuevo. El ciclo recorre `forgot-password` → email con token → `validate-reset-token` → `reset-password` → `sign-in` con la nueva credencial. En `Development` el email nunca sale: `Smtp:Mode=Logger` lo escribe al log y de ahí se lee el token.

---

## Prerrequisitos

- `SGV.Api` levantado con `Smtp:Mode=Logger` y `Smtp:WebBaseUrl=http://localhost:5266` (defaults de `src/SGV.Api/appsettings.Development.json`).
- `SGV.Web` levantado en el puerto 5266.
- Email del usuario existente en `AspNetUsers`.

---

## Paso 1 — Disparar el forgot-password

Hacé `POST` con cualquier cliente (curl, Postman, DevTools):

```bash
curl -X POST http://localhost:7160/api/v1/auth/forgot-password \
  -H "Content-Type: application/json" \
  -d '{"userNameOrEmail":"admin@sgv.local"}'
```

**Verificación:** respuesta HTTP `200 OK` con body `{"mensaje":"Si la cuenta existe, te enviamos un correo..."}`. La política rate-limit `ForgotPassword` admite **3 req / 15 min por IP** (definida en `src/SGV.Api/Program.cs`); superada devuelve `429`.

> El endpoint colapsa `UserNotFound` y `Success` en una sola respuesta para evitar enumeración de usuarios (ver `src/SGV.Api/Controllers/AuthController.cs::ForgotPassword`).

---

## Paso 2 — Leer el token del log

`SmtpEmailSender` con `Mode=Logger` no hace `ConnectAsync`: emite una línea `Information` con el `SMTP (Logger mode) -> from=… to=… subject=… bodyLength=…`. **El token no aparece en el log** porque `SendPasswordResetLinkAsync` sólo escribe el cuerpo pre-armado en `src/SGV.Infraestructura/Seguridad/PasswordResetService.cs::BuildRecoveryBody`, que no contiene el query string.

Para que el token sea visible durante una operación manual, interceptá el `Body` de la línea de log o activá temporalmente `Logging:LogLevel:SGV.Infraestructura.Email=Trace` (no incluido por default). El cuerpo del email tiene la pinta:

```html
<a href="http://localhost:5266/auth/reset-password?userId=<id>&token=<token>">Restablecer contraseña</a>
```

Extraé los valores `userId` y `token` (vienen URL-encoded).

**Verificación:** el token tiene expiración de 1 hora (configurada vía `DataProtectionTokenProviderOptions.TokenLifespan` en `Program.cs`). Pasada esa ventana, `validate-reset-token` rechaza con `400`.

---

## Paso 3 — Validar el token sin consumirlo

```bash
curl -X POST http://localhost:7160/api/v1/auth/validate-reset-token \
  -H "Content-Type: application/json" \
  -d '{"userId":"<id>","token":"<token>"}'
```

**Verificación:** HTTP `200` con `{"mensaje":"El token es válido."}`. Si el token está expirado o el `userId` no existe, devuelve `400 Bad Request` con `{"mensaje":"El enlace de restablecimiento no es válido o ya expiró."}`.

---

## Paso 4 — Consumir el token y setear la nueva contraseña

```bash
curl -X POST http://localhost:7160/api/v1/auth/reset-password \
  -H "Content-Type: application/json" \
  -d '{"userId":"<id>","token":"<token>","newPassword":"NuevoPass1!xyz"}'
```

`PasswordPolicy` exige mínimo 6 caracteres, minúscula, mayúscula, dígito y símbolo. La política es la misma fuente única (`src/SGV.Contracts/Seguridad/PasswordPolicy.cs`) que consume Identity, los validators y la Razor Page.

**Verificación:** HTTP `200` con `{"mensaje":"Tu contraseña fue actualizada."}`. Identity rota el `SecurityStamp` del usuario; cualquier cookie o bearer vigente queda invalidado en el próximo request (`RevalidatorCredenciales` rechaza la credencial).

---

## Paso 5 — Iniciar sesión con la contraseña nueva

En el navegador, abrí <http://localhost:5266/auth/sign-in> e ingresá el `UserName` + la nueva contraseña.

**Verificación:** redirect a `/` y cookie `sgv.auth` emitida. El endpoint `POST /api/v1/auth/login` emite access + refresh token y la Razor Page persiste el refresh en la cookie `sgv.rt` vía `IRefreshTokenCookieAccessor`.

---

## Troubleshooting

- **El log no muestra ningún `SMTP (Logger mode)`**: `Smtp:Mode` no quedó en `Logger` o el usuario no se encontró (anti-enumeration: el servicio loggea `Password recovery requested for unknown identifier` en vez de emitir el email).
- **HTTP 429 en forgot-password**: política `ForgotPassword` agotada. Esperá 15 min o reiniciá la API para resetear la cuota en memoria (no persiste).
- **Token rechazado inmediatamente**: el `WebBaseUrl` configurado al emitir difiere del esperado por la URL; revisá `Smtp:WebBaseUrl` en `appsettings.Development.json` (debe ser la URL absoluta de la Web).
- **HTTP 400 con `{"mensaje":"El enlace de restablecimiento no es válido o ya expiró."}` tras reset válido**: el `SecurityStamp` del usuario ya rotó (otra operación). Generá un nuevo forgot-password.

---

## Referencias

- `src/SGV.Api/Controllers/AuthController.cs` — endpoints `forgot-password`, `reset-password`, `validate-reset-token`.
- `src/SGV.Infraestructura/Seguridad/PasswordResetService.cs` — implementación del flujo.
- `src/SGV.Infraestructura/Email/SmtpEmailSender.cs` — `BuildPasswordResetLink`.
- `src/SGV.Contracts/Seguridad/PasswordPolicy.cs` — fuente única de la política de contraseñas.
- `../tutorials/01-levantar-sistema-local.md` — prepara el entorno de Development con `Smtp:Mode=Logger`.
- [R-03-03](../reference/03-wire-types-contracts.md) — Referencia del
  wire contract `ForgotPasswordRequest` / `ResetPasswordRequest` y los demás
  records del módulo Seguridad.
