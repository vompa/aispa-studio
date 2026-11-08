using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AispaStudio.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class TestTse : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "TestTseClientState",
                schema: "aispa",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ClientId = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    PrivateKey = table.Column<string>(type: "text", nullable: false),
                    PublicKey = table.Column<string>(type: "text", nullable: false),
                    TransactionCounter = table.Column<long>(type: "bigint", nullable: false),
                    SignatureCounter = table.Column<long>(type: "bigint", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TestTseClientState", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TestTseClientState_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalSchema: "aispa",
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TestTseClientState_TenantId",
                schema: "aispa",
                table: "TestTseClientState",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_TestTseClientState_TenantId_ClientId",
                schema: "aispa",
                table: "TestTseClientState",
                columns: new[] { "TenantId", "ClientId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TestTseClientState",
                schema: "aispa");
        }
    }
}
