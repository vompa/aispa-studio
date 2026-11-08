using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AispaStudio.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class Zugaenge : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Invitations",
                schema: "aispa",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TokenHash = table.Column<byte[]>(type: "bytea", nullable: false),
                    Email = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    DisplayName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Role = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    AllLocations = table.Column<bool>(type: "boolean", nullable: false),
                    LocationIds = table.Column<List<Guid>>(type: "uuid[]", nullable: false),
                    EmployeeId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedByUserId = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ExpiresAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    AcceptedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    AcceptedByUserId = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: true),
                    RevokedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Invitations", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "StaffAccess",
                schema: "aispa",
                columns: table => new
                {
                    UserId = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: false),
                    Role = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    AllLocations = table.Column<bool>(type: "boolean", nullable: false),
                    LocationIds = table.Column<List<Guid>>(type: "uuid[]", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StaffAccess", x => x.UserId);
                    table.ForeignKey(
                        name: "FK_StaffAccess_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalSchema: "aispa",
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Employees_UserId",
                schema: "aispa",
                table: "Employees",
                column: "UserId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Invitations_Email",
                schema: "aispa",
                table: "Invitations",
                column: "Email");

            migrationBuilder.CreateIndex(
                name: "IX_Invitations_TokenHash",
                schema: "aispa",
                table: "Invitations",
                column: "TokenHash",
                unique: true);

            // Bestehende Konten stammen aus der Entwicklung (Testkonten vor Einführung der Rollen); sie behalten vollen Zugriff.
            migrationBuilder.Sql("""
                INSERT INTO aispa."StaffAccess" ("UserId", "Role", "AllLocations", "LocationIds", "IsActive", "UpdatedAt")
                SELECT "Id", 'Owner', TRUE, '{}', TRUE, now() FROM aispa."AspNetUsers";
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Invitations",
                schema: "aispa");

            migrationBuilder.DropTable(
                name: "StaffAccess",
                schema: "aispa");

            migrationBuilder.DropIndex(
                name: "IX_Employees_UserId",
                schema: "aispa",
                table: "Employees");
        }
    }
}
