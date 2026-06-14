CREATE TABLE IF NOT EXISTS `__EFMigrationsHistory` (
    `MigrationId` varchar(150) CHARACTER SET utf8mb4 NOT NULL,
    `ProductVersion` varchar(32) CHARACTER SET utf8mb4 NOT NULL,
    CONSTRAINT `PK___EFMigrationsHistory` PRIMARY KEY (`MigrationId`)
) CHARACTER SET=utf8mb4;

START TRANSACTION;
DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260614183103_InicialSgvo') THEN

    ALTER DATABASE CHARACTER SET utf8mb4;

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260614183103_InicialSgvo') THEN

    CREATE TABLE `AspNetRoles` (
        `Id` varchar(255) CHARACTER SET utf8mb4 NOT NULL,
        `Name` varchar(256) CHARACTER SET utf8mb4 NULL,
        `NormalizedName` varchar(256) CHARACTER SET utf8mb4 NULL,
        `ConcurrencyStamp` longtext CHARACTER SET utf8mb4 NULL,
        CONSTRAINT `PK_AspNetRoles` PRIMARY KEY (`Id`)
    ) CHARACTER SET=utf8mb4;

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260614183103_InicialSgvo') THEN

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

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260614183103_InicialSgvo') THEN

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

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260614183103_InicialSgvo') THEN

    CREATE TABLE `Cargos` (
        `Id` char(36) COLLATE ascii_general_ci NOT NULL,
        `Codigo` varchar(50) CHARACTER SET utf8mb4 NOT NULL,
        `Nombre` varchar(200) CHARACTER SET utf8mb4 NOT NULL,
        `Descripcion` varchar(1000) CHARACTER SET utf8mb4 NULL,
        `Nivel` varchar(50) CHARACTER SET utf8mb4 NULL,
        `IsActive` tinyint(1) NOT NULL,
        `ActiveCodigoUnique` varchar(255) CHARACTER SET utf8mb4 AS (CASE WHEN `IsDeleted` = 0 THEN `Codigo` ELSE NULL END) NULL,
        `CreatedAt` datetime(6) NOT NULL,
        `CreatedByUserId` varchar(450) CHARACTER SET utf8mb4 NULL,
        `UpdatedAt` datetime(6) NULL,
        `UpdatedByUserId` varchar(450) CHARACTER SET utf8mb4 NULL,
        `IsDeleted` tinyint(1) NOT NULL,
        `DeletedAt` datetime(6) NULL,
        `DeletedByUserId` varchar(450) CHARACTER SET utf8mb4 NULL,
        CONSTRAINT `PK_Cargos` PRIMARY KEY (`Id`)
    ) CHARACTER SET=utf8mb4;

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260614183103_InicialSgvo') THEN

    CREATE TABLE `EstadosPostulacion` (
        `Id` char(36) COLLATE ascii_general_ci NOT NULL,
        `Codigo` varchar(50) CHARACTER SET utf8mb4 NOT NULL,
        `Nombre` varchar(100) CHARACTER SET utf8mb4 NOT NULL,
        `Orden` int NOT NULL,
        `EsTerminal` tinyint(1) NOT NULL,
        `EsTerminalPositivo` tinyint(1) NOT NULL,
        CONSTRAINT `PK_EstadosPostulacion` PRIMARY KEY (`Id`)
    ) CHARACTER SET=utf8mb4;

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260614183103_InicialSgvo') THEN

    CREATE TABLE `EstadosVacante` (
        `Id` char(36) COLLATE ascii_general_ci NOT NULL,
        `Codigo` varchar(50) CHARACTER SET utf8mb4 NOT NULL,
        `Nombre` varchar(100) CHARACTER SET utf8mb4 NOT NULL,
        `Orden` int NOT NULL,
        `EsTerminal` tinyint(1) NOT NULL,
        CONSTRAINT `PK_EstadosVacante` PRIMARY KEY (`Id`)
    ) CHARACTER SET=utf8mb4;

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260614183103_InicialSgvo') THEN

    CREATE TABLE `Habilidades` (
        `Id` char(36) COLLATE ascii_general_ci NOT NULL,
        `Codigo` varchar(50) CHARACTER SET utf8mb4 NOT NULL,
        `Nombre` varchar(200) CHARACTER SET utf8mb4 NOT NULL,
        `Descripcion` varchar(1000) CHARACTER SET utf8mb4 NULL,
        `Categoria` varchar(100) CHARACTER SET utf8mb4 NULL,
        `IsActive` tinyint(1) NOT NULL,
        `ActiveCodigoUnique` varchar(255) CHARACTER SET utf8mb4 AS (CASE WHEN `IsDeleted` = 0 THEN `Codigo` ELSE NULL END) NULL,
        `CreatedAt` datetime(6) NOT NULL,
        `CreatedByUserId` varchar(450) CHARACTER SET utf8mb4 NULL,
        `UpdatedAt` datetime(6) NULL,
        `UpdatedByUserId` varchar(450) CHARACTER SET utf8mb4 NULL,
        `IsDeleted` tinyint(1) NOT NULL,
        `DeletedAt` datetime(6) NULL,
        `DeletedByUserId` varchar(450) CHARACTER SET utf8mb4 NULL,
        CONSTRAINT `PK_Habilidades` PRIMARY KEY (`Id`)
    ) CHARACTER SET=utf8mb4;

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260614183103_InicialSgvo') THEN

    CREATE TABLE `NivelesHabilidad` (
        `Id` char(36) COLLATE ascii_general_ci NOT NULL,
        `Codigo` varchar(50) CHARACTER SET utf8mb4 NOT NULL,
        `Nombre` varchar(100) CHARACTER SET utf8mb4 NOT NULL,
        `ValorNumerico` tinyint unsigned NOT NULL,
        `Orden` int NOT NULL,
        CONSTRAINT `PK_NivelesHabilidad` PRIMARY KEY (`Id`),
        CONSTRAINT `CK_NivelesHabilidad_ValorNumerico` CHECK (`ValorNumerico` BETWEEN 1 AND 4)
    ) CHARACTER SET=utf8mb4;

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260614183103_InicialSgvo') THEN

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
        `ActiveDocumentoUnique` varchar(255) CHARACTER SET utf8mb4 AS (CASE WHEN `TipoDocumento` IS NOT NULL AND `NumeroDocumento` IS NOT NULL AND `IsDeleted` = 0 THEN CONCAT(`TipoDocumento`, ':', `NumeroDocumento`) ELSE NULL END) NULL,
        `ActiveEmailUnique` varchar(255) CHARACTER SET utf8mb4 AS (CASE WHEN `Email` IS NOT NULL AND `IsDeleted` = 0 THEN `Email` ELSE NULL END) NULL,
        `ActiveLegajoUnique` varchar(255) CHARACTER SET utf8mb4 AS (CASE WHEN `Legajo` IS NOT NULL AND `IsDeleted` = 0 THEN `Legajo` ELSE NULL END) NULL,
        `CreatedAt` datetime(6) NOT NULL,
        `CreatedByUserId` varchar(450) CHARACTER SET utf8mb4 NULL,
        `UpdatedAt` datetime(6) NULL,
        `UpdatedByUserId` varchar(450) CHARACTER SET utf8mb4 NULL,
        `IsDeleted` tinyint(1) NOT NULL,
        `DeletedAt` datetime(6) NULL,
        `DeletedByUserId` varchar(450) CHARACTER SET utf8mb4 NULL,
        CONSTRAINT `PK_Personas` PRIMARY KEY (`Id`)
    ) CHARACTER SET=utf8mb4;

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260614183103_InicialSgvo') THEN

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
        `ActiveCodigoUnique` varchar(255) CHARACTER SET utf8mb4 AS (CASE WHEN `IsDeleted` = 0 THEN `Codigo` ELSE NULL END) NULL,
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

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260614183103_InicialSgvo') THEN

    CREATE TABLE `AspNetRoleClaims` (
        `Id` int NOT NULL AUTO_INCREMENT,
        `RoleId` varchar(255) CHARACTER SET utf8mb4 NOT NULL,
        `ClaimType` longtext CHARACTER SET utf8mb4 NULL,
        `ClaimValue` longtext CHARACTER SET utf8mb4 NULL,
        CONSTRAINT `PK_AspNetRoleClaims` PRIMARY KEY (`Id`),
        CONSTRAINT `FK_AspNetRoleClaims_AspNetRoles_RoleId` FOREIGN KEY (`RoleId`) REFERENCES `AspNetRoles` (`Id`) ON DELETE CASCADE
    ) CHARACTER SET=utf8mb4;

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260614183103_InicialSgvo') THEN

    CREATE TABLE `AspNetUserClaims` (
        `Id` int NOT NULL AUTO_INCREMENT,
        `UserId` varchar(255) CHARACTER SET utf8mb4 NOT NULL,
        `ClaimType` longtext CHARACTER SET utf8mb4 NULL,
        `ClaimValue` longtext CHARACTER SET utf8mb4 NULL,
        CONSTRAINT `PK_AspNetUserClaims` PRIMARY KEY (`Id`),
        CONSTRAINT `FK_AspNetUserClaims_AspNetUsers_UserId` FOREIGN KEY (`UserId`) REFERENCES `AspNetUsers` (`Id`) ON DELETE CASCADE
    ) CHARACTER SET=utf8mb4;

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260614183103_InicialSgvo') THEN

    CREATE TABLE `AspNetUserLogins` (
        `LoginProvider` varchar(255) CHARACTER SET utf8mb4 NOT NULL,
        `ProviderKey` varchar(255) CHARACTER SET utf8mb4 NOT NULL,
        `ProviderDisplayName` longtext CHARACTER SET utf8mb4 NULL,
        `UserId` varchar(255) CHARACTER SET utf8mb4 NOT NULL,
        CONSTRAINT `PK_AspNetUserLogins` PRIMARY KEY (`LoginProvider`, `ProviderKey`),
        CONSTRAINT `FK_AspNetUserLogins_AspNetUsers_UserId` FOREIGN KEY (`UserId`) REFERENCES `AspNetUsers` (`Id`) ON DELETE CASCADE
    ) CHARACTER SET=utf8mb4;

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260614183103_InicialSgvo') THEN

    CREATE TABLE `AspNetUserRoles` (
        `UserId` varchar(255) CHARACTER SET utf8mb4 NOT NULL,
        `RoleId` varchar(255) CHARACTER SET utf8mb4 NOT NULL,
        CONSTRAINT `PK_AspNetUserRoles` PRIMARY KEY (`UserId`, `RoleId`),
        CONSTRAINT `FK_AspNetUserRoles_AspNetRoles_RoleId` FOREIGN KEY (`RoleId`) REFERENCES `AspNetRoles` (`Id`) ON DELETE CASCADE,
        CONSTRAINT `FK_AspNetUserRoles_AspNetUsers_UserId` FOREIGN KEY (`UserId`) REFERENCES `AspNetUsers` (`Id`) ON DELETE CASCADE
    ) CHARACTER SET=utf8mb4;

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260614183103_InicialSgvo') THEN

    CREATE TABLE `AspNetUserTokens` (
        `UserId` varchar(255) CHARACTER SET utf8mb4 NOT NULL,
        `LoginProvider` varchar(255) CHARACTER SET utf8mb4 NOT NULL,
        `Name` varchar(255) CHARACTER SET utf8mb4 NOT NULL,
        `Value` longtext CHARACTER SET utf8mb4 NULL,
        CONSTRAINT `PK_AspNetUserTokens` PRIMARY KEY (`UserId`, `LoginProvider`, `Name`),
        CONSTRAINT `FK_AspNetUserTokens_AspNetUsers_UserId` FOREIGN KEY (`UserId`) REFERENCES `AspNetUsers` (`Id`) ON DELETE CASCADE
    ) CHARACTER SET=utf8mb4;

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260614183103_InicialSgvo') THEN

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

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260614183103_InicialSgvo') THEN

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

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260614183103_InicialSgvo') THEN

    CREATE TABLE `Postulantes` (
        `Id` char(36) COLLATE ascii_general_ci NOT NULL,
        `PersonaId` char(36) COLLATE ascii_general_ci NULL,
        `Nombres` varchar(100) CHARACTER SET utf8mb4 NULL,
        `Apellidos` varchar(100) CHARACTER SET utf8mb4 NULL,
        `Email` varchar(320) CHARACTER SET utf8mb4 NULL,
        `Telefono` varchar(50) CHARACTER SET utf8mb4 NULL,
        `Fuente` varchar(100) CHARACTER SET utf8mb4 NULL,
        `Observaciones` varchar(1000) CHARACTER SET utf8mb4 NULL,
        `ActivePersonaIdUnique` char(36) COLLATE ascii_general_ci AS (CASE WHEN `PersonaId` IS NOT NULL AND `IsDeleted` = 0 THEN `PersonaId` ELSE NULL END) NULL,
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

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260614183103_InicialSgvo') THEN

    CREATE TABLE `Puestos` (
        `Id` char(36) COLLATE ascii_general_ci NOT NULL,
        `UnidadOrganizativaId` char(36) COLLATE ascii_general_ci NOT NULL,
        `CargoId` char(36) COLLATE ascii_general_ci NOT NULL,
        `PuestoSuperiorId` char(36) COLLATE ascii_general_ci NULL,
        `Codigo` varchar(50) CHARACTER SET utf8mb4 NOT NULL,
        `Nombre` varchar(200) CHARACTER SET utf8mb4 NOT NULL,
        `Descripcion` varchar(1000) CHARACTER SET utf8mb4 NULL,
        `IsActive` tinyint(1) NOT NULL,
        `ActiveCodigoUnique` varchar(255) CHARACTER SET utf8mb4 AS (CASE WHEN `IsDeleted` = 0 THEN `Codigo` ELSE NULL END) NULL,
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

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260614183103_InicialSgvo') THEN

    CREATE TABLE `Ocupaciones` (
        `Id` char(36) COLLATE ascii_general_ci NOT NULL,
        `PersonaId` char(36) COLLATE ascii_general_ci NOT NULL,
        `PuestoId` char(36) COLLATE ascii_general_ci NOT NULL,
        `FechaInicio` date NOT NULL,
        `FechaFin` date NULL,
        `TipoAsignacion` varchar(50) CHARACTER SET utf8mb4 NOT NULL,
        `Observaciones` varchar(1000) CHARACTER SET utf8mb4 NULL,
        `ActivePersonaIdUnique` int AS (CASE WHEN `FechaFin` IS NULL AND `IsDeleted` = 0 THEN `PersonaId` ELSE NULL END) NULL,
        `ActivePuestoIdUnique` int AS (CASE WHEN `FechaFin` IS NULL AND `IsDeleted` = 0 THEN `PuestoId` ELSE NULL END) NULL,
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

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260614183103_InicialSgvo') THEN

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

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260614183103_InicialSgvo') THEN

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

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260614183103_InicialSgvo') THEN

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

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260614183103_InicialSgvo') THEN

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

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260614183103_InicialSgvo') THEN

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

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260614183103_InicialSgvo') THEN

    INSERT INTO `AspNetRoles` (`Id`, `ConcurrencyStamp`, `Name`, `NormalizedName`)
    VALUES ('Administrador', 'rol-Administrador', 'Administrador', 'ADMINISTRADOR'),
    ('EvaluadorSeleccion', 'rol-EvaluadorSeleccion', 'EvaluadorSeleccion', 'EVALUADORSELECCION'),
    ('GestorOrganizacional', 'rol-GestorOrganizacional', 'GestorOrganizacional', 'GESTORORGANIZACIONAL'),
    ('Lector', 'rol-Lector', 'Lector', 'LECTOR'),
    ('RecursosHumanos', 'rol-RecursosHumanos', 'RecursosHumanos', 'RECURSOSHUMANOS');

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260614183103_InicialSgvo') THEN

    INSERT INTO `Cargos` (`Id`, `Codigo`, `CreatedAt`, `CreatedByUserId`, `DeletedAt`, `DeletedByUserId`, `Descripcion`, `IsActive`, `IsDeleted`, `Nivel`, `Nombre`, `UpdatedAt`, `UpdatedByUserId`)
    VALUES ('40000000-0000-0000-0000-000000000001', 'DECANO', TIMESTAMP '0001-01-01 00:00:00', NULL, NULL, NULL, NULL, TRUE, FALSE, 'Directivo', 'Decano', NULL, NULL),
    ('40000000-0000-0000-0000-000000000002', 'SECRETARIO', TIMESTAMP '0001-01-01 00:00:00', NULL, NULL, NULL, NULL, TRUE, FALSE, 'Directivo', 'Secretario', NULL, NULL),
    ('40000000-0000-0000-0000-000000000003', 'DIRECTOR', TIMESTAMP '0001-01-01 00:00:00', NULL, NULL, NULL, NULL, TRUE, FALSE, 'Conducción media', 'Director', NULL, NULL),
    ('40000000-0000-0000-0000-000000000004', 'JEFE_DEPARTAMENTO', TIMESTAMP '0001-01-01 00:00:00', NULL, NULL, NULL, NULL, TRUE, FALSE, 'Conducción media', 'Jefe de Departamento', NULL, NULL),
    ('40000000-0000-0000-0000-000000000005', 'ADMINISTRATIVO', TIMESTAMP '0001-01-01 00:00:00', NULL, NULL, NULL, NULL, TRUE, FALSE, 'Operativo', 'Administrativo', NULL, NULL),
    ('40000000-0000-0000-0000-000000000006', 'PROFESOR', TIMESTAMP '0001-01-01 00:00:00', NULL, NULL, NULL, NULL, TRUE, FALSE, 'Académico', 'Profesor', NULL, NULL);

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260614183103_InicialSgvo') THEN

    INSERT INTO `EstadosPostulacion` (`Id`, `Codigo`, `EsTerminal`, `EsTerminalPositivo`, `Nombre`, `Orden`)
    VALUES ('30000000-0000-0000-0000-000000000001', 'Postulado', FALSE, FALSE, 'Postulado', 1),
    ('30000000-0000-0000-0000-000000000002', 'Preseleccionado', FALSE, FALSE, 'Preseleccionado', 2),
    ('30000000-0000-0000-0000-000000000003', 'Entrevistado', FALSE, FALSE, 'Entrevistado', 3),
    ('30000000-0000-0000-0000-000000000004', 'Aprobado', FALSE, FALSE, 'Aprobado', 4),
    ('30000000-0000-0000-0000-000000000005', 'Rechazado', TRUE, FALSE, 'Rechazado', 5),
    ('30000000-0000-0000-0000-000000000006', 'Contratado', TRUE, TRUE, 'Contratado', 6);

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260614183103_InicialSgvo') THEN

    INSERT INTO `EstadosVacante` (`Id`, `Codigo`, `EsTerminal`, `Nombre`, `Orden`)
    VALUES ('20000000-0000-0000-0000-000000000001', 'Abierta', FALSE, 'Abierta', 1),
    ('20000000-0000-0000-0000-000000000002', 'EnSeleccion', FALSE, 'En Selección', 2),
    ('20000000-0000-0000-0000-000000000003', 'Cubierta', TRUE, 'Cubierta', 3),
    ('20000000-0000-0000-0000-000000000004', 'Cancelada', TRUE, 'Cancelada', 4);

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260614183103_InicialSgvo') THEN

    INSERT INTO `Habilidades` (`Id`, `Categoria`, `Codigo`, `CreatedAt`, `CreatedByUserId`, `DeletedAt`, `DeletedByUserId`, `Descripcion`, `IsActive`, `IsDeleted`, `Nombre`, `UpdatedAt`, `UpdatedByUserId`)
    VALUES ('50000000-0000-0000-0000-000000000001', 'Conducción', 'LIDERAZGO', TIMESTAMP '0001-01-01 00:00:00', NULL, NULL, NULL, NULL, TRUE, FALSE, 'Liderazgo', NULL, NULL),
    ('50000000-0000-0000-0000-000000000002', 'Conducción', 'GESTION_PERSONAL', TIMESTAMP '0001-01-01 00:00:00', NULL, NULL, NULL, NULL, TRUE, FALSE, 'Gestión de Personal', NULL, NULL),
    ('50000000-0000-0000-0000-000000000003', 'Técnica', 'SQL_SERVER', TIMESTAMP '0001-01-01 00:00:00', NULL, NULL, NULL, NULL, TRUE, FALSE, 'SQL Server', NULL, NULL),
    ('50000000-0000-0000-0000-000000000004', 'Técnica', 'EF_CORE', TIMESTAMP '0001-01-01 00:00:00', NULL, NULL, NULL, NULL, TRUE, FALSE, 'Entity Framework Core', NULL, NULL),
    ('50000000-0000-0000-0000-000000000005', 'Técnica', 'DOTNET', TIMESTAMP '0001-01-01 00:00:00', NULL, NULL, NULL, NULL, TRUE, FALSE, 'Programación .NET', NULL, NULL),
    ('50000000-0000-0000-0000-000000000006', 'Dominio', 'ADMINISTRACION_PUBLICA', TIMESTAMP '0001-01-01 00:00:00', NULL, NULL, NULL, NULL, TRUE, FALSE, 'Administración Pública', NULL, NULL),
    ('50000000-0000-0000-0000-000000000007', 'Académica', 'DOCENCIA_UNIVERSITARIA', TIMESTAMP '0001-01-01 00:00:00', NULL, NULL, NULL, NULL, TRUE, FALSE, 'Docencia Universitaria', NULL, NULL);

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260614183103_InicialSgvo') THEN

    INSERT INTO `NivelesHabilidad` (`Id`, `Codigo`, `Nombre`, `Orden`, `ValorNumerico`)
    VALUES ('10000000-0000-0000-0000-000000000001', 'Basico', 'Básico', 1, 1),
    ('10000000-0000-0000-0000-000000000002', 'Intermedio', 'Intermedio', 2, 2),
    ('10000000-0000-0000-0000-000000000003', 'Avanzado', 'Avanzado', 3, 3),
    ('10000000-0000-0000-0000-000000000004', 'Experto', 'Experto', 4, 4);

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260614183103_InicialSgvo') THEN

    CREATE INDEX `IX_AspNetRoleClaims_RoleId` ON `AspNetRoleClaims` (`RoleId`);

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260614183103_InicialSgvo') THEN

    CREATE UNIQUE INDEX `RoleNameIndex` ON `AspNetRoles` (`NormalizedName`);

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260614183103_InicialSgvo') THEN

    CREATE INDEX `IX_AspNetUserClaims_UserId` ON `AspNetUserClaims` (`UserId`);

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260614183103_InicialSgvo') THEN

    CREATE INDEX `IX_AspNetUserLogins_UserId` ON `AspNetUserLogins` (`UserId`);

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260614183103_InicialSgvo') THEN

    CREATE INDEX `IX_AspNetUserRoles_RoleId` ON `AspNetUserRoles` (`RoleId`);

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260614183103_InicialSgvo') THEN

    CREATE INDEX `EmailIndex` ON `AspNetUsers` (`NormalizedEmail`);

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260614183103_InicialSgvo') THEN

    CREATE UNIQUE INDEX `UserNameIndex` ON `AspNetUsers` (`NormalizedUserName`);

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260614183103_InicialSgvo') THEN

    CREATE INDEX `IX_Auditorias_CorrelationId` ON `Auditorias` (`CorrelationId`);

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260614183103_InicialSgvo') THEN

    CREATE INDEX `IX_Auditorias_EntityName_EntityId_OccurredAt` ON `Auditorias` (`EntityName`, `EntityId`, `OccurredAt`);

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260614183103_InicialSgvo') THEN

    CREATE INDEX `IX_Auditorias_UserId_OccurredAt` ON `Auditorias` (`UserId`, `OccurredAt`);

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260614183103_InicialSgvo') THEN

    CREATE UNIQUE INDEX `IX_CargoHabilidades_CargoId_HabilidadId` ON `CargoHabilidades` (`CargoId`, `HabilidadId`);

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260614183103_InicialSgvo') THEN

    CREATE INDEX `IX_CargoHabilidades_HabilidadId` ON `CargoHabilidades` (`HabilidadId`);

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260614183103_InicialSgvo') THEN

    CREATE INDEX `IX_CargoHabilidades_NivelRequeridoId` ON `CargoHabilidades` (`NivelRequeridoId`);

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260614183103_InicialSgvo') THEN

    CREATE UNIQUE INDEX `IX_Cargos_ActiveCodigoUnique` ON `Cargos` (`ActiveCodigoUnique`);

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260614183103_InicialSgvo') THEN

    CREATE INDEX `IX_Cargos_IsDeleted` ON `Cargos` (`IsDeleted`);

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260614183103_InicialSgvo') THEN

    CREATE INDEX `IX_Cargos_Nombre` ON `Cargos` (`Nombre`);

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260614183103_InicialSgvo') THEN

    CREATE UNIQUE INDEX `IX_EstadosPostulacion_Codigo` ON `EstadosPostulacion` (`Codigo`);

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260614183103_InicialSgvo') THEN

    CREATE UNIQUE INDEX `IX_EstadosVacante_Codigo` ON `EstadosVacante` (`Codigo`);

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260614183103_InicialSgvo') THEN

    CREATE INDEX `IX_EvaluacionesPostulacion_EvaluadoAt` ON `EvaluacionesPostulacion` (`EvaluadoAt`);

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260614183103_InicialSgvo') THEN

    CREATE INDEX `IX_EvaluacionesPostulacion_IsDeleted` ON `EvaluacionesPostulacion` (`IsDeleted`);

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260614183103_InicialSgvo') THEN

    CREATE INDEX `IX_EvaluacionesPostulacion_PostulacionId` ON `EvaluacionesPostulacion` (`PostulacionId`);

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260614183103_InicialSgvo') THEN

    CREATE UNIQUE INDEX `IX_Habilidades_ActiveCodigoUnique` ON `Habilidades` (`ActiveCodigoUnique`);

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260614183103_InicialSgvo') THEN

    CREATE INDEX `IX_Habilidades_Categoria` ON `Habilidades` (`Categoria`);

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260614183103_InicialSgvo') THEN

    CREATE INDEX `IX_Habilidades_IsDeleted` ON `Habilidades` (`IsDeleted`);

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260614183103_InicialSgvo') THEN

    CREATE INDEX `IX_HistorialEstadosPostulacion_EstadoAnteriorId` ON `HistorialEstadosPostulacion` (`EstadoAnteriorId`);

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260614183103_InicialSgvo') THEN

    CREATE INDEX `IX_HistorialEstadosPostulacion_EstadoNuevoId` ON `HistorialEstadosPostulacion` (`EstadoNuevoId`);

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260614183103_InicialSgvo') THEN

    CREATE INDEX `IX_HistorialEstadosPostulacion_PostulacionId_ChangedAt` ON `HistorialEstadosPostulacion` (`PostulacionId`, `ChangedAt`);

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260614183103_InicialSgvo') THEN

    CREATE INDEX `IX_HistorialEstadosVacante_EstadoAnteriorId` ON `HistorialEstadosVacante` (`EstadoAnteriorId`);

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260614183103_InicialSgvo') THEN

    CREATE INDEX `IX_HistorialEstadosVacante_EstadoNuevoId` ON `HistorialEstadosVacante` (`EstadoNuevoId`);

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260614183103_InicialSgvo') THEN

    CREATE INDEX `IX_HistorialEstadosVacante_VacanteId_ChangedAt` ON `HistorialEstadosVacante` (`VacanteId`, `ChangedAt`);

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260614183103_InicialSgvo') THEN

    CREATE UNIQUE INDEX `IX_NivelesHabilidad_Codigo` ON `NivelesHabilidad` (`Codigo`);

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260614183103_InicialSgvo') THEN

    CREATE UNIQUE INDEX `IX_NivelesHabilidad_ValorNumerico` ON `NivelesHabilidad` (`ValorNumerico`);

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260614183103_InicialSgvo') THEN

    CREATE UNIQUE INDEX `IX_Ocupaciones_ActivePersonaIdUnique` ON `Ocupaciones` (`ActivePersonaIdUnique`);

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260614183103_InicialSgvo') THEN

    CREATE UNIQUE INDEX `IX_Ocupaciones_ActivePuestoIdUnique` ON `Ocupaciones` (`ActivePuestoIdUnique`);

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260614183103_InicialSgvo') THEN

    CREATE INDEX `IX_Ocupaciones_IsDeleted` ON `Ocupaciones` (`IsDeleted`);

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260614183103_InicialSgvo') THEN

    CREATE INDEX `IX_Ocupaciones_PersonaId_FechaInicio_FechaFin` ON `Ocupaciones` (`PersonaId`, `FechaInicio`, `FechaFin`);

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260614183103_InicialSgvo') THEN

    CREATE INDEX `IX_Ocupaciones_PuestoId_FechaInicio_FechaFin` ON `Ocupaciones` (`PuestoId`, `FechaInicio`, `FechaFin`);

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260614183103_InicialSgvo') THEN

    CREATE INDEX `IX_PersonaHabilidades_HabilidadId` ON `PersonaHabilidades` (`HabilidadId`);

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260614183103_InicialSgvo') THEN

    CREATE INDEX `IX_PersonaHabilidades_NivelHabilidadId` ON `PersonaHabilidades` (`NivelHabilidadId`);

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260614183103_InicialSgvo') THEN

    CREATE UNIQUE INDEX `IX_PersonaHabilidades_PersonaId_HabilidadId` ON `PersonaHabilidades` (`PersonaId`, `HabilidadId`);

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260614183103_InicialSgvo') THEN

    CREATE UNIQUE INDEX `IX_Personas_ActiveDocumentoUnique` ON `Personas` (`ActiveDocumentoUnique`);

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260614183103_InicialSgvo') THEN

    CREATE UNIQUE INDEX `IX_Personas_ActiveEmailUnique` ON `Personas` (`ActiveEmailUnique`);

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260614183103_InicialSgvo') THEN

    CREATE UNIQUE INDEX `IX_Personas_ActiveLegajoUnique` ON `Personas` (`ActiveLegajoUnique`);

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260614183103_InicialSgvo') THEN

    CREATE INDEX `IX_Personas_Apellidos_Nombres` ON `Personas` (`Apellidos`, `Nombres`);

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260614183103_InicialSgvo') THEN

    CREATE INDEX `IX_Personas_IsDeleted` ON `Personas` (`IsDeleted`);

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260614183103_InicialSgvo') THEN

    CREATE INDEX `IX_Postulaciones_EstadoPostulacionId` ON `Postulaciones` (`EstadoPostulacionId`);

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260614183103_InicialSgvo') THEN

    CREATE INDEX `IX_Postulaciones_IsDeleted` ON `Postulaciones` (`IsDeleted`);

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260614183103_InicialSgvo') THEN

    CREATE INDEX `IX_Postulaciones_PostulanteId` ON `Postulaciones` (`PostulanteId`);

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260614183103_InicialSgvo') THEN

    CREATE INDEX `IX_Postulaciones_VacanteId_EstadoPostulacionId` ON `Postulaciones` (`VacanteId`, `EstadoPostulacionId`);

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260614183103_InicialSgvo') THEN

    CREATE UNIQUE INDEX `IX_Postulaciones_VacanteId_PostulanteId` ON `Postulaciones` (`VacanteId`, `PostulanteId`);

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260614183103_InicialSgvo') THEN

    CREATE UNIQUE INDEX `IX_Postulantes_ActivePersonaIdUnique` ON `Postulantes` (`ActivePersonaIdUnique`);

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260614183103_InicialSgvo') THEN

    CREATE INDEX `IX_Postulantes_Apellidos_Nombres` ON `Postulantes` (`Apellidos`, `Nombres`);

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260614183103_InicialSgvo') THEN

    CREATE INDEX `IX_Postulantes_Email` ON `Postulantes` (`Email`);

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260614183103_InicialSgvo') THEN

    CREATE INDEX `IX_Postulantes_IsDeleted` ON `Postulantes` (`IsDeleted`);

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260614183103_InicialSgvo') THEN

    CREATE INDEX `IX_Postulantes_PersonaId` ON `Postulantes` (`PersonaId`);

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260614183103_InicialSgvo') THEN

    CREATE UNIQUE INDEX `IX_Puestos_ActiveCodigoUnique` ON `Puestos` (`ActiveCodigoUnique`);

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260614183103_InicialSgvo') THEN

    CREATE INDEX `IX_Puestos_CargoId` ON `Puestos` (`CargoId`);

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260614183103_InicialSgvo') THEN

    CREATE INDEX `IX_Puestos_IsDeleted` ON `Puestos` (`IsDeleted`);

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260614183103_InicialSgvo') THEN

    CREATE INDEX `IX_Puestos_PuestoSuperiorId` ON `Puestos` (`PuestoSuperiorId`);

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260614183103_InicialSgvo') THEN

    CREATE INDEX `IX_Puestos_UnidadOrganizativaId` ON `Puestos` (`UnidadOrganizativaId`);

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260614183103_InicialSgvo') THEN

    CREATE UNIQUE INDEX `IX_UnidadesOrganizativas_ActiveCodigoUnique` ON `UnidadesOrganizativas` (`ActiveCodigoUnique`);

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260614183103_InicialSgvo') THEN

    CREATE INDEX `IX_UnidadesOrganizativas_IsDeleted` ON `UnidadesOrganizativas` (`IsDeleted`);

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260614183103_InicialSgvo') THEN

    CREATE INDEX `IX_UnidadesOrganizativas_Nombre` ON `UnidadesOrganizativas` (`Nombre`);

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260614183103_InicialSgvo') THEN

    CREATE INDEX `IX_UnidadesOrganizativas_UnidadPadreId` ON `UnidadesOrganizativas` (`UnidadPadreId`);

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260614183103_InicialSgvo') THEN

    CREATE INDEX `IX_Vacantes_EstadoVacanteId` ON `Vacantes` (`EstadoVacanteId`);

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260614183103_InicialSgvo') THEN

    CREATE INDEX `IX_Vacantes_EstadoVacanteId_FechaApertura` ON `Vacantes` (`EstadoVacanteId`, `FechaApertura`);

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260614183103_InicialSgvo') THEN

    CREATE INDEX `IX_Vacantes_FechaApertura` ON `Vacantes` (`FechaApertura`);

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260614183103_InicialSgvo') THEN

    CREATE INDEX `IX_Vacantes_IsDeleted` ON `Vacantes` (`IsDeleted`);

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260614183103_InicialSgvo') THEN

    CREATE INDEX `IX_Vacantes_PuestoId` ON `Vacantes` (`PuestoId`);

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260614183103_InicialSgvo') THEN

    INSERT INTO `__EFMigrationsHistory` (`MigrationId`, `ProductVersion`)
    VALUES ('20260614183103_InicialSgvo', '9.0.0');

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260614183109_AgregarDatosSemillaBase') THEN

    INSERT INTO `__EFMigrationsHistory` (`MigrationId`, `ProductVersion`)
    VALUES ('20260614183109_AgregarDatosSemillaBase', '9.0.0');

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

COMMIT;

