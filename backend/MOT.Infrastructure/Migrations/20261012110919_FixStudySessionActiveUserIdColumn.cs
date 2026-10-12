using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MOT.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class FixStudySessionActiveUserIdColumn : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Ensures ActiveUserId column exists as nullable char(36) regardless of whether previous migration ran DDL
            migrationBuilder.Sql(@"
                SET @col_exists := (
                    SELECT COUNT(*) 
                    FROM INFORMATION_SCHEMA.COLUMNS 
                    WHERE TABLE_SCHEMA = DATABASE() 
                      AND TABLE_NAME = 'StudySessions' 
                      AND COLUMN_NAME = 'ActiveUserId'
                );
                SET @sql := IF(@col_exists = 0, 
                    'ALTER TABLE `StudySessions` ADD `ActiveUserId` char(36) COLLATE ascii_general_ci NULL;', 
                    'ALTER TABLE `StudySessions` MODIFY COLUMN `ActiveUserId` char(36) COLLATE ascii_general_ci NULL;'
                );
                PREPARE stmt FROM @sql;
                EXECUTE stmt;
                DEALLOCATE PREPARE stmt;
            ");

            // Backfill active sessions
            migrationBuilder.Sql(@"
                UPDATE `StudySessions` 
                SET `ActiveUserId` = `UserId` 
                WHERE `IsCompleted` = 0 AND `EndTime` IS NULL;
            ");

            // Ensure unique index exists
            migrationBuilder.Sql(@"
                SET @idx_exists := (
                    SELECT COUNT(*) 
                    FROM INFORMATION_SCHEMA.STATISTICS 
                    WHERE TABLE_SCHEMA = DATABASE() 
                      AND TABLE_NAME = 'StudySessions' 
                      AND INDEX_NAME = 'IX_StudySessions_ActiveUserId'
                );
                SET @sql := IF(@idx_exists = 0, 
                    'CREATE UNIQUE INDEX `IX_StudySessions_ActiveUserId` ON `StudySessions` (`ActiveUserId`);', 
                    'SELECT 1;'
                );
                PREPARE stmt FROM @sql;
                EXECUTE stmt;
                DEALLOCATE PREPARE stmt;
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                SET @idx_exists := (
                    SELECT COUNT(*) 
                    FROM INFORMATION_SCHEMA.STATISTICS 
                    WHERE TABLE_SCHEMA = DATABASE() 
                      AND TABLE_NAME = 'StudySessions' 
                      AND INDEX_NAME = 'IX_StudySessions_ActiveUserId'
                );
                SET @sql := IF(@idx_exists > 0, 
                    'DROP INDEX `IX_StudySessions_ActiveUserId` ON `StudySessions`;', 
                    'SELECT 1;'
                );
                PREPARE stmt FROM @sql;
                EXECUTE stmt;
                DEALLOCATE PREPARE stmt;
            ");

            migrationBuilder.Sql(@"
                SET @col_exists := (
                    SELECT COUNT(*) 
                    FROM INFORMATION_SCHEMA.COLUMNS 
                    WHERE TABLE_SCHEMA = DATABASE() 
                      AND TABLE_NAME = 'StudySessions' 
                      AND COLUMN_NAME = 'ActiveUserId'
                );
                SET @sql := IF(@col_exists > 0, 
                    'ALTER TABLE `StudySessions` DROP COLUMN `ActiveUserId`;', 
                    'SELECT 1;'
                );
                PREPARE stmt FROM @sql;
                EXECUTE stmt;
                DEALLOCATE PREPARE stmt;
            ");
        }
    }
}
