# Propuesta: Implementa módulo Usuarios

## Intención

Cerrar el módulo Usuarios hoy backend-only: `UsuariosController` lista en plano con N+1 en `UserManager.GetRolesAsync`, no expone `/consulta` paginado, no hay baja lógica (`AspNetUsers` borra físicamente) y `SGV.Web` no tiene ni integration client ni Razor Pages. Calcar el patrón archivado `2026-07-14-frontend-crud-personas` (4 PRs encadenados) más fix N+1 y soft-delete con columna generada.

## Alcance

### Incluye
- Migración EF Core `IsDeleted TINYINT(1) NOT NULL DEFAULT 0` en `AspNetUsers` + columna generada `ActiveUserNameUnique` (`CASE WHEN IsDeleted=0 THEN UserName ELSE NULL END`) con índice único — replica `2026-07-11-fix-active-puesto-id-unique-type`.
- `GET /api/v1/usuarios/consulta?page=&pageSize=&search=&sort=&status=` → `PagedResult<UsuarioDto>` con `Include`/proyección que trae `Persona.Nombres|Apellidos` y roles en una sola query.
- `PUT /api/v1/usuarios/{id}` (UserName/Email/roles), `DELETE /api/v1/usuarios/{id}` (soft), `PATCH /api/v1/usuarios/{id}/reactivar`.
- `UsuarioDto` + `Nombres` + `Apellidos` (de `Persona`).
- `Integration/Usuarios/` con `ApiBearerTokenHandler` (10 s, análogo a `Cargo`/`Persona`).
- Razor Pages: `Pages/Seguridad/Usuarios/{Index, Create, Edit, Details}.cshtml(.cs)` + `_Form.cshtml` + ítem colapsable en `_Sidenav.cshtml`.
- Reactivación con PRG preservando `p|search|sort|status` y `PageFeedback` (análogo a Personas).
- `[Authorize(Roles = RolesSgv.Administrador)]` en POST/PUT/DELETE/PATCH; GETs autenticados.

### No incluye
- Login/OAuth/refresh, multi-tenant, gestión de sesiones, lockout/unlock, cambio de contraseña desde admin, auditoría extendida de login, typeahead de usuarios.
- CRUD de roles (`RolesSgv` fijo), habilidades de usuario.
- `[Obsolete]` removal de enums de error: queda para `sdd-archive` del change #125.

## Capacidades

### Nuevas
- `usuario-web-listado-detalle-baja`: Index segmentado `activas|eliminadas`, Details readonly, baja lógica y reactivación con PRG (espejo de los requisitos web de `persona-management`).
- `usuario-web-crear-editar`: Create con dropdown de Personas activas, Edit de UserName/Email/roles, ambos `[Authorize(Roles=Administrador)]` con redirect `/error/403` y `Forbid()` en POST.

