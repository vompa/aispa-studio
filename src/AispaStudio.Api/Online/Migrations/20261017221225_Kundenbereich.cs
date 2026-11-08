using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace AispaStudio.Api.Online.Migrations
{
    /// <inheritdoc />
    public partial class Kundenbereich : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "online");

            migrationBuilder.EnsureSchema(
                name: "health");

            migrationBuilder.CreateTable(
                name: "AccessLinks",
                schema: "online",
                columns: table => new
                {
                    TokenHash = table.Column<byte[]>(type: "bytea", maxLength: 32, nullable: false),
                    Purpose = table.Column<int>(type: "integer", nullable: false),
                    CustomerId = table.Column<Guid>(type: "uuid", nullable: false),
                    AppointmentId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ExpiresAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AccessLinks", x => x.TokenHash);
                });

            migrationBuilder.CreateTable(
                name: "AnamnesisRecords",
                schema: "health",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CustomerId = table.Column<Guid>(type: "uuid", nullable: false),
                    AppointmentId = table.Column<Guid>(type: "uuid", nullable: true),
                    FormVersion = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Channel = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    SubmittedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ConsentGivenAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Payload = table.Column<byte[]>(type: "bytea", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AnamnesisRecords", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CustomerAccounts",
                schema: "online",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CustomerId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UserName = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    NormalizedUserName = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    Email = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    NormalizedEmail = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    EmailConfirmed = table.Column<bool>(type: "boolean", nullable: false),
                    PasswordHash = table.Column<string>(type: "text", nullable: true),
                    SecurityStamp = table.Column<string>(type: "text", nullable: true),
                    ConcurrencyStamp = table.Column<string>(type: "text", nullable: true),
                    PhoneNumber = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    PhoneNumberConfirmed = table.Column<bool>(type: "boolean", nullable: false),
                    TwoFactorEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    LockoutEnd = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    LockoutEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    AccessFailedCount = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CustomerAccounts", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "OnlineBookings",
                schema: "online",
                columns: table => new
                {
                    AppointmentId = table.Column<Guid>(type: "uuid", nullable: false),
                    CustomerId = table.Column<Guid>(type: "uuid", nullable: false),
                    CustomerAccountId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    PrivacyAcceptedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    PrivacyNoticeVersion = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Language = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: false),
                    CancelledAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OnlineBookings", x => x.AppointmentId);
                });

            migrationBuilder.CreateTable(
                name: "SignInCodes",
                schema: "online",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    NormalizedEmail = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    CodeHash = table.Column<byte[]>(type: "bytea", maxLength: 32, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ExpiresAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    FailedAttempts = table.Column<int>(type: "integer", nullable: false),
                    UsedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SignInCodes", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CustomerAccountClaims",
                schema: "online",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    ClaimType = table.Column<string>(type: "text", nullable: true),
                    ClaimValue = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CustomerAccountClaims", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CustomerAccountClaims_CustomerAccounts_UserId",
                        column: x => x.UserId,
                        principalSchema: "online",
                        principalTable: "CustomerAccounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CustomerAccountLogins",
                schema: "online",
                columns: table => new
                {
                    LoginProvider = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    ProviderKey = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    ProviderDisplayName = table.Column<string>(type: "text", nullable: true),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CustomerAccountLogins", x => new { x.LoginProvider, x.ProviderKey });
                    table.ForeignKey(
                        name: "FK_CustomerAccountLogins_CustomerAccounts_UserId",
                        column: x => x.UserId,
                        principalSchema: "online",
                        principalTable: "CustomerAccounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CustomerAccountPasskeys",
                schema: "online",
                columns: table => new
                {
                    CredentialId = table.Column<byte[]>(type: "bytea", maxLength: 1024, nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Data = table.Column<string>(type: "jsonb", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CustomerAccountPasskeys", x => x.CredentialId);
                    table.ForeignKey(
                        name: "FK_CustomerAccountPasskeys_CustomerAccounts_UserId",
                        column: x => x.UserId,
                        principalSchema: "online",
                        principalTable: "CustomerAccounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CustomerAccountTokens",
                schema: "online",
                columns: table => new
                {
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    LoginProvider = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    Name = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    Value = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CustomerAccountTokens", x => new { x.UserId, x.LoginProvider, x.Name });
                    table.ForeignKey(
                        name: "FK_CustomerAccountTokens_CustomerAccounts_UserId",
                        column: x => x.UserId,
                        principalSchema: "online",
                        principalTable: "CustomerAccounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AccessLinks_AppointmentId",
                schema: "online",
                table: "AccessLinks",
                column: "AppointmentId");

            migrationBuilder.CreateIndex(
                name: "IX_AccessLinks_ExpiresAt",
                schema: "online",
                table: "AccessLinks",
                column: "ExpiresAt");

            migrationBuilder.CreateIndex(
                name: "IX_AnamnesisRecords_CustomerId_SubmittedAt",
                schema: "health",
                table: "AnamnesisRecords",
                columns: new[] { "CustomerId", "SubmittedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_CustomerAccountClaims_UserId",
                schema: "online",
                table: "CustomerAccountClaims",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_CustomerAccountLogins_UserId",
                schema: "online",
                table: "CustomerAccountLogins",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_CustomerAccountPasskeys_UserId",
                schema: "online",
                table: "CustomerAccountPasskeys",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "EmailIndex",
                schema: "online",
                table: "CustomerAccounts",
                column: "NormalizedEmail");

            migrationBuilder.CreateIndex(
                name: "IX_CustomerAccounts_CustomerId",
                schema: "online",
                table: "CustomerAccounts",
                column: "CustomerId");

            migrationBuilder.CreateIndex(
                name: "UserNameIndex",
                schema: "online",
                table: "CustomerAccounts",
                column: "NormalizedUserName",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_OnlineBookings_CustomerId",
                schema: "online",
                table: "OnlineBookings",
                column: "CustomerId");

            migrationBuilder.CreateIndex(
                name: "IX_SignInCodes_NormalizedEmail_CreatedAt",
                schema: "online",
                table: "SignInCodes",
                columns: new[] { "NormalizedEmail", "CreatedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AccessLinks",
                schema: "online");

            migrationBuilder.DropTable(
                name: "AnamnesisRecords",
                schema: "health");

            migrationBuilder.DropTable(
                name: "CustomerAccountClaims",
                schema: "online");

            migrationBuilder.DropTable(
                name: "CustomerAccountLogins",
                schema: "online");

            migrationBuilder.DropTable(
                name: "CustomerAccountPasskeys",
                schema: "online");

            migrationBuilder.DropTable(
                name: "CustomerAccountTokens",
                schema: "online");

            migrationBuilder.DropTable(
                name: "OnlineBookings",
                schema: "online");

            migrationBuilder.DropTable(
                name: "SignInCodes",
                schema: "online");

            migrationBuilder.DropTable(
                name: "CustomerAccounts",
                schema: "online");
        }
    }
}
