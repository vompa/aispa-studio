using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AispaStudio.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class Gruppenbuchung : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "Places",
                schema: "aispa",
                table: "Rooms",
                type: "integer",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<int>(
                name: "Slot",
                schema: "aispa",
                table: "ResourceBookings",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<Guid>(
                name: "GroupId",
                schema: "aispa",
                table: "Appointments",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Appointments_GroupId",
                schema: "aispa",
                table: "Appointments",
                column: "GroupId");

            // Mehrplatzräume (Paarsuite): die Ausschlussbedingung gilt je Ressource und Platz. Alle anderen Belegungen haben Platz 0.
            migrationBuilder.Sql("""
                ALTER TABLE aispa."ResourceBookings" DROP CONSTRAINT "EX_ResourceBookings_NoOverlap";
                ALTER TABLE aispa."ResourceBookings"
                ADD CONSTRAINT "EX_ResourceBookings_NoOverlap"
                EXCLUDE USING gist ("ResourceId" WITH =, "Slot" WITH =, tstzrange("From", "Until") WITH &&)
                DEFERRABLE INITIALLY DEFERRED;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DELETE FROM aispa."ResourceBookings" WHERE "Slot" <> 0;
                ALTER TABLE aispa."ResourceBookings" DROP CONSTRAINT "EX_ResourceBookings_NoOverlap";
                ALTER TABLE aispa."ResourceBookings"
                ADD CONSTRAINT "EX_ResourceBookings_NoOverlap"
                EXCLUDE USING gist ("ResourceId" WITH =, tstzrange("From", "Until") WITH &&)
                DEFERRABLE INITIALLY DEFERRED;
                """);

            migrationBuilder.DropIndex(
                name: "IX_Appointments_GroupId",
                schema: "aispa",
                table: "Appointments");

            migrationBuilder.DropColumn(
                name: "Places",
                schema: "aispa",
                table: "Rooms");

            migrationBuilder.DropColumn(
                name: "Slot",
                schema: "aispa",
                table: "ResourceBookings");

            migrationBuilder.DropColumn(
                name: "GroupId",
                schema: "aispa",
                table: "Appointments");
        }
    }
}