### Modificadas
- `identity-user-role-management`: paginación `/consulta`, soft-delete vía `IsDeleted`, edición de UserName/Email, DTO con `Nombres|Apellidos`, taxonomía `ErrorCategoria` (regla #125).

## Enfoque

| Área | Decisión |
|---|---|
| Migración | EF Core `AddSoftDeleteToAspNetUsers`; `SgvIdentityUserConfiguracion` agrega `IsDeleted` + `HasComputedColumnSql`. |
| `/consulta` | `UsuarioIdentityGateway.QueryAsync` proyecta `Persona.Nombres|Apellidos` con `Include`; agrupa roles vía `GroupJoin`/`ToDictionary` en una sola query. |
| `UsuarioDto` | Sumar `Nombres`, `Apellidos` (nullable: defensa por si la Persona aún no está proyectable). |
| Cliente tipado | `IUsuarioApiClient`: `GetAllActivasAsync()` (catálogo), `QueryAsync(UsuarioListQuery)`, `GetByIdAsync`, `CreateAsync`, `UpdateAsync`, `DesactivarAsync`, `ReactivarAsync`. |
| Pages Razor | `PageFeedback` + `IAuthSessionRedirector` + `TransportFailureClassifier.IsTransportFailure`; switch exhaustivo sobre `ErrorCategoria` (CS8524 endémico aceptado). |
| Reactivación | `PATCH` + TempData + `LastDeletedId`; si la `PersonaId` asociada está `IsDeleted = 1` → `ErrorCategoria.Conflict` con mensaje accionable. |
| Autorización | `[Authorize(Roles=Administrador)]` por acción en controller. UI gating con `EsAdministrador` en Index y redirect `/error/403` en Create/Edit. |

## Áreas afectadas (resumen por capa)

- **Contracts** (`src/SGV.Contracts/Seguridad/Usuarios/`): `UsuarioContracts.cs` modificado — + `Nombres`/`Apellidos`, + `UsuarioListQuery`/`SegmentoListado`/`ListadoDto`.
- **Aplicación** (`src/SGV.Aplicacion/Seguridad/Usuarios/`): `UsuarioContracts.cs` modificado (`IUsuarioServicioConsulta.QueryAsync`), `UsuarioServicioComandos.cs` modificado (+ `ActualizarAsync`/`DesactivarAsync`/`ReactivarAsync`).
- **Infraestructura**: `SgvIdentityUser.cs` (+ `IsDeleted`), `UsuarioIdentityGateway.cs` (`QueryAsync` sin N+1, + `Actualizar/Desactivar/Reactivar`), `SgvIdentityUserConfiguracion.cs` (columna + índice único), `Persistencia/Migraciones/*` nueva `AddSoftDeleteToAspNetUsers`.
- **API** (`src/SGV.Api/Controllers/UsuariosController.cs`): + `GetConsulta`, + `Update/Desactivar/Reactivar`.
- **Web** (`src/SGV.Web/`): `Integration/Usuarios/*.cs` nuevo; `Pages/Seguridad/Usuarios/{Index,Create,Edit,Details}.cshtml(.cs)` + `_Form.cshtml` nuevos; `_Sidenav.cshtml` (+ ítem "Usuarios"); `Program.cs` (+ `AddHttpClient<IUsuarioApiClient, UsuarioApiClient>`).
- **Tests**: `Api/UsuariosControllerTests.cs` + tests `[MySqlFact]`; `Aplicacion/Seguridad/UsuarioServicioComandosTests.cs` extendido; `tests/SGV.Tests/Web/Usuario/**` nuevo (~80 tests).

## PR slicing sugerido (feature-branch-chain, 4 PRs)

> Estrategia `ask-always` y budget 800 líneas ⇒ chained PRs obligatorio. Tracker: `feat/2026-07-15-implementa-modulo-usuarios-tracker`.

| PR | Scope | Tests | Forecast |
|---|---|---|---|
| 1 | Backend: migración IsDeleted, columna generada, wire-types paginados, `GET /consulta` sin N+1, `UsuarioDto` con `Nombres|Apellidos`, `PUT/DELETE/PATCH`, `[Authorize]` por acción. | ~200 backend | ~600 |
| 2 | `Integration/Usuarios/` + DI + nav `_Sidenav` (sin pages). | ~50 contract | ~250 |
| 3 | Razor Pages: `Index` segmentado, `Details`, `Delete`/`Reactivate` con PRG. | ~50 web | ~700 |
| 4 | Razor Pages: `Create` (dropdown Personas activas) + `Edit` (UserName/Email/roles) + auditoría tests. | ~30 web | ~500 |

Tracker PR (no-merge) mantiene el squash hasta decisión final.

## Riesgos

| Riesgo | Prob. | Mitigación |
|---|---|---|
| Migración toca `AspNetUsers` — riesgo de lock/contention. | Media | `ALGORITHM=INPLACE, LOCK=NONE` en columna generada; gate por `[MySqlFact]` antes del primer `dotnet ef database update` productivo. |
| Cambio en `UsuarioDto` es breaking en contrato. | Baja | Único consumidor actual: el propio Web. Si aparece otro caller, agregar campo nullable en lugar de reordenar. |
| Forecast total >800 líneas → excede budget. | Alta | Chained PRs propuesto (4 PRs, cada uno <800). `sdd-tasks` debe revalidar antes de `apply`. |
| Reactivación: la `Persona` asociada podría estar inactiva. | Media | Validar `Persona.IsDeleted == false` en `ReactivarAsync`; si no, `ErrorCategoria.Conflict` con `PersonaInactiva`. |
| N+1 residual si el helper de roles no se proyecta correctamente. | Baja | Test en `UsuarioIdentityGatewayQueryTests` que asserte `UserManager.GetRolesAsync` no se invoque dentro del bucle. |
| Dropdown de Create depende de `GET /api/v1/personas` completo. | Baja | Aceptable bajo 500 personas (decisión archive #120/#125); documentar follow-up si crece. |

## Plan de Rollback

1. **Migración**: borrar `AddSoftDeleteToAspNetUsers` o aplicar nueva migración `DropSoftDeleteToAspNetUsers` (`DROP COLUMN ... DROP INDEX ...`).
2. **Backend**: `git revert` PRs 1-4; el listado plano `GET /api/v1/usuarios` queda vigente.
3. **Web**: borrar `Pages/Seguridad/Usuarios/` y `Integration/Usuarios/`; revertir `Program.cs` y `_Sidenav.cshtml`.
4. **Datos**: usuarios existentes quedan intactos (default `0`); sin pérdida.

## Suposiciones y dependencias

- `GET /api/v1/personas` (lista plana activas) sirve como dropdown de Create; si supera el dataset operativo, queda como gap en change futuro (mismo follow-up que el typeahead de Personas).
- Módulo Personas archivado `2026-07-14-frontend-crud-personas` operativo en `main`.
- Soft-delete en `Persona` NO bloquea el alta: `UsuarioServicioComandos` valida existencia independientemente de `IsDeleted`. Solo bloquea la **reactivación** del Usuario.
- `MySqlFactAttribute` cubre la nueva columna: tests aplican `Migrate()` automáticamente.

## Criterios de éxito

- [ ] `dotnet build SGV.slnx` compila sin errores ni warnings nuevos.
- [ ] `dotnet test SGV.slnx` pasa (3 corridas consecutivas idénticas, sin `MSB4166`).
- [ ] `GET /api/v1/usuarios/consulta` responde `200` con `PagedResult<UsuarioDto>` que incluye `Nombres|Apellidos` y roles — `EXPLAIN` muestra una sola query (no N+1).
- [ ] Listado web `activas|eliminadas` alterna segmentos preservando `search|sort`; reactivación funciona vía PRG con banner y feedback.
- [ ] `dotnet ef migrations script --idempotent` produce DDL limpio contra MySQL 8 en CI; suite `[MySqlFact]` verde.

## Referencias

- `docs/decisiones-implementacion.md` — patrones de soft-delete (computed column) y default-deny API.
- `openspec/specs/identity-user-role-management/spec.md` — capability backend a modificar.
- `openspec/specs/persona-management/spec.md` — espejo funcional del módulo Usuarios (frontend).
- `openspec/changes/archive/2026-07-14-frontend-crud-personas/` — patrón de slicing y tracker.
- `openspec/changes/archive/2026-07-11-fix-active-puesto-id-unique-type/` — patrón de columna generada.
- `openspec/changes/archive/2026-07-13-taxonomia-errores-commandresult/` — regla de `ErrorCategoria` y CS8524 aceptado.
- `src/SGV.Web/Integration/Personas/PersonaApiClient.cs` y `src/SGV.Web/Pages/Personas/Index.cshtml.cs` — referencia 1:1 para `UsuarioApiClient`/`Usuarios/Index`.
- `src/SGV.Infraestructura/Persistencia/Configuraciones/PersonaConfiguracion.cs` — referencia para columna generada.
