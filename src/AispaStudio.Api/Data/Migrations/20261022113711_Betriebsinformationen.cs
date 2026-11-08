using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AispaStudio.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class Betriebsinformationen : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "BusinessContacts",
                schema: "aispa",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Category = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Designation = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    Company = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    ContactPerson = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    Phone = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    Mobile = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    Email = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Address = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    Responsibility = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    CustomerNumber = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: true),
                    Insurer = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    PolicyNumber = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: true),
                    Coverage = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    ClaimsHotline = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    AllLocations = table.Column<bool>(type: "boolean", nullable: false),
                    Notes = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    Visibility = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    ArchivedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BusinessContacts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BusinessContacts_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalSchema: "aispa",
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "BusinessContactLocations",
                schema: "aispa",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    BusinessContactId = table.Column<Guid>(type: "uuid", nullable: false),
                    LocationId = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BusinessContactLocations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BusinessContactLocations_BusinessContacts_BusinessContactId",
                        column: x => x.BusinessContactId,
                        principalSchema: "aispa",
                        principalTable: "BusinessContacts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_BusinessContactLocations_Locations_LocationId",
                        column: x => x.LocationId,
                        principalSchema: "aispa",
                        principalTable: "Locations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_BusinessContactLocations_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalSchema: "aispa",
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_BusinessContactLocations_BusinessContactId_LocationId",
                schema: "aispa",
                table: "BusinessContactLocations",
                columns: new[] { "BusinessContactId", "LocationId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_BusinessContactLocations_LocationId",
                schema: "aispa",
                table: "BusinessContactLocations",
                column: "LocationId");

            migrationBuilder.CreateIndex(
                name: "IX_BusinessContactLocations_TenantId",
                schema: "aispa",
                table: "BusinessContactLocations",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_BusinessContacts_Category",
                schema: "aispa",
                table: "BusinessContacts",
                column: "Category");

            migrationBuilder.CreateIndex(
                name: "IX_BusinessContacts_TenantId",
                schema: "aispa",
                table: "BusinessContacts",
                column: "TenantId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "BusinessContactLocations",
                schema: "aispa");

            migrationBuilder.DropTable(
                name: "BusinessContacts",
                schema: "aispa");
        }
    }
}
