using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AispaStudio.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class Bereiche : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsArea",
                schema: "aispa",
                table: "Rooms",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<Guid>(
                name: "AreaVisitId",
                schema: "aispa",
                table: "ResourceBookings",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "AreaVisits",
                schema: "aispa",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    LocationId = table.Column<Guid>(type: "uuid", nullable: false),
                    RoomId = table.Column<Guid>(type: "uuid", nullable: false),
                    CustomerId = table.Column<Guid>(type: "uuid", nullable: false),
                    Persons = table.Column<int>(type: "integer", nullable: false),
                    From = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Until = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    IsCancelled = table.Column<bool>(type: "boolean", nullable: false),
                    Note = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AreaVisits", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AreaVisits_Customers_CustomerId",
                        column: x => x.CustomerId,
                        principalSchema: "aispa",
                        principalTable: "Customers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AreaVisits_Locations_LocationId",
                        column: x => x.LocationId,
                        principalSchema: "aispa",
                        principalTable: "Locations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AreaVisits_Rooms_RoomId",
                        column: x => x.RoomId,
                        principalSchema: "aispa",
                        principalTable: "Rooms",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AreaVisits_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalSchema: "aispa",
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ResourceBookings_AreaVisitId",
                schema: "aispa",
                table: "ResourceBookings",
                column: "AreaVisitId");

            migrationBuilder.CreateIndex(
                name: "IX_AreaVisits_CustomerId",
                schema: "aispa",
                table: "AreaVisits",
                column: "CustomerId");

            migrationBuilder.CreateIndex(
                name: "IX_AreaVisits_LocationId_From",
                schema: "aispa",
                table: "AreaVisits",
                columns: new[] { "LocationId", "From" });

            migrationBuilder.CreateIndex(
                name: "IX_AreaVisits_RoomId",
                schema: "aispa",
                table: "AreaVisits",
                column: "RoomId");

            migrationBuilder.CreateIndex(
                name: "IX_AreaVisits_TenantId",
                schema: "aispa",
                table: "AreaVisits",
                column: "TenantId");

            migrationBuilder.AddForeignKey(
                name: "FK_ResourceBookings_AreaVisits_AreaVisitId",
                schema: "aispa",
                table: "ResourceBookings",
                column: "AreaVisitId",
                principalSchema: "aispa",
                principalTable: "AreaVisits",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ResourceBookings_AreaVisits_AreaVisitId",
                schema: "aispa",
                table: "ResourceBookings");

            migrationBuilder.DropTable(
                name: "AreaVisits",
                schema: "aispa");

            migrationBuilder.DropIndex(
                name: "IX_ResourceBookings_AreaVisitId",
                schema: "aispa",
                table: "ResourceBookings");

            migrationBuilder.DropColumn(
                name: "IsArea",
                schema: "aispa",
                table: "Rooms");

            migrationBuilder.DropColumn(
                name: "AreaVisitId",
                schema: "aispa",
                table: "ResourceBookings");
        }
    }
}
