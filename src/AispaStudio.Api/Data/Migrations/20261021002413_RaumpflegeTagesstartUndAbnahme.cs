using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AispaStudio.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class RaumpflegeTagesstartUndAbnahme : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "RoomApprovals",
                schema: "aispa",
                columns: table => new
                {
                    RoomId = table.Column<Guid>(type: "uuid", nullable: false),
                    Date = table.Column<DateOnly>(type: "date", nullable: false),
                    Kind = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false, defaultValue: "DayStart"),
                    ApprovedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ApprovedByUserId = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: true),
                    RecordedByUserId = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: true),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RoomApprovals", x => new { x.RoomId, x.Date, x.Kind });
                    table.ForeignKey(
                        name: "FK_RoomApprovals_Rooms_RoomId",
                        column: x => x.RoomId,
                        principalSchema: "aispa",
                        principalTable: "Rooms",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RoomApprovals_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalSchema: "aispa",
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "RoomCareChecks",
                schema: "aispa",
                columns: table => new
                {
                    AppointmentId = table.Column<Guid>(type: "uuid", nullable: false),
                    RoomId = table.Column<Guid>(type: "uuid", nullable: false),
                    CheckedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UserId = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: true),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RoomCareChecks", x => x.AppointmentId);
                    table.ForeignKey(
                        name: "FK_RoomCareChecks_Appointments_AppointmentId",
                        column: x => x.AppointmentId,
                        principalSchema: "aispa",
                        principalTable: "Appointments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_RoomCareChecks_Rooms_RoomId",
                        column: x => x.RoomId,
                        principalSchema: "aispa",
                        principalTable: "Rooms",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RoomCareChecks_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalSchema: "aispa",
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "RoomStartItems",
                schema: "aispa",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    RoomId = table.Column<Guid>(type: "uuid", nullable: false),
                    Position = table.Column<int>(type: "integer", nullable: false),
                    Text = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RoomStartItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RoomStartItems_Rooms_RoomId",
                        column: x => x.RoomId,
                        principalSchema: "aispa",
                        principalTable: "Rooms",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RoomStartItems_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalSchema: "aispa",
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "RoomStartChecks",
                schema: "aispa",
                columns: table => new
                {
                    RoomId = table.Column<Guid>(type: "uuid", nullable: false),
                    Date = table.Column<DateOnly>(type: "date", nullable: false),
                    ItemId = table.Column<Guid>(type: "uuid", nullable: false),
                    CheckedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UserId = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: true),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RoomStartChecks", x => new { x.RoomId, x.Date, x.ItemId });
                    table.ForeignKey(
                        name: "FK_RoomStartChecks_RoomStartItems_ItemId",
                        column: x => x.ItemId,
                        principalSchema: "aispa",
                        principalTable: "RoomStartItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RoomStartChecks_Rooms_RoomId",
                        column: x => x.RoomId,
                        principalSchema: "aispa",
                        principalTable: "Rooms",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RoomStartChecks_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalSchema: "aispa",
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_RoomApprovals_TenantId",
                schema: "aispa",
                table: "RoomApprovals",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_RoomCareChecks_RoomId",
                schema: "aispa",
                table: "RoomCareChecks",
                column: "RoomId");

            migrationBuilder.CreateIndex(
                name: "IX_RoomCareChecks_TenantId",
                schema: "aispa",
                table: "RoomCareChecks",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_RoomStartChecks_ItemId",
                schema: "aispa",
                table: "RoomStartChecks",
                column: "ItemId");

            migrationBuilder.CreateIndex(
                name: "IX_RoomStartChecks_TenantId",
                schema: "aispa",
                table: "RoomStartChecks",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_RoomStartItems_RoomId_Position",
                schema: "aispa",
                table: "RoomStartItems",
                columns: new[] { "RoomId", "Position" });

            migrationBuilder.CreateIndex(
                name: "IX_RoomStartItems_TenantId",
                schema: "aispa",
                table: "RoomStartItems",
                column: "TenantId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "RoomApprovals",
                schema: "aispa");

            migrationBuilder.DropTable(
                name: "RoomCareChecks",
                schema: "aispa");

            migrationBuilder.DropTable(
                name: "RoomStartChecks",
                schema: "aispa");

            migrationBuilder.DropTable(
                name: "RoomStartItems",
                schema: "aispa");
        }
    }
}
