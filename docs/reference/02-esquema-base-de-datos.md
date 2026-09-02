# R-03-02 — Esquema de base de datos

Referencia estructural del esquema MySQL 8 gestionado por `SgvDbContext` (`src/SGV.Infraestructura/Persistencia/SgvDbContext.cs`). Cada tabla deriva de un `DbSet<T>` y se materializa mediante una `IEntityTypeConfiguration<T>` ubicada en `src/SGV.Infraestructura/Persistencia/Configuraciones/`.

> **Collation por defecto**: `utf8mb4_unicode_ci`. La columna `Persona.TipoDocumentoId` usa `ascii_general_ci`; la columna `ActiveDocumentoUnique` usa `utf8mb4_unicode_ci` con `varchar(120)`.
> **Generador de PK**: `Id` Guid, `ValueGeneratedNever()` — el dominio asigna el Guid en el alta (no autoincrement).
> **Soft delete**: columnas `IsDeleted`, `DeletedAt`, `DeletedByUserId` para todas las tablas que extienden `AuditableEntityBase` (excepto `AspNetUsers` desde la migración `20260716120000`).
> **Columnas generadas**: varias tablas usan columnas `STORED` (`HasComputedColumnSql(... stored: true)`) para emular índices únicos filtrados, dado que MySQL 8 no soporta `WHERE` en unique indexes.

## Resumen de tablas

| Tabla | DbSet | Tipo de borrado | Columnas generadas | Triggers / check constraints |
| --- | --- | --- | --- | --- |
| `UnidadesOrganizativas` | `UnidadesOrganizativas` | Soft | `ActiveCodigoUnique` | `CK_UnidadesOrganizativas_UnidadPadre`; triggers anti-ciclos INSERT/UPDATE |
| `Cargos` | `Cargos` | Soft | `ActiveCodigoUnique` | — |
| `Habilidades` | `Habilidades` | Soft | `ActiveCodigoUnique` | — |
| `NivelesHabilidad` | `NivelesHabilidad` | Hard | — | — |
| `CargoHabilidades` | `CargoHabilidades` | Hard | — | `CK_CargoHabilidades_Ponderacion` (>0) |
| `Personas` | `Personas` | Soft | `ActiveLegajoUnique`, `ActiveEmailUnique`, `ActiveDocumentoUnique` | — |
| `PersonaHabilidades` | `PersonaHabilidades` | Hard | — | — |
| `Puestos` | `Puestos` | Soft | `ActiveCodigoUnique` | `CK_Puestos_PuestoSuperior` |
| `Ocupaciones` | `Ocupaciones` | Soft | `ActivePuestoIdUnique`, `ActivePersonaPuestoUnique` | `CK_Ocupaciones_Fechas` (FechaFin ≥ FechaInicio) |
| `EstadosVacante` | `EstadosVacante` | Hard | — | — |
| `Vacantes` | `Vacantes` | Soft | `ActivePuestoIdUnique` | — |
| `HistorialEstadosVacante` | `HistorialEstadosVacante` | Hard | — | — |
| `Postulantes` | `Postulantes` | Hard | — | — |
| `EstadosPostulacion` | `EstadosPostulacion` | Hard | — | — |
| `Postulaciones` | `Postulaciones` | Soft | — | `CK_Postulaciones_PuntajeCompatibilidad` (0..100) |
| `HistorialEstadosPostulacion` | `HistorialEstadosPostulacion` | Hard | — | — |
| `EvaluacionesPostulacion` | `EvaluacionesPostulacion` | Hard | — | — |
| `Auditorias` | `Auditorias` | Hard | — | — |
| `TiposUnidadOrganizativa` | `TiposUnidadOrganizativa` | Hard | — | — |
| `NivelesCargo` | `NivelesCargo` | Hard | — | — |
| `TiposDocumento` | `TiposDocumento` | Hard | — | — |
| `CategoriasHabilidad` | `CategoriasHabilidad` | Hard | — | — |
| `RefreshTokens` | `RefreshTokens` | Hard | — | — |
| `AspNetUsers` | (Identity) | Hard | — | — |
| `AspNetRoles` | (Identity) | Hard | — | — |
| `AspNetUserRoles` | (Identity) | Hard | — | — |
| `AspNetUserClaims` | (Identity) | Hard | — | — |
| `AspNetUserLogins` | (Identity) | Hard | — | — |
| `AspNetUserTokens` | (Identity) | Hard | — | — |
| `AspNetRoleClaims` | (Identity) | Hard | — | — |

