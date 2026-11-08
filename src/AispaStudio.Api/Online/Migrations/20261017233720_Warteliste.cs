using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AispaStudio.Api.Online.Migrations
{
    /// <inheritdoc />
    public partial class Warteliste : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "WaitlistEntryId",
                schema: "online",
                table: "AccessLinks",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "WaitlistEntries",
                schema: "online",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CustomerId = table.Column<Guid>(type: "uuid", nullable: false),
                    CustomerAccountId = table.Column<Guid>(type: "uuid", nullable: true),
                    LocationId = table.Column<Guid>(type: "uuid", nullable: false),
                    TreatmentId = table.Column<Guid>(type: "uuid", nullable: false),
                    From = table.Column<DateOnly>(type: "date", nullable: false),
                    Until = table.Column<DateOnly>(type: "date", nullable: false),
                    DayPart = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    Language = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    PrivacyAcceptedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    PrivacyNoticeVersion = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    ClosedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    AppointmentId = table.Column<Guid>(type: "uuid", nullable: true),
                    OfferCount = table.Column<int>(type: "integer", nullable: false),
                    LastOfferedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    LastOfferedDate = table.Column<DateOnly>(type: "date", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WaitlistEntries", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_WaitlistEntries_CustomerId_Status",
                schema: "online",
                table: "WaitlistEntries",
                columns: new[] { "CustomerId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_WaitlistEntries_LocationId_Status_From",
                schema: "online",
                table: "WaitlistEntries",
                columns: new[] { "LocationId", "Status", "From" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "WaitlistEntries",
                schema: "online");

            migrationBuilder.DropColumn(
                name: "WaitlistEntryId",
                schema: "online",
                table: "AccessLinks");
        }
    }
}
