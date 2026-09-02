# R-03-01 — Mapa de APIs HTTP

Referencia exhaustiva de los endpoints HTTP expuestos por `SGV.Api` (ASP.NET Core, .NET 10). La convención vigente es `api/v1/{recurso}` y la `FallbackPolicy` configurada en `Program.cs` exige `[Authorize]` por defecto; los pocos endpoints anónimos están marcados con `AllowAnonymous` y referenciados en la columna **Authz**.

## Convenciones globales

| Aspecto | Valor |
| --- | --- |
| Versión de ruta | `api/v1` (literal en todos los controllers) |
| Fallback policy | `RequireAuthenticatedUser()` (`src/SGV.Api/Program.cs` línea 225) |
| Formato de respuesta | `application/json` (atributo `[Produces("application/json")]` en cada controller) |
| Errores tipados | `ApiResults` (`ProblemDetails` / `ValidationProblemDetails` con `traceId`) |
| Errores anónimos (rate-limit) | Rate-limit policies en `Program.cs` (429 con header `Retry-After`) |
| OpenAPI / Swagger | `AddSwaggerGen` con security scheme `Bearer` (JWT) |
| Headers de respuesta | `traceId` automático en errores vía `ApplyTraceId` |

## Auth — `api/v1/auth`

Namespace `SGV.Contracts.Auth.AuthApiRoutes`. Controller `AuthController`.

| Método | Ruta | Authz | Códigos | Rate limit | Descripción |
| --- | --- | --- | --- | --- | --- |
| `POST` | `/api/v1/auth/login` | `AllowAnonymous` | 200, 401 | — | Emite par `access + refresh` contra `LoginRequest` (`Email`, `Password`). |
| `POST` | `/api/v1/auth/refresh` | `AllowAnonymous` | 200, 401, 429 | `Refresh` (20 req / 15 min, IP) | Rota refresh token; colapsa a 401 cualquier modo de falla. |
| `POST` | `/api/v1/auth/logout` | `[Authorize]` | 200, 401 | — | Revoca todos los refresh tokens del sujeto. |
| `POST` | `/api/v1/auth/change-password` | `[Authorize]` | 200, 400, 429 | `ChangePassword` (5 req / 15 min, subject o IP) | Cambia contraseña; rota `SecurityStamp`. |
| `POST` | `/api/v1/auth/forgot-password` | `AllowAnonymous` | 200, 400, 429 | `ForgotPassword` (3 req / 15 min, IP) | Anti-enumeración: colapsa `UserNotFound` a `200`. |
| `POST` | `/api/v1/auth/reset-password` | `AllowAnonymous` | 200, 400, 429 | `ResetPassword` (5 req / 15 min, IP) | Ejecuta el reset a partir del token enviado por email. |
| `POST` | `/api/v1/auth/validate-reset-token` | `AllowAnonymous` | 200, 400 | — | Validación lightweight del token; sin cambio de contraseña. |

## Auditorías — `api/v1/auditorias`

Controller `AuditoriasController`. Acceso total: `[Authorize(Roles = RolesSgv.Administrador)]`.

| Método | Ruta | Códigos | Descripción |
| --- | --- | --- | --- |
| `GET` | `/api/v1/auditorias` | 200, 400, 401, 403 | Listado paginado y filtrado (`entityName`, `operation`, `dateFrom`, `dateTo`, `userId`, `correlationId`, `sort`, `page`, `pageSize`). |
| `GET` | `/api/v1/auditorias/{id}` | 200, 401, 403, 404 | Detalle enriquecido (`EntityId`, `OldValuesJson`, `NewValuesJson`, `UserName`). |
| `GET` | `/api/v1/auditorias/filter-options` | 200, 401, 403 | Opciones para poblar `<select>` (DISTINCT `EntityName` y `Operation`, cap 100). |

## Cargos — `api/v1/cargos`

Controller `CargosController`. Lecturas: `[Authorize]`. Mutaciones: `[Authorize(Roles = Administrador)]`.