## Bases de herencia

| Tipo | Columnas provistas | Aplicado vía |
| --- | --- | --- |
| `EntityBase` | `Id` (Guid, `ValueGeneratedNever`) | `ConfigurarId<T>()` |
| `AuditableEntityBase : EntityBase` | `CreatedAt`, `CreatedByUserId` (450), `UpdatedAt?`, `UpdatedByUserId?` (450), `IsDeleted` (bool), `DeletedAt?`, `DeletedByUserId?` (450) + índice sobre `IsDeleted` | `ConfigurarAuditoria<T>()` |
| `IdentityUser` (claves string) | `Id`, `Email`, `PasswordHash`, `LockoutEnd`, etc. | Scaffold Identity |

## UnidadesOrganizativas

`UnidadOrganizativaEntity : AuditableEntityBase`. Configuración: `UnidadOrganizativaConfiguracion.cs`.

| Columna | Tipo CLR | Tipo SQL | Restricciones / notas |
| --- | --- | --- | --- |
| `Id` | `Guid` | `char(36)` (via `ConfigurarId`) | PK |
| `UnidadPadreId` | `Guid?` | `char(36)` | FK self-referencing con `OnDelete(Restrict)` |
| `Codigo` | `string` (50) | `varchar(50)` | `IsRequired` |
| `Nombre` | `string` (200) | `varchar(200)` | `IsRequired` |
| `TipoUnidadOrganizativaId` | `Guid` | `char(36)` | FK `Restrict` |
| `Descripcion` | `string?` (1000) | `varchar(1000)` | Opcional |
| `VigenteDesde` | `DateOnly?` | `date` | Opcional |
| `VigenteHasta` | `DateOnly?` | `date` | Opcional |
| `IsActive` | `bool` | `tinyint(1)` | — |
| Columnas de auditoría | — | — | `CreatedAt/By`, `UpdatedAt/By`, `IsDeleted`, `DeletedAt/By` |
| `ActiveCodigoUnique` (shadow) | `string?` | `varchar(50)` STORED | `CASE WHEN IsDeleted = 0 THEN Codigo ELSE NULL END`; índice UNIQUE |

**Check**: `CK_UnidadesOrganizativas_UnidadPadre` (`UnidadPadreId IS NULL OR UnidadPadreId <> Id`).
**Triggers** (migración `20260816203122`):
- `trg_UnidadesOrganizativas_BeforeInsert_Ciclo`: rechaza cuando la CTE recursiva de padres hasta `depth < 32` cierra sobre `NEW.Id` con `SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'CicloJerarquico'`.
- `trg_UnidadesOrganizativas_BeforeUpdate_Ciclo`: análogo en UPDATE; cortocircuita cuando `NEW.UnidadPadreId = OLD.UnidadPadreId`.

**Índices**: `IX_UnidadesOrganizativas_TipoUnidadOrganizativaId`, `IX_UnidadesOrganizativas_UnidadPadreId`, `IX_UnidadesOrganizativas_Nombre`, `IX_UnidadesOrganizativas_ActivoPadre (IsActive, IsDeleted, UnidadPadreId)`, `IX_UnidadesOrganizativas_ActivoTipo (IsActive, IsDeleted, TipoUnidadOrganizativaId)`, `IX_UnidadesOrganizativas_ActivoCodigo (IsActive, IsDeleted, Codigo)`, índice UNIQUE sobre `ActiveCodigoUnique`.

## Cargos

`CargoEntity : AuditableEntityBase`. Configuración: `CargoConfiguracion.cs`.

| Columna | Tipo CLR | Tipo SQL | Notas |
| --- | --- | --- | --- |
| `Id` | `Guid` | `char(36)` | PK |
| `Codigo` | `string` (50) | `varchar(50)` | `IsRequired` |
| `Nombre` | `string` (200) | `varchar(200)` | `IsRequired` |
| `Descripcion` | `string?` (1000) | `varchar(1000)` | — |
| `NivelId` | `Guid` | `char(36)` | FK `NivelesCargo`, `Restrict` |
| Auditoría | — | — | estándar |
| `ActiveCodigoUnique` (shadow) | `string?` | `varchar(50)` STORED | `CASE WHEN IsDeleted = 0 THEN Codigo ELSE NULL END`; índice UNIQUE |

