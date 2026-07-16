# Tasks: Quita soft-delete de usuarios

## Review Workload Forecast

| Field | Value |
|-------|-------|
| Estimated changed lines | 1200–1800 |
| 400-line budget risk | High |
| Chained PRs recommended | Yes |
| Suggested split | PR 1 (Foundation) → PR 2 (Core) → PR 3 (Web) → PR 4 (Tests) |
| Delivery strategy | ask-on-risk |
| Chain strategy | pending |

Decision needed before apply: Yes
Chained PRs recommended: Yes
Chain strategy: pending
400-line budget risk: High

### Suggested Work Units

| Unit | Goal | Likely PR | Focused test command | Runtime harness | Rollback boundary |
|------|------|-----------|----------------------|-----------------|-------------------|
| 1 | Entity+EF, contracts, gateway, auth, migration | PR 1 | `dotnet test --filter UsuarioIdentityGateway` | N/A — dominio+infraestructura | Revertir migración + SgvIdentityUser/Config/Gateway |
| 2 | Revalidator, comando, controller, hooks JWT/cookie | PR 2 | `dotnet test --filter UsuariosController` | `POST /bloquear` → `GET /api/me` → 401 | Revertir endpoints y hooks auth |
| 3 | Web client, Index/Details, modales, auto-fence | PR 3 | `dotnet test --filter Usuarios` SGV.Tests.Web | Navegar, bloquear → redirect sign-in | Revertir Web/Pages y Web/Integration |
| 4 | Unit + MySqlFact + cutoff tests | PR 4 | `dotnet test SGV.slnx` | MySqlFact: emitir JWT, bloquear, 401 | Test code only |

**Threat Matrix**: todas las filas son N/A (Documentation-like paths, Git repo, Commit/Push state, PR commands). Ninguna genera tarea.

## Phase 1: Foundation

- [x] 1.1 RED: Integration test Q1 — verificar OnTokenValidated/OnValidatePrincipal corren antes de autorización, fallback post-auth
- [x] 1.2 RED: Test migración preflight fail-loud con duplicados PersonaId
- [x] 1.3 RED: Test migración backfill `IsDeleted=1` → `LockoutEnd` futuro
- [x] 1.4 GREEN: `SgvIdentityUser.cs` — quitar `IsDeleted`
- [x] 1.5 GREEN: `SgvIdentityUserConfiguracion.cs` — quitar `IsDeleted`, columnas generadas, sus índices; swap `IX_AspNetUsers_PersonaId` a UNIQUE
- [x] 1.6 GREEN: `UsuarioContracts.cs` — `Eliminadas`→`Bloqueadas`, `Bloqueado:bool`, error codes
- [x] 1.7 GREEN: `IUsuarioIdentityGateway` + `IUsuarioServicioComandos` — `Bloquear/Desbloquear/EliminarAsync`, quitar `Desactivar/Reactivar`
- [x] 1.8 GREEN: `UsuarioIdentityGateway.cs` — `Bloquear/Desbloquear/EliminarAsync` usando `SetLockoutEndAsync`/`DeleteAsync`; `QueryAsync` filtra `LockoutEnd>UtcNow`; `CrearAsync` sin `!IsDeleted`
- [x] 1.9 GREEN: `AuthServicio.LoginAsync` — invocar `IsLockedOutAsync` antes de `CheckPasswordAsync`, retornar null si bloqueado
- [x] 1.10 GREEN: Migración forward-only D7: (1) preflight, (2) backfill, (3) DROP FK, (4) DROP índices generados, (5) DROP cols, (6) DROP `IX_AspNetUsers_PersonaId`, (7) ADD UNIQUE `IX_AspNetUsers_PersonaId`, (8) ADD FK RESTRICT
- [x] 1.11 Regenerar `docs/migracion-inicial-sgv.sql` con `--idempotent`

## Phase 2: Core

- [x] 2.1 RED: Test `UsuarioServicioComandos.Bloquear/Desbloquear/EliminarAsync` auto-fence, idempotencia, doble delete 404
- [x] 2.2 GREEN: `UsuarioServicioComandos.cs` — `Bloquear/Desbloquear/EliminarAsync`, auditoría `BloqueoUsuario`/`DesbloqueoUsuario`/`EliminacionFisica`, quitar `ReactivarAsync`
- [x] 2.3 GREEN: Crear `RevalidatorCredenciales.cs` en `SGV.Api/Seguridad/` — `IRevalidatorCredenciales.SigueVigenteAsync(sub)`
- [x] 2.4 GREEN: Configurar `JwtBearerOptions.Events.OnTokenValidated` — invocar revalidator, fallback middleware re-intenta
- [x] 2.5 GREEN: Crear `CookiePrincipalRevalidator.cs` en `SGV.Web/Auth/` — hook `AddCookie.Events.OnValidatePrincipal`
- [x] 2.6 GREEN: `UsuariosController.cs` — `DELETE` 204, `POST /bloquear` 200, `POST /desbloquear` 200, auto-fence, quitar `PATCH /reactivar`; `status=bloqueadas`
- [x] 2.7 GREEN: DI registration en `Program.cs` Api + Web (revalidator, hooks)

## Phase 3: Web Layer

- [ ] 3.1 `IUsuarioApiClient.cs` + `UsuarioApiClient.cs` — `Eliminar/Bloquear/DesbloquearAsync`, `status=bloqueadas`, quitar `Reactivar`
- [ ] 3.2 `Index.cshtml.cs` — handlers `Bloquear/Desbloquear`, modal irreversible, ocultar auto-acciones, sin `LastDeletedId`/`OnPostReactivateAsync`
- [ ] 3.3 `Index.cshtml` — labels `bloqueadas`, modal sin `UserName`
- [ ] 3.4 `Details.cshtml.cs` — consulta DTO real, `returnStatus` solo decide vista, 404 recuperable
- [ ] 3.5 `Details.cshtml` — estado bloqueado/inexistente

## Phase 4: Tests

- [ ] 4.1 Unit: gateway `Bloquear/Desbloquear/EliminarAsync`, filtro `LockoutEnd>UtcNow` en QueryAsync
- [ ] 4.2 Unit: comando auto-fence (`AutoBloqueo`/`AutoEliminacion`), idempotencia bloqueo, doble delete 404
- [ ] 4.3 Unit: login `UsuarioBloqueado`, login sin lockout emite JWT
- [ ] 4.4 `MySqlFact`: migración idempotente (2º run), preflight, backfill datetime(6), FK CASCADE, Persona/Auditoría sobreviven
- [ ] 4.5 `MySqlFact`: API `DELETE` 204, `POST /bloquear` 200, auto-fence 403, doble DELETE 404
- [ ] 4.6 `MySqlFact`: Corte inmediato JWT — emitir JWT, bloquear, verificar 401
- [ ] 4.7 `MySqlFact`: Corte inmediato cookie — cookie previa a bloqueo redirige a `/auth/sign-in`
- [ ] 4.8 `MySqlFact`: segmentos `activas|bloqueadas` en web, modal irreversible, autoacciones ocultas

## Phase 5: Cleanup

- [ ] 5.1 Cero referencias a `IsDeleted` o `Reactivar` en toda la solución
- [ ] 5.2 `dotnet test SGV.slnx` verde + `bun run build` sin errores
- [ ] 5.3 Coherencia especificaciones base post-delta
