# R-03-04 — Roles y matriz de autorización

Referencia exhaustiva de la autorización vigente en `SGV.Api` y `SGV.Web`. El modelo de roles vive en `SGV.Contracts.Seguridad.RolesSgv` y se aplica vía atributos `[Authorize]` / `[Authorize(Roles = ...)]` sobre controllers y acciones.

## Roles disponibles

| Constante `RolesSgv` | Valor | Definido en |
| --- | --- | --- |
| `Administrador` | `Administrador` | `src/SGV.Contracts/Seguridad/RolesSgv.cs` |
| `GestorVacantes` | `GestorVacantes` | idem |
| `Consultor` | `Consultor` | idem |
| `RolesSgvMutacion` | `Administrador,GestorVacantes` | idem (utilizado por `[Authorize(Roles = ...)]` en `VacantesController`) |

La validación de nombres se hace con `RolesSgv.EsValido(string)` / `RolesSgv.TodosValidos(IEnumerable<string>)` (case-sensitive Ordinal).

## Defaults

| Comportamiento | Implementación |
| --- | --- |
| `FallbackPolicy` API | `RequireAuthenticatedUser()` (`src/SGV.Api/Program.cs` línea 225) |
| Auth scheme API | `JwtBearerDefaults.AuthenticationScheme` |
| Auth scheme Web | `CookieAuthenticationDefaults.AuthenticationScheme` |
| Bridge Web→API | `ApiBearerTokenHandler` (Transient) reenvía `Authorization: Bearer` |
| Revalidación API | `RevalidatorCredenciales` (Singleton) — chequea `LockoutEnd` y existencia del usuario |
| Revalidación Web | `CookiePrincipalRevalidator` (Scoped) invocado por `OnValidatePrincipal` |

## Convención de atributos

| Patrón | Aplicación |
| --- | --- |
| `[Authorize]` (sin roles) | Cualquier usuario autenticado (incluye roles básicos) |
| `[Authorize(Roles = RolesSgv.Administrador)]` | Sólo `Administrador` |
| `[Authorize(Roles = RolesSgv.RolesSgvMutacion)]` | `Administrador` o `GestorVacantes` |
| `[AllowAnonymous]` | Override del `FallbackPolicy` |

## Matriz por controller

### AuthController

| Endpoint | Authz | Roles |
| --- | --- | --- |
| `POST /api/v1/auth/login` | `AllowAnonymous` | — |
| `POST /api/v1/auth/refresh` | `AllowAnonymous` | — |
| `POST /api/v1/auth/logout` | `[Authorize]` | autenticado |
| `POST /api/v1/auth/change-password` | `[Authorize]` + rate-limit subject | autenticado |
| `POST /api/v1/auth/forgot-password` | `AllowAnonymous` + rate-limit IP | — |
| `POST /api/v1/auth/reset-password` | `AllowAnonymous` + rate-limit IP | — |
| `POST /api/v1/auth/validate-reset-token` | `AllowAnonymous` | — |

### AuditoriasController

| Endpoint | Authz |
| --- | --- |
| `GET /api/v1/auditorias` | `[Authorize(Roles = Administrador)]` |
| `GET /api/v1/auditorias/{id}` | idem |
| `GET /api/v1/auditorias/filter-options` | idem |

### CargosController

| Endpoint | Authz |
| --- | --- |
| `GET /api/v1/cargos` | `[Authorize]` |
| `GET /api/v1/cargos/consulta` | `[Authorize]` |
| `GET /api/v1/cargos/{id}` | `[Authorize]` |
| `POST /api/v1/cargos` | `[Authorize(Roles = Administrador)]` |
| `PUT /api/v1/cargos/{id}` | idem |
| `DELETE /api/v1/cargos/{id}` | idem |
| `PATCH /api/v1/cargos/{id}/reactivar` | idem |
| `GET /api/v1/cargos/{cargoId}/skills` | `[Authorize]` |
| `PUT /api/v1/cargos/{cargoId}/skills/{skillId}` | `[Authorize(Roles = Administrador)]` |
| `DELETE /api/v1/cargos/{cargoId}/skills/{skillId}` | idem |

### CategoriasHabilidadController

| Endpoint | Authz |
| --- | --- |
| `GET /api/v1/categorias-habilidad` | `[Authorize]` |
| `GET /api/v1/categorias-habilidad/{id}` | `[Authorize]` |

### EstadosVacanteController

| Endpoint | Authz |
| --- | --- |
| `GET /api/v1/estados-vacante` | `[Authorize]` |

### NivelesCargoController

| Endpoint | Authz |
| --- | --- |
| `GET /api/v1/niveles-cargo` | `[Authorize]` |
| `GET /api/v1/niveles-cargo/{id}` | `[Authorize]` |

### NivelesHabilidadController

