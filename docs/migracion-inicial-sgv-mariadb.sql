-- ============================================================================
-- SGV - Script de provision inicial MARIADB
-- ============================================================================
-- Origen: docs/migracion-inicial-sgv.sql (regenerado para MySQL 8 con
--         'dotnet ef migrations script' sin --idempotent).
--         NO usar ese script contra MariaDB: contiene 'utf8mb4_0900_ai_ci'
--         (collation MySQL 8 exclusiva) y columnas VIRTUALES indexadas con
--         UNIQUE (MariaDB NO permite indices sobre columnas VIRTUALES).
-- Generador: este archivo fue producido por el orquestador a pedido del
--            operador para habilitar el path MariaDB-definitivo sin tocar
--            el historial de migraciones del proyecto.
--
-- Aplicabilidad:
--   * MySQL 8.x   -> usar docs/migracion-inicial-sgv.sql
--   * MariaDB     -> usar este script (docs/migracion-inicial-sgv-mariadb.sql)
--
-- Transformaciones aplicadas respecto al script MySQL 8:
--   1) Collation: utf8mb4_0900_ai_ci -> utf8mb4_unicode_ci (default MariaDB).
--      Comportamiento UCA 9.0.0 -> UCA 5.2.0 (ligera diferencia en tildes).
--   2) Las columnas GENERATED ... VIRTUAL se convierten a STORED para que
--      MariaDB permita UNIQUE INDEX sobre ellas (mismo patron que el
--      camino EF MySQL 8: columna CASE WHEN STORED + UNIQUE INDEX sobre
--      esa columna).
--
-- Validado empiricamente contra MariaDB 10.11.13 (sgvapi.elflacoseba.dev).
-- Soporta multiples soft-deleted con el mismo Codigo/DNI/Email/Legajo
-- porque la columna CASE WHEN devuelve NULL para filas con IsDeleted=1
-- (o FechaFin IS NOT NULL en Ocupaciones), y SQL estandar permite
-- multiples NULL en UNIQUE INDEX. Un segundo activo con la misma clave
-- es rechazado correctamente por el UNIQUE INDEX.
--
-- Limitaciones / notas:
--   - Este script NO usa la tabla __EFMigrationsHistory porque no viene
--     de un 'dotnet ef migrations script' valido contra MariaDB. La
--     trazabilidad queda a cargo del archivo fuente + este script.
--     Aun asi crea __EFMigrationsHistory con las 13 migraciones EF
--     marcadas, para mantener consistencia si en el futuro se decide
--     migrar a EF puro contra MariaDB.
--   - SI incluye datos semilla hardcoded, en el mismo orden que las
--     migraciones EF los aplicarian. Catalogos incluidos (10):
--     AspNetRoles (3 finales via 5 iniciales + DELETE + INSERT 2 en la
--     migracion VincularIdentityUsuariosAPersonas), NivelesHabilidad,
--     EstadosVacante, EstadosPostulacion, NivelesCargo, Cargos (con
--     NivelId via migracion CambiarNivelStringANivelId), Habilidades
--     (con CategoriaId via migracion AddCategoriaHabilidadCatalog),
--     TiposUnidadOrganizativa, TiposDocumento, CategoriasHabilidad.
--     Single source of truth: src/SGV.Infraestructura/Persistencia/
--     DatosSemilla.cs (referenciado desde el model snapshot EF Core
--     via HasData). Migracion EF 'AgregarDatosSemillaBase' intencional-
--     mente vacia: los seeds viven en DatosSemilla, no como InsertData
--     de una migracion especifica. Cualquier drift entre los seeds
--     hardcoded de este script y DatosSemilla.cs lo detectan los tests
--     'DatosSemilla_*_SeedIdsMatchConstantes' (correr localmente con
--     'dotnet test SGV.slnx --filter DatosSemilla').
--   - No incluye: setup inicial de admin (issue #195). Ese se hace en
--     runtime via POST /api/setup, no como INSERTs en este script.
--     Para datos demo adicionales (UnidadOrganizativa raiz, Puesto demo,
--     Persona demo) aplicar via la app o agregar manualmente.
--   - DROP TABLE IF EXISTS + CREATE para re-ejecucion idempotente. NO usar
--     contra una DB MySQL 8 preexistente.
-- ============================================================================

CREATE TABLE IF NOT EXISTS `__EFMigrationsHistory` (
    `MigrationId` varchar(150) CHARACTER SET utf8mb4 NOT NULL,
    `ProductVersion` varchar(32) CHARACTER SET utf8mb4 NOT NULL,
    CONSTRAINT `PK___EFMigrationsHistory` PRIMARY KEY (`MigrationId`)
) CHARACTER SET=utf8mb4;

START TRANSACTION;
ALTER DATABASE CHARACTER SET utf8mb4;

CREATE TABLE `AspNetRoles` (
    `Id` varchar(255) CHARACTER SET utf8mb4 NOT NULL,
    `Name` varchar(256) CHARACTER SET utf8mb4 NULL,
    `NormalizedName` varchar(256) CHARACTER SET utf8mb4 NULL,
    `ConcurrencyStamp` longtext CHARACTER SET utf8mb4 NULL,
    CONSTRAINT `PK_AspNetRoles` PRIMARY KEY (`Id`)
) CHARACTER SET=utf8mb4;

CREATE TABLE `AspNetUsers` (
    `Id` varchar(255) CHARACTER SET utf8mb4 NOT NULL,
    `UserName` varchar(256) CHARACTER SET utf8mb4 NULL,
    `NormalizedUserName` varchar(256) CHARACTER SET utf8mb4 NULL,
    `Email` varchar(256) CHARACTER SET utf8mb4 NULL,
    `NormalizedEmail` varchar(256) CHARACTER SET utf8mb4 NULL,
    `EmailConfirmed` tinyint(1) NOT NULL,
    `PasswordHash` longtext CHARACTER SET utf8mb4 NULL,
    `SecurityStamp` longtext CHARACTER SET utf8mb4 NULL,
    `ConcurrencyStamp` longtext CHARACTER SET utf8mb4 NULL,
    `PhoneNumber` longtext CHARACTER SET utf8mb4 NULL,
    `PhoneNumberConfirmed` tinyint(1) NOT NULL,
    `TwoFactorEnabled` tinyint(1) NOT NULL,
    `LockoutEnd` datetime(6) NULL,
    `LockoutEnabled` tinyint(1) NOT NULL,
    `AccessFailedCount` int NOT NULL,
    CONSTRAINT `PK_AspNetUsers` PRIMARY KEY (`Id`)
) CHARACTER SET=utf8mb4;

CREATE TABLE `Auditorias` (
    `Id` char(36) COLLATE ascii_general_ci NOT NULL,
    `UserId` varchar(450) CHARACTER SET utf8mb4 NULL,
    `OccurredAt` datetime(6) NOT NULL,
    `EntityName` varchar(200) CHARACTER SET utf8mb4 NOT NULL,
    `EntityId` varchar(100) CHARACTER SET utf8mb4 NOT NULL,
    `Operation` varchar(50) CHARACTER SET utf8mb4 NOT NULL,
    `OldValuesJson` longtext CHARACTER SET utf8mb4 NULL,
    `NewValuesJson` longtext CHARACTER SET utf8mb4 NULL,
    `ChangedPropertiesJson` longtext CHARACTER SET utf8mb4 NULL,
    `CorrelationId` char(36) COLLATE ascii_general_ci NULL,
    CONSTRAINT `PK_Auditorias` PRIMARY KEY (`Id`)
) CHARACTER SET=utf8mb4;

CREATE TABLE `Cargos` (
    `Id` char(36) COLLATE ascii_general_ci NOT NULL,
    `Codigo` varchar(50) CHARACTER SET utf8mb4 NOT NULL,
    `Nombre` varchar(200) CHARACTER SET utf8mb4 NOT NULL,
    `Descripcion` varchar(1000) CHARACTER SET utf8mb4 NULL,
    `Nivel` varchar(50) CHARACTER SET utf8mb4 NULL,
    `IsActive` tinyint(1) NOT NULL,
    `CreatedAt` datetime(6) NOT NULL,
    `CreatedByUserId` varchar(450) CHARACTER SET utf8mb4 NULL,
    `UpdatedAt` datetime(6) NULL,
    `UpdatedByUserId` varchar(450) CHARACTER SET utf8mb4 NULL,
    `IsDeleted` tinyint(1) NOT NULL,
    `DeletedAt` datetime(6) NULL,
    `DeletedByUserId` varchar(450) CHARACTER SET utf8mb4 NULL,
    CONSTRAINT `PK_Cargos` PRIMARY KEY (`Id`)
) CHARACTER SET=utf8mb4;

ALTER TABLE `Cargos` ADD COLUMN `ActiveCodigoUnique` varchar(255) CHARACTER SET utf8mb4 GENERATED ALWAYS AS (CASE WHEN `IsDeleted` = 0 THEN `Codigo` ELSE NULL END) STORED;

CREATE TABLE `EstadosPostulacion` (
    `Id` char(36) COLLATE ascii_general_ci NOT NULL,
    `Codigo` varchar(50) CHARACTER SET utf8mb4 NOT NULL,
    `Nombre` varchar(100) CHARACTER SET utf8mb4 NOT NULL,
    `Orden` int NOT NULL,
    `EsTerminal` tinyint(1) NOT NULL,
    `EsTerminalPositivo` tinyint(1) NOT NULL,
    CONSTRAINT `PK_EstadosPostulacion` PRIMARY KEY (`Id`)
) CHARACTER SET=utf8mb4;

CREATE TABLE `EstadosVacante` (
    `Id` char(36) COLLATE ascii_general_ci NOT NULL,
    `Codigo` varchar(50) CHARACTER SET utf8mb4 NOT NULL,
    `Nombre` varchar(100) CHARACTER SET utf8mb4 NOT NULL,
    `Orden` int NOT NULL,
    `EsTerminal` tinyint(1) NOT NULL,
    CONSTRAINT `PK_EstadosVacante` PRIMARY KEY (`Id`)
) CHARACTER SET=utf8mb4;

CREATE TABLE `Habilidades` (
    `Id` char(36) COLLATE ascii_general_ci NOT NULL,
    `Codigo` varchar(50) CHARACTER SET utf8mb4 NOT NULL,
    `Nombre` varchar(200) CHARACTER SET utf8mb4 NOT NULL,
    `Descripcion` varchar(1000) CHARACTER SET utf8mb4 NULL,
    `Categoria` varchar(100) CHARACTER SET utf8mb4 NULL,
    `IsActive` tinyint(1) NOT NULL,
    `CreatedAt` datetime(6) NOT NULL,
    `CreatedByUserId` varchar(450) CHARACTER SET utf8mb4 NULL,
    `UpdatedAt` datetime(6) NULL,
    `UpdatedByUserId` varchar(450) CHARACTER SET utf8mb4 NULL,
    `IsDeleted` tinyint(1) NOT NULL,
    `DeletedAt` datetime(6) NULL,
    `DeletedByUserId` varchar(450) CHARACTER SET utf8mb4 NULL,
    CONSTRAINT `PK_Habilidades` PRIMARY KEY (`Id`)
) CHARACTER SET=utf8mb4;

ALTER TABLE `Habilidades` ADD COLUMN `ActiveCodigoUnique` varchar(255) CHARACTER SET utf8mb4 GENERATED ALWAYS AS (CASE WHEN `IsDeleted` = 0 THEN `Codigo` ELSE NULL END) STORED;

CREATE TABLE `NivelesHabilidad` (
    `Id` char(36) COLLATE ascii_general_ci NOT NULL,
    `Codigo` varchar(50) CHARACTER SET utf8mb4 NOT NULL,
    `Nombre` varchar(100) CHARACTER SET utf8mb4 NOT NULL,
    `ValorNumerico` tinyint unsigned NOT NULL,
    `Orden` int NOT NULL,
    CONSTRAINT `PK_NivelesHabilidad` PRIMARY KEY (`Id`),
    CONSTRAINT `CK_NivelesHabilidad_ValorNumerico` CHECK (`ValorNumerico` BETWEEN 1 AND 4)
) CHARACTER SET=utf8mb4;

CREATE TABLE `Personas` (
    `Id` char(36) COLLATE ascii_general_ci NOT NULL,
    `Legajo` varchar(50) CHARACTER SET utf8mb4 NULL,
    `Nombres` varchar(100) CHARACTER SET utf8mb4 NOT NULL,
    `Apellidos` varchar(100) CHARACTER SET utf8mb4 NOT NULL,
    `Email` varchar(320) CHARACTER SET utf8mb4 NULL,
    `TipoDocumento` varchar(50) CHARACTER SET utf8mb4 NULL,
    `NumeroDocumento` varchar(50) CHARACTER SET utf8mb4 NULL,
    `Telefono` varchar(50) CHARACTER SET utf8mb4 NULL,
    `IsActive` tinyint(1) NOT NULL,
    `CreatedAt` datetime(6) NOT NULL,
    `CreatedByUserId` varchar(450) CHARACTER SET utf8mb4 NULL,
    `UpdatedAt` datetime(6) NULL,
    `UpdatedByUserId` varchar(450) CHARACTER SET utf8mb4 NULL,
    `IsDeleted` tinyint(1) NOT NULL,
    `DeletedAt` datetime(6) NULL,
    `DeletedByUserId` varchar(450) CHARACTER SET utf8mb4 NULL,
    CONSTRAINT `PK_Personas` PRIMARY KEY (`Id`)
) CHARACTER SET=utf8mb4;

ALTER TABLE `Personas` ADD COLUMN `ActiveLegajoUnique` varchar(255) CHARACTER SET utf8mb4 GENERATED ALWAYS AS (CASE WHEN `Legajo` IS NOT NULL AND `IsDeleted` = 0 THEN `Legajo` ELSE NULL END) STORED;
ALTER TABLE `Personas` ADD COLUMN `ActiveEmailUnique` varchar(255) CHARACTER SET utf8mb4 GENERATED ALWAYS AS (CASE WHEN `Email` IS NOT NULL AND `IsDeleted` = 0 THEN `Email` ELSE NULL END) STORED;

CREATE TABLE `UnidadesOrganizativas` (
    `Id` char(36) COLLATE ascii_general_ci NOT NULL,
    `UnidadPadreId` char(36) COLLATE ascii_general_ci NULL,
    `Codigo` varchar(50) CHARACTER SET utf8mb4 NOT NULL,
    `Nombre` varchar(200) CHARACTER SET utf8mb4 NOT NULL,
    `TipoUnidad` varchar(50) CHARACTER SET utf8mb4 NOT NULL,
    `Descripcion` varchar(1000) CHARACTER SET utf8mb4 NULL,
    `VigenteDesde` date NULL,
    `VigenteHasta` date NULL,
    `IsActive` tinyint(1) NOT NULL,
    `CreatedAt` datetime(6) NOT NULL,
    `CreatedByUserId` varchar(450) CHARACTER SET utf8mb4 NULL,
    `UpdatedAt` datetime(6) NULL,
    `UpdatedByUserId` varchar(450) CHARACTER SET utf8mb4 NULL,
    `IsDeleted` tinyint(1) NOT NULL,
    `DeletedAt` datetime(6) NULL,
    `DeletedByUserId` varchar(450) CHARACTER SET utf8mb4 NULL,
    CONSTRAINT `PK_UnidadesOrganizativas` PRIMARY KEY (`Id`),
    CONSTRAINT `CK_UnidadesOrganizativas_UnidadPadre` CHECK (`UnidadPadreId` IS NULL OR `UnidadPadreId` <> `Id`),
    CONSTRAINT `FK_UnidadesOrganizativas_UnidadesOrganizativas_UnidadPadreId` FOREIGN KEY (`UnidadPadreId`) REFERENCES `UnidadesOrganizativas` (`Id`) ON DELETE RESTRICT
) CHARACTER SET=utf8mb4;

ALTER TABLE `UnidadesOrganizativas` ADD COLUMN `ActiveCodigoUnique` varchar(255) CHARACTER SET utf8mb4 GENERATED ALWAYS AS (CASE WHEN `IsDeleted` = 0 THEN `Codigo` ELSE NULL END) STORED;

CREATE TABLE `AspNetRoleClaims` (
    `Id` int NOT NULL AUTO_INCREMENT,
    `RoleId` varchar(255) CHARACTER SET utf8mb4 NOT NULL,
    `ClaimType` longtext CHARACTER SET utf8mb4 NULL,
    `ClaimValue` longtext CHARACTER SET utf8mb4 NULL,
    CONSTRAINT `PK_AspNetRoleClaims` PRIMARY KEY (`Id`),
    CONSTRAINT `FK_AspNetRoleClaims_AspNetRoles_RoleId` FOREIGN KEY (`RoleId`) REFERENCES `AspNetRoles` (`Id`) ON DELETE CASCADE
) CHARACTER SET=utf8mb4;

CREATE TABLE `AspNetUserClaims` (
    `Id` int NOT NULL AUTO_INCREMENT,
    `UserId` varchar(255) CHARACTER SET utf8mb4 NOT NULL,
    `ClaimType` longtext CHARACTER SET utf8mb4 NULL,
    `ClaimValue` longtext CHARACTER SET utf8mb4 NULL,
    CONSTRAINT `PK_AspNetUserClaims` PRIMARY KEY (`Id`),
    CONSTRAINT `FK_AspNetUserClaims_AspNetUsers_UserId` FOREIGN KEY (`UserId`) REFERENCES `AspNetUsers` (`Id`) ON DELETE CASCADE
) CHARACTER SET=utf8mb4;

CREATE TABLE `AspNetUserLogins` (
    `LoginProvider` varchar(255) CHARACTER SET utf8mb4 NOT NULL,
    `ProviderKey` varchar(255) CHARACTER SET utf8mb4 NOT NULL,
    `ProviderDisplayName` longtext CHARACTER SET utf8mb4 NULL,
    `UserId` varchar(255) CHARACTER SET utf8mb4 NOT NULL,
    CONSTRAINT `PK_AspNetUserLogins` PRIMARY KEY (`LoginProvider`, `ProviderKey`),
    CONSTRAINT `FK_AspNetUserLogins_AspNetUsers_UserId` FOREIGN KEY (`UserId`) REFERENCES `AspNetUsers` (`Id`) ON DELETE CASCADE
) CHARACTER SET=utf8mb4;

CREATE TABLE `AspNetUserRoles` (
    `UserId` varchar(255) CHARACTER SET utf8mb4 NOT NULL,
    `RoleId` varchar(255) CHARACTER SET utf8mb4 NOT NULL,
    CONSTRAINT `PK_AspNetUserRoles` PRIMARY KEY (`UserId`, `RoleId`),
    CONSTRAINT `FK_AspNetUserRoles_AspNetRoles_RoleId` FOREIGN KEY (`RoleId`) REFERENCES `AspNetRoles` (`Id`) ON DELETE CASCADE,
    CONSTRAINT `FK_AspNetUserRoles_AspNetUsers_UserId` FOREIGN KEY (`UserId`) REFERENCES `AspNetUsers` (`Id`) ON DELETE CASCADE
) CHARACTER SET=utf8mb4;

CREATE TABLE `AspNetUserTokens` (
    `UserId` varchar(255) CHARACTER SET utf8mb4 NOT NULL,
    `LoginProvider` varchar(255) CHARACTER SET utf8mb4 NOT NULL,
    `Name` varchar(255) CHARACTER SET utf8mb4 NOT NULL,
    `Value` longtext CHARACTER SET utf8mb4 NULL,
    CONSTRAINT `PK_AspNetUserTokens` PRIMARY KEY (`UserId`, `LoginProvider`, `Name`),
    CONSTRAINT `FK_AspNetUserTokens_AspNetUsers_UserId` FOREIGN KEY (`UserId`) REFERENCES `AspNetUsers` (`Id`) ON DELETE CASCADE
) CHARACTER SET=utf8mb4;

CREATE TABLE `CargoHabilidades` (
    `Id` char(36) COLLATE ascii_general_ci NOT NULL,
    `CargoId` char(36) COLLATE ascii_general_ci NOT NULL,
    `HabilidadId` char(36) COLLATE ascii_general_ci NOT NULL,
    `NivelRequeridoId` char(36) COLLATE ascii_general_ci NOT NULL,
    `Ponderacion` decimal(5,2) NOT NULL,
    `EsObligatoria` tinyint(1) NOT NULL,
    CONSTRAINT `PK_CargoHabilidades` PRIMARY KEY (`Id`),
    CONSTRAINT `CK_CargoHabilidades_Ponderacion` CHECK (`Ponderacion` > 0),
    CONSTRAINT `FK_CargoHabilidades_Cargos_CargoId` FOREIGN KEY (`CargoId`) REFERENCES `Cargos` (`Id`) ON DELETE CASCADE,
    CONSTRAINT `FK_CargoHabilidades_Habilidades_HabilidadId` FOREIGN KEY (`HabilidadId`) REFERENCES `Habilidades` (`Id`) ON DELETE RESTRICT,
    CONSTRAINT `FK_CargoHabilidades_NivelesHabilidad_NivelRequeridoId` FOREIGN KEY (`NivelRequeridoId`) REFERENCES `NivelesHabilidad` (`Id`) ON DELETE RESTRICT
) CHARACTER SET=utf8mb4;

CREATE TABLE `PersonaHabilidades` (
    `Id` char(36) COLLATE ascii_general_ci NOT NULL,
    `PersonaId` char(36) COLLATE ascii_general_ci NOT NULL,
    `HabilidadId` char(36) COLLATE ascii_general_ci NOT NULL,
    `NivelHabilidadId` char(36) COLLATE ascii_general_ci NOT NULL,
    `VerificadoAt` datetime(6) NULL,
    `Fuente` varchar(100) CHARACTER SET utf8mb4 NULL,
    CONSTRAINT `PK_PersonaHabilidades` PRIMARY KEY (`Id`),
    CONSTRAINT `FK_PersonaHabilidades_Habilidades_HabilidadId` FOREIGN KEY (`HabilidadId`) REFERENCES `Habilidades` (`Id`) ON DELETE RESTRICT,
    CONSTRAINT `FK_PersonaHabilidades_NivelesHabilidad_NivelHabilidadId` FOREIGN KEY (`NivelHabilidadId`) REFERENCES `NivelesHabilidad` (`Id`) ON DELETE RESTRICT,
    CONSTRAINT `FK_PersonaHabilidades_Personas_PersonaId` FOREIGN KEY (`PersonaId`) REFERENCES `Personas` (`Id`) ON DELETE CASCADE
) CHARACTER SET=utf8mb4;

CREATE TABLE `Postulantes` (
    `Id` char(36) COLLATE ascii_general_ci NOT NULL,
    `PersonaId` varchar(36) COLLATE ascii_general_ci NULL,
    `Nombres` varchar(100) CHARACTER SET utf8mb4 NULL,
    `Apellidos` varchar(100) CHARACTER SET utf8mb4 NULL,
    `Email` varchar(320) CHARACTER SET utf8mb4 NULL,
    `Telefono` varchar(50) CHARACTER SET utf8mb4 NULL,
    `Fuente` varchar(100) CHARACTER SET utf8mb4 NULL,
    `Observaciones` varchar(1000) CHARACTER SET utf8mb4 NULL,
    `CreatedAt` datetime(6) NOT NULL,
    `CreatedByUserId` varchar(450) CHARACTER SET utf8mb4 NULL,
    `UpdatedAt` datetime(6) NULL,
    `UpdatedByUserId` varchar(450) CHARACTER SET utf8mb4 NULL,
    `IsDeleted` tinyint(1) NOT NULL,
    `DeletedAt` datetime(6) NULL,
    `DeletedByUserId` varchar(450) CHARACTER SET utf8mb4 NULL,
    CONSTRAINT `PK_Postulantes` PRIMARY KEY (`Id`),
    CONSTRAINT `FK_Postulantes_Personas_PersonaId` FOREIGN KEY (`PersonaId`) REFERENCES `Personas` (`Id`) ON DELETE RESTRICT
) CHARACTER SET=utf8mb4;

ALTER TABLE `Postulantes` ADD COLUMN `ActivePersonaIdUnique` varchar(36) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci GENERATED ALWAYS AS (CASE WHEN `PersonaId` IS NOT NULL AND `IsDeleted` = 0 THEN `PersonaId` ELSE NULL END) STORED;

CREATE TABLE `Puestos` (
    `Id` char(36) COLLATE ascii_general_ci NOT NULL,
    `UnidadOrganizativaId` char(36) COLLATE ascii_general_ci NOT NULL,
    `CargoId` char(36) COLLATE ascii_general_ci NOT NULL,
    `PuestoSuperiorId` char(36) COLLATE ascii_general_ci NULL,
    `Codigo` varchar(50) CHARACTER SET utf8mb4 NOT NULL,
    `Nombre` varchar(200) CHARACTER SET utf8mb4 NOT NULL,
    `Descripcion` varchar(1000) CHARACTER SET utf8mb4 NULL,
    `IsActive` tinyint(1) NOT NULL,
    `CreatedAt` datetime(6) NOT NULL,
    `CreatedByUserId` varchar(450) CHARACTER SET utf8mb4 NULL,
    `UpdatedAt` datetime(6) NULL,
    `UpdatedByUserId` varchar(450) CHARACTER SET utf8mb4 NULL,
    `IsDeleted` tinyint(1) NOT NULL,
    `DeletedAt` datetime(6) NULL,
    `DeletedByUserId` varchar(450) CHARACTER SET utf8mb4 NULL,
    CONSTRAINT `PK_Puestos` PRIMARY KEY (`Id`),
    CONSTRAINT `CK_Puestos_PuestoSuperior` CHECK (`PuestoSuperiorId` IS NULL OR `PuestoSuperiorId` <> `Id`),
    CONSTRAINT `FK_Puestos_Cargos_CargoId` FOREIGN KEY (`CargoId`) REFERENCES `Cargos` (`Id`) ON DELETE RESTRICT,
    CONSTRAINT `FK_Puestos_Puestos_PuestoSuperiorId` FOREIGN KEY (`PuestoSuperiorId`) REFERENCES `Puestos` (`Id`) ON DELETE RESTRICT,
    CONSTRAINT `FK_Puestos_UnidadesOrganizativas_UnidadOrganizativaId` FOREIGN KEY (`UnidadOrganizativaId`) REFERENCES `UnidadesOrganizativas` (`Id`) ON DELETE RESTRICT
) CHARACTER SET=utf8mb4;

ALTER TABLE `Puestos` ADD COLUMN `ActiveCodigoUnique` varchar(255) CHARACTER SET utf8mb4 GENERATED ALWAYS AS (CASE WHEN `IsDeleted` = 0 THEN `Codigo` ELSE NULL END) STORED;

CREATE TABLE `Ocupaciones` (
    `Id` char(36) COLLATE ascii_general_ci NOT NULL,
    `PersonaId` varchar(36) COLLATE ascii_general_ci NOT NULL,
    `PuestoId` varchar(36) COLLATE ascii_general_ci NOT NULL,
    `FechaInicio` date NOT NULL,
    `FechaFin` date NULL,
    `TipoAsignacion` varchar(50) CHARACTER SET utf8mb4 NOT NULL,
    `Observaciones` varchar(1000) CHARACTER SET utf8mb4 NULL,
    `CreatedAt` datetime(6) NOT NULL,
    `CreatedByUserId` varchar(450) CHARACTER SET utf8mb4 NULL,
    `UpdatedAt` datetime(6) NULL,
    `UpdatedByUserId` varchar(450) CHARACTER SET utf8mb4 NULL,
    `IsDeleted` tinyint(1) NOT NULL,
    `DeletedAt` datetime(6) NULL,
    `DeletedByUserId` varchar(450) CHARACTER SET utf8mb4 NULL,
    CONSTRAINT `PK_Ocupaciones` PRIMARY KEY (`Id`),
    CONSTRAINT `CK_Ocupaciones_Fechas` CHECK (`FechaFin` IS NULL OR `FechaFin` >= `FechaInicio`),
    CONSTRAINT `FK_Ocupaciones_Personas_PersonaId` FOREIGN KEY (`PersonaId`) REFERENCES `Personas` (`Id`) ON DELETE RESTRICT,
    CONSTRAINT `FK_Ocupaciones_Puestos_PuestoId` FOREIGN KEY (`PuestoId`) REFERENCES `Puestos` (`Id`) ON DELETE RESTRICT
) CHARACTER SET=utf8mb4;

ALTER TABLE `Ocupaciones` ADD COLUMN `ActivePersonaIdUnique` varchar(36) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci GENERATED ALWAYS AS (CASE WHEN `FechaFin` IS NULL AND `IsDeleted` = 0 THEN `PersonaId` ELSE NULL END) STORED;
ALTER TABLE `Ocupaciones` ADD COLUMN `ActivePuestoIdUnique` varchar(36) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci GENERATED ALWAYS AS (CASE WHEN `FechaFin` IS NULL AND `IsDeleted` = 0 THEN `PuestoId` ELSE NULL END) STORED;
ALTER TABLE `Ocupaciones` ADD COLUMN `ActivePersonaPuestoUnique` varchar(100) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci GENERATED ALWAYS AS (CASE WHEN `FechaFin` IS NULL AND `IsDeleted` = 0 THEN CONCAT(`PersonaId`, ':', `PuestoId`) ELSE NULL END) STORED;

CREATE TABLE `Vacantes` (
    `Id` char(36) COLLATE ascii_general_ci NOT NULL,
    `PuestoId` char(36) COLLATE ascii_general_ci NOT NULL,
    `EstadoVacanteId` char(36) COLLATE ascii_general_ci NOT NULL,
    `FechaApertura` datetime(6) NOT NULL,
    `FechaCierre` datetime(6) NULL,
    `Motivo` varchar(500) CHARACTER SET utf8mb4 NOT NULL,
    `Observaciones` varchar(1000) CHARACTER SET utf8mb4 NULL,
    `CreatedAt` datetime(6) NOT NULL,
    `CreatedByUserId` varchar(450) CHARACTER SET utf8mb4 NULL,
    `UpdatedAt` datetime(6) NULL,
    `UpdatedByUserId` varchar(450) CHARACTER SET utf8mb4 NULL,
    `IsDeleted` tinyint(1) NOT NULL,
    `DeletedAt` datetime(6) NULL,
    `DeletedByUserId` varchar(450) CHARACTER SET utf8mb4 NULL,
    CONSTRAINT `PK_Vacantes` PRIMARY KEY (`Id`),
    CONSTRAINT `FK_Vacantes_EstadosVacante_EstadoVacanteId` FOREIGN KEY (`EstadoVacanteId`) REFERENCES `EstadosVacante` (`Id`) ON DELETE RESTRICT,
    CONSTRAINT `FK_Vacantes_Puestos_PuestoId` FOREIGN KEY (`PuestoId`) REFERENCES `Puestos` (`Id`) ON DELETE RESTRICT
) CHARACTER SET=utf8mb4;

CREATE TABLE `HistorialEstadosVacante` (
    `Id` char(36) COLLATE ascii_general_ci NOT NULL,
    `VacanteId` char(36) COLLATE ascii_general_ci NOT NULL,
    `EstadoAnteriorId` char(36) COLLATE ascii_general_ci NULL,
    `EstadoNuevoId` char(36) COLLATE ascii_general_ci NOT NULL,
    `ChangedAt` datetime(6) NOT NULL,
    `ChangedByUserId` varchar(450) CHARACTER SET utf8mb4 NULL,
    `Motivo` varchar(500) CHARACTER SET utf8mb4 NULL,
    CONSTRAINT `PK_HistorialEstadosVacante` PRIMARY KEY (`Id`),
    CONSTRAINT `FK_HistorialEstadosVacante_EstadosVacante_EstadoAnteriorId` FOREIGN KEY (`EstadoAnteriorId`) REFERENCES `EstadosVacante` (`Id`) ON DELETE RESTRICT,
    CONSTRAINT `FK_HistorialEstadosVacante_EstadosVacante_EstadoNuevoId` FOREIGN KEY (`EstadoNuevoId`) REFERENCES `EstadosVacante` (`Id`) ON DELETE RESTRICT,
    CONSTRAINT `FK_HistorialEstadosVacante_Vacantes_VacanteId` FOREIGN KEY (`VacanteId`) REFERENCES `Vacantes` (`Id`) ON DELETE CASCADE
) CHARACTER SET=utf8mb4;

CREATE TABLE `Postulaciones` (
    `Id` char(36) COLLATE ascii_general_ci NOT NULL,
    `VacanteId` char(36) COLLATE ascii_general_ci NOT NULL,
    `PostulanteId` char(36) COLLATE ascii_general_ci NOT NULL,
    `EstadoPostulacionId` char(36) COLLATE ascii_general_ci NOT NULL,
    `FechaPostulacion` datetime(6) NOT NULL,
    `PuntajeCompatibilidad` decimal(5,2) NULL,
    `NivelCompatibilidad` varchar(50) CHARACTER SET utf8mb4 NULL,
    `Observaciones` varchar(1000) CHARACTER SET utf8mb4 NULL,
    `CreatedAt` datetime(6) NOT NULL,
    `CreatedByUserId` varchar(450) CHARACTER SET utf8mb4 NULL,
    `UpdatedAt` datetime(6) NULL,
    `UpdatedByUserId` varchar(450) CHARACTER SET utf8mb4 NULL,
    `IsDeleted` tinyint(1) NOT NULL,
    `DeletedAt` datetime(6) NULL,
    `DeletedByUserId` varchar(450) CHARACTER SET utf8mb4 NULL,
    CONSTRAINT `PK_Postulaciones` PRIMARY KEY (`Id`),
    CONSTRAINT `CK_Postulaciones_PuntajeCompatibilidad` CHECK (`PuntajeCompatibilidad` IS NULL OR (`PuntajeCompatibilidad` >= 0 AND `PuntajeCompatibilidad` <= 100)),
    CONSTRAINT `FK_Postulaciones_EstadosPostulacion_EstadoPostulacionId` FOREIGN KEY (`EstadoPostulacionId`) REFERENCES `EstadosPostulacion` (`Id`) ON DELETE RESTRICT,
    CONSTRAINT `FK_Postulaciones_Postulantes_PostulanteId` FOREIGN KEY (`PostulanteId`) REFERENCES `Postulantes` (`Id`) ON DELETE RESTRICT,
    CONSTRAINT `FK_Postulaciones_Vacantes_VacanteId` FOREIGN KEY (`VacanteId`) REFERENCES `Vacantes` (`Id`) ON DELETE RESTRICT
) CHARACTER SET=utf8mb4;

CREATE TABLE `EvaluacionesPostulacion` (
    `Id` char(36) COLLATE ascii_general_ci NOT NULL,
    `PostulacionId` char(36) COLLATE ascii_general_ci NOT NULL,
    `EvaluadoAt` datetime(6) NOT NULL,
    `EvaluadoByUserId` varchar(450) CHARACTER SET utf8mb4 NULL,
    `PuntajeTecnico` decimal(5,2) NULL,
    `PuntajeEntrevista` decimal(5,2) NULL,
    `PuntajeCompatibilidad` decimal(5,2) NULL,
    `Recomendacion` varchar(50) CHARACTER SET utf8mb4 NULL,
    `Observaciones` varchar(2000) CHARACTER SET utf8mb4 NULL,
    `CreatedAt` datetime(6) NOT NULL,
    `CreatedByUserId` varchar(450) CHARACTER SET utf8mb4 NULL,
    `UpdatedAt` datetime(6) NULL,
    `UpdatedByUserId` varchar(450) CHARACTER SET utf8mb4 NULL,
    `IsDeleted` tinyint(1) NOT NULL,
    `DeletedAt` datetime(6) NULL,
    `DeletedByUserId` varchar(450) CHARACTER SET utf8mb4 NULL,
    CONSTRAINT `PK_EvaluacionesPostulacion` PRIMARY KEY (`Id`),
    CONSTRAINT `CK_EvaluacionesPostulacion_PuntajeCompatibilidad` CHECK (`PuntajeCompatibilidad` IS NULL OR (`PuntajeCompatibilidad` >= 0 AND `PuntajeCompatibilidad` <= 100)),
    CONSTRAINT `CK_EvaluacionesPostulacion_PuntajeEntrevista` CHECK (`PuntajeEntrevista` IS NULL OR (`PuntajeEntrevista` >= 0 AND `PuntajeEntrevista` <= 100)),
    CONSTRAINT `CK_EvaluacionesPostulacion_PuntajeTecnico` CHECK (`PuntajeTecnico` IS NULL OR (`PuntajeTecnico` >= 0 AND `PuntajeTecnico` <= 100)),
    CONSTRAINT `FK_EvaluacionesPostulacion_Postulaciones_PostulacionId` FOREIGN KEY (`PostulacionId`) REFERENCES `Postulaciones` (`Id`) ON DELETE CASCADE
) CHARACTER SET=utf8mb4;

CREATE TABLE `HistorialEstadosPostulacion` (
    `Id` char(36) COLLATE ascii_general_ci NOT NULL,
    `PostulacionId` char(36) COLLATE ascii_general_ci NOT NULL,
    `EstadoAnteriorId` char(36) COLLATE ascii_general_ci NULL,
    `EstadoNuevoId` char(36) COLLATE ascii_general_ci NOT NULL,
    `ChangedAt` datetime(6) NOT NULL,
    `ChangedByUserId` varchar(450) CHARACTER SET utf8mb4 NULL,
    `Observaciones` varchar(1000) CHARACTER SET utf8mb4 NULL,
    CONSTRAINT `PK_HistorialEstadosPostulacion` PRIMARY KEY (`Id`),
    CONSTRAINT `FK_HistorialEstadosPostulacion_EstadosPostulacion_EstadoAnterio~` FOREIGN KEY (`EstadoAnteriorId`) REFERENCES `EstadosPostulacion` (`Id`) ON DELETE RESTRICT,
    CONSTRAINT `FK_HistorialEstadosPostulacion_EstadosPostulacion_EstadoNuevoId` FOREIGN KEY (`EstadoNuevoId`) REFERENCES `EstadosPostulacion` (`Id`) ON DELETE RESTRICT,
    CONSTRAINT `FK_HistorialEstadosPostulacion_Postulaciones_PostulacionId` FOREIGN KEY (`PostulacionId`) REFERENCES `Postulaciones` (`Id`) ON DELETE CASCADE
) CHARACTER SET=utf8mb4;

INSERT INTO `AspNetRoles` (`Id`, `ConcurrencyStamp`, `Name`, `NormalizedName`)
VALUES ('Administrador', 'rol-Administrador', 'Administrador', 'ADMINISTRADOR'),
('EvaluadorSeleccion', 'rol-EvaluadorSeleccion', 'EvaluadorSeleccion', 'EVALUADORSELECCION'),
('GestorOrganizacional', 'rol-GestorOrganizacional', 'GestorOrganizacional', 'GESTORORGANIZACIONAL'),
('Lector', 'rol-Lector', 'Lector', 'LECTOR'),
('RecursosHumanos', 'rol-RecursosHumanos', 'RecursosHumanos', 'RECURSOSHUMANOS');

INSERT INTO `Cargos` (`Id`, `Codigo`, `CreatedAt`, `CreatedByUserId`, `DeletedAt`, `DeletedByUserId`, `Descripcion`, `IsActive`, `IsDeleted`, `Nivel`, `Nombre`, `UpdatedAt`, `UpdatedByUserId`)
VALUES ('40000000-0000-0000-0000-000000000001', 'DECANO', TIMESTAMP '0001-01-01 00:00:00', NULL, NULL, NULL, NULL, TRUE, FALSE, 'Directivo', 'Decano', NULL, NULL),
('40000000-0000-0000-0000-000000000002', 'SECRETARIO', TIMESTAMP '0001-01-01 00:00:00', NULL, NULL, NULL, NULL, TRUE, FALSE, 'Directivo', 'Secretario', NULL, NULL),
('40000000-0000-0000-0000-000000000003', 'DIRECTOR', TIMESTAMP '0001-01-01 00:00:00', NULL, NULL, NULL, NULL, TRUE, FALSE, 'Conducción media', 'Director', NULL, NULL),
('40000000-0000-0000-0000-000000000004', 'JEFE_DEPARTAMENTO', TIMESTAMP '0001-01-01 00:00:00', NULL, NULL, NULL, NULL, TRUE, FALSE, 'Conducción media', 'Jefe de Departamento', NULL, NULL),
('40000000-0000-0000-0000-000000000005', 'ADMINISTRATIVO', TIMESTAMP '0001-01-01 00:00:00', NULL, NULL, NULL, NULL, TRUE, FALSE, 'Operativo', 'Administrativo', NULL, NULL),
('40000000-0000-0000-0000-000000000006', 'PROFESOR', TIMESTAMP '0001-01-01 00:00:00', NULL, NULL, NULL, NULL, TRUE, FALSE, 'Académico', 'Profesor', NULL, NULL);

INSERT INTO `EstadosPostulacion` (`Id`, `Codigo`, `EsTerminal`, `EsTerminalPositivo`, `Nombre`, `Orden`)
VALUES ('30000000-0000-0000-0000-000000000001', 'Postulado', FALSE, FALSE, 'Postulado', 1),
('30000000-0000-0000-0000-000000000002', 'Preseleccionado', FALSE, FALSE, 'Preseleccionado', 2),
('30000000-0000-0000-0000-000000000003', 'Entrevistado', FALSE, FALSE, 'Entrevistado', 3),
('30000000-0000-0000-0000-000000000004', 'Aprobado', FALSE, FALSE, 'Aprobado', 4),
('30000000-0000-0000-0000-000000000005', 'Rechazado', TRUE, FALSE, 'Rechazado', 5),
('30000000-0000-0000-0000-000000000006', 'Contratado', TRUE, TRUE, 'Contratado', 6);

INSERT INTO `EstadosVacante` (`Id`, `Codigo`, `EsTerminal`, `Nombre`, `Orden`)
VALUES ('20000000-0000-0000-0000-000000000001', 'Abierta', FALSE, 'Abierta', 1),
('20000000-0000-0000-0000-000000000002', 'EnSeleccion', FALSE, 'En Selección', 2),
('20000000-0000-0000-0000-000000000003', 'Cubierta', TRUE, 'Cubierta', 3),
('20000000-0000-0000-0000-000000000004', 'Cancelada', TRUE, 'Cancelada', 4);

INSERT INTO `Habilidades` (`Id`, `Categoria`, `Codigo`, `CreatedAt`, `CreatedByUserId`, `DeletedAt`, `DeletedByUserId`, `Descripcion`, `IsActive`, `IsDeleted`, `Nombre`, `UpdatedAt`, `UpdatedByUserId`)
VALUES ('50000000-0000-0000-0000-000000000001', 'Conducción', 'LIDERAZGO', TIMESTAMP '0001-01-01 00:00:00', NULL, NULL, NULL, NULL, TRUE, FALSE, 'Liderazgo', NULL, NULL),
('50000000-0000-0000-0000-000000000002', 'Conducción', 'GESTION_PERSONAL', TIMESTAMP '0001-01-01 00:00:00', NULL, NULL, NULL, NULL, TRUE, FALSE, 'Gestión de Personal', NULL, NULL),
('50000000-0000-0000-0000-000000000003', 'Técnica', 'SQL_SERVER', TIMESTAMP '0001-01-01 00:00:00', NULL, NULL, NULL, NULL, TRUE, FALSE, 'SQL Server', NULL, NULL),
('50000000-0000-0000-0000-000000000004', 'Técnica', 'EF_CORE', TIMESTAMP '0001-01-01 00:00:00', NULL, NULL, NULL, NULL, TRUE, FALSE, 'Entity Framework Core', NULL, NULL),
('50000000-0000-0000-0000-000000000005', 'Técnica', 'DOTNET', TIMESTAMP '0001-01-01 00:00:00', NULL, NULL, NULL, NULL, TRUE, FALSE, 'Programación .NET', NULL, NULL),
('50000000-0000-0000-0000-000000000006', 'Dominio', 'ADMINISTRACION_PUBLICA', TIMESTAMP '0001-01-01 00:00:00', NULL, NULL, NULL, NULL, TRUE, FALSE, 'Administración Pública', NULL, NULL),
('50000000-0000-0000-0000-000000000007', 'Académica', 'DOCENCIA_UNIVERSITARIA', TIMESTAMP '0001-01-01 00:00:00', NULL, NULL, NULL, NULL, TRUE, FALSE, 'Docencia Universitaria', NULL, NULL);

INSERT INTO `NivelesHabilidad` (`Id`, `Codigo`, `Nombre`, `Orden`, `ValorNumerico`)
VALUES ('10000000-0000-0000-0000-000000000001', 'Basico', 'Básico', 1, 1),
('10000000-0000-0000-0000-000000000002', 'Intermedio', 'Intermedio', 2, 2),
('10000000-0000-0000-0000-000000000003', 'Avanzado', 'Avanzado', 3, 3),
('10000000-0000-0000-0000-000000000004', 'Experto', 'Experto', 4, 4);

CREATE INDEX `IX_AspNetRoleClaims_RoleId` ON `AspNetRoleClaims` (`RoleId`);

CREATE UNIQUE INDEX `RoleNameIndex` ON `AspNetRoles` (`NormalizedName`);

CREATE INDEX `IX_AspNetUserClaims_UserId` ON `AspNetUserClaims` (`UserId`);

CREATE INDEX `IX_AspNetUserLogins_UserId` ON `AspNetUserLogins` (`UserId`);

CREATE INDEX `IX_AspNetUserRoles_RoleId` ON `AspNetUserRoles` (`RoleId`);

CREATE INDEX `EmailIndex` ON `AspNetUsers` (`NormalizedEmail`);

CREATE UNIQUE INDEX `UserNameIndex` ON `AspNetUsers` (`NormalizedUserName`);

CREATE INDEX `IX_Auditorias_CorrelationId` ON `Auditorias` (`CorrelationId`);

CREATE INDEX `IX_Auditorias_EntityName_EntityId_OccurredAt` ON `Auditorias` (`EntityName`, `EntityId`, `OccurredAt`);

CREATE INDEX `IX_Auditorias_UserId_OccurredAt` ON `Auditorias` (`UserId`, `OccurredAt`);

CREATE UNIQUE INDEX `IX_CargoHabilidades_CargoId_HabilidadId` ON `CargoHabilidades` (`CargoId`, `HabilidadId`);

CREATE INDEX `IX_CargoHabilidades_HabilidadId` ON `CargoHabilidades` (`HabilidadId`);

CREATE INDEX `IX_CargoHabilidades_NivelRequeridoId` ON `CargoHabilidades` (`NivelRequeridoId`);

CREATE UNIQUE INDEX `IX_Cargos_ActiveCodigoUnique` ON `Cargos` (`ActiveCodigoUnique`);

CREATE INDEX `IX_Cargos_IsDeleted` ON `Cargos` (`IsDeleted`);

CREATE INDEX `IX_Cargos_Nombre` ON `Cargos` (`Nombre`);

CREATE UNIQUE INDEX `IX_EstadosPostulacion_Codigo` ON `EstadosPostulacion` (`Codigo`);

CREATE UNIQUE INDEX `IX_EstadosVacante_Codigo` ON `EstadosVacante` (`Codigo`);

CREATE INDEX `IX_EvaluacionesPostulacion_EvaluadoAt` ON `EvaluacionesPostulacion` (`EvaluadoAt`);

CREATE INDEX `IX_EvaluacionesPostulacion_IsDeleted` ON `EvaluacionesPostulacion` (`IsDeleted`);

CREATE INDEX `IX_EvaluacionesPostulacion_PostulacionId` ON `EvaluacionesPostulacion` (`PostulacionId`);

CREATE UNIQUE INDEX `IX_Habilidades_ActiveCodigoUnique` ON `Habilidades` (`ActiveCodigoUnique`);

CREATE INDEX `IX_Habilidades_Categoria` ON `Habilidades` (`Categoria`);

CREATE INDEX `IX_Habilidades_IsDeleted` ON `Habilidades` (`IsDeleted`);

CREATE INDEX `IX_HistorialEstadosPostulacion_EstadoAnteriorId` ON `HistorialEstadosPostulacion` (`EstadoAnteriorId`);

CREATE INDEX `IX_HistorialEstadosPostulacion_EstadoNuevoId` ON `HistorialEstadosPostulacion` (`EstadoNuevoId`);

CREATE INDEX `IX_HistorialEstadosPostulacion_PostulacionId_ChangedAt` ON `HistorialEstadosPostulacion` (`PostulacionId`, `ChangedAt`);

CREATE INDEX `IX_HistorialEstadosVacante_EstadoAnteriorId` ON `HistorialEstadosVacante` (`EstadoAnteriorId`);

CREATE INDEX `IX_HistorialEstadosVacante_EstadoNuevoId` ON `HistorialEstadosVacante` (`EstadoNuevoId`);

CREATE INDEX `IX_HistorialEstadosVacante_VacanteId_ChangedAt` ON `HistorialEstadosVacante` (`VacanteId`, `ChangedAt`);

CREATE UNIQUE INDEX `IX_NivelesHabilidad_Codigo` ON `NivelesHabilidad` (`Codigo`);

CREATE UNIQUE INDEX `IX_NivelesHabilidad_ValorNumerico` ON `NivelesHabilidad` (`ValorNumerico`);

CREATE UNIQUE INDEX `IX_Ocupaciones_ActivePersonaIdUnique` ON `Ocupaciones` (`ActivePersonaIdUnique`);

CREATE UNIQUE INDEX `IX_Ocupaciones_ActivePuestoIdUnique` ON `Ocupaciones` (`ActivePuestoIdUnique`);

CREATE INDEX `IX_Ocupaciones_IsDeleted` ON `Ocupaciones` (`IsDeleted`);

CREATE INDEX `IX_Ocupaciones_PersonaId_FechaInicio_FechaFin` ON `Ocupaciones` (`PersonaId`, `FechaInicio`, `FechaFin`);

CREATE INDEX `IX_Ocupaciones_PuestoId_FechaInicio_FechaFin` ON `Ocupaciones` (`PuestoId`, `FechaInicio`, `FechaFin`);

CREATE INDEX `IX_PersonaHabilidades_HabilidadId` ON `PersonaHabilidades` (`HabilidadId`);

CREATE INDEX `IX_PersonaHabilidades_NivelHabilidadId` ON `PersonaHabilidades` (`NivelHabilidadId`);

CREATE UNIQUE INDEX `IX_PersonaHabilidades_PersonaId_HabilidadId` ON `PersonaHabilidades` (`PersonaId`, `HabilidadId`);


CREATE UNIQUE INDEX `IX_Personas_ActiveEmailUnique` ON `Personas` (`ActiveEmailUnique`);

CREATE UNIQUE INDEX `IX_Personas_ActiveLegajoUnique` ON `Personas` (`ActiveLegajoUnique`);

CREATE INDEX `IX_Personas_Apellidos_Nombres` ON `Personas` (`Apellidos`, `Nombres`);

CREATE INDEX `IX_Personas_IsDeleted` ON `Personas` (`IsDeleted`);

CREATE INDEX `IX_Postulaciones_EstadoPostulacionId` ON `Postulaciones` (`EstadoPostulacionId`);

CREATE INDEX `IX_Postulaciones_IsDeleted` ON `Postulaciones` (`IsDeleted`);

CREATE INDEX `IX_Postulaciones_PostulanteId` ON `Postulaciones` (`PostulanteId`);

CREATE INDEX `IX_Postulaciones_VacanteId_EstadoPostulacionId` ON `Postulaciones` (`VacanteId`, `EstadoPostulacionId`);

CREATE UNIQUE INDEX `IX_Postulaciones_VacanteId_PostulanteId` ON `Postulaciones` (`VacanteId`, `PostulanteId`);

CREATE UNIQUE INDEX `IX_Postulantes_ActivePersonaIdUnique` ON `Postulantes` (`ActivePersonaIdUnique`);

CREATE INDEX `IX_Postulantes_Apellidos_Nombres` ON `Postulantes` (`Apellidos`, `Nombres`);

CREATE INDEX `IX_Postulantes_Email` ON `Postulantes` (`Email`);

CREATE INDEX `IX_Postulantes_IsDeleted` ON `Postulantes` (`IsDeleted`);

CREATE INDEX `IX_Postulantes_PersonaId` ON `Postulantes` (`PersonaId`);

CREATE UNIQUE INDEX `IX_Puestos_ActiveCodigoUnique` ON `Puestos` (`ActiveCodigoUnique`);

CREATE INDEX `IX_Puestos_CargoId` ON `Puestos` (`CargoId`);

CREATE INDEX `IX_Puestos_IsDeleted` ON `Puestos` (`IsDeleted`);

CREATE INDEX `IX_Puestos_PuestoSuperiorId` ON `Puestos` (`PuestoSuperiorId`);

CREATE INDEX `IX_Puestos_UnidadOrganizativaId` ON `Puestos` (`UnidadOrganizativaId`);

CREATE UNIQUE INDEX `IX_UnidadesOrganizativas_ActiveCodigoUnique` ON `UnidadesOrganizativas` (`ActiveCodigoUnique`);

CREATE INDEX `IX_UnidadesOrganizativas_IsDeleted` ON `UnidadesOrganizativas` (`IsDeleted`);

CREATE INDEX `IX_UnidadesOrganizativas_Nombre` ON `UnidadesOrganizativas` (`Nombre`);

CREATE INDEX `IX_UnidadesOrganizativas_UnidadPadreId` ON `UnidadesOrganizativas` (`UnidadPadreId`);

CREATE INDEX `IX_Vacantes_EstadoVacanteId` ON `Vacantes` (`EstadoVacanteId`);

CREATE INDEX `IX_Vacantes_EstadoVacanteId_FechaApertura` ON `Vacantes` (`EstadoVacanteId`, `FechaApertura`);

CREATE INDEX `IX_Vacantes_FechaApertura` ON `Vacantes` (`FechaApertura`);

CREATE INDEX `IX_Vacantes_IsDeleted` ON `Vacantes` (`IsDeleted`);

CREATE INDEX `IX_Vacantes_PuestoId` ON `Vacantes` (`PuestoId`);

INSERT INTO `__EFMigrationsHistory` (`MigrationId`, `ProductVersion`)
VALUES ('20260614183103_InicialSgvo', '9.0.0');

INSERT INTO `__EFMigrationsHistory` (`MigrationId`, `ProductVersion`)
VALUES ('20260614183109_AgregarDatosSemillaBase', '9.0.0');

CREATE TABLE `TiposUnidadOrganizativa` (
    `Id` char(36) COLLATE ascii_general_ci NOT NULL,
    `Codigo` varchar(50) CHARACTER SET utf8mb4 NOT NULL,
    `Nombre` varchar(100) CHARACTER SET utf8mb4 NOT NULL,
    CONSTRAINT `PK_TiposUnidadOrganizativa` PRIMARY KEY (`Id`),
    CONSTRAINT `CK_TiposUnidadOrganizativa_Codigo` CHECK (`Codigo` <> '')
) CHARACTER SET=utf8mb4;

CREATE UNIQUE INDEX `IX_TiposUnidadOrganizativa_Codigo` ON `TiposUnidadOrganizativa` (`Codigo`);

INSERT INTO `TiposUnidadOrganizativa` (`Id`, `Codigo`, `Nombre`)
VALUES ('60000000-0000-0000-0000-000000000001', 'Institucion', 'Institución'),
('60000000-0000-0000-0000-000000000002', 'Facultad', 'Facultad'),
('60000000-0000-0000-0000-000000000003', 'Secretaria', 'Secretaría'),
('60000000-0000-0000-0000-000000000004', 'Direccion', 'Dirección'),
('60000000-0000-0000-0000-000000000005', 'Departamento', 'Departamento'),
('60000000-0000-0000-0000-000000000006', 'Division', 'División'),
('60000000-0000-0000-0000-000000000007', 'Area', 'Área');


                CREATE TEMPORARY TABLE _DirtyTiposUnidad AS
                SELECT DISTINCT TipoUnidad
                FROM UnidadesOrganizativas
                WHERE TipoUnidad NOT IN (
                    'Institucion', 'Facultad', 'Secretaria', 'Direccion',
                    'Departamento', 'Division', 'Area'
                );
            


                SET @has_dirty = (SELECT COUNT(*) FROM _DirtyTiposUnidad);
                SET @msg = (
                    SELECT COALESCE(
                        (SELECT GROUP_CONCAT(TipoUnidad SEPARATOR ', ')
                         FROM (SELECT TipoUnidad FROM _DirtyTiposUnidad LIMIT 5) AS d),
                        'ninguno')
                );
                SET @signal = CONCAT(
                    'SIGNAL SQLSTATE \'45000\' SET MESSAGE_TEXT = \'Backfill fail-loud: ',
                    @has_dirty, ' valores de TipoUnidad sin catalogar. Ejemplos: ',
                    @msg, '\''
                );
                SET @noop = 'SELECT 1 AS ok';
                SET @sql = IF(@has_dirty > 0, @signal, @noop);
                PREPARE stmt FROM @sql;
                EXECUTE stmt;
                DEALLOCATE PREPARE stmt;
            

DROP TEMPORARY TABLE IF EXISTS _DirtyTiposUnidad;

ALTER TABLE `UnidadesOrganizativas` ADD `TipoUnidadOrganizativaId` char(36) COLLATE ascii_general_ci NULL;

CREATE INDEX `IX_UnidadesOrganizativas_TipoUnidadOrganizativaId` ON `UnidadesOrganizativas` (`TipoUnidadOrganizativaId`);


                UPDATE UnidadesOrganizativas u
                INNER JOIN TiposUnidadOrganizativa t ON t.Codigo = u.TipoUnidad
                SET u.TipoUnidadOrganizativaId = t.Id;
            


                ALTER TABLE `UnidadesOrganizativas`
                MODIFY COLUMN `TipoUnidadOrganizativaId` char(36) NOT NULL
                COLLATE ascii_general_ci;
            

ALTER TABLE `UnidadesOrganizativas` ADD CONSTRAINT `FK_UnidadesOrganizativas_TiposUnidadOrganizativa_TipoUnidadOrgan` FOREIGN KEY (`TipoUnidadOrganizativaId`) REFERENCES `TiposUnidadOrganizativa` (`Id`) ON DELETE RESTRICT;

ALTER TABLE `UnidadesOrganizativas` DROP COLUMN `TipoUnidad`;

INSERT INTO `__EFMigrationsHistory` (`MigrationId`, `ProductVersion`)
VALUES ('20260616190624_CambiarTipoUnidadATablaTipoUnidadOrganizativa', '9.0.0');


                CREATE TEMPORARY TABLE IF NOT EXISTS _DirtyNivelesCargo AS
                SELECT DISTINCT Nivel
                FROM Cargos
                WHERE Nivel IS NOT NULL
                  AND Nivel NOT IN ('Directivo', 'Conducción media', 'Operativo', 'Académico');

                SET @dirtyCount = (SELECT COUNT(*) FROM _DirtyNivelesCargo);
                SET @dirtyExamples = (
                    SELECT COALESCE(GROUP_CONCAT(Nivel SEPARATOR ', '), 'ninguno')
                    FROM (SELECT Nivel FROM _DirtyNivelesCargo LIMIT 5) AS d
                );

                SET @msg = CONCAT(
                    'Backfill fail-loud: ', @dirtyCount,
                    ' valores de Nivel sin catalogar. Ejemplos: ', @dirtyExamples
                );

                SET @sql = IF(@dirtyCount > 0,
                    CONCAT('SIGNAL SQLSTATE \'45000\' SET MESSAGE_TEXT = ''', @msg, ''''),
                    'SELECT 1');
                PREPARE stmt FROM @sql;
                EXECUTE stmt;
                DEALLOCATE PREPARE stmt;

                DROP TEMPORARY TABLE IF EXISTS _DirtyNivelesCargo;
            

CREATE TABLE `NivelesCargo` (
    `Id` char(36) COLLATE ascii_general_ci NOT NULL,
    `Codigo` varchar(50) COLLATE ascii_general_ci NOT NULL,
    `Nombre` varchar(100) CHARACTER SET utf8mb4 NOT NULL,
    `ValorNumerico` tinyint unsigned NOT NULL,
    `Orden` int NOT NULL,
    CONSTRAINT `PK_NivelesCargo` PRIMARY KEY (`Id`),
    CONSTRAINT `CK_NivelesCargo_ValorNumerico` CHECK (`ValorNumerico` >= 0 AND `ValorNumerico` <= 255)
) CHARACTER SET=utf8mb4;

INSERT INTO `NivelesCargo` (`Id`, `Codigo`, `Nombre`, `Orden`, `ValorNumerico`)
VALUES ('70000000-0000-0000-0000-000000000001', 'Directivo', 'Directivo', 1, 1),
('70000000-0000-0000-0000-000000000002', 'ConduccionMedia', 'Conducción Media', 2, 2),
('70000000-0000-0000-0000-000000000003', 'Operativo', 'Operativo', 3, 3),
('70000000-0000-0000-0000-000000000004', 'Academico', 'Académico', 4, 4);

ALTER TABLE `Cargos` ADD `NivelId` char(36) COLLATE ascii_general_ci NULL;


                UPDATE Cargos c
                INNER JOIN NivelesCargo nc ON
                    (c.Nivel = 'Directivo'        AND nc.Codigo = 'Directivo')
                    OR (c.Nivel = 'Conducción media' AND nc.Codigo = 'ConduccionMedia')
                    OR (c.Nivel = 'Operativo'        AND nc.Codigo = 'Operativo')
                    OR (c.Nivel = 'Académico'        AND nc.Codigo = 'Academico')
                SET c.NivelId = nc.Id
                WHERE c.Nivel IS NOT NULL;
            

UPDATE `Cargos` SET `NivelId` = '70000000-0000-0000-0000-000000000001'
WHERE `Id` = '40000000-0000-0000-0000-000000000001';
SELECT ROW_COUNT();


UPDATE `Cargos` SET `NivelId` = '70000000-0000-0000-0000-000000000001'
WHERE `Id` = '40000000-0000-0000-0000-000000000002';
SELECT ROW_COUNT();


UPDATE `Cargos` SET `NivelId` = '70000000-0000-0000-0000-000000000002'
WHERE `Id` = '40000000-0000-0000-0000-000000000003';
SELECT ROW_COUNT();


UPDATE `Cargos` SET `NivelId` = '70000000-0000-0000-0000-000000000002'
WHERE `Id` = '40000000-0000-0000-0000-000000000004';
SELECT ROW_COUNT();


UPDATE `Cargos` SET `NivelId` = '70000000-0000-0000-0000-000000000003'
WHERE `Id` = '40000000-0000-0000-0000-000000000005';
SELECT ROW_COUNT();


UPDATE `Cargos` SET `NivelId` = '70000000-0000-0000-0000-000000000004'
WHERE `Id` = '40000000-0000-0000-0000-000000000006';
SELECT ROW_COUNT();


ALTER TABLE `Cargos` MODIFY COLUMN `NivelId` char(36) COLLATE ascii_general_ci NOT NULL;

CREATE INDEX `IX_Cargos_NivelId` ON `Cargos` (`NivelId`);

CREATE UNIQUE INDEX `IX_NivelesCargo_Codigo` ON `NivelesCargo` (`Codigo`);

ALTER TABLE `Cargos` ADD CONSTRAINT `FK_Cargos_NivelesCargo_NivelId` FOREIGN KEY (`NivelId`) REFERENCES `NivelesCargo` (`Id`) ON DELETE RESTRICT;

ALTER TABLE `Cargos` DROP COLUMN `Nivel`;

INSERT INTO `__EFMigrationsHistory` (`MigrationId`, `ProductVersion`)
VALUES ('20260618180508_CambiarNivelStringANivelId', '9.0.0');


                SET @existingUsers = (SELECT COUNT(*) FROM AspNetUsers);
                SET @userMsg = CONCAT(
                    'Backfill fail-loud: AspNetUsers contains ', @existingUsers,
                    ' existing users. Populate PersonaId explicitly before applying this migration.'
                );
                SET @userSql = IF(@existingUsers > 0,
                    CONCAT('SIGNAL SQLSTATE \'45000\' SET MESSAGE_TEXT = ''', @userMsg, ''''),
                    'SELECT 1');
                PREPARE stmt FROM @userSql;
                EXECUTE stmt;
                DEALLOCATE PREPARE stmt;

                SET @legacyAssignments = (
                    SELECT COUNT(*)
                    FROM AspNetUserRoles ur
                    INNER JOIN AspNetRoles r ON r.Id = ur.RoleId
                    WHERE r.Id IN ('RecursosHumanos', 'GestorOrganizacional', 'EvaluadorSeleccion', 'Lector')
                );
                SET @roleMsg = CONCAT(
                    'Backfill fail-loud: ', @legacyAssignments,
                    ' assignments use legacy roles. Reassign users to Administrador, GestorVacantes, or Consultor before applying this migration.'
                );
                SET @roleSql = IF(@legacyAssignments > 0,
                    CONCAT('SIGNAL SQLSTATE \'45000\' SET MESSAGE_TEXT = ''', @roleMsg, ''''),
                    'SELECT 1');
                PREPARE stmt FROM @roleSql;
                EXECUTE stmt;
                DEALLOCATE PREPARE stmt;
            

DELETE FROM `AspNetRoles`
WHERE `Id` = 'EvaluadorSeleccion';
SELECT ROW_COUNT();


DELETE FROM `AspNetRoles`
WHERE `Id` = 'GestorOrganizacional';
SELECT ROW_COUNT();


DELETE FROM `AspNetRoles`
WHERE `Id` = 'Lector';
SELECT ROW_COUNT();


DELETE FROM `AspNetRoles`
WHERE `Id` = 'RecursosHumanos';
SELECT ROW_COUNT();


ALTER TABLE `AspNetUsers` ADD `PersonaId` char(36) COLLATE ascii_general_ci NOT NULL DEFAULT '00000000-0000-0000-0000-000000000000';

INSERT INTO `AspNetRoles` (`Id`, `ConcurrencyStamp`, `Name`, `NormalizedName`)
VALUES ('Consultor', 'rol-Consultor', 'Consultor', 'CONSULTOR'),
('GestorVacantes', 'rol-GestorVacantes', 'GestorVacantes', 'GESTORVACANTES');

CREATE UNIQUE INDEX `IX_AspNetUsers_PersonaId` ON `AspNetUsers` (`PersonaId`);

ALTER TABLE `AspNetUsers` ADD CONSTRAINT `FK_AspNetUsers_Personas_PersonaId` FOREIGN KEY (`PersonaId`) REFERENCES `Personas` (`Id`) ON DELETE RESTRICT;

INSERT INTO `__EFMigrationsHistory` (`MigrationId`, `ProductVersion`)
VALUES ('20260621202540_VincularIdentityUsuariosAPersonas', '9.0.0');

UPDATE `Ocupaciones` SET `TipoAsignacion` = '0' WHERE `TipoAsignacion` = 'Permanente';

UPDATE `Ocupaciones` SET `TipoAsignacion` = '1' WHERE `TipoAsignacion` = 'Interina';

UPDATE `Ocupaciones` SET `TipoAsignacion` = '2' WHERE `TipoAsignacion` = 'Temporal';

ALTER TABLE `Ocupaciones` MODIFY COLUMN `TipoAsignacion` int NOT NULL;


CREATE UNIQUE INDEX `IX_Ocupaciones_ActivePersonaPuestoUnique` ON `Ocupaciones` (`ActivePersonaPuestoUnique`);

INSERT INTO `__EFMigrationsHistory` (`MigrationId`, `ProductVersion`)
VALUES ('20260624153353_ConvertirTipoAsignacionAEnumYActualizarUnicidad', '9.0.0');


INSERT INTO `__EFMigrationsHistory` (`MigrationId`, `ProductVersion`)
VALUES ('20260711181615_FixActivePuestoIdUniqueType', '9.0.0');

ALTER TABLE `AspNetUsers`
  ADD COLUMN `IsDeleted` TINYINT(1) NOT NULL DEFAULT 0,
  ALGORITHM=INPLACE,
  LOCK=NONE;



ALTER TABLE `AspNetUsers`
  DROP FOREIGN KEY `FK_AspNetUsers_Personas_PersonaId`,
  ALGORITHM=INPLACE,
  LOCK=NONE;

ALTER TABLE `AspNetUsers`
  DROP INDEX `IX_AspNetUsers_PersonaId`,
  ALGORITHM=INPLACE,
  LOCK=NONE;

ALTER TABLE `AspNetUsers`
  ADD INDEX `IX_AspNetUsers_PersonaId` (`PersonaId`),
  ALGORITHM=INPLACE,
  LOCK=NONE;

ALTER TABLE `AspNetUsers`
  ADD CONSTRAINT `FK_AspNetUsers_Personas_PersonaId`
  FOREIGN KEY (`PersonaId`) REFERENCES `Personas` (`Id`)
  ON DELETE RESTRICT,
  ALGORITHM=COPY;



INSERT INTO `__EFMigrationsHistory` (`MigrationId`, `ProductVersion`)
VALUES ('20260715145121_AddSoftDeleteToAspNetUsers', '9.0.0');

DROP PROCEDURE IF EXISTS __sgvApplyD7;

DELIMITER $$
CREATE PROCEDURE __sgvApplyD7()
BEGIN
    DECLARE _needsD7 INT;
    SET _needsD7 = (
        SELECT COUNT(*) FROM information_schema.COLUMNS
        WHERE table_schema = DATABASE()
          AND table_name = 'AspNetUsers'
          AND column_name = 'IsDeleted'
    );
    IF _needsD7 > 0 THEN
        -- Paso 1: preflight fail-loud
        SET @duplicatePersonas = (
            SELECT COUNT(*) FROM (
                SELECT `PersonaId` FROM `AspNetUsers`
                GROUP BY `PersonaId` HAVING COUNT(*) > 1
            ) AS dupes
        );
        SET @preflightMsg = CONCAT(
            'Backfill fail-loud: ', @duplicatePersonas,
            ' PersonaId duplicados entre AspNetUsers activas. ',
            'Resolver duplicados manualmente antes de aplicar esta migración.'
        );
        IF @duplicatePersonas > 0 THEN
            SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = @preflightMsg;
        END IF;

        -- Paso 2: backfill
        UPDATE `AspNetUsers`
        SET `LockoutEnabled` = 1,
            `LockoutEnd` = '9999-12-31 23:59:59.999999'
        WHERE `IsDeleted` = 1;

        -- Paso 3: DROP FK
        ALTER TABLE `AspNetUsers`
          DROP FOREIGN KEY `FK_AspNetUsers_Personas_PersonaId`,
          ALGORITHM=INPLACE, LOCK=NONE;

        -- Paso 4: DROP INDEX ActiveUserNameUnique
        
        -- Paso 5: DROP INDEX ActivePersonaIdUnique
        
        -- Paso 6: DROP COLUMNs
        ALTER TABLE `AspNetUsers`
          DROP COLUMN `IsDeleted`,
          ALGORITHM=INPLACE, LOCK=NONE;

        -- Paso 7: DROP INDEX PersonaId
        ALTER TABLE `AspNetUsers`
          DROP INDEX `IX_AspNetUsers_PersonaId`,
          ALGORITHM=INPLACE, LOCK=NONE;

        -- Paso 8: ADD UNIQUE INDEX PersonaId
        ALTER TABLE `AspNetUsers`
          ADD UNIQUE INDEX `IX_AspNetUsers_PersonaId` (`PersonaId`),
          ALGORITHM=INPLACE, LOCK=NONE;

        -- Paso 9: ADD CONSTRAINT FK
        ALTER TABLE `AspNetUsers`
          ADD CONSTRAINT `FK_AspNetUsers_Personas_PersonaId`
          FOREIGN KEY (`PersonaId`) REFERENCES `Personas` (`Id`)
          ON DELETE RESTRICT,
          ALGORITHM=COPY;
    END IF;
END$$

DELIMITER ;
CALL __sgvApplyD7();
DROP PROCEDURE __sgvApplyD7;

INSERT INTO `__EFMigrationsHistory` (`MigrationId`, `ProductVersion`)
VALUES ('20260716120000_DropSoftDeleteFromAspNetUsers', '9.0.0');

CREATE INDEX `IX_Personas_NumeroDocumento` ON `Personas` (`NumeroDocumento`);

INSERT INTO `__EFMigrationsHistory` (`MigrationId`, `ProductVersion`)
VALUES ('20260719180541_AddPersonasNumeroDocumentoIndex', '9.0.0');

CREATE TABLE `TiposDocumento` (
    `Id` char(36) COLLATE ascii_general_ci NOT NULL,
    `Codigo` varchar(50) COLLATE ascii_general_ci NOT NULL,
    `Nombre` varchar(100) CHARACTER SET utf8mb4 NOT NULL,
    `PatronValidacion` varchar(255) CHARACTER SET utf8mb4 NULL,
    `LongitudMinima` int NULL,
    `LongitudMaxima` int NULL,
    CONSTRAINT `PK_TiposDocumento` PRIMARY KEY (`Id`),
    CONSTRAINT `CK_TiposDocumento_Codigo` CHECK (`Codigo` <> ''),
    CONSTRAINT `CK_TiposDocumento_Longitudes` CHECK (`LongitudMinima` IS NULL OR `LongitudMaxima` IS NULL OR `LongitudMinima` <= `LongitudMaxima`)
) CHARACTER SET=utf8mb4;

INSERT INTO `TiposDocumento` (`Id`, `Codigo`, `Nombre`, `PatronValidacion`, `LongitudMinima`, `LongitudMaxima`)
VALUES ('71000000-0000-0000-0000-000000000001', 'DNI', 'Documento Nacional de Identidad', '^\\d{7,8}$', 7, 8),
('71000000-0000-0000-0000-000000000002', 'LE', 'Libreta de Enrolamiento', '^\\d{6,8}$', 6, 8),
('71000000-0000-0000-0000-000000000003', 'LC', 'Libreta Cívica', '^\\d{6,8}$', 6, 8),
('71000000-0000-0000-0000-000000000004', 'Pasaporte', 'Pasaporte', '^[A-Za-z]{3}\\d{6}$', 9, 9);

CREATE UNIQUE INDEX `IX_TiposDocumento_Codigo` ON `TiposDocumento` (`Codigo`);


                CREATE TEMPORARY TABLE IF NOT EXISTS _DirtyTipoDocumento AS
                SELECT DISTINCT p.TipoDocumento
                FROM Personas p
                WHERE p.TipoDocumento IS NOT NULL
                  AND p.TipoDocumento NOT IN ('DNI', 'LE', 'LC', 'Pasaporte');

                SET @dirtyCount = (SELECT COUNT(*) FROM _DirtyTipoDocumento);
                SET @dirtyExamples = (
                    SELECT COALESCE(GROUP_CONCAT(DISTINCT TipoDocumento SEPARATOR ', '), 'ninguno')
                    FROM (SELECT TipoDocumento FROM _DirtyTipoDocumento LIMIT 5) AS d
                );

                -- Log diagnóstico (no aborta). Se puede inspeccionar vía
                -- SHOW ENGINE INNODB STATUS o capturando el output del
                -- EF migration.
                SELECT CONCAT('Backfill opt-in relajado: ', @dirtyCount,
                              ' valores de TipoDocumento sin catalogar. Ejemplos: ',
                              @dirtyExamples) AS _backfill_diagnostics;

                DROP TEMPORARY TABLE IF EXISTS _DirtyTipoDocumento;
            

ALTER TABLE `Personas` ADD `TipoDocumentoId` varchar(36) COLLATE ascii_general_ci NULL;

CREATE INDEX `IX_Personas_TipoDocumentoId` ON `Personas` (`TipoDocumentoId`);


                UPDATE Personas p
                INNER JOIN TiposDocumento t ON t.Codigo = p.TipoDocumento
                SET p.TipoDocumentoId = t.Id
                WHERE p.TipoDocumento IS NOT NULL;
            

ALTER TABLE `Personas` ADD COLUMN `ActiveDocumentoUnique` varchar(255) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci GENERATED ALWAYS AS (CASE WHEN `TipoDocumentoId` IS NOT NULL AND `NumeroDocumento` IS NOT NULL AND `IsDeleted` = 0 THEN CONCAT(CAST(`TipoDocumentoId` AS CHAR CHARACTER SET utf8mb4), ':', `NumeroDocumento`) ELSE NULL END) STORED;

CREATE UNIQUE INDEX `IX_Personas_ActiveDocumentoUnique` ON `Personas` (`ActiveDocumentoUnique`);

ALTER TABLE `Personas` ADD CONSTRAINT `FK_Personas_TiposDocumento_TipoDocumentoId` FOREIGN KEY (`TipoDocumentoId`) REFERENCES `TiposDocumento` (`Id`) ON DELETE RESTRICT;

ALTER TABLE `Personas` DROP COLUMN `TipoDocumento`;

INSERT INTO `__EFMigrationsHistory` (`MigrationId`, `ProductVersion`)
VALUES ('20260720230343_TipoDocumentoCatalogoYPersonaFk', '9.0.0');

CREATE TABLE `CategoriasHabilidad` (
    `Id` char(36) COLLATE ascii_general_ci NOT NULL,
    `Codigo` varchar(50) COLLATE ascii_general_ci NOT NULL,
    `Nombre` varchar(100) CHARACTER SET utf8mb4 NOT NULL,
    CONSTRAINT `PK_CategoriasHabilidad` PRIMARY KEY (`Id`),
    CONSTRAINT `CK_CategoriasHabilidad_Codigo` CHECK (`Codigo` <> '')
) CHARACTER SET=utf8mb4;

INSERT INTO `CategoriasHabilidad` (`Id`, `Codigo`, `Nombre`)
VALUES ('72000000-0000-0000-0000-000000000000', 'Conduccion', 'Conducción'),
('72000000-0000-0000-0000-000000000001', 'Tecnica', 'Técnica'),
('72000000-0000-0000-0000-000000000002', 'Dominio', 'Dominio'),
('72000000-0000-0000-0000-000000000003', 'Academica', 'Académica');

CREATE UNIQUE INDEX `IX_CategoriasHabilidad_Codigo` ON `CategoriasHabilidad` (`Codigo`);


                CREATE TEMPORARY TABLE IF NOT EXISTS _DirtyCategoriaHabilidad AS
                SELECT DISTINCT h.Categoria
                FROM Habilidades h
                WHERE h.Categoria IS NOT NULL
                  AND LOWER(h.Categoria) NOT IN (
                      SELECT LOWER(Nombre) FROM CategoriasHabilidad
                  );

                SET @dirtyCount = (SELECT COUNT(*) FROM _DirtyCategoriaHabilidad);
                SET @dirtyExamples = (
                    SELECT COALESCE(GROUP_CONCAT(DISTINCT Categoria SEPARATOR ', '), 'ninguno')
                    FROM (SELECT Categoria FROM _DirtyCategoriaHabilidad LIMIT 5) AS d
                );

                -- Log diagnóstico (no aborta). Se puede inspeccionar vía
                -- SHOW ENGINE INNODB STATUS o capturando el output del
                -- EF migration.
                SELECT CONCAT('Backfill opt-in relajado: ', @dirtyCount,
                              ' valores de Categoria sin catalogar. Ejemplos: ',
                              @dirtyExamples) AS _backfill_diagnostics;

                DROP TEMPORARY TABLE IF EXISTS _DirtyCategoriaHabilidad;
            

ALTER TABLE `Habilidades` ADD `CategoriaId` char(36) COLLATE ascii_general_ci NULL;

CREATE INDEX `IX_Habilidades_CategoriaId` ON `Habilidades` (`CategoriaId`);


                UPDATE Habilidades h
                INNER JOIN CategoriasHabilidad c ON LOWER(c.Nombre) = LOWER(h.Categoria)
                SET h.CategoriaId = c.Id
                WHERE h.Categoria IS NOT NULL;
            


                INSERT INTO Auditorias (Id, UserId, OccurredAt, EntityName, EntityId, Operation, NewValuesJson)
                SELECT
                    UUID(),
                    NULL,
                    UTC_TIMESTAMP(6),
                    'Habilidad',
                    h.Id,
                    'BackfillLegacyCategoriaToNull',
                    JSON_OBJECT(
                        'Origen', 'Migracion.AddCategoriaHabilidadCatalog',
                        'CategoriaOriginal', h.Categoria
                    )
                FROM Habilidades h
                WHERE h.Categoria IS NOT NULL AND h.CategoriaId IS NULL;
            

UPDATE `Habilidades` SET `CategoriaId` = '72000000-0000-0000-0000-000000000000'
WHERE `Id` = '50000000-0000-0000-0000-000000000001';
SELECT ROW_COUNT();


UPDATE `Habilidades` SET `CategoriaId` = '72000000-0000-0000-0000-000000000000'
WHERE `Id` = '50000000-0000-0000-0000-000000000002';
SELECT ROW_COUNT();


UPDATE `Habilidades` SET `CategoriaId` = '72000000-0000-0000-0000-000000000001'
WHERE `Id` = '50000000-0000-0000-0000-000000000003';
SELECT ROW_COUNT();


UPDATE `Habilidades` SET `CategoriaId` = '72000000-0000-0000-0000-000000000001'
WHERE `Id` = '50000000-0000-0000-0000-000000000004';
SELECT ROW_COUNT();


UPDATE `Habilidades` SET `CategoriaId` = '72000000-0000-0000-0000-000000000001'
WHERE `Id` = '50000000-0000-0000-0000-000000000005';
SELECT ROW_COUNT();


UPDATE `Habilidades` SET `CategoriaId` = '72000000-0000-0000-0000-000000000002'
WHERE `Id` = '50000000-0000-0000-0000-000000000006';
SELECT ROW_COUNT();


UPDATE `Habilidades` SET `CategoriaId` = '72000000-0000-0000-0000-000000000003'
WHERE `Id` = '50000000-0000-0000-0000-000000000007';
SELECT ROW_COUNT();


ALTER TABLE `Habilidades` ADD CONSTRAINT `FK_Habilidades_CategoriasHabilidad_CategoriaId` FOREIGN KEY (`CategoriaId`) REFERENCES `CategoriasHabilidad` (`Id`) ON DELETE RESTRICT;

ALTER TABLE `Habilidades` DROP INDEX `IX_Habilidades_Categoria`;

ALTER TABLE `Habilidades` DROP COLUMN `Categoria`;

INSERT INTO `__EFMigrationsHistory` (`MigrationId`, `ProductVersion`)
VALUES ('20260723203015_AddCategoriaHabilidadCatalog', '9.0.0');





















INSERT INTO `__EFMigrationsHistory` (`MigrationId`, `ProductVersion`)
VALUES ('20260729145632_MariaDbStoredColumnsAndCollation', '9.0.0');

-- ============================================================================
-- Migración: 20260731173842_AddActivePuestoIdUniqueToVacantes
-- Forward-only (precedente FixActivePuestoIdUniqueType).
-- Issue #238 — TOCTOU de CrearAsync de vacantes (issue paralelo al patrón
-- ya vigente en Ocupaciones desde la migración MariaDbStoredColumnsAndCollation).
-- Patrón: columna calculada STORED `ActivePuestoIdUnique` + UNIQUE INDEX
-- parcial sobre la misma. La columna evalúa a NULL cuando la vacante está
-- cerrada o soft-deleted — MariaDB ignora NULL en UNIQUE INDEX, permitiendo
-- múltiples filas con la constraint satisfecha.
--
-- Source-of-truth C#: src/SGV.Infraestructura/Persistencia/
--   Migraciones/20260731173842_AddActivePuestoIdUniqueToVacantes.cs
-- Configuración EF: src/SGV.Infraestructura/Persistencia/Configuraciones/
--   VacanteConfiguracion.cs (shadow property "ActivePuestoIdUnique").
--
-- Notas MariaDB:
--   * STORED es obligatorio para UNIQUE INDEX sobre columna generada
--     (mismo motivo que MariaDbStoredColumnsAndCollation).
--   * Collation `ascii_general_ci` matches el snapshot de modelo EF y la
--     FixActivePuestoIdUniqueType previa para Ocupaciones. PuestoId es
--     Guid de 36 chars → solo ASCII 0-9 a-f '-'; utf8mb4_unicode_ci
--     funciona pero es más caro en comparaciones.
-- ============================================================================
ALTER TABLE `Vacantes` ADD COLUMN `ActivePuestoIdUnique` varchar(36) COLLATE ascii_general_ci GENERATED ALWAYS AS (CASE WHEN `FechaCierre` IS NULL AND `IsDeleted` = 0 THEN `PuestoId` ELSE NULL END) STORED;

CREATE UNIQUE INDEX `IX_Vacantes_ActivePuestoIdUnique` ON `Vacantes` (`ActivePuestoIdUnique`);

INSERT INTO `__EFMigrationsHistory` (`MigrationId`, `ProductVersion`)
VALUES ('20260731173842_AddActivePuestoIdUniqueToVacantes', '9.0.0');

COMMIT;

