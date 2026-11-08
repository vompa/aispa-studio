using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AispaStudio.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class Kundenname : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "City",
                schema: "aispa",
                table: "Customers",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Country",
                schema: "aispa",
                table: "Customers",
                type: "character varying(2)",
                maxLength: 2,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PostalCode",
                schema: "aispa",
                table: "Customers",
                type: "character varying(16)",
                maxLength: 16,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PreferredName",
                schema: "aispa",
                table: "Customers",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Salutation",
                schema: "aispa",
                table: "Customers",
                type: "character varying(8)",
                maxLength: 8,
                nullable: false,
                defaultValue: "None");

            migrationBuilder.AddColumn<string>(
                name: "Street",
                schema: "aispa",
                table: "Customers",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "TitleInGreeting",
                schema: "aispa",
                table: "Customers",
                type: "boolean",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TitlesAfter",
                schema: "aispa",
                table: "Customers",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TitlesBefore",
                schema: "aispa",
                table: "Customers",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "CustomerNameChanges",
                schema: "aispa",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CustomerId = table.Column<Guid>(type: "uuid", nullable: false),
                    OldName = table.Column<string>(type: "character varying(201)", maxLength: 201, nullable: false),
                    NewName = table.Column<string>(type: "character varying(201)", maxLength: 201, nullable: false),
                    ChangedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ChangedByUserId = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: true),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CustomerNameChanges", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CustomerNameChanges_Customers_CustomerId",
                        column: x => x.CustomerId,
                        principalSchema: "aispa",
                        principalTable: "Customers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CustomerNameChanges_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalSchema: "aispa",
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CustomerNameChanges_CustomerId",
                schema: "aispa",
                table: "CustomerNameChanges",
                column: "CustomerId");

            migrationBuilder.CreateIndex(
                name: "IX_CustomerNameChanges_TenantId",
                schema: "aispa",
                table: "CustomerNameChanges",
                column: "TenantId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CustomerNameChanges",
                schema: "aispa");

            migrationBuilder.DropColumn(
                name: "City",
                schema: "aispa",
                table: "Customers");

            migrationBuilder.DropColumn(
                name: "Country",
                schema: "aispa",
                table: "Customers");

            migrationBuilder.DropColumn(
                name: "PostalCode",
                schema: "aispa",
                table: "Customers");

            migrationBuilder.DropColumn(
                name: "PreferredName",
                schema: "aispa",
                table: "Customers");

            migrationBuilder.DropColumn(
                name: "Salutation",
                schema: "aispa",
                table: "Customers");

            migrationBuilder.DropColumn(
                name: "Street",
                schema: "aispa",
                table: "Customers");

            migrationBuilder.DropColumn(
                name: "TitleInGreeting",
                schema: "aispa",
                table: "Customers");

            migrationBuilder.DropColumn(
                name: "TitlesAfter",
                schema: "aispa",
                table: "Customers");

            migrationBuilder.DropColumn(
                name: "TitlesBefore",
                schema: "aispa",
                table: "Customers");
        }
    }
}
