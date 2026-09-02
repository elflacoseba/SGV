# R-03-03 — Wire-types (SGV.Contracts)

Catálogo exhaustivo de los tipos compartidos entre `SGV.Api` y `SGV.Web` que residen en `src/SGV.Contracts/`. El paquete es leaf transversal: lo linkean tanto `SGV.Aplicacion` (lógica) como `SGV.Api` (composition root), y `SGV.Web` (clientes tipados y BFF).

> Los record/DTO viven como `public sealed record ...` (C# 14, `nullable enabled`). Los tipos de dominio tipados son `public sealed class ...` cuando no encajan en un record (ej. `ErrorCategoria`).

## Auth — `SGV.Contracts.Auth` + `SGV.Contracts.Seguridad`

### Constantes de ruta

| Constante | Valor |
| --- | --- |
| `AuthApiRoutes.Base` | `api/v1/auth` |
| `AuthApiRoutes.Login` / `LoginRelative` | `/api/v1/auth/login` |
| `AuthApiRoutes.Refresh` / `RefreshRelative` | `/api/v1/auth/refresh` |
| `AuthApiRoutes.Logout` / `LogoutRelative` | `/api/v1/auth/logout` |
| `AuthApiRoutes.ChangePassword` / `ChangePasswordRelative` | `/api/v1/auth/change-password` |
| `AuthApiRoutes.ForgotPassword` / `ForgotPasswordRelative` | `/api/v1/auth/forgot-password` |
| `AuthApiRoutes.ResetPassword` / `ResetPasswordRelative` | `/api/v1/auth/reset-password` |
| `AuthApiRoutes.ValidateResetToken` / `ValidateResetTokenRelative` | `/api/v1/auth/validate-reset-token` |
| `AuthApiRoutes.RefreshPolicyName` | `Refresh` (rate-limit) |
| `AuthApiRoutes.ChangePasswordPolicyName` | `ChangePassword` |
| `AuthApiRoutes.ForgotPasswordPolicyName` | `ForgotPassword` |
| `AuthApiRoutes.ResetPasswordPolicyName` | `ResetPassword` |

### Records (`Seguridad/Usuarios/UsuarioContracts.cs` + `Auth/LogoutContracts.cs` + `Seguridad/Usuarios/Refresh*.cs`)

| Tipo | Propósito | Notas |
| --- | --- | --- |
| `LoginRequest(string UserNameOrEmail, string Password)` | Body de `POST /login` | — |
| `LoginResponse(string AccessToken, DateTime ExpiresAt, string RefreshToken, DateTime RefreshTokenExpiresAt)` | Par emitido en login exitoso | — |
| `RefreshRequest(string RefreshToken)` | Body de `POST /refresh` | — |
| `RefreshResponse(string AccessToken, DateTime ExpiresAt, string RefreshToken, DateTime RefreshTokenExpiresAt)` | Par rotado | — |
| `LogoutRequest(string? RefreshToken = null)` | Body opcional de `POST /logout` | — |
| `LogoutResponse(bool Success)` | Resultado de logout | — |
| `ChangePasswordRequest(string CurrentPassword, string NewPassword)` | Body de `POST /change-password` | — |
| `ForgotPasswordRequest(string UserNameOrEmail)` | Body de `POST /forgot-password` | — |
| `ResetPasswordRequest(string UserId, string Token, string NewPassword)` | Body de `POST /reset-password` | — |
| `ValidateResetTokenRequest(string UserId, string Token)` | Body de `POST /validate-reset-token` | — |

### Clases de opciones y políticas

| Tipo | Sección `appsettings` | Notas |
| --- | --- | --- |
| `JwtOptions` | `Jwt` | `Issuer` ("SGV"), `Audience` ("SGV"), `SigningKey` (≥32 bytes UTF-8, obligatorio), `TokenLifetimeMinutes` (default 60) |
| `RefreshTokenOptions` | `RefreshToken` | `RefreshTokenLifetimeDays` (14), `RateLimitPermitLimit` (20), `RateLimitWindowMinutes` (15) |
| `PasswordPolicy` | (constantes) | `MinLength=6`, `RequireDigit/Lowercase/Uppercase/NonAlphanumeric` todos `true`. Métodos: `IsCompliant(password)` |
| `RolesSgv` | (constantes) | `Administrador`, `GestorVacantes`, `Consultor`; `RolesSgvMutacion="Administrador,GestorVacantes"` |
| `JwtTokenValidationParameters` | (helpers) | Validación estándar JWT |

### Errors

| Tipo | Variantes | Notas |
| --- | --- | --- |
| `UsuarioError` | (record) | `Type` (`UsuarioErrorType`), `Message`, `Categoria`, `StatusCode?`, `Code` |
| `UsuarioErrorType` | enum legacy | `NotFound`, `Conflict`, `Validation`, `Unauthorized` |
| `UsuarioCommandResult` | (record) | Resultado de comandos de usuario |

## Auditoria — `SGV.Contracts.Auditoria`

| Tipo | Propósito | Notas |
| --- | --- | --- |
| `AuditoriaDto` | Proyección segura (sin `Old/NewValuesJson`) | Para listado |
| `AuditoriaDetalleDto` | Detalle enriquecido con `EntityId`, `OldValuesJson`, `NewValuesJson`, `UserName` | Para `GET /{id}` |
| `AuditoriaFilterOptions` | Listas DISTINCT de `EntityName` y `Operation` | Para poblar `<select>` |
| `AuditoriaListQuery` | Query string de `GET /auditorias` | Incluye `EntityName`, `Operation`, `DateFrom`, `DateTo`, `UserId`, `CorrelationId`, `Sort`, `Page`, `PageSize` |

## Habilidades — `SGV.Contracts.Habilidades`

### Comandos (`Habilidades/Comandos/`)

| Tipo | Propósito |
| --- | --- |
| `CrearHabilidadRequest(string Codigo, string Nombre, string? Descripcion, Guid CategoriaId)` | Body de `POST /skills` |
| `ActualizarHabilidadRequest(string Codigo, string Nombre, string? Descripcion, Guid CategoriaId)` | Body de `PUT /skills/{id}`; `Codigo` obligatorio |
| `AsignarCargoSkillRequest` (en `Organizacion/Comandos`) | Body de `PUT /cargos/{cargoId}/skills/{skillId}` |
| `AsignarPersonaSkillRequest` (en `Personas/Comandos`) | Body de `PUT /personas/{personaId}/skills/{skillId}` |
| `HabilidadError(Type, Message, Categoria?, StatusCode?, Code)` | Error tipado |
| `HabilidadErrorType` (legacy) | `NotFound`, `Conflict`, `Validation`, `Infrastructure` |
| `HabilidadCommandResult` | Envelope de retorno |

### Consultas (`Habilidades/Consultas/Dtos/`)

| Tipo | Propósito |
| --- | --- |
| `HabilidadDto` | Proyección completa (`Id`, `Codigo`, `Nombre`, `Descripcion`, `CategoriaId`, `CategoriaNombre`, `NivelId?`, `NivelNombre?`) |
| `NivelHabilidadDto` | Proyección de nivel |
| `HabilidadListQuery` | Query string de `GET /skills/consulta` |
| `HabilidadCargosListQuery` | Query string de `GET /skills/{id}/cargos` |
| `HabilidadPersonasListQuery` | Query string de `GET /skills/{id}/personas` |
| `SkillCargoDetailDto` | Fila de cargos asociados a una habilidad |
| `SkillPersonaDetailDto` | Fina de personas con una habilidad |
| `PersonaHabilidadesPageResult` | Envelope paginado de personas para el subrecurso `skills/{id}/personas` |
| `CategoriaHabilidadDto` | Proyección del catálogo `CategoriasHabilidad` |

### Categorías (`Habilidades/Categorias/Consultas/`)

| Tipo | Propósito |
| --- | --- |
| `CategoriaHabilidadDto` | Proyección del catálogo |

## Ocupaciones — `SGV.Contracts.Ocupaciones`

### Constantes

| Constante | Valor |
| --- | --- |
| `OcupacionApiRoutes.Base` | `api/v1/ocupaciones` |
| `OcupacionApiRoutes.ById` | `/{id:guid}` |
| `OcupacionApiRoutes.Finalize` | `/{id:guid}/finalizar` |
| `OcupacionApiRoutes.Reactivate` | `/{id:guid}/reactivar` |
| Query constants | `StatusQuery`, `PersonaIdQuery`, `PuestoIdQuery`, `PageQuery`, `PageSizeQuery`, `SearchQuery`, `SortQuery` |
| Sort whitelist | `SortFechaInicioAsc="fechainicio_asc"`, `SortPersonaAsc/Desc`, `SortPuestoAsc/Desc` |

### Records

| Tipo | Propósito |
| --- | --- |
| `CrearOcupacionRequest(Guid PersonaId, Guid PuestoId, DateOnly FechaInicio, Guid? VacanteId, string? Observaciones)` | Body de `POST /ocupaciones` |
| `ActualizarOcupacionRequest(DateOnly FechaInicio, string? Observaciones)` | Body de `PUT /ocupaciones/{id}` |
| `FinalizarOcupacionRequest(DateOnly FechaFin, string? Observaciones)` | Body de `PATCH /ocupaciones/{id}/finalizar` |
| `OcupacionListQuery(int Page, int PageSize, string? Search, string? Sort, OcupacionSegmentoListado Segmento, Guid? PersonaId, Guid? PuestoId)` | Query de `GET /ocupaciones` |
| `OcupacionDto(Id, PersonaId, PersonaDescripcion, PuestoId, PuestoDescripcion, VacanteId?, FechaInicio, FechaFin?, TipoAsignacion, Observaciones?, IsActive)` | Proyección |
| `OcupacionCommandResult` | Envelope |
| `OcupacionError(Type, Code, Message, Categoria, StatusCode?)` | Error tipado |
| `OcupacionErrorCodigo` | `PuestoOcupado`, `PersonaOcupada`, `PuestoInactivo`, `PersonaInactiva`, `OcupacionNoEncontrada`, `PersonaNoEncontrada`, `PuestoNoEncontrado`, `VacanteNoEncontrada`, `OcupacionYaFinalizada`, `OcupacionYaActiva`, `DatosInvalidos` |

### Enums

| Enum | Variantes |
| --- | --- |
| `OcupacionTipoAsignacion` | `Titular`, `Interino`, `Suplente` |
| `OcupacionSegmentoListado` | `Activas`, `Eliminadas` |
| `OcupacionEstado` | (proyección para UI) |

## Organización — `SGV.Contracts.Organizacion`

### Comandos (`Organizacion/Comandos/`)

| Tipo | Propósito |
| --- | --- |
| `CrearCargoRequest(string Codigo, string Nombre, string? Descripcion, Guid NivelId)` | Body de `POST /cargos` |
| `ActualizarCargoRequest(string Codigo, string Nombre, string? Descripcion, Guid NivelId)` | Body de `PUT /cargos/{id}` |
| `AsignarCargoSkillRequest(Guid NivelRequeridoId, decimal Ponderacion)` | Body de `PUT /cargos/{cargoId}/skills/{skillId}` |
| `CargoError(Type, Code, Message, Categoria, StatusCode?)` | Error tipado |
| `CargoErrorType` | `NotFound`, `Conflict`, `Validation`, `Unauthorized`, `Forbidden`, `Transport`, `Unexpected` |
| `CargoCommandResult` | Envelope de cargo |
| `CargoSkillError(Type, Code, Message, Categoria, StatusCode?)` | Error de subrecurso cargo-skill |
| `CargoSkillErrorType` | `NotFound`, `Validation`, `Conflict`, `Unauthorized`, `Forbidden`, `Transport` |
| `CargoSkillCommandResult` | Envelope de cargo-skill |
| `CargoSkillDeleteResult` | Resultado de borrado físico |
| `CrearPuestoRequest(string Codigo, string Nombre, string? Descripcion, Guid UnidadOrganizativaId, Guid CargoId, Guid? PuestoSuperiorId)` | Body de `POST /puestos` |
| `ActualizarPuestoRequest(string Nombre, string? Descripcion, Guid CargoId, Guid? PuestoSuperiorId)` | Body de `PUT /puestos/{id}`; `Codigo` inmutable |
| `PuestoError` / `PuestoErrorType` / `PuestoCommandResult` | Análogos a cargo |
| `CrearUnidadOrganizativaRequest(string Codigo, string Nombre, Guid TipoUnidadOrganizativaId, Guid? UnidadPadreId, string? Descripcion, DateOnly? VigenteDesde, DateOnly? VigenteHasta)` | Body de `POST /unidades-organizativas` |
| `ActualizarUnidadOrganizativaRequest(...)` | Body de `PUT /unidades-organizativas/{id}` |
| `CambiarUnidadPadreRequest(Guid? UnidadPadreId)` | Body de `PATCH /unidades-organizativas/{id}/unidad-padre` |
| `UnidadOrganizativaError` / `UnidadOrganizativaErrorType` / `UnidadOrganizativaCommandResult` | Análogos |
| `UnidadOrganizativaErrorCodigos` | `CodigoDuplicado`, `NombreDuplicado`, `CicloJerarquico`, `ConHijosActivos`, `ConRecursosAsociados`, `PadreInexistente`, `UnidadInexistente`, `PadreInactivo`, `DatosInvalidos`, `JerarquiaInvalida` |

### Consultas (`Organizacion/Consultas/Dtos/`)

| Tipo | Propósito |
| --- | --- |
| `PagedResult<T>(Items, TotalCount, Page, PageSize)` | Wrapper genérico |
| `CargoDto` | Proyección de cargo |
| `CargoListQuery(int Page, int PageSize, string? Search, string? Sort, CargoSegmentoListado Segmento)` | Query de `GET /cargos/consulta` |
| `CargoSkillDto(Id, NivelRequeridoId, NivelRequeridoNombre, Ponderacion)` | Proyección de cargo-skill |
| `CargoSkillDetailDto(...)` | Variante con detalle |
| `NivelCargoDto` | Proyección de nivel |
| `PuestoDto` | Proyección de puesto |
| `PuestoListQuery` | Query de `GET /puestos/consulta` |
| `TipoUnidadOrganizativaDto` | Proyección |
| `UnidadOrganizativaDto` | Proyección plana |
| `UnidadOrganizativaTreeNodeDto` | Nodo para el árbol |
| `UnidadOrganizativaArbolResponse` | `{Arbol, NodosConCiloDetectado}` |
| `UnidadOrganizativaQuery(int Page, int PageSize, string? Search, string? Sort, Guid? TipoUnidadOrganizativaId, Guid? UnidadPadreId, DateOnly? VigenteEn, UnidadOrganizativaSegmentoListado Segmento)` | Query de `GET /unidades-organizativas/consulta` |
| `CicloDetectado(List<Guid> Nodos)` | Fila devuelta por el diagnóstico |

### Enums (`Organizacion/Consultas/...`)

| Enum | Variantes |
| --- | --- |
| `CargoSegmentoListado` | `Activas`, `Eliminadas` |
| `PuestoSegmentoListado` | `Activas`, `Eliminadas` |
| `UnidadOrganizativaSegmentoListado` | `Activas`, `Eliminadas` |

## Personas — `SGV.Contracts.Personas`

### Comandos (`Personas/Comandos/`)

| Tipo | Propósito |
| --- | --- |
| `CrearPersonaRequest(string? Legajo, string Nombres, string Apellidos, string? Email, Guid? TipoDocumentoId, string? NumeroDocumento, string? Telefono)` | Body de `POST /personas` |
| `ActualizarPersonaRequest(string? Legajo, string Nombres, string Apellidos, string? Email, Guid? TipoDocumentoId, string? NumeroDocumento, string? Telefono)` | Body de `PUT /personas/{id}` |
| `AsignarPersonaSkillRequest(Guid NivelHabilidadId)` | Body de `PUT /personas/{personaId}/skills/{skillId}` |
| `PersonaError(Type, Code, Message)` | Error tipado |
| `PersonaErrorType` | `NotFound`, `Conflict`, `Validation`, `Unauthorized`, `Forbidden`, `Transport`, `Unexpected` |
| `PersonaCommandResult` | Envelope |
| `PersonaDeleteResult` | Resultado de soft-delete |
| `PersonaSkillError(Type, Code, Message, Categoria, StatusCode?)` | Error de subrecurso |
| `PersonaSkillCommandResult` | Envelope |
| `PersonaSkillDeleteResult` | Resultado de borrado físico |

### Consultas (`Personas/Consultas/Dtos/`)

| Tipo | Propósito |
| --- | --- |
| `PersonaDto(Id, Legajo?, Nombres, Apellidos, Email?, TipoDocumentoId?, TipoDocumentoNombre?, NumeroDocumento?, Telefono?, IsActive)` | Proyección completa |
| `PersonaListadoDto` | Wrapper de paginado de personas |
| `PersonaListQuery(int Page, int PageSize, string? Search, string? Sort, PersonaSegmentoListado Segmento, bool? SoloSinUsuario)` | Query de `GET /personas/consulta` |
| `PersonaSkillDto` | Proyección de subrecurso |
| `PersonaSkillDetailDto` | Variante con detalle |
| `TipoDocumentoDto` | Proyección del catálogo |
| `PersonaSegmentoListado` (enum) | `Activas`, `Eliminadas` |

## Setup — `SGV.Contracts.Setup`

### Constantes y records

| Tipo | Propósito |
| --- | --- |
| `SetupApiRoutes.Base` | `api/v1/setup` |
| `SetupApiRoutes.StatusRelative` | `status` |
| `SetupApiRoutes.SetupPolicyName` | `Setup` (rate-limit) |
| `SetupRequest(...)` | Body de `POST /setup` (creación inicial) |
| `SetupResult(Guid PersonaId, string UserId, string UserName)` | Datos devueltos tras setup exitoso |
| `SetupStatusResponse(bool RequiresSetup)` | Body de `GET /setup/status` |
| `SetupCommandResult` | Envelope de retorno |

### Enum

| Enum | Variantes (mapeo HTTP) |
| --- | --- |
| `SetupErrorCode` | `SetupYaCompletado`, `UserNameDuplicado`, `EmailDuplicado`, `LegajoDuplicado`, `DocumentoDuplicado`, `PersonaConUsuario` (todos → 409); `EmailInvalido`, `UserNameInvalido`, `PasswordDebil`, `ValidacionIdentity`, `DatosInvalidos` (→ 400); `TransaccionFallida` (→ 500) |

## Vacantes — `SGV.Contracts.Vacantes`

### Constantes

| Constante | Valor |
| --- | --- |
| `VacanteApiRoutes.Base` | `api/v1/vacantes` |
| `VacanteApiRoutes.ById` | `/{id:guid}` |
| `VacanteApiRoutes.CambiarEstado` | `/{id:guid}/estado` |
| `VacanteApiRoutes.EstadosVacanteBase` | `api/v1/estados-vacante` |
| `VacanteApiRoutes.PuestosBase` / `PuestosRoot` | `api/v1/puestos` (legacy) |
| `VacanteApiRoutes.PuestosDisponiblesBase` / `Root` | `api/v1/puestos/disponibles` |
| `StatusAbiertas/Cerradas/Todas` | `abiertas`, `cerradas`, `todas` |
| Sort whitelist | `SortFechaAperturaDesc="fechaapertura_desc"`, `SortFechaAperturaAsc`, `SortPuestoAsc="puesto_asc"` |

### Records (`Vacantes/Comandos/`)

| Tipo | Propósito |
| --- | --- |
| `CrearVacanteRequest(Guid PuestoId, DateTime FechaApertura, string Motivo, string? Observaciones)` | Body de `POST /vacantes` |
| `CambiarEstadoVacanteRequest(Guid EstadoVacanteId, string? Motivo, string? Observaciones)` | Body de `PATCH /vacantes/{id}/estado` |
| `VacanteError(Type, Code, Message, Categoria, StatusCode?)` | Error tipado |
| `VacanteErrorCodigo` (constantes string) | `PuestoInexistente`, `EstadoVacanteInexistente`, `PuestoConVacanteAbierta`, `PuestoOcupado`, `VacanteInexistente`, `EstadoTerminalInmutable`, `ObservacionesMuyLargas`, `CubrirVacanteRequiereCrearOcupacion`, `PersonaIdRequeridoParaCubrir` (obsoleto), `DatosInvalidos` |
| `VacanteCommandResult` | Envelope |

### Consultas (`Vacantes/Consultas/`)

| Tipo | Propósito |
| --- | --- |
| `VacanteListQuery(int Page, int PageSize, string? Search, string? Sort, VacanteSegmentoListado Segmento, Guid? PuestoId)` | Query de `GET /vacantes` |
| `VacanteDto(Id, PuestoId, PuestoDescripcion, EstadoVacanteId, EstadoVacanteNombre, FechaApertura, FechaCierre?, Motivo, Observaciones?)` | Proyección |
| `VacanteDetailDto` | Variante con `HistorialEstadosVacante` |
| `EstadoVacanteDto` | Proyección del catálogo |
| `HistorialEstadoVacanteDto` | Proyección de fila de historial |
| `VacanteSegmentoListado` (enum) | `Abiertas`, `Cerradas`, `Todas` |

### Catálogo

| Tipo | Propósito |
| --- | --- |
| `EstadoVacanteCodigos` | `Abierta`, `EnSeleccion`, `Cubierta`, `Cancelada` |

## Usuarios — `SGV.Contracts.Seguridad.Usuarios`

(ver sección Auth arriba para `Usuario*` records).

| Tipo adicional | Propósito |
| --- | --- |
| `CrearUsuarioRequest(...)` | Body de `POST /usuarios` |
| `ActualizarUsuarioRequest(...)` | Body de `PUT /usuarios/{id}` |
| `AsignarRolesRequest(IReadOnlyCollection<string> Roles)` | Body de `PUT /usuarios/{userId}/roles` |
| `UsuarioListQuery` | Query de `GET /usuarios/consulta` |
| `UsuarioListadoDto(PagedResult<UsuarioDto> Result)` | Wrapper |
| `UsuarioDto(Id, UserName, Email, PersonaId, LockoutEnd?, IsLockedOut, Roles[])` | Proyección |

## Común — `SGV.Contracts.Comun`

| Tipo | Propósito | Notas |
| --- | --- | --- |
| `ErrorCategoria` (enum) | Taxonomía append-only: `NotFound=0`, `Conflict=1`, `Validation=2`, `Unauthorized=3`, `Forbidden=4`, `Transport=5`, `Unexpected=6` | Ver R-03-10 |
| `ErrorCategoriaMappers` | Switches exhaustivos nombre-a-nombre entre `*ErrorType` legacy y `ErrorCategoria` | Sin conversión por ordinal |

## Referencias

- How-to: [Auditar quién modificó entidad](../how-to/08-auditar-quien-modifico-entidad.md)
- How-to: [Rotar JWT signing key](../how-to/03-rotar-jwt-signing-key.md)
- Tutorial: [Levantar el sistema local](../tutorials/01-levantar-sistema-local.md)
- R-03-10 — Taxonomía de errores (referencia cruzada directa)
