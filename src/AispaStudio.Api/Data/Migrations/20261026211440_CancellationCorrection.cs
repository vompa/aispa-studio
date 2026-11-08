using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AispaStudio.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class CancellationCorrection : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ClosureNotices_ClosureId_AppointmentId",
                schema: "aispa",
                table: "ClosureNotices");

            migrationBuilder.AddColumn<bool>(
                name: "IsCorrection",
                schema: "aispa",
                table: "ClosureNotices",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateIndex(
                name: "IX_ClosureNotices_ClosureId_AppointmentId_IsCorrection",
                schema: "aispa",
                table: "ClosureNotices",
                columns: new[] { "ClosureId", "AppointmentId", "IsCorrection" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ClosureNotices_ClosureId_AppointmentId_IsCorrection",
                schema: "aispa",
                table: "ClosureNotices");

            migrationBuilder.DropColumn(
                name: "IsCorrection",
                schema: "aispa",
                table: "ClosureNotices");

            migrationBuilder.CreateIndex(
                name: "IX_ClosureNotices_ClosureId_AppointmentId",
                schema: "aispa",
                table: "ClosureNotices",
                columns: new[] { "ClosureId", "AppointmentId" },
                unique: true);
        }
    }
}
