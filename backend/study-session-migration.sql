START TRANSACTION;

CREATE TABLE `StudySessions` (
    `Id` char(36) COLLATE ascii_general_ci NOT NULL,
    `UserId` char(36) COLLATE ascii_general_ci NOT NULL,
    `Title` longtext CHARACTER SET utf8mb4 NOT NULL,
    `StartTime` datetime(6) NOT NULL,
    `EndTime` datetime(6) NULL,
    `DurationMinutes` int NOT NULL,
    `IsCompleted` tinyint(1) NOT NULL,
    CONSTRAINT `PK_StudySessions` PRIMARY KEY (`Id`),
    CONSTRAINT `FK_StudySessions_Users_UserId` FOREIGN KEY (`UserId`) REFERENCES `Users` (`Id`) ON DELETE CASCADE
) CHARACTER SET=utf8mb4;

CREATE INDEX `IX_StudySessions_UserId` ON `StudySessions` (`UserId`);

INSERT INTO `__EFMigrationsHistory` (`MigrationId`, `ProductVersion`)
VALUES ('20261002081502_AddStudySessions', '8.0.4');

COMMIT;

