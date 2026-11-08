using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AispaStudio.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class KalenderAbos : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CalendarSubscriptions",
                schema: "aispa",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    LocationId = table.Column<Guid>(type: "uuid", nullable: true),
                    Name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    Url = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    Kind = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Region = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    Preset = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    LastFetchAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    LastResult = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    LastError = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    NextCheckAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CalendarSubscriptions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CalendarSubscriptions_Locations_LocationId",
                        column: x => x.LocationId,
                        principalSchema: "aispa",
                        principalTable: "Locations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CalendarSubscriptions_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalSchema: "aispa",
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CalendarSubscriptionDecisions",
                schema: "aispa",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SubscriptionId = table.Column<Guid>(type: "uuid", nullable: false),
                    Date = table.Column<DateOnly>(type: "date", nullable: false),
                    Decision = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    UserId = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: false),
                    DecidedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CalendarSubscriptionDecisions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CalendarSubscriptionDecisions_CalendarSubscriptions_Subscri~",
                        column: x => x.SubscriptionId,
                        principalSchema: "aispa",
                        principalTable: "CalendarSubscriptions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CalendarSubscriptionDecisions_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalSchema: "aispa",
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CalendarSubscriptionEntries",
                schema: "aispa",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SubscriptionId = table.Column<Guid>(type: "uuid", nullable: false),
                    From = table.Column<DateOnly>(type: "date", nullable: false),
                    Until = table.Column<DateOnly>(type: "date", nullable: false),
                    Title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    AllDay = table.Column<bool>(type: "boolean", nullable: false),
                    Scope = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    RegionNames = table.Column<string>(type: "character varying(400)", maxLength: 400, nullable: true),
                    Source = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CalendarSubscriptionEntries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CalendarSubscriptionEntries_CalendarSubscriptions_Subscript~",
                        column: x => x.SubscriptionId,
                        principalSchema: "aispa",
                        principalTable: "CalendarSubscriptions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CalendarSubscriptionEntries_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalSchema: "aispa",
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CalendarSubscriptionHidden",
                schema: "aispa",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SubscriptionId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CalendarSubscriptionHidden", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CalendarSubscriptionHidden_CalendarSubscriptions_Subscripti~",
                        column: x => x.SubscriptionId,
                        principalSchema: "aispa",
                        principalTable: "CalendarSubscriptions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CalendarSubscriptionHidden_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalSchema: "aispa",
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CalendarSubscriptionNotices",
                schema: "aispa",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SubscriptionId = table.Column<Guid>(type: "uuid", nullable: true),
                    LocationId = table.Column<Guid>(type: "uuid", nullable: true),
                    Kind = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Key = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    Title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Text = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ResolvedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ForLeadership = table.Column<bool>(type: "boolean", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CalendarSubscriptionNotices", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CalendarSubscriptionNotices_CalendarSubscriptions_Subscript~",
                        column: x => x.SubscriptionId,
                        principalSchema: "aispa",
                        principalTable: "CalendarSubscriptions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CalendarSubscriptionNotices_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalSchema: "aispa",
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CalendarSubscriptionDecisions_SubscriptionId_Date",
                schema: "aispa",
                table: "CalendarSubscriptionDecisions",
                columns: new[] { "SubscriptionId", "Date" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CalendarSubscriptionDecisions_TenantId",
                schema: "aispa",
                table: "CalendarSubscriptionDecisions",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_CalendarSubscriptionEntries_SubscriptionId_From",
                schema: "aispa",
                table: "CalendarSubscriptionEntries",
                columns: new[] { "SubscriptionId", "From" });

            migrationBuilder.CreateIndex(
                name: "IX_CalendarSubscriptionEntries_TenantId",
                schema: "aispa",
                table: "CalendarSubscriptionEntries",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_CalendarSubscriptionHidden_SubscriptionId_UserId",
                schema: "aispa",
                table: "CalendarSubscriptionHidden",
                columns: new[] { "SubscriptionId", "UserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CalendarSubscriptionHidden_TenantId",
                schema: "aispa",
                table: "CalendarSubscriptionHidden",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_CalendarSubscriptionNotices_Key",
                schema: "aispa",
                table: "CalendarSubscriptionNotices",
                column: "Key");

            migrationBuilder.CreateIndex(
                name: "IX_CalendarSubscriptionNotices_SubscriptionId",
                schema: "aispa",
                table: "CalendarSubscriptionNotices",
                column: "SubscriptionId");

            migrationBuilder.CreateIndex(
                name: "IX_CalendarSubscriptionNotices_TenantId",
                schema: "aispa",
                table: "CalendarSubscriptionNotices",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_CalendarSubscriptions_LocationId",
                schema: "aispa",
                table: "CalendarSubscriptions",
                column: "LocationId");

            migrationBuilder.CreateIndex(
                name: "IX_CalendarSubscriptions_NextCheckAt",
                schema: "aispa",
                table: "CalendarSubscriptions",
                column: "NextCheckAt");

            migrationBuilder.CreateIndex(
                name: "IX_CalendarSubscriptions_TenantId",
                schema: "aispa",
                table: "CalendarSubscriptions",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_CalendarSubscriptions_TenantId_Preset",
                schema: "aispa",
                table: "CalendarSubscriptions",
                columns: new[] { "TenantId", "Preset" },
                unique: true);

            // Jedes bestehende Spa bekommt die drei Feiertags-Abos nach Land (ADR 0075); aktiv ist das Abo des Landes des Spas.
            // Die Einträge holt der Hintergrundjob beim ersten Lauf, die Migration ruft nichts ab.
            migrationBuilder.Sql("""
                INSERT INTO aispa."CalendarSubscriptions"
                    ("Id", "TenantId", "Name", "Url", "Kind", "Region", "IsActive", "Preset", "CreatedAt", "LastResult")
                SELECT gen_random_uuid(), t."Id", p."Name", p."Url", 'PublicHoliday', p."Country", t."Country" = p."Country", p."Country", now(), 'Never'
                FROM aispa."Tenants" t
                CROSS JOIN (VALUES
                    ('AT', 'Feiertage Österreich', 'https://calendar.google.com/calendar/ical/de.austrian%23holiday%40group.v.calendar.google.com/public/basic.ics'),
                    ('DE', 'Feiertage Deutschland', 'https://calendar.google.com/calendar/ical/de.german%23holiday%40group.v.calendar.google.com/public/basic.ics'),
                    ('CH', 'Feiertage Schweiz', 'https://calendar.google.com/calendar/ical/de.ch%23holiday%40group.v.calendar.google.com/public/basic.ics')
                ) AS p("Country", "Name", "Url")
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CalendarSubscriptionDecisions",
                schema: "aispa");

            migrationBuilder.DropTable(
                name: "CalendarSubscriptionEntries",
                schema: "aispa");

            migrationBuilder.DropTable(
                name: "CalendarSubscriptionHidden",
                schema: "aispa");

            migrationBuilder.DropTable(
                name: "CalendarSubscriptionNotices",
                schema: "aispa");

            migrationBuilder.DropTable(
                name: "CalendarSubscriptions",
                schema: "aispa");
        }
    }
}
