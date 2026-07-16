# Proposal: Quita soft-delete de usuarios y agrega bloqueo independiente

## Intent

El módulo Usuarios usa `IsDeleted` + columnas generadas en paralelo al lockout nativo de Identity. **Decisiones cerradas**: `DELETE` será hard-delete de la cuenta Identity; bloquear/desbloquear son independientes sobre `LockoutEnd`; y **bloquear o eliminar una cuenta DEBE cortar de inmediato el acceso de JWT bearer y cookie web ya emitidos**, sin esperar al `exp` natural.

## Scope

**In Scope.** Migración MySQL con backfill `IsDeleted=1` → `LockoutEnd` antes del DROP. Gateway con `Eliminar/Bloquear/DesbloquearAsync` y segmentos `Activos|Bloqueados`. `AuthServicio.LoginAsync` invoca `IsLockedOutAsync` antes de `CheckPasswordAsync`. **Corte inmediato**: tras bloquear o eliminar, la siguiente petición protegida MUST perder acceso (API `401`, Web cookie rechazada + redirect a `/auth/sign-in`). API: `DELETE`, `POST /bloquear`, `POST /desbloquear` (rol `Administrador`). Web con segmentos `activos|bloqueados` y modal irreversible. Pruebas strict TDD.

**Out of Scope.** Borrar `Persona` o `Auditorias`. Cargos, Habilidades, UOs, Puestos, Ocupaciones, Personas, Skills. Rotación general de claves JWT o refresh tokens. CORS, hardening lateral.

## Capabilities

**New.** `usuario-delete-fisico` (hard-delete Identity). `usuario-lockout-administrativo` (bloqueo vía `LockoutEnd`).

**Modified.** `identity-user-role-management`: ciclo abandona baja lógica; **bloquear o eliminar MUST invalidar de inmediato JWT bearer y sesiones cookie emitidas** (observable). `sgv-web-authentication`: cuenta bloqueada o eliminada MUST rechazar la cookie y redirigir a `/auth/sign-in` por petición protegida. `usuario-web-listado-detalle-baja`: `eliminadas` → `bloqueados`; reactivación → desbloqueo.

## Approach

Backfill + DROP, gateway `UserManager`, `FluentValidation`, `[Authorize(Roles="Administrador")]`, UI modal irreversible. Mecanismo del corte: lo elige design.

## Affected Areas

`AuthServicio.cs`, `ApiBearerTokenHandler.cs`, `Program.cs`, `UsuarioServicioComandos.cs`, `UsuariosController.cs`, `Pages/Seguridad/Usuarios/*`, `Integration/Usuarios/UsuarioApiClient.cs`, `*_DropSoftDeleteUsuarios.cs`.

## Risks

| Riesgo | Mitigación |
|--------|------------|
| `CheckPasswordAsync` no valida lockout | `IsLockedOutAsync` antes |
| Backfill afecta usuarios activos | WHERE `IsDeleted = 1`; count pre/post |
| Auto-eliminación o auto-bloqueo | Validación server-side + UI que oculta |
| Migración no idempotente | `Database.Migrate()` reentrante; `MySqlFact` cubren 2º run |

## Rollback Plan

Restaurar `IsDeleted`, columnas e índices desde `__bkp_usuarios_softdelete`; revertir segmento a `Eliminadas` y `Desactivar/ReactivarAsync`; re-deploy previa. El corte inmediato se conserva.

## Dependencies

Migración forward-only. `Database.Migrate()` idempotente.

## Success Criteria

- [ ] Cero referencias a `IsDeleted` o segmento `Eliminadas`; migración idempotente convierte los previos a lockout.
- [ ] `DELETE` borra sólo la cuenta Identity; `Persona` y `Auditorias` permanecen.
- [ ] **Tras bloquear o eliminar, la siguiente petición protegida falla: API `401`, Web cookie rechazada + redirect a `/auth/sign-in`, sin esperar `exp`.**
- [ ] Desbloquear permite nueva sesión vía login; no revive tokens invalidados.
- [ ] Ningún `Administrador` puede bloquearse ni eliminarse a sí mismo.
- [ ] `dotnet test SGV.slnx` verde; `bun run build` sin errores.

## Open Questions

1. Mecanismo de corte. Lo decide design cumpliendo el observable.
2. Plazo del backfill: permanente o finito para `IsDeleted=1` históricos.
3. Lockouts temporales: indistinguibles de administrativos (cerrado: aceptado, sin `OrigenBloqueo`).