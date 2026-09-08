using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PedeLocal.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddDeviceTokensRemoveUserFcmToken : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "device_tokens",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Token = table.Column<string>(type: "character varying(4096)", maxLength: 4096, nullable: false),
                    Platform = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_device_tokens", x => x.Id);
                    table.ForeignKey(
                        name: "FK_device_tokens_users_UserId",
                        column: x => x.UserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_device_tokens_Token",
                table: "device_tokens",
                column: "Token",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_device_tokens_UserId_IsActive",
                table: "device_tokens",
                columns: new[] { "UserId", "IsActive" });

            // Preserve any FCM token already registered under the old single-token-per-user model
            // (docs/PUSH_NOTIFICATIONS.md) instead of silently discarding it — Platform is unknown
            // for these pre-existing rows since it was never captured before.
            migrationBuilder.Sql("""
                INSERT INTO device_tokens ("Id", "UserId", "Token", "Platform", "IsActive", "CreatedAtUtc", "UpdatedAtUtc")
                SELECT gen_random_uuid(), "Id", "FcmToken", 'Unknown', true, now(), now()
                FROM users
                WHERE "FcmToken" IS NOT NULL AND "FcmToken" <> ''
                ON CONFLICT ("Token") DO NOTHING;
                """);

            migrationBuilder.DropColumn(
                name: "FcmToken",
                table: "users");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "device_tokens");

            migrationBuilder.AddColumn<string>(
                name: "FcmToken",
                table: "users",
                type: "character varying(512)",
                maxLength: 512,
                nullable: true);
        }
    }
}