| Endpoint | Authz |
| --- | --- |
| `GET /api/v1/niveles-habilidad` | `[Authorize]` |
| `GET /api/v1/niveles-habilidad/{id}` | `[Authorize]` |

### OcupacionesController

| Endpoint | Authz |
| --- | --- |
| `GET /api/v1/ocupaciones` | `[Authorize]` |
| `GET /api/v1/ocupaciones/{id}` | `[Authorize]` |
| `POST /api/v1/ocupaciones` | `[Authorize(Roles = Administrador)]` |
| `PUT /api/v1/ocupaciones/{id}` | idem |
| `PATCH /api/v1/ocupaciones/{id}/finalizar` | idem |
| `PATCH /api/v1/ocupaciones/{id}/reactivar` | idem |
| `DELETE /api/v1/ocupaciones/{id}` | idem |

### PersonasController

| Endpoint | Authz |
| --- | --- |
| `GET /api/v1/personas` | `[Authorize]` |
| `GET /api/v1/personas/consulta` | `[Authorize]` |
| `GET /api/v1/personas/buscar` | `[Authorize]` |
| `GET /api/v1/personas/{id}` | `[Authorize]` |
| `POST /api/v1/personas` | `[Authorize(Roles = Administrador)]` |
| `PUT /api/v1/personas/{id}` | idem |
| `DELETE /api/v1/personas/{id}` | idem |
| `PATCH /api/v1/personas/{id}/reactivar` | idem |
| `GET /api/v1/personas/{personaId}/skills` | `[Authorize]` |
| `PUT /api/v1/personas/{personaId}/skills/{skillId}` | `[Authorize(Roles = Administrador)]` |
| `DELETE /api/v1/personas/{personaId}/skills/{skillId}` | idem |

### PuestosController

| Endpoint | Authz |
| --- | --- |
| `GET /api/v1/puestos` | `[Authorize]` |
| `GET /api/v1/puestos/disponibles` | `[Authorize]` |
| `GET /api/v1/puestos/{id}` | `[Authorize]` |
| `POST /api/v1/puestos` | `[Authorize(Roles = Administrador)]` |
| `PUT /api/v1/puestos/{id}` | idem |
| `DELETE /api/v1/puestos/{id}` | idem |
| `GET /api/v1/puestos/consulta` | `[Authorize]` |
| `PATCH /api/v1/puestos/{id}/reactivar` | `[Authorize(Roles = Administrador)]` |

### SetupController

| Endpoint | Authz | Rate-limit |
| --- | --- | --- |
| `GET /api/v1/setup/status` | `AllowAnonymous` | — |
| `POST /api/v1/setup` | `AllowAnonymous` | `Setup` (5/15 min/IP) |

### SkillsController

| Endpoint | Authz |
| --- | --- |
| `GET /api/v1/skills` | `[Authorize]` |
| `GET /api/v1/skills/{id}` | `[Authorize]` |
| `GET /api/v1/skills/consulta` | `[Authorize]` |
| `GET /api/v1/skills/{skillId}/cargos` | `[Authorize]` |
| `GET /api/v1/skills/{skillId}/personas` | `[Authorize]` |
| `POST /api/v1/skills` | `[Authorize(Roles = Administrador)]` |
| `PUT /api/v1/skills/{id}` | idem |
| `DELETE /api/v1/skills/{id}` | idem |
| `PATCH /api/v1/skills/{id}/reactivar` | idem |

### TiposDocumentoController

| Endpoint | Authz |
| --- | --- |
| `GET /api/v1/tipos-documento` | `AllowAnonymous` (override `[Authorize]` del controller; requerido por el setup inicial) |
| `GET /api/v1/tipos-documento/{id}` | `[Authorize]` (heredado del controller) |

### TipoUnidadesOrganizativasController

| Endpoint | Authz |
| --- | --- |
| `GET /api/v1/tipos-unidad-organizativa` | `[Authorize]` |
| `GET /api/v1/tipos-unidad-organizativa/{id}` | `[Authorize]` |

### UnidadesOrganizativasController

| Endpoint | Authz |
| --- | --- |
| `GET /api/v1/unidades-organizativas` | `[Authorize]` |
| `GET /api/v1/unidades-organizativas/{id}` | `[Authorize]` |
| `POST /api/v1/unidades-organizativas` | `[Authorize(Roles = Administrador)]` |
| `PUT /api/v1/unidades-organizativas/{id}` | idem |
| `PATCH /api/v1/unidades-organizativas/{id}/unidad-padre` | idem |
| `GET /api/v1/unidades-organizativas/consulta` | `[Authorize]` |
| `GET /api/v1/unidades-organizativas/arbol` | `[Authorize]` |
| `DELETE /api/v1/unidades-organizativas/{id}` | `[Authorize(Roles = Administrador)]` |
| `PATCH /api/v1/unidades-organizativas/{id}/reactivar` | idem |
| `GET /api/v1/unidades-organizativas/diagnostico-jerarquia` | `[Authorize(Roles = Administrador)]` |