| Método | Ruta | Códigos | Descripción |
| --- | --- | --- | --- |
| `GET` | `/api/v1/cargos` | 200, 401 | Lista de cargos activos. |
| `GET` | `/api/v1/cargos/consulta` | 200, 401 | Paginado con `status=activas\|eliminadas` (whitelist sort: `codigo_asc/desc`, `nombre_asc/desc`, `nivel_asc/desc`). |
| `GET` | `/api/v1/cargos/{id}` | 200, 401, 404 | Cargo puntual. |
| `POST` | `/api/v1/cargos` | 201, 400, 401, 403, 409 | Crea cargo (`CrearCargoRequest`). |
| `PUT` | `/api/v1/cargos/{id}` | 200, 400, 401, 403, 404, 409 | Actualiza; `Codigo` obligatorio en el body. |
| `DELETE` | `/api/v1/cargos/{id}` | 204, 401, 403, 404, 409 | Soft-delete. |
| `PATCH` | `/api/v1/cargos/{id}/reactivar` | 200, 401, 403, 404, 409 | Reactivación. |
| `GET` | `/api/v1/cargos/{cargoId}/skills` | 200, 401 | Habilidades del cargo (`CargoSkillDetailDto`). |
| `PUT` | `/api/v1/cargos/{cargoId}/skills/{skillId}` | 200, 400, 401, 403, 404 | Upsert de habilidad con ponderación. |
| `DELETE` | `/api/v1/cargos/{cargoId}/skills/{skillId}` | 204, 401, 403, 404 | Quita habilidad. |

## Categorías de habilidad — `api/v1/categorias-habilidad`

Controller `CategoriasHabilidadController`. Read-only: `[Authorize]`.

| Método | Ruta | Códigos | Descripción |
| --- | --- | --- | --- |
| `GET` | `/api/v1/categorias-habilidad` | 200, 401 | Lista alfabética de las 4 filas seed. |
| `GET` | `/api/v1/categorias-habilidad/{id}` | 200, 400, 401, 404 | Categoría puntual. |

## Estados de vacante — `api/v1/estados-vacante`

Controller `EstadosVacanteController`. Read-only: `[Authorize]`. Ruta vía `VacanteApiRoutes.EstadosVacanteBase`.

| Método | Ruta | Códigos | Descripción |
| --- | --- | --- | --- |
| `GET` | `/api/v1/estados-vacante` | 200, 401 | 4 estados seed ordenados por `Orden` asc. |

## Niveles de cargo — `api/v1/niveles-cargo`

Controller `NivelesCargoController`. Read-only: `[Authorize]`.

| Método | Ruta | Códigos | Descripción |
| --- | --- | --- | --- |
| `GET` | `/api/v1/niveles-cargo` | 200, 401 | Lista del catálogo. |
| `GET` | `/api/v1/niveles-cargo/{id}` | 200, 400, 401, 404 | Nivel puntual. |

## Niveles de habilidad — `api/v1/niveles-habilidad`

Controller `NivelesHabilidadController`. Read-only: `[Authorize]`.

| Método | Ruta | Códigos | Descripción |
| --- | --- | --- | --- |
| `GET` | `/api/v1/niveles-habilidad` | 200, 401 | Lista del catálogo. |
| `GET` | `/api/v1/niveles-habilidad/{id}` | 200, 400, 401, 404 | Nivel puntual. |

## Ocupaciones — `api/v1/ocupaciones`

Controller `OcupacionesController`. Lecturas: `[Authorize]`. Mutaciones: `[Authorize(Roles = Administrador)]`. Ruta vía `OcupacionApiRoutes.Base`.

| Método | Ruta | Códigos | Descripción |
| --- | --- | --- | --- |
| `GET` | `/api/v1/ocupaciones` | 200, 401 | Paginado con `status=activas\|eliminadas`, `personaId`, `puestoId`, `sort` whitelist. |
| `GET` | `/api/v1/ocupaciones/{id}` | 200, 401, 404 | Ocupación puntual. |
| `POST` | `/api/v1/ocupaciones` | 201, 400, 401, 403, 404, 409 | Crea ocupación. |
| `PUT` | `/api/v1/ocupaciones/{id}` | 200, 400, 401, 403, 404, 409 | Actualiza campos editables. |
| `PATCH` | `/api/v1/ocupaciones/{id}/finalizar` | 200, 400, 401, 403, 404, 409 | Finaliza (setea `FechaFin`). |
| `PATCH` | `/api/v1/ocupaciones/{id}/reactivar` | 200, 401, 403, 404, 409 | Reactiva. |
| `DELETE` | `/api/v1/ocupaciones/{id}` | 204, 401, 403, 404, 409 | Soft-delete. |

## Personas — `api/v1/personas`

Controller `PersonasController`. Lecturas: `[Authorize]`. Mutaciones: `[Authorize(Roles = Administrador)]`.

