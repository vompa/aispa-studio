using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AispaStudio.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class CompanyProfile : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AccountHolder",
                schema: "aispa",
                table: "Tenants",
                type: "character varying(70)",
                maxLength: 70,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Bic",
                schema: "aispa",
                table: "Tenants",
                type: "character varying(11)",
                maxLength: 11,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "BusinessPurpose",
                schema: "aispa",
                table: "Tenants",
                type: "character varying(300)",
                maxLength: 300,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ChamberAndRules",
                schema: "aispa",
                table: "Tenants",
                type: "character varying(300)",
                maxLength: 300,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "City",
                schema: "aispa",
                table: "Tenants",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CompanyRegisterCourt",
                schema: "aispa",
                table: "Tenants",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CompanyRegisterNumber",
                schema: "aispa",
                table: "Tenants",
                type: "character varying(30)",
                maxLength: 30,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Email",
                schema: "aispa",
                table: "Tenants",
                type: "character varying(254)",
                maxLength: 254,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Iban",
                schema: "aispa",
                table: "Tenants",
                type: "character varying(34)",
                maxLength: 34,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LegalName",
                schema: "aispa",
                table: "Tenants",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Phone",
                schema: "aispa",
                table: "Tenants",
                type: "character varying(40)",
                maxLength: 40,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PostalCode",
                schema: "aispa",
                table: "Tenants",
                type: "character varying(10)",
                maxLength: 10,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Street",
                schema: "aispa",
                table: "Tenants",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SupervisoryAuthority",
                schema: "aispa",
                table: "Tenants",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "VatId",
                schema: "aispa",
                table: "Tenants",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Website",
                schema: "aispa",
                table: "Tenants",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AccountHolder",
                schema: "aispa",
                table: "Tenants");

            migrationBuilder.DropColumn(
                name: "Bic",
                schema: "aispa",
                table: "Tenants");

            migrationBuilder.DropColumn(
                name: "BusinessPurpose",
                schema: "aispa",
                table: "Tenants");

            migrationBuilder.DropColumn(
                name: "ChamberAndRules",
                schema: "aispa",
                table: "Tenants");

            migrationBuilder.DropColumn(
                name: "City",
                schema: "aispa",
                table: "Tenants");

            migrationBuilder.DropColumn(
                name: "CompanyRegisterCourt",
                schema: "aispa",
                table: "Tenants");

            migrationBuilder.DropColumn(
                name: "CompanyRegisterNumber",
                schema: "aispa",
                table: "Tenants");

            migrationBuilder.DropColumn(
                name: "Email",
                schema: "aispa",
                table: "Tenants");

            migrationBuilder.DropColumn(
                name: "Iban",
                schema: "aispa",
                table: "Tenants");

            migrationBuilder.DropColumn(
                name: "LegalName",
                schema: "aispa",
                table: "Tenants");

            migrationBuilder.DropColumn(
                name: "Phone",
                schema: "aispa",
                table: "Tenants");

            migrationBuilder.DropColumn(
                name: "PostalCode",
                schema: "aispa",
                table: "Tenants");

            migrationBuilder.DropColumn(
                name: "Street",
                schema: "aispa",
                table: "Tenants");

            migrationBuilder.DropColumn(
                name: "SupervisoryAuthority",
                schema: "aispa",
                table: "Tenants");

            migrationBuilder.DropColumn(
                name: "VatId",
                schema: "aispa",
                table: "Tenants");

            migrationBuilder.DropColumn(
                name: "Website",
                schema: "aispa",
                table: "Tenants");
        }
    }
}
