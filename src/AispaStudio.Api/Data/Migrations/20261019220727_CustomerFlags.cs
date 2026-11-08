using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AispaStudio.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class CustomerFlags : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CustomerFlags",
                schema: "aispa",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CustomerId = table.Column<Guid>(type: "uuid", nullable: false),
                    Kind = table.Column<string>(type: "character varying(24)", maxLength: 24, nullable: false),
                    EmployeeId = table.Column<Guid>(type: "uuid", nullable: true),
                    Reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedByUserId = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: true),
                    RemovedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    RemovedByUserId = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: true),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CustomerFlags", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CustomerFlags_Customers_CustomerId",
                        column: x => x.CustomerId,
                        principalSchema: "aispa",
                        principalTable: "Customers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CustomerFlags_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalSchema: "aispa",
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CustomerFlags_CustomerId",
                schema: "aispa",
                table: "CustomerFlags",
                column: "CustomerId");

            migrationBuilder.CreateIndex(
                name: "IX_CustomerFlags_TenantId",
                schema: "aispa",
                table: "CustomerFlags",
                column: "TenantId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CustomerFlags",
                schema: "aispa");
        }
    }
}
