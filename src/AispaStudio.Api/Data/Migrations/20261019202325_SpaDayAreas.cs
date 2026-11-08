using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AispaStudio.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class SpaDayAreas : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "GroupId",
                schema: "aispa",
                table: "AreaVisits",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_AreaVisits_GroupId",
                schema: "aispa",
                table: "AreaVisits",
                column: "GroupId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_AreaVisits_GroupId",
                schema: "aispa",
                table: "AreaVisits");

            migrationBuilder.DropColumn(
                name: "GroupId",
                schema: "aispa",
                table: "AreaVisits");
        }
    }
}
