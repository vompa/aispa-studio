using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AispaStudio.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class Dienstplanentwurf : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "RosterDrafts",
                schema: "aispa",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    EmployeeId = table.Column<Guid>(type: "uuid", nullable: false),
                    Date = table.Column<DateOnly>(type: "date", nullable: false),
                    LocationId = table.Column<Guid>(type: "uuid", nullable: false),
                    Reset = table.Column<bool>(type: "boolean", nullable: false),
                    Note = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedByUserId = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: true),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RosterDrafts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RosterDrafts_Employees_EmployeeId",
                        column: x => x.EmployeeId,
                        principalSchema: "aispa",
                        principalTable: "Employees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_RosterDrafts_Locations_LocationId",
                        column: x => x.LocationId,
                        principalSchema: "aispa",
                        principalTable: "Locations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_RosterDrafts_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalSchema: "aispa",
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "RosterPublications",
                schema: "aispa",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    LocationId = table.Column<Guid>(type: "uuid", nullable: false),
                    From = table.Column<DateOnly>(type: "date", nullable: false),
                    Until = table.Column<DateOnly>(type: "date", nullable: false),
                    Days = table.Column<int>(type: "integer", nullable: false),
                    PublishedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    PublishedByUserId = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: true),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RosterPublications", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RosterPublications_Locations_LocationId",
                        column: x => x.LocationId,
                        principalSchema: "aispa",
                        principalTable: "Locations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_RosterPublications_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalSchema: "aispa",
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "RosterDraftEntry",
                schema: "aispa",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    RosterDraftId = table.Column<Guid>(type: "uuid", nullable: false),
                    Kind = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    LocationId = table.Column<Guid>(type: "uuid", nullable: true),
                    Start = table.Column<TimeOnly>(type: "time without time zone", nullable: true),
                    End = table.Column<TimeOnly>(type: "time without time zone", nullable: true),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RosterDraftEntry", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RosterDraftEntry_Locations_LocationId",
                        column: x => x.LocationId,
                        principalSchema: "aispa",
                        principalTable: "Locations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RosterDraftEntry_RosterDrafts_RosterDraftId",
                        column: x => x.RosterDraftId,
                        principalSchema: "aispa",
                        principalTable: "RosterDrafts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_RosterDraftEntry_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalSchema: "aispa",
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_RosterDraftEntry_LocationId",
                schema: "aispa",
                table: "RosterDraftEntry",
                column: "LocationId");

            migrationBuilder.CreateIndex(
                name: "IX_RosterDraftEntry_RosterDraftId",
                schema: "aispa",
                table: "RosterDraftEntry",
                column: "RosterDraftId");

            migrationBuilder.CreateIndex(
                name: "IX_RosterDraftEntry_TenantId",
                schema: "aispa",
                table: "RosterDraftEntry",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_RosterDrafts_EmployeeId_Date",
                schema: "aispa",
                table: "RosterDrafts",
                columns: new[] { "EmployeeId", "Date" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RosterDrafts_LocationId_Date",
                schema: "aispa",
                table: "RosterDrafts",
                columns: new[] { "LocationId", "Date" });

            migrationBuilder.CreateIndex(
                name: "IX_RosterDrafts_TenantId",
                schema: "aispa",
                table: "RosterDrafts",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_RosterPublications_LocationId_PublishedAt",
                schema: "aispa",
                table: "RosterPublications",
                columns: new[] { "LocationId", "PublishedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_RosterPublications_TenantId",
                schema: "aispa",
                table: "RosterPublications",
                column: "TenantId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "RosterDraftEntry",
                schema: "aispa");

            migrationBuilder.DropTable(
                name: "RosterPublications",
                schema: "aispa");

            migrationBuilder.DropTable(
                name: "RosterDrafts",
                schema: "aispa");
        }
    }
}
