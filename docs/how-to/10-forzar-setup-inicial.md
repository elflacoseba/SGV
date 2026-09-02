# H-02-10 — Forzar el setup inicial cuando `AspNetUsers` está vacía

Un sistema recién instalado nunca muestra la pantalla `/auth/setup` porque el cache TTL 30s del cliente `ISetupApiClient` cree que la API ya está inicializada, o porque la API cae y `SignIn.OnGetAsync` cae al fail-open del cache. Este how-to explica el contrato del setup y cómo forzarlo manualmente cuando la redirección automática no dispara.

---

## Prerrequisitos

- Acceso a la base de datos MySQL del ambiente (con permiso `SELECT` y `DELETE` sobre `AspNetUsers` / `AspNetUserRoles` para una eventual restauración).
- Acceso a los logs de `SGV.Api` (la consola captura el WARNING de diagnóstico).
- Haber leído `src/SGV.Aplicacion/Setup/ISetupServicio.cs` y `src/SGV.Web/Pages/Auth/Setup.cshtml.cs`.

---

## Paso 1 — Diagnosticar el síntoma

Síntomas típicos:

1. Vas a `/auth/sign-in` y ves el formulario de login en vez de la pantalla de setup, pero `SELECT COUNT(*) FROM AspNetUsers;` devuelve `0`.
2. La pantalla de setup aparece, pero al enviar el formulario devuelve error de transporte o timeout.
3. La redirección funciona una vez y después queda pegada aún cuando la base tiene usuarios.

**Verificación:** la causa probable es el cache TTL 30s del cliente `ISetupApiClient` (mencionado en `SignIn.OnGetAsync`, líneas 38-45). El cliente hace fail-open: si la API falla, devuelve `RequiresSetup=false` para no romper el SignIn en producción.

---

## Paso 2 — Confirmar el estado real del sistema

Pegale directo al endpoint sin pasar por el cliente cacheado:

```bash
curl http://localhost:7160/api/v1/setup/status
```

La respuesta es `SetupStatusResponse` con `RequiresSetup: true|false`. El endpoint es anónimo (`[AllowAnonymous]` en `SetupController.GetStatus`).

**Verificación:** un `true` con la base vacía confirma que la API está reportando correctamente. Un `false` con la base vacía indica un desfasaje entre el cliente cacheado y el backend.

---

## Paso 3 — Forzar el setup manualmente (sin esperar el cache)

Si la API responde `RequiresSetup=true` pero la Web no redirige, andá directo a la pantalla de setup en el navegador:

```
http://localhost:5266/auth/setup
```

El handler `OnGetAsync` de `Setup.cshtml.cs` consulta otra vez `ISetupApiClient.ObtenerEstadoAsync`. Si el cache está caliente con `false`, redirige a `/Auth/SignIn` (línea 56) — eso confirma que el problema es el cache.

**Verificación:** ves el formulario de setup con los campos `Nombres`, `Apellidos`, `Legajo`, `Email`, `UserName`, `Password`, `TipoDocumentoId`, `NumeroDocumento`, `Telefono`. Si la página te devolvió a SignIn, esperá 30s y reintentá (el TTL del cache expira solo).

---

## Paso 4 — Si la API está caída

El cliente cachea el último estado conocido por **30s**; durante esa ventana, una API caída puede:

- Si el cache tenía `false`: seguir mostrando SignIn (fail-open intencional, documentado en `SignIn.cshtml.cs`).
- Si el cache tenía `true`: la Web igual intenta renderizar `/auth/setup` pero el `LoadTiposDocumentoAsync` puede caer en `LoadTiposDocumentoAsync` con dropdown vacío (líneas 162-181).

**Verificación:** los logs de la API muestran intentos de `GET /api/v1/setup/status` con error de transporte. Levantá la API o recargá la Web después del TTL.

---

## Paso 5 — Qué hace `SetupServicio.CrearAdminAsync` por dentro

