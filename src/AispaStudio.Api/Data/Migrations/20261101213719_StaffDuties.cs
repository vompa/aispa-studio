using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AispaStudio.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class StaffDuties : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "DutyTypes",
                schema: "hr",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Key = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    RequiredCertificate = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    MinimumOnDuty = table.Column<int>(type: "integer", nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DutyTypes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DutyTypes_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalSchema: "aispa",
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "StaffCertificates",
                schema: "hr",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    EmployeeId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    IssuedOn = table.Column<DateOnly>(type: "date", nullable: true),
                    ValidUntil = table.Column<DateOnly>(type: "date", nullable: true),
                    IssuedBy = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    RemindDays = table.Column<int>(type: "integer", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StaffCertificates", x => x.Id);
                    table.ForeignKey(
                        name: "FK_StaffCertificates_Employees_EmployeeId",
                        column: x => x.EmployeeId,
                        principalSchema: "aispa",
                        principalTable: "Employees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_StaffCertificates_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalSchema: "aispa",
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "StaffDuties",
                schema: "hr",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    EmployeeId = table.Column<Guid>(type: "uuid", nullable: false),
                    DutyTypeId = table.Column<Guid>(type: "uuid", nullable: false),
                    ValidFrom = table.Column<DateOnly>(type: "date", nullable: false),
                    ValidUntil = table.Column<DateOnly>(type: "date", nullable: true),
                    DeputyId = table.Column<Guid>(type: "uuid", nullable: true),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StaffDuties", x => x.Id);
                    table.ForeignKey(
                        name: "FK_StaffDuties_DutyTypes_DutyTypeId",
                        column: x => x.DutyTypeId,
                        principalSchema: "hr",
                        principalTable: "DutyTypes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_StaffDuties_Employees_DeputyId",
                        column: x => x.DeputyId,
                        principalSchema: "aispa",
                        principalTable: "Employees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_StaffDuties_Employees_EmployeeId",
                        column: x => x.EmployeeId,
                        principalSchema: "aispa",
                        principalTable: "Employees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_StaffDuties_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalSchema: "aispa",
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "StaffCertificateScans",
                schema: "hr",
                columns: table => new
                {
                    CertificateId = table.Column<Guid>(type: "uuid", nullable: false),
                    EmployeeId = table.Column<Guid>(type: "uuid", nullable: false),
                    FileName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    ContentType = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Content = table.Column<byte[]>(type: "bytea", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StaffCertificateScans", x => x.CertificateId);
                    table.ForeignKey(
                        name: "FK_StaffCertificateScans_StaffCertificates_CertificateId",
                        column: x => x.CertificateId,
                        principalSchema: "hr",
                        principalTable: "StaffCertificates",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_StaffCertificateScans_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalSchema: "aispa",
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DutyTypes_TenantId",
                schema: "hr",
                table: "DutyTypes",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_DutyTypes_TenantId_Key",
                schema: "hr",
                table: "DutyTypes",
                columns: new[] { "TenantId", "Key" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_StaffCertificates_EmployeeId",
                schema: "hr",
                table: "StaffCertificates",
                column: "EmployeeId");

            migrationBuilder.CreateIndex(
                name: "IX_StaffCertificates_TenantId",
                schema: "hr",
                table: "StaffCertificates",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_StaffCertificateScans_TenantId",
                schema: "hr",
                table: "StaffCertificateScans",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_StaffDuties_DeputyId",
                schema: "hr",
                table: "StaffDuties",
                column: "DeputyId");

            migrationBuilder.CreateIndex(
                name: "IX_StaffDuties_DutyTypeId",
                schema: "hr",
                table: "StaffDuties",
                column: "DutyTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_StaffDuties_EmployeeId",
                schema: "hr",
                table: "StaffDuties",
                column: "EmployeeId");

            migrationBuilder.CreateIndex(
                name: "IX_StaffDuties_TenantId",
                schema: "hr",
                table: "StaffDuties",
                column: "TenantId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "StaffCertificateScans",
                schema: "hr");

            migrationBuilder.DropTable(
                name: "StaffDuties",
                schema: "hr");

            migrationBuilder.DropTable(
                name: "StaffCertificates",
                schema: "hr");

            migrationBuilder.DropTable(
                name: "DutyTypes",
                schema: "hr");
        }
    }
}
