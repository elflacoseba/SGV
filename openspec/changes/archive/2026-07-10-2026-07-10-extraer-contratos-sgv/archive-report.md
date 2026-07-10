# Archive Report — `2026-07-10-extraer-contratos-sgv`

> Change archivado el **2026-07-10** sobre la rama `refactor/100-contracts-pr4-seguridad-cleanup`.
> Modo `hybrid`: este archivo en filesystem + observación homóloga en Engram.

## Resumen ejecutivo

| Campo | Valor |
|---|---|
| Issue cerrada | **#100** — extraer `SGV.Contracts` y romper la dependencia directa `SGV.Web → SGV.Api` |
| Tipo de change | **Refactor arquitectónico puro** (namespace-only, sin cambios funcionales observables) |
| Veredicto del verify | **PASS WITH WARNINGS** (sin CRITICAL) |
| Tasks completas | **33 / 33** (1.1 a 4.11) |
| PRs encadenados ejecutados | **4 / 4** (PR1 #108, PR2 #109, PR3 #110 mergeados a `develop`; PR4 `f924d945` aplicado localmente) |
| Delta spec funcional | **Ninguno** (declarado formalmente en Engram `#843`) |
| Grafo final | `Dominio ← Aplicacion ← Contracts ← {Api, Web}` |

## Outcome

El grafo de proyectos quedó materializado en disco y la arista `SGV.Web → SGV.Api` está **ROTA**.

```
$ dotnet list src/SGV.Web reference
Project reference(s)
--------------------
../SGV.Contracts/SGV.Contracts.csproj      # única referencia

$ dotnet list src/SGV.Api reference
Project reference(s)
--------------------
../SGV.Aplicacion/SGV.Aplicacion.csproj
../SGV.Contracts/SGV.Contracts.csproj
../SGV.Infraestructura/SGV.Infraestructura.csproj

$ dotnet list src/SGV.Aplicacion reference
Project reference(s)
--------------------
../SGV.Dominio/SGV.Dominio.csproj
../SGV.Contracts/SGV.Contracts.csproj

$ dotnet list src/SGV.Contracts reference
There are no Project to Project references in project src/SGV.Contracts.
```

- `SGV.Contracts` es **leaf** (sin ProjectReference de negocio).
- `SGV.Web` consume **únicamente** `SGV.Contracts`; ya no depende de `SGV.Api`.
- El wire contract (DTOs, requests, results, errors, `RolesSgv`) vive en `src/SGV.Contracts/{Auth, Organizacion, Habilidades, Seguridad}/`.
- Las interfaces de aplicación (`IAuthServicio`, `IUsuarioServicioComandos`, `IUsuarioActual`, etc.) permanecen en `SGV.Aplicacion` por design rule (puertos de aplicación ≠ wire-types).

## Specs — sin delta funcional

Este change **no creó ni modificó specs**. La observación Engram `#843` (`sdd/2026-07-10-extraer-contratos-sgv/spec`) declara formalmente:

> *"No corresponde crear delta spec funcional. La propuesta declara este cambio como refactor puro: New Capabilities: None, Modified Capabilities: None, sin cambios en endpoints, payloads JSON, autorización, validaciones ni reglas de negocio."*

Por tanto, `sdd-archive` **no sincronizó** nada a `openspec/specs/`. Los 8 specs vigentes quedan **preservados** (no rotos):

| Spec | Estado |
|---|---|
| `web-apiclient-transport-contract` | preservado |
| `puesto-management` | preservado |
| `cargo-management` | preservado |
| `habilidad-management` | preservado |
| `sgv-web-authentication` | preservado |
| `unidad-organizativa-crud` / `unidad-organizativa-web-listado` | preservado |
| `identity-user-role-management` | preservado |
| `skill-cargo-query-contract` / `cargo-skill-query-contract` / `persona-skill-query-contract` | preservado |

## PRs encadenados ejecutados

| PR | Tema | Estado |
|---|---|---|
| PR1 (#108) | Auth — crear `SGV.Contracts`, mover `AuthApiRoutes`, eliminar `src/SGV.Api/Contracts/` | mergeado a `develop` |
| PR2 (#109) | Organizacion — DTOs/Requests/Results/Errors; abre arista `Aplicacion → Contracts` | mergeado a `develop` |
| PR3 (#110) | Habilidades — DTOs/Requests/Results; resuelve dependencia sobre Organización | mergeado a `develop` |
| PR4 (`f924d945`) | Seguridad + cleanup — `RolesSgv` + tipos de Usuario, rompe `Web → Api`, docs | aplicado localmente en `refactor/100-contracts-pr4-seguridad-cleanup` |

Cada PR deja build + tests verdes. El orden Auth → Organizacion → Habilidades → Seguridad quedó fijado por la dependencia `SkillCargoDetailDto → CargoDto`.

## Verificación final (verify-report #867)

- **Build**: ✅ 0 warnings / 0 errors sobre los 7 proyectos (incluido el nuevo `SGV.Contracts`).
- **Tests (approval testing)**: ✅ bit-idéntico al baseline.
  - Pre-refactor: 1625 total · 1613 passed · 12 failed.
  - Post-refactor: 1625 total · 1613 passed · 12 failed.
- **Greps estáticos**: todos en 0 (sin imports residuales de `SGV.Aplicacion` en Web, sin `SGV.Api.Contracts` en `src/`).
- **Grafo**: `Dominio ← Aplicacion ← Contracts ← {Api, Web}` confirmado.
- **Docs**: `AGENTS.md` y `docs/decisiones-implementacion.md` actualizadas.

## Issues & desviaciones (todas WARNING)

**No hay CRITICAL.**

| # | Issue | Naturaleza | Bloquea archive |
|---|---|---|---|
| 1 | 12 fallos pre-existentes en `OcupacionRepositoryTests` (bug #59 — `ActivePuestoIdUnique INT` vs `PuestoId CHAR(36)`) | pre-existente, no introducido por este change, tracked en issue separada | No |
| 2 | `HabilidadDto`/`NivelHabilidadDto` adelantados en PR2 | documentada y coherente con D4 (orden por dependencia) | No |
| 3 | `PackageReference System.IdentityModel.Tokens.Jwt 8.14.0` directo en `SGV.Web.csproj` | compensa dependencia transitiva perdida al romper `Web → Api` | No |
| 4 | `UsuarioContracts.cs` (Aplicación) partido en dos (interfaces en Aplicación, wire-types en Contracts) | coherente con design rule "interfaces son puertos" | No |
| 5 | `IUsuarioActual` no se movió | es puerto de aplicación, no wire-type | No |
| 6 | Task 4.6 estimaba 7 tests modificados, reales fueron ~18 | sin impacto funcional | No |

Estas desviaciones están documentadas en el `verify-report.md` archivado y la observación Engram `#867`. **Ninguna bloquea el archive**.

## Trazabilidad de artefactos

| Artefacto | Filesystem (en archive) | Engram ID |
|---|---|---|
| proposal | `proposal.md` | — |
| spec (no-delta) | — | `#843` (`sdd/2026-07-10-extraer-contratos-sgv/spec`) |
| design | `design.md` | — |
| tasks | `tasks.md` (33/33 `[x]`) | — |
| apply-progress | — | `#848` (`sdd/2026-07-10-extraer-contratos-sgv/apply-progress`) |
| verify-report | `verify-report.md` | `#867` (`sdd/2026-07-10-extraer-contratos-sgv/verify-report`) |
| archive-report | `archive-report.md` (este archivo) | `sdd/2026-07-10-extraer-contratos-sgv/archive-report` |
| exploration (opcional) | `exploration.md` | — |

## Estado del ciclo SDD

- **Change cerrado**: ✅
- **Issue #100 cerrada**: ✅
- **Cero delta funcional**: ✅ (refactor puro, preserva los 8 specs vigentes)
- **Grafo objetivo cumplido**: ✅ (`Dominio ← Aplicacion ← Contracts ← {Api, Web}`)
- **`SGV.Web` ya no referencia `SGV.Api`**: ✅
- **Archive move**: ✅ (`openspec/changes/2026-07-10-extraer-contratos-sgv/` ya no existe como cambio activo; vive en `openspec/changes/archive/2026-07-10-2026-07-10-extraer-contratos-sgv/`)

## Next

- El próximo cambio SDD que toque contratos wire-types debe vivir en `src/SGV.Contracts/` desde el inicio.
- El bug #59 (12 fallos `OcupacionRepositoryTests` por incompatibilidad `ActivePuestoIdUnique INT` vs `PuestoId CHAR(36)`) sigue pendiente en una SDD change propia, no relacionada con este archivado.
- Reviewers que vuelvan a este change pueden reconstruir el contexto desde los IDs de Engram listados arriba + el filesystem del archive.