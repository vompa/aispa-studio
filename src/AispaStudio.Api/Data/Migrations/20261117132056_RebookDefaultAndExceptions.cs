using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AispaStudio.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class RebookDefaultAndExceptions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<int>(
                name: "Days",
                schema: "aispa",
                table: "TreatmentRebookIntervals",
                type: "integer",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "integer");

            migrationBuilder.AddColumn<int>(
                name: "DefaultDays",
                schema: "aispa",
                table: "RebookSettings",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "CategoryRebookIntervals",
                schema: "aispa",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Category = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    Days = table.Column<int>(type: "integer", nullable: true),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CategoryRebookIntervals", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CategoryRebookIntervals_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalSchema: "aispa",
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CategoryRebookIntervals_TenantId",
                schema: "aispa",
                table: "CategoryRebookIntervals",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_CategoryRebookIntervals_TenantId_Category",
                schema: "aispa",
                table: "CategoryRebookIntervals",
                columns: new[] { "TenantId", "Category" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CategoryRebookIntervals",
                schema: "aispa");

            migrationBuilder.DropColumn(
                name: "DefaultDays",
                schema: "aispa",
                table: "RebookSettings");

            migrationBuilder.AlterColumn<int>(
                name: "Days",
                schema: "aispa",
                table: "TreatmentRebookIntervals",
                type: "integer",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "integer",
                oldNullable: true);
        }
    }
}
