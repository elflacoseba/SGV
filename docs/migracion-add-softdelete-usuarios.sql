START TRANSACTION;
DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260715145121_AddSoftDeleteToAspNetUsers') THEN

    ALTER TABLE `AspNetUsers`
      ADD COLUMN `IsDeleted` TINYINT(1) NOT NULL DEFAULT 0,
      ALGORITHM=INPLACE,
      LOCK=NONE;

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260715145121_AddSoftDeleteToAspNetUsers') THEN

    ALTER TABLE `AspNetUsers`
      ADD COLUMN `ActiveUserNameUnique` VARCHAR(256)
        COLLATE `utf8mb4_0900_ai_ci`
        GENERATED ALWAYS AS (CASE WHEN `IsDeleted` = 0 THEN LOWER(`UserName`) ELSE NULL END) STORED,
      ALGORITHM=COPY;

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260715145121_AddSoftDeleteToAspNetUsers') THEN

    ALTER TABLE `AspNetUsers`
      ADD UNIQUE INDEX `IX_AspNetUsers_ActiveUserNameUnique` (`ActiveUserNameUnique`),
      ALGORITHM=INPLACE,
      LOCK=NONE;

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

DROP PROCEDURE IF EXISTS MigrationsScript;
DELIMITER //
CREATE PROCEDURE MigrationsScript()
BEGIN
    IF NOT EXISTS(SELECT 1 FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260715145121_AddSoftDeleteToAspNetUsers') THEN

    INSERT INTO `__EFMigrationsHistory` (`MigrationId`, `ProductVersion`)
    VALUES ('20260715145121_AddSoftDeleteToAspNetUsers', '9.0.0');

    END IF;
END //
DELIMITER ;
CALL MigrationsScript();
DROP PROCEDURE MigrationsScript;

COMMIT;
