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
- Retiro de `UsuarioSegmentoListado.Eliminadas` (queda para Phase 5/cleanup).

## Phase 3: Web Layer — COMPLETED

- [x] 3.1 `IUsuarioApiClient` + `UsuarioApiClient` migrados: `EliminarAsync` (DELETE 204 con `Value:null`), `BloquearAsync` (POST `/{id}/bloquear`), `DesbloquearAsync` (POST `/{id}/desbloquear`). `DesactivarAsync` y `ReactivarAsync` retirados del interface; `DeleteAsync` queda como alias default-implemented de `EliminarAsync` para no romper call sites legacy.
- [x] 3.2 `Index.cshtml.cs` reescrito: handlers `OnPostBloquearAsync`, `OnPostDesbloquearAsync`, `OnPostDeleteAsync` (hard-delete). `OnPostReactivateAsync` retirado. `LastDeletedId` eliminado. `EsAutoAccion(id)` server-side fence contra AutoBloqueo/AutoEliminacion (devuelve feedback inline y redirige sin llamar a la API). El segmento exitoso `Bloquear` redirige a `bloqueadas`; `Desbloquear` y `Eliminar` a `activas`.
- [x] 3.3 `Index.cshtml` actualizado: labels `Bloqueados` (segmento toggle), botones `Bloquear`/`Desbloquear` con auto-fence, modal irreversible sin `UserName` (sólo "este usuario"). Sin banner de Reactivar tras Delete. El segmento `bloqueadas` no muestra Delete ni Bloquear (sólo Desbloquear).
- [x] 3.4 `Details.cshtml.cs` extendido: `Bloqueado` y `CurrentUserId`/`EsAutoAccion` ahora son parte del estado de la página; `returnStatus` queda como hint de view para el link "Volver al listado" pero NO es fuente de verdad — el render del banner "Cuenta bloqueada" y de los botones se decide desde el DTO. 404 recuperable con link "Volver al listado" (también cubre errores de transporte que caen al catch-all).
- [x] 3.5 `Details.cshtml` actualizado: banner amarillo "Cuenta bloqueada" visible cuando `Bloqueado=true`; acciones según estado: Bloqueado ⇒ sólo Desbloquear; Activo+self ⇒ sólo Edit; Activo+otro ⇒ Edit + Bloquear + Eliminar; NotFound ⇒ mensaje + Volver al listado.

## Evidencia TDD

| Tarea | Archivo(s) de prueba | Safety net | RED | GREEN | Triangulación | Refactor |
|------|-----------------------|------------|-----|-------|---------------|----------|
| 3.1 | `UsuarioApiClientBloquearDesbloquearEliminarTests.cs`, `IUsuarioApiClientContractTests.cs`, `UsuarioApiClientBasicTests.cs` (Removed Reactivar tests) | 19/19 cliente antes; suite cayó a errores de compilación cuando se retiró `DesactivarAsync`/`ReactivarAsync` | Compile failure + nuevos RED tests | 6/6 nuevos + 9/9 contrato + 8/8 cliente refactorizado | 204 vs 200 con body, 403 AutoBloqueo/AutoEliminacion, 404 doble delete | Interfaz actualizada con alias DeleteAsync→EliminarAsync |
| 3.2 | `IndexPageTests.cs` (reescrito), `FakeUsuarioApiClientTests.cs` | 24/24 Index antes | Compile failure al retirar `Desactivar`/`Reactivar` | 18/18 IndexPageTests + 8/8 Fake | Bloquear/Desbloquear/Eliminar, auto-fence UI, AutoBloqueo/AutoEliminacion feedback, transport recuperable | Helper `EsAutoAccion` en PageModel + helper análogo en Index.cshtml |
| 3.3 | n/a (view, sin PageModel test dedicado) | n/a | Banner eliminado, modal reactivate removido, etiquetas actualizadas | Render verificado vía `Post_Delete_WhenSuccessful_RedirectsToActiveSegmentWithFeedback`, `Get_Index_WhenSegmentIsBloqueadas_ExposesOnlyDesbloquearAction`, `Get_Index_WhenCurrentUserListed_HidesBloquearAndDeleteActions` | n/a | Refactor mínimo — sólo labels + modal |
| 3.4 | `DetailsPageTests.cs` (extendido) | 7/7 Details antes; tests viejos siguen pasando con la nueva semántica DTO-source-of-truth | 4 nuevos RED | 11/11 Details | 404 recuperable, transport degradado, Bloqueado-banner-overridea-returnStatus, Bloqueado+admin ve Desbloquear | Helper `EsAutoAccion` y propiedad `Bloqueado` (DTO-truth) |
| 3.5 | n/a (view) | n/a | Banner nuevo + acciones reordenadas + Volver al listado en 404 | Render verificado vía tests 3.4 (banner string, form data-attributes) | n/a | n/a |

## Archivos modificados (Phase 3)

