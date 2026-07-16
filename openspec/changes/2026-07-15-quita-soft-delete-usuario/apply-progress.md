# Apply Progress: 2026-07-15-quita-soft-delete-usuario

## Estado

- Rama: `feat/quita-soft-delete-usuario-core`
- Worktree: `quita-soft-delete-usuario-core`
- Estrategia: `feature-branch-chain`; este lote corresponde al PR 2 / Core y apunta al tracker `feat/quita-soft-delete-usuario`.
- Modo de pruebas: **Strict TDD** (`openspec/config.yaml: strict_tdd: true`).
- Phase 1: completada y mergeada al tracker mediante PR #150 (`89a3f6ef`).
- Phase 2: completada, tareas 2.1–2.7 marcadas `[x]`.
- Alcance de este lote: únicamente Core; no se modificaron Pages de Usuarios, clientes Web de Usuarios, migraciones, `LockoutOptions` ni el placeholder Q1.

## Phase 1: Foundation — COMPLETED

La implementación de Phase 1 ya estaba presente en la base de este worktree:

- `IsDeleted` y columnas generadas retiradas del modelo Identity.
- Migración forward-only con backfill a `LockoutEnd` y unicidad plana de `PersonaId`.
- Gateway Identity con `BloquearAsync`, `DesbloquearAsync`, `EliminarAsync` y segmentación por lockout.
- `AuthServicio.LoginAsync` valida lockout antes de contraseña.
- Contracts con `UsuarioDto.Bloqueado` y segmento `Bloqueadas`, conservando temporalmente el alias `Eliminadas` para Phase 3.

## Phase 2: Core — COMPLETED

- [x] 2.1 RED: se agregaron pruebas de comando para bloqueo idempotente, desbloqueo idempotente, doble eliminación 404, eliminación inexistente sin auditoría y auto-fence existente. También se agregaron pruebas HTTP RED de los nuevos endpoints y contratos estructurales de los revalidators.
- [x] 2.2 GREEN: `UsuarioServicioComandos` ahora audita sólo transiciones reales de bloqueo/desbloqueo; conserva la auditoría `EliminacionFisica` después de comprobar existencia. Se retiraron los wrappers `DesactivarAsync`/`ReactivarAsync` de `IUsuarioIdentityGateway`, `IUsuarioServicioComandos`, `UsuarioServicioComandos` y `UsuarioIdentityGateway`.
- [x] 2.3 GREEN: se creó `src/SGV.Api/Seguridad/RevalidatorCredenciales.cs` con `IServiceScopeFactory`, `UserManager<SgvIdentityUser>` y `IsLockedOutAsync` por request.
- [x] 2.4 GREEN: `SGV.Api/Program.cs` registra el revalidator singleton, conserva handlers JWT previos al post-configure y agrega `OnTokenValidated`. Se agregó middleware fallback después de `UseAuthentication`, restringido al esquema bearer para no alterar esquemas de prueba u otros autenticadores.
- [x] 2.5 GREEN: se creó `src/SGV.Web/Auth/CookiePrincipalRevalidator.cs` y se conectó a `AddCookie.Events.OnValidatePrincipal`. Valida la cookie contra un cliente HTTP dedicado, sin `ApiBearerTokenHandler`, para evitar reentrancia de `GetTokenAsync` durante la propia validación cookie.
- [x] 2.6 GREEN: `UsuariosController` expone `DELETE` físico 204, `POST /{id}/bloquear` 200 y `POST /{id}/desbloquear` 200; retiró `PATCH /reactivar` y mantiene `status=bloqueadas`. Se actualizó `FakeUsuarioServicioComandos` y se agregaron pruebas de éxito, autorización y auto-fence.
- [x] 2.7 GREEN: DI actualizado en API y Web para revalidator, cliente HTTP dedicado y hooks de autenticación.

## Evidencia TDD

