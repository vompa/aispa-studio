using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AispaStudio.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class PostfachUndNummernJeSpa : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "IntakeRoutes",
                schema: "aispa",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Channel = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    Address = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: false),
                    SenderId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IntakeRoutes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_IntakeRoutes_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalSchema: "aispa",
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "SpaMailboxes",
                schema: "aispa",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Kind = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    Address = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: false),
                    Enabled = table.Column<bool>(type: "boolean", nullable: false),
                    Host = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    Port = table.Column<int>(type: "integer", nullable: false),
                    Security = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    UserName = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: true),
                    Folder = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    ProcessedFolder = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    DirectoryTenantId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    ClientId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    ProtectedSecret = table.Column<string>(type: "character varying(8000)", maxLength: 8000, nullable: true),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    LastCheckedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    LastError = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SpaMailboxes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SpaMailboxes_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalSchema: "aispa",
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_IntakeRoutes_Channel_Address",
                schema: "aispa",
                table: "IntakeRoutes",
                columns: new[] { "Channel", "Address" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_IntakeRoutes_TenantId",
                schema: "aispa",
                table: "IntakeRoutes",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_SpaMailboxes_TenantId",
                schema: "aispa",
                table: "SpaMailboxes",
                column: "TenantId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "IntakeRoutes",
                schema: "aispa");

            migrationBuilder.DropTable(
                name: "SpaMailboxes",
                schema: "aispa");
        }
    }
}