**Índices**: `IX_Cargos_NivelId`, `IX_Cargos_Nombre`, índice UNIQUE sobre `ActiveCodigoUnique`.

## Habilidades

`HabilidadEntity : AuditableEntityBase`. Configuración: `HabilidadConfiguracion.cs`.

| Columna | Tipo CLR | Tipo SQL | Notas |
| --- | --- | --- | --- |
| `Id` | `Guid` | `char(36)` | PK |
| `Codigo` | `string` (50) | `varchar(50)` | `IsRequired` |
| `Nombre` | `string` (200) | `varchar(200)` | `IsRequired` |
| `Descripcion` | `string?` (1000) | `varchar(1000)` | — |
| `CategoriaId` | `Guid?` | `char(36)` | FK `CategoriasHabilidad.Id`, `Restrict` |
| Auditoría | — | — | estándar |
| `ActiveCodigoUnique` (shadow) | `string?` | `varchar(50)` STORED | UNIQUE |

**Índices**: `IX_Habilidades_CategoriaId`, índice UNIQUE sobre `ActiveCodigoUnique`.

## NivelesHabilidad

`NivelHabilidadEntity : AuditableEntityBase` (entidad catálogo con bloque GUID `70000000-…`; ver R-03-08).

## CargoHabilidades

`CargoHabilidadEntity : EntityBase`. Configuración: `CargoHabilidadConfiguracion.cs`.

| Columna | Tipo CLR | Notas |
| --- | --- | --- |
| `Id` | `Guid` | PK |
| `CargoId` | `Guid` | FK `Cargos` con `OnDelete(Cascade)` |
| `HabilidadId` | `Guid` | FK `Habilidades`, `Restrict` |
| `NivelRequeridoId` | `Guid` | FK `NivelesHabilidad`, `Restrict` |
| `Ponderacion` | `decimal` (precision 5,2) | `>0` (`CK_CargoHabilidades_Ponderacion`) |

**Índices**: `IX_CargoHabilidades_CargoId_HabilidadId UNIQUE`, `IX_CargoHabilidades_HabilidadId`.

## Personas

`PersonaEntity : AuditableEntityBase`. Configuración: `PersonaConfiguracion.cs`.

| Columna | Tipo CLR | Tipo SQL | Notas |
| --- | --- | --- | --- |
| `Id` | `Guid` | `char(36)` | PK |
| `Legajo` | `string?` (50) | `varchar(50)` | Opcional |
| `Nombres` | `string` (100) | `varchar(100)` | `IsRequired` |
| `Apellidos` | `string` (100) | `varchar(100)` | `IsRequired` |
| `Email` | `string?` (320) | `varchar(320)` | Opcional |
| `TipoDocumentoId` | `Guid?` | `char(36)`, collation `ascii_general_ci` | FK `TiposDocumento`, `Restrict` |
| `NumeroDocumento` | `string?` (50) | `varchar(50)` | Opcional |
| `Telefono` | `string?` (50) | `varchar(50)` | Opcional |
| `IsActive` | `bool` | `tinyint(1)` | — |
| Auditoría | — | — | estándar |
| `ActiveLegajoUnique` (shadow) | `string?` | `varchar(50)` STORED | UNIQUE |
| `ActiveEmailUnique` (shadow) | `string?` | `varchar(320)` STORED | UNIQUE |
| `ActiveDocumentoUnique` (shadow) | `string?` | `varchar(120)` STORED, collation `utf8mb4_unicode_ci` | `CONCAT(TipoDocumentoId, ':', NumeroDocumento)`; UNIQUE |

**Índices**: `IX_Personas_NumeroDocumento`, `IX_Personas_Apellidos_Nombres`, índices UNIQUE sobre las tres columnas generadas.

## PersonaHabilidades

`PersonaHabilidadEntity : EntityBase`. Configuración: `PersonaHabilidadConfiguracion.cs`.

| Columna | Tipo CLR | Notas |
| --- | --- | --- |
| `Id` | `Guid` | PK |
| `PersonaId` | `Guid` | FK `Personas` con `OnDelete(Cascade)` |
| `HabilidadId` | `Guid` | FK `Habilidades`, `Restrict` |
| `NivelHabilidadId` | `Guid` | FK `NivelesHabilidad`, `Restrict` |
| `Fuente` | `string?` (100) | — |

