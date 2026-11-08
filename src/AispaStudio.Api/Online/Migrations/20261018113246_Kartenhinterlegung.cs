using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AispaStudio.Api.Online.Migrations
{
    /// <inheritdoc />
    public partial class Kartenhinterlegung : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CardGuarantees",
                schema: "online",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AppointmentId = table.Column<Guid>(type: "uuid", nullable: false),
                    LocationId = table.Column<Guid>(type: "uuid", nullable: false),
                    CustomerId = table.Column<Guid>(type: "uuid", nullable: false),
                    Email = table.Column<string>(type: "character varying(254)", maxLength: 254, nullable: false),
                    Language = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: false),
                    FeePercent = table.Column<int>(type: "integer", nullable: false),
                    FeeAmount = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: false),
                    Currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    AcceptedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    DeadlineHours = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    Provider = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Attempts = table.Column<int>(type: "integer", nullable: false),
                    SessionReference = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    SetupUrl = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: true),
                    CardReference = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    ProviderCustomer = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    SavedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ChargedBy = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    ChargeReference = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    BillId = table.Column<Guid>(type: "uuid", nullable: true),
                    ReceiptId = table.Column<Guid>(type: "uuid", nullable: true),
                    ChargedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    Error = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CardGuarantees", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CardGuarantees_AppointmentId",
                schema: "online",
                table: "CardGuarantees",
                column: "AppointmentId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CardGuarantees",
                schema: "online");
        }
    }
}
