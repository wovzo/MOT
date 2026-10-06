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
            // Active study session invariant (!IsCompleted && EndTime == null) is enforced at application layer
            // via StartStudySessionCommandHandler throwing ActiveStudySessionAlreadyExistsException (HTTP 409 Conflict).
            // Stored generated column UNIQUE indexing is unsupported on the host MariaDB 10.11 engine.
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
        }
    }
}
