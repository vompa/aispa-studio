using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AispaStudio.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class Terminannahme : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CalendarFeeds",
                schema: "aispa",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    LocationId = table.Column<Guid>(type: "uuid", nullable: false),
                    EmployeeId = table.Column<Guid>(type: "uuid", nullable: true),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Channel = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: true),
                    Token = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CalendarFeeds", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ChannelBookings",
                schema: "aispa",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Channel = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    ExternalId = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    AppointmentId = table.Column<Guid>(type: "uuid", nullable: true),
                    InboundMailId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ChannelBookings", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ChannelMappings",
                schema: "aispa",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Channel = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    Kind = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    ExternalName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    NormalizedName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    TargetId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ChannelMappings", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "InboundMails",
                schema: "aispa",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    MessageId = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    Channel = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    From = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: false),
                    FromName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    To = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: false),
                    Subject = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    Body = table.Column<string>(type: "text", nullable: false),
                    ReceivedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    SenderVerified = table.Column<bool>(type: "boolean", nullable: false),
                    Status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    Kind = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Language = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: true),
                    Summary = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    Note = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    LocationId = table.Column<Guid>(type: "uuid", nullable: true),
                    TreatmentId = table.Column<Guid>(type: "uuid", nullable: true),
                    CustomerId = table.Column<Guid>(type: "uuid", nullable: true),
                    AppointmentId = table.Column<Guid>(type: "uuid", nullable: true),
                    GuestFirstName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    GuestLastName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    GuestPhone = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    Proposals = table.Column<string>(type: "jsonb", nullable: false),
                    ReplyStatus = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    ReplyTo = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: true),
                    ReplySubject = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    ReplyBody = table.Column<string>(type: "text", nullable: true),
                    ProcessedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    RepliedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InboundMails", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CalendarFeeds_LocationId",
                schema: "aispa",
                table: "CalendarFeeds",
                column: "LocationId");

            migrationBuilder.CreateIndex(
                name: "IX_CalendarFeeds_Token",
                schema: "aispa",
                table: "CalendarFeeds",
                column: "Token",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ChannelBookings_AppointmentId",
                schema: "aispa",
                table: "ChannelBookings",
                column: "AppointmentId");

            migrationBuilder.CreateIndex(
                name: "IX_ChannelBookings_Channel_ExternalId",
                schema: "aispa",
                table: "ChannelBookings",
                columns: new[] { "Channel", "ExternalId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ChannelMappings_Channel_Kind_NormalizedName",
                schema: "aispa",
                table: "ChannelMappings",
                columns: new[] { "Channel", "Kind", "NormalizedName" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_InboundMails_MessageId",
                schema: "aispa",
                table: "InboundMails",
                column: "MessageId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_InboundMails_Status_ReceivedAt",
                schema: "aispa",
                table: "InboundMails",
                columns: new[] { "Status", "ReceivedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CalendarFeeds",
                schema: "aispa");

            migrationBuilder.DropTable(
                name: "ChannelBookings",
                schema: "aispa");

            migrationBuilder.DropTable(
                name: "ChannelMappings",
                schema: "aispa");

            migrationBuilder.DropTable(
                name: "InboundMails",
                schema: "aispa");
        }
    }
}
