# Tasks — Implementa módulo usuarios

## Review Workload Forecast

| Field | Value |
|-------|-------|
| Estimated changed lines | ~2.200 (PR1: 750, PR2: 250, PR3: 700, PR4: 500) |
| 800-line budget risk | High single-PR; Low per-PR chain |
| Chained PRs recommended | Yes |
| Suggested split | PR1 Backend → PR2 Integration → PR3 Listado/Delete/Reactivate → PR4 Create/Edit |
| Delivery strategy | ask-always |
| Chain strategy | feature-branch-chain (tentativo — confirmar) |

Decision needed before apply: Yes
Chained PRs recommended: Yes
Chain strategy: feature-branch-chain
400-line budget risk: High

### Suggested Work Units

| Unit | Goal | Likely PR | Focused test command | Runtime harness | Rollback boundary |
|------|------|-----------|----------------------|-----------------|-------------------|
| 1 | Backend: migración + gateway/handlers + controller | PR 1 | `dotnet test --filter "Api.Usuarios\|Persistencia.Usuarios\|Aplicacion.Usuarios"` | N/A — CI-only (MySqlFact requiere MySQL) | Revertir migración + borrar endpoints nuevos |
| 2 | Web Integration: clientes tipados + DI | PR 2 | `dotnet test --filter "Web.Usuario.*ApiClient\|Contract.*Usuario"` | N/A — fake client tests | Borrar `Integration/Usuarios/` + revertir `Program.cs` |
| 3 | Pages: Index + Details + Delete + Reactivate | PR 3 | `dotnet test --filter "Web.Usuario.*Page"` | N/A — WebApplicationFactory con fake | Borrar `Pages/Seguridad/Usuarios/{Index,Details}.cshtml*` |
| 4 | Pages: Create + Edit + _Form | PR 4 | `dotnet test --filter "Web.Usuario.*(Create\|Edit)"` | N/A — WebApplicationFactory con fake | Borrar `Pages/Seguridad/Usuarios/{Create,Edit,_Form}.cshtml*` |

## Tracker

`feat/2026-07-15-implementa-modulo-usuarios-tracker` (tentativo — confirmar fecha y nombre)

## Estrategia de PRs

**Feature Branch Chain** (tentativa): PR#1 → tracker; PR#2 → PR#1 branch; PR#3 → PR#2 branch; PR#4 → PR#3 branch. Solo el tracker mergea a `main`. Cada child PR debe retargetearse si muestra diff de PRs previos.

## PR1 — Backend (Migración + Gateway + Handlers + Controller + ApiResults)

Objetivo: base de datos con soft-delete, gateway sin N+1, handlers de aplicación para consulta/actualización/baja/reactivación, y endpoints API completos.

- [x] **1.1 [RED]** Tests de dominio para auto-baja (D-01) y reactivación con Persona inactiva (D-02) — verificar que `403 Forbidden`/`409 Conflict` se mapean correctamente
- [x] **1.2** Migración EF: agregar `IsDeleted` + columna generada `ActiveUserNameUnique` + índice único a `AspNetUsers` vía `AddSoftDeleteToAspNetUsers` → `src/SGV.Infraestructura/Persistencia/Migraciones/` + `SgvIdentityUser.cs` (+ `IsDeleted`) + `SgvIdentityUserConfiguracion.cs`
  - **Dependency**: task 1.5
- [x] **1.3** Generar script SQL idempotente: `dotnet ef migrations script --idempotent --output docs/migracion-add-softdelete-usuarios.sql`
  - **Dependency**: 1.2
- [x] **1.4** Agregar `Nombres`/`Apellidos` (string?) a `UsuarioDto` en `src/SGV.Contracts/Seguridad/Usuarios/UsuarioContracts.cs` (al final, nullable)
- [x] **1.5** Agregar `UsuarioListQuery(Page,PageSize,Search,Sort,SegmentoListado)` + `SegmentoListado{Activas,Eliminadas}` + `UsuarioListadoDto(PagedResult<UsuarioDto>)` en `src/SGV.Contracts/Seguridad/Usuarios/`
- [x] **1.6** Extender `IUsuarioServicioConsulta` con `QueryAsync(UsuarioListQuery)` en `src/SGV.Aplicacion/Seguridad/Usuarios/UsuarioContracts.cs`
- [x] **1.7** Implementar `QueryAsync` en `UsuarioIdentityGateway` (`src/SGV.Infraestructura/Seguridad/UsuarioIdentityGateway.cs`) — proyección con `Include(Persona)` + `GroupJoin` roles + `ToDictionary` (sin N+1)
- [x] **1.8** Agregar métodos al gateway y handlers aplicación:
  - `ActualizarAsync`: `PUT` atómico UserName+Email+Roles
  - `DesactivarAsync`: `IsDeleted=true` + auto-baja guard D-01
  - `ReactivarAsync`: `IsDeleted=false` + validar Persona activa D-02
  - En: `IUsuarioIdentityGateway` + `IUsuarioServicioComandos` + implementaciones
