using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AispaStudio.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class TenantEmbedAllowedOrigins : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<List<string>>(
                name: "EmbedAllowedOrigins",
                schema: "aispa",
                table: "Tenants",
                type: "text[]",
                nullable: false,
                defaultValueSql: "'{}'");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "EmbedAllowedOrigins",
                schema: "aispa",
                table: "Tenants");
        }
    }
}
