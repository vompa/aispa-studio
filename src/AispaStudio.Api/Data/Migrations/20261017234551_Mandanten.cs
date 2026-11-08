using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AispaStudio.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class Mandanten : Migration
    {
        private static readonly Guid ExistingTenant = new("0199f000-0000-7000-8000-00000000a000");

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_RevenueAccounts_Code",
                schema: "aispa",
                table: "RevenueAccounts");

            migrationBuilder.DropIndex(
                name: "IX_Qualifications_Name",
                schema: "aispa",
                table: "Qualifications");

            migrationBuilder.DropIndex(
                name: "IX_InboundMails_MessageId",
                schema: "aispa",
                table: "InboundMails");

            migrationBuilder.DropIndex(
                name: "IX_ChannelMappings_Channel_Kind_NormalizedName",
                schema: "aispa",
                table: "ChannelMappings");

            migrationBuilder.DropIndex(
                name: "IX_ChannelBookings_Channel_ExternalId",
                schema: "aispa",
                table: "ChannelBookings");

            migrationBuilder.DropIndex(
                name: "IX_CashRegisters_Code",
                schema: "aispa",
                table: "CashRegisters");

            migrationBuilder.AddColumn<Guid>(
                name: "TenantId",
                schema: "aispa",
                table: "Vouchers",
                type: "uuid",
                nullable: false,
                defaultValue: ExistingTenant);

            migrationBuilder.AddColumn<Guid>(
                name: "TenantId",
                schema: "aispa",
                table: "Treatments",
                type: "uuid",
                nullable: false,
                defaultValue: ExistingTenant);

            migrationBuilder.AddColumn<Guid>(
                name: "TenantId",
                schema: "aispa",
                table: "TreatmentPreparationSteps",
                type: "uuid",
                nullable: false,
                defaultValue: ExistingTenant);

            migrationBuilder.AddColumn<Guid>(
                name: "TenantId",
                schema: "aispa",
                table: "TreatmentPosSettings",
                type: "uuid",
                nullable: false,
                defaultValue: ExistingTenant);

            migrationBuilder.AddColumn<Guid>(
                name: "TenantId",
                schema: "aispa",
                table: "TreatmentMaterials",
                type: "uuid",
                nullable: false,
                defaultValue: ExistingTenant);

            migrationBuilder.AddColumn<Guid>(
                name: "TenantId",
                schema: "aispa",
                table: "TestSignatureKey",
                type: "uuid",
                nullable: false,
                defaultValue: ExistingTenant);

            migrationBuilder.AddColumn<Guid>(
                name: "TenantId",
                schema: "aispa",
                table: "StaffAccess",
                type: "uuid",
                nullable: false,
                defaultValue: ExistingTenant);

            migrationBuilder.AddColumn<Guid>(
                name: "TenantId",
                schema: "aispa",
                table: "RosterDays",
                type: "uuid",
                nullable: false,
                defaultValue: ExistingTenant);

            migrationBuilder.AddColumn<Guid>(
                name: "TenantId",
                schema: "aispa",
                table: "RosterDayEntries",
                type: "uuid",
                nullable: false,
                defaultValue: ExistingTenant);

            migrationBuilder.AddColumn<Guid>(
                name: "TenantId",
                schema: "aispa",
                table: "Rooms",
                type: "uuid",
                nullable: false,
                defaultValue: ExistingTenant);

            migrationBuilder.AddColumn<Guid>(
                name: "TenantId",
                schema: "aispa",
                table: "RoomCosts",
                type: "uuid",
                nullable: false,
                defaultValue: ExistingTenant);

            migrationBuilder.AddColumn<Guid>(
                name: "TenantId",
                schema: "aispa",
                table: "RevenueAccounts",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "TenantId",
                schema: "aispa",
                table: "ResourceBookings",
                type: "uuid",
                nullable: false,
                defaultValue: ExistingTenant);

            migrationBuilder.AddColumn<Guid>(
                name: "TenantId",
                schema: "aispa",
                table: "Receipts",
                type: "uuid",
                nullable: false,
                defaultValue: ExistingTenant);

            migrationBuilder.AddColumn<Guid>(
                name: "TenantId",
                schema: "aispa",
                table: "ReceiptPayment",
                type: "uuid",
                nullable: false,
                defaultValue: ExistingTenant);

            migrationBuilder.AddColumn<Guid>(
                name: "TenantId",
                schema: "aispa",
                table: "ReceiptLine",
                type: "uuid",
                nullable: false,
                defaultValue: ExistingTenant);

            migrationBuilder.AddColumn<Guid>(
                name: "TenantId",
                schema: "aispa",
                table: "Qualifications",
                type: "uuid",
                nullable: false,
                defaultValue: ExistingTenant);

            migrationBuilder.AddColumn<Guid>(
                name: "TenantId",
                schema: "aispa",
                table: "Locations",
                type: "uuid",
                nullable: false,
                defaultValue: ExistingTenant);

            migrationBuilder.AddColumn<Guid>(
                name: "TenantId",
                schema: "aispa",
                table: "LocationOpeningHours",
                type: "uuid",
                nullable: false,
                defaultValue: ExistingTenant);

            migrationBuilder.AddColumn<Guid>(
                name: "TenantId",
                schema: "aispa",
                table: "LocationCostSettings",
                type: "uuid",
                nullable: false,
                defaultValue: ExistingTenant);

            migrationBuilder.AddColumn<Guid>(
                name: "TenantId",
                schema: "aispa",
                table: "Invitations",
                type: "uuid",
                nullable: false,
                defaultValue: ExistingTenant);

            migrationBuilder.AddColumn<Guid>(
                name: "TenantId",
                schema: "aispa",
                table: "InboundMails",
                type: "uuid",
                nullable: false,
                defaultValue: ExistingTenant);

            migrationBuilder.AddColumn<Guid>(
                name: "TenantId",
                schema: "aispa",
                table: "Equipment",
                type: "uuid",
                nullable: false,
                defaultValue: ExistingTenant);

            migrationBuilder.AddColumn<Guid>(
                name: "TenantId",
                schema: "aispa",
                table: "EmployeeWorkingHours",
                type: "uuid",
                nullable: false,
                defaultValue: ExistingTenant);

            migrationBuilder.AddColumn<Guid>(
                name: "TenantId",
                schema: "aispa",
                table: "Employees",
                type: "uuid",
                nullable: false,
                defaultValue: ExistingTenant);

            migrationBuilder.AddColumn<Guid>(
                name: "TenantId",
                schema: "aispa",
                table: "EmployeeCosts",
                type: "uuid",
                nullable: false,
                defaultValue: ExistingTenant);

            migrationBuilder.AddColumn<Guid>(
                name: "TenantId",
                schema: "aispa",
                table: "DayClosings",
                type: "uuid",
                nullable: false,
                defaultValue: ExistingTenant);

            migrationBuilder.AddColumn<Guid>(
                name: "TenantId",
                schema: "aispa",
                table: "Customers",
                type: "uuid",
                nullable: false,
                defaultValue: ExistingTenant);

            migrationBuilder.AddColumn<Guid>(
                name: "TenantId",
                schema: "aispa",
                table: "CustomerPreferences",
                type: "uuid",
                nullable: false,
                defaultValue: ExistingTenant);

            migrationBuilder.AddColumn<Guid>(
                name: "TenantId",
                schema: "aispa",
                table: "ChannelMappings",
                type: "uuid",
                nullable: false,
                defaultValue: ExistingTenant);

            migrationBuilder.AddColumn<Guid>(
                name: "TenantId",
                schema: "aispa",
                table: "ChannelBookings",
                type: "uuid",
                nullable: false,
                defaultValue: ExistingTenant);

            migrationBuilder.AddColumn<Guid>(
                name: "TenantId",
                schema: "aispa",
                table: "CashRegisters",
                type: "uuid",
                nullable: false,
                defaultValue: ExistingTenant);

            migrationBuilder.AddColumn<Guid>(
                name: "TenantId",
                schema: "aispa",
                table: "CashMovements",
                type: "uuid",
                nullable: false,
                defaultValue: ExistingTenant);

            migrationBuilder.AddColumn<Guid>(
                name: "TenantId",
                schema: "aispa",
                table: "CalendarFeeds",
                type: "uuid",
                nullable: false,
                defaultValue: ExistingTenant);

            migrationBuilder.AddColumn<Guid>(
                name: "TenantId",
                schema: "aispa",
                table: "Bills",
                type: "uuid",
                nullable: false,
                defaultValue: ExistingTenant);

            migrationBuilder.AddColumn<Guid>(
                name: "TenantId",
                schema: "aispa",
                table: "BillLines",
                type: "uuid",
                nullable: false,
                defaultValue: ExistingTenant);

            migrationBuilder.AddColumn<Guid>(
                name: "TenantId",
                schema: "aispa",
                table: "Articles",
                type: "uuid",
                nullable: false,
                defaultValue: ExistingTenant);

            migrationBuilder.AddColumn<Guid>(
                name: "TenantId",
                schema: "aispa",
                table: "Appointments",
                type: "uuid",
                nullable: false,
                defaultValue: ExistingTenant);

            migrationBuilder.AddColumn<Guid>(
                name: "TenantId",
                schema: "aispa",
                table: "AppointmentPreparationChecks",
                type: "uuid",
                nullable: false,
                defaultValue: ExistingTenant);

            migrationBuilder.AddColumn<Guid>(
                name: "TenantId",
                schema: "aispa",
                table: "AppointmentFollowUps",
                type: "uuid",
                nullable: false,
                defaultValue: ExistingTenant);

            migrationBuilder.AddColumn<Guid>(
                name: "TenantId",
                schema: "aispa",
                table: "AccountingSettings",
                type: "uuid",
                nullable: false,
                defaultValue: ExistingTenant);

            migrationBuilder.CreateTable(
                name: "Tenants",
                schema: "aispa",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Country = table.Column<string>(type: "character(2)", fixedLength: true, maxLength: 2, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Tenants", x => x.Id);
                });

            // Bestehende Daten (aus der Zeit vor der Mandantentrennung) gehören einem Mandanten „Bestand“.
            // Nur anlegen, wenn es Daten gibt; eine leere Datenbank bleibt ohne Mandant.
            migrationBuilder.Sql($"""
                INSERT INTO aispa."Tenants" ("Id", "Name", "Country", "CreatedAt")
                SELECT '{ExistingTenant}', 'Bestand', 'AT', now()
                WHERE EXISTS (SELECT 1 FROM aispa."AccountingSettings") OR EXISTS (SELECT 1 FROM aispa."AppointmentFollowUps") OR EXISTS (SELECT 1 FROM aispa."AppointmentPreparationChecks") OR EXISTS (SELECT 1 FROM aispa."Appointments") OR EXISTS (SELECT 1 FROM aispa."Articles") OR EXISTS (SELECT 1 FROM aispa."BillLines") OR EXISTS (SELECT 1 FROM aispa."Bills") OR EXISTS (SELECT 1 FROM aispa."CalendarFeeds") OR EXISTS (SELECT 1 FROM aispa."CashMovements") OR EXISTS (SELECT 1 FROM aispa."CashRegisters") OR EXISTS (SELECT 1 FROM aispa."ChannelBookings") OR EXISTS (SELECT 1 FROM aispa."ChannelMappings") OR EXISTS (SELECT 1 FROM aispa."CustomerPreferences") OR EXISTS (SELECT 1 FROM aispa."Customers") OR EXISTS (SELECT 1 FROM aispa."DayClosings") OR EXISTS (SELECT 1 FROM aispa."EmployeeCosts") OR EXISTS (SELECT 1 FROM aispa."EmployeeWorkingHours") OR EXISTS (SELECT 1 FROM aispa."Employees") OR EXISTS (SELECT 1 FROM aispa."Equipment") OR EXISTS (SELECT 1 FROM aispa."InboundMails") OR EXISTS (SELECT 1 FROM aispa."Invitations") OR EXISTS (SELECT 1 FROM aispa."LocationCostSettings") OR EXISTS (SELECT 1 FROM aispa."LocationOpeningHours") OR EXISTS (SELECT 1 FROM aispa."Locations") OR EXISTS (SELECT 1 FROM aispa."Qualifications") OR EXISTS (SELECT 1 FROM aispa."ReceiptLine") OR EXISTS (SELECT 1 FROM aispa."ReceiptPayment") OR EXISTS (SELECT 1 FROM aispa."Receipts") OR EXISTS (SELECT 1 FROM aispa."ResourceBookings") OR EXISTS (SELECT 1 FROM aispa."RoomCosts") OR EXISTS (SELECT 1 FROM aispa."Rooms") OR EXISTS (SELECT 1 FROM aispa."RosterDayEntries") OR EXISTS (SELECT 1 FROM aispa."RosterDays") OR EXISTS (SELECT 1 FROM aispa."StaffAccess") OR EXISTS (SELECT 1 FROM aispa."TestSignatureKey") OR EXISTS (SELECT 1 FROM aispa."TreatmentMaterials") OR EXISTS (SELECT 1 FROM aispa."TreatmentPosSettings") OR EXISTS (SELECT 1 FROM aispa."TreatmentPreparationSteps") OR EXISTS (SELECT 1 FROM aispa."Treatments") OR EXISTS (SELECT 1 FROM aispa."Vouchers");
                UPDATE aispa."RevenueAccounts" SET "TenantId" = '{ExistingTenant}'
                WHERE "Id" NOT IN ('0199f000-0000-7000-8000-000000003600', '0199f000-0000-7000-8000-000000004000', '0199f000-0000-7000-8000-000000004100')
                  AND EXISTS (SELECT 1 FROM aispa."Tenants" WHERE "Id" = '{ExistingTenant}');
                """);

            // Der Standardwert diente nur dem Nachtragen; neue Einträge bekommen den Mandanten beim Speichern.
            migrationBuilder.Sql("""
                ALTER TABLE aispa."AccountingSettings" ALTER COLUMN "TenantId" DROP DEFAULT;
                ALTER TABLE aispa."AppointmentFollowUps" ALTER COLUMN "TenantId" DROP DEFAULT;
                ALTER TABLE aispa."AppointmentPreparationChecks" ALTER COLUMN "TenantId" DROP DEFAULT;
                ALTER TABLE aispa."Appointments" ALTER COLUMN "TenantId" DROP DEFAULT;
                ALTER TABLE aispa."Articles" ALTER COLUMN "TenantId" DROP DEFAULT;
                ALTER TABLE aispa."BillLines" ALTER COLUMN "TenantId" DROP DEFAULT;
                ALTER TABLE aispa."Bills" ALTER COLUMN "TenantId" DROP DEFAULT;
                ALTER TABLE aispa."CalendarFeeds" ALTER COLUMN "TenantId" DROP DEFAULT;
                ALTER TABLE aispa."CashMovements" ALTER COLUMN "TenantId" DROP DEFAULT;
                ALTER TABLE aispa."CashRegisters" ALTER COLUMN "TenantId" DROP DEFAULT;
                ALTER TABLE aispa."ChannelBookings" ALTER COLUMN "TenantId" DROP DEFAULT;
                ALTER TABLE aispa."ChannelMappings" ALTER COLUMN "TenantId" DROP DEFAULT;
                ALTER TABLE aispa."CustomerPreferences" ALTER COLUMN "TenantId" DROP DEFAULT;
                ALTER TABLE aispa."Customers" ALTER COLUMN "TenantId" DROP DEFAULT;
                ALTER TABLE aispa."DayClosings" ALTER COLUMN "TenantId" DROP DEFAULT;
                ALTER TABLE aispa."EmployeeCosts" ALTER COLUMN "TenantId" DROP DEFAULT;
                ALTER TABLE aispa."EmployeeWorkingHours" ALTER COLUMN "TenantId" DROP DEFAULT;
                ALTER TABLE aispa."Employees" ALTER COLUMN "TenantId" DROP DEFAULT;
                ALTER TABLE aispa."Equipment" ALTER COLUMN "TenantId" DROP DEFAULT;
                ALTER TABLE aispa."InboundMails" ALTER COLUMN "TenantId" DROP DEFAULT;
                ALTER TABLE aispa."Invitations" ALTER COLUMN "TenantId" DROP DEFAULT;
                ALTER TABLE aispa."LocationCostSettings" ALTER COLUMN "TenantId" DROP DEFAULT;
                ALTER TABLE aispa."LocationOpeningHours" ALTER COLUMN "TenantId" DROP DEFAULT;
                ALTER TABLE aispa."Locations" ALTER COLUMN "TenantId" DROP DEFAULT;
                ALTER TABLE aispa."Qualifications" ALTER COLUMN "TenantId" DROP DEFAULT;
                ALTER TABLE aispa."ReceiptLine" ALTER COLUMN "TenantId" DROP DEFAULT;
                ALTER TABLE aispa."ReceiptPayment" ALTER COLUMN "TenantId" DROP DEFAULT;
                ALTER TABLE aispa."Receipts" ALTER COLUMN "TenantId" DROP DEFAULT;
                ALTER TABLE aispa."ResourceBookings" ALTER COLUMN "TenantId" DROP DEFAULT;
                ALTER TABLE aispa."RoomCosts" ALTER COLUMN "TenantId" DROP DEFAULT;
                ALTER TABLE aispa."Rooms" ALTER COLUMN "TenantId" DROP DEFAULT;
                ALTER TABLE aispa."RosterDayEntries" ALTER COLUMN "TenantId" DROP DEFAULT;
                ALTER TABLE aispa."RosterDays" ALTER COLUMN "TenantId" DROP DEFAULT;
                ALTER TABLE aispa."StaffAccess" ALTER COLUMN "TenantId" DROP DEFAULT;
                ALTER TABLE aispa."TestSignatureKey" ALTER COLUMN "TenantId" DROP DEFAULT;
                ALTER TABLE aispa."TreatmentMaterials" ALTER COLUMN "TenantId" DROP DEFAULT;
                ALTER TABLE aispa."TreatmentPosSettings" ALTER COLUMN "TenantId" DROP DEFAULT;
                ALTER TABLE aispa."TreatmentPreparationSteps" ALTER COLUMN "TenantId" DROP DEFAULT;
                ALTER TABLE aispa."Treatments" ALTER COLUMN "TenantId" DROP DEFAULT;
                ALTER TABLE aispa."Vouchers" ALTER COLUMN "TenantId" DROP DEFAULT;
                """);

            migrationBuilder.UpdateData(
                schema: "aispa",
                table: "RevenueAccounts",
                keyColumn: "Id",
                keyValue: new Guid("0199f000-0000-7000-8000-000000003600"),
                column: "TenantId",
                value: null);

            migrationBuilder.UpdateData(
                schema: "aispa",
                table: "RevenueAccounts",
                keyColumn: "Id",
                keyValue: new Guid("0199f000-0000-7000-8000-000000004000"),
                column: "TenantId",
                value: null);

            migrationBuilder.UpdateData(
                schema: "aispa",
                table: "RevenueAccounts",
                keyColumn: "Id",
                keyValue: new Guid("0199f000-0000-7000-8000-000000004100"),
                column: "TenantId",
                value: null);

            migrationBuilder.CreateIndex(
                name: "IX_Vouchers_TenantId",
                schema: "aispa",
                table: "Vouchers",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_Treatments_TenantId",
                schema: "aispa",
                table: "Treatments",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_TreatmentPreparationSteps_TenantId",
                schema: "aispa",
                table: "TreatmentPreparationSteps",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_TreatmentPosSettings_TenantId",
                schema: "aispa",
                table: "TreatmentPosSettings",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_TreatmentMaterials_TenantId",
                schema: "aispa",
                table: "TreatmentMaterials",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_TestSignatureKey_TenantId",
                schema: "aispa",
                table: "TestSignatureKey",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_StaffAccess_TenantId",
                schema: "aispa",
                table: "StaffAccess",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_RosterDays_TenantId",
                schema: "aispa",
                table: "RosterDays",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_RosterDayEntries_TenantId",
                schema: "aispa",
                table: "RosterDayEntries",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_Rooms_TenantId",
                schema: "aispa",
                table: "Rooms",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_RoomCosts_TenantId",
                schema: "aispa",
                table: "RoomCosts",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_RevenueAccounts_TenantId",
                schema: "aispa",
                table: "RevenueAccounts",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_RevenueAccounts_TenantId_Code",
                schema: "aispa",
                table: "RevenueAccounts",
                columns: new[] { "TenantId", "Code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ResourceBookings_TenantId",
                schema: "aispa",
                table: "ResourceBookings",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_Receipts_TenantId",
                schema: "aispa",
                table: "Receipts",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_ReceiptPayment_TenantId",
                schema: "aispa",
                table: "ReceiptPayment",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_ReceiptLine_TenantId",
                schema: "aispa",
                table: "ReceiptLine",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_Qualifications_TenantId",
                schema: "aispa",
                table: "Qualifications",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_Qualifications_TenantId_Name",
                schema: "aispa",
                table: "Qualifications",
                columns: new[] { "TenantId", "Name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Locations_TenantId",
                schema: "aispa",
                table: "Locations",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_LocationOpeningHours_TenantId",
                schema: "aispa",
                table: "LocationOpeningHours",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_LocationCostSettings_TenantId",
                schema: "aispa",
                table: "LocationCostSettings",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_Invitations_TenantId",
                schema: "aispa",
                table: "Invitations",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_InboundMails_TenantId",
                schema: "aispa",
                table: "InboundMails",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_InboundMails_TenantId_MessageId",
                schema: "aispa",
                table: "InboundMails",
                columns: new[] { "TenantId", "MessageId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Equipment_TenantId",
                schema: "aispa",
                table: "Equipment",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_EmployeeWorkingHours_TenantId",
                schema: "aispa",
                table: "EmployeeWorkingHours",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_Employees_TenantId",
                schema: "aispa",
                table: "Employees",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_EmployeeCosts_TenantId",
                schema: "aispa",
                table: "EmployeeCosts",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_DayClosings_TenantId",
                schema: "aispa",
                table: "DayClosings",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_Customers_TenantId",
                schema: "aispa",
                table: "Customers",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_CustomerPreferences_TenantId",
                schema: "aispa",
                table: "CustomerPreferences",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_ChannelMappings_TenantId",
                schema: "aispa",
                table: "ChannelMappings",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_ChannelMappings_TenantId_Channel_Kind_NormalizedName",
                schema: "aispa",
                table: "ChannelMappings",
                columns: new[] { "TenantId", "Channel", "Kind", "NormalizedName" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ChannelBookings_TenantId",
                schema: "aispa",
                table: "ChannelBookings",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_ChannelBookings_TenantId_Channel_ExternalId",
                schema: "aispa",
                table: "ChannelBookings",
                columns: new[] { "TenantId", "Channel", "ExternalId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CashRegisters_TenantId",
                schema: "aispa",
                table: "CashRegisters",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_CashRegisters_TenantId_Code",
                schema: "aispa",
                table: "CashRegisters",
                columns: new[] { "TenantId", "Code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CashMovements_TenantId",
                schema: "aispa",
                table: "CashMovements",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_CalendarFeeds_TenantId",
                schema: "aispa",
                table: "CalendarFeeds",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_Bills_TenantId",
                schema: "aispa",
                table: "Bills",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_BillLines_TenantId",
                schema: "aispa",
                table: "BillLines",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_Articles_TenantId",
                schema: "aispa",
                table: "Articles",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_Appointments_TenantId",
                schema: "aispa",
                table: "Appointments",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_AppointmentPreparationChecks_TenantId",
                schema: "aispa",
                table: "AppointmentPreparationChecks",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_AppointmentFollowUps_TenantId",
                schema: "aispa",
                table: "AppointmentFollowUps",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_AccountingSettings_TenantId",
                schema: "aispa",
                table: "AccountingSettings",
                column: "TenantId");

            migrationBuilder.AddForeignKey(
                name: "FK_AccountingSettings_Tenants_TenantId",
                schema: "aispa",
                table: "AccountingSettings",
                column: "TenantId",
                principalSchema: "aispa",
                principalTable: "Tenants",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_AppointmentFollowUps_Tenants_TenantId",
                schema: "aispa",
                table: "AppointmentFollowUps",
                column: "TenantId",
                principalSchema: "aispa",
                principalTable: "Tenants",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_AppointmentPreparationChecks_Tenants_TenantId",
                schema: "aispa",
                table: "AppointmentPreparationChecks",
                column: "TenantId",
                principalSchema: "aispa",
                principalTable: "Tenants",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Appointments_Tenants_TenantId",
                schema: "aispa",
                table: "Appointments",
                column: "TenantId",
                principalSchema: "aispa",
                principalTable: "Tenants",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Articles_Tenants_TenantId",
                schema: "aispa",
                table: "Articles",
                column: "TenantId",
                principalSchema: "aispa",
                principalTable: "Tenants",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_BillLines_Tenants_TenantId",
                schema: "aispa",
                table: "BillLines",
                column: "TenantId",
                principalSchema: "aispa",
                principalTable: "Tenants",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Bills_Tenants_TenantId",
                schema: "aispa",
                table: "Bills",
                column: "TenantId",
                principalSchema: "aispa",
                principalTable: "Tenants",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_CalendarFeeds_Tenants_TenantId",
                schema: "aispa",
                table: "CalendarFeeds",
                column: "TenantId",
                principalSchema: "aispa",
                principalTable: "Tenants",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_CashMovements_Tenants_TenantId",
                schema: "aispa",
                table: "CashMovements",
                column: "TenantId",
                principalSchema: "aispa",
                principalTable: "Tenants",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_CashRegisters_Tenants_TenantId",
                schema: "aispa",
                table: "CashRegisters",
                column: "TenantId",
                principalSchema: "aispa",
                principalTable: "Tenants",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_ChannelBookings_Tenants_TenantId",
                schema: "aispa",
                table: "ChannelBookings",
                column: "TenantId",
                principalSchema: "aispa",
                principalTable: "Tenants",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_ChannelMappings_Tenants_TenantId",
                schema: "aispa",
                table: "ChannelMappings",
                column: "TenantId",
                principalSchema: "aispa",
                principalTable: "Tenants",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_CustomerPreferences_Tenants_TenantId",
                schema: "aispa",
                table: "CustomerPreferences",
                column: "TenantId",
                principalSchema: "aispa",
                principalTable: "Tenants",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Customers_Tenants_TenantId",
                schema: "aispa",
                table: "Customers",
                column: "TenantId",
                principalSchema: "aispa",
                principalTable: "Tenants",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_DayClosings_Tenants_TenantId",
                schema: "aispa",
                table: "DayClosings",
                column: "TenantId",
                principalSchema: "aispa",
                principalTable: "Tenants",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_EmployeeCosts_Tenants_TenantId",
                schema: "aispa",
                table: "EmployeeCosts",
                column: "TenantId",
                principalSchema: "aispa",
                principalTable: "Tenants",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Employees_Tenants_TenantId",
                schema: "aispa",
                table: "Employees",
                column: "TenantId",
                principalSchema: "aispa",
                principalTable: "Tenants",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_EmployeeWorkingHours_Tenants_TenantId",
                schema: "aispa",
                table: "EmployeeWorkingHours",
                column: "TenantId",
                principalSchema: "aispa",
                principalTable: "Tenants",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Equipment_Tenants_TenantId",
                schema: "aispa",
                table: "Equipment",
                column: "TenantId",
                principalSchema: "aispa",
                principalTable: "Tenants",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_InboundMails_Tenants_TenantId",
                schema: "aispa",
                table: "InboundMails",
                column: "TenantId",
                principalSchema: "aispa",
                principalTable: "Tenants",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Invitations_Tenants_TenantId",
                schema: "aispa",
                table: "Invitations",
                column: "TenantId",
                principalSchema: "aispa",
                principalTable: "Tenants",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_LocationCostSettings_Tenants_TenantId",
                schema: "aispa",
                table: "LocationCostSettings",
                column: "TenantId",
                principalSchema: "aispa",
                principalTable: "Tenants",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_LocationOpeningHours_Tenants_TenantId",
                schema: "aispa",
                table: "LocationOpeningHours",
                column: "TenantId",
                principalSchema: "aispa",
                principalTable: "Tenants",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Locations_Tenants_TenantId",
                schema: "aispa",
                table: "Locations",
                column: "TenantId",
                principalSchema: "aispa",
                principalTable: "Tenants",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Qualifications_Tenants_TenantId",
                schema: "aispa",
                table: "Qualifications",
                column: "TenantId",
                principalSchema: "aispa",
                principalTable: "Tenants",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_ReceiptLine_Tenants_TenantId",
                schema: "aispa",
                table: "ReceiptLine",
                column: "TenantId",
                principalSchema: "aispa",
                principalTable: "Tenants",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_ReceiptPayment_Tenants_TenantId",
                schema: "aispa",
                table: "ReceiptPayment",
                column: "TenantId",
                principalSchema: "aispa",
                principalTable: "Tenants",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Receipts_Tenants_TenantId",
                schema: "aispa",
                table: "Receipts",
                column: "TenantId",
                principalSchema: "aispa",
                principalTable: "Tenants",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_ResourceBookings_Tenants_TenantId",
                schema: "aispa",
                table: "ResourceBookings",
                column: "TenantId",
                principalSchema: "aispa",
                principalTable: "Tenants",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_RevenueAccounts_Tenants_TenantId",
                schema: "aispa",
                table: "RevenueAccounts",
                column: "TenantId",
                principalSchema: "aispa",
                principalTable: "Tenants",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_RoomCosts_Tenants_TenantId",
                schema: "aispa",
                table: "RoomCosts",
                column: "TenantId",
                principalSchema: "aispa",
                principalTable: "Tenants",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Rooms_Tenants_TenantId",
                schema: "aispa",
                table: "Rooms",
                column: "TenantId",
                principalSchema: "aispa",
                principalTable: "Tenants",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_RosterDayEntries_Tenants_TenantId",
                schema: "aispa",
                table: "RosterDayEntries",
                column: "TenantId",
                principalSchema: "aispa",
                principalTable: "Tenants",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_RosterDays_Tenants_TenantId",
                schema: "aispa",
                table: "RosterDays",
                column: "TenantId",
                principalSchema: "aispa",
                principalTable: "Tenants",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_StaffAccess_Tenants_TenantId",
                schema: "aispa",
                table: "StaffAccess",
                column: "TenantId",
                principalSchema: "aispa",
                principalTable: "Tenants",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_TestSignatureKey_Tenants_TenantId",
                schema: "aispa",
                table: "TestSignatureKey",
                column: "TenantId",
                principalSchema: "aispa",
                principalTable: "Tenants",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_TreatmentMaterials_Tenants_TenantId",
                schema: "aispa",
                table: "TreatmentMaterials",
                column: "TenantId",
                principalSchema: "aispa",
                principalTable: "Tenants",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_TreatmentPosSettings_Tenants_TenantId",
                schema: "aispa",
                table: "TreatmentPosSettings",
                column: "TenantId",
                principalSchema: "aispa",
                principalTable: "Tenants",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_TreatmentPreparationSteps_Tenants_TenantId",
                schema: "aispa",
                table: "TreatmentPreparationSteps",
                column: "TenantId",
                principalSchema: "aispa",
                principalTable: "Tenants",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Treatments_Tenants_TenantId",
                schema: "aispa",
                table: "Treatments",
                column: "TenantId",
                principalSchema: "aispa",
                principalTable: "Tenants",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Vouchers_Tenants_TenantId",
                schema: "aispa",
                table: "Vouchers",
                column: "TenantId",
                principalSchema: "aispa",
                principalTable: "Tenants",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_AccountingSettings_Tenants_TenantId",
                schema: "aispa",
                table: "AccountingSettings");

            migrationBuilder.DropForeignKey(
                name: "FK_AppointmentFollowUps_Tenants_TenantId",
                schema: "aispa",
                table: "AppointmentFollowUps");

            migrationBuilder.DropForeignKey(
                name: "FK_AppointmentPreparationChecks_Tenants_TenantId",
                schema: "aispa",
                table: "AppointmentPreparationChecks");

            migrationBuilder.DropForeignKey(
                name: "FK_Appointments_Tenants_TenantId",
                schema: "aispa",
                table: "Appointments");

            migrationBuilder.DropForeignKey(
                name: "FK_Articles_Tenants_TenantId",
                schema: "aispa",
                table: "Articles");

            migrationBuilder.DropForeignKey(
                name: "FK_BillLines_Tenants_TenantId",
                schema: "aispa",
                table: "BillLines");

            migrationBuilder.DropForeignKey(
                name: "FK_Bills_Tenants_TenantId",
                schema: "aispa",
                table: "Bills");

            migrationBuilder.DropForeignKey(
                name: "FK_CalendarFeeds_Tenants_TenantId",
                schema: "aispa",
                table: "CalendarFeeds");

            migrationBuilder.DropForeignKey(
                name: "FK_CashMovements_Tenants_TenantId",
                schema: "aispa",
                table: "CashMovements");

            migrationBuilder.DropForeignKey(
                name: "FK_CashRegisters_Tenants_TenantId",
                schema: "aispa",
                table: "CashRegisters");

            migrationBuilder.DropForeignKey(
                name: "FK_ChannelBookings_Tenants_TenantId",
                schema: "aispa",
                table: "ChannelBookings");

            migrationBuilder.DropForeignKey(
                name: "FK_ChannelMappings_Tenants_TenantId",
                schema: "aispa",
                table: "ChannelMappings");

            migrationBuilder.DropForeignKey(
                name: "FK_CustomerPreferences_Tenants_TenantId",
                schema: "aispa",
                table: "CustomerPreferences");

            migrationBuilder.DropForeignKey(
                name: "FK_Customers_Tenants_TenantId",
                schema: "aispa",
                table: "Customers");

            migrationBuilder.DropForeignKey(
                name: "FK_DayClosings_Tenants_TenantId",
                schema: "aispa",
                table: "DayClosings");

            migrationBuilder.DropForeignKey(
                name: "FK_EmployeeCosts_Tenants_TenantId",
                schema: "aispa",
                table: "EmployeeCosts");

            migrationBuilder.DropForeignKey(
                name: "FK_Employees_Tenants_TenantId",
                schema: "aispa",
                table: "Employees");

            migrationBuilder.DropForeignKey(
                name: "FK_EmployeeWorkingHours_Tenants_TenantId",
                schema: "aispa",
                table: "EmployeeWorkingHours");

            migrationBuilder.DropForeignKey(
                name: "FK_Equipment_Tenants_TenantId",
                schema: "aispa",
                table: "Equipment");

            migrationBuilder.DropForeignKey(
                name: "FK_InboundMails_Tenants_TenantId",
                schema: "aispa",
                table: "InboundMails");

            migrationBuilder.DropForeignKey(
                name: "FK_Invitations_Tenants_TenantId",
                schema: "aispa",
                table: "Invitations");

            migrationBuilder.DropForeignKey(
                name: "FK_LocationCostSettings_Tenants_TenantId",
                schema: "aispa",
                table: "LocationCostSettings");

            migrationBuilder.DropForeignKey(
                name: "FK_LocationOpeningHours_Tenants_TenantId",
                schema: "aispa",
                table: "LocationOpeningHours");

            migrationBuilder.DropForeignKey(
                name: "FK_Locations_Tenants_TenantId",
                schema: "aispa",
                table: "Locations");

            migrationBuilder.DropForeignKey(
                name: "FK_Qualifications_Tenants_TenantId",
                schema: "aispa",
                table: "Qualifications");

            migrationBuilder.DropForeignKey(
                name: "FK_ReceiptLine_Tenants_TenantId",
                schema: "aispa",
                table: "ReceiptLine");

            migrationBuilder.DropForeignKey(
                name: "FK_ReceiptPayment_Tenants_TenantId",
                schema: "aispa",
                table: "ReceiptPayment");

            migrationBuilder.DropForeignKey(
                name: "FK_Receipts_Tenants_TenantId",
                schema: "aispa",
                table: "Receipts");

            migrationBuilder.DropForeignKey(
                name: "FK_ResourceBookings_Tenants_TenantId",
                schema: "aispa",
                table: "ResourceBookings");

            migrationBuilder.DropForeignKey(
                name: "FK_RevenueAccounts_Tenants_TenantId",
                schema: "aispa",
                table: "RevenueAccounts");

            migrationBuilder.DropForeignKey(
                name: "FK_RoomCosts_Tenants_TenantId",
                schema: "aispa",
                table: "RoomCosts");

            migrationBuilder.DropForeignKey(
                name: "FK_Rooms_Tenants_TenantId",
                schema: "aispa",
                table: "Rooms");

            migrationBuilder.DropForeignKey(
                name: "FK_RosterDayEntries_Tenants_TenantId",
                schema: "aispa",
                table: "RosterDayEntries");

            migrationBuilder.DropForeignKey(
                name: "FK_RosterDays_Tenants_TenantId",
                schema: "aispa",
                table: "RosterDays");

            migrationBuilder.DropForeignKey(
                name: "FK_StaffAccess_Tenants_TenantId",
                schema: "aispa",
                table: "StaffAccess");

            migrationBuilder.DropForeignKey(
                name: "FK_TestSignatureKey_Tenants_TenantId",
                schema: "aispa",
                table: "TestSignatureKey");

            migrationBuilder.DropForeignKey(
                name: "FK_TreatmentMaterials_Tenants_TenantId",
                schema: "aispa",
                table: "TreatmentMaterials");

            migrationBuilder.DropForeignKey(
                name: "FK_TreatmentPosSettings_Tenants_TenantId",
                schema: "aispa",
                table: "TreatmentPosSettings");

            migrationBuilder.DropForeignKey(
                name: "FK_TreatmentPreparationSteps_Tenants_TenantId",
                schema: "aispa",
                table: "TreatmentPreparationSteps");

            migrationBuilder.DropForeignKey(
                name: "FK_Treatments_Tenants_TenantId",
                schema: "aispa",
                table: "Treatments");

            migrationBuilder.DropForeignKey(
                name: "FK_Vouchers_Tenants_TenantId",
                schema: "aispa",
                table: "Vouchers");

            migrationBuilder.DropTable(
                name: "Tenants",
                schema: "aispa");

            migrationBuilder.DropIndex(
                name: "IX_Vouchers_TenantId",
                schema: "aispa",
                table: "Vouchers");

            migrationBuilder.DropIndex(
                name: "IX_Treatments_TenantId",
                schema: "aispa",
                table: "Treatments");

            migrationBuilder.DropIndex(
                name: "IX_TreatmentPreparationSteps_TenantId",
                schema: "aispa",
                table: "TreatmentPreparationSteps");

            migrationBuilder.DropIndex(
                name: "IX_TreatmentPosSettings_TenantId",
                schema: "aispa",
                table: "TreatmentPosSettings");

            migrationBuilder.DropIndex(
                name: "IX_TreatmentMaterials_TenantId",
                schema: "aispa",
                table: "TreatmentMaterials");

            migrationBuilder.DropIndex(
                name: "IX_TestSignatureKey_TenantId",
                schema: "aispa",
                table: "TestSignatureKey");

            migrationBuilder.DropIndex(
                name: "IX_StaffAccess_TenantId",
                schema: "aispa",
                table: "StaffAccess");

            migrationBuilder.DropIndex(
                name: "IX_RosterDays_TenantId",
                schema: "aispa",
                table: "RosterDays");

            migrationBuilder.DropIndex(
                name: "IX_RosterDayEntries_TenantId",
                schema: "aispa",
                table: "RosterDayEntries");

            migrationBuilder.DropIndex(
                name: "IX_Rooms_TenantId",
                schema: "aispa",
                table: "Rooms");

            migrationBuilder.DropIndex(
                name: "IX_RoomCosts_TenantId",
                schema: "aispa",
                table: "RoomCosts");

            migrationBuilder.DropIndex(
                name: "IX_RevenueAccounts_TenantId",
                schema: "aispa",
                table: "RevenueAccounts");

            migrationBuilder.DropIndex(
                name: "IX_RevenueAccounts_TenantId_Code",
                schema: "aispa",
                table: "RevenueAccounts");

            migrationBuilder.DropIndex(
                name: "IX_ResourceBookings_TenantId",
                schema: "aispa",
                table: "ResourceBookings");

            migrationBuilder.DropIndex(
                name: "IX_Receipts_TenantId",
                schema: "aispa",
                table: "Receipts");

            migrationBuilder.DropIndex(
                name: "IX_ReceiptPayment_TenantId",
                schema: "aispa",
                table: "ReceiptPayment");

            migrationBuilder.DropIndex(
                name: "IX_ReceiptLine_TenantId",
                schema: "aispa",
                table: "ReceiptLine");

            migrationBuilder.DropIndex(
                name: "IX_Qualifications_TenantId",
                schema: "aispa",
                table: "Qualifications");

            migrationBuilder.DropIndex(
                name: "IX_Qualifications_TenantId_Name",
                schema: "aispa",
                table: "Qualifications");

            migrationBuilder.DropIndex(
                name: "IX_Locations_TenantId",
                schema: "aispa",
                table: "Locations");

            migrationBuilder.DropIndex(
                name: "IX_LocationOpeningHours_TenantId",
                schema: "aispa",
                table: "LocationOpeningHours");

            migrationBuilder.DropIndex(
                name: "IX_LocationCostSettings_TenantId",
                schema: "aispa",
                table: "LocationCostSettings");

            migrationBuilder.DropIndex(
                name: "IX_Invitations_TenantId",
                schema: "aispa",
                table: "Invitations");

            migrationBuilder.DropIndex(
                name: "IX_InboundMails_TenantId",
                schema: "aispa",
                table: "InboundMails");

            migrationBuilder.DropIndex(
                name: "IX_InboundMails_TenantId_MessageId",
                schema: "aispa",
                table: "InboundMails");

            migrationBuilder.DropIndex(
                name: "IX_Equipment_TenantId",
                schema: "aispa",
                table: "Equipment");

            migrationBuilder.DropIndex(
                name: "IX_EmployeeWorkingHours_TenantId",
                schema: "aispa",
                table: "EmployeeWorkingHours");

            migrationBuilder.DropIndex(
                name: "IX_Employees_TenantId",
                schema: "aispa",
                table: "Employees");

            migrationBuilder.DropIndex(
                name: "IX_EmployeeCosts_TenantId",
                schema: "aispa",
                table: "EmployeeCosts");

            migrationBuilder.DropIndex(
                name: "IX_DayClosings_TenantId",
                schema: "aispa",
                table: "DayClosings");

            migrationBuilder.DropIndex(
                name: "IX_Customers_TenantId",
                schema: "aispa",
                table: "Customers");

            migrationBuilder.DropIndex(
                name: "IX_CustomerPreferences_TenantId",
                schema: "aispa",
                table: "CustomerPreferences");

            migrationBuilder.DropIndex(
                name: "IX_ChannelMappings_TenantId",
                schema: "aispa",
                table: "ChannelMappings");

            migrationBuilder.DropIndex(
                name: "IX_ChannelMappings_TenantId_Channel_Kind_NormalizedName",
                schema: "aispa",
                table: "ChannelMappings");

            migrationBuilder.DropIndex(
                name: "IX_ChannelBookings_TenantId",
                schema: "aispa",
                table: "ChannelBookings");

            migrationBuilder.DropIndex(
                name: "IX_ChannelBookings_TenantId_Channel_ExternalId",
                schema: "aispa",
                table: "ChannelBookings");

            migrationBuilder.DropIndex(
                name: "IX_CashRegisters_TenantId",
                schema: "aispa",
                table: "CashRegisters");

            migrationBuilder.DropIndex(
                name: "IX_CashRegisters_TenantId_Code",
                schema: "aispa",
                table: "CashRegisters");

            migrationBuilder.DropIndex(
                name: "IX_CashMovements_TenantId",
                schema: "aispa",
                table: "CashMovements");

            migrationBuilder.DropIndex(
                name: "IX_CalendarFeeds_TenantId",
                schema: "aispa",
                table: "CalendarFeeds");

            migrationBuilder.DropIndex(
                name: "IX_Bills_TenantId",
                schema: "aispa",
                table: "Bills");

            migrationBuilder.DropIndex(
                name: "IX_BillLines_TenantId",
                schema: "aispa",
                table: "BillLines");

            migrationBuilder.DropIndex(
                name: "IX_Articles_TenantId",
                schema: "aispa",
                table: "Articles");

            migrationBuilder.DropIndex(
                name: "IX_Appointments_TenantId",
                schema: "aispa",
                table: "Appointments");

            migrationBuilder.DropIndex(
                name: "IX_AppointmentPreparationChecks_TenantId",
                schema: "aispa",
                table: "AppointmentPreparationChecks");

            migrationBuilder.DropIndex(
                name: "IX_AppointmentFollowUps_TenantId",
                schema: "aispa",
                table: "AppointmentFollowUps");

            migrationBuilder.DropIndex(
                name: "IX_AccountingSettings_TenantId",
                schema: "aispa",
                table: "AccountingSettings");

            migrationBuilder.DropColumn(
                name: "TenantId",
                schema: "aispa",
                table: "Vouchers");

            migrationBuilder.DropColumn(
                name: "TenantId",
                schema: "aispa",
                table: "Treatments");

            migrationBuilder.DropColumn(
                name: "TenantId",
                schema: "aispa",
                table: "TreatmentPreparationSteps");

            migrationBuilder.DropColumn(
                name: "TenantId",
                schema: "aispa",
                table: "TreatmentPosSettings");

            migrationBuilder.DropColumn(
                name: "TenantId",
                schema: "aispa",
                table: "TreatmentMaterials");

            migrationBuilder.DropColumn(
                name: "TenantId",
                schema: "aispa",
                table: "TestSignatureKey");

            migrationBuilder.DropColumn(
                name: "TenantId",
                schema: "aispa",
                table: "StaffAccess");

            migrationBuilder.DropColumn(
                name: "TenantId",
                schema: "aispa",
                table: "RosterDays");

            migrationBuilder.DropColumn(
                name: "TenantId",
                schema: "aispa",
                table: "RosterDayEntries");

            migrationBuilder.DropColumn(
                name: "TenantId",
                schema: "aispa",
                table: "Rooms");

            migrationBuilder.DropColumn(
                name: "TenantId",
                schema: "aispa",
                table: "RoomCosts");

            migrationBuilder.DropColumn(
                name: "TenantId",
                schema: "aispa",
                table: "RevenueAccounts");

            migrationBuilder.DropColumn(
                name: "TenantId",
                schema: "aispa",
                table: "ResourceBookings");

            migrationBuilder.DropColumn(
                name: "TenantId",
                schema: "aispa",
                table: "Receipts");

            migrationBuilder.DropColumn(
                name: "TenantId",
                schema: "aispa",
                table: "ReceiptPayment");

            migrationBuilder.DropColumn(
                name: "TenantId",
                schema: "aispa",
                table: "ReceiptLine");

            migrationBuilder.DropColumn(
                name: "TenantId",
                schema: "aispa",
                table: "Qualifications");

            migrationBuilder.DropColumn(
                name: "TenantId",
                schema: "aispa",
                table: "Locations");

            migrationBuilder.DropColumn(
                name: "TenantId",
                schema: "aispa",
                table: "LocationOpeningHours");

            migrationBuilder.DropColumn(
                name: "TenantId",
                schema: "aispa",
                table: "LocationCostSettings");

            migrationBuilder.DropColumn(
                name: "TenantId",
                schema: "aispa",
                table: "Invitations");

            migrationBuilder.DropColumn(
                name: "TenantId",
                schema: "aispa",
                table: "InboundMails");

            migrationBuilder.DropColumn(
                name: "TenantId",
                schema: "aispa",
                table: "Equipment");

            migrationBuilder.DropColumn(
                name: "TenantId",
                schema: "aispa",
                table: "EmployeeWorkingHours");

            migrationBuilder.DropColumn(
                name: "TenantId",
                schema: "aispa",
                table: "Employees");

            migrationBuilder.DropColumn(
                name: "TenantId",
                schema: "aispa",
                table: "EmployeeCosts");

            migrationBuilder.DropColumn(
                name: "TenantId",
                schema: "aispa",
                table: "DayClosings");

            migrationBuilder.DropColumn(
                name: "TenantId",
                schema: "aispa",
                table: "Customers");

            migrationBuilder.DropColumn(
                name: "TenantId",
                schema: "aispa",
                table: "CustomerPreferences");

            migrationBuilder.DropColumn(
                name: "TenantId",
                schema: "aispa",
                table: "ChannelMappings");

            migrationBuilder.DropColumn(
                name: "TenantId",
                schema: "aispa",
                table: "ChannelBookings");

            migrationBuilder.DropColumn(
                name: "TenantId",
                schema: "aispa",
                table: "CashRegisters");

            migrationBuilder.DropColumn(
                name: "TenantId",
                schema: "aispa",
                table: "CashMovements");

            migrationBuilder.DropColumn(
                name: "TenantId",
                schema: "aispa",
                table: "CalendarFeeds");

            migrationBuilder.DropColumn(
                name: "TenantId",
                schema: "aispa",
                table: "Bills");

            migrationBuilder.DropColumn(
                name: "TenantId",
                schema: "aispa",
                table: "BillLines");

            migrationBuilder.DropColumn(
                name: "TenantId",
                schema: "aispa",
                table: "Articles");

            migrationBuilder.DropColumn(
                name: "TenantId",
                schema: "aispa",
                table: "Appointments");

            migrationBuilder.DropColumn(
                name: "TenantId",
                schema: "aispa",
                table: "AppointmentPreparationChecks");

            migrationBuilder.DropColumn(
                name: "TenantId",
                schema: "aispa",
                table: "AppointmentFollowUps");

            migrationBuilder.DropColumn(
                name: "TenantId",
                schema: "aispa",
                table: "AccountingSettings");

            migrationBuilder.CreateIndex(
                name: "IX_RevenueAccounts_Code",
                schema: "aispa",
                table: "RevenueAccounts",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Qualifications_Name",
                schema: "aispa",
                table: "Qualifications",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_InboundMails_MessageId",
                schema: "aispa",
                table: "InboundMails",
                column: "MessageId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ChannelMappings_Channel_Kind_NormalizedName",
                schema: "aispa",
                table: "ChannelMappings",
                columns: new[] { "Channel", "Kind", "NormalizedName" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ChannelBookings_Channel_ExternalId",
                schema: "aispa",
                table: "ChannelBookings",
                columns: new[] { "Channel", "ExternalId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CashRegisters_Code",
                schema: "aispa",
                table: "CashRegisters",
                column: "Code",
                unique: true);
        }
    }
}
