# Design: Quita soft-delete de usuarios

## Enfoque técnico

`IsDeleted` + columnas generadas se reemplazan por `LockoutEnd` nativo de Identity. `DELETE` ejecuta `UserManager.DeleteAsync` (hard-delete de `AspNetUsers`); `Bloquear`/`Desbloquear` son comandos independientes. Migración MySQL forward-only con backfill `IsDeleted=1 → LockoutEnd='9999-12-31 23:59:59.999999'` y preflight fail-loud. **Corte inmediato**: cada request autenticada revalida contra DB `LockoutEnd` y existencia del usuario por `sub`; cualquier bloqueo o eliminación invalida la credencial activa sin esperar `exp`; el desbloqueo NO revive tokens previos.

## Decisiones

| # | Decisión | Rationale |
|---|----------|-----------|
| D1 | `LockoutEnabled=true` permanente; bloqueado = `LockoutEnd > UtcNow` (`IsLockedOutAsync`). Bloqueo: `LockoutEnd='9999-12-31 23:59:59.999999'` UTC. Desbloqueo: `null`. `AccessFailedCount` NO se resetea | API canónica. `AddYears(9999)` desborda `MaxValue` (7 fracciones), la 7ª se trunca; `datetime(6)` admite 6 (verificado en `information_schema.COLUMNS`). No resetear evita brute-force tras desbloqueo admin. |
| D2 | `LoginAsync` invoca `IsLockedOutAsync` ANTES de `CheckPasswordAsync`; bloqueado → `null` con `UsuarioBloqueado` | SGV valida con `CheckPasswordAsync` directo; chequear antes evita enumeración y timing leaks. |
| D3 | **Corte inmediato** (API): `IRevalidatorCredenciales.SigueVigenteAsync(sub)` (UserManager + DbContext vía `IServiceScopeFactory`) en `JwtBearerOptions.Events.OnTokenValidated` (API) + middleware fallback post-auth | (a) `RevokedTokens` agrega query+storage por logout admin; (b) `SecurityStamp` claim carga al usuario por `sub` igual. (c) PK lookup, 1 query por request, sin token store, sin cambios al JWT. Desbloqueo NO revive JWT previo. |
| D3b | **Corte inmediato** (Web): `CookiePrincipalRevalidator` registrado en `AddCookie.Events.OnValidatePrincipal`. **Implementación vía cliente HTTP dedicado** (no `UserManager` directo) porque `SGV.Web` sólo referencia `SGV.Contracts` y no tiene acceso a `SgvIdentityUser`/`SgvDbContext`. El cliente consulta un endpoint del API con el JWT bearer del `AuthenticationProperties` y delega la decisión. **Fail-open en 5xx** del transporte: si la API está caída, la cookie se preserva temporalmente para no cerrar sesiones por indisponibilidad. `ApiBearerTokenHandler` propaga el rechazo al cliente tipado | Mantiene la frontera arquitectónica del repo (`SGV.Web` shell, `SGV.Api` composition root). El fail-open evita un DoS accidental cuando la API está degradada. El coste es 1 HTTP request extra por request autenticada en Web (mitigable con Q2 cache 1-2s, diferido). |
| D4 | `UserManager.DeleteAsync` + FK CASCADE purgan `AspNetUserRoles/Claims/Logins/Tokens`. `Personas` (FK RESTRICT) y `Auditorias` (string sin FK) sobreviven | Cascade garantiza atomicidad. |
| D5 | `UsuarioSegmentoListado.Eliminadas`→`Bloqueadas`; `UsuarioDto.Bloqueado:bool` derivado de `LockoutEnd > UtcNow`. Sin `OrigenBloqueo` en wire | Decisión cerrada: un segmento cubre admin + lockouts temporales. |
| D6 | API: `DELETE`→204; `POST /{id}/bloquear` y `POST /{id}/desbloquear`→200. `[Authorize(Roles="Administrador")]`. `ProblemDetails` vía `ApiResults.ToProblemResult`. Auditoría: `BloqueoUsuario`, `DesbloqueoUsuario`, `EliminacionFisica` atómicos bajo la misma tx EF | 200 permite confirmar estado con `UsuarioDto`. El interceptor de auditoría ya envuelve la transacción. |
| D7 | Migración forward-only. Conservar `UserNameIndex` único de Identity. `Down` lanza `NotSupportedException`. `docs/migracion-inicial-sgv.sql` es provisioning, no backup | MySQL no permite `DROP INDEX` que sostiene la FK ni `DROP COLUMN` con columna generada STORED que sostiene unique index. El orden evita `ALGORITHM=COPY` extra y conserva unicidad. |

## Flujo de datos

```
Bloquear/Eliminar ─► SetLockoutEndDateAsync / DeleteAsync + Auditoria (misma tx)

Request protegida API ─► JWT bearer
                     └► IRevalidatorCredenciales.SigueVigenteAsync(sub)  [UserManager directo]
                  if null || IsLockedOutAsync ─► Fail
                            Request sigue

Request protegida Web ─► cookie auth
                     └► CookiePrincipalRevalidator
                          └► HttpClient (bearer) ─► GET /api/v1/auth/vigente ─► RevalidatorCredenciales (en API)
                  if !SigueVigente || 401/403/404 ─► RejectPrincipal + SignOut cookie
                  if 5xx ─► fail-open (preserva cookie, log warning)
                            Request sigue
```

## Cambios de archivos (resumen)

