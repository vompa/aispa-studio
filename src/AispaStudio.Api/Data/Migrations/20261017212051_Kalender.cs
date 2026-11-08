using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AispaStudio.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class Kalender : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterDatabase()
                .Annotation("Npgsql:PostgresExtension:btree_gist", ",,");

            migrationBuilder.CreateTable(
                name: "Customers",
                schema: "aispa",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    FirstName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    LastName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Email = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    Phone = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    BirthDate = table.Column<DateOnly>(type: "date", nullable: true),
                    Note = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Customers", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Employees",
                schema: "aispa",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    DisplayName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Color = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    UserId = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Employees", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Locations",
                schema: "aispa",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    TimeZone = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Currency = table.Column<string>(type: "character(3)", fixedLength: true, maxLength: 3, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Locations", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Qualifications",
                schema: "aispa",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Qualifications", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Treatments",
                schema: "aispa",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    DurationMinutes = table.Column<int>(type: "integer", nullable: false),
                    PreparationMinutes = table.Column<int>(type: "integer", nullable: false),
                    FollowUpMinutes = table.Column<int>(type: "integer", nullable: false),
                    Price = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Treatments", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "EmployeeWorkingHours",
                schema: "aispa",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    EmployeeId = table.Column<Guid>(type: "uuid", nullable: false),
                    LocationId = table.Column<Guid>(type: "uuid", nullable: false),
                    Day = table.Column<int>(type: "integer", nullable: false),
                    Start = table.Column<TimeOnly>(type: "time without time zone", nullable: false),
                    End = table.Column<TimeOnly>(type: "time without time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EmployeeWorkingHours", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EmployeeWorkingHours_Employees_EmployeeId",
                        column: x => x.EmployeeId,
                        principalSchema: "aispa",
                        principalTable: "Employees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_EmployeeWorkingHours_Locations_LocationId",
                        column: x => x.LocationId,
                        principalSchema: "aispa",
                        principalTable: "Locations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Equipment",
                schema: "aispa",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    LocationId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Equipment", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Equipment_Locations_LocationId",
                        column: x => x.LocationId,
                        principalSchema: "aispa",
                        principalTable: "Locations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "LocationOpeningHours",
                schema: "aispa",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    LocationId = table.Column<Guid>(type: "uuid", nullable: false),
                    Day = table.Column<int>(type: "integer", nullable: false),
                    Opens = table.Column<TimeOnly>(type: "time without time zone", nullable: false),
                    Closes = table.Column<TimeOnly>(type: "time without time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LocationOpeningHours", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LocationOpeningHours_Locations_LocationId",
                        column: x => x.LocationId,
                        principalSchema: "aispa",
                        principalTable: "Locations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Rooms",
                schema: "aispa",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    LocationId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    CleaningMinutes = table.Column<int>(type: "integer", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Rooms", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Rooms_Locations_LocationId",
                        column: x => x.LocationId,
                        principalSchema: "aispa",
                        principalTable: "Locations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "EmployeeQualifications",
                schema: "aispa",
                columns: table => new
                {
                    EmployeeId = table.Column<Guid>(type: "uuid", nullable: false),
                    QualificationsId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EmployeeQualifications", x => new { x.EmployeeId, x.QualificationsId });
                    table.ForeignKey(
                        name: "FK_EmployeeQualifications_Employees_EmployeeId",
                        column: x => x.EmployeeId,
                        principalSchema: "aispa",
                        principalTable: "Employees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_EmployeeQualifications_Qualifications_QualificationsId",
                        column: x => x.QualificationsId,
                        principalSchema: "aispa",
                        principalTable: "Qualifications",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "TreatmentQualifications",
                schema: "aispa",
                columns: table => new
                {
                    RequiredQualificationsId = table.Column<Guid>(type: "uuid", nullable: false),
                    TreatmentId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TreatmentQualifications", x => new { x.RequiredQualificationsId, x.TreatmentId });
                    table.ForeignKey(
                        name: "FK_TreatmentQualifications_Qualifications_RequiredQualificatio~",
                        column: x => x.RequiredQualificationsId,
                        principalSchema: "aispa",
                        principalTable: "Qualifications",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_TreatmentQualifications_Treatments_TreatmentId",
                        column: x => x.TreatmentId,
                        principalSchema: "aispa",
                        principalTable: "Treatments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "TreatmentEquipment",
                schema: "aispa",
                columns: table => new
                {
                    EquipmentId = table.Column<Guid>(type: "uuid", nullable: false),
                    TreatmentId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TreatmentEquipment", x => new { x.EquipmentId, x.TreatmentId });
                    table.ForeignKey(
                        name: "FK_TreatmentEquipment_Equipment_EquipmentId",
                        column: x => x.EquipmentId,
                        principalSchema: "aispa",
                        principalTable: "Equipment",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_TreatmentEquipment_Treatments_TreatmentId",
                        column: x => x.TreatmentId,
                        principalSchema: "aispa",
                        principalTable: "Treatments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Appointments",
                schema: "aispa",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    LocationId = table.Column<Guid>(type: "uuid", nullable: false),
                    TreatmentId = table.Column<Guid>(type: "uuid", nullable: false),
                    CustomerId = table.Column<Guid>(type: "uuid", nullable: false),
                    EmployeeId = table.Column<Guid>(type: "uuid", nullable: false),
                    RoomId = table.Column<Guid>(type: "uuid", nullable: false),
                    EquipmentId = table.Column<Guid>(type: "uuid", nullable: true),
                    Start = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    End = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    BlockedFrom = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    BlockedUntil = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    Price = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: false),
                    Note = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    CancelReason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Appointments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Appointments_Customers_CustomerId",
                        column: x => x.CustomerId,
                        principalSchema: "aispa",
                        principalTable: "Customers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Appointments_Employees_EmployeeId",
                        column: x => x.EmployeeId,
                        principalSchema: "aispa",
                        principalTable: "Employees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Appointments_Equipment_EquipmentId",
                        column: x => x.EquipmentId,
                        principalSchema: "aispa",
                        principalTable: "Equipment",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Appointments_Locations_LocationId",
                        column: x => x.LocationId,
                        principalSchema: "aispa",
                        principalTable: "Locations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Appointments_Rooms_RoomId",
                        column: x => x.RoomId,
                        principalSchema: "aispa",
                        principalTable: "Rooms",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Appointments_Treatments_TreatmentId",
                        column: x => x.TreatmentId,
                        principalSchema: "aispa",
                        principalTable: "Treatments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "TreatmentRooms",
                schema: "aispa",
                columns: table => new
                {
                    RoomsId = table.Column<Guid>(type: "uuid", nullable: false),
                    TreatmentId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TreatmentRooms", x => new { x.RoomsId, x.TreatmentId });
                    table.ForeignKey(
                        name: "FK_TreatmentRooms_Rooms_RoomsId",
                        column: x => x.RoomsId,
                        principalSchema: "aispa",
                        principalTable: "Rooms",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_TreatmentRooms_Treatments_TreatmentId",
                        column: x => x.TreatmentId,
                        principalSchema: "aispa",
                        principalTable: "Treatments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ResourceBookings",
                schema: "aispa",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Kind = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    ResourceId = table.Column<Guid>(type: "uuid", nullable: false),
                    From = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Until = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    AppointmentId = table.Column<Guid>(type: "uuid", nullable: true),
                    Reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ResourceBookings", x => x.Id);
                    table.CheckConstraint("CK_ResourceBookings_Period", "\"From\" < \"Until\"");
                    table.ForeignKey(
                        name: "FK_ResourceBookings_Appointments_AppointmentId",
                        column: x => x.AppointmentId,
                        principalSchema: "aispa",
                        principalTable: "Appointments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Appointments_CustomerId",
                schema: "aispa",
                table: "Appointments",
                column: "CustomerId");

            migrationBuilder.CreateIndex(
                name: "IX_Appointments_EmployeeId",
                schema: "aispa",
                table: "Appointments",
                column: "EmployeeId");

            migrationBuilder.CreateIndex(
                name: "IX_Appointments_EquipmentId",
                schema: "aispa",
                table: "Appointments",
                column: "EquipmentId");

            migrationBuilder.CreateIndex(
                name: "IX_Appointments_LocationId_Start",
                schema: "aispa",
                table: "Appointments",
                columns: new[] { "LocationId", "Start" });

            migrationBuilder.CreateIndex(
                name: "IX_Appointments_RoomId",
                schema: "aispa",
                table: "Appointments",
                column: "RoomId");

            migrationBuilder.CreateIndex(
                name: "IX_Appointments_TreatmentId",
                schema: "aispa",
                table: "Appointments",
                column: "TreatmentId");

            migrationBuilder.CreateIndex(
                name: "IX_Customers_Email",
                schema: "aispa",
                table: "Customers",
                column: "Email");

            migrationBuilder.CreateIndex(
                name: "IX_Customers_LastName_FirstName",
                schema: "aispa",
                table: "Customers",
                columns: new[] { "LastName", "FirstName" });

            migrationBuilder.CreateIndex(
                name: "IX_Customers_Phone",
                schema: "aispa",
                table: "Customers",
                column: "Phone");

            migrationBuilder.CreateIndex(
                name: "IX_EmployeeQualifications_QualificationsId",
                schema: "aispa",
                table: "EmployeeQualifications",
                column: "QualificationsId");

            migrationBuilder.CreateIndex(
                name: "IX_EmployeeWorkingHours_EmployeeId",
                schema: "aispa",
                table: "EmployeeWorkingHours",
                column: "EmployeeId");

            migrationBuilder.CreateIndex(
                name: "IX_EmployeeWorkingHours_LocationId_Day",
                schema: "aispa",
                table: "EmployeeWorkingHours",
                columns: new[] { "LocationId", "Day" });

            migrationBuilder.CreateIndex(
                name: "IX_Equipment_LocationId",
                schema: "aispa",
                table: "Equipment",
                column: "LocationId");

            migrationBuilder.CreateIndex(
                name: "IX_LocationOpeningHours_LocationId",
                schema: "aispa",
                table: "LocationOpeningHours",
                column: "LocationId");

            migrationBuilder.CreateIndex(
                name: "IX_Qualifications_Name",
                schema: "aispa",
                table: "Qualifications",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ResourceBookings_AppointmentId",
                schema: "aispa",
                table: "ResourceBookings",
                column: "AppointmentId");

            migrationBuilder.CreateIndex(
                name: "IX_ResourceBookings_ResourceId_From",
                schema: "aispa",
                table: "ResourceBookings",
                columns: new[] { "ResourceId", "From" });

            migrationBuilder.CreateIndex(
                name: "IX_Rooms_LocationId",
                schema: "aispa",
                table: "Rooms",
                column: "LocationId");

            migrationBuilder.CreateIndex(
                name: "IX_TreatmentEquipment_TreatmentId",
                schema: "aispa",
                table: "TreatmentEquipment",
                column: "TreatmentId");

            migrationBuilder.CreateIndex(
                name: "IX_TreatmentQualifications_TreatmentId",
                schema: "aispa",
                table: "TreatmentQualifications",
                column: "TreatmentId");

            migrationBuilder.CreateIndex(
                name: "IX_TreatmentRooms_TreatmentId",
                schema: "aispa",
                table: "TreatmentRooms",
                column: "TreatmentId");

            // Keine zwei Belegungen derselben Ressource dürfen sich überschneiden – auch nicht bei gleichzeitigen Buchungen.
            // Zeiträume sind halboffen [From, Until): ein Termin darf direkt an den nächsten anschließen.
            // DEFERRABLE INITIALLY DEFERRED: geprüft wird beim Commit, damit Verschieben (alte Belegung löschen, neue anlegen)
            // in einem Schritt geht.
            migrationBuilder.Sql("""
                ALTER TABLE aispa."ResourceBookings"
                ADD CONSTRAINT "EX_ResourceBookings_NoOverlap"
                EXCLUDE USING gist ("ResourceId" WITH =, tstzrange("From", "Until") WITH &&)
                DEFERRABLE INITIALLY DEFERRED;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "EmployeeQualifications",
                schema: "aispa");

            migrationBuilder.DropTable(
                name: "EmployeeWorkingHours",
                schema: "aispa");

            migrationBuilder.DropTable(
                name: "LocationOpeningHours",
                schema: "aispa");

            migrationBuilder.DropTable(
                name: "ResourceBookings",
                schema: "aispa");

            migrationBuilder.DropTable(
                name: "TreatmentEquipment",
                schema: "aispa");

            migrationBuilder.DropTable(
                name: "TreatmentQualifications",
                schema: "aispa");

            migrationBuilder.DropTable(
                name: "TreatmentRooms",
                schema: "aispa");

            migrationBuilder.DropTable(
                name: "Appointments",
                schema: "aispa");

            migrationBuilder.DropTable(
                name: "Qualifications",
                schema: "aispa");

            migrationBuilder.DropTable(
                name: "Customers",
                schema: "aispa");

            migrationBuilder.DropTable(
                name: "Employees",
                schema: "aispa");

            migrationBuilder.DropTable(
                name: "Equipment",
                schema: "aispa");

            migrationBuilder.DropTable(
                name: "Rooms",
                schema: "aispa");

            migrationBuilder.DropTable(
                name: "Treatments",
                schema: "aispa");

            migrationBuilder.DropTable(
                name: "Locations",
                schema: "aispa");

            migrationBuilder.AlterDatabase()
                .OldAnnotation("Npgsql:PostgresExtension:btree_gist", ",,");
        }
    }
}