**Índices**: `IX_PersonaHabilidades_PersonaId_HabilidadId UNIQUE`, `IX_PersonaHabilidades_HabilidadId`.

## Puestos

`PuestoEntity : AuditableEntityBase`. Configuración: `PuestoConfiguracion.cs`.

| Columna | Tipo CLR | Notas |
| --- | --- | --- |
| `Id` | `Guid` | PK |
| `Codigo` | `string` (50) | `IsRequired` |
| `Nombre` | `string` (200) | `IsRequired` |
| `Descripcion` | `string?` (1000) | — |
| `UnidadOrganizativaId` | `Guid` | FK `UnidadesOrganizativas`, `Restrict` |
| `CargoId` | `Guid` | FK `Cargos`, `Restrict` |
| `PuestoSuperiorId` | `Guid?` | FK self, `Restrict` |
| Auditoría | — | estándar |
| `ActiveCodigoUnique` (shadow) | `string?` STORED | UNIQUE |

**Check**: `CK_Puestos_PuestoSuperior` (`PuestoSuperiorId IS NULL OR PuestoSuperiorId <> Id`).
**Índices**: `IX_Puestos_UnidadOrganizativaId`, `IX_Puestos_CargoId`, `IX_Puestos_PuestoSuperiorId`, índice UNIQUE sobre `ActiveCodigoUnique`.

## Ocupaciones

`OcupacionEntity : AuditableEntityBase`. Configuración: `OcupacionConfiguracion.cs`.

| Columna | Tipo CLR | Notas |
| --- | --- | --- |
| `Id` | `Guid` | PK |
| `PersonaId` | `Guid` | FK `Personas`, `Restrict` |
| `PuestoId` | `Guid` | FK `Puestos`, `Restrict` |
| `VacanteId` | `Guid?` | FK opcional `Vacantes`, `Restrict` (migración `20260804235936`) |
| `FechaInicio` | `DateOnly` | `IsRequired` |
| `FechaFin` | `DateOnly?` | Nulable |
| `TipoAsignacion` | enum (int) | `IsRequired`, convertido a `int` |
| `Observaciones` | `string?` (1000) | — |
| Auditoría | — | estándar |
| `ActivePuestoIdUnique` (shadow) | `string?` (36) STORED, collation `ascii_general_ci` | `CASE WHEN FechaFin IS NULL AND IsDeleted = 0 THEN PuestoId ELSE NULL END`; UNIQUE |
| `ActivePersonaPuestoUnique` (shadow) | `string?` (100) STORED | `CONCAT(PersonaId, ':', PuestoId)`; UNIQUE |

**Check**: `CK_Ocupaciones_Fechas` (`FechaFin IS NULL OR FechaFin >= FechaInicio`).
**Índices**: `IX_Ocupaciones_VacanteId`, `IX_Ocupaciones_PuestoId_FechaInicio_FechaFin`, `IX_Ocupaciones_PersonaId_FechaInicio_FechaFin`, UNIQUE sobre las dos columnas generadas.

## Vacantes

`VacanteEntity : AuditableEntityBase`. Configuración: `VacanteConfiguracion.cs`.

| Columna | Tipo CLR | Notas |
| --- | --- | --- |
| `Id` | `Guid` | PK |
| `PuestoId` | `Guid` | FK `Puestos`, `Restrict` |
| `EstadoVacanteId` | `Guid` | FK `EstadosVacante`, `Restrict` |
| `FechaApertura` | `DateTime` | `IsRequired` |
| `FechaCierre` | `DateTime?` | Nulable |
| `Motivo` | `string` (500) | `IsRequired` |
| `Observaciones` | `string?` (1000) | — |
| Auditoría | — | estándar |
| `ActivePuestoIdUnique` (shadow) | `string?` (36) STORED, collation `ascii_general_ci` | `CASE WHEN FechaCierre IS NULL AND IsDeleted = 0 THEN PuestoId ELSE NULL END`; UNIQUE (`IX_Vacantes_ActivePuestoIdUnique`) |

