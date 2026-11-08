using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace AispaStudio.Api.System.KeyStoreMigrations
{
    /// <inheritdoc />
    public partial class InitialKeyStore : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "system");

            migrationBuilder.CreateTable(
                name: "DataProtectionKeys",
                schema: "system",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    FriendlyName = table.Column<string>(type: "text", nullable: true),
                    Xml = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DataProtectionKeys", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DataProtectionKeys_FriendlyName",
                schema: "system",
                table: "DataProtectionKeys",
                column: "FriendlyName",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DataProtectionKeys",
                schema: "system");
        }
    }
}