### UsuariosController

| Endpoint | Authz |
| --- | --- |
| `GET /api/v1/usuarios` | `[Authorize]` |
| `GET /api/v1/usuarios/consulta` | `[Authorize]` |
| `GET /api/v1/usuarios/{id}` | `[Authorize]` |
| `GET /api/v1/usuarios/roles` | `[Authorize(Roles = Administrador)]` |
| `POST /api/v1/usuarios` | idem |
| `PUT /api/v1/usuarios/{id}` | idem |
| `DELETE /api/v1/usuarios/{id}` | idem |
| `POST /api/v1/usuarios/{id}/bloquear` | idem |
| `POST /api/v1/usuarios/{id}/desbloquear` | idem |
| `PUT /api/v1/usuarios/{userId}/roles` | idem |

### VacantesController

| Endpoint | Authz |
| --- | --- |
| `GET /api/v1/vacantes` | `[Authorize]` |
| `GET /api/v1/vacantes/{id}` | `[Authorize]` |
| `POST /api/v1/vacantes` | `[Authorize(Roles = RolesSgvMutacion)]` |
| `PATCH /api/v1/vacantes/{id}/estado` | idem |

### Health checks

| Endpoint | Authz |
| --- | --- |
| `GET /health/live` | `AllowAnonymous` |
| `GET /health/ready` | `AllowAnonymous` |

## Matriz agregada por módulo

| Módulo | Lecturas (cualquier autenticado) | Mutaciones (Admin) | Mutaciones (Admin+GestorVacantes) | Anónimo |
| --- | --- | --- | --- | --- |
| Auth | `change-password`, `logout` | — | — | `login`, `refresh`, `forgot-password`, `reset-password`, `validate-reset-token` |
| Auditoría | — | listado + detalle + filter-options | — | — |
| Cargos | listado, consulta, byId, GET skills | create, update, delete, reactivate, upsert skill, delete skill | — | — |
| Categorías Habilidad | listado, byId | — | — | — |
| Estados Vacante | listado | — | — | — |
| Niveles Cargo | listado, byId | — | — | — |
| Niveles Habilidad | listado, byId | — | — | — |
| Ocupaciones | listado, byId | create, update, finalize, reactivate, delete | — | — |
| Personas | listado, consulta, buscar, byId, GET skills | create, update, delete, reactivate, upsert skill, delete skill | — | — |
| Puestos | listado, disponibles, byId, consulta | create, update, delete, reactivate | — | — |
| Setup | — | — | — | status, create (one-time) |
| Skills | listado, byId, consulta, GET cargos, GET personas | create, update, delete, reactivate | — | — |
| Tipos Documento | byId | — | — | listado (consumido por setup) |
| Tipos Unidad | listado, byId | — | — | — |
| Unidades Organizativas | listado, byId, consulta, árbol | create, update, change-parent, delete, reactivate, diagnostico | — | — |
| Usuarios | listado, consulta, byId | create, update, delete, bloquear, desbloquear, asignar roles, listado roles | — | — |
| Vacantes | listado, byId | — | create, cambiar estado | — |

## Comportamiento del revalidator (API)

`IRevalidatorCredenciales` se ejecuta en `JwtBearerEvents.OnTokenValidated` y como middleware propio (`app.Use(...)` después de `UseAuthentication`). El middleware defensivo:

1. Detecta si el principal tiene claim `iss` (JWT real).
2. Lee `NameIdentifier`/`sub`.
3. Si no hay subject, responde `401`.
4. Si el revalidator responde `SigueVigente = false` (usuario borrado o `LockoutEnd` futuro), responde `401`.

`SigueVigenteAsync(userId, ct)` consulta `SgvIdentityUser` por Id, evalúa `LockoutEnd <= UtcNow` y devuelve false si el usuario está bloqueado o eliminado.

## Comportamiento del revalidator (Web)

`OnValidatePrincipal` dispara `CookiePrincipalRevalidator.ValidateAsync(context)` en cada request autenticada. Si el JWT contra la API responde 401 (cuenta bloqueada o eliminada), el cookie auth ticket se invalida y se redirige a `/auth/sign-in`.

## Referencias

- How-to: [Bloquear y desbloquear usuario](../how-to/04-bloquear-desbloquear-usuario.md)
- How-to: [Rotar JWT signing key](../how-to/03-rotar-jwt-signing-key.md)
- How-to: [Operar flujo de recuperación de contraseña](../how-to/02-operar-flujo-recuperacion-contrasena.md)
- R-03-01 — Mapa de APIs HTTP (referencia cruzada de endpoints)
- R-03-10 — Taxonomía de errores (mapeo HTTP status por rol/escenario)
