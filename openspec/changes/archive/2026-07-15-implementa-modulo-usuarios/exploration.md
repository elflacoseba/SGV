# Exploración: Implementar Módulo Usuarios

## Contexto

El sistema SGV actualmente gestiona usuarios a través de ASP.NET Core Identity con `SgvIdentityUser` (que extiende `IdentityUser` agregando `PersonaId`). La API expone endpoints básicos de administración de usuarios, pero **no existe un frontend web** que permita listar, crear, editar o gestionar usuarios desde la shell de `SGV.Web`.

Este módulo es necesario para completar la funcionalidad administrativa del sistema: actualmente Personas, Cargos, Habilidades, Unidades Organizativas, Puestos y Ocupaciones tienen su módulo web completo, pero Usuarios solo existe como API.

---

## 1. Estado Actual por Capa

### 1.1 `SGV.Contracts` — Wire-types

Existen contratos completos en `src/SGV.Contracts/Seguridad/Usuarios/`:

- **`CrearUsuarioRequest`** — `(Guid PersonaId, string UserName, string Email, string Password, IReadOnlyCollection<string> Roles)`
- **`AsignarRolesRequest`** — `(IReadOnlyCollection<string> Roles)`
- **`LoginRequest` / `LoginResponse`** — autenticación
- **`UsuarioDto`** — `(string Id, Guid PersonaId, string UserName, string Email, IReadOnlyCollection<string> Roles)`
- **`UsuarioErrorType`** — `NotFound | Conflict | Validation | Unauthorized`
- **`UsuarioError`** — error tipado con `Code`, `Message`, `Categoria`
- **`UsuarioCommandResult`** — resultado discriminado `Success`/`Failure`

También en `src/SGV.Contracts/Seguridad/`:
- **`RolesSgv`** — catálogo fijo: `Administrador`, `GestorVacantes`, `Consultor`
- **`JwtOptions`** — configuración JWT

Y en `src/SGV.Contracts/Auth/`:
- **`AuthApiRoutes`** — rutas centralizadas (`api/v1/auth/login`)

**Gap detectado:** `UsuarioDto` no incluye `Nombres`/`Apellidos` de la `Persona` vinculada. Para el listado web se necesitaría agregar esos campos o resolver la proyección desde `IUsuarioServicioConsulta`.

### 1.2 `SGV.Dominio` — No hay entidad Usuario

No existe una entidad `Usuario` en el dominio. El modelo Identity (`SgvIdentityUser`) vive en `SGV.Infraestructura`, lo cual es coherente con Clean Architecture: Identity es un detalle de persistencia. La capa de dominio conoce a `Persona` (entidad) pero no a los usuarios del sistema.

**Decisión consciente:** El módulo Usuarios opera indirectamente sobre Identity. No se espera crear una entidad `Usuario` en dominio — la abstracción ya está mediada por `IUsuarioIdentityGateway` en Aplicación.

### 1.3 `SGV.Aplicacion` — Servicios y puertos

En `src/SGV.Aplicacion/Seguridad/Usuarios/`:

- **`IUsuarioIdentityGateway`** — port para Identity (crear usuario, asignar roles)
- **`IUsuarioServicioComandos`** — aplicación: valida persona existe + roles válidos, luego delega en gateway
- **`IUsuarioServicioConsulta`** — query: `ListAsync()` devuelve `IReadOnlyList<UsuarioDto>` (plano, sin paginación)
- **`IRolServicioConsulta`** — catálogo fijo de roles
- **`IAuthServicio`** — login (también en Infraestructura)
- **`IUsuarioActual`** — contexto del usuario autenticado (UserId, PersonaId, Roles, CorrelationId)

Implementaciones:
- **`UsuarioServicioComandos`** — valida `PersonaId`, `UserName`, `Email`, `Password`, roles válidos, consulta `PersonaRepository`; luego delega en `IUsuarioIdentityGateway`
- **`RolServicioConsulta`** — retorna `RolesSgv.Todos`

**Gaps detectados:**
- `IUsuarioServicioConsulta.ListAsync()` retorna lista plana sin paginación, búsqueda ni segmentación (a diferencia del patrón `QueryAsync` de los otros módulos)
- No existe endpoint de reactivación/desactivación lógica de usuarios (Identity usa borrado físico con `DeleteAsync`)
- No existe actualización de datos del IdentityUser (UserName, Email)
- No existe bloqueo/desbloqueo de cuenta (`LockoutEnabled`/`LockoutEnd`)

### 1.4 `SGV.Infraestructura` — Identity gateway

En `src/SGV.Infraestructura/Seguridad/`:

