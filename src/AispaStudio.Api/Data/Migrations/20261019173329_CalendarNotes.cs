using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AispaStudio.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class CalendarNotes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CalendarNotes",
                schema: "aispa",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    LocationId = table.Column<Guid>(type: "uuid", nullable: false),
                    Date = table.Column<DateOnly>(type: "date", nullable: false),
                    Kind = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: true),
                    ResourceId = table.Column<Guid>(type: "uuid", nullable: true),
                    At = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    Text = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    Done = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CalendarNotes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CalendarNotes_Locations_LocationId",
                        column: x => x.LocationId,
                        principalSchema: "aispa",
                        principalTable: "Locations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CalendarNotes_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalSchema: "aispa",
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CalendarNotes_LocationId_Date",
                schema: "aispa",
                table: "CalendarNotes",
                columns: new[] { "LocationId", "Date" });

            migrationBuilder.CreateIndex(
                name: "IX_CalendarNotes_TenantId",
                schema: "aispa",
                table: "CalendarNotes",
                column: "TenantId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CalendarNotes",
                schema: "aispa");
        }
    }
}
