using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MOT.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddStudySessionPauseAndResume : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "AccumulatedSeconds",
                table: "StudySessions",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<bool>(
                name: "IsPaused",
                table: "StudySessions",
                type: "tinyint(1)",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "LastPausedAt",
                table: "StudySessions",
                type: "datetime(6)",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "LastResumedAt",
                table: "StudySessions",
                type: "datetime(6)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AccumulatedSeconds",
                table: "StudySessions");

            migrationBuilder.DropColumn(
                name: "IsPaused",
                table: "StudySessions");

            migrationBuilder.DropColumn(
                name: "LastPausedAt",
                table: "StudySessions");

            migrationBuilder.DropColumn(
                name: "LastResumedAt",
                table: "StudySessions");
        }
    }
}