**Índices**: `IX_Vacantes_PuestoId`, `IX_Vacantes_EstadoVacanteId`, `IX_Vacantes_FechaApertura`, `IX_Vacantes_EstadoVacanteId_FechaApertura`, UNIQUE sobre `ActivePuestoIdUnique`.

## EstadosVacante

`EstadoVacanteEntity : EntityBase`. Catálogo inmutable con 4 filas (ver R-03-08).

| Columna | Tipo CLR | Notas |
| --- | --- | --- |
| `Id` | `Guid` | PK; bloque GUID reservado |
| `Nombre` | `string` (50) | — |
| `Orden` | `int` | Posición en el flujo |
| `EsTerminal` | `bool` | Marca estados sin salida |
| `Color` | `string?` | — |

## HistorialEstadosVacante

| Columna | Tipo | Notas |
| --- | --- | --- |
| `Id` | `Guid` | PK |
| `VacanteId` | `Guid` | FK `Vacantes`, `Restrict` |
| `EstadoVacanteId` | `Guid` | FK `EstadosVacante`, `Restrict` |
| `Fecha` | `DateTime` | `IsRequired` |
| `Motivo` | `string?` (500) | — |
| `UsuarioId` | `string?` (450) | FK `AspNetUsers` |

## Postulantes / Postulaciones / EvaluacionesPostulacion

| Tabla | Columnas clave | Notas |
| --- | --- | --- |
| `Postulantes` | `Id`, `PersonaId` (FK `Personas`), `Email`, `Telefono`, `CvUrl` | Catálogo de candidatos externos |
| `EstadosPostulacion` | `Id`, `Nombre`, `Orden`, `EsTerminal` | Catálogo inmutable |
| `Postulaciones` | `Id`, `VacanteId`, `PostulanteId`, `EstadoPostulacionId`, `FechaPostulacion`, `PuntajeCompatibilidad` (decimal 5,2), `NivelCompatibilidad` (50), `Observaciones` (1000) | FKs en `Restrict`; check `PuntajeCompatibilidad IS NULL OR (0..100)` |
| `HistorialEstadosPostulacion` | `Id`, `PostulacionId`, `EstadoPostulacionId`, `Fecha`, `Motivo`, `UsuarioId` | — |
| `EvaluacionesPostulacion` | `Id`, `PostulacionId`, `EvaluadorId`, `Fecha`, `Puntaje`, `Comentarios` | — |

**Índices** (`PostulacionConfiguracion`): `IX_Postulaciones_VacanteId_PostulanteId UNIQUE`, `IX_Postulaciones_EstadoPostulacionId`, `IX_Postulaciones_VacanteId_EstadoPostulacionId`.

## Auditorias

`AuditoriaEntity : EntityBase` (no auditable; es la tabla destino de la auditoría). Configuración: `AuditoriaConfiguracion.cs`.

| Columna | Tipo CLR | Tipo SQL | Notas |
| --- | --- | --- | --- |
| `Id` | `Guid` | `char(36)` | PK |
| `UserId` | `string?` (450) | `varchar(450)` | Opcional |
| `UserName` | `string?` | — | — |
| `EntityName` | `string` (200) | `varchar(200)` | `IsRequired` |
| `EntityId` | `string` (100) | `varchar(100)` | `IsRequired` |
| `Operation` | `string` (50) | `varchar(50)` | `IsRequired` |
| `OccurredAt` | `DateTime` | — | — |
| `CorrelationId` | `string?` | — | Indexable (ver índices) |
| `OldValuesJson` | `string?` | `longtext` | — |
| `NewValuesJson` | `string?` | `longtext` | — |
| `ChangedPropertiesJson` | `string?` | `longtext` | — |

**Índices**: `IX_Auditorias_EntityName_EntityId_OccurredAt`, `IX_Auditorias_UserId_OccurredAt`, `IX_Auditorias_CorrelationId_OccurredAt` (compuesto covering para `sort=correlacion_desc`).

## Catálogos inmutables

`TiposUnidadOrganizativa`, `NivelesCargo`, `TiposDocumento`, `CategoriasHabilidad` siguen el patrón `EntityBase` (sin soft delete) con bloque GUID reservado (ver R-03-08). Sus PK son Guid y se siembran en `DatosSemilla.cs`.

