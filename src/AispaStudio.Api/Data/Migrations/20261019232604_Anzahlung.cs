using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AispaStudio.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class Anzahlung : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_BillLines_AppointmentId",
                schema: "aispa",
                table: "BillLines");

            migrationBuilder.AddColumn<int>(
                name: "DepositPercent",
                schema: "aispa",
                table: "Treatments",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "DepositId",
                schema: "aispa",
                table: "ReceiptLine",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "DepositPercent",
                schema: "aispa",
                table: "LocationOnlineSettings",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "AppointmentDeposits",
                schema: "aispa",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AppointmentId = table.Column<Guid>(type: "uuid", nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: false),
                    TaxCategory = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    TaxPercent = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: false),
                    RevenueAccountId = table.Column<Guid>(type: "uuid", nullable: false),
                    ReceiptId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    SettledOnReceiptId = table.Column<Guid>(type: "uuid", nullable: true),
                    VoidedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppointmentDeposits", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AppointmentDeposits_Appointments_AppointmentId",
                        column: x => x.AppointmentId,
                        principalSchema: "aispa",
                        principalTable: "Appointments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AppointmentDeposits_Receipts_ReceiptId",
                        column: x => x.ReceiptId,
                        principalSchema: "aispa",
                        principalTable: "Receipts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AppointmentDeposits_Receipts_SettledOnReceiptId",
                        column: x => x.SettledOnReceiptId,
                        principalSchema: "aispa",
                        principalTable: "Receipts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AppointmentDeposits_RevenueAccounts_RevenueAccountId",
                        column: x => x.RevenueAccountId,
                        principalSchema: "aispa",
                        principalTable: "RevenueAccounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AppointmentDeposits_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalSchema: "aispa",
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_BillLines_AppointmentId",
                schema: "aispa",
                table: "BillLines",
                column: "AppointmentId",
                unique: true,
                filter: "\"AppointmentId\" IS NOT NULL AND \"Kind\" = 'Appointment'");

            migrationBuilder.CreateIndex(
                name: "IX_AppointmentDeposits_AppointmentId",
                schema: "aispa",
                table: "AppointmentDeposits",
                column: "AppointmentId");

            migrationBuilder.CreateIndex(
                name: "IX_AppointmentDeposits_ReceiptId",
                schema: "aispa",
                table: "AppointmentDeposits",
                column: "ReceiptId");

            migrationBuilder.CreateIndex(
                name: "IX_AppointmentDeposits_RevenueAccountId",
                schema: "aispa",
                table: "AppointmentDeposits",
                column: "RevenueAccountId");

            migrationBuilder.CreateIndex(
                name: "IX_AppointmentDeposits_SettledOnReceiptId",
                schema: "aispa",
                table: "AppointmentDeposits",
                column: "SettledOnReceiptId");

            migrationBuilder.CreateIndex(
                name: "IX_AppointmentDeposits_TenantId",
                schema: "aispa",
                table: "AppointmentDeposits",
                column: "TenantId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AppointmentDeposits",
                schema: "aispa");

            migrationBuilder.DropIndex(
                name: "IX_BillLines_AppointmentId",
                schema: "aispa",
                table: "BillLines");

            migrationBuilder.DropColumn(
                name: "DepositPercent",
                schema: "aispa",
                table: "Treatments");

            migrationBuilder.DropColumn(
                name: "DepositId",
                schema: "aispa",
                table: "ReceiptLine");

            migrationBuilder.DropColumn(
                name: "DepositPercent",
                schema: "aispa",
                table: "LocationOnlineSettings");

            migrationBuilder.CreateIndex(
                name: "IX_BillLines_AppointmentId",
                schema: "aispa",
                table: "BillLines",
                column: "AppointmentId",
                unique: true,
                filter: "\"AppointmentId\" IS NOT NULL");
        }
    }
}
