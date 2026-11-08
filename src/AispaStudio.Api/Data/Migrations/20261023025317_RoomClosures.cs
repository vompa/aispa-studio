using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AispaStudio.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class RoomClosures : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "RoomClosures",
                schema: "aispa",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    RoomId = table.Column<Guid>(type: "uuid", nullable: false),
                    FromDate = table.Column<DateOnly>(type: "date", nullable: false),
                    FromTime = table.Column<TimeOnly>(type: "time without time zone", nullable: true),
                    UntilDate = table.Column<DateOnly>(type: "date", nullable: false),
                    UntilTime = table.Column<TimeOnly>(type: "time without time zone", nullable: true),
                    Kind = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Reason = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    CreatedByUserId = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: false),
                    CreatedByName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RoomClosures", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RoomClosures_Rooms_RoomId",
                        column: x => x.RoomId,
                        principalSchema: "aispa",
                        principalTable: "Rooms",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RoomClosures_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalSchema: "aispa",
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_RoomClosures_RoomId_FromDate_UntilDate",
                schema: "aispa",
                table: "RoomClosures",
                columns: new[] { "RoomId", "FromDate", "UntilDate" });

            migrationBuilder.CreateIndex(
                name: "IX_RoomClosures_TenantId",
                schema: "aispa",
                table: "RoomClosures",
                column: "TenantId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "RoomClosures",
                schema: "aispa");
        }
    }
}
