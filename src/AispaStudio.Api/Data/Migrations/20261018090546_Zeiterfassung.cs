using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AispaStudio.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class Zeiterfassung : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "TimeRecords",
                schema: "aispa",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    EmployeeId = table.Column<Guid>(type: "uuid", nullable: false),
                    LocationId = table.Column<Guid>(type: "uuid", nullable: false),
                    Start = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    End = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    Source = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TimeRecords", x => x.Id);
                    table.CheckConstraint("CK_TimeRecords_Order", "\"End\" IS NULL OR \"Start\" < \"End\"");
                    table.ForeignKey(
                        name: "FK_TimeRecords_Employees_EmployeeId",
                        column: x => x.EmployeeId,
                        principalSchema: "aispa",
                        principalTable: "Employees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TimeRecords_Locations_LocationId",
                        column: x => x.LocationId,
                        principalSchema: "aispa",
                        principalTable: "Locations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TimeRecords_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalSchema: "aispa",
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "TimeRecordChanges",
                schema: "aispa",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TimeRecordId = table.Column<Guid>(type: "uuid", nullable: false),
                    ChangedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ChangedByUserId = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: false),
                    ChangedByName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Action = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    OldStart = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    OldEnd = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    NewStart = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    NewEnd = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    Reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TimeRecordChanges", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TimeRecordChanges_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalSchema: "aispa",
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TimeRecordChanges_TimeRecords_TimeRecordId",
                        column: x => x.TimeRecordId,
                        principalSchema: "aispa",
                        principalTable: "TimeRecords",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TimeRecordChanges_TenantId",
                schema: "aispa",
                table: "TimeRecordChanges",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_TimeRecordChanges_TimeRecordId",
                schema: "aispa",
                table: "TimeRecordChanges",
                column: "TimeRecordId");

            migrationBuilder.CreateIndex(
                name: "IX_TimeRecords_EmployeeId_Start",
                schema: "aispa",
                table: "TimeRecords",
                columns: new[] { "EmployeeId", "Start" });

            migrationBuilder.CreateIndex(
                name: "IX_TimeRecords_LocationId_Start",
                schema: "aispa",
                table: "TimeRecords",
                columns: new[] { "LocationId", "Start" });

            migrationBuilder.CreateIndex(
                name: "IX_TimeRecords_Running",
                schema: "aispa",
                table: "TimeRecords",
                column: "EmployeeId",
                unique: true,
                filter: "\"End\" IS NULL AND NOT \"IsDeleted\"");

            migrationBuilder.CreateIndex(
                name: "IX_TimeRecords_TenantId",
                schema: "aispa",
                table: "TimeRecords",
                column: "TenantId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TimeRecordChanges",
                schema: "aispa");

            migrationBuilder.DropTable(
                name: "TimeRecords",
                schema: "aispa");
        }
    }
}
