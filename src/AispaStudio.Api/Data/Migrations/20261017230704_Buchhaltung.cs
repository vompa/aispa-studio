using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AispaStudio.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class Buchhaltung : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AccountingSettings",
                schema: "aispa",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    DatevConsultantNumber = table.Column<string>(type: "character varying(7)", maxLength: 7, nullable: true),
                    DatevClientNumber = table.Column<string>(type: "character varying(5)", maxLength: 5, nullable: true),
                    AccountLength = table.Column<int>(type: "integer", nullable: false),
                    FiscalYearStartMonth = table.Column<int>(type: "integer", nullable: false),
                    TransitAccount = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    CashAccount = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    CardAccount = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    InstantTransferAccount = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    VoucherAccount = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    CashTransferAccount = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    DatevTaxKeys = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AccountingSettings", x => x.Id);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AccountingSettings",
                schema: "aispa");
        }
    }
}
