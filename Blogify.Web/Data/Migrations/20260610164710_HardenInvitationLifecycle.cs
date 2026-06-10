using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Blogify.Web.Data.Migrations
{
    /// <inheritdoc />
    public partial class HardenInvitationLifecycle : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_BlogInvitations_BlogId_Email",
                table: "BlogInvitations");

            migrationBuilder.DropIndex(
                name: "IX_BlogInvitations_Token",
                table: "BlogInvitations");

            migrationBuilder.DropColumn(
                name: "Token",
                table: "BlogInvitations");

            migrationBuilder.AddColumn<long>(
                name: "LastSentAtUtc",
                table: "BlogInvitations",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Status",
                table: "BlogInvitations",
                type: "TEXT",
                maxLength: 50,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "TokenHash",
                table: "BlogInvitations",
                type: "TEXT",
                maxLength: 64,
                nullable: false,
                defaultValue: "");

            // Existing bearer tokens are deliberately invalidated. Preserve accepted history,
            // cancel every other legacy invitation, and assign unique non-bearer placeholders.
            migrationBuilder.Sql(
                """
                UPDATE BlogInvitations
                SET TokenHash = lower(hex(randomblob(32))),
                    Status = CASE WHEN AcceptedAtUtc IS NULL THEN 'Cancelled' ELSE 'Accepted' END
                """);

            migrationBuilder.CreateTable(
                name: "BlogInvitationEvents",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    InvitationId = table.Column<Guid>(type: "TEXT", nullable: false),
                    EventType = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    ActorUserId = table.Column<string>(type: "TEXT", maxLength: 450, nullable: true),
                    Details = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    CreatedAtUtc = table.Column<long>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BlogInvitationEvents", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_BlogInvitations_BlogId_Email",
                table: "BlogInvitations",
                columns: new[] { "BlogId", "Email" },
                unique: true,
                filter: "\"Status\" = 'Pending'");

            migrationBuilder.CreateIndex(
                name: "IX_BlogInvitations_TokenHash",
                table: "BlogInvitations",
                column: "TokenHash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_BlogInvitationEvents_InvitationId",
                table: "BlogInvitationEvents",
                column: "InvitationId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "BlogInvitationEvents");

            migrationBuilder.DropIndex(
                name: "IX_BlogInvitations_BlogId_Email",
                table: "BlogInvitations");

            migrationBuilder.DropIndex(
                name: "IX_BlogInvitations_TokenHash",
                table: "BlogInvitations");

            migrationBuilder.DropColumn(
                name: "LastSentAtUtc",
                table: "BlogInvitations");

            migrationBuilder.DropColumn(
                name: "Status",
                table: "BlogInvitations");

            migrationBuilder.DropColumn(
                name: "TokenHash",
                table: "BlogInvitations");

            migrationBuilder.AddColumn<string>(
                name: "Token",
                table: "BlogInvitations",
                type: "TEXT",
                maxLength: 128,
                nullable: false,
                defaultValue: "");

            migrationBuilder.Sql(
                "UPDATE BlogInvitations SET Token = lower(hex(randomblob(32)))");

            migrationBuilder.CreateIndex(
                name: "IX_BlogInvitations_BlogId_Email",
                table: "BlogInvitations",
                columns: new[] { "BlogId", "Email" });

            migrationBuilder.CreateIndex(
                name: "IX_BlogInvitations_Token",
                table: "BlogInvitations",
                column: "Token",
                unique: true);
        }
    }
}
