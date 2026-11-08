using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AispaStudio.Api.Online.Migrations
{
    /// <inheritdoc />
    public partial class Anamnesewege : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AnamnesisScans",
                schema: "health",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CustomerId = table.Column<Guid>(type: "uuid", nullable: false),
                    AppointmentId = table.Column<Guid>(type: "uuid", nullable: true),
                    UploadedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UploadedBy = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    ContentType = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Language = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: false),
                    Image = table.Column<byte[]>(type: "bytea", nullable: false),
                    Status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    Reader = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    Reading = table.Column<byte[]>(type: "bytea", nullable: true),
                    ReadAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    RecordId = table.Column<Guid>(type: "uuid", nullable: true),
                    ConfirmedBy = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AnamnesisScans", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AnamnesisScans_CustomerId_Status",
                schema: "health",
                table: "AnamnesisScans",
                columns: new[] { "CustomerId", "Status" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AnamnesisScans",
                schema: "health");
        }
    }
}
