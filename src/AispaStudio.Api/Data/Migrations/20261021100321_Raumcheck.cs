using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AispaStudio.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class Raumcheck : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "RoomCareRestockAnswers",
                schema: "aispa",
                columns: table => new
                {
                    AppointmentId = table.Column<Guid>(type: "uuid", nullable: false),
                    ItemId = table.Column<Guid>(type: "uuid", nullable: false),
                    State = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    At = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UserId = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: true),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RoomCareRestockAnswers", x => new { x.AppointmentId, x.ItemId });
                    table.ForeignKey(
                        name: "FK_RoomCareRestockAnswers_Appointments_AppointmentId",
                        column: x => x.AppointmentId,
                        principalSchema: "aispa",
                        principalTable: "Appointments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_RoomCareRestockAnswers_StockItems_ItemId",
                        column: x => x.ItemId,
                        principalSchema: "aispa",
                        principalTable: "StockItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RoomCareRestockAnswers_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalSchema: "aispa",
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_RoomCareRestockAnswers_ItemId",
                schema: "aispa",
                table: "RoomCareRestockAnswers",
                column: "ItemId");

            migrationBuilder.CreateIndex(
                name: "IX_RoomCareRestockAnswers_TenantId",
                schema: "aispa",
                table: "RoomCareRestockAnswers",
                column: "TenantId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "RoomCareRestockAnswers",
                schema: "aispa");
        }
    }
}
