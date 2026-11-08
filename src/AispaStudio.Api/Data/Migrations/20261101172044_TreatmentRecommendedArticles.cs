using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AispaStudio.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class TreatmentRecommendedArticles : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "TreatmentRecommendedArticles",
                schema: "aispa",
                columns: table => new
                {
                    TreatmentId = table.Column<Guid>(type: "uuid", nullable: false),
                    ArticleId = table.Column<Guid>(type: "uuid", nullable: false),
                    Position = table.Column<int>(type: "integer", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TreatmentRecommendedArticles", x => new { x.TreatmentId, x.ArticleId });
                    table.ForeignKey(
                        name: "FK_TreatmentRecommendedArticles_Articles_ArticleId",
                        column: x => x.ArticleId,
                        principalSchema: "aispa",
                        principalTable: "Articles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_TreatmentRecommendedArticles_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalSchema: "aispa",
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TreatmentRecommendedArticles_Treatments_TreatmentId",
                        column: x => x.TreatmentId,
                        principalSchema: "aispa",
                        principalTable: "Treatments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TreatmentRecommendedArticles_ArticleId",
                schema: "aispa",
                table: "TreatmentRecommendedArticles",
                column: "ArticleId");

            migrationBuilder.CreateIndex(
                name: "IX_TreatmentRecommendedArticles_TenantId",
                schema: "aispa",
                table: "TreatmentRecommendedArticles",
                column: "TenantId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TreatmentRecommendedArticles",
                schema: "aispa");
        }
    }
}
