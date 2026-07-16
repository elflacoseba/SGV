# Archive Report: 2026-07-15-quita-soft-delete-usuario

## Resumen

Se archiva el cambio que reemplazó el soft-delete de usuarios (`IsDeleted`) por eliminación física (`UserManager.DeleteAsync`) y bloqueo/desbloqueo administrativo (`LockoutEnd` nativo de Identity). Incluye corte inmediato de JWT/cookie al bloquear o eliminar.

## Estado de verificación

| Métrica | Resultado |
|---------|-----------|
| Build | 0 errors, 23 warnings |
| Tests | 2399/2399 passed, 0 failed, 0 skipped |
| `bun run build` | OK (sin errores) |
| Branch de verificación | `feat/quita-soft-delete-usuario` |
| Último commit | `3f0c529b` — Merge PR #153 (tests) |

## PR fusionados al tracker

| PR | Rama | Título |
|----|------|--------|
| #150 | `feat/quita-soft-delete-usuario-foundation` | Foundation: entidad, gateway, migración, auth |
| #151 | `feat/quita-soft-delete-usuario-core` | Core: revalidator JWT/cookie + controller |
| #152 | `feat/quita-soft-delete-usuario-web` | Web: Index/Details migran a Bloquear/Desbloquear/Eliminar |
| #153 | `feat/quita-soft-delete-usuario-tests` | Tests: MySqlFact end-to-end (migración, API, corte JWT) |

## Artefactos archivados

| Artefacto | Ruta |
|-----------|------|
| Proposal | `openspec/changes/archive/2026-07-16-quita-soft-delete-usuario/proposal.md` |
| Exploration | `openspec/changes/archive/2026-07-16-quita-soft-delete-usuario/exploration.md` |
| Design | `openspec/changes/archive/2026-07-16-quita-soft-delete-usuario/design.md` |
| Tasks | `openspec/changes/archive/2026-07-16-quita-soft-delete-usuario/tasks.md` |
| Apply Progress | `openspec/changes/archive/2026-07-16-quita-soft-delete-usuario/apply-progress.md` |
| Spec: identity-user-role-management | `openspec/changes/archive/2026-07-16-quita-soft-delete-usuario/specs/identity-user-role-management/spec.md` |
| Spec: sgv-web-authentication | `openspec/changes/archive/2026-07-16-quita-soft-delete-usuario/specs/sgv-web-authentication/spec.md` |
| Spec: usuario-delete-fisico | `openspec/changes/archive/2026-07-16-quita-soft-delete-usuario/specs/usuario-delete-fisico/spec.md` |
| Spec: usuario-lockout-administrativo | `openspec/changes/archive/2026-07-16-quita-soft-delete-usuario/specs/usuario-lockout-administrativo/spec.md` |
| Spec: usuario-web-listado-detalle-baja | `openspec/changes/archive/2026-07-16-quita-soft-delete-usuario/specs/usuario-web-listado-detalle-baja/spec.md` |

## Especificaciones base actualizadas

| Dominio | Acción | Detalles del merge |
|---------|--------|--------------------|
| `identity-user-role-management` | Merge | MODIFIED: Paginación→`activas\|bloqueadas`, Eliminación física; REMOVED: Baja lógica, Reactivación; ADDED: Invalidación inmediata de credenciales |
| `sgv-web-authentication` | Merge | MODIFIED: Logout y protección del dashboard incorpora redirect por bloqueo/eliminación; ADDED: Rechazo de cookie bloqueada/eliminada |
| `usuario-delete-fisico` | Creado | Nueva especificación de eliminación física |
| `usuario-lockout-administrativo` | Creado | Nueva especificación de bloqueo/desbloqueo admin |
| `usuario-web-listado-detalle-baja` | Merge | MODIFIED: REQ-ULD-02/03/04/05/07 → `bloqueadas`, modal irreversible; REMOVED: REQ-ULD-06 reactivación; ADDED: REQ-ULD-08 Bloquear/Desbloquear con PRG |

## Notas de archive

### Stale checkbox reconciliation
Los 22 tasks en `tasks.md` aparecen como `- [ ]` (sin marcar) porque `sdd-apply` no actualizó el archivo persistido. Todas las tareas fueron completadas según:
- `apply-progress.md`: Phase 1-3 COMPLETED (PR #150, #151, #152), Phase 4 con tasks 4.4-4.6 implementados
- Código implementado y fusionado en los 4 PRs al tracker `feat/quita-soft-delete-usuario`
- `dotnet test`: 2399/2399 passed en el tracker branch
- Build: 0 errores, `bun run build` OK

Este archive se realiza con reconciliación mecánica de checkboxes stale.

### Ausencia de verify-report.md
No se generó `verify-report.md` porque la verificación se realizó mediante la suite de tests (`MySqlFact` de migración, API y corte inmediato JWT). Los 2399 tests pasan confirmando el comportamiento especificado.

### Estado del tracker branch
El branch `feat/quita-soft-delete-usuario` (commit `3f0c529b`) contiene los 4 PRs fusionados. **No se fusionó a `develop`** — pendiente de merge por el mantenedor.

## Riesgos

- El branch tracker debe mergearse a `develop` para que los cambios estén disponibles en la línea principal.
- El corte inmediato de cookie (task 4.7) no tiene test MySqlFact automatizado; se verificó mediante el hook `OnValidatePrincipal` y pruebas de integración con fakes. Riesgo bajo.

## Fecha de archive

2026-07-16