- [x] **1.9** Extender `UsuarioServicioComandos` con `ActualizarAsync`/`DesactivarAsync`/`ReactivarAsync` en `src/SGV.Aplicacion/Seguridad/Usuarios/UsuarioServicioComandos.cs`
  - **Dependency**: 1.8
- [x] **1.10** [RED] Tests unitarios aplicación ~12 tests — cubren: validación campos, auto-baja, PersonaInactiva, atomicidad PUT, LWW
- [x] **1.11** [RED] Tests `[MySqlFact]` ~10 tests — cubren: QueryAsync sin N+1, search por Nombres/Apellidos, sort por UserName, exclusión eliminadas, reactivación
- [x] **1.12** Extender `UsuariosController`: `GetConsulta`, `Put/{id}`, `Delete/{id}` (soft), `Patch/{id}/reactivar` + `[Authorize(Roles=Administrador)]` por acción en `src/SGV.Api/Controllers/UsuariosController.cs`
  - **Dependency**: 1.6, 1.7, 1.9
- [x] **1.13** Extender `ApiResults.MapUsuarioStatus` y `ErrorCategoriaMappers.ToTipoUsuario` con nuevos códigos: `AutoBaja`, `PersonaInactiva`, `UserNameDuplicado`, `EmailDuplicado`, `PersonaYaTieneUsuario`
  - **Dependency**: 1.12
- [x] **1.14** [RED] Tests API ~16 tests — cubren: GetConsulta paginado, GetById, Create, Update, Delete_AutoBaja→403, Reactivate_PersonaInactiva→409, autorización admin
  - **Dependency**: 1.12
- [x] **1.15** Validar PR1: `dotnet build SGV.slnx` + `dotnet test --filter "Api.Usuarios|Persistencia.Usuarios|Aplicacion.Usuarios"`

## PR2 — Web Integration (Clientes tipados + DI + navegación)

Objetivo: conectar `SGV.Web` con `SGV.Api` a través de `IUsuarioApiClient`/`UsuarioApiClient` y registro DI.

- [ ] **2.1** [RED] Contract tests interface `IUsuarioApiClient` en `tests/SGV.Tests/Web/Usuario/` (análogo a `IPersonaApiClientContractTests`)
- [ ] **2.2** Crear `Integration/Usuarios/` completo:
  - `IUsuarioApiClient.cs` — GetAllActivasAsync, QueryAsync, GetByIdAsync, CreateAsync, UpdateAsync, DesactivarAsync, ReactivarAsync
  - `UsuarioApiClient.cs` — timeout 10s, bearer forwarding, retry en fallos de transporte
  - `UsuarioInputModel.cs` — modelo bindeable para Create/Edit
  - `UsuarioListItemViewModel.cs` — view model para Index
  - `UsuarioPostResultMapper.cs` — mapea `UsuarioCommandResult` → `PostResult`
  - `UsuarioFormHelpers.cs` — helper de field errors
  - **Dependency**: PR1 (contratos backend)
- [ ] **2.3** [RED] Tests del cliente tipado ~8 tests — casos felices + 1 fallo recuperable + contrato
  - **Dependency**: 2.2
- [ ] **2.4** [RED] Tests de integración con fake ~6 tests — `FakeUsuarioApiClient` verifica cada operación
  - **Dependency**: 2.2
- [ ] **2.5** Registrar `AddHttpClient<IUsuarioApiClient, UsuarioApiClient>` con `ApiBearerTokenHandler` en `src/SGV.Web/Program.cs`
  - **Dependency**: 2.2
- [ ] **2.6** Agregar ítem "Seguridad" → "Usuarios" en `src/SGV.Web/Pages/Shared/Partials/_Sidenav.cshtml` (icono `ti ti-shield-lock`, grupo colapsable)
- [ ] **2.7** Validar PR2: `dotnet build SGV.slnx` + `dotnet test --filter "Web.Usuario.*(ApiClient|Contract)"`

## PR3 — Pages Index + Details + Delete + Reactivate

Objetivo: listado segmentado `activas|eliminadas`, detalle readonly, baja lógica y reactivación con PRG + `PageFeedback`.

