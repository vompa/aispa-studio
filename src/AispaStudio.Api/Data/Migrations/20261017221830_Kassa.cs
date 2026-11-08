using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace AispaStudio.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class Kassa : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CashRegisters",
                schema: "aispa",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    LocationId = table.Column<Guid>(type: "uuid", nullable: false),
                    Code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Country = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: false),
                    ReceiptHeader = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    PayeeName = table.Column<string>(type: "character varying(70)", maxLength: 70, nullable: true),
                    Iban = table.Column<string>(type: "character varying(34)", maxLength: 34, nullable: true),
                    Bic = table.Column<string>(type: "character varying(11)", maxLength: 11, nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    LastReceiptNumber = table.Column<long>(type: "bigint", nullable: false),
                    LastReceiptAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    TurnoverCounter = table.Column<long>(type: "bigint", nullable: false),
                    LastChainValue = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    TurnoverKey = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    CertificateSerial = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    SignatureDeviceFailedSince = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CashRegisters", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CashRegisters_Locations_LocationId",
                        column: x => x.LocationId,
                        principalSchema: "aispa",
                        principalTable: "Locations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "RevenueAccounts",
                schema: "aispa",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Code = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    CostCenter = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RevenueAccounts", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Vouchers",
                schema: "aispa",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Code = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Kind = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    InitialValue = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: false),
                    Balance = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: false),
                    TreatmentId = table.Column<Guid>(type: "uuid", nullable: true),
                    Status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    ValidUntil = table.Column<DateOnly>(type: "date", nullable: true),
                    SoldOnReceiptId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Vouchers", x => x.Id);
                    table.CheckConstraint("CK_Vouchers_Balance", "\"Balance\" >= 0 AND \"Balance\" <= \"InitialValue\"");
                    table.ForeignKey(
                        name: "FK_Vouchers_Treatments_TreatmentId",
                        column: x => x.TreatmentId,
                        principalSchema: "aispa",
                        principalTable: "Treatments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Bills",
                schema: "aispa",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    RegisterId = table.Column<Guid>(type: "uuid", nullable: false),
                    CustomerId = table.Column<Guid>(type: "uuid", nullable: true),
                    Status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Bills", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Bills_CashRegisters_RegisterId",
                        column: x => x.RegisterId,
                        principalSchema: "aispa",
                        principalTable: "CashRegisters",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Bills_Customers_CustomerId",
                        column: x => x.CustomerId,
                        principalSchema: "aispa",
                        principalTable: "Customers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CashMovements",
                schema: "aispa",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    RegisterId = table.Column<Guid>(type: "uuid", nullable: false),
                    Kind = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: false),
                    Reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    At = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UserId = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CashMovements", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CashMovements_CashRegisters_RegisterId",
                        column: x => x.RegisterId,
                        principalSchema: "aispa",
                        principalTable: "CashRegisters",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "DayClosings",
                schema: "aispa",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    RegisterId = table.Column<Guid>(type: "uuid", nullable: false),
                    From = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Until = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CountedCash = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: false),
                    UserId = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: true),
                    Report = table.Column<string>(type: "jsonb", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DayClosings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DayClosings_CashRegisters_RegisterId",
                        column: x => x.RegisterId,
                        principalSchema: "aispa",
                        principalTable: "CashRegisters",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Receipts",
                schema: "aispa",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    RegisterId = table.Column<Guid>(type: "uuid", nullable: false),
                    Number = table.Column<long>(type: "bigint", nullable: false),
                    Kind = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    IssuedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    BillId = table.Column<Guid>(type: "uuid", nullable: true),
                    CustomerId = table.Column<Guid>(type: "uuid", nullable: true),
                    Total = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    Change = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: false),
                    VoidsReceiptId = table.Column<Guid>(type: "uuid", nullable: true),
                    VoidReason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    FiscalCode = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    FiscalSignature = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    SignatureDeviceFailed = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedByUserId = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Receipts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Receipts_CashRegisters_RegisterId",
                        column: x => x.RegisterId,
                        principalSchema: "aispa",
                        principalTable: "CashRegisters",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Receipts_Receipts_VoidsReceiptId",
                        column: x => x.VoidsReceiptId,
                        principalSchema: "aispa",
                        principalTable: "Receipts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "TestSignatureKey",
                schema: "aispa",
                columns: table => new
                {
                    RegisterId = table.Column<Guid>(type: "uuid", nullable: false),
                    PrivateKey = table.Column<string>(type: "text", nullable: false),
                    Certificate = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TestSignatureKey", x => x.RegisterId);
                    table.ForeignKey(
                        name: "FK_TestSignatureKey_CashRegisters_RegisterId",
                        column: x => x.RegisterId,
                        principalSchema: "aispa",
                        principalTable: "CashRegisters",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Articles",
                schema: "aispa",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Price = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: false),
                    TaxCategory = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    RevenueAccountId = table.Column<Guid>(type: "uuid", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Articles", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Articles_RevenueAccounts_RevenueAccountId",
                        column: x => x.RevenueAccountId,
                        principalSchema: "aispa",
                        principalTable: "RevenueAccounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "TreatmentPosSettings",
                schema: "aispa",
                columns: table => new
                {
                    TreatmentId = table.Column<Guid>(type: "uuid", nullable: false),
                    TaxCategory = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    RevenueAccountId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TreatmentPosSettings", x => x.TreatmentId);
                    table.ForeignKey(
                        name: "FK_TreatmentPosSettings_RevenueAccounts_RevenueAccountId",
                        column: x => x.RevenueAccountId,
                        principalSchema: "aispa",
                        principalTable: "RevenueAccounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TreatmentPosSettings_Treatments_TreatmentId",
                        column: x => x.TreatmentId,
                        principalSchema: "aispa",
                        principalTable: "Treatments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "BillLines",
                schema: "aispa",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    BillId = table.Column<Guid>(type: "uuid", nullable: false),
                    Position = table.Column<int>(type: "integer", nullable: false),
                    Kind = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    Description = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    Quantity = table.Column<int>(type: "integer", nullable: false),
                    UnitPrice = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: false),
                    Discount = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: false),
                    PaidAmount = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: false),
                    TaxCategory = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    TaxPercent = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: false),
                    RevenueAccountId = table.Column<Guid>(type: "uuid", nullable: false),
                    AppointmentId = table.Column<Guid>(type: "uuid", nullable: true),
                    TreatmentId = table.Column<Guid>(type: "uuid", nullable: true),
                    ArticleId = table.Column<Guid>(type: "uuid", nullable: true),
                    VoucherKind = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BillLines", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BillLines_Appointments_AppointmentId",
                        column: x => x.AppointmentId,
                        principalSchema: "aispa",
                        principalTable: "Appointments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_BillLines_Bills_BillId",
                        column: x => x.BillId,
                        principalSchema: "aispa",
                        principalTable: "Bills",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_BillLines_RevenueAccounts_RevenueAccountId",
                        column: x => x.RevenueAccountId,
                        principalSchema: "aispa",
                        principalTable: "RevenueAccounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ReceiptLine",
                schema: "aispa",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ReceiptId = table.Column<Guid>(type: "uuid", nullable: false),
                    Position = table.Column<int>(type: "integer", nullable: false),
                    Description = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    Quantity = table.Column<int>(type: "integer", nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: false),
                    TaxCategory = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    TaxPercent = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: false),
                    RevenueAccountId = table.Column<Guid>(type: "uuid", nullable: false),
                    BillLineId = table.Column<Guid>(type: "uuid", nullable: true),
                    AppointmentId = table.Column<Guid>(type: "uuid", nullable: true),
                    VoucherId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReceiptLine", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ReceiptLine_Receipts_ReceiptId",
                        column: x => x.ReceiptId,
                        principalSchema: "aispa",
                        principalTable: "Receipts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ReceiptLine_RevenueAccounts_RevenueAccountId",
                        column: x => x.RevenueAccountId,
                        principalSchema: "aispa",
                        principalTable: "RevenueAccounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ReceiptPayment",
                schema: "aispa",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ReceiptId = table.Column<Guid>(type: "uuid", nullable: false),
                    Method = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: false),
                    VoucherId = table.Column<Guid>(type: "uuid", nullable: true),
                    Reference = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReceiptPayment", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ReceiptPayment_Receipts_ReceiptId",
                        column: x => x.ReceiptId,
                        principalSchema: "aispa",
                        principalTable: "Receipts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.InsertData(
                schema: "aispa",
                table: "RevenueAccounts",
                columns: new[] { "Id", "Code", "CostCenter", "Name" },
                values: new object[,]
                {
                    { new Guid("0199f000-0000-7000-8000-000000003600"), "3600", null, "Mehrzweckgutscheine (nicht steuerbar)" },
                    { new Guid("0199f000-0000-7000-8000-000000004000"), "4000", null, "Erlöse Behandlungen" },
                    { new Guid("0199f000-0000-7000-8000-000000004100"), "4100", null, "Erlöse Produkte" }
                });

            migrationBuilder.CreateIndex(
                name: "IX_Articles_RevenueAccountId",
                schema: "aispa",
                table: "Articles",
                column: "RevenueAccountId");

            migrationBuilder.CreateIndex(
                name: "IX_BillLines_AppointmentId",
                schema: "aispa",
                table: "BillLines",
                column: "AppointmentId",
                unique: true,
                filter: "\"AppointmentId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_BillLines_BillId",
                schema: "aispa",
                table: "BillLines",
                column: "BillId");

            migrationBuilder.CreateIndex(
                name: "IX_BillLines_RevenueAccountId",
                schema: "aispa",
                table: "BillLines",
                column: "RevenueAccountId");

            migrationBuilder.CreateIndex(
                name: "IX_Bills_CustomerId",
                schema: "aispa",
                table: "Bills",
                column: "CustomerId");

            migrationBuilder.CreateIndex(
                name: "IX_Bills_RegisterId_Status",
                schema: "aispa",
                table: "Bills",
                columns: new[] { "RegisterId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_CashMovements_RegisterId_At",
                schema: "aispa",
                table: "CashMovements",
                columns: new[] { "RegisterId", "At" });

            migrationBuilder.CreateIndex(
                name: "IX_CashRegisters_Code",
                schema: "aispa",
                table: "CashRegisters",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CashRegisters_LocationId",
                schema: "aispa",
                table: "CashRegisters",
                column: "LocationId");

            migrationBuilder.CreateIndex(
                name: "IX_DayClosings_RegisterId_Until",
                schema: "aispa",
                table: "DayClosings",
                columns: new[] { "RegisterId", "Until" });

            migrationBuilder.CreateIndex(
                name: "IX_ReceiptLine_AppointmentId",
                schema: "aispa",
                table: "ReceiptLine",
                column: "AppointmentId");

            migrationBuilder.CreateIndex(
                name: "IX_ReceiptLine_ReceiptId",
                schema: "aispa",
                table: "ReceiptLine",
                column: "ReceiptId");

            migrationBuilder.CreateIndex(
                name: "IX_ReceiptLine_RevenueAccountId",
                schema: "aispa",
                table: "ReceiptLine",
                column: "RevenueAccountId");

            migrationBuilder.CreateIndex(
                name: "IX_ReceiptPayment_ReceiptId",
                schema: "aispa",
                table: "ReceiptPayment",
                column: "ReceiptId");

            migrationBuilder.CreateIndex(
                name: "IX_ReceiptPayment_VoucherId",
                schema: "aispa",
                table: "ReceiptPayment",
                column: "VoucherId");

            migrationBuilder.CreateIndex(
                name: "IX_Receipts_BillId",
                schema: "aispa",
                table: "Receipts",
                column: "BillId");

            migrationBuilder.CreateIndex(
                name: "IX_Receipts_RegisterId_IssuedAt",
                schema: "aispa",
                table: "Receipts",
                columns: new[] { "RegisterId", "IssuedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_Receipts_RegisterId_Number",
                schema: "aispa",
                table: "Receipts",
                columns: new[] { "RegisterId", "Number" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Receipts_VoidsReceiptId",
                schema: "aispa",
                table: "Receipts",
                column: "VoidsReceiptId",
                unique: true,
                filter: "\"VoidsReceiptId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_RevenueAccounts_Code",
                schema: "aispa",
                table: "RevenueAccounts",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TreatmentPosSettings_RevenueAccountId",
                schema: "aispa",
                table: "TreatmentPosSettings",
                column: "RevenueAccountId");

            migrationBuilder.CreateIndex(
                name: "IX_Vouchers_Code",
                schema: "aispa",
                table: "Vouchers",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Vouchers_TreatmentId",
                schema: "aispa",
                table: "Vouchers",
                column: "TreatmentId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Articles",
                schema: "aispa");

            migrationBuilder.DropTable(
                name: "BillLines",
                schema: "aispa");

            migrationBuilder.DropTable(
                name: "CashMovements",
                schema: "aispa");

            migrationBuilder.DropTable(
                name: "DayClosings",
                schema: "aispa");

            migrationBuilder.DropTable(
                name: "ReceiptLine",
                schema: "aispa");

            migrationBuilder.DropTable(
                name: "ReceiptPayment",
                schema: "aispa");

            migrationBuilder.DropTable(
                name: "TestSignatureKey",
                schema: "aispa");

            migrationBuilder.DropTable(
                name: "TreatmentPosSettings",
                schema: "aispa");

            migrationBuilder.DropTable(
                name: "Vouchers",
                schema: "aispa");

            migrationBuilder.DropTable(
                name: "Bills",
                schema: "aispa");

            migrationBuilder.DropTable(
                name: "Receipts",
                schema: "aispa");

            migrationBuilder.DropTable(
                name: "RevenueAccounts",
                schema: "aispa");

            migrationBuilder.DropTable(
                name: "CashRegisters",
                schema: "aispa");
        }
    }
}
