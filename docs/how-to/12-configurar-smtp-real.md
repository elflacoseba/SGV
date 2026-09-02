# H-02-12 — Configurar SMTP real (no `Logger`) en staging/producción

En `Development` la API nunca sale a la red: `Smtp:Mode=Logger` registra los mensajes en el log y nada más. En staging/producción hay que conectar a un relay SMTP real para que los emails de recuperación de contraseña lleguen a la casilla del usuario.

---

## Prerrequisitos

- Relay SMTP accesible (Mailgun, SendGrid, SES, Postfix interno, etc.) con `host`, `port`, credenciales `UserName`/`Password` y soporte TLS.
- Dirección `FromAddress` válida y reputada (los relays suelen rechazar `gmail.com` aleatorio o dominios sin SPF/DKIM).
- URL absoluta del shell web para construir el link de recuperación (`Smtp:WebBaseUrl`).

---

## Paso 1 — Entender el fail-loud de `SmtpOptions`

`SmtpOptions` (en `src/SGV.Infraestructura/Email/SmtpOptions.cs`) implementa `IValidatableObject`. `Program.cs` (líneas 157-161) llama `ValidateDataAnnotations().ValidateOnStart()`, que invoca `Validate(...)` cuando el mode es `Smtp`:

```csharp
if (Mode != SmtpDeliveryMode.Smtp) yield break;
if (string.IsNullOrWhiteSpace(Host)) yield return new ValidationResult("Smtp:Host es obligatorio cuando Mode=Smtp.");
// ... chequea Port 1-65535, UserName + Password si Host no es localhost
```

Si cualquier regla falla, **el host no arranca**: `OptionsValidationException` antes del primer request. Las DataAnnotations exigen `FromAddress` con formato email, `FromName` no vacío, `WebBaseUrl` con `[Url]` (URL absoluta).

**Verificación:** con la sección `Smtp` incompleta en `Production`, el arranque aborta con mensaje específico por campo.

---

## Paso 2 — Declarar la configuración por variable de entorno

ASP.NET Core mapea `__` a `:` para binding. Cada propiedad de `SmtpOptions` se setea con prefijo `Smtp__`:

```bash
export Smtp__Mode=Smtp
export Smtp__Host="smtp.sendgrid.net"
export Smtp__Port="587"
export Smtp__EnableSsl="true"
export Smtp__UserName="apikey"
export Smtp__Password="<sendgrid-api-key>"
export Smtp__FromAddress="no-reply@sgv.example.com"
export Smtp__FromName="SGV"
export Smtp__WebBaseUrl="https://sgv.example.com"
```

> ⚠️ A verificar: el host exige `Smtp:UserName` y `Smtp:Password` no vacíos **cuando el host no es localhost**. Esto cierra el caso típico de "dejo la password vacía en staging porque el relay es anónimo" — la validación lo rechaza al arrancar, no en el primer envío.

---

## Paso 3 — Validar `WebBaseUrl`

La DataAnnotation `[Url]` exige URL absoluta (`https://...`). El email arma el link como `<WebBaseUrl>/auth/reset-password?userId=...&token=...`; un valor relativo produce un link roto dentro del mailbox del destinatario.

**Verificación:** `curl -I <WebBaseUrl>/auth/sign-in` responde `200 OK` desde la red del relay (al menos `200` o `302` a SignIn). Si devuelve error, el link que llega al usuario termina en una URL inalcanzable.

---

## Paso 4 — Reiniciar la API

```bash
dotnet run --project src/SGV.Api
```

El host debe arrancar limpio. Los logs muestran las opciones SMTP cargadas (sin filtrar la password — `IOptions<SmtpOptions>` no loggea por default).

**Verificación:** el log de arranque no contiene `OptionsValidationException`. Si ves `Smtp:UserName es obligatorio cuando el host no es localhost.`, agregá las credenciales.

---

## Paso 5 — Probar el flujo end-to-end

Disparar el flujo de recuperación con un usuario real:

```bash
curl -X POST http://localhost:5160/api/v1/auth/forgot-password \
  -H "Content-Type: application/json" \
  -d '{"userNameOrEmail":"smoke-test@sgv.example.com"}'
```

**Verificación:** la API responde `200 OK` con `{"mensaje":"Si la cuenta existe, te enviamos un correo..."}`. En 1-2 minutos, el mailbox de `smoke-test@sgv.example.com` recibe el email con el link `https://sgv.example.com/auth/reset-password?...`.

Si el relay rechaza el envío, los logs de la API muestran `MailKit.Security.AuthenticationException` o `SmtpCommandException`. Pegale al relay con `openssl s_client -connect smtp.sendgrid.net:587 -starttls smtp` para validar manualmente las credenciales.

---

## Paso 6 — Manejar `EnableSsl`

`SmtpEmailSender.SendViaMailKitAsync` mapea `EnableSsl=true → StartTls` y `EnableSsl=false → StartTlsWhenAvailable`. Si el relay no soporta TLS, cambiá `EnableSsl=false` y dejá que MailKit negocie cuando esté disponible.

---

## Paso 7 — Persistir secretos

No commitees `Smtp__Password` a `appsettings.*.json`. Usá variables de entorno (Kubernetes Secrets, ECS Task Definition) o un secret manager (AWS Secrets Manager, GCP Secret Manager, Azure Key Vault) montado como env var al arranque.

**Verificación:** `grep -r "Smtp:Password" appsettings*.json` no encuentra literales. La rotación de la password del relay sigue el procedimiento del operador del secret manager.

---

## Troubleshooting

- **`OptionsValidationException` al arrancar con `Mode=Smtp` y todo configurado**: revisá que `Smtp:Port` sea un entero parseable entre 1 y 65535. `Smtp:Port="25a"` falla `Validate(...)`.
- **Mailbox no recibe el email**: el relay puede estar rechazando por SPF/DKIM del dominio del `FromAddress`. Verificá con `nslookup -type=txt sgv.example.com` que el registro SPF autoriza al relay.
- **TLS handshake falla**: el relay espera `SSL on connect` (puerto 465) en lugar de `StartTls` (587). Cambiá `Smtp__Port="465"` y `Smtp__EnableSsl="true"` (MailKit acepta ambos cuando el puerto lo requiere).
- **Email llega como spam**: el dominio no tiene DKIM/DMARC alineado. Configuralos en el proveedor DNS antes de promover a producción.

---

## Referencias

- `src/SGV.Infraestructura/Email/SmtpOptions.cs` — DataAnnotations + `IValidatableObject`.
- `src/SGV.Infraestructura/Email/SmtpEmailSender.cs` — `SendViaMailKitAsync` con MailKit.
- `src/SGV.Api/Program.cs` (líneas 157-161) — `ValidateDataAnnotations().ValidateOnStart()`.
- `src/SGV.Api/appsettings.Development.json` — defaults de dev con `Mode=Logger`.
- `../how-to/02-operar-flujo-recuperacion-contrasena.md` — flujo end-to-end de recuperación.
- [R-03-05](../reference/05-configuracion-opciones-secretos.md) —
  Referencia de la sección `Smtp` y el resto de las opciones de
  configuración.
