using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MOT.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddSingleActiveStudySessionConstraint : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "ActiveUserId",
                table: "StudySessions",
                type: "char(36)",
                nullable: true,
                computedColumnSql: "CASE WHEN `IsCompleted` = 0 AND `EndTime` IS NULL THEN `UserId` ELSE NULL END",
                stored: true,
                collation: "ascii_general_ci");

            migrationBuilder.CreateIndex(
                name: "IX_StudySessions_ActiveUserId",
                table: "StudySessions",
                column: "ActiveUserId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_StudySessions_ActiveUserId",
                table: "StudySessions");

            migrationBuilder.DropColumn(
                name: "ActiveUserId",
                table: "StudySessions");
        }
    }
}