| Columna común | Tipo | Notas |
| --- | --- | --- |
| `Id` | `Guid` | Bloque reservado (ver R-03-08) |
| `Nombre` | `string` (50–200) | — |
| `Descripcion` | `string?` | Opcional |
| `Orden` | `int?` | Opcional (donde aplique) |

## AspNetUsers (Identity)

`SgvIdentityUser : IdentityUser` con clave string. Configuración: `SgvIdentityUserConfiguracion.cs`.

| Columna extra | Tipo | Notas |
| --- | --- | --- |
| `PersonaId` | `Guid` | FK `Personas`, `OnDelete(Restrict)`, constraint `FK_AspNetUsers_Personas_PersonaId`. Índice UNIQUE heredado de la migración `VincularIdentityUsuariosAPersonas`. |

**Tablas Identity estándar**: `AspNetRoles`, `AspNetUserRoles`, `AspNetUserClaims`, `AspNetUserLogins`, `AspNetUserTokens`, `AspNetRoleClaims`. `AspNetRoleNames` no se usa — los nombres de rol viven en `RolesSgv` (`Administrador`, `GestorVacantes`, `Consultor`).

## RefreshTokens

`RefreshTokenEntity : EntityBase`. Configuración: `RefreshTokenConfiguracion.cs`.

| Columna | Tipo CLR | Tipo SQL | Notas |
| --- | --- | --- | --- |
| `Id` | `Guid` | `char(36)` | PK |
| `UserId` | `string` | `varchar(450)` | FK `AspNetUsers` con `OnDelete(Cascade)`, constraint `FK_RefreshTokens_AspNetUsers_UserId` |
| `FamilyId` | `Guid` | `char(36)` | Agrupa rotaciones de un mismo login |
| `TokenHash` | `string` | `varchar(64)` | SHA-256 hex |
| `CreatedAt` | `DateTime` | `datetime(6)` | — |
| `ExpiresAt` | `DateTime` | `datetime(6)` | — |
| `LastUsedAt` | `DateTime` | `datetime(6)` | — |
| `ReplacedById` | `Guid?` | `char(36)` | Puntero lógico al token siguiente (sin FK self-referencing) |

**Índices**: `IX_RefreshTokens_TokenHash UNIQUE`, `IX_RefreshTokens_UserId`, `IX_RefreshTokens_FamilyId`, `IX_RefreshTokens_ReplacedById`.

## Columnas de auditoría (todas las tablas auditables)

| Columna | Tipo | Notas |
| --- | --- | --- |
| `CreatedAt` | `DateTime` | Set por `AuditoriaSaveChangesInterceptor` al `Insert` |
| `CreatedByUserId` | `string?` (450) | FK opcional `AspNetUsers.Id` (sin constraint) |
| `UpdatedAt` | `DateTime?` | Set en cada `Update` |
| `UpdatedByUserId` | `string?` (450) | FK opcional |
| `IsDeleted` | `bool` | Soft delete flag |
| `DeletedAt` | `DateTime?` | — |
| `DeletedByUserId` | `string?` (450) | FK opcional |

Índice implícito `IX_<Tabla>_IsDeleted` vía `ConfigurarAuditoria()`.

## Notas operativas

- **Script idempotente**: `docs/migracion-inicial-sgv.sql` materializa el esquema completo sobre una base vacía; la variante MariaDB vive en `docs/migracion-inicial-sgv-mariadb.sql` (collation `utf8mb4_unicode_ci` + columnas generadas `STORED`).
- **Trigger anti-ciclos**: el script `docs/script-listar-ciclos-jerarquia-unidades-organizativas.sql` diagnostica la jerarquía tras desactivar los triggers (operación de remediación documentada en R-03-09 health checks).
- **Auditoría**: las columnas `Token*` y `*Json` están marcadas como sensibles en el interceptor; ver R-03-09 / R-03-12.

## Referencias

- Tutorial: [Levantar el sistema local](../tutorials/01-levantar-sistema-local.md)
- How-to: [Agregar migración EF Core](../how-to/05-agregar-migracion-ef-core.md)
- How-to: [Diagnosticar ciclos jerárquicos](../how-to/01-diagnosticar-ciclos-jerarquia.md)
- How-to: [Auditar quién modificó entidad](../how-to/08-auditar-quien-modifico-entidad.md)
- How-to: [Levantar MySQL Docker para tests](../how-to/07-levantar-mysql-docker-para-tests.md)
