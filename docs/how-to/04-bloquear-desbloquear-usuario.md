# H-02-04 — Bloquear y desbloquear un usuario

Un usuario está bajo ataque de fuerza bruta, fue comprometido, o necesita un bloqueo administrativo mientras se revisa su caso. La operación setea `LockoutEnd` a un valor sentinela (`9999-12-31 23:59:59 UTC`) para que `IsLockedOutAsync` rechace cada intento de login, sin perder el `AccessFailedCount` ni el `LockoutEnabled`.

---

## Prerrequisitos

- Sesión iniciada como `Administrador` (todos los endpoints de usuarios están protegidos por `[Authorize(Roles = RolesSgv.Administrador)]` en `src/SGV.Api/Controllers/UsuariosController.cs`).
- `UserId` del usuario objetivo (visible en `Pages/Seguridad/Usuarios/Index` o en una fila de auditoría).

---

## Paso 1 — Disparar el bloqueo

```bash
curl -X POST "http://localhost:7160/api/v1/usuarios/<userId>/bloquear" \
  -H "Authorization: Bearer <access-token-admin>"
```

**Verificación:** HTTP `200` con el `UsuarioDto` actualizado; el campo `bloqueado` queda `true`. La operación escribe una fila de auditoría con `Operacion=BloqueoUsuario`, los valores anteriores (UserName, Email, Roles, Bloqueado=false) y los nuevos (Bloqueado=true), bajo el `CorrelationId` del request (ver `UsuarioServicioComandos.BloquearAsync`).

> El endpoint rechaza `userId == usuarioActual.UserId` con HTTP 403 y código `AutoBloqueo` (defensa contra auto-bloqueo accidental).

---

## Paso 2 — Confirmar el gate por request

El `RevalidatorCredenciales` corre dentro del handler `JwtBearerEvents.OnTokenValidated` (`src/SGV.Api/Program.cs`, líneas 199-223) y de nuevo en el middleware fallback (`src/SGV.Api/Program.cs`, líneas 488-526). Cada request autenticado del usuario objetivo cae en:

```
Credential rejected because user {UserId} is locked out.
```

**Verificación:** el siguiente request del usuario bloqueado (con su cookie o su bearer) devuelve HTTP 401 sin entrar al controller. La cookie en el navegador queda obsoleta; la Web redirige a SignIn cuando el shell detecta 401 autenticado.

---

## Paso 3 — Desbloquear

```bash
curl -X POST "http://localhost:7160/api/v1/usuarios/<userId>/desbloquear" \
  -H "Authorization: Bearer <access-token-admin>"
```

**Verificación:** HTTP `200` con el `UsuarioDto` con `bloqueado=false`. La implementación (`UsuarioIdentityGateway.DesbloquearAsync`) preserva `LockoutEnabled=true` y sólo limpia `LockoutEnd`, así un próximo intento de fuerza bruta puede re-bloquear la cuenta sin perder el contador previo.

> La fila de auditoría registra `Operacion=DesbloqueoUsuario` con los snapshots `Bloqueado=true → Bloqueado=false`.

---

## Paso 4 — Verificar el listado de bloqueadas

En el navegador, abrí <http://localhost:5266/seguridad/usuarios> y filtrá por segmento `bloqueadas`:

```bash
curl "http://localhost:7160/api/v1/usuarios/consulta?status=bloqueadas&page=1&pageSize=20" \
  -H "Authorization: Bearer <access-token-admin>"
```

**Verificación:** el listado consume `GET /api/v1/usuarios/consulta` con `status=bloqueadas`, que mapea a `UsuarioSegmentoListado.Bloqueadas` en `UsuariosController.GetConsulta`. El usuario objetivo aparece en la grilla con badge de bloqueado. Los usuarios sin `LockoutEnd` futuro caen en el segmento `activas`.

---

## Troubleshooting

- **El endpoint devuelve 404 después de bloquear**: el usuario fue eliminado entre el alta del bloqueo y la consulta. Revisá `aspnetusers` con un SELECT directo.
- **El usuario sigue pudiendo loguearse**: la política de rate-limit puede estar enmascarando el lockout. Confirmá que `LockoutEnabled=1` y `LockoutEnd` futuro en la fila de `AspNetUsers` (consulta directa: `SELECT LockoutEnabled, LockoutEnd FROM AspNetUsers WHERE Id = '<userId>';`).
- **El listado muestra el usuario como activo**: el cliente está consultando con `status=activas` o sin `status`. El shell envía el valor crudo `bloqueadas`/`activas`; cualquier otra cadena cae a `Activas`.

---

## Referencias

- `src/SGV.Api/Controllers/UsuariosController.cs` — endpoints `POST /{id}/bloquear` y `POST /{id}/desbloquear`.
- `src/SGV.Infraestructura/Seguridad/UsuarioIdentityGateway.cs` — `BloquearAsync`/`DesbloquearAsync` y el `LockoutSentinelUtc`.
- `src/SGV.Aplicacion/Seguridad/Usuarios/UsuarioServicioComandos.cs` — orquestación + auditoría.
- `src/SGV.Api/Seguridad/RevalidatorCredenciales.cs` — gate en cada request autenticado.
- `../tutorials/01-levantar-sistema-local.md` — para preparar el entorno.
- [R-03-03](../reference/03-wire-types-contracts.md) — Referencia del
  wire contract de `BloquearUsuarioRequest` / `DesbloquearUsuarioRequest`
  y los demás records del módulo Seguridad.
