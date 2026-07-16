# Apply Progress: 2026-07-15-quita-soft-delete-usuario

## Phase 1: Foundation — COMPLETED (merged PR #150)
## Phase 2: Core — COMPLETED (merged PR #151)
## Phase 3: Web Layer — COMPLETED (merged PR #152)

## Phase 4: Tests — STARTED 2026-07-16

Rama: `feat/quita-soft-delete-usuario-tests` desde `origin/feat/quita-soft-delete-usuario` (c039a2cb).
PR target: `feat/quita-soft-delete-usuario` (feature-branch-chain).

### State pre-Phase 4

Tasks 4.1-4.3 (unit tests) already implemented as part of Phase 1-3:
- 4.1 gateway tests: `BloquearDesbloquearEliminarGatewayTests`, `UsuarioIdentityGatewayTests` (MySqlFact + unit)
- 4.2 command auto-fence tests: `UsuarioServicioComandosTests`
- 4.3 login tests: `SoftDeletedUserLoginTests`

### Scope confirmado por el usuario (interactive preflight)
Core Q1: tasks 4.4, 4.5, 4.6 only.
- 4.4 `[MySqlFact]`: Migración idempotente contra MySQL real
- 4.5 `[MySqlFact]`: API endpoints (DELETE 204, POST /bloquear 200, auto-fence 403, doble DELETE 404) contra MySQL real
- 4.6 `[MySqlFact]`: Corte inmediato JWT — emitir JWT, bloquear usuario, verificar 401

Excluidos:
- 4.7 (cookie corte): end-to-end demasiado complejo (API+Web+MySQL+cookie); requiere CI pipeline
- 4.8 (web segments): ya cubierto por tests con fakes en Phase 3
- Cookie corte Q1 verificación diferida a smoke test manual o Phase 5

### Tasks

- [ ] 4.4 `[MySqlFact]`: migración idempotente (2º run), preflight fail-loud, backfill datetime(6), FK CASCADE, Persona/Auditoría sobreviven
- [ ] 4.5 `[MySqlFact]`: API `DELETE` 204, `POST /bloquear` 200, auto-fence 403, doble DELETE 404
- [ ] 4.6 `[MySqlFact]`: Corte inmediato JWT — emitir JWT, bloquear, verificar 401

### Resultado esperado al cierre de Phase 4
- `dotnet test SGV.slnx` verde con nuevos MySqlFact tests
- `dotnet build` sin warnings nuevos
- Tasks 4.4-4.6 marcadas `[x]`
- Los tests de corte inmediato JWT cierran el observable Q1 del change
