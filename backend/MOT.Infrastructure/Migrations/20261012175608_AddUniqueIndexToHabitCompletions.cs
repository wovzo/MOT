using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MOT.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddUniqueIndexToHabitCompletions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_HabitCompletions_HabitId_Date",
                table: "HabitCompletions",
                columns: new[] { "HabitId", "Date" },
                unique: true);

            migrationBuilder.DropIndex(
                name: "IX_HabitCompletions_HabitId",
                table: "HabitCompletions");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_HabitCompletions_HabitId",
                table: "HabitCompletions",
                column: "HabitId");

            migrationBuilder.DropIndex(
                name: "IX_HabitCompletions_HabitId_Date",
                table: "HabitCompletions");
        }
    }
}
