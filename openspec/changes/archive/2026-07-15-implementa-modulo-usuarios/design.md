# Diseño técnico — Implementa módulo Usuarios

Change: `Implementa módulo usuarios` · Tracker: `feat/2026-07-15-implementa-modulo-usuarios-tracker`.

## 1. Contexto

[`proposal.md`](./proposal.md) · [`exploration.md`](./exploration.md) · 3 specs en `./specs/`. Precedentes: `archive/2026-07-14-frontend-crud-personas` (PRG), `archive/2026-07-11-fix-active-puesto-id-unique-type` (computed column), `archive/2026-07-13-taxonomia-errores-commandresult` (regla #125).

## 2. Decisiones de diseño (resuelven los 4 huecos del spec)

| # | Decisión | Elección | Alt. rechazada | Justificación |
|---|---|---|---|---|
| D-01 | Auto-baja prohibida | `403 Forbidden` + `ErrorCategoria.Forbidden` + code `AutoBaja` | `409 Conflict` | Prohibición a la identidad del caller (`IUsuarioActual.UserId == id`), no conflicto de estado. Precedente: `PersonasController`/`Create` retorna `403` sin admin. |
| D-02 | Persona inactiva en reactivación | `409 Conflict` + `ErrorCategoria.Conflict` + code `PersonaInactiva` | `403 Forbidden` | Conflicto entre dos entidades (Usuario reactivándose pero `Persona.IsDeleted=1`). Paralelo con `OcupacionesController.Patch`. |
| D-03 | Concurrencia | Last-write-wins sin `RowVersion` | `[Timestamp]`/`xmin` | Identity no expone RowVersion; Cargos (#101) y Personas son LWW. PUT devuelve DTO → cliente detecta diff. |
| D-04 | Shape del PUT | `PUT /usuarios/{id}` único y atómico (UserName+Email+Roles) | PUT credenciales + PUT roles separados | Atomicidad: si Email colisiona en 409 pero UserName+Roles ya validados, PUT dividido deja estado parcial. PUT único = transacción. `PUT /usuarios/{userId}/roles` se preserva como atajo. |

## 3. Modelo de datos y migración

### 3.1 DDL `AddSoftDeleteToAspNetUsers` (`ALGORITHM=INPLACE, LOCK=NONE`)

```sql
ALTER TABLE `AspNetUsers`
  ADD COLUMN `IsDeleted` TINYINT(1) NOT NULL DEFAULT 0,
  ADD COLUMN `ActiveUserNameUnique` VARCHAR(256)
    GENERATED ALWAYS AS (CASE WHEN `IsDeleted` = 0 THEN LOWER(`UserName`) ELSE NULL END) STORED
    COLLATE `utf8mb4_0900_ai_ci`,
  ADD UNIQUE INDEX `IX_AspNetUsers_ActiveUserNameUnique` (`ActiveUserNameUnique`);
```

`DEFAULT 0` deja usuarios preexistentes activos; `STORED` (índice único exige materialización); `LOWER(UserName)` matchea collation de `UserManager.NormalizeUserNameAsync`; FK `Persona` (`Restrict`) ya existe; `Down()` lanza `NotSupportedException` (forward-only). EF: `IsDeleted HasDefaultValue(false)` + columna generada + índice único.

## 4. APIs: contratos

| Verbo | Ruta | Auth | Éxito | Fallos |
|---|---|---|---|---|
| `GET` | `/usuarios/consulta?page&pageSize&search&sort&status` | autenticado | `200 PagedResult<UsuarioDto>` | `400` pageSize∉[1,100] |
| `GET` | `/usuarios/{id}` | autenticado | `200 UsuarioDto` | `404` |
| `POST` | `/usuarios` | Admin | `201 UsuarioDto`+`Location` | `400`/`404 PersonaNoEncontrada`/`409 PersonaYaTieneUsuario` |
| `PUT` | `/usuarios/{id}` | Admin | `200 UsuarioDto` | `400`/`409 UserName\|EmailDuplicado`/`404` |
| `DELETE` | `/usuarios/{id}` | Admin | `204` (soft) | `403 AutoBaja`/`404` |
| `PATCH` | `/usuarios/{id}/reactivar` | Admin | `200 UsuarioDto` | `409 PersonaInactiva`/`404` |
| `PUT` | `/usuarios/{userId}/roles` | Admin | `200 UsuarioDto` | preservado |
| `GET` | `/usuarios/roles` | Admin | `200 string[]` | preservado |

`PagedResult<T>` + `UsuarioListQuery(Page, PageSize, Search, Sort, Segmento)` + `SegmentoListado{Activas,Eliminadas}` en `SGV.Contracts.{Comun,Seguridad.Usuarios}`. `UsuarioDto` agrega `Nombres string?`/`Apellidos string?` **al final** (compat JSON); nulos si Persona inactiva/inexistente.

## 5. Capas Clean Architecture

| Endpoint | Handler Aplicación | Gateway Infraestructura |
|---|---|---|
| `POST /usuarios` | `UsuarioServicioComandos.CrearAsync` (existe) | `IUsuarioIdentityGateway.CrearAsync` |
| `GET /usuarios/consulta` | `IUsuarioServicioConsulta.QueryAsync` | `QueryAsync`: 1 query `userManager.Users.Include(Persona)` + `GroupJoin` roles + `ToDictionary` (test asserter: `GetRolesAsync` NO en bucle) |
| `GET /usuarios/{id}` | `ObtenerUsuarioHandler` | `FindByIdAsync`+`MapAsync` |
| `PUT /usuarios/{id}` | `ActualizarAsync` (catálogo roles + Email format) | `UpdateAsync`+`SetEmailAsync`+replace-roles atómico |
| `DELETE /usuarios/{id}` | `DesactivarAsync` (auto-baja vs `IUsuarioActual.UserId`) | `user.IsDeleted=true; SaveChangesAsync` |
| `PATCH /usuarios/{id}/reactivar` | `ReactivarAsync` (`PersonaRepository.GetByIdAsync`+`IsDeleted`) | `user.IsDeleted=false; SaveChangesAsync` |

`ApiResults.MapUsuarioStatus` extendido: `AutoBaja`, `PersonaInactiva`, `UserNameDuplicado`, `EmailDuplicado`, `PersonaYaTieneUsuario`, `PersonaRequerida`, `RolNoSoportado`.

Web: `Integration/Usuarios/{IUsuarioApiClient, UsuarioApiClient, UsuarioInputModel, UsuarioListItemViewModel, UsuarioPostResultMapper, UsuarioFormHelpers}` (timeout 10s + bearer + retry). Pages `Index/Create/Edit/Details`+`_Form.cshtml`; POST/PUT/DELETE/PATCH con `EsAdministrador` → `Forbid()` sin rol; PRG con `PageFeedback` (`TempData` con status|search|sort|p+LastDeletedId). `_Sidenav`: + ítem colapsable "Seguridad"→"Usuarios" (icono `ti ti-shield-lock`).

## 6. Auditoría

`AuditoriaSaveChangesInterceptor` no captura `SgvIdentityUser` (no extiende `AuditableEntityBase`). Solución: auditoría explícita vía `IAuditoriaServicio.RegistrarAsync(entidad, accion, usuarioOperadorId, diffCampos)` desde handlers de Aplicación (patrón Ocupaciones). Riesgo #7 a verificar en `tasks.md`.

## 7. Plan de pruebas (~82 tests RED→GREEN)

Unit aplicación ~12 · `[MySqlFact]` ~16 (config + gateway SinN1/ExcluyeEliminadas/SearchPorNombresApellidos/SortPorUserName_*) · API ~16 (GetConsulta/GetById/Create/Update/Delete_AutoBaja→403/Reactivate_PersonaInactiva→409) · Web ~24 (`Web/Usuario/{Index,Details,Create,Edit}PageTests` con SgvWebApplicationFactory+FakeUsuarioApiClient) · contratos + web seam ~14. NO perseguir 100%; priorizar auto-baja, `PersonaInactiva`, autorización, atomicidad PUT. Tests web sólo para `PageModels` con lógica.

## 8. PR slicing (4 chained PRs contra tracker)

| PR | Scope | Tests | Líneas | Risk |
|---|---|---|---|---|
| 1 | Backend: migración + wire-types + gateway/handlers + controller + `ApiResults` | ~36 | ~750 | Medium |
| 2 | Web Integration: `Integration/Usuarios/` + DI en `Program.cs` | ~14 | ~250 | Low |
| 3 | Web Pages: Index+Details+Delete+Reactivate con PRG+`PageFeedback` + ítem Sidenav | ~24 | ~700 | Medium |
| 4 | Web Pages: Create+Edit+`_Form.cshtml` compartido + tests auditoría | ~16 | ~500 | Low |
| **Total** | | **~90** | **~2200** | chain obligatorio |

`Decision needed before apply: Yes` (forecast PR1). `Chained PRs recommended: Yes`. `400-line budget risk: High single-PR; Low per-PR chain`.

## 9. Riesgos

1. Migración `AspNetUsers` lock → `ALGORITHM=INPLACE, LOCK=NONE` + gate `[MySqlFact]` antes del primer `dotnet ef database update` productivo.
2. `UsuarioDto` agrega campos (breaking JSON) → al final + nullable.
3. N+1 residual → test asserter `GetRolesAsync` NO en bucle.
4. Race Persona inactiva → transacción única; verificar `SELECT ... FOR UPDATE`.
5. Dropdown Personas → aceptable <500 personas (archive #120/#125).
6. LWW pierde cambios del segundo admin → PUT devuelve DTO; cliente detecta diff.
7. `AuditoriaSaveChangesInterceptor` no captura `IdentityUser` → `IAuditoriaServicio` explícito.
8. Sidenav "Seguridad" rompe active state → grupo aislado; test regresión.

## 10. Threat Matrix

`N/A — no routing, shell, subprocess, VCS/PR automation, executable-file classification, or process-integration boundary.`

## 11. Notas operativas

Sin cambios en secretos JWT ni cookie auth. MySQL 8 soporta `GENERATED ... STORED`. Rollback: borrar `Pages/Seguridad/Usuarios/`+`Integration/Usuarios/`, revertir `Program.cs`+`_Sidenav.cshtml`, borrar migración — cero impacto en datos. Próxima fase: `sdd-tasks` (confirmar forecast PR1).