- **`SgvIdentityUser`** — `class : IdentityUser { Guid PersonaId }`
- **`UsuarioIdentityGateway`** — implementa `IUsuarioIdentityGateway` + `IUsuarioServicioConsulta`
  - `CrearAsync`: crea usuario y asigna roles via `UserManager`
  - `AsignarRolesAsync`: reemplaza todos los roles del usuario
  - `ListAsync`: lista todos los usuarios via `UserManager.Users`
- **`AuthServicio`** — login: busca por username o email, verifica password, genera JWT con claims (sub, persona_id, nombres, apellidos, roles)

En `src/SGV.Infraestructura/Persistencia/Configuraciones/`:
- **`SgvIdentityUserConfiguracion`** — FK `PersonaId` → `PersonaEntity` (1:1, Restrict)

**Gap detectado:** `ListAsync` itera con N+1: pide todos los usuarios con `ToListAsync` y luego por cada uno llama a `GetRolesAsync`. Para el listado paginado web, esto necesita optimizarse (proyectar con Include/Select de UserRoles + Roles).

### 1.5 `SGV.Api` — Controladores

En `src/SGV.Api/Controllers/`:

- **`UsuariosController`** (`[Route("api/v1/usuarios")]`)
  - `[Authorize(Roles = RolesSgv.Administrador)]` a nivel de clase
  - `GET /` — `GetAll()`: lista plana (sin paginación)
  - `GET /roles` — `GetRoles()`: catálogo fijo
  - `POST /` — `Create(CrearUsuarioRequest)`: crea usuario con persona vinculada
  - `PUT /{userId}/roles` — `AssignRoles(string userId, AsignarRolesRequest)`: reemplaza roles
  - **No hay**: `GET /consulta?page=&search=&sort=&status=`, `PUT /{id}`, `DELETE /{id}`, `PATCH /{id}/reactivar`

- **`AuthController`** (`[Route("api/v1/auth")]`)
  - `POST /login` — login público (AllowAnonymous), delega en `IAuthServicio`

### 1.6 `SGV.Web` — Frontend

**No existe ningún frontend web para usuarios.**

- No existe `Pages/Seguridad/Usuarios/` ni `Pages/Usuarios/`
- No existe `Integration/Usuarios/` clientes tipados
- El único cliente de integración existente es `AuthApiClient` (`LoginAsync`)

Contraste con los módulos existentes de Organización, que tienen:
- `Pages/Organizacion/{Subdominio}/Index.cshtml` + `Create.cshtml` + `Edit.cshtml` + `Details.cshtml` + `_Form.cshtml`
- `Integration/{Subdominio}/{Subdominio}ApiClient.cs` y su interfaz

### 1.7 Tests

- **`tests/SGV.Tests/Api/UsuariosControllerTests.cs`** — 3 tests de autorización (Forbidden sin admin, Unauthorized sin creds, roles)
- **`tests/SGV.Tests/Aplicacion/Seguridad/UsuarioServicioComandosTests.cs`** — tests del servicio de comandos
- **No hay** tests web de integración para usuarios
- **No hay** tests de `UsuarioIdentityGateway`
- **No hay** tests de consulta/paginación

---

## 2. Patrones Vigentes (Módulos Organizacion, Personas, Habilidades)

### 2.1 Estructura de módulo completa

Cada subdominio consolidado sigue esta estructura:

```
SGV.Contracts/{Subdominio}/
├── Comandos/        → CrearXRequest, ActualizarXRequest, XCommandResult, XError
├── Consultas/Dtos/  → XDto, XListQuery, XSegmentoListado (enum), PagedResult<T>

SGV.Aplicacion/{Subdominio}/
├── Comandos/        → IServicioComandos, ServicioComandos, Validaciones/
├── Consultas/       → IServicioConsulta, ServicioConsulta, I{Subdominio}Repository

SGV.Infraestructura/Persistencia/
├── Repositorios/    → {Subdominio}Repository : I{Subdominio}Repository
├── Configuraciones/ → {Subdominio}Configuracion (IEntityTypeConfiguration)
├── Mapeos/          → DomainToPersistenceMapper

SGV.Api/Controllers/{Subdominio}Controller.cs

SGV.Web/Pages/Organizacion/{Subdominio}/
├── Index.cshtml / .cshtml.cs
├── Create.cshtml / .cshtml.cs
├── Edit.cshtml / .cshtml.cs
├── Details.cshtml / .cshtml.cs
├── _Form.cshtml (partial)

SGV.Web/Integration/{Subdominio}/
├── I{Subdominio}ApiClient.cs
├── {Subdominio}ApiClient.cs
```

