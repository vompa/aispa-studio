using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AispaStudio.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class Schliesszeiten : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "CancelledByBusiness",
                schema: "aispa",
                table: "Appointments",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "Closures",
                schema: "aispa",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    LocationId = table.Column<Guid>(type: "uuid", nullable: false),
                    FromDate = table.Column<DateOnly>(type: "date", nullable: false),
                    FromTime = table.Column<TimeOnly>(type: "time without time zone", nullable: true),
                    UntilDate = table.Column<DateOnly>(type: "date", nullable: false),
                    UntilTime = table.Column<TimeOnly>(type: "time without time zone", nullable: true),
                    Kind = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    InternalReason = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    GuestText = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    CreatedByUserId = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: false),
                    CreatedByName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Closures", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Closures_Locations_LocationId",
                        column: x => x.LocationId,
                        principalSchema: "aispa",
                        principalTable: "Locations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Closures_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalSchema: "aispa",
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ClosureNotices",
                schema: "aispa",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ClosureId = table.Column<Guid>(type: "uuid", nullable: false),
                    AppointmentId = table.Column<Guid>(type: "uuid", nullable: false),
                    Channel = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    State = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    Error = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    Subject = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Text = table.Column<string>(type: "character varying(2400)", maxLength: 2400, nullable: false),
                    AiWritten = table.Column<bool>(type: "boolean", nullable: false),
                    OfferedRebooking = table.Column<bool>(type: "boolean", nullable: false),
                    SentByUserId = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: false),
                    SentByName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    SentAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ClosureNotices", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ClosureNotices_Appointments_AppointmentId",
                        column: x => x.AppointmentId,
                        principalSchema: "aispa",
                        principalTable: "Appointments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ClosureNotices_Closures_ClosureId",
                        column: x => x.ClosureId,
                        principalSchema: "aispa",
                        principalTable: "Closures",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ClosureNotices_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalSchema: "aispa",
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ClosureNotices_AppointmentId",
                schema: "aispa",
                table: "ClosureNotices",
                column: "AppointmentId");

            migrationBuilder.CreateIndex(
                name: "IX_ClosureNotices_ClosureId_AppointmentId",
                schema: "aispa",
                table: "ClosureNotices",
                columns: new[] { "ClosureId", "AppointmentId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ClosureNotices_TenantId",
                schema: "aispa",
                table: "ClosureNotices",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_Closures_LocationId_FromDate_UntilDate",
                schema: "aispa",
                table: "Closures",
                columns: new[] { "LocationId", "FromDate", "UntilDate" });

            migrationBuilder.CreateIndex(
                name: "IX_Closures_TenantId",
                schema: "aispa",
                table: "Closures",
                column: "TenantId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ClosureNotices",
                schema: "aispa");

            migrationBuilder.DropTable(
                name: "Closures",
                schema: "aispa");

            migrationBuilder.DropColumn(
                name: "CancelledByBusiness",
                schema: "aispa",
                table: "Appointments");
        }
    }
}
