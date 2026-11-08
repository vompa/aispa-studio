using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AispaStudio.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class Kundenzuordnung : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CustomerCandidates",
                schema: "aispa",
                table: "InboundMails",
                type: "jsonb",
                nullable: false,
                defaultValue: "[]");

            migrationBuilder.AddColumn<bool>(
                name: "CustomerConfirmed",
                schema: "aispa",
                table: "InboundMails",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            // Bisherige Zuordnungen kamen aus gleicher Mailadresse oder Nummer bzw. aus der Rezeption: sie gelten als bestätigt.
            migrationBuilder.Sql("UPDATE aispa.\"InboundMails\" SET \"CustomerConfirmed\" = TRUE WHERE \"CustomerId\" IS NOT NULL;");

            migrationBuilder.AddColumn<string>(
                name: "GuestEmail",
                schema: "aispa",
                table: "InboundMails",
                type: "character varying(320)",
                maxLength: 320,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CreatedFromChannel",
                schema: "aispa",
                table: "Customers",
                type: "character varying(16)",
                maxLength: 16,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "CustomerEmails",
                schema: "aispa",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CustomerId = table.Column<Guid>(type: "uuid", nullable: false),
                    Address = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    NormalizedAddress = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CustomerEmails", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CustomerEmails_Customers_CustomerId",
                        column: x => x.CustomerId,
                        principalSchema: "aispa",
                        principalTable: "Customers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CustomerEmails_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalSchema: "aispa",
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CustomerEmails_CustomerId_NormalizedAddress",
                schema: "aispa",
                table: "CustomerEmails",
                columns: new[] { "CustomerId", "NormalizedAddress" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CustomerEmails_NormalizedAddress",
                schema: "aispa",
                table: "CustomerEmails",
                column: "NormalizedAddress");

            migrationBuilder.CreateIndex(
                name: "IX_CustomerEmails_TenantId",
                schema: "aispa",
                table: "CustomerEmails",
                column: "TenantId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CustomerEmails",
                schema: "aispa");

            migrationBuilder.DropColumn(
                name: "CustomerCandidates",
                schema: "aispa",
                table: "InboundMails");

            migrationBuilder.DropColumn(
                name: "CustomerConfirmed",
                schema: "aispa",
                table: "InboundMails");

            migrationBuilder.DropColumn(
                name: "GuestEmail",
                schema: "aispa",
                table: "InboundMails");

            migrationBuilder.DropColumn(
                name: "CreatedFromChannel",
                schema: "aispa",
                table: "Customers");
        }
    }
}
