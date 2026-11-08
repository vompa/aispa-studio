using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AispaStudio.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class SchnellwechselPin : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "SharedDevices",
                schema: "aispa",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TokenHash = table.Column<byte[]>(type: "bytea", nullable: false),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    LocationId = table.Column<Guid>(type: "uuid", nullable: false),
                    RegisteredByUserId = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    LastUsedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    RevokedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SharedDevices", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SharedDevices_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalSchema: "aispa",
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "StaffPins",
                schema: "aispa",
                columns: table => new
                {
                    UserId = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: false),
                    PinHash = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    FailedAttempts = table.Column<int>(type: "integer", nullable: false),
                    LockedUntil = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StaffPins", x => x.UserId);
                    table.ForeignKey(
                        name: "FK_StaffPins_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalSchema: "aispa",
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_StaffPins_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalSchema: "aispa",
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SharedDevices_LocationId",
                schema: "aispa",
                table: "SharedDevices",
                column: "LocationId");

            migrationBuilder.CreateIndex(
                name: "IX_SharedDevices_TenantId",
                schema: "aispa",
                table: "SharedDevices",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_SharedDevices_TokenHash",
                schema: "aispa",
                table: "SharedDevices",
                column: "TokenHash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_StaffPins_TenantId",
                schema: "aispa",
                table: "StaffPins",
                column: "TenantId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SharedDevices",
                schema: "aispa");

            migrationBuilder.DropTable(
                name: "StaffPins",
                schema: "aispa");
        }
    }
}