### 2.2 Patrón de listado segmentado

| Elemento | Patrón |
|---|---|
| Query param | `?status=activas|eliminadas` |
| Endpoint API | `GET /api/v1/{recurso}/consulta?page=&pageSize=&search=&sort=&status=` |
| Backend enum | `{Recurso}SegmentoListado` (`Activas=0`, `Eliminadas=1`) |
| Web toggle | btn-group `Activas` / `Eliminadas` con route values |
| Paginación | `PagedResult<T>(Items, TotalCount, Page, PageSize)` |
| PRG feedback | `TempData` via `PageFeedback` (success/warning/danger) |
| Reactivación | `PATCH /api/v1/{recurso}/{id}/reactivar` + banner con undo |
| Baja lógica | `DELETE /api/v1/{recurso}/{id}` (soft-delete, no físico) |

### 2.3 Patrón de autorización web

- `[Authorize]` en PageModel (clase)
- Controles admin: `if (!EsAdministrador) return Forbid();` en handlers POST
- `EsAdministrador => User.IsInRole(RolesSgv.Administrador)` para UI gating
- `IAuthSessionRedirector` para sesiones expiradas
- `TransportFailureClassifier.IsTransportFailure(ex)` para errores de transporte
- CAF (Consistent Async Failure): los handlers capturan excepciones de transporte y muestran página recuperable

### 2.4 Patrón de creación web

- `Create.cshtml.cs` tiene un `InputModel` con `[BindProperty]`
- `OnGetAsync` carga catálogos necesarios (personas activas para dropdown, roles)
- `OnPostAsync` llama al cliente tipado, redirige a Index en éxito, muestra errores en fallo
- PRG: `RedirectToPage` tras éxito, `Page()` en fallo de validación

---

## 3. Gaps Detectados

### 3.1 Funcionales

| Gap | Detalle | Prioridad |
|---|---|---|
| **Sin frontend web** | No hay páginas Razor para listar, crear, editar, ver detalle de usuarios | **Alta** |
| **Sin paginación** | `ListAsync()` retorna todo sin paginar (N+1 en roles) | **Alta** |
| **Sin baja/reactivación lógica** | Identity usa borrado físico; no hay soft-delete en `SgvIdentityUser` | **Media** |
| **Sin actualización de usuario** | No hay endpoint `PUT /api/v1/usuarios/{id}` para editar UserName/Email | **Alta** |
| **Sin bloqueo de cuenta** | No hay endpoint para lockout/unlock desde la UI | **Baja** |
| **Integration client** | No existe `IUsuarioApiClient` en `SGV.Web` | **Alta** |
| **Datos de Persona en DTO** | `UsuarioDto` no incluye nombres/apellidos de la persona vinculada | **Media** |
| **Sin edición de contraseña** | No hay funcionalidad de cambio de contraseña desde admin | **Media** |

### 3.2 Técnicos

| Gap | Detalle |
|---|---|
| **N+1 en listado** | `UsuarioIdentityGateway.ListAsync` itera usuarios y por cada uno llama `GetRolesAsync` |
| **Segmentación** | No existe `UsuarioSegmentoListado` ni filtro activas/eliminadas |
| **Query endpoint** | No existe `GET /api/v1/usuarios/consulta?page=&search=&sort=&status=` |
| **Cobertura de tests** | Faltan tests de integración web, del gateway y del endpoint de consulta |

### 3.3 Usuarios no sigue el patrón de soft-delete

Los módulos existentes (Cargos, Habilidades, Personas, UnidadesOrganizativas, Ocupaciones) usan `IsDeleted`/`IsActive` con columnas generadas para unicidad en activos. Identity no ofrece esto naturalmente.

Para el módulo Usuarios hay que decidir:
- **Opción A**: Usar `LockoutEnabled/LockoutEnd` como "baja lógica" (no requiere migración de esquema)
- **Opción B**: Agregar columna `IsDeleted`/`IsActive` a `AspNetUsers` (requiere migración + extender Identity)
- **Opción C**: No implementar segmentación activas/eliminadas y solo permitir crear/asignar roles

El patrón existente favorece la Opción B, pero implica modificar el esquema de Identity.

---

## 4. Decisiones Vigentes que Aplican

Extraídas de `docs/decisiones-implementacion.md`:

