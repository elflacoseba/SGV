# Archive Report — setup-admin-inicial-issue-195

> **Issue**: [#195 — Crear una pantalla para crear el usuario Administrador](https://github.com/elflacoseba/SGV/issues/195)
> **Change**: `setup-admin-inicial-issue-195`
> **Archivado**: 2026-07-27
> **Modo**: OpenSpec

## Resumen ejecutivo

Se archivó el cambio que implementa el flujo completo de bootstrap del primer usuario Administrador del sistema. El cambio incluye backend (API con rate limiting, creación atómica de Persona + Usuario + rol, apertura anónima del catálogo TipoDocumento), frontend (Razor Page `/auth/setup` con 9 campos, typed client anónimo con cache, redirección desde SignIn) y documentación arquitectónica en `docs/decisiones-implementacion.md`. Todo el código está mergeado a `develop` vía 3 PRs encadenados y un tracker branch.

## Specs sincronizados

| Dominio | Acción | Detalles |
|---------|--------|----------|
| `setup-initial-admin` | **Creado** (nuevo) | 6 requirements (REQ-SETUP-001 a REQ-SETUP-006), 14 escenarios Given/When/Then |

La spec es nueva — no existía spec previa para este dominio. Se copió directamente de `specs/setup/spec.md` del change a `openspec/specs/setup-initial-admin/spec.md`.

## PRs mergeados

| PR | Rama | SHA | Estado |
|----|------|-----|--------|
| [#196 (PR1 backend)](https://github.com/elflacoseba/SGV/pull/196) | `feat/setup-admin-inicial-issue-195-pr1-backend` | Mergeado a develop `2026-07-24T19:17:23Z` | ✅ Mergeado |
| [#197 (PR2 frontend)](https://github.com/elflacoseba/SGV/pull/197) | `feat/setup-admin-inicial-issue-195-pr2-frontend` | Mergeado a develop `2026-07-24T19:28:07Z` | ✅ Mergeado |
| [#198 (PR3 docs)](https://github.com/elflacoseba/SGV/pull/198) | Docs | Mergeado a develop `2026-07-24T21:27:32Z` | ✅ Mergeado |
| [#199 (tracker)](https://github.com/elflacoseba/SGV/pull/199) | `feat/setup-admin-inicial-issue-195` | Mergeado a develop `2026-07-24T22:12:16Z` | ✅ Mergeado |

## Trabajo completado

| WU | Nombre | Archivos | Estado |
|----|--------|----------|--------|
| WU-1 | Contracts en `SGV.Contracts/Setup/` | 5 nuevos | ✅ Complete — PR #1 |
| WU-2 | `SetupServicio` (Aplicación + Infraestructura) + tests | 5 nuevos, 1 modificado | ✅ Complete — PR #1 |
| WU-3 | `SetupController` + rate limit + `[AllowAnonymous]` + tests | 7 nuevos, 3 modificados | ✅ Complete — PR #1 |
| WU-4 | Razor Page `/auth/setup` + `SetupApiClient` + tests web | 5 nuevos, 1 modificado | ✅ Complete — PR #2 |
| WU-5 | Filtro redirección en `SignIn` + cache + tests | 1 nuevo, 1 modificado | ✅ Complete — PR #2 |
| WU-6 | Documentación en `docs/decisiones-implementacion.md` | 1 modificado | ✅ Complete — PR #3 |

## Decisiones técnicas aplicadas

1. **Atomicidad por compensación (no transacción EF outer)**: Pomelo 9 + MySqlConnector rechazan `BeginTransactionAsync` anidados. En lugar de una transacción única, se implementó compensación: si Persona OK pero Usuario falla, se soft-deletea la Persona vía `DesactivarAsync`. Audit es best-effort. Documentado en `docs/decisiones-implementacion.md`.
2. **Defensa contra race condition**: El índice único `IX_AspNetUsers_NormalizedUserName` es la defensa real contra doble admin simultáneo, no la guarda `AnyUsersAsync()` (ejecutada fuera de transacción).
3. **`[AllowAnonymous]` en catálogo TipoDocumento**: `TiposDocumentoController.GetAll` se abrió a anónimos (necesario para el dropdown del formulario de setup). `GetById` mantiene `[Authorize]`.
4. **Rate limiting**: Política fixed window 5 requests / 15 minutos en `POST /api/v1/setup`.
5. **Fail-open + cache**: `SetupApiClient` anónimo usa `IMemoryCache` TTL 30s para status; fail-open ante `HttpRequestException`/`TaskCanceledException` retorna `RequiresSetup=false`.
6. **Chain strategy**: 3 PRs encadenados + tracker branch `feat/setup-admin-inicial-issue-195`.

## Warnings de verify-report.md (PR #1 backend)

| ID | Descripción | Severidad |
|----|-------------|-----------|
| W-001 | Atomicidad best-effort en lugar de transacción EF única. Estado final siempre consistente (1 admin + 0-1 Persona soft-deleted). | WARNING — aceptable con mitigación |
| W-002 | `AnyUsersAsync` ejecutado fuera de transacción. Defensa real es índice único Identity. | WARNING — aceptable con mitigación |
| W-003 | Test de fallo transaccional usa mock (no DB real). Comportamiento de compensación revisado estáticamente. | WARNING — no bloqueante |

## Warnings de verify-report-frontend.md (PR #2 frontend)

| ID | Descripción | Severidad |
|----|-------------|-----------|
| W-001 | Test de PRG no prueba el mensaje TempData en el GET posterior. Implementación estática asigna clave correcta. | WARNING — no bloqueante |
| W-002 | Tests integration del cache validan un fake que replica el TTL. El cache real está cubierto por `SetupApiClientTests`. | WARNING — aceptable con mitigación |
| W-003 | `_AuthLayout` no se aserta explícitamente en tests. Inspección estática confirma herencia por `_ViewStart`. | WARNING — no bloqueante |

No se detectaron hallazgos CRITICAL en ningún verify report.

## Archivos en el archive

- `proposal.md` ✅
- `specs/setup/spec.md` ✅
- `design.md` ✅
- `tasks.md` ✅ (6 WUs completos)
- `verify-report.md` ✅ (PR #1 backend)
- `verify-report-frontend.md` ✅ (PR #2 frontend)
- `exploration.md` ✅
- `archive-report.md` ✅ (este archivo)

## Source of Truth actualizado

- `openspec/specs/setup-initial-admin/spec.md` — spec completo del dominio setup inicial del Administrador (nuevo).

## SDD Cycle Complete

El cambio ha sido completamente planificado, implementado, verificado y archivado. 6 requirements, 14 escenarios, 3 PRs, ~2150 líneas estimadas.

**Veredicto final**: ✅ SUCCESS — archivado con warnings documentados, sin issues CRITICAL pendientes.