**Producción**
- `src/SGV.Web/Integration/Usuarios/IUsuarioApiClient.cs` — interface migrado
- `src/SGV.Web/Integration/Usuarios/UsuarioApiClient.cs` — implementar `EliminarAsync` (con soporte 204), `BloquearAsync`, `DesbloquearAsync`; quitar `DesactivarAsync`, `ReactivarAsync`
- `src/SGV.Web/Pages/Seguridad/Usuarios/Index.cshtml.cs` — handlers `Bloquear`/`Desbloquear`/`Delete` (hard-delete); helper `EsAutoAccion`; eliminado `LastDeletedId`/`Reactivar`
- `src/SGV.Web/Pages/Seguridad/Usuarios/Index.cshtml` — labels `Bloqueados`, modal irreversible, auto-fence UI
- `src/SGV.Web/Pages/Seguridad/Usuarios/Details.cshtml.cs` — `Bloqueado`/`CurrentUserId`/`EsAutoAccion`; `returnStatus` es hint
- `src/SGV.Web/Pages/Seguridad/Usuarios/Details.cshtml` — banner bloqueada, acciones según estado, link "Volver al listado" en 404

**Tests**
- `tests/SGV.Tests/Web/Usuario/IUsuarioApiClientContractTests.cs` — tests `BloquearAsync`/`DesbloquearAsync`/`EliminarAsync` firma; assert NOT-exists `DesactivarAsync`/`ReactivarAsync`; recuenta total
- `tests/SGV.Tests/Web/Usuario/UsuarioApiClientBloquearDesbloquearEliminarTests.cs` — NUEVO (6 tests)
- `tests/SGV.Tests/Web/Usuario/UsuarioApiClientBasicTests.cs` — reemplazados tests de `DesactivarAsync` por `EliminarAsync`; quitados tests de `ReactivarAsync`
- `tests/SGV.Tests/Web/Usuario/UsuarioWebSeamTests.cs` — tests de seam migrados a `EliminarAsync`/`AutoEliminacion`
- `tests/SGV.Tests/Web/Usuario/FakeUsuarioApiClient.cs` — reescrito: modela `Eliminar` + `Bloquear`/`Desbloquear` con `SeedBlocked`; `ApplyStatusFilter` proyecta `Bloqueado` sobre el DTO
- `tests/SGV.Tests/Web/Usuario/FakeUsuarioApiClientTests.cs` — actualizado a segmentos `Activas|Bloqueadas` y a `Bloquear`/`Desbloquear`/`Eliminar`
- `tests/SGV.Tests/Web/Usuario/IndexPageTests.cs` — reescrito: 18 tests cubriendo Bloquear/Desbloquear/Eliminar + auto-fence UI + AutoBloqueo/AutoEliminacion feedback + transport recuperable
- `tests/SGV.Tests/Web/Usuario/DetailsPageTests.cs` — extendido: 11 tests cubriendo banner Bloqueado + 404 recuperable + transport degradado + returnStatus hint

## Desviaciones y decisiones (Phase 3)

1. **`Index.cshtml` redirige Bloquear→`bloqueadas`, no→`activas`.** Decisión UX: tras un bloqueo el admin quiere ver la cuenta recién bloqueada, no seguir revisando activas. La redirect preserva `search`/`sort`/`p` para mantener el contexto de filtrado.
2. **`Details.cshtml` considera `Bloqueado` del DTO, no `returnStatus`.** El query string `returnStatus` se mantiene sólo como hint para el link "Volver al listado" (preserva `status=` en el back-link); el render del banner y de los botones sale del DTO. Esto previene una inconsistencia visible donde el caller pasa `activas` pero la cuenta está bloqueada.
3. **Helper `EsAutoAccion(id)` server-side + render-side.** Doble guard: el PageModel compara `CurrentUserId` con el id del form y devuelve feedback inline si hay match; el view además oculta los botones Bloquear/Eliminar sobre la fila del admin actual. Defensa en profundidad por si un form se construye fuera del flujo normal.
4. **`DeleteAsync` queda como default interface method** que delega en `EliminarAsync`. Mantiene source-compat con cualquier call site histórico que aún use `DeleteAsync` en el shell.
5. **`UsuarioSegmentoListado.Eliminadas` [Obsolete] sigue intacto.** Fuera de scope de Phase 3; Phase 5 (cleanup) lo retira junto con la rotación de tests legacy.
6. **No se regeneró `docs/migracion-inicial-sgv.sql`** porque Phase 3 no introduce migraciones (es Web-only).
7. **No se tocó `bun run build`** porque Phase 3 no modifica `wwwroot/**` ni `package.json`. Las nuevas clases Tabler (`ti ti-lock`, `ti ti-lock-open`) ya están presentes en el bundle del template Inspinia.

## Validación final (Phase 3)

- Focused test (Web.Usuario): 114/114 PASS.
- Full suite: 2382/2382 PASS, 0 skipped.
- Delta contra baseline 2366: **+16 net tests** (11 Details + 6 cliente nuevos + 18 IndexPageTests - tests viejos de Reactivar/Desactivar = ~+16).
- Build: 0 errors.
- Pre-existing tests revisitados: 0 regresiones.

## Estado de tareas

- Phase 1: 11/11.
- Phase 2: 7/7.
- Phase 3: 5/5.
- Change completo: 23/34 tareas; Phase 4 (tests) y Phase 5 (cleanup) permanecen pendientes.
- Próximo paso recomendado: `sdd-verify` para validar el slice Web contra las specs del delta.