| Decisión | Impacto |
|---|---|
| **Default-deny API** | Endpoints nuevos deben tener `[Authorize]` explícito |
| **Roles fijos SGV** | No hay CRUD de roles; solo `Administrador`, `GestorVacantes`, `Consultor` |
| **Administrador para writes** | Toda mutación de usuarios requiere rol Administrador |
| **Identity con string key** | `SgvIdentityUser` hereda `IdentityUser<string>` → IDs son varchar(255) |
| **MySQL provider único** | No asumir SQL Server; columnas generadas para unicidad con soft-delete |
| **Auditoría central** | Tabla `Auditorias` vía interceptor EF Core (excluye datos sensibles) |
| **JWT bearer en API** | `SGV.Api` valida JWT; `SGV.Web` usa cookie+JWT bridge via `ApiBearerTokenHandler` |
| **Fail-loud en startup** | JWT SigningKey y ConnectionString se validan al arranque |
| **Vínculo 1:1 Identity ↔ Persona** | `PersonaId` es FK obligatoria con DeleteBehavior.Restrict |
| **InternalsVisibleTo** | `SGV.Dominio` → `SGV.Tests` + `SGV.Infraestructura` |
| **Reconstitute pattern** | Las 6 entidades principales usan factory `internal static Reconstitute` |
| **Catálogo vs listado** | Separar catálogos completos (dropdown) de listados paginados (consulta) |

---

## 5. Riesgos y Dependencias

### Dependencias

| Dependencia | Tipo | Detalle |
|---|---|---|
| **Módulo Personas** | Fuerte | Crear usuario requiere `PersonaId` existente. El dropdown de creación debe poder listar personas activas |
| **Identity schema** | Fuerte | Cualquier modificación (soft-delete, nuevo campo) requiere migración EF Core y pruebas `[MySqlFact]` |
| **Auth bridge Web→API** | Medio | El cliente tipado de usuarios en Web debe usar `ApiBearerTokenHandler` para JWT forwarding |
| **RolesSgv** | Débil | Catálogo fijo; si se necesita CRUD de roles, es otro cambio aparte |

### Riesgos

| Riesgo | Probabilidad | Impacto | Mitigación |
|---|---|---|---|
| **N+1 en listado** | Alta | Degradación con >100 usuarios | Optimizar en el gateway con proyección `Include`/`Select` |
| **Soft-delete Identity** | Media | Rompe convenciones de Identity | Evaluar Opción A (Lockout) vs Opción B (IsDeleted) temprano |
| **Cambio en `UsuarioDto`** | Media | Afecta consumidores API existentes | Agregar campos opcionales o crear nueva versión del DTO |
| **Omisión de paginación** | Baja | UI web funcional pero sin consistencia con otros módulos | Incluir paginación desde el diseño inicial |
| **Pruebas de integración con Identity** | Media | Identity tiene dependencias de DB real | Usar `[MySqlFact]` y fixtures existentes |

---

## 6. Preguntas Abiertas

1. **¿Soft-delete en usuarios?** ¿Se implementa baja lógica con columna `IsDeleted` (Opción B, requiere migración) o se usa `LockoutEnabled` (Opción A, sin migración)?
2. **¿Paginación del endpoint?** ¿Se agrega `GET /api/v1/usuarios/consulta` paginado o se modifica el `GET /api/v1/usuarios` existente?
3. **¿Nombres/apellidos en DTO?** ¿Se agregan `Nombres`/`Apellidos` a `UsuarioDto` o se deja que la UI haga una llamada adicional al módulo Personas?
4. **¿Edición de contraseña?** ¿Se incluye cambio de contraseña desde la UI de administración?
5. **¿Ubicación en el menú?** ¿Dónde se coloca la navegación a Usuarios en la shell web? (¿Seguridad? ¿Configuración?)
6. **¿Alcance del módulo?** ¿Solo CRUD básico de usuarios (crear, editar roles, listar) o también gestión de sesiones, bloqueo, historial de login?

---

## 7. Recomendación

Se recomienda un enfoque por **slices**:

1. **Slice 1 — Backend de consulta paginada**: Agregar `GET /api/v1/usuarios/consulta` con paginación, búsqueda y sort (replicando patrón `CargosController.GetConsulta`). Optimizar N+1 en `UsuarioIdentityGateway`. Decidir modelo de baja lógica.
2. **Slice 2 — Integration client + Web Index**: Crear `IUsuarioApiClient`/`UsuarioApiClient` en `SGV.Web/Integration/Usuarios/`. Implementar página `Index` con listado paginado segmentado, toggle activas/eliminadas y PRG.
3. **Slice 3 — Web Create/Edit/Details**: Páginas de creación (con dropdown de personas activas), edición (UserName, Email, roles) y detalle.
4. **Slice 4 — Web baja/reactivación**: Baja lógica (o bloqueo) y reactivación con banner undo, consistente con el patrón de Cargos/Personas.