| Método | Ruta | Códigos | Descripción |
| --- | --- | --- | --- |
| `GET` | `/api/v1/personas` | 200 | Lista completa de personas activas (sin paginar). |
| `GET` | `/api/v1/personas/consulta` | 200, 400, 401 | Paginado (`status`, `soloSinUsuario`, sort whitelist). |
| `GET` | `/api/v1/personas/buscar` | 200, 400, 401 | Typeahead (`q`, `take` ≤ 100, `soloSinUsuario`). |
| `GET` | `/api/v1/personas/{id}` | 200, 404 | Persona puntual. |
| `POST` | `/api/v1/personas` | 201, 400, 401, 403, 409 | Crea persona. |
| `PUT` | `/api/v1/personas/{id}` | 200, 400, 401, 403, 404, 409 | Actualiza. |
| `DELETE` | `/api/v1/personas/{id}` | 204, 401, 403, 404 | Soft-delete. |
| `PATCH` | `/api/v1/personas/{id}/reactivar` | 200, 401, 403, 404, 409 | Reactivación. |
| `GET` | `/api/v1/personas/{personaId}/skills` | 200 | Habilidades de la persona. |
| `PUT` | `/api/v1/personas/{personaId}/skills/{skillId}` | 200, 400, 401, 403, 404 | Upsert de habilidad. |
| `DELETE` | `/api/v1/personas/{personaId}/skills/{skillId}` | 204, 401, 403, 404 | Quita habilidad. |

## Puestos — `api/v1/puestos`

Controller `PuestosController`. Lecturas: `[Authorize]`. Mutaciones: `[Authorize(Roles = Administrador)]`.

| Método | Ruta | Códigos | Descripción |
| --- | --- | --- | --- |
| `GET` | `/api/v1/puestos` | 200, 401 | Lista de puestos activos. |
| `GET` | `/api/v1/puestos/disponibles` | 200, 401 | Puestos sin ocupación vigente ni vacante abierta. |
| `GET` | `/api/v1/puestos/{id}` | 200, 401, 404 | Puesto puntual. |
| `POST` | `/api/v1/puestos` | 201, 400, 401, 403, 409 | Crea puesto. |
| `PUT` | `/api/v1/puestos/{id}` | 200, 400, 401, 403, 404 | Actualiza. |
| `DELETE` | `/api/v1/puestos/{id}` | 204, 401, 403, 404, 409 | Soft-delete. |
| `GET` | `/api/v1/puestos/consulta` | 200, 401 | Paginado con `status=activas\|eliminadas`. |
| `PATCH` | `/api/v1/puestos/{id}/reactivar` | 200, 401, 403, 404, 409 | Reactivación. |

## Setup — `api/v1/setup`

