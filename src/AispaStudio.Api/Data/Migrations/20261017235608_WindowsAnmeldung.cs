using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AispaStudio.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class WindowsAnmeldung : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "DeviceSignInCodes",
                schema: "aispa",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CodeHash = table.Column<byte[]>(type: "bytea", nullable: false),
                    UserId = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ExpiresAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UsedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DeviceSignInCodes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DeviceSignInCodes_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalSchema: "aispa",
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_DeviceSignInCodes_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalSchema: "aispa",
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DeviceSignInCodes_CodeHash",
                schema: "aispa",
                table: "DeviceSignInCodes",
                column: "CodeHash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DeviceSignInCodes_TenantId",
                schema: "aispa",
                table: "DeviceSignInCodes",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_DeviceSignInCodes_UserId",
                schema: "aispa",
                table: "DeviceSignInCodes",
                column: "UserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DeviceSignInCodes",
                schema: "aispa");
        }
    }
}