| Tarea | Archivo(s) de prueba | Safety net | RED | GREEN | Triangulación | Refactor |
|------|-----------------------|------------|-----|-------|---------------|----------|
| 2.1 | `UsuarioServicioComandosTests.cs`, `UsuariosControllerTests.cs` | 26/26 comando; API inicial bloqueada por restore faltante, luego 24/24 pre-change | 2 fallos de comportamiento + 1 desbloqueo idempotente; controller compilación falló al retirar wrappers | 30/30 comandos; 29/29 controller | bloqueo repetido, lockout existente, unlock ya activo, doble delete, inexistente, endpoints y errores 403 | Guards de transición y fake mutable |
| 2.2 | `UsuarioServicioComandosTests.cs` | 26/26 | Fallos de auditoría duplicada y estado bloqueado observable | 30/30 | happy path + lockout/unlock ya vigente + delete 404 | Wrappers eliminados y auditoría condicionada |
| 2.3 | `RevalidatorCredencialesTests.cs` | N/A (tipo nuevo) | Falló compilación por tipos inexistentes | 2/2 | contrato y constructor DI | XML docs y scope async |
| 2.4 | `JwtCookiePipelineQ1RedTests.cs` (placeholder existente) | 2/2 | Placeholder permaneció sin reemplazarse | 2/2 después del hook | evento JWT y fallback bearer; Q1 real sigue diferido | middleware no revalida esquemas no-bearer |
| 2.5 | `CookiePrincipalRevalidatorTests.cs` | N/A (tipo nuevo) | Falló compilación por clase inexistente | 3/3 | API 200, API 404 y contrato del handler | cliente dedicado evita reentrancia cookie |
| 2.6 | `UsuariosControllerTests.cs` | 24/24 API antes del rediseño | Compile failure al retirar wrappers + endpoints nuevos ausentes | 29/29 | 204, dos 200, auto-eliminación/auto-bloqueo 403, no-admin | controller thin y `ApiResults` reutilizado |
| 2.7 | `WebCookieAuthenticationOptionsTests.cs`, Q1 placeholder | 2/2 + 2/2 | Hooks ausentes antes de GREEN | 68/68 focused total; full suite verde | API/Web schemes y opciones cookie | preservación de Events existentes en JWT |

## Work Unit Evidence

| Evidencia | Resultado |
|----------|-----------|
| Focused test command | `dotnet test tests/SGV.Tests/SGV.Tests.csproj --filter "FullyQualifiedName~UsuarioServicioComandosTests\|FullyQualifiedName~UsuariosControllerTests\|FullyQualifiedName~RevalidatorCredencialesTests\|FullyQualifiedName~CookiePrincipalRevalidatorTests\|FullyQualifiedName~JwtCookiePipelineQ1RedTests\|FullyQualifiedName~WebCookieAuthenticationOptionsTests" --no-restore` → **68/68 PASS** |
| Build | `dotnet build SGV.slnx --no-restore` → **0 errors, 0 warnings** |
| Runtime/full harness | `DOTNET_USE_POLLING_FILE_WATCHER=1 dotnet test SGV.slnx --no-restore` → **2359/2359 PASS**; tres corridas consecutivas `--no-build` también **2359/2359 PASS** |
| Rollback boundary | Revertir los archivos de `src/SGV.Api/Seguridad`, `src/SGV.Api/Program.cs`, `src/SGV.Api/Controllers/UsuariosController.cs`, `src/SGV.Aplicacion/Seguridad/Usuarios`, `src/SGV.Infraestructura/Seguridad/UsuarioIdentityGateway.cs`, `src/SGV.Web/Auth`, `src/SGV.Web/Program.cs` y los tests Core listados abajo; no involucra migraciones ni Pages/Integration de usuarios Web. |

## Desviaciones y decisiones

1. `SGV.Web` referencia sólo `SGV.Contracts` y no registra `UserManager<SgvIdentityUser>`. Por eso `CookiePrincipalRevalidator` no puede compartir la implementación API directa del diseño: usa un cliente HTTP dedicado con el bearer almacenado en `AuthenticationProperties`. Respuestas 401/403/404 invalidan la cookie; errores de transporte/5xx preservan temporalmente la cookie para no cerrar sesión por indisponibilidad del upstream.
2. El middleware fallback API sólo revalida identidades del esquema `Bearer`. Sin este filtro, los tests y cualquier esquema alternativo (`Test`) sufrían 401 porque el fallback consultaba DB para credenciales que no eran JWT.
3. `UsuarioContractsTests.cs` se ajustó para reconocer `UsuarioDto.Bloqueado`, que ya existía desde Phase 1 pero dejaba la suite completa en rojo por una aprobación de constructor desactualizada.
4. No se tocó `UsuarioSegmentoListado.Eliminadas`; queda para Phase 3 según la exclusión explícita.

## Validación final

- Tests iniciales de comandos antes de cambios: 26/26 PASS.
- Tests finales focalizados: 68/68 PASS.
- Suite final: 2359/2359 PASS, 0 skipped.
- Delta contra baseline reportado por Phase 1 (2343): **+16 tests netos**.
- Build final: PASS.

## Pendientes fuera de Phase 2

- `LockoutOptions` explícito en `Program.cs`.
- Test RED Q1 real de orden de hooks y corte inmediato con MySQL.
- Tests de redireccionamiento de wrappers `[Obsolete]` (los wrappers de comandos fueron retirados).
- MySqlFact end-to-end de corte inmediato JWT/cookie (Phase 4.6/4.7).
- Cache Q2 por `sub`.
- Retiro de `UsuarioSegmentoListado.Eliminadas` y actualización de Web client/Pages (Phase 3).

## Estado de tareas

- Phase 1: 11/11.
- Phase 2: 7/7.
- Change completo: 18/34 tareas; Phase 3–5 permanecen pendientes.
- Próximo paso para este slice: `sdd-verify` después de que el orchestrator revise la desviación de arquitectura Web y el tratamiento fail-open de errores de transporte.