Controller `SetupController`. Endpoints anónimos (issue #195, chicken-and-egg inicial). Ruta vía `SetupApiRoutes.Base`.

| Método | Ruta | Authz | Códigos | Rate limit | Descripción |
| --- | --- | --- | --- | --- | --- |
| `GET` | `/api/v1/setup/status` | `AllowAnonymous` | 200 | — | Devuelve `SetupStatusResponse` (`requiresSetup=true` si `AspNetUsers` está vacía). |
| `POST` | `/api/v1/setup` | `AllowAnonymous` | 200, 400, 409, 429, 500 | `Setup` (5 req / 15 min, IP) | Crea Persona + Usuario + rol `Administrador` atómicamente. |

## Skills (habilidades) — `api/v1/skills`

Controller `SkillsController`. Lecturas: `[Authorize]`. Mutaciones: `[Authorize(Roles = Administrador)]`.

| Método | Ruta | Códigos | Descripción |
| --- | --- | --- | --- |
| `GET` | `/api/v1/skills` | 200, 401 | Lista de habilidades activas. |
| `GET` | `/api/v1/skills/{id}` | 200, 401, 404 | Habilidad puntual. |
| `GET` | `/api/v1/skills/consulta` | 200, 401 | Paginado (`status`, sort whitelist). |
| `GET` | `/api/v1/skills/{skillId}/cargos` | 200, 401, 404 | Cargos que requieren esta habilidad. |
| `GET` | `/api/v1/skills/{skillId}/personas` | 200, 401, 404 | Personas con esta habilidad. |
| `POST` | `/api/v1/skills` | 201, 400, 401, 403, 409 | Crea habilidad. |
| `PUT` | `/api/v1/skills/{id}` | 200, 400, 401, 403, 404, 409 | Actualiza (`Codigo` obligatorio). |
| `DELETE` | `/api/v1/skills/{id}` | 204, 401, 403, 404 | Soft-delete. |
| `PATCH` | `/api/v1/skills/{id}/reactivar` | 200, 401, 403, 404, 409 | Reactivación. |

## Tipos de documento — `api/v1/tipos-documento`

Controller `TiposDocumentoController`. Catálogo read-only. `GetAll` es `AllowAnonymous` para soportar el setup inicial; `GetById` mantiene `[Authorize]`.

| Método | Ruta | Authz | Códigos | Descripción |
| --- | --- | --- | --- | --- |
| `GET` | `/api/v1/tipos-documento` | `AllowAnonymous` | 200 | 4 filas seed (DNI, LE, LC, Pasaporte). |
| `GET` | `/api/v1/tipos-documento/{id}` | `[Authorize]` | 200, 400, 401, 404 | Tipo de documento puntual. |

## Tipos de unidad organizativa — `api/v1/tipos-unidad-organizativa`

Controller `TipoUnidadesOrganizativasController`. Read-only: `[Authorize]`. Sin `[ProducesResponseType]` decorando el método (sólo `[ProducesResponseType(StatusCodes.Status401Unauthorized)]`).

| Método | Ruta | Códigos | Descripción |
| --- | --- | --- | --- |
| `GET` | `/api/v1/tipos-unidad-organizativa` | 401 | Lista del catálogo. |
| `GET` | `/api/v1/tipos-unidad-organizativa/{id}` | 401 | Tipo puntual. |

## Unidades organizativas — `api/v1/unidades-organizativas`

Controller `UnidadesOrganizativasController`. Lecturas: `[Authorize]`. Mutaciones: `[Authorize(Roles = Administrador)]`.

| Método | Ruta | Códigos | Descripción |
| --- | --- | --- | --- |
| `GET` | `/api/v1/unidades-organizativas` | 200, 400, 401 | Topes a `MaxGetAllItems = 100`; usar `/consulta` para universos grandes. |
| `GET` | `/api/v1/unidades-organizativas/{id}` | 200, 401, 404 | Unidad puntual. |
| `POST` | `/api/v1/unidades-organizativas` | 201, 400, 401, 403, 409 | Crea unidad. |
| `PUT` | `/api/v1/unidades-organizativas/{id}` | 200, 400, 401, 403, 404, 409 | Actualiza. |
| `PATCH` | `/api/v1/unidades-organizativas/{id}/unidad-padre` | 200, 400, 401, 403, 404, 409 | Cambia padre jerárquico. |
| `GET` | `/api/v1/unidades-organizativas/consulta` | 200, 401 | Paginado (`status`, `tipoUnidadOrganizativaId`, `unidadPadreId`, `vigenteEn`, sort whitelist). |
| `GET` | `/api/v1/unidades-organizativas/arbol` | 200, 401 | Árbol parcial con `nodosConCiloDetectado`. |
| `DELETE` | `/api/v1/unidades-organizativas/{id}` | 204, 401, 403, 404, 409 | Soft-delete. |
| `PATCH` | `/api/v1/unidades-organizativas/{id}/reactivar` | 200, 401, 403, 404, 409 | Reactivación. |
| `GET` | `/api/v1/unidades-organizativas/diagnostico-jerarquia` | 200, 401, 403 | Admin-only: ciclos detectados en la jerarquía activa. |

## Usuarios — `api/v1/usuarios`

Controller `UsuariosController`. Lecturas: `[Authorize]`. Mutaciones y `roles`: `[Authorize(Roles = Administrador)]`.

| Método | Ruta | Códigos | Descripción |
| --- | --- | --- | --- |
| `GET` | `/api/v1/usuarios` | 200, 401 | Lista completa. |
| `GET` | `/api/v1/usuarios/consulta` | 200, 401 | Paginado (`status=activas\|bloqueadas`, `sort`, `search`). |
| `GET` | `/api/v1/usuarios/{id}` | 200, 401, 404 | Usuario puntual. |
| `GET` | `/api/v1/usuarios/roles` | 200, 401, 403 | Roles seed del sistema. |
| `POST` | `/api/v1/usuarios` | 201, 400, 401, 403, 404, 409 | Crea usuario. |
| `PUT` | `/api/v1/usuarios/{id}` | 200, 400, 401, 403, 404, 409 | Actualiza. |
| `DELETE` | `/api/v1/usuarios/{id}` | 204, 401, 403, 404 | Borrado físico (admin). |
| `POST` | `/api/v1/usuarios/{id}/bloquear` | 200, 400, 401, 403, 404 | Lockout administrativo. |
| `POST` | `/api/v1/usuarios/{id}/desbloquear` | 200, 400, 401, 403, 404 | Desbloqueo. |
| `PUT` | `/api/v1/usuarios/{userId}/roles` | 200, 400, 401, 403, 404 | Asigna roles (`AsignarRolesRequest`). |

## Vacantes — `api/v1/vacantes`

Controller `VacantesController`. Lecturas: `[Authorize]`. Mutaciones: `[Authorize(Roles = RolesSgvMutacion)]` (Administrador + GestorVacantes). Ruta vía `VacanteApiRoutes.Base`.

| Método | Ruta | Códigos | Descripción |
| --- | --- | --- | --- |
| `GET` | `/api/v1/vacantes` | 200, 401 | Paginado (`status=abiertas\|cerradas\|todas`, `puestoId`, `search`, `sort` whitelist). |
| `GET` | `/api/v1/vacantes/{id}` | 200, 401, 404 | Vacante + `HistorialEstadoVacante` cronológico. |
| `POST` | `/api/v1/vacantes` | 201, 400, 401, 403, 404, 409 | Abre vacante. |
| `PATCH` | `/api/v1/vacantes/{id}/estado` | 200, 400, 401, 403, 404, 409 | Transición de estado + historial atómico. |

## Wire-types principales por módulo

| Módulo | Wire-type principal | Otros records |
| --- | --- | --- |
| Auth | `LoginRequest`, `LoginResponse`, `RefreshRequest`, `RefreshResponse`, `LogoutResponse`, `ForgotPasswordRequest`, `ResetPasswordRequest`, `ValidateResetTokenRequest`, `ChangePasswordRequest` | — |
| Auditoría | `AuditoriaDto`, `AuditoriaDetalleDto`, `AuditoriaFilterOptions`, `AuditoriaListQuery` | — |
| Cargos | `CargoDto`, `CargoSkillDto`, `CargoSkillDetailDto`, `CargoSegmentoListado`, `CargoListQuery`, `CrearCargoRequest`, `ActualizarCargoRequest`, `AsignarCargoSkillRequest` | — |
| Habilidades | `HabilidadDto`, `CategoriaHabilidadDto`, `NivelHabilidadDto`, `HabilidadListQuery`, `HabilidadCargosListQuery`, `HabilidadPersonasListQuery`, `CrearHabilidadRequest`, `ActualizarHabilidadRequest`, `SkillCargoDetailDto`, `PersonaHabilidadesPageResult` | — |
| Ocupaciones | `OcupacionDto`, `CrearOcupacionRequest`, `ActualizarOcupacionRequest`, `FinalizarOcupacionRequest`, `OcupacionListQuery`, `OcupacionSegmentoListado` | — |
| Organización | `UnidadOrganizativaDto`, `UnidadOrganizativaArbolResponse`, `CicloDetectado`, `PuestoDto`, `NivelCargoDto`, `TipoUnidadOrganizativaDto`, `UnidadOrganizativaQuery`, `PuestoListQuery` | `CrearUnidadOrganizativaRequest`, `ActualizarUnidadOrganizativaRequest`, `CambiarUnidadPadreRequest`, `CrearPuestoRequest`, `ActualizarPuestoRequest` |
| Personas | `PersonaDto`, `PersonaListadoDto`, `PersonaSkillDto`, `PersonaSkillDetailDto`, `PersonaListQuery`, `CrearPersonaRequest`, `ActualizarPersonaRequest`, `AsignarPersonaSkillRequest`, `TipoDocumentoDto`, `PersonaSegmentoListado` | — |
| Seguridad | `JwtOptions`, `RefreshTokenOptions`, `RolesSgv` | — |
| Setup | `SetupRequest`, `SetupCommandResult`, `SetupStatusResponse`, `SetupErrorCode` | — |
| Vacantes | `VacanteDto`, `VacanteDetailDto`, `EstadoVacanteDto`, `CrearVacanteRequest`, `CambiarEstadoVacanteRequest`, `VacanteListQuery`, `VacanteSegmentoListado` | — |
| Usuarios | `UsuarioDto`, `UsuarioListadoDto`, `UsuarioListQuery`, `UsuarioSegmentoListado`, `CrearUsuarioRequest`, `ActualizarUsuarioRequest`, `AsignarRolesRequest` | — |

## Referencias

- Tutorial: [Levantar el sistema local](../tutorials/01-levantar-sistema-local.md)
- Tutorial: [Primera mutación de unidad organizativa](../tutorials/02-primera-mutacion-unidad-organizativa.md)
- How-to: [Forzar setup inicial](../how-to/10-forzar-setup-inicial.md)
- How-to: [Auditar quién modificó entidad](../how-to/08-auditar-quien-modifico-entidad.md)
