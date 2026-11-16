using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AispaStudio.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class RegisterLongOutage : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "LongOutageReenteredAt",
                schema: "aispa",
                table: "CashRegisters",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "LongOutageSince",
                schema: "aispa",
                table: "CashRegisters",
                type: "timestamp with time zone",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "LongOutageReenteredAt",
                schema: "aispa",
                table: "CashRegisters");

            migrationBuilder.DropColumn(
                name: "LongOutageSince",
                schema: "aispa",
                table: "CashRegisters");
        }
    }
}
