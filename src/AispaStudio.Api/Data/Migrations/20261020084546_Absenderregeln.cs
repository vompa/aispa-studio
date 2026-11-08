using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AispaStudio.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class Absenderregeln : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "SenderRulesSeeded",
                schema: "aispa",
                table: "Tenants",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "AcceptInPlatformPending",
                schema: "aispa",
                table: "InboundMails",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "PlatformPayment",
                schema: "aispa",
                table: "InboundMails",
                type: "character varying(16)",
                maxLength: 16,
                nullable: false,
                defaultValue: "None");

            migrationBuilder.AddColumn<string>(
                name: "PlatformReference",
                schema: "aispa",
                table: "InboundMails",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "PrepaidAmount",
                schema: "aispa",
                table: "InboundMails",
                type: "numeric(10,2)",
                precision: 10,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SenderRulePattern",
                schema: "aispa",
                table: "InboundMails",
                type: "character varying(320)",
                maxLength: 320,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "AssignmentOpen",
                schema: "aispa",
                table: "Customers",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "AcceptInPlatformPending",
                schema: "aispa",
                table: "Appointments",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "PlatformPayment",
                schema: "aispa",
                table: "Appointments",
                type: "character varying(16)",
                maxLength: 16,
                nullable: false,
                defaultValue: "None");

            migrationBuilder.AddColumn<decimal>(
                name: "PrepaidAmount",
                schema: "aispa",
                table: "Appointments",
                type: "numeric(10,2)",
                precision: 10,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TreatwellAccount",
                schema: "aispa",
                table: "AccountingSettings",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "1400");

            migrationBuilder.CreateTable(
                name: "SenderRules",
                schema: "aispa",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Pattern = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: false),
                    Platform = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    Handling = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    Active = table.Column<bool>(type: "boolean", nullable: false),
                    Note = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SenderRules", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SenderRules_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalSchema: "aispa",
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SenderRules_TenantId",
                schema: "aispa",
                table: "SenderRules",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_SenderRules_TenantId_Pattern",
                schema: "aispa",
                table: "SenderRules",
                columns: new[] { "TenantId", "Pattern" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SenderRules",
                schema: "aispa");

            migrationBuilder.DropColumn(
                name: "SenderRulesSeeded",
                schema: "aispa",
                table: "Tenants");

            migrationBuilder.DropColumn(
                name: "AcceptInPlatformPending",
                schema: "aispa",
                table: "InboundMails");

            migrationBuilder.DropColumn(
                name: "PlatformPayment",
                schema: "aispa",
                table: "InboundMails");

            migrationBuilder.DropColumn(
                name: "PlatformReference",
                schema: "aispa",
                table: "InboundMails");

            migrationBuilder.DropColumn(
                name: "PrepaidAmount",
                schema: "aispa",
                table: "InboundMails");

            migrationBuilder.DropColumn(
                name: "SenderRulePattern",
                schema: "aispa",
                table: "InboundMails");

            migrationBuilder.DropColumn(
                name: "AssignmentOpen",
                schema: "aispa",
                table: "Customers");

            migrationBuilder.DropColumn(
                name: "AcceptInPlatformPending",
                schema: "aispa",
                table: "Appointments");

            migrationBuilder.DropColumn(
                name: "PlatformPayment",
                schema: "aispa",
                table: "Appointments");

            migrationBuilder.DropColumn(
                name: "PrepaidAmount",
                schema: "aispa",
                table: "Appointments");

            migrationBuilder.DropColumn(
                name: "TreatwellAccount",
                schema: "aispa",
                table: "AccountingSettings");
        }
    }
}