La implementación vive en `src/SGV.Infraestructura/Setup/SetupServicio.cs`. Los pasos en orden:

1. **FluentValidation** sobre `SetupRequest` (mismas reglas que `CambiarContrasena` para `Password`).
2. **Guarda `AnyUsersAsync`**: si `AspNetUsers` ya tiene filas, devuelve `409 SetupYaCompletado`.
3. **Crea `Persona`** vía `IPersonaServicioComandos.CrearAsync` (unicidad de Legajo / Email / Documento).
4. **Crea `Usuario`** vía `IUsuarioIdentityGateway.CrearAsync` con rol `Administrador` dentro de su propia transacción.
5. **Compensa** con `personaServicio.DesactivarAsync(personaId)` si el paso 4 falla, para no dejar una `Persona` huérfana.
6. **Auditoría explícita** con `usuarioOperadorId="system"` y `entidad="SetupInicial"`.

> ⚠️ A verificar: el doc-comment de la implementación aclara que la atomicidad se logra por compensación (soft-delete sobre Persona si Usuario falla) y no por transacción outer — Pomelo 9 + MySqlConnector rechazan `BeginTransactionAsync` anidados.

**Verificación:** la respuesta `200 OK` con `SetupResult(PersonaId, UserId, UserName)` indica éxito. El TempData `SetupSuccess` aparece como banner verde en `/auth/sign-in` tras el PRG.

---

## Paso 6 — Restaurar tras un setup fallido a medias

Si el paso 3 ó 4 dejó filas inconsistentes:

```sql
-- Ver filas semilla creadas antes del fallo
SELECT Id, UserName, Email, PersonaId FROM AspNetUsers;

-- Si quedaron Personas huérfanas sin Usuario, quedan en Personas
-- con IsDeleted=true (compensación) o activas (fallo de compensación).
SELECT Id, Nombres, Apellidos, IsDeleted FROM Personas ORDER BY CreatedAt DESC LIMIT 10;
```

**Verificación:** si la API responde `SetupYaCompletado (409)` con un solo admin parcial, completá manualmente el setup cargando el SQL de docs/migracion-inicial-sgv.sql sobre una base vacía y volvé a invocar `POST /api/v1/setup` (siempre que AspNetUsers siga vacía — la guarda es dura).

---

## Troubleshooting

- **El formulario se envía y devuelve 429**: política rate-limit `Setup` (5 req / 15 min por IP) agotada. Esperá la ventana o reiniciá la API.
- **La pantalla de setup se renderea con dropdown de `TipoDocumento` vacío**: el catálogo de `TiposDocumento` no terminó de migrar o la API está caída. Confirmá que `GET /api/v1/tipos-documento` responde 200.
- **El setup completa pero la sesión no se inicia automáticamente**: la pantalla hace PRG a `/auth/sign-in` con un banner verde; tenés que ingresar las credenciales manualmente. El setup NO emite cookie ni JWT — sólo crea la fila de admin.
- **`RequiresSetup` queda en `true` aún con usuarios**: bug del contador `AnyAsync` o el cache TTL está vencido pero la respuesta no se actualizó. Reiniciá `SGV.Web` para purgar el `IMemoryCache`.

---

## Referencias

- `src/SGV.Web/Pages/Auth/SignIn.cshtml.cs` — gate `RequiresSetup` con cache TTL 30s.
- `src/SGV.Web/Pages/Auth/Setup.cshtml.cs` — formulario + `ApplyFailureToModelState`.
- `src/SGV.Api/Controllers/SetupController.cs` — `GET status` + `POST` con rate-limit.
- `src/SGV.Infraestructura/Setup/SetupServicio.cs` — orquestación transaccional.
- `../tutorials/01-levantar-sistema-local.md` — paso 7 describe el camino feliz.
- [R-03-03](../reference/03-wire-types-contracts.md) — Referencia del
  wire `SetupRequest` / `SetupStatusResponse` / `SetupResult` y demás
  records del módulo Setup.
