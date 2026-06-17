using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Blogify.Web.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddCommentModeration : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "ModeratedAt",
                table: "Comments",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ModeratedByUserId",
                table: "Comments",
                type: "TEXT",
                maxLength: 450,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ModerationReason",
                table: "Comments",
                type: "TEXT",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ModerationStatus",
                table: "Comments",
                type: "TEXT",
                maxLength: 50,
                nullable: false,
                defaultValue: "Pending");

            migrationBuilder.CreateIndex(
                name: "IX_Comments_BlogId_ModerationStatus_CreatedAt",
                table: "Comments",
                columns: new[] { "BlogId", "ModerationStatus", "CreatedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Comments_BlogId_ModerationStatus_CreatedAt",
                table: "Comments");

            migrationBuilder.DropColumn(
                name: "ModeratedAt",
                table: "Comments");

            migrationBuilder.DropColumn(
                name: "ModeratedByUserId",
                table: "Comments");

            migrationBuilder.DropColumn(
                name: "ModerationReason",
                table: "Comments");

            migrationBuilder.DropColumn(
                name: "ModerationStatus",
                table: "Comments");
        }
    }
}