Quitar `IsDeleted` y columnas generadas de `SgvIdentityUser(.Configuracion)`; volver `IX_AspNetUsers_PersonaId` UNIQUE. `AuthServicio.LoginAsync` invoca `IsLockedOutAsync` antes de `CheckPasswordAsync`. `UsuarioIdentityGateway`: `Bloquear/Desbloquear/EliminarAsync`; `QueryAsync` filtra `LockoutEnd > UtcNow`. Crear `Api.Seguridad.RevalidatorCredenciales` + `ConfigureJwtBearerFromJwtBearerOptions` (JWT `OnTokenValidated`). Crear `Web.Auth.CookiePrincipalRevalidator` + hook en `Program.cs AddCookie.Events` (cookie `OnValidatePrincipal`). `Aplicacion + Contracts`: `Eliminar/Bloquear/Desbloquear`; `Eliminadas`→`Bloqueadas`; `Bloqueado:bool`; códigos `AutoEliminacion/AutoBloqueo/UsuarioBloqueado/UsuarioNoEncontrado`. `UsuariosController`: `Delete` (204) `Bloquear/Desbloquear` (200); sin `Reactivate`; `status=bloqueadas`. Nueva migración forward-only D7. `Web.Integration.Usuarios`: `Eliminar/Bloquear/DesbloquearAsync`; `BuildQueryUri Bloqueadas → status=bloqueadas`. `Pages.Seguridad.Usuarios`: `Index` (handlers `Bloquear/Desbloquear`, modal irreversible sin `UserName`, ocultar auto-acciones, sin `LastDeletedId`/`OnPostReactivateAsync`) y `Details` (consulta DTO real, `returnStatus` solo decide vista, 404 recuperable). Tests: adaptar a `Bloquear/Desbloquear/Eliminar`; flags `BloquearCalled/DesbloquearCalled/EliminarCalled`; `Eliminadas`→`Bloqueadas`; **tests de corte inmediato** (JWT y cookie).

## Interfaces / contratos

```csharp
public enum UsuarioSegmentoListado { Activas = 0, Bloqueadas = 1 }
public sealed record UsuarioDto(..., bool Bloqueado = false);
Task<UsuarioCommandResult> BloquearAsync(string userId, CancellationToken ct = default);
Task<UsuarioCommandResult> DesbloquearAsync(string userId, CancellationToken ct = default);
Task<UsuarioCommandResult> EliminarAsync(string userId, CancellationToken ct = default);
public interface IRevalidatorCredenciales { Task<bool> SigueVigenteAsync(string userId, CancellationToken ct = default); }
```

## Pruebas (strict TDD)

| Capa | Test clave | MySQL |
|------|-----------|-------|
| Aplicación | Auto-fence; `Bloquear` idempotente; `Eliminar` no audita si no existe | No |
| Persistencia | Migración idempotente (2º run); preflight fail-loud; FK CASCADE; `Personas`/`Auditorias` sobreviven; backfill en `datetime(6)` máximo | ✓ |
| API | `DELETE` 204; `POST /bloquear` 200; auto-fence 403; `404`; doble `DELETE` → 404; **corte inmediato**: JWT previo a `POST /bloquear` responde 401 | ✓ |
| Web | Segmentos `activas|bloqueadas`; modal irreversible; autoacciones ocultas; PRG preserva filtros; **corte inmediato cookie**: cookie previa a `POST /bloquear` redirige a `/auth/sign-in`; `Details` 404 recuperable | ✓ |
| Login | `IsLockedOutAsync` rechaza con `UsuarioBloqueado`; sin lockout emite JWT | ✓ |

## Threat Matrix

| Boundary | Applicability |
|----------|---------------|
| Documentation-like paths | N/A |
| Git repository selection | N/A |
| Commit state | N/A |
| Push state | N/A |
| PR commands | N/A |

## Migración / rollout

D7 orden: (1) preflight fail-loud duplicados `PersonaId` activos; (2) backfill `IsDeleted=1 → LockoutEnabled=1, LockoutEnd='9999-12-31 23:59:59.999999'`; (3) `DROP FK FK_AspNetUsers_Personas_PersonaId`; (4) `DROP INDEX IX_AspNetUsers_ActiveUserNameUnique` y `IX_AspNetUsers_ActivePersonaIdUnique`; (5) `DROP COLUMN ActiveUserNameUnique, ActivePersonaIdUnique, IsDeleted`; (6) `DROP INDEX IX_AspNetUsers_PersonaId`; (7) `ADD UNIQUE INDEX IX_AspNetUsers_PersonaId`; (8) `ADD CONSTRAINT FK_AspNetUsers_Personas_PersonaId` RESTRICT. Réplicas primero; freeze breve de login. `dotnet ef migrations script --idempotent` regenera `docs/migracion-inicial-sgv.sql` (provisioning). `bun run build` tras tocar `src/SGV.Web`.

## Preguntas abiertas

| # | Tema | Bloqueante | Mitigación |
|---|------|-----------|-----------|
| Q1 | Verificar en CI real que `OnTokenValidated`/`OnValidatePrincipal` corren tras la firma del token pero antes de la autorización, con `UserManager` resuelto desde scope | Sí | Test integration API/Web con MySQL: emitir JWT, bloquear, verificar 401/redirect. Fallback: middleware filter post-auth que revalide. |
| Q2 | Latencia del revalidator: 1 lookup PK + `IsLockedOutAsync` por request autenticada | No | PK clustered; cache de 1-2s por `sub` con invalidación al bloquear si p95 > 10ms. |
