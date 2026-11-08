using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AispaStudio.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class TerminZeiten : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ExtraFollowUpMinutes",
                schema: "aispa",
                table: "Appointments",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "FollowUpMinutes",
                schema: "aispa",
                table: "Appointments",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "PreparationMinutes",
                schema: "aispa",
                table: "Appointments",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            // Bestehende Termine: Zeiten aus der Bindung zurückrechnen; der Puffer lässt sich rückwirkend nicht trennen und bleibt 0.
            migrationBuilder.Sql("""
                UPDATE aispa."Appointments"
                SET "PreparationMinutes" = (EXTRACT(EPOCH FROM ("Start" - "BlockedFrom")) / 60)::integer,
                    "FollowUpMinutes" = (EXTRACT(EPOCH FROM ("BlockedUntil" - "End")) / 60)::integer
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ExtraFollowUpMinutes",
                schema: "aispa",
                table: "Appointments");

            migrationBuilder.DropColumn(
                name: "FollowUpMinutes",
                schema: "aispa",
                table: "Appointments");

            migrationBuilder.DropColumn(
                name: "PreparationMinutes",
                schema: "aispa",
                table: "Appointments");
        }
    }
}
