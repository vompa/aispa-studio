using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AispaStudio.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class TseExport : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "TestTseLog",
                schema: "aispa",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ClientId = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    TransactionNumber = table.Column<long>(type: "bigint", nullable: false),
                    SignatureCounter = table.Column<long>(type: "bigint", nullable: false),
                    Start = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    End = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ProcessType = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    ProcessData = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    Signature = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TestTseLog", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TestTseLog_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalSchema: "aispa",
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "TseExport",
                schema: "aispa",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    RegisterId = table.Column<Guid>(type: "uuid", nullable: false),
                    From = table.Column<DateOnly>(type: "date", nullable: false),
                    Until = table.Column<DateOnly>(type: "date", nullable: false),
                    Status = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    RequestedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    RequestedBy = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: true),
                    CompletedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    FileName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Content = table.Column<byte[]>(type: "bytea", nullable: true),
                    Error = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TseExport", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TseExport_CashRegisters_RegisterId",
                        column: x => x.RegisterId,
                        principalSchema: "aispa",
                        principalTable: "CashRegisters",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TseExport_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalSchema: "aispa",
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TestTseLog_ClientId_End",
                schema: "aispa",
                table: "TestTseLog",
                columns: new[] { "ClientId", "End" });

            migrationBuilder.CreateIndex(
                name: "IX_TestTseLog_TenantId",
                schema: "aispa",
                table: "TestTseLog",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_TseExport_RegisterId_RequestedAt",
                schema: "aispa",
                table: "TseExport",
                columns: new[] { "RegisterId", "RequestedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_TseExport_TenantId",
                schema: "aispa",
                table: "TseExport",
                column: "TenantId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TestTseLog",
                schema: "aispa");

            migrationBuilder.DropTable(
                name: "TseExport",
                schema: "aispa");
        }
    }
}