- [ ] **3.1** Crear `Pages/Seguridad/Usuarios/Index.cshtml` + `Index.cshtml.cs`:
  - Toggle `activas|eliminadas` preservando `search`/`sort`/`p`
  - Tabla: UserName, Email, Nombres, Apellidos, roles
  - `EsAdministrador` gating: ocultar Editar/Eliminar si no admin
  - PRG en Delete/Reactivate con `TempData` + `PageFeedback`
  - **Dependency**: PR2 (cliente tipado)
- [ ] **3.2** Crear `Pages/Seguridad/Usuarios/Details.cshtml` + `Details.cshtml.cs`:
  - Readonly con retorno al listado preservando `p/search/sort/status`
  - `404` → estado recuperable
  - **Dependency**: PR2
- [ ] **3.3** Implementar handlers POST Delete (`?handler=Delete`) y Reactivate (`?handler=Reactivate`) en `Index.cshtml.cs`:
  - Delete → `DELELE /api/v1/usuarios/{id}` + `PageFeedback.SetLastDeletedId`
  - Reactivate → `PATCH /api/v1/usuarios/{id}/reactivar` + validar `PersonaInactiva` → `ErrorCategoria.Conflict`
  - `403 AutoBaja` → feedback accionable
  - `Forbid()` si no admin
  - **Dependency**: 3.1
- [ ] **3.4** [RED] Tests web Index ~14 tests — PRG Delete/Reactivate, toggle segmentos, auto-baja, PersonaInactiva, role gating, paginación, búsqueda
  - **Dependency**: 3.1, 3.3
- [ ] **3.5** [RED] Tests web Details ~4 tests — carga readonly, 404 recuperable, retorno con filtros
  - **Dependency**: 3.2
- [ ] **3.6** Validar PR3: `dotnet build SGV.slnx` + `dotnet test --filter "Web.Usuario.*(Index|Details)"` + `bun run build` en `src/SGV.Web`

## PR4 — Pages Create + Edit + _Form

Objetivo: alta de usuario con dropdown de Personas activas + edición de UserName/Email/roles + formulario compartido.

- [ ] **4.1** Crear `_Form.cshtml` partial compartido con:
  - Dropdown Personas activas (solo en Create, readonly en Edit)
  - UserName, Email, Password (solo Create)
  - Roles checkboxes (catálogo fijo `Administrador`, `GestorVacantes`, `Consultor`)
  - **Dependency**: PR2
- [ ] **4.2** Crear `Create.cshtml` + `Create.cshtml.cs`:
  - `OnGetAsync`: cargar catálogo Personas activas (`GET /api/v1/personas`)
  - `OnPostAsync`: validar + `POST /api/v1/usuarios` → PRG a Details con feedback success
  - `400`/`409` → mapear a field errors
  - `[Authorize(Roles=Administrador)]` gating + `Forbid()` si no admin
  - Dropdown vacío → mensaje guía con link a `/personas/crear`
  - **Dependency**: 4.1
- [ ] **4.3** Crear `Edit.cshtml` + `Edit.cshtml.cs`:
  - `OnGetAsync`: cargar `UsuarioDto` por id + prellenar campos (Persona readonly)
  - `OnPostAsync`: `PUT /api/v1/usuarios/{id}` atómico → PRG al propio editor con feedback success
  - `400`/`409` → field errors preservando resto del form
  - `404` → estado recuperable
  - **Dependency**: 4.1
- [ ] **4.4** [RED] Tests web Create ~8 tests — dropdown poblado/vacío, validación, 201→PRG Details, 409→field error, Forbid sin admin
  - **Dependency**: 4.2
- [ ] **4.5** [RED] Tests web Edit ~8 tests — prellenado, PUT exitoso→PRG, duplicado UserName→field error, 404 recuperable
  - **Dependency**: 4.3
- [ ] **4.6** Validar PR4: `dotnet build SGV.slnx` + `dotnet test --filter "Web.Usuario.*(Create|Edit)"` + `bun run build`

## Riesgos operativos de la división

| Riesgo | Mitigación |
|--------|------------|
| PR1 migración locks `AspNetUsers` en prod | `ALGORITHM=INPLACE, LOCK=NONE` en DDL; gate `[MySqlFact]` antes del `database update` productivo |
| PR2+3+4 dependen de PR1 mergeado | Si PR1 se traba, todo el chain queda bloqueado. Priorizar revisión PR1 |
| Child PR muestra diff de PRs previos | Retargetear/rebasear contra el parent correcto hasta que diff sea limpio |
| Auditoría `SaveChangesInterceptor` no captura `SgvIdentityUser` | Auditoría explícita vía `IAuditoriaServicio.RegistrarAsync` desde handlers (patrón Ocupaciones) |
| Sidebar "Seguridad" rompe active state de otro módulo | Grupo aislado; test de regresión visual en PR3 |
