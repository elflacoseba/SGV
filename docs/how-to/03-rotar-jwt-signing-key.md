# H-02-03 — Rotar el `Jwt:SigningKey` sin tumbar sesiones

La clave HMAC que firma y valida los access tokens quedó expuesta (secret en un log, baja de un dev, incidente de seguridad) y hay que cambiarla. La rotación invalida los access tokens emitidos con la clave vieja, pero no necesariamente las sesiones de cookie abiertas en el navegador.

---

## Prerrequisitos

- Acceso a la configuración de runtime de `SGV.Api` y `SGV.Web` (variable de entorno o secret manager).
- Acceso al secret manager del proveedor cloud (AWS Secrets Manager, GCP Secret Manager, Azure Key Vault, etc.) si la clave se inyecta por ahí.
- Confirmación de que `Jwt:SigningKey` se valida con `ValidateOnStart` en `src/SGV.Api/Program.cs` (líneas ~133-139) — esto significa que **el primer arranque con la clave nueva debe pasar** antes de seguir.

---

## Paso 1 — Generar la clave nueva

```bash
openssl rand -base64 48
```

Regla: **≥32 bytes UTF-8** (validado por `Validate(o => Encoding.UTF8.GetByteCount(o.SigningKey) >= 32)`). El output Base64-48 mide 64 bytes UTF-8 y entra holgado.

---

## Paso 2 — Actualizar la clave en `SGV.Api`

```bash
# Variables de entorno
export Jwt__SigningKey="<clave-nueva>"

# O user-secrets en dev
dotnet user-secrets set "Jwt:SigningKey" "<clave-nueva>" --project src/SGV.Api
```

> ⚠️ A verificar: si tu entorno usa Kubernetes Secrets o Vault, el cambio se hace vía patch del Secret / reload del injector. Confirmá que el pod recibe la nueva env var antes de matar el viejo.

---

## Paso 3 — Actualizar la clave en `SGV.Web`

`SGV.Web` también firma y valida JWT (la cookie auth descifra el JWT y revalida firma, issuer, audience y lifetime antes de aceptar claims). Si la Web queda con la clave vieja, rechaza TODOS los access tokens que emita la API con la nueva, y los redirects posteriores rompen.

```bash
dotnet user-secrets set "Jwt:SigningKey" "<misma-clave-nueva>" --project src/SGV.Web
```

> ⚠️ A verificar: la clave DEBE ser idéntica byte a byte entre `SGV.Api` y `SGV.Web`. La diferencia entre claves se manifiesta como 401 genérico en cada request autenticado sin pista en el body.

---

## Paso 4 — Reiniciar `SGV.Api`

```bash
# El host valida la nueva clave con ValidateOnStart; si falla, no arranca.
# La consola emite OptionsValidationException antes del primer request.
dotnet run --project src/SGV.Api
```

**Verificación:** el log muestra el host escuchando (`Now listening on: …`). Si ves `Jwt:SigningKey must be configured and ≥32 UTF-8 bytes`, la nueva clave no se cargó; revisá env vars / user-secrets.

---

## Paso 5 — Invalidar sesiones activas (opcional)

La rotación de `Jwt:SigningKey` invalida access tokens, pero **no** las cookies de autenticación del shell web que ya tienen el JWT encriptado. Cuando la Web revalide contra la API en el próximo request, fallará porque la API firma con la clave nueva. El comportamiento depende del flujo:

- **Access token en el body del JWT descifrado:** la API rechaza con 401 en `OnTokenValidated`, `RevalidatorCredenciales` lo registra como `Credencial revocada o cuenta bloqueada`, y el navegador recibe un 401 que la Razor Page traduce a redirect al SignIn.
- **Refresh tokens persistidos en `sgv.rt`:** el primer `RefreshAsync` falla porque el access token embedded ya no valida; el `RefreshTokenServicio` lo trata como `InvalidToken`. Si querés cortar también el refresh, **rotá los refresh tokens explícitamente** (Paso 6).

**Verificación:** abrí un cliente autenticado con la cookie vieja. Al hacer un request, debería terminar en `/auth/sign-in` (no en un 500). El log de la API muestra un 401 con el subject del usuario viejo.

---

## Paso 6 — Revocar todos los refresh tokens (sólo si necesitás logout masivo)

Si querés forzar re-login de TODOS los usuarios (no sólo los que tenían el JWT viejo en tránsito), revocá las filas de `RefreshTokens` activo en la base. Como la tabla vive en `SgvDbContext` y no hay endpoint admin para esto, hacelo por SQL hasta que exista uno:

```sql
UPDATE RefreshTokens SET RevokedAt = UTC_TIMESTAMP(6) WHERE RevokedAt IS NULL;
```

**Verificación:** un usuario que intente refresh después de la rotación cae en `RefreshOutcome.Invalid` y la API responde `401 {"mensaje":"La sesión expiró. Iniciá sesión nuevamente."}`.

> ⚠️ A verificar: este UPDATE no genera filas de auditoría (el interceptor EF no ve `ExecuteUpdateAsync`). Si necesitás trazabilidad, agregá un endpoint admin antes de la rotación.

---

## Troubleshooting

- **La API arranca pero cada request devuelve 401**: las claves de `SGV.Api` y `SGV.Web` no coinciden. Compará el valor exacto con `dotnet user-secrets list --project src/SGV.Api` y lo mismo para `src/SGV.Web`.
- **El primer request post-rotación devuelve 401 con `Credencial revocada`**: es el comportamiento esperado cuando un access token con la firma vieja llega a la API nueva. El usuario tiene que volver a SignIn.
- **Errores de `IDX10720` o `IDX10503` en logs**: la firma falló por clave incorrecta o longitud insuficiente. Confirmá que la nueva clave mide ≥32 bytes UTF-8.

---

## Referencias

- `src/SGV.Contracts/Seguridad/JwtOptions.cs` — opciones y defaults.
- `src/SGV.Api/Program.cs` (líneas 133-139) — validación `ValidateOnStart`.
- `src/SGV.Infraestructura/Seguridad/JwtAccessTokenIssuer.cs` — firma con `SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.SigningKey))`.
- `src/SGV.Api/Seguridad/RevalidatorCredenciales.cs` — gate por request autenticado.
- `../tutorials/01-levantar-sistema-local.md` — setup inicial de la clave.
- [E-04-02](../explanation/02-bridge-cookie-jwt.md) — Explanation del
  bridge cookie → JWT y el ciclo de credenciales en el shell web.
