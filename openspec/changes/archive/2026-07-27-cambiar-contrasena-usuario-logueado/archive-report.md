# Archive Report: Cambiar contraseña de usuario logueado

> Change: `2026-07-27-cambiar-contrasena-usuario-logueado` · Issue: #204
> Idioma: español · Fecha de archive: 2026-07-27
> Modo: hybrid (Engram + filesystem OpenSpec)

## Resumen

Se archivó el cambio que habilita a un usuario autenticado a cambiar su propia
contraseña desde la UI web. El flujo incluye endpoint `[Authorize]` en la API
con rate limiting (5 req / 15 min), rotación de `SecurityStamp`, cliente HTTP
autenticado en Web, nueva Razor Page `/auth/cambiar-contrasena`, ítem en el
topbar y banner de éxito en SignIn. El cambio se entregó en 3 PRs encadenados
(backend, endpoint API, web layer) con 27 tests enfocados verdes y build .NET
+ frontend correctos. No se requirió migración de BD.

## Specs Synced

| Domain | Action | Details |
|--------|--------|---------|
| `password-change` | Created | Nueva spec full: endpoint autenticado, rate limiting, rotación de `SecurityStamp`, mensajes y wire-types. 7 requisitos, 14 escenarios. |
| `password-change-web` | Created | Nueva spec full: Razor Page, topbar, flujo POST, cliente HTTP y banner. 8 requisitos, 14 escenarios. |
| `web-apiclient-transport-contract` | Updated (ADDED) | Se agregaron 3 requisitos (ChangePasswordAsync autenticado, traducción HTTP→Outcome, firma del contrato) con 12 escenarios nuevos. |

## Archive Contents

- `proposal.md` ✅ — Propuesta completa del cambio.
- `design.md` ✅ — Diseño técnico detallado (17 secciones).
- `tasks.md` ✅ — 10 tareas implementadas, todas `[x]` completas.
- `specs/` ✅ — 3 specs delta:
  - `specs/password-change/spec.md`
  - `specs/password-change-web/spec.md`
  - `specs/web-apiclient-transport-contract/spec.md`
- `verify-report.md` ✅ — Verificación final **PASS WITH WARNINGS**, sin CRITICAL.

## Source of Truth Updated

Los siguientes specs ahora reflejan el nuevo comportamiento:

- `openspec/specs/password-change/spec.md` — nuevo dominio backend de cambio de contraseña autenticado.
- `openspec/specs/password-change-web/spec.md` — nuevo dominio web de cambio de contraseña.
- `openspec/specs/web-apiclient-transport-contract/spec.md` — extendido con el contrato de `ChangePasswordAsync`.

## Task Completion Summary

| Tarea | Estado | PR |
|-------|--------|----|
| T-1 Contracts | ✅ | PR1 |
| T-2 Validator + Interface (TDD) | ✅ | PR1 |
| T-3 Infra Service + DI | ✅ | PR1 |
| T-4 Rate Limiter | ✅ | PR1 |
| T-5 API Endpoint (TDD) | ✅ | PR2 |
| T-6 Web Client (TDD) | ✅ | PR3 |
| T-7 Razor Page (TDD) | ✅ | PR3 |
| T-8 Topbar + Smoke | ✅ | PR3 |
| T-9 SignIn Banner | ✅ | PR3 |
| T-10 Docs (SQL) | ✅ | PR3 |

## Pending Items (SUGGESTIONS from verify-report)

- Agregar en una iteración futura test runtime dedicado para `ConfirmPassword != NewPassword` en el endpoint API.
- Agregar test runtime dedicado para dos bearer distintos del mismo subject compartiendo el bucket de rate limit.
- Revisar warnings `NU1510` preexistentes del build.
- Actualizar Browserslist/caniuse-lite fuera del alcance de este cambio.

## SDD Cycle Complete

El cambio fue completamente planificado, implementado, verificado y archivado.
Todos los artefactos están preservados en el audit trail.
