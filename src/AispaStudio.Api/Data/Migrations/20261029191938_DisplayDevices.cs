using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AispaStudio.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class DisplayDevices : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "DisplayDevices",
                schema: "aispa",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    LocationId = table.Column<Guid>(type: "uuid", nullable: false),
                    PairedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    PairedBy = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: true),
                    LastSeenAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    RevokedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DisplayDevices", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DisplayDevices_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalSchema: "aispa",
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "DisplayPairings",
                schema: "aispa",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CodeHash = table.Column<byte[]>(type: "bytea", nullable: false),
                    SecretHash = table.Column<byte[]>(type: "bytea", nullable: false),
                    ExpiresAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    DeviceId = table.Column<Guid>(type: "uuid", nullable: true),
                    RedeemedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DisplayPairings", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DisplayDevices_LocationId",
                schema: "aispa",
                table: "DisplayDevices",
                column: "LocationId");

            migrationBuilder.CreateIndex(
                name: "IX_DisplayDevices_TenantId",
                schema: "aispa",
                table: "DisplayDevices",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_DisplayPairings_CodeHash",
                schema: "aispa",
                table: "DisplayPairings",
                column: "CodeHash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DisplayPairings_SecretHash",
                schema: "aispa",
                table: "DisplayPairings",
                column: "SecretHash",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DisplayDevices",
                schema: "aispa");

            migrationBuilder.DropTable(
                name: "DisplayPairings",
                schema: "aispa");
        }
    }
}
